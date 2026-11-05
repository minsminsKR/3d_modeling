"""Derive an opt-in distant LOD from the trunk-preserving pine candidate.

Blender 4.0.2 and fast-simplification 0.1.12; background/CPU. The production
pine and the source candidate are untouched. Game-camera review is required.
"""
import hashlib
import json
import math
from pathlib import Path

import bpy
import numpy as np
import fast_simplification
from mathutils import Matrix, Vector
from mathutils.bvhtree import BVHTree
from mathutils.geometry import barycentric_transform, closest_point_on_tri

here = Path(__file__).resolve().parent
root = here / 'pine-review'
source = root / 'pine-trunk-review.blend'
editable = root / 'pine-distance-review.blend'
export = root / 'pine-distance-review.fbx'
sha = lambda path: hashlib.sha256(path.read_bytes()).hexdigest()
source_hash = sha(source)
bpy.ops.wm.open_mainfile(filepath=str(source))
bpy.context.preferences.filepaths.save_version = 0
near = bpy.data.objects['CE_pine_trunk_review']; mesh = near.data; mesh.calc_loop_triangles()
vertices = np.empty(len(mesh.vertices) * 3, dtype=np.float32)
mesh.vertices.foreach_get('co', vertices); vertices = vertices.reshape(-1, 3)
faces = np.empty(len(mesh.loop_triangles) * 3, dtype=np.int32)
mesh.loop_triangles.foreach_get('vertices', faces); faces = faces.reshape(-1, 3)
if not mesh.uv_layers.active or not np.isfinite(vertices).all():
    raise RuntimeError('Review pine source lacks valid geometry or atlas')
reduced, triangles = fast_simplification.simplify(vertices, faces, target_count=30000, agg=5)
if len(triangles) > 50000 or len(triangles) < 1000 or not np.isfinite(reduced).all():
    raise RuntimeError('Distant review mesh exceeds its triangle budget or is invalid')

source_vertices = [Vector(row) for row in vertices]
tree = BVHTree.FromPolygons(source_vertices, faces.tolist(), all_triangles=True)
atlas = mesh.uv_layers.active.data
uv = np.empty((len(triangles) * 3, 2), dtype=np.float32)
projection_errors = []
for i, face in enumerate(triangles):
    points = [Vector(reduced[index]) for index in face]
    hit, _, index, distance = tree.find_nearest(sum(points, Vector()) / 3)
    if index is None: raise RuntimeError('Cannot project the distant pine atlas')
    source_triangle = mesh.loop_triangles[index]
    a, b, c = (source_vertices[j] for j in source_triangle.vertices)
    ta, tb, tc = (Vector((*atlas[j].uv, 0)) for j in source_triangle.loops)
    projection_errors.append(distance)
    for corner, point in enumerate(points):
        mapped = barycentric_transform(closest_point_on_tri(point, a, b, c), a, b, c, ta, tb, tc)
        uv[i * 3 + corner] = mapped.x, mapped.y
if not np.isfinite(uv).all(): raise RuntimeError('Invalid projected atlas')
ground_offset = float(vertices[:, 2].min() - reduced[:, 2].min())
reduced[:, 2] += ground_offset
raw = bpy.data.meshes.new('Review pine QEM normals')
raw.from_pydata(reduced.tolist(), [], triangles.tolist()); raw.update()
for face in raw.polygons: face.use_smooth = True
raw.calc_normals_split()
normals = np.empty(len(raw.loops) * 3, dtype=np.float32)
raw.loops.foreach_get('normal', normals); normals = normals.reshape(-1, 3)
# QEM returns coincident triangles, sometimes with distinct UVs or opposite
# winding. Blender's FBX importer otherwise merges those faces. Give only
# repeated geometric faces their own vertices while keeping every loop UV and
# normal; this avoids splitting every triangle into three unique vertices.
split_faces = triangles.copy(); split_vertices = reduced.tolist(); seen = set(); isolated = 0
for i, face in enumerate(triangles):
    key = tuple(sorted(int(index) for index in face))
    if key in seen:
        isolated += 1; first = len(split_vertices)
        split_vertices.extend(reduced[face].tolist()); split_faces[i] = (first, first + 1, first + 2)
    else: seen.add(key)
