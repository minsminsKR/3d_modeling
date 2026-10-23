"""Keep verified service originals and their full source archive provenance.

Normal Python, from repository root. Run after collect/resume has downloaded the
same request into Builds/ModelingDownloads/SchoolRenewal/<key>.
"""
from pathlib import Path
import argparse
import hashlib
import json
import shutil
import zipfile

SCHOOL = Path(__file__).resolve().parents[1]
PROJECT = SCHOOL.parents[1]
KEYS = ('classroom-door', 'classroom-window', 'fluorescent-fixture', 'shoe-bank')


def sha(path):
    with path.open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def preserve(key):
    destination = SCHOOL / key
    settings = json.loads((destination / 'generation-settings.json').read_text(encoding='utf-8'))
    model = settings['model']
    job = model['job_id']
    downloads = PROJECT / 'Builds/ModelingDownloads/SchoolRenewal' / key
    archive = downloads / (job + '.zip')
    receipt_path = downloads / (job + '.download.json')
    receipt = json.loads(receipt_path.read_text(encoding='utf-8'))
    digest = sha(archive)
    expected = receipt.get('archive_sha256') or receipt.get('sha256')
    if expected and digest != expected:
        raise ValueError('Verified archive changed: ' + key)
    with zipfile.ZipFile(archive) as source:
        for item in source.infolist():
            if item.is_dir():
                continue
            relative = Path(item.filename)
            if relative.is_absolute() or '..' in relative.parts:
                raise ValueError('Unsafe archive member')
            target = destination / ('original.glb' if item.filename == 'model.glb'
                                    else 'service/' + item.filename)
            target.parent.mkdir(parents=True, exist_ok=True)
            with source.open(item) as stream, target.open('wb') as output:
                shutil.copyfileobj(stream, output)
    original = destination / 'original.glb'
    if not original.is_file():
        raise ValueError('Actual hydrated service GLB missing')
    with original.open('rb') as stream:
        if stream.read(4) != b'glTF':
            raise ValueError('Actual hydrated service GLB missing')
    record = destination / 'service/generation-record.json'
    if not record.is_file():
        raise ValueError('Server generation record missing')
    receipt['archive'] = archive.name
    (destination / 'download-receipt.json').write_text(
        json.dumps(receipt, indent=2) + '\n', encoding='utf-8')
    report = dict(key=key, server=model['service'], requestId=model['request_id'],
                  jobId=job, webUrl=model['web_url'], archiveSha256=digest,
                  archiveBytes=archive.stat().st_size, original='original.glb',
                  originalSha256=sha(original), reference='reference.png',
                  referenceSha256=sha(destination / 'reference.png'),
                  referencePrompt='reference-prompt.txt', referenceTool='built-in image_gen',
                  sourceRecord='service/generation-record.json',
                  archivePathMapping={'model.glb': 'original.glb', '*': 'service/*'},
                  visualReview='web-review.json',
                  license='New project-generated reference and model; no third-party stock model used.')
    (destination / 'provenance.json').write_text(json.dumps(report, indent=2) + '\n', encoding='utf-8')
    print(key, 'PRESERVED', original.stat().st_size, report['originalSha256'])


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('keys', nargs='+', choices=KEYS)
    for key in parser.parse_args().keys:
        preserve(key)
