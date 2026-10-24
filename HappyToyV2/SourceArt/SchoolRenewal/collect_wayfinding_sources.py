"""Copy verified CLI archives for the school radiator and projecting sign.

Run from the repository root after the preserved requests finish via resume.
Only public asset inputs/results and provenance are copied, never CLI auth state.
"""
from pathlib import Path, PurePosixPath
import hashlib
import json
import shutil
import zipfile

SOURCE = Path(__file__).resolve().parent
PROJECT = SOURCE.parents[1]
CACHE = PROJECT / 'Builds/SchoolWayfindingDownloads'
JOBS = {'radiator': 'b1e29407-07b5-42ff-993f-9d87d1207e31',
        'room-sign': 'fcac9c34-bece-4450-b42a-9a2dfd921e4e'}


def sha(path):
    with path.open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def collect():
    results = []
    for key, job_id in JOBS.items():
        folder = SOURCE / key
        archive = CACHE / key / (job_id + '.zip')
        receipt_path = archive.with_suffix('.download.json')
        if not archive.is_file() or not receipt_path.is_file():
            results.append(dict(key=key, status='pending'))
            continue
        receipt = json.loads(receipt_path.read_text(encoding='utf-8-sig'))
        if sha(archive) != receipt['archive_sha256']:
            raise RuntimeError('Verified CLI archive changed: ' + job_id)
        mappings = {'model.glb': 'original.glb', 'inputs/01_reference.png': 'reference.png',
                    'processed_inputs/01.png': 'processed-reference.png',
                    'metadata.json': 'metadata.json', 'generation-record.json': 'generation-record.json'}
        with zipfile.ZipFile(archive) as package:
            for member in package.infolist():
                if member.is_dir():
                    continue
                name = PurePosixPath(member.filename)
                if name.is_absolute() or '..' in name.parts or ':' in member.filename or '\\' in member.filename:
                    raise RuntimeError('Unsafe archive member: ' + member.filename)
                relative = Path(mappings[member.filename]) if member.filename in mappings else Path('generation', *name.parts)
                target = folder / relative
                target.parent.mkdir(parents=True, exist_ok=True)
                if target.is_file():
                    with package.open(member) as incoming:
                        expected = hashlib.sha256(incoming.read()).hexdigest()
                    if sha(target) == expected:
                        continue
                with package.open(member) as incoming, target.open('wb') as outgoing:
                    shutil.copyfileobj(incoming, outgoing, length=1024 * 1024)
        receipt['archive'] = archive.name
        (folder / 'download-receipt.json').write_text(json.dumps(receipt, indent=2) + '\n', encoding='utf-8')
        request = json.loads((folder / 'generation-request.json').read_text(encoding='utf-8'))
        provenance = dict(key=key, requestId=request['requestId'], jobId=job_id,
            server=request['server'], webUrl=request['server'] + '/jobs/' + job_id,
            archiveSha256=receipt['archive_sha256'], archiveBytes=receipt['archive_bytes'],
            original='original.glb', originalSha256=sha(folder / 'original.glb'),
            reference='reference.png', referenceSha256=sha(folder / 'reference.png'),
            userReference='user-reference.png', userReferenceSha256=sha(folder / 'user-reference.png'),
            referencePrompt='reference-settings.json', referencePromptSha256=sha(folder / 'reference-settings.json'),
            referenceTool='built-in image_gen', settings='generation-request.json',
            sourceRecord='generation-record.json', sourceRecordSha256=sha(folder / 'generation-record.json'),
            downloadReceipt='download-receipt.json', visualReview='web-review.json',
            archivePathMapping={**mappings, '*': 'generation/*'},
            license='New project-generated reference and model; no third-party stock model used.',
            licenseNotice='../LICENSE.md', generatorLicense='../TRELLIS.2-LICENSE.txt')
        (folder / 'provenance.json').write_text(json.dumps(provenance, indent=2) + '\n', encoding='utf-8')
        results.append(dict(key=key, status='preserved', originalSha256=provenance['originalSha256']))
    print(json.dumps(results, indent=2))


if __name__ == '__main__':
    collect()
