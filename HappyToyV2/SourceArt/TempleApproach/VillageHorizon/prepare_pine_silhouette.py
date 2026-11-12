"""Derive a trunk-preserving village silhouette from the existing pine FBX.

Blender 4.2.23, Python 3.11, fast-simplification 0.1.12. Set PYTHONPATH to
the installed package and run:
blender -b --threads 2 --python-use-system-env --python-exit-code 1 --python prepare_pine_silhouette.py
UVs are projected onto the nearest original triangle; the Unity material is
assigned from the existing pine surface at runtime.
"""
from pathlib import Path
import hashlib
import bpy
import numpy as np
import fast_simplification
from mathutils import Vector
from mathutils.bvhtree import BVHTree
from mathutils.geometry import barycentric_transform, closest_point_on_tri

root = Path(__file__).resolve().parent
project = root.parents[2]
source = project / 'Assets/Resources/TemplePrologue/pine-distance-review.fbx'
output = project / 'Assets/Resources/TemplePrologue/pine-village-silhouette.fbx'
expected = 'afab3402733d2bdc799e6f6a09fe03d9a904f1c158b19f6a6929ef79da644abb'
if hashlib.sha256(source.read_bytes()).hexdigest() != expected:
    raise RuntimeError('Pine source changed; review the silhouette recipe')
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.context.preferences.filepaths.save_version = 0
bpy.ops.import_scene.fbx(filepath=str(source))
meshes = [obj for obj in bpy.context.scene.objects if obj.type == 'MESH']
if len(meshes) != 1:
    raise RuntimeError('Expected one source pine mesh')
obj = meshes[0]
mesh = obj.data
mesh.calc_loop_triangles()
if len(mesh.loop_triangles) != 40697 or not mesh.uv_layers.active:
    raise RuntimeError('Unexpected source mesh or missing atlas')
vertices = np.empty(len(mesh.vertices) * 3, dtype=np.float32)
mesh.vertices.foreach_get('co', vertices)
vertices = vertices.reshape((-1, 3))
faces = np.empty(len(mesh.loop_triangles) * 3, dtype=np.int32)
mesh.loop_triangles.foreach_get('vertices', faces)
faces = faces.reshape((-1, 3))
welded, inverse = np.unique(np.round(vertices, 5), axis=0, return_inverse=True)
welded_faces = inverse[faces]
valid = (welded_faces[:, 0] != welded_faces[:, 1]) & (welded_faces[:, 1] != welded_faces[:, 2]) & (welded_faces[:, 2] != welded_faces[:, 0])
welded_faces = welded_faces[valid]
_, unique_indices = np.unique(np.sort(welded_faces, axis=1), axis=0, return_index=True)
welded_faces = welded_faces[np.sort(unique_indices)].astype(np.int32)
reduced, triangles = fast_simplification.simplify(welded.astype(np.float32), welded_faces, target_count=4000, agg=7)
valid = (triangles[:, 0] != triangles[:, 1]) & (triangles[:, 1] != triangles[:, 2]) & (triangles[:, 2] != triangles[:, 0])
triangles = triangles[valid]
_, unique_indices = np.unique(np.sort(triangles, axis=1), axis=0, return_index=True)
triangles = triangles[np.sort(unique_indices)]
print('SIMPLIFICATION_RESULT', len(reduced), len(triangles), flush=True)
if not 1000 <= len(triangles) <= 10000 or not np.isfinite(reduced).all():
    raise RuntimeError('Pine silhouette missed its budget')
tree = BVHTree.FromPolygons([Vector(v) for v in vertices], faces.tolist(), all_triangles=True)
atlas = mesh.uv_layers.active.data
uv = np.empty((len(triangles) * 3, 2), dtype=np.float32)
for i, face in enumerate(triangles):
    points = [Vector(reduced[index]) for index in face]
    centre = (points[0] + points[1] + points[2]) / 3
    _, _, index, _ = tree.find_nearest(centre)
    if index is None:
        raise RuntimeError('Cannot project pine atlas')
    tri = mesh.loop_triangles[index]
    a, b, c = (Vector(vertices[v]) for v in tri.vertices)
    ta, tb, tc = (Vector((*atlas[loop].uv, 0)) for loop in tri.loops)
    for corner, point in enumerate(points):
        mapped = barycentric_transform(closest_point_on_tri(point, a, b, c), a, b, c, ta, tb, tc)
        uv[i * 3 + corner] = mapped.x, mapped.y
if not np.isfinite(uv).all():
    raise RuntimeError('Invalid projected UVs')
reduced[:, 2] += float(vertices[:, 2].min() - reduced[:, 2].min())
# Preserve the original lower trunk. Aggressive QEM can otherwise collapse it
# into disconnected crowns even though the total mesh budget looks good.
cut = vertices[:, 2].min() + .52 * np.ptp(vertices[:, 2])
source_indices = np.flatnonzero(vertices[faces, 2].min(axis=1) < cut)
keep = reduced[triangles, 2].min(axis=1) >= cut
top_triangles = triangles[keep]
top_uv = uv.reshape((-1, 3, 2))[keep]
trunk_uv = np.empty((len(source_indices), 3, 2), dtype=np.float32)
for i, index in enumerate(source_indices):
    tri = mesh.loop_triangles[int(index)]
    for corner, loop in enumerate(tri.loops):
        trunk_uv[i, corner] = atlas[loop].uv
combined_vertices = np.concatenate((reduced, vertices), axis=0)
combined_faces = np.concatenate((top_triangles, faces[source_indices] + len(reduced)), axis=0)
combined_uv = np.concatenate((top_uv, trunk_uv), axis=0)
print('TRUNK_PRESERVED_FACES', len(source_indices), 'TOTAL', len(combined_faces), flush=True)
if len(combined_faces) > 13000:
    raise RuntimeError('Trunk-preserving mesh exceeded budget')
newmesh = bpy.data.meshes.new('Village distant pine silhouette')
newmesh.from_pydata(combined_vertices.tolist(), [], combined_faces.tolist()); newmesh.update()
newmesh.uv_layers.new(name='Projected source atlas').data.foreach_set('uv', combined_uv.ravel())
for polygon in newmesh.polygons:
    polygon.use_smooth = True
new = bpy.data.objects.new('Village distant pine silhouette', newmesh)
bpy.context.scene.collection.objects.link(new)
new.matrix_world = obj.matrix_world.copy()
bpy.data.objects.remove(obj, do_unlink=True)
for material in list(bpy.data.materials):
    bpy.data.materials.remove(material)
for image in list(bpy.data.images):
    bpy.data.images.remove(image)
bpy.context.view_layer.objects.active = new
bpy.ops.object.select_all(action='DESELECT'); new.select_set(True)
bpy.ops.wm.save_as_mainfile(filepath=str(root / 'VillageHorizonPineEditable.blend'))
bpy.ops.export_scene.fbx(filepath=str(output), use_selection=True, object_types={'MESH'},
    axis_forward='-Z', axis_up='Y', bake_anim=False, add_leaf_bones=False,
    path_mode='AUTO', embed_textures=False)
print('VILLAGE_PINE_FACES', len(faces), len(combined_faces), flush=True)
