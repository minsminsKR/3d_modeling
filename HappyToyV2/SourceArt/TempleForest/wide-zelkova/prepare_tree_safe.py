"""Prepare a source-preserving alternative without collapse decimation.

The previous collapse-LOD can stretch triangles across branch gaps. This
candidate retains original topology nearby and uses only low-angle planar
dissolve at distance. It never overwrites the original GLB, earlier blend or
previous candidate exports. Blender 4.0.2; run in a background process.
"""
import json
import math
from pathlib import Path

import bpy
import bmesh
import numpy as np
from mathutils import Matrix, Vector

root = Path(__file__).resolve().parent
out = root / 'safe-export'
out.mkdir(exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.context.preferences.filepaths.save_version = 0
bpy.ops.import_scene.gltf(filepath=str(root / 'original.glb'))
originals = list(bpy.context.scene.objects)
meshes = [obj for obj in originals if obj.type == 'MESH']
if len(meshes) != 1:
    raise RuntimeError('Expected one source mesh')
source = meshes[0]
positions = [source.matrix_world @ vertex.co for vertex in source.data.vertices]
low = Vector(tuple(min(point[i] for point in positions) for i in range(3)))
high = Vector(tuple(max(point[i] for point in positions) for i in range(3)))
centre = Vector(((low.x + high.x) * .5, (low.y + high.y) * .5, low.z))
normalization = Matrix.Scale(8 / (high.z - low.z), 4) @ Matrix.Translation(-centre)
originals_collection = bpy.data.collections.new('Complete unchanged webapp original')
bpy.context.scene.collection.children.link(originals_collection)
for obj in originals:
    for collection in list(obj.users_collection):
        collection.objects.unlink(obj)
    originals_collection.objects.link(obj)
game_collection = bpy.data.collections.new('Editable source-preserving candidates')
bpy.context.scene.collection.children.link(game_collection)
near = source.copy()
near.data = source.data.copy()
near.parent = None
near.matrix_world = normalization @ source.matrix_world
near.name = 'CE_wide-zelkova-safe_LOD0'
game_collection.objects.link(near)
bpy.ops.object.select_all(action='DESELECT')
near.select_set(True)
bpy.context.view_layer.objects.active = near
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
far = near.copy()
far.data = near.data.copy()
far.name = 'CE_wide-zelkova-safe_LOD1'
game_collection.objects.link(far)
bpy.ops.object.select_all(action='DESELECT')
far.select_set(True)
bpy.context.view_layer.objects.active = far
modifier = far.modifiers.new('Source-preserving planar reduction', 'DECIMATE')
modifier.decimate_type = 'DISSOLVE'
modifier.angle_limit = math.radians(3)
modifier.delimit = {'MATERIAL', 'UV'}
bpy.ops.object.modifier_apply(modifier=modifier.name)
modifier = far.modifiers.new('Explicit triangles', 'TRIANGULATE')
bpy.ops.object.modifier_apply(modifier=modifier.name)
def repair_zero_normals(obj):
    mesh = obj.data
    mesh.calc_normals_split()
    values = np.empty(len(mesh.loops) * 3, dtype=np.float32)
    mesh.loops.foreach_get('normal', values)
    values = values.reshape(-1, 3)
    invalid = set(np.where(np.linalg.norm(values, axis=1) < 1e-6)[0].tolist())
    original_count = len(invalid)
    if invalid:
        bad_faces = [face.index for face in mesh.polygons if any(index in invalid for index in face.loop_indices)]
        bm = bmesh.new()
        bm.from_mesh(mesh)
        bm.faces.ensure_lookup_table()
        edges = {edge for index in bad_faces for edge in bm.faces[index].edges}
        bmesh.ops.split_edges(bm, edges=list(edges))
        bm.to_mesh(mesh)
        bm.free()
        mesh.update()
        mesh.calc_normals_split()
        values = np.empty(len(mesh.loops) * 3, dtype=np.float32)
        mesh.loops.foreach_get('normal', values)
        values = values.reshape(-1, 3)
        invalid = set(np.where(np.linalg.norm(values, axis=1) < 1e-6)[0].tolist())
    for face in mesh.polygons:
        for index in face.loop_indices:
            if index in invalid:
                if face.normal.length < 1e-6:
                    raise RuntimeError('Zero normal cannot be repaired from a degenerate face')
                values[index] = face.normal[:]
    if invalid:
        mesh.use_auto_smooth = True
        mesh.normals_split_custom_set(values.tolist())
        mesh.calc_normals_split()
        if any(loop.normal.length < 1e-6 for loop in mesh.loops):
            raise RuntimeError('Zero normal remained after local repair')
    return original_count


results = []
for index, obj in enumerate((near, far)):
    repaired = repair_zero_normals(obj)
    bpy.ops.object.select_all(action='DESELECT')
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    obj.data.calc_loop_triangles()
    path = out / ('wide-zelkova-safe-lod' + str(index) + '.fbx')
    bpy.ops.export_scene.fbx(filepath=str(path), use_selection=True, object_types={'MESH'},
                             axis_forward='-Z', axis_up='Y', bake_anim=False,
                             add_leaf_bones=False, path_mode='STRIP', use_mesh_modifiers=True)
    results.append(dict(file=path.name, triangles=len(obj.data.loop_triangles), vertices=len(obj.data.vertices),
                        zeroNormalsRepaired=repaired))
bpy.ops.file.pack_all()
originals_collection.hide_render = True
originals_collection.hide_viewport = True
far.hide_render = True
far.hide_set(True)
text = bpy.data.texts.new('SOURCE - prepare_tree_safe.py')
text.write(Path(__file__).read_text(encoding='utf-8'))
bpy.ops.wm.save_as_mainfile(filepath=str(root / 'wide-zelkova-safe.blend'), compress=True)
(root / 'safe-export.json').write_text(json.dumps(dict(scope='Raw source near; 3-degree UV-delimited dissolve far; pending reimport and surface checks',
    metres=8, low=list(low), high=list(high), lods=results), indent=2) + '\n', encoding='utf-8')
print('WIDE_SAFE_PREPARED', json.dumps(results), flush=True)
