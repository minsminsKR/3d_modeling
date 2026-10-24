"""Preserve verified base/AI-rig CLI archives, without personal CLI state.

Run after collect/resume; --rig-entry is the actual archive GLB entry inspected
from the refreshed result, never an inferred alternate generation.
"""
from pathlib import Path, PurePosixPath
import argparse
import hashlib
import json
import shutil
import zipfile

HERE = Path(__file__).resolve().parent
PROJECT = HERE.parents[1]
JOB = 'ba4c614f-570d-4433-b6ae-950ebaefadfe'


def sha(path):
    with path.open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def collect(phase, rig_entry):
    cache = PROJECT / 'Builds/SchoolWalkerDownloads'
    archive = cache / (JOB + '.zip')
    receipt = json.loads(archive.with_suffix('.download.json').read_text(encoding='utf-8-sig'))
    if sha(archive) != receipt['archive_sha256']:
        raise RuntimeError('CLI archive no longer matches its verified receipt')
    if phase == 'rigged' and not rig_entry:
        raise ValueError('Select the actual rigged GLB archive entry explicitly')
    with zipfile.ZipFile(archive) as package:
        if rig_entry and rig_entry not in package.namelist():
            raise ValueError('The selected rig source is not in this archive')
        for member in package.infolist():
            if member.is_dir():
                continue
            if 'preview' in PurePosixPath(member.filename).name.lower():
                # Disposable web-viewer decimations are not authoring inputs.
                continue
            name = PurePosixPath(member.filename)
            if name.is_absolute() or '..' in name.parts or ':' in member.filename or '\\' in member.filename:
                raise ValueError('Unsafe archive member: ' + member.filename)
            if member.filename == 'model.glb':
                target = HERE / 'original.glb'
            elif member.filename == rig_entry:
                target = HERE / 'rigged-original.glb'
            elif member.filename == 'inputs/01_reference-v2-purple-flower.png':
                target = HERE / 'reference-v2-purple-flower.png'
            elif member.filename == 'processed_inputs/01.png':
                target = HERE / 'processed-reference.png'
            else:
                target = HERE / 'service' / phase / Path(*name.parts)
            target.parent.mkdir(parents=True, exist_ok=True)
            if target.is_file():
                with package.open(member) as incoming:
                    expected = hashlib.sha256(incoming.read()).hexdigest()
                if sha(target) == expected:
                    continue
                raise RuntimeError('An existing preserved source would change: ' + target.name)
            with package.open(member) as incoming, target.open('wb') as outgoing:
                shutil.copyfileobj(incoming, outgoing, 1024 * 1024)
    receipt['archive'] = archive.name
    (HERE / ('download-receipt-' + phase + '.json')).write_text(json.dumps(receipt, indent=2) + '\n', encoding='utf-8')
    provenance_path = HERE / 'provenance.json'
    provenance = json.loads(provenance_path.read_text(encoding='utf-8')) if provenance_path.exists() else {}
    request = json.loads((HERE / 'generation-request.json').read_text(encoding='utf-8'))
    provenance.update(server=request['server'], requestId=request['requestId'], jobId=JOB,
        webUrl=request['server'] + '/jobs/' + JOB,
        original='original.glb', originalSha256=sha(HERE / 'original.glb'),
        reference='reference-v2-purple-flower.png', referenceSha256=sha(HERE / 'reference-v2-purple-flower.png'),
        userFaceReference='face-reference.jpg', userFaceReferenceSha256=sha(HERE / 'face-reference.jpg'),
        settings='generation-request.json', referencePrompts=['reference-settings.json', 'reference-v2-settings.json'],
        origin='User-provided portrait and project-generated reference/base model; no stock character model',
        license='LICENSE.md', generatorLicense='TRELLIS.2-LICENSE.txt')
    provenance[phase + 'Archive'] = dict(sha256=receipt['archive_sha256'], bytes=receipt['archive_bytes'],
        receipt='download-receipt-' + phase + '.json', records='service/' + phase)
    if phase == 'rigged':
        provenance['riggedOriginal'] = dict(file='rigged-original.glb', sha256=sha(HERE / 'rigged-original.glb'), archiveEntry=rig_entry)
    provenance_path.write_text(json.dumps(provenance, indent=2) + '\n', encoding='utf-8')
    print(json.dumps(dict(status='PRESERVED', phase=phase, originalSha256=provenance['originalSha256'])))


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--phase', required=True, choices=('base', 'rigged'))
    parser.add_argument('--rig-entry')
    arguments = parser.parse_args()
    collect(arguments.phase, arguments.rig_entry)
