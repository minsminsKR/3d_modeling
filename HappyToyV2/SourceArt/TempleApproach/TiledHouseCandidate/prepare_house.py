"""Preserve the generated model and derive two static background LODs.

Run in Blender 4.2.23 with --background --threads 2 --python-exit-code 1.
"""
from pathlib import Path
import hashlib
import bpy
from mathutils import Vector

root=Path(__file__).resolve().parent
source=root/'model.glb'
if hashlib.sha256(source.read_bytes()).hexdigest()!='c88d4eabab1c4d8c3e5b6b8efa8aafd689db64925d09dc986dc805785d520f33':
    raise RuntimeError('Reviewed generated house input changed')
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=str(source))
meshes=[obj for obj in bpy.context.scene.objects if obj.type=='MESH']
if not meshes: raise RuntimeError('Generated house has no mesh')
bpy.context.view_layer.update()
corners=[obj.matrix_world@Vector(corner) for obj in meshes for corner in obj.bound_box]
lo=Vector(tuple(min(point[i] for point in corners) for i in range(3)))
hi=Vector(tuple(max(point[i] for point in corners) for i in range(3)))
height=hi.z-lo.z
if height<.001: raise RuntimeError('Invalid building height')
holder=bpy.data.objects.new('Old Korean tiled roof house',None)
bpy.context.scene.collection.objects.link(holder)
for obj in [obj for obj in bpy.context.scene.objects if obj!=holder and obj.parent is None]: obj.parent=holder
holder.scale=(1/height,)*3
holder.location=(-((lo.x+hi.x)/2)/height,-((lo.y+hi.y)/2)/height,-lo.z/height)
bpy.context.view_layer.update()
albedo=None
for mat in bpy.data.materials:
    if not mat.use_nodes: continue
    for node in mat.node_tree.nodes:
        if node.type!='BSDF_PRINCIPLED': continue
        socket=node.inputs.get('Base Color')
        if socket and socket.is_linked:
            texture=socket.links[0].from_node
            if texture.type=='TEX_IMAGE' and texture.image and texture.image.packed_file:
                albedo=bytes(texture.image.packed_file.data)
if not albedo: raise RuntimeError('Original packed house albedo is missing')
(root/'albedo.png').write_bytes(albedo)
for obj in meshes:
    if not obj.data.uv_layers: raise RuntimeError('Generated house has no UVs')
bpy.ops.file.pack_all()
bpy.context.preferences.filepaths.save_version=0
bpy.ops.wm.save_as_mainfile(filepath=str(root/'TiledHouseEditable.blend'))
def reduce_to(budget):
    faces=sum(len(obj.data.polygons) for obj in meshes)
    ratio=min(1.,budget/max(faces,1))
    for obj in meshes:
        bpy.context.view_layer.objects.active=obj;obj.select_set(True)
        if ratio<1:
            modifier=obj.modifiers.new('Background face budget','DECIMATE');modifier.ratio=ratio
            bpy.ops.object.modifier_apply(modifier=modifier.name)
        obj.select_set(False)
    print('HOUSE_LOD',faces,sum(len(obj.data.polygons) for obj in meshes),flush=True)
def export(name):
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.export_scene.fbx(filepath=str(root/name),use_selection=True,add_leaf_bones=False,
        bake_anim=False,axis_forward='-Z',axis_up='Y',path_mode='AUTO',embed_textures=False)
reduce_to(250000);export('TiledHouseHero.fbx')
reduce_to(125000);export('TiledHouseHigh.fbx')
reduce_to(60000);export('TiledHouseMid.fbx')
reduce_to(30000);export('TiledHouseFar.fbx')
