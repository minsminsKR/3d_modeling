"""Keep the source editable and export a reduced static game mesh with UVs.

Run Blender 4.2.23 in background: blender -b --threads 2 --python-exit-code 1
--python prepare_freezer.py. The source GLB is generation/model.glb.
"""
from pathlib import Path
import bpy
from mathutils import Vector

root = Path(__file__).resolve().parent
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=str(root / 'model.glb'))
meshes = [obj for obj in bpy.context.scene.objects if obj.type == 'MESH']
if not meshes:
    raise RuntimeError('Storefront GLB has no mesh')
bpy.context.view_layer.update()
corners = [obj.matrix_world @ Vector(corner) for obj in meshes for corner in obj.bound_box]
lo = Vector(tuple(min(point[i] for point in corners) for i in range(3)))
hi = Vector(tuple(max(point[i] for point in corners) for i in range(3)))
height = hi.z - lo.z
if height < .001: raise RuntimeError('Storefront height is invalid')
holder = bpy.data.objects.new('Old Korean plaster house', None)
bpy.context.scene.collection.objects.link(holder)
for obj in [obj for obj in bpy.context.scene.objects if obj != holder and obj.parent is None]:
    obj.parent = holder
holder.scale = (1 / height,) * 3
holder.location = (-((lo.x + hi.x) / 2) / height, -((lo.y + hi.y) / 2) / height, -lo.z / height)
bpy.context.view_layer.update()
for mat in bpy.data.materials:
    if not mat.use_nodes: continue
    for node in mat.node_tree.nodes:
        if node.type != 'BSDF_PRINCIPLED': continue
        socket = node.inputs.get('Base Color')
        if socket and socket.is_linked:
            image_node = socket.links[0].from_node
            if image_node.type == 'TEX_IMAGE' and image_node.image and image_node.image.packed_file:
                (root / 'albedo.png').write_bytes(bytes(image_node.image.packed_file.data))
                break
if not (root / 'albedo.png').exists(): raise RuntimeError('No packed base-color image in source GLB')
source_faces = sum(len(obj.data.polygons) for obj in meshes)
print('BRICK_HOUSE_SOURCE', len(meshes), 'meshes', source_faces, 'faces', 'height', height)
bpy.ops.file.pack_all()
bpy.ops.wm.save_as_mainfile(filepath=str(root / 'PlasterHouseEditable.blend'))
ratio = min(1., 125000 / max(1, source_faces))
for obj in meshes:
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    if ratio < 1:
        modifier = obj.modifiers.new('Game face budget', 'DECIMATE')
        modifier.ratio = ratio
        bpy.ops.object.modifier_apply(modifier=modifier.name)
    obj.select_set(False)
print('BRICK_HOUSE_RUNTIME', sum(len(obj.data.polygons) for obj in meshes), 'faces')
bpy.ops.object.select_all(action='SELECT')
bpy.ops.export_scene.fbx(filepath=str(root / 'PlasterHouse.fbx'), use_selection=True,
    add_leaf_bones=False, bake_anim=False, axis_forward='-Z', axis_up='Y', path_mode='COPY', embed_textures=True)
