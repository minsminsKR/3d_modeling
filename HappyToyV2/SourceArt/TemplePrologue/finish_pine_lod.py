"""Build a distant pine LOD from the preserved Blender game mesh (CPU only).

Requires fast-simplification 0.1.12 for Blender's Python 3.10 on PYTHONPATH.
The low mesh is QEM-reduced; each triangle gets UVs projected onto its nearest
source triangle so UV seams are not averaged across unrelated atlas islands.
The resulting LOD requires in-game visual review before quality approval.
"""
from pathlib import Path
import hashlib
import json
import sys

import bpy
import numpy as np
from mathutils import Vector
from mathutils.bvhtree import BVHTree
from mathutils.geometry import barycentric_transform, closest_point_on_tri
import fast_simplification

here = Path(__file__).resolve().parent
blend = here / 'pine' / 'pine.blend'
export = here / 'game-export' / 'pine-lod1.fbx'
bpy.ops.wm.open_mainfile(filepath=str(blend))
bpy.context.preferences.filepaths.save_version = 0
source = bpy.data.objects['CE_pine_LOD0']
mesh = source.data
mesh.calc_loop_triangles()
vertices = np.empty(len(mesh.vertices) * 3, dtype=np.float32)
mesh.vertices.foreach_get('co', vertices)
vertices = vertices.reshape((-1, 3))
faces = np.empty(len(mesh.loop_triangles) * 3, dtype=np.int32)
mesh.loop_triangles.foreach_get('vertices', faces)
faces = faces.reshape((-1, 3))
if not mesh.uv_layers.active or not np.isfinite(vertices).all():
    raise RuntimeError('The source mesh must have finite positions and an atlas')
reduced, triangles = fast_simplification.simplify(vertices, faces, target_count=30000, agg=5)
if len(triangles) > 50000 or len(triangles) < 1000 or not np.isfinite(reduced).all():
    raise RuntimeError('Distant LOD did not reach the checked triangle budget')

source_vertices = [Vector(row) for row in vertices]
tree = BVHTree.FromPolygons(source_vertices, faces.tolist(), all_triangles=True)
loop_triangles = mesh.loop_triangles
atlas = mesh.uv_layers.active.data
uv = np.empty((len(triangles) * 3, 2), dtype=np.float32)
errors = []
for i, face in enumerate(triangles):
    points = [Vector(reduced[index]) for index in face]
    centre = (points[0] + points[1] + points[2]) / 3
    hit, normal, index, distance = tree.find_nearest(centre)
    if index is None:
        raise RuntimeError('Cannot project LOD triangle onto original atlas')
    tri = loop_triangles[index]
    a, b, c = (source_vertices[v] for v in tri.vertices)
    ta, tb, tc = (Vector((*atlas[loop].uv, 0)) for loop in tri.loops)
    errors.append(distance)
    for corner, point in enumerate(points):
        nearest = closest_point_on_tri(point, a, b, c)
        mapped = barycentric_transform(nearest, a, b, c, ta, tb, tc)
        uv[i * 3 + corner] = mapped.x, mapped.y
if not np.isfinite(uv).all():
    raise RuntimeError('Invalid projected UVs')
# Collapse can remove the lowest root point. Keep both LODs on the same
# support plane so distant trees do not visibly rise at the switch.
ground_offset = float(vertices[:, 2].min() - reduced[:, 2].min())
reduced[:, 2] += ground_offset

old = bpy.data.objects.get('CE_pine_LOD1')
if old: bpy.data.objects.remove(old, do_unlink=True)
newmesh = bpy.data.meshes.new('Pine distant source-derived mesh')
newmesh.from_pydata(reduced.tolist(), [], triangles.tolist()); newmesh.update()
newmesh.uv_layers.new(name='Projected source atlas').data.foreach_set('uv', uv.ravel())
for face in newmesh.polygons: face.use_smooth = True
for material in mesh.materials: newmesh.materials.append(material)
lod = bpy.data.objects.new('CE_pine_LOD1', newmesh)
bpy.data.collections['Editable source-derived game LODs'].objects.link(lod)
lod.matrix_world = source.matrix_world.copy()
bpy.ops.object.select_all(action='DESELECT'); lod.select_set(True)
bpy.context.view_layer.objects.active = lod
bpy.ops.export_scene.fbx(filepath=str(export), use_selection=True, object_types={'MESH'},
    axis_forward='-Z', axis_up='Y', bake_anim=False, add_leaf_bones=False,
    path_mode='RELATIVE', use_mesh_modifiers=True)
lod.hide_render = True; lod.hide_set(True)
text = bpy.data.texts.get('SOURCE - finish_pine_lod.py') or bpy.data.texts.new('SOURCE - finish_pine_lod.py')
text.clear(); text.write(Path(__file__).read_text(encoding='utf-8'))
bpy.ops.wm.save_as_mainfile(filepath=str(blend), compress=True)
digest = hashlib.sha256(export.read_bytes()).hexdigest()
report_path = here / 'game-export' / 'pine' / 'asset.json'
report = json.loads(report_path.read_text(encoding='utf-8'))
report['lods'] = report['lods'][:1] + [dict(file=export.name, targetTriangles=50000,
    triangles=len(triangles), vertices=len(reduced), sha256=digest,
    boundsMin=reduced.min(axis=0).tolist(), boundsMax=reduced.max(axis=0).tolist(),
    projectedCentroidDistanceMax=max(errors), projectedCentroidDistanceP95=float(np.percentile(errors, 95)),
    groundOffset=ground_offset,
    method='fast-simplification 0.1.12 QEM and nearest source-triangle UV projection')]
for path in (report_path, here / 'pine' / 'export.json'):
    path.write_text(json.dumps(report, indent=2) + '\n', encoding='utf-8', newline='\n')
print('PINE_LOD1', json.dumps(report['lods'][1]), flush=True)
