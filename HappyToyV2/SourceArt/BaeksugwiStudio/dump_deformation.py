"""Dump exact rest/evaluated vertex coordinates for surface strain QA.

Blender 4.0.2 --background --python dump_deformation.py -- --output PATH.npz
Coordinates are armature-local/world Blender Z-up with identity object transform.
No geometry, animation, weights or authoring files are modified.
"""
import argparse
import json
import sys
from pathlib import Path
import bpy
import numpy as np

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
from author_blender import mesh_points, digest

parser = argparse.ArgumentParser()
parser.add_argument('--output', type=Path, required=True)
arguments = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
args = parser.parse_args(arguments)
bpy.ops.wm.open_mainfile(filepath=str(HERE / 'Baeksugwi_Run.blend'))
rig = bpy.data.objects['Baeksugwi_Rig']
meshes = [o for o in bpy.data.objects if o.type == 'MESH']
if len(meshes) != 1:
    raise ValueError('Strain dump expects the single original source mesh')
obj = meshes[0]
motion = json.loads((HERE / 'run-motion-report.json').read_text(encoding='utf-8'))
rig.animation_data.action = bpy.data.actions[motion['actions']['Run_InPlace']['action']]
original = mesh_points(obj)
source = np.load(HERE / 'surface-weights.npz', allow_pickle=False)['positions']
source_blender = source[:, (0, 2, 1)].copy()
source_blender[:, 1] *= -1
if original.shape != source_blender.shape or np.max(np.abs(original - source_blender)) > 1e-6:
    raise AssertionError('Vertex order differs from original source positions')
obj.data.calc_loop_triangles()
triangles = np.empty(len(obj.data.loop_triangles) * 3, dtype=np.int32)
obj.data.loop_triangles.foreach_get('vertices', triangles)
arrays = {'rest': original, 'rest_gltf': source, 'triangles': triangles.reshape((-1, 3)),
          'coordinate_system': np.asarray('BlenderZup (x,-glTFz,glTFy)'),
          'weights_sha256': np.asarray(digest(HERE / 'surface-weights.npz'))}
for frame in (1, 12, 23):
    bpy.context.scene.frame_set(frame)
    posed = mesh_points(obj, evaluated=True)
    if posed.shape != original.shape:
        raise AssertionError('Evaluated mesh changed topology')
    arrays['frame' + str(frame)] = posed
args.output.parent.mkdir(parents=True, exist_ok=True)
np.savez_compressed(args.output, **arrays)
print('DEFORMATION_DUMP_COMPLETE', args.output, len(original), len(triangles) // 3, flush=True)
