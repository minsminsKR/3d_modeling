"""CPU experiment: inspect an additional QEM reduction without writing assets.

Pass --module-dir to a Python 3.10 installation of fast-simplification 0.1.12.
"""
from pathlib import Path
import sys
import bpy
import numpy as np

args = sys.argv[sys.argv.index('--') + 1:]
sys.path.insert(0, args[args.index('--module-dir') + 1])
import fast_simplification

here = Path(__file__).resolve().parent
bpy.ops.wm.open_mainfile(filepath=str(here / 'pine' / 'pine.blend'))
obj = bpy.data.objects['CE_pine_LOD0']
mesh = obj.data
vertices = np.empty(len(mesh.vertices) * 3, dtype=np.float32)
mesh.vertices.foreach_get('co', vertices)
mesh.calc_loop_triangles()
faces = np.empty(len(mesh.loop_triangles) * 3, dtype=np.int32)
mesh.loop_triangles.foreach_get('vertices', faces)
vertices = vertices.reshape((-1, 3)); faces = faces.reshape((-1, 3))
reduced, triangles = fast_simplification.simplify(vertices, faces, target_count=30000, agg=5)
print('PINE_FAST_REDUCTION', len(vertices), len(faces), len(reduced), len(triangles),
      'bounds', reduced.min(axis=0).tolist(), reduced.max(axis=0).tolist(), flush=True)
