#!/usr/bin/env python3
"""Decode bounded, hash-verified HappyToy UTF evidence from NUnit XML or a UBA log.

This verifies transport integrity only. It does not turn screenshots or DSP samples
into manual gameplay, listening or target-hardware performance certification.
"""
import argparse
import base64
import hashlib
import json
from pathlib import Path
import re
import xml.etree.ElementTree as ET

NAME = re.compile(r"^[A-Za-z0-9_-]+\.(?:png|wav|json)$")
MAX_FILE = 1_500_000
# The six player-feedback fixes add real cabinet, corner and room camera views.
# Keep a hard aggregate cap; this is transport capacity, never a test pass gate.
MAX_TOTAL = 16_000_000
MAX_FILES = 32


def decode(text):
    records = {}
    for line in text.splitlines():
        marker = line.find('HAPPYTOY_ARTIFACT_')
        if marker < 0:
            continue
        tokens = line[marker:].strip().split()
        kind = tokens[0]
        if kind == 'HAPPYTOY_ARTIFACT_BEGIN':
            if len(tokens) != 5:
                raise ValueError('Malformed artifact header')
            _, name, length, sha, chunks = tokens
            if (not NAME.fullmatch(name) or name.lower() == 'transport-manifest.json'
                    or not re.fullmatch('[0-9a-f]{64}', sha)):
                raise ValueError('Unsafe artifact name or digest')
            length, chunks = int(length), int(chunks)
            if not 0 < length <= MAX_FILE or not 0 < chunks <= (MAX_FILE * 4 // 3 + 4095) // 4096:
                raise ValueError('Artifact exceeds bounded envelope')
            expected = (length, sha, chunks)
            if name in records:
                if records[name]['header'] != expected:
                    raise ValueError('Conflicting artifact header')
            else:
                if len(records) >= MAX_FILES:
                    raise ValueError('Too many artifacts')
                records[name] = {'header': expected, 'chunks': {}, 'ended': False}
        elif kind == 'HAPPYTOY_ARTIFACT_CHUNK':
            if len(tokens) != 4 or tokens[1] not in records:
                raise ValueError('Chunk without complete header')
            _, name, index, payload = tokens
            record = records[name]
            index = int(index)
            if not 0 <= index < record['header'][2] or len(payload) > 4096:
                raise ValueError('Chunk outside declared bounds')
            if index in record['chunks'] and record['chunks'][index] != payload:
                raise ValueError('Conflicting duplicate chunk')
            record['chunks'][index] = payload
        elif kind == 'HAPPYTOY_ARTIFACT_END':
            if len(tokens) != 2 or tokens[1] not in records:
                raise ValueError('End without header')
            records[tokens[1]]['ended'] = True
        else:
            raise ValueError('Unknown artifact marker')
    if not records:
        raise ValueError('No HappyToy artifact envelopes found')
    output = {}
    total = 0
    for name, record in records.items():
        length, sha, chunks = record['header']
        if not record['ended'] or len(record['chunks']) != chunks:
            raise ValueError('Incomplete artifact: ' + name)
        payload = ''.join(record['chunks'][i] for i in range(chunks))
        data = base64.b64decode(payload, validate=True)
        if len(data) != length or hashlib.sha256(data).hexdigest() != sha:
            raise ValueError('Artifact length/hash mismatch: ' + name)
        if name.endswith('.png') and not data.startswith(b'\x89PNG\r\n\x1a\n'):
            raise ValueError('Invalid PNG signature')
        if name.endswith('.wav') and (data[:4] != b'RIFF' or data[8:12] != b'WAVE'):
            raise ValueError('Invalid WAV signature')
        if name.endswith('.json'):
            json.loads(data)
        total += length
        if total > MAX_TOTAL:
            raise ValueError('Total artifacts exceed bounded envelope')
        output[name] = data
    return output


def read_input(path):
    data = path.read_bytes()
    text = data.decode('utf-8-sig')
    if path.suffix.lower() == '.xml':
        root = ET.fromstring(text)
        text = '\n'.join(element.text or '' for element in root.iter('output'))
    return data, text


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('input', type=Path)
    parser.add_argument('--output', required=True, type=Path, help='A new directory, outside the Unity project')
    parser.add_argument('--build', required=True, type=int)
    parser.add_argument('--commit', required=True, help='Exact independently verified 40-character UBA checkout revision')
    args = parser.parse_args()
    if args.build < 1 or not re.fullmatch('[0-9a-f]{40}', args.commit):
        parser.error('A positive build number and full lowercase commit SHA are required')
    project = Path(__file__).resolve().parents[2]
    output = args.output.resolve()
    if output == project or project in output.parents:
        parser.error('Evidence outputs must remain outside the Unity project')
    original, text = read_input(args.input)
    artifacts = decode(text)  # Fully validate before writing any artifact.
    output.mkdir(parents=True, exist_ok=False)
    for name, data in artifacts.items():
        with (output / name).open('xb') as stream:
            stream.write(data)
    manifest = {
        'scope': 'Hash-verified real-engine output transport; not manual, hardware-performance or device-listening certification',
        'build': args.build, 'commit': args.commit,
        'provenanceNote': 'Build and commit supplied from independently inspected UBA checkout; envelope alone does not prove the revision',
        'input': args.input.name, 'inputSha256': hashlib.sha256(original).hexdigest(),
        'artifacts': [{'filename': name, 'bytes': len(data), 'sha256': hashlib.sha256(data).hexdigest()} for name, data in artifacts.items()],
        'metrics': [line.strip() for line in text.splitlines() if 'HAPPYTOY_' in line and '_METRICS' in line],
    }
    (output / 'transport-manifest.json').write_text(json.dumps(manifest, indent=2) + '\n')
    print(json.dumps(manifest, indent=2))


if __name__ == '__main__':
    main()
