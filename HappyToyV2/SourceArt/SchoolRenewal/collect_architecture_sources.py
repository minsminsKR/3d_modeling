"""Preserve hash-verified modeling CLI archives; cache ZIPs remain under Builds."""
from pathlib import Path, PurePosixPath
import hashlib
import json
import shutil
import zipfile

SOURCE = Path(__file__).resolve().parent
CACHE = SOURCE.parents[1] / 'Builds/SchoolRenewalDownloads/architecture'
JOBS = {'wall-panel': '838f97dc-630b-4bc8-893f-be5abf8bf609',
        'floor-slab': '2b706300-e051-453f-b1db-022c53a3b9dd',
        'ceiling-panel': 'b1c1dc08-d260-4492-8263-468a92ca6b16'}

def sha(path):
    with path.open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()

def collect():
    for key, job in JOBS.items():
        archive = CACHE / (job + '.zip')
        receipt_path = CACHE / (job + '.download.json')
        if not archive.is_file() or not receipt_path.is_file():
            print(key, 'pending')
            continue
        receipt = json.loads(receipt_path.read_text(encoding='utf-8'))
        if sha(archive) != receipt['archive_sha256']:
            raise RuntimeError('Verified CLI archive changed: ' + key)
        target = SOURCE / key
        with zipfile.ZipFile(archive) as package:
            for member in package.infolist():
                path = PurePosixPath(member.filename)
                if member.is_dir():
                    continue
                if path.is_absolute() or '..' in path.parts or ':' in member.filename or '\\' in member.filename:
                    raise RuntimeError('Unsafe archive source path')
                relative = Path('original.glb') if member.filename == 'model.glb' else Path('service', *path.parts)
                destination = target / relative
                destination.parent.mkdir(parents=True, exist_ok=True)
                with package.open(member) as incoming, destination.open('wb') as outgoing:
                    shutil.copyfileobj(incoming, outgoing, length=1024*1024)
        receipt['archive'] = archive.name
        (target / 'download-receipt.json').write_text(json.dumps(receipt, indent=2)+'\n', encoding='utf-8')
        provenance = dict(key=key, jobId=job, requestId='a2929154-6538-4783-b09c-84c59a8ed541',
            webUrl='https://agrees-mic-icon-contribution.trycloudflare.com/jobs/'+job,
            original='original.glb', originalSha256=sha(target/'original.glb'),
            reference='reference.png', referenceSha256=sha(target/'reference.png'),
            generationRecord='service/generation-record.json', referencePrompt='reference-prompt.json',
            license='Original project-generated image and model; see ../LICENSE.md')
        (target / 'provenance.json').write_text(json.dumps(provenance, indent=2)+'\n', encoding='utf-8')
        print(key, 'PRESERVED', provenance['originalSha256'])

if __name__ == '__main__':
    collect()
