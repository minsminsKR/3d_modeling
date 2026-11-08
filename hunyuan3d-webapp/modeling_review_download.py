"""Preserve an explicitly unapproved review-required GLB without fabricating a ZIP.

Server ETags bind a transfer to one snapshot but are not content hashes. The
receipt distinguishes a local-only SHA-256 from a matching server artifact hash.
"""
from __future__ import annotations

import contextlib
import hashlib
import json
import os
import re
from pathlib import Path
import struct
import time

try:
    import requests
except ImportError:
    requests = None

_REQUEST_TIMEOUT = requests.Timeout if requests else ()
_REQUEST_ERROR = requests.RequestException if requests else ()

from modeling_download import (CHUNK_BYTES, CHECKPOINT_BYTES, CHECKPOINT_SECONDS,
    _RANGE, _drop, _integer, _length, _load_partial, _open, _regular, _strong,
    download_lock)


def _glb(path, size, error):
    """Check GLB v2 framing and JSON without claiming semantic/visual quality."""
    try:
        with path.open('rb') as stream:
            header = stream.read(12)
            if len(header) != 12:
                raise ValueError('truncated GLB header')
            magic, version, declared = struct.unpack('<4sII', header)
            if magic != b'glTF' or version != 2 or declared != size:
                raise ValueError('invalid GLB header')
            index = 12
            first = True
            while index < size:
                chunk_header = stream.read(8)
                if len(chunk_header) != 8:
                    raise ValueError('truncated GLB chunk header')
                length, kind = struct.unpack('<I4s', chunk_header)
                index += 8
                if length % 4 or length > size - index:
                    raise ValueError('invalid GLB chunk length')
                if first:
                    if kind != b'JSON' or not 0 < length <= 32 * 1024 * 1024:
                        raise ValueError('invalid GLB JSON chunk')
                    value = json.loads(stream.read(length).decode('utf-8'))
                    if (not isinstance(value, dict) or not isinstance(value.get('asset'), dict) or
                        value['asset'].get('version') != '2.0'):
                        raise ValueError('missing or unsupported glTF asset metadata')
                    first = False
                else:
                    stream.seek(length, 1)
                index += length
            if first or index != size:
                raise ValueError('invalid GLB structure')
    except (OSError, ValueError, UnicodeError, struct.error) as exc:
        raise error('Downloaded review result is not a structurally valid GLB.', code='INTEGRITY_ERROR') from exc


