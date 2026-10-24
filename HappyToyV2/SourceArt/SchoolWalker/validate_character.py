"""Verify the complete portable flower-man original/rig/game source handoff."""
from pathlib import Path
import hashlib
import json
import struct

from PIL import Image, ImageStat

HERE = Path(__file__).resolve().parent
PROJECT = HERE.parents[1]
GAME = PROJECT / 'Assets/Resources/SchoolWalker'


def digest(path):
    checksum = hashlib.sha256()
    with path.open('rb') as stream:
        for block in iter(lambda: stream.read(8 * 1024 * 1024), b''):
            checksum.update(block)
    return checksum.hexdigest()


def validate():
    report = json.loads((HERE / 'export.json').read_text(encoding='utf-8'))
    if report != json.loads((GAME / 'asset.json').read_text(encoding='utf-8')):
        raise RuntimeError('Portable source and game export records differ')
    for name, expected in ((report['generatedSource'], report['generatedSha256']),
                           (report['riggedSource'], report['riggedSha256'])):
        path = HERE / name
        if digest(path) != expected:
            raise RuntimeError('Preserved actual app source no longer matches finishing input: ' + name)
        with path.open('rb') as stream:
            magic, version, length = struct.unpack('<4sII', stream.read(12))
        if magic != b'glTF' or version != 2 or length != path.stat().st_size:
            raise RuntimeError('Original is not a hydrated complete GLB: ' + name)
    editable = HERE / report['editable']
    if not editable.is_file() or editable.stat().st_size < 100000:
        raise RuntimeError('Packed editable body/AI skeleton/actions are absent')
    for level, budget in zip(report['lods'], (50000, 12000, 4000)):
        if level['triangles'] > budget or level['skin']['maximumInfluences'] > 4:
            raise RuntimeError('Actual game LOD or skin exceeds its agreed budget')
    for model in report['fbx']:
        if digest(GAME / model['file']) != model['sha256']:
            raise RuntimeError('Exported FBX differs from the source record: ' + model['file'])
    for prefix, limit in (('SW_LOD0', 4096), ('SW_LOD2', 1024)):
        for channel in ('baseColor', 'metallicSmoothness', 'normal'):
            image = Image.open(GAME / (prefix + '-' + channel + '.png'))
            if max(image.size) > limit or min(image.size) < 512:
                raise RuntimeError('Actual game map has an invalid size')
            if channel == 'normal' and sum(ImageStat.Stat(image.convert('RGB')).var) < .01:
                raise RuntimeError('Claimed source-derived normal map is flat')
    if abs(report['normalization']['heightMetres'] - 1.77) > .0001 or report['canonicalForward'] != 'Unity +Z':
        raise RuntimeError('Actor lost its reviewed scale or canonical facing')
    walk = report['walk']
    if abs(walk['worldTravelPerCycleMetres'] - walk['travelSpeedMetresPerSecond'] * walk['durationSeconds']) > .00001:
        raise RuntimeError('Authored planted stride and runtime travel contract differ')
    for name in ('rig-plan.json', 'finish_character.py', 'heavy_walk.py', 'inspect_character.py',
                 'review_character.py', 'reference-v2-purple-flower.png', 'reference-v2-settings.json',
                 'web-review.json', 'finishing-review.json', 'README.md', 'AUTHORING.md'):
        if not (HERE / name).is_file():
            raise RuntimeError('Missing required authoring/review input: ' + name)
    reviewed = json.loads((HERE / 'finishing-review.json').read_text(encoding='utf-8'))
    if reviewed['editableSha256'] != digest(editable) or reviewed['status'] != 'PASS':
        raise RuntimeError('Finished actual body review is missing or stale')
    return dict(status='PASS', generatedSourceSha256=report['generatedSha256'],
                riggedSourceSha256=report['riggedSha256'], editableSha256=reviewed['editableSha256'],
                bones=len(report['preservedAISourceSnapshot']['bones']),
                triangles=[level['triangles'] for level in report['lods']],
                worldTravelPerCycleMetres=walk['worldTravelPerCycleMetres'],
                scope='Portable original/AI rig, source-derived LOD/PBR/actions, actual Blender review; not Unity/runtime verification')


if __name__ == '__main__':
    print('SCHOOL_WALKER_SOURCE_HANDOFF_PASS ' + json.dumps(validate()))
