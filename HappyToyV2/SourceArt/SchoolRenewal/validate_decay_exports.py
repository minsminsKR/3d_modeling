"""Validate the source-derived decay game exports without running Unity.

This verifies actual files, source identity, LOD budgets and non-flat source-normal
textures; in-game placement, collisions and visual approval belong to Unity QA.
"""
from pathlib import Path
import hashlib
import json
import numpy as np
from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
BUDGETS = {'pipe-manifold': (8000,7000), 'concrete-rubble': (7000,6600),
           'loose-stones': (2500,1100), 'pipe-run': (4000,1500)}

def sha(path):
    with path.open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()

def validate(key):
    source = ROOT / 'SourceArt/SchoolRenewal' / key
    game = ROOT / 'Assets/Resources/SchoolRenewal' / key
    data = json.loads((game / 'asset.json').read_text(encoding='utf-8'))
    assert data['key'] == key
    assert sha(source / 'original.glb') == data['originalSha256']
    assert (source / (key + '.blend')).stat().st_size > 1024
    assert len(data['lods']) == 2 and len(data['materials']) == 2
    normal_stats = []
    for index, (lod, mat) in enumerate(zip(data['lods'], data['materials'])):
        assert 12 < lod['triangles'] <= BUDGETS[key][index] + 4
        assert sha(game / lod['file']) == lod['sha256']
        size = 2048 if index == 0 else 1024
        for name in ('baseColor', 'metallicSmoothness', 'normal'):
            with Image.open(game / mat[name]) as image:
                assert image.size == (size,size), (key, name, image.size)
                values = np.asarray(image.convert('RGB'))
                if name == 'normal':
                    occupied = values[:,:,2] > 64
                    assert np.count_nonzero(occupied) > size * size * .2
                    variation = np.std(values[:,:,:2][occupied], axis=0)
                    assert max(variation) > 2, (key, 'flat normal bake', variation.tolist())
                    normal_stats.append(variation.tolist())
    report = {'key': key, 'sourceUnchanged': True, 'lodTriangles': [lod['triangles'] for lod in data['lods']],
              'textureSizes': [2048,1024], 'tangentNormalVariation': normal_stats,
              'limitations': 'File/geometry/PBR validation only; see root webapp/Unity review.'}
    (source / 'export-validation.json').write_text(json.dumps(report,indent=2)+'\n',encoding='utf-8')
    print('DECAY_EXPORT_PASS',json.dumps(report))

if __name__ == '__main__':
    import argparse
    parser=argparse.ArgumentParser()
    parser.add_argument('keys',nargs='+',choices=BUDGETS)
    for key in parser.parse_args().keys:
        validate(key)
