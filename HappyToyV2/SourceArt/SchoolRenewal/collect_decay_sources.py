"""Preserve verified A100 modeling archive sources without ZIP/cache duplication.

Run from the repository root with normal Python 3.11. The modeling CLI must have
collected the request into Builds/SchoolRenewalDownloads/<key> first.
"""
from pathlib import Path
import hashlib
import json
import shutil
import zipfile

ROOT = Path(__file__).resolve().parents[2]
SERVICE = 'https://agrees-mic-icon-contribution.trycloudflare.com'
ASSETS = {
    'pipe-manifold': ('4810cde1-a314-459a-950e-a8c8747e3c42', '3c8d0abc-5b34-44f0-862f-2a0e3e9ee49e'),
    'concrete-rubble': ('4810cde1-a314-459a-950e-a8c8747e3c43', '3701cae2-3b9a-4417-b0cf-36e224e1d624'),
    'loose-stones': ('4810cde1-a314-459a-950e-a8c8747e3c44', '0d29e70e-1537-46ae-a7bb-731e75571052'),
    'pipe-run': ('4810cde1-a314-459a-950e-a8c8747e3c45', 'ca3dd93f-8f1a-47fe-be77-ea77dfc4b5dd'),
}

def sha(path):
    with path.open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()

def preserve_receipt(key):
    _, job = ASSETS[key]
    downloads = ROOT / 'Builds/SchoolRenewalDownloads' / key
    receipt = json.loads((downloads / (job + '.download.json')).read_text(encoding='utf-8'))
    receipt['archive'] = job + '.zip'
    receipt['archivePathNote'] = 'Optional verified archive cache; all original contents are preserved here under the recorded archivePathMapping.'
    (ROOT / 'SourceArt/SchoolRenewal' / key / 'download-receipt.json').write_text(
        json.dumps(receipt, indent=2) + '\n', encoding='utf-8')

def preserve(key):
    request, job = ASSETS[key]
    downloads = ROOT / 'Builds/SchoolRenewalDownloads' / key
    destination = ROOT / 'SourceArt/SchoolRenewal' / key
    archive = downloads / (job + '.zip')
    receipt = json.loads((downloads / (job + '.download.json')).read_text(encoding='utf-8'))
    digest = sha(archive)
    expected = receipt.get('archive_sha256') or receipt.get('sha256')
    if expected and digest != expected:
        raise ValueError('Verified archive changed: ' + key)
    with zipfile.ZipFile(archive) as source:
        for info in source.infolist():
            if info.is_dir():
                continue
            relative = Path(info.filename)
            if relative.is_absolute() or '..' in relative.parts:
                raise ValueError('Unsafe archive member')
            target = destination / ('original.glb' if info.filename == 'model.glb' else 'service/' + info.filename)
            target.parent.mkdir(parents=True, exist_ok=True)
            with source.open(info) as stream, target.open('wb') as output:
                shutil.copyfileobj(stream, output)
    duplicate = destination / 'service/model.glb'
    if duplicate.is_file():
        if sha(duplicate) != sha(destination / 'original.glb'):
            raise ValueError('Unexpected previous source')
        duplicate.unlink()
    record = json.loads((destination / 'service/generation-record.json').read_text(encoding='utf-8'))
    provenance = {
        'key': key, 'requestId': request, 'jobId': job, 'server': SERVICE,
        'webUrl': SERVICE + '/jobs/' + job,
        'archiveSha256': digest, 'archiveBytes': archive.stat().st_size,
        'original': 'original.glb', 'originalSha256': sha(destination / 'original.glb'),
        'reference': 'reference.png', 'referenceSha256': sha(destination / 'reference.png'),
        'referencePrompt': 'reference-prompt.txt', 'referenceTool': 'built-in image_gen',
        'sourceRecord': 'service/generation-record.json',
        'archivePathMapping': {'model.glb': 'original.glb', '*': 'service/*'},
        'visualReview': 'web-review.json' if (destination / 'web-review.json').is_file()
                        else 'Pending direct webapp inspection by integration owner',
        'license': 'New project-generated reference and model; no third-party stock model used.',
    }
    (destination / 'provenance.json').write_text(json.dumps(provenance, indent=2) + '\n', encoding='utf-8')
    preserve_receipt(key)
    print(key, 'PRESERVED', provenance['originalSha256'], 'recordKeys', sorted(record))

if __name__ == '__main__':
    import argparse
    parser = argparse.ArgumentParser()
    parser.add_argument('keys', nargs='+', choices=ASSETS)
    for key in parser.parse_args().keys:
        preserve(key)
