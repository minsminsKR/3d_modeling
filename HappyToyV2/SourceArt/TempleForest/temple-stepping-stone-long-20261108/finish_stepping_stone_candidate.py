"""Finish the web-reviewed elongated stepping stone as reversible Blender review candidates.

Requires Blender 4.0.2 in a disposable background session.  It never modifies
the generated GLB.  No output is accepted as gameplay-ready without independent
web, Blender and in-game visual/performance review.

blender -b -t 2 --factory-startup --python-exit-code 1 --python <this file>
"""
from pathlib import Path
import importlib.util
import json
import sys

import bpy

HERE = Path(__file__).resolve().parent
KEY = HERE.name
ORIGINAL = HERE / 'original.glb'
OUTPUT = HERE / 'game-export'
BLEND = HERE / (KEY + '.blend')
REPORT = HERE / 'export.json'
RECIPE = HERE.parents[1] / 'CorridorEntrance' / 'finish_entrance_assets.py'
BUDGETS = (6000, 2000, 600)

if bpy.app.version[:3] != (4, 0, 2):
    raise RuntimeError('Expected Blender 4.0.2, got ' + bpy.app.version_string)
if not ORIGINAL.is_file() or not RECIPE.is_file():
    raise RuntimeError('Preserved original and shared corridor finishing recipe are required')
if OUTPUT.exists() or BLEND.exists() or REPORT.exists():
    raise RuntimeError('Review output exists; preserve it and use a fresh source folder')

spec = importlib.util.spec_from_file_location('corridor_entrance_finish', RECIPE)
finish = importlib.util.module_from_spec(spec)
spec.loader.exec_module(finish)
source_hash = finish.sha(ORIGINAL)
finish.SOURCE = HERE.parent
finish.OUTPUT = OUTPUT
# Normalize the long horizontal Blender Y axis to one metre. Do not square, flatten or synthesize a
# normal map; the generated stone silhouette and original PBR are the source.
# These are candidates, not approved budgets or actual rendered triangle counts.
finish.SETTINGS[KEY] = dict(axis=1, metres=1.0, texture=2048, budgets=BUDGETS)
try:
    OUTPUT.mkdir()
    finish.prepare(KEY, 0)
    if finish.sha(ORIGINAL) != source_hash:
        raise RuntimeError('The immutable original changed during finishing')
    source_text = bpy.data.texts.new('SOURCE - finish_stepping_stone_candidate.py')
    source_text.write(Path(__file__).read_text(encoding='utf-8'))
    bpy.ops.file.pack_all()
    bpy.ops.wm.save_as_mainfile(filepath=str(BLEND), compress=True)
    result = json.loads(REPORT.read_text(encoding='utf-8'))
    # The shared recipe labels every non-X axis as height; axis=1 is Blender Y depth.
    result['normalizedAxis'] = 'depth'
    result['front'] = 'No intrinsic front; long axis Blender Y, vertical Blender Z; verify in Unity'
    result['editableSha256'] = finish.sha(BLEND)
    result['recipe'] = Path(__file__).name
    result['sharedRecipe'] = '../../CorridorEntrance/finish_entrance_assets.py'
    result['candidatePurpose'] = 'review only; actual quality and performance are not certified'
    result['limitations'] = ('Shared recipe welds coincident vertices at 2e-5 m and recalculates normals. '
        'No normal map is invented. FBX reimport, UVs, sampled surface deviations, surface contact, '
        'appearance under game lighting, LOD transitions and GPU performance require separate review.')
    serialized = json.dumps(result, indent=2) + '\n'
    for path in (REPORT, OUTPUT / KEY / 'asset.json'):
        path.write_text(serialized, encoding='utf-8', newline='\n')
    print('STEPPING_STONE_REVIEW_CANDIDATES ' + json.dumps({
        'originalSha256': source_hash, 'editableSha256': result['editableSha256'],
        'lods': result['lods'], 'materials': result['materials']}), flush=True)
finally:
    if finish.sha(ORIGINAL) != source_hash:
        print('ERROR: preserved original digest changed', file=sys.stderr, flush=True)
