"""Verify complete generated originals, editable sources and game PBR handoff."""
from pathlib import Path
import argparse
import hashlib
import json
import struct
from PIL import Image, ImageStat

SOURCE = Path(__file__).resolve().parent
PROJECT = SOURCE.parents[1]
KEYS = ('wall-panel','floor-slab','ceiling-panel','classroom-door','classroom-window',
        'pipe-manifold','pipe-run','shoe-bank','school-desk','school-chair','archive-shelf',
        'metal-cabinet','timber-cabinet','fluorescent-fixture','concrete-rubble','loose-stones',
        'radiator','room-sign')

def sha(path):
    with path.open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()

def validate():
    results = []
    for key in KEYS:
        folder = SOURCE / key
        game = PROJECT / 'Assets/Resources/SchoolRenewal' / key
        report = json.loads((folder/'export.json').read_text(encoding='utf-8-sig'))
        asset = json.loads((game/'asset.json').read_text(encoding='utf-8-sig'))
        if report != asset:
            raise ValueError('Source/game finishing records differ: '+key)
        original = folder/'original.glb'
        if sha(original) != report['originalSha256']:
            raise ValueError('Generated original no longer matches its finishing input: '+key)
        with original.open('rb') as stream:
            magic, version, length = struct.unpack('<4sII', stream.read(12))
        if magic != b'glTF' or version != 2 or length != original.stat().st_size:
            raise ValueError('Original is not a hydrated GLB: '+key)
        editable = folder/report['editable']
        if not editable.is_file() or editable.stat().st_size < 10000:
            raise ValueError('Packed editable source absent: '+key)
        reference = folder/'reference.png'
        Image.open(reference).verify()
        provenance = json.loads((folder/'provenance.json').read_text(encoding='utf-8-sig'))
        review = json.loads((folder/'web-review.json').read_text(encoding='utf-8-sig'))
        approved = review.get('approvedForBlenderFinishing', review.get('approved', False))
        approved = approved or review.get('review', {}).get('decision', '').startswith('Approved for Blender finishing')
        if not approved:
            raise ValueError('Direct web viewer inspection absent: '+key)
        levels = []
        for level, material in zip(report['lods'], report['materials']):
            mesh = game/level['file']
            if sha(mesh) != level['sha256']:
                raise ValueError('Derived FBX no longer matches source export: '+key)
            maps = {}
            for channel in ('baseColor','metallicSmoothness','normal'):
                path = game/material[channel]
                image = Image.open(path)
                if max(image.size) > report['textureMaxSize']:
                    raise ValueError('Game texture exceeds imported budget: '+key)
                maps[channel] = dict(file=path.name, size=image.size, sha256=sha(path))
                if channel == 'normal' and sum(ImageStat.Stat(image.convert('RGB')).var) < .01:
                    raise ValueError('Claimed high-to-low normal map is flat: '+key)
            if key == 'fluorescent-fixture':
                mask = Image.open(game/material['emission']).convert('L')
                dark, bright = mask.histogram()[0], sum(mask.histogram()[128:])
                if dark == 0 or bright == 0:
                    raise ValueError('Tube-only emission lost its dark casing: '+key)
            levels.append(dict(file=mesh.name, triangles=level['triangles'], maps=maps))
        results.append(dict(key=key, originalSha256=report['originalSha256'], originalBytes=original.stat().st_size,
            editable=editable.name, editableSha256=sha(editable), referenceSha256=sha(reference),
            provenance='provenance.json', reviewed=True, lods=levels))
    return dict(status='PASS', count=len(results), sourceScope='Original GLB/reference/records, packed Blender, source-derived game LODs/PBR; no manual-play or FPS claim', assets=results)

if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--output', type=Path)
    arguments = parser.parse_args()
    result = validate()
    if arguments.output:
        arguments.output.write_text(json.dumps(result, indent=2)+'\n', encoding='utf-8')
    print('SCHOOL_RENEWAL_KIT_PASS', result['count'])