def transfer_review(client, job_id, output, record, *, atomic_json, error):
    """Collect only a review-required raw GLB; never approve or resubmit a job."""
    if not isinstance(record, dict) or record.get('id') != job_id or record.get('status') != 'review_required':
        raise error('Only the exact review_required job can be collected by download-review.', code='JOB_NOT_READY')
    artifacts = record.get('artifacts', [])
    if not isinstance(artifacts, list):
        raise error('Review generation record has invalid artifacts.', code='PROTOCOL_ERROR')
    matching = [entry for entry in artifacts if isinstance(entry, dict) and entry.get('name') == 'model.glb']
    if len(matching) > 1:
        raise error('Review record has duplicate model artifacts.', code='PROTOCOL_ERROR')
    server_sha = None
    server_size = None
    if matching:
        server_sha, server_size = matching[0].get('sha256'), matching[0].get('size')
        if not isinstance(server_sha, str) or not re.fullmatch(r'[a-f0-9]{64}', server_sha) or not _integer(server_size):
            raise error('Review model server hash or size is invalid.', code='PROTOCOL_ERROR')
    output = Path(output).expanduser().resolve()
    endpoint = '/jobs/' + job_id + '/model'
    final = output / (job_id + '.review.glb')
    receipt_path = output / (job_id + '.review.json')
    part = output / ('.' + job_id + '.review.glb.part')
    sidecar = output / ('.' + job_id + '.review.glb.part.json')
    identity = {'server': client.server, 'job_id': job_id, 'url': client.url(endpoint)}
    with download_lock(output, job_id + '-review', error, client.timeout):
        for path in (final, receipt_path, part, sidecar):
            _regular(path, error)
        with client.request('HEAD', endpoint, headers={'Accept-Encoding': 'identity'}) as head:
            client.check(head)
            if head.status_code != 200:
                raise error('Review model HEAD must return HTTP 200.', code='PROTOCOL_ERROR')
            etag, size = _strong(head.headers.get('ETag')), _length(head, error)
            encoding = head.headers.get('Content-Encoding', 'identity').strip().lower()
            if not etag or not _integer(size) or size < 20 or encoding not in {'', 'identity'}:
                raise error('Review model requires a strong ETag, size and identity encoding.', code='PROTOCOL_ERROR')
            if server_sha and size != server_size:
                raise error('Review model size differs from the server record.', code='INTEGRITY_ERROR')
        if final.exists() or receipt_path.exists():
            # Never replace a prior file; a receipt verified against all bytes
            # and the current server version can be reused without a new GET.
            try:
                if not final.exists() or not receipt_path.exists() or receipt_path.stat().st_size > 4 * 1024 * 1024:
                    raise ValueError('incomplete receipt')
                old = json.loads(receipt_path.read_text(encoding='utf-8'))
                digest = hashlib.sha256()
                with final.open('rb') as stream:
                    for chunk in iter(lambda: stream.read(CHUNK_BYTES), b''):
                        digest.update(chunk)
                if (old.get('server') != client.server or old.get('job_id') != job_id or
                    old.get('request_id') != record.get('request_id') or old.get('etag') != etag or old.get('size_bytes') != size or
                    old.get('sha256') != digest.hexdigest() or old.get('quality_status') != 'review_required' or
                    old.get('server_sha256_available') is not bool(server_sha) or
                    (server_sha and server_sha != digest.hexdigest()) or final.stat().st_size != size):
                    raise ValueError('receipt mismatch')
                _glb(final, size, error)
                return {'path': str(final), 'receipt': str(receipt_path), 'sha256': digest.hexdigest(),
                        'size_bytes': size, 'etag': etag, 'resumed_bytes': 0, 'received_bytes': 0,
                        'server_sha256_available': bool(server_sha), 'quality_status': 'review_required', 'skipped': True}
            except (OSError, ValueError, TypeError, KeyError, json.JSONDecodeError):
                raise error('Existing review file or receipt differs; preserve it and select a new output directory.', code='LOCAL_IO_ERROR')
        prior = _load_partial(part, sidecar, identity, {'etag': etag, 'size': size}, error)
        data, digest = prior if prior else (None, hashlib.sha256())
        offset = data['checkpoint_bytes'] if data else 0
        resumed = offset
        count = durable = offset
        received = 0
        started = time.monotonic()

        def checkpoint(handle):
            nonlocal durable
            handle.flush(); os.fsync(handle.fileno())
            atomic_json(sidecar, {'schema_version': 1, **identity, 'etag': etag, 'size': size,
                                  'checkpoint_bytes': count, 'prefix_sha256': digest.hexdigest()})
            durable = count

        try:
            if offset < size:
                headers = {'Accept-Encoding': 'identity', 'If-Match': etag}
                if offset:
                    headers.update({'Range': 'bytes=' + str(offset) + '-', 'If-Range': etag})
                with client.request('GET', endpoint, stream=True, headers=headers) as response:
                    client.check(response)
                    response_tag = _strong(response.headers.get('ETag'))
                    length = _length(response, error)
                    encoding = response.headers.get('Content-Encoding', 'identity').strip().lower()
                    content_type = response.headers.get('Content-Type', '').lower()
                    if response_tag != etag or encoding not in {'', 'identity'} or 'text/html' in content_type:
                        raise error('Review model response changed or is not a raw model.', code='PROTOCOL_ERROR')
                    if response.status_code == 206:
                        match = _RANGE.fullmatch(response.headers.get('Content-Range', ''))
                        if not offset or not match:
                            raise error('Unexpected review model range.', code='PROTOCOL_ERROR')
                        first, last, total = map(int, match.groups())
                        if first != offset or last != size - 1 or total != size or length != size - offset:
                            raise error('Review model range differs from its checkpoint.', code='PROTOCOL_ERROR')
                        mode = 'r+b'
                    elif response.status_code == 200:
                        if length != size:
                            raise error('Review model size differs from HEAD.', code='PROTOCOL_ERROR')
                        # A server may ignore Range. Never append a full body.
                        _drop(part, sidecar, error)
                        digest = hashlib.sha256(); offset = resumed = count = durable = 0
                        mode = 'wb'
                    else:
                        raise error('Review model GET must return HTTP 200 or 206.', code='PROTOCOL_ERROR')
                    with _open(part, mode, error) as handle:
                        handle.seek(offset); checkpoint(handle); last_checkpoint = time.monotonic()
                        try:
                            for chunk in response.iter_content(CHUNK_BYTES):
                                if not chunk: continue
                                if count + len(chunk) > size:
                                    raise error('Review model exceeded declared size.', code='PROTOCOL_ERROR')
                                handle.write(chunk); digest.update(chunk); count += len(chunk); received += len(chunk)
                                if count - durable >= CHECKPOINT_BYTES or time.monotonic() - last_checkpoint >= CHECKPOINT_SECONDS:
                                    checkpoint(handle); last_checkpoint = time.monotonic()
                        except BaseException:
                            checkpoint(handle)
                            raise
                        checkpoint(handle)
            if count != size:
                raise error('Review model ended early; rerun download-review to resume.', code='NETWORK_ERROR')
            if server_sha and digest.hexdigest() != server_sha:
                raise error('Review model SHA-256 differs from the server record.', code='INTEGRITY_ERROR')
            _glb(part, size, error)
            # Catch a snapshot change after the body was served; the ETag is a
            # version identifier, not an independent cryptographic checksum.
            with client.request('HEAD', endpoint, headers={'Accept-Encoding': 'identity'}) as head:
                client.check(head)
                if head.status_code != 200 or _strong(head.headers.get('ETag')) != etag or _length(head, error) != size:
                    raise error('Review model changed during transfer; preserve the partial and retry.', code='INTEGRITY_ERROR')
            for path in (final, receipt_path):
                _regular(path, error)
                if path.exists():
                    raise error('Review destination appeared during transfer; select a new output directory.', code='LOCAL_IO_ERROR')
            # Hard-link publication is exclusive and on the same filesystem;
            # unlike replace it cannot overwrite an existing user file.
            os.link(part, final)
            receipt = {'schema_version': 1, 'server': client.server, 'job_id': job_id,
                       'request_id': record.get('request_id'), 'quality_status': 'review_required',
                       'record': record, 'path': str(final), 'etag': etag, 'size_bytes': size,
                       'sha256': digest.hexdigest(), 'server_sha256_available': bool(server_sha),
                       'sha256_scope': ('matched server artifact SHA-256' if server_sha else 'local transfer hash; server did not provide an artifact SHA-256'),
                       'transfer_seconds': round(time.monotonic() - started, 6)}
            atomic_json(receipt_path, receipt)
            part.unlink()
            with contextlib.suppress(FileNotFoundError): sidecar.unlink()
            return {'path': str(final), 'receipt': str(receipt_path), 'sha256': receipt['sha256'],
                    'size_bytes': size, 'etag': etag, 'resumed_bytes': resumed, 'received_bytes': received,
                    'server_sha256_available': bool(server_sha), 'quality_status': 'review_required', 'skipped': False}
        except _REQUEST_TIMEOUT as exc:
            raise error('Review download timed out; rerun download-review.', code='REQUEST_TIMEOUT',
                        details={'partial_bytes': durable, 'resume_available': durable > 0}) from exc
        except _REQUEST_ERROR as exc:
            raise error('Review download interrupted; rerun download-review.', code='NETWORK_ERROR',
                        details={'partial_bytes': durable, 'resume_available': durable > 0}) from exc
        except OSError as exc:
            raise error('Cannot write review model; preserve existing files and check the output directory.',
                        code='LOCAL_IO_ERROR', details={'partial_bytes': durable, 'resume_available': durable > 0}) from exc
