"""Build separate source-derived path LOD and irregular-edge review candidates.

Blender 4.0.2. The untouched generated original and full PBR maps are packed
into the editable blend. This recipe is not a Unity integration or approval.
Run in a disposable background process with two CPU threads. A pre-existing
output is never overwritten; use a fresh source directory for a new revision.
"""
from pathlib import Path
import hashlib
import importlib.util
import json
import math
import numpy as np
import bpy

here = Path(__file__).resolve().parent
key = here.name
original = here / 'original.glb'
out = here / 'game-export'
blend = here / (key + '.blend')
if not original.is_file():
    raise RuntimeError('Preserved generated GLB is required')
if out.exists() or blend.exists() or (here / 'export.json').exists():
    raise RuntimeError('Candidate output already exists; do not overwrite it')
recipe = here.parents[1] / 'CorridorEntrance' / 'finish_entrance_assets.py'
spec = importlib.util.spec_from_file_location('entrance_finish', recipe)
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)
module.SOURCE = here.parent
module.OUTPUT = out
out.mkdir()
module.SETTINGS[key] = dict(axis=0, metres=2.0, texture=2048,
    budgets=(60000, 30000, 18000), square=True, max_height=.12)
module.prepare(key, 0)

# Independent source-derived variants: no faces, UV loops, or source textures
# are removed. Deform only the outer 28 percent of the normalized tile. Both
# the lateral inset and the downward feather are smooth, small and deterministic.
# The soil intersection and actual seam visibility require Unity validation.
base = [bpy.data.objects['CE_' + key + '_LOD' + str(i)] for i in range(3)]
collection = bpy.data.collections.new('Irregular edge review variants')
bpy.context.scene.collection.children.link(collection)
records = []
for variant, phase in [('a', .37), ('b', 2.11)]:
    for level, source in enumerate(base):
        obj = source.copy(); obj.data = source.data.copy()
        obj.name = 'CE_' + key + '_edge_' + variant + '_LOD' + str(level)
        collection.objects.link(obj)
        coords = np.empty(len(obj.data.vertices) * 3, dtype=np.float32)
        obj.data.vertices.foreach_get('co', coords)
        coords = coords.reshape((-1, 3))
        x, y = coords[:, 0].copy(), coords[:, 1].copy()
        theta = np.arctan2(y, x)
        edge = np.maximum(np.abs(x), np.abs(y))
        t = np.clip((edge - .72) / .26, 0, 1)
        # Limit deformation to the strength reviewed across all three LODs.
        # Stronger edge movement rotated several triangles past 90 degrees.
        weight = .25 * t * t * (3 - 2 * t)
        inset = np.clip(.040 + .018 * np.sin(5 * theta + phase)
                        + .012 * np.sin(9 * theta - 1.3 * phase), .008, .075)
        drop = np.clip(.030 + .012 * np.sin(4 * theta - phase)
                       + .006 * np.cos(7 * theta + phase), .012, .048)
        coords[:, 0] = x * (1 - weight * inset)
        coords[:, 1] = y * (1 - weight * inset)
        coords[:, 2] -= weight * drop
        obj.data.vertices.foreach_set('co', coords.reshape(-1))
        obj.data.update()
        obj.hide_render = False; obj.hide_set(False)
        bpy.ops.object.select_all(action='DESELECT')
        obj.select_set(True); bpy.context.view_layer.objects.active = obj
        path = out / (key + '-edge-' + variant + '-lod' + str(level) + '.fbx')
        if path.exists(): raise RuntimeError('Refusing to overwrite: ' + str(path))
        bpy.ops.export_scene.fbx(filepath=str(path), use_selection=True, object_types={'MESH'},
            axis_forward='-Z', axis_up='Y', bake_anim=False, add_leaf_bones=False,
            path_mode='RELATIVE', use_mesh_modifiers=True)
        # Blender can leave obj.bound_box cached immediately after foreach_set.
        # Measure transformed actual vertices, not a stale cached bound box.
        matrix = np.asarray(obj.matrix_world, dtype=np.float64)
        points = coords.astype(np.float64) @ matrix[:3, :3].T + matrix[:3, 3]
        low, high = points.min(axis=0).tolist(), points.max(axis=0).tolist()
        records.append({'variant':variant,'level':level,'file':path.name,
            'triangles':module.triangles(obj),'vertices':len(obj.data.vertices),
            'boundsMin':list(low),'boundsMax':list(high),'sha256':module.sha(path),
            'phaseRadians':phase})
        obj.hide_render = True; obj.hide_set(True)

text = bpy.data.texts.new('SOURCE - finish_haunted_path_candidate.py')
text.write(Path(__file__).read_text(encoding='utf-8'))
bpy.ops.file.pack_all()
bpy.ops.wm.save_as_mainfile(filepath=str(blend), compress=True)
report_path = here / 'export.json'
report = json.loads(report_path.read_text(encoding='utf-8'))
report['edgeReviewVariants'] = records
report['edgeReviewRecipe'] = {'innerUnchangedMaxAbsXY':.72,'outerFullWeightAbsXY':.98,
    'strength':.25, 'insetFractionRange':[.002,.01875], 'downwardMetresRange':[.003,.012],
    'phasesRadians':{'a':.37,'b':2.11},
    'limitations':'Visual-only review candidates; surface distortion, tiling seams, self-intersection, LOD transitions and performance require explicit inspection.'}
report['editableSha256'] = module.sha(blend)
for path in [report_path, out / key / 'asset.json']:
    path.write_text(json.dumps(report, indent=2) + '\n', encoding='utf-8', newline='\n')
print('HAUNTED_PATH_CANDIDATES ' + json.dumps({'lods': report['lods'], 'variants': records,
      'editableSha256':report['editableSha256']}), flush=True)
