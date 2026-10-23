"""Preserve verified CLI furniture archives as portable editable source files.

Run from the repository root after collect/resume has produced final ZIP receipts.
The ZIP caches stay in Builds; no authentication or private CLI state is copied.
"""
from pathlib import Path, PurePosixPath
import hashlib
import json
import shutil
import zipfile

SOURCE = Path(__file__).resolve().parent
PROJECT = SOURCE.parents[1]
CACHE = PROJECT / 'Builds/SchoolRenewalDownloads/furniture'
KEYS = ('school-desk', 'school-chair', 'archive-shelf', 'metal-cabinet')


def sha(path):
    digest = hashlib.sha256()
    with path.open('rb') as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b''):
            digest.update(block)
    return digest.hexdigest()


def collect():
    jobs = []
    for relative, keys in (('furniture-submission.json', KEYS),
                           ('timber-cabinet/submission.json', ('timber-cabinet',))):
        path = SOURCE / relative
        if path.is_file():
            submission = json.loads(path.read_text(encoding='utf-8-sig'))
            jobs.extend((job, keys[int(job['batch_index']) - 1]) for job in submission['jobs'])
    results = []
    for job, key in jobs:
        folder = SOURCE / key
        archive = CACHE / (job['id'] + '.zip')
        receipt_path = CACHE / (job['id'] + '.download.json')
        if not archive.is_file() or not receipt_path.is_file():
            results.append(dict(key=key, status='pending'))
            continue
        receipt = json.loads(receipt_path.read_text(encoding='utf-8'))
        if sha(archive) != receipt['archive_sha256']:
            raise RuntimeError('Verified CLI archive hash changed: ' + job['id'])
        with zipfile.ZipFile(archive) as package:
            for member in package.infolist():
                name = PurePosixPath(member.filename)
                if member.is_dir():
                    continue
                if name.is_absolute() or '..' in name.parts or ':' in member.filename or '\\' in member.filename:
                    raise RuntimeError('Unsafe archive source path: ' + member.filename)
                if member.filename == 'model.glb':
                    relative = Path('original.glb')
                elif member.filename == 'inputs/01_reference.png':
                    relative = Path('reference.png')
                elif member.filename == 'processed_inputs/01.png':
                    relative = Path('processed-reference.png')
                elif member.filename in ('metadata.json', 'generation-record.json'):
                    relative = Path(member.filename)
                else:
                    relative = Path('generation') / Path(*name.parts)
                target = folder / relative
                target.parent.mkdir(parents=True, exist_ok=True)
                if target.is_file():
                    with package.open(member) as incoming:
                        expected = hashlib.sha256(incoming.read()).hexdigest()
                    if sha(target) == expected:
                        continue
                with package.open(member) as incoming, target.open('wb') as outgoing:
                    shutil.copyfileobj(incoming, outgoing, length=1024 * 1024)
        # Archive receipt carries server/content hashes, no personal cookie state.
        receipt['archive'] = archive.name
        (folder / 'download-receipt.json').write_text(json.dumps(receipt, indent=2) + '\n', encoding='utf-8')
        settings = json.loads((folder / 'generation-settings.json').read_text(encoding='utf-8'))
        provenance = dict(key=key, requestId=job['request_id'], jobId=job['id'],
            server=settings['model']['service'], webUrl=job['web_url'],
            archiveSha256=receipt['archive_sha256'], archiveBytes=receipt['archive_bytes'],
            original='original.glb', originalSha256=sha(folder / 'original.glb'),
            reference='reference.png', referenceSha256=sha(folder / 'reference.png'),
            referencePrompt='reference-prompt.txt', referencePromptSha256=sha(folder / 'reference-prompt.txt'),
            referenceTool='built-in image_gen', settings='generation-settings.json',
            sourceRecord='generation-record.json', sourceRecordSha256=sha(folder / 'generation-record.json'),
            downloadReceipt='download-receipt.json', visualReview='web-review.json',
            archivePathMapping={'model.glb': 'original.glb', 'inputs/01_reference.png': 'reference.png',
                'processed_inputs/01.png': 'processed-reference.png', 'metadata.json': 'metadata.json',
                'generation-record.json': 'generation-record.json', '*': 'generation/*'},
            license='New project-generated reference and model; no third-party stock model used.',
            licenseNotice='../LICENSE.md', generatorLicense='../TRELLIS.2-LICENSE.txt')
        (folder / 'provenance.json').write_text(json.dumps(provenance, indent=2) + '\n', encoding='utf-8')
        results.append(dict(key=key, status='preserved', originalSha256=sha(folder / 'original.glb')))
    print(json.dumps(results, indent=2))


if __name__ == '__main__':
    collect()
