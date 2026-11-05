"""Measure the actual central ash surface in the editable vessel LODs.

Blender 4.0.2 background. Ray hits are in normalized source metres; Unity
axis conversion and the exact game placement need their own check.
"""
import json
from pathlib import Path
import bpy
from mathutils import Vector
from mathutils.bvhtree import BVHTree

root = Path(__file__).resolve().parent
bpy.ops.wm.open_mainfile(filepath=str(root / 'incense-vessel.blend'))
results = []
for i in range(2):
    obj = bpy.data.objects['CE_incense-vessel_LOD' + str(i)]
    mesh = obj.data
    mesh.calc_loop_triangles()
    vertices = [obj.matrix_world @ vertex.co for vertex in mesh.vertices]
    triangles = [tuple(triangle.vertices) for triangle in mesh.loop_triangles]
    tree = BVHTree.FromPolygons(vertices, triangles, all_triangles=True)
    low = min(vertex.z for vertex in vertices)
    high = max(vertex.z for vertex in vertices)
    samples = []
    for x in (-.025, 0, .025):
        for y in (-.025, 0, .025):
            position, normal, index, distance = tree.ray_cast(Vector((x, y, high + .1)), Vector((0, 0, -1)))
            if position is None:
                raise RuntimeError('Central opening has no source surface under ash at ' + str((x, y)))
            samples.append(dict(x=x, y=y, height=position.z, normalZ=normal.z))
    results.append(dict(lod=i, bottom=low, rim=high, samples=samples,
                        center=next(s['height'] for s in samples if s['x'] == s['y'] == 0)))
out = dict(scope='Blender BVH hits on source-derived LOD, not Unity placement or alpha validation', lods=results)
(root / 'ash-surface.json').write_text(json.dumps(out, indent=2) + '\n', encoding='utf-8')
print('INCENSE_ASH_SURFACE', json.dumps(out), flush=True)