newmesh = bpy.data.meshes.new('Review pine distant source-derived mesh')
newmesh.from_pydata(split_vertices, [], split_faces.tolist()); newmesh.update()
newmesh.uv_layers.new(name='Projected source atlas').data.foreach_set('uv', uv.ravel())
for face in newmesh.polygons: face.use_smooth = True
newmesh.use_auto_smooth = True; newmesh.normals_split_custom_set(normals.tolist())
for material in mesh.materials: newmesh.materials.append(material)
lod = bpy.data.objects.new('CE_pine_distance_review', newmesh)
bpy.context.scene.collection.objects.link(lod)
lod.matrix_world = near.matrix_world.copy()

original = bpy.data.objects['geometry_0']; baseline = bpy.data.objects['CE_pine_LOD1']
corners = [original.matrix_world @ Vector(point) for point in original.bound_box]
low = Vector(tuple(min(point[i] for point in corners) for i in range(3)))
high = Vector(tuple(max(point[i] for point in corners) for i in range(3)))
centre = Vector(((low.x + high.x) / 2, (low.y + high.y) / 2, low.z))
normalization = Matrix.Scale(10 / (high.z - low.z), 4) @ Matrix.Translation(-centre) @ original.matrix_world
samples = [normalization @ original.data.vertices[i].co for i in range(0, len(original.data.vertices), 97)
           if (normalization @ original.data.vertices[i].co).z < 3.5]
if len(samples) < 50: raise RuntimeError('Too few lower-trunk samples')
def distances(obj):
    obj.data.calc_loop_triangles()
    points = [obj.matrix_world @ v.co for v in obj.data.vertices]
    polys = [tuple(tri.vertices) for tri in obj.data.loop_triangles]
    surface = BVHTree.FromPolygons(points, polys, all_triangles=True)
    values = sorted(surface.find_nearest(point)[3] for point in samples)
    if not all(math.isfinite(value) for value in values): raise RuntimeError('Invalid surface distance')
    return dict(p95=values[math.ceil((len(values)-1)*.95)], maximum=values[-1])
comparison = dict(samples=len(samples), baseline=distances(baseline), candidate=distances(lod))
if comparison['candidate']['p95'] >= comparison['baseline']['p95']:
    raise RuntimeError('Distant candidate does not improve sampled root/trunk fidelity')

bpy.ops.object.select_all(action='DESELECT'); lod.select_set(True); bpy.context.view_layer.objects.active = lod
bpy.ops.export_scene.fbx(filepath=str(export), use_selection=True, object_types={'MESH'},
    axis_forward='-Z', axis_up='Y', bake_anim=False, add_leaf_bones=False,
    path_mode='STRIP', use_mesh_modifiers=True)
# A compact editable blend contains the candidate and its packed source material;
# its input blend and generation settings are preserved independently.
for obj in list(bpy.data.objects):
    if obj != lod: bpy.data.objects.remove(obj, do_unlink=True)
text = bpy.data.texts.get('SOURCE - prepare_pine_distance_review.py') or bpy.data.texts.new('SOURCE - prepare_pine_distance_review.py')
text.clear(); text.write(Path(__file__).read_text(encoding='utf-8'))
bpy.ops.wm.save_as_mainfile(filepath=str(editable), compress=True)
if sha(source) != source_hash: raise RuntimeError('Near review source changed')
report = dict(scope='Distant review candidate; game-camera and performance unverified',
    source=source.name, sourceSha256=source_hash, editable=editable.name, editableSha256=sha(editable),
    export=export.name, exportSha256=sha(export), triangles=len(triangles), vertices=len(split_vertices),
    qemVertices=len(reduced), coincidentFacesIsolated=isolated,
    simplifier='fast-simplification 0.1.12', targetTriangles=30000, aggressiveness=5,
    groundOffset=ground_offset, projectionDistanceP95=float(np.percentile(projection_errors,95)),
    lowerTrunkDistanceMetres=comparison)
(root/'distance-report.json').write_text(json.dumps(report, indent=2)+'\n', encoding='utf-8')
print('PINE_DISTANCE_REVIEW_PASS', json.dumps(report), flush=True)
