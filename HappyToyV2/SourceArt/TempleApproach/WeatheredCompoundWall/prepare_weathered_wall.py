"""Normalize and derive a game mesh from the retained generated wall GLB.

Run in Blender 4.2.23 with --background --threads 2 --python-exit-code 1.
The source GLB and its packed PBR textures are retained unmodified.
"""
from pathlib import Path
import hashlib
import bpy
from mathutils import Vector

root=Path(__file__).resolve().parent
expected='7dc0d933305dea1b99ff6287304fb10981846294f19989f87427a92b7dbeaf45'
source=root/'model.glb'
if hashlib.sha256(source.read_bytes()).hexdigest()!=expected:
    raise RuntimeError('Generated wall input differs from the reviewed GLB')
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=str(source))
meshes=[obj for obj in bpy.context.scene.objects if obj.type=='MESH']
if not meshes: raise RuntimeError('Generated wall has no mesh')
bpy.context.view_layer.update()
corners=[obj.matrix_world @ Vector(corner) for obj in meshes for corner in obj.bound_box]
lo=Vector(tuple(min(point[i] for point in corners) for i in range(3)))
hi=Vector(tuple(max(point[i] for point in corners) for i in range(3)))
height=hi.z-lo.z
if height < .001: raise RuntimeError('Invalid wall bounds')
holder=bpy.data.objects.new('Weathered Korean compound wall',None)
bpy.context.scene.collection.objects.link(holder)
for obj in [obj for obj in bpy.context.scene.objects if obj!=holder and obj.parent is None]: obj.parent=holder
holder.scale=(1/height,)*3
holder.location=(-((lo.x+hi.x)/2)/height,-((lo.y+hi.y)/2)/height,-lo.z/height)
bpy.context.view_layer.update()
albedo=None; metallic_roughness=None
for mat in bpy.data.materials:
    if not mat.use_nodes: continue
    for node in mat.node_tree.nodes:
        if node.type=='BSDF_PRINCIPLED':
            socket=node.inputs.get('Base Color')
            if socket and socket.is_linked:
                tex=socket.links[0].from_node
                if tex.type=='TEX_IMAGE' and tex.image and tex.image.packed_file:
                    albedo=bytes(tex.image.packed_file.data)
        if node.type=='TEX_IMAGE' and node.image and node.image.packed_file and any(
                link.to_node.type=='SEPARATE_COLOR' for output in node.outputs for link in output.links):
            metallic_roughness=bytes(node.image.packed_file.data)
if not albedo or not metallic_roughness: raise RuntimeError('Original packed PBR textures are missing')
(root/'albedo.png').write_bytes(albedo)
(root/'metallic-roughness.png').write_bytes(metallic_roughness)
source_faces=sum(len(obj.data.polygons) for obj in meshes)
for obj in meshes:
    if not obj.data.uv_layers: raise RuntimeError('Generated wall has no texture coordinates')
bpy.ops.file.pack_all()
bpy.context.preferences.filepaths.save_version=0
bpy.ops.wm.save_as_mainfile(filepath=str(root/'WeatheredWallEditable.blend'))
ratio=min(1.,60000/max(1,source_faces))
for obj in meshes:
    bpy.context.view_layer.objects.active=obj; obj.select_set(True)
    if ratio < 1:
        modifier=obj.modifiers.new('Game face budget','DECIMATE');modifier.ratio=ratio
        bpy.ops.object.modifier_apply(modifier=modifier.name)
    obj.select_set(False)
bpy.ops.object.select_all(action='SELECT')
bpy.ops.export_scene.fbx(filepath=str(root/'WeatheredWall.fbx'),use_selection=True,
    add_leaf_bones=False,bake_anim=False,axis_forward='-Z',axis_up='Y',path_mode='AUTO',embed_textures=False)
print('WEATHERED_WALL',source_faces,sum(len(obj.data.polygons) for obj in meshes),'height',height,flush=True)
