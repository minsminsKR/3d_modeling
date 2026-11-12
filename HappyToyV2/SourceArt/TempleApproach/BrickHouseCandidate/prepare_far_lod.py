"""Derive the background LOD from the preserved prepared brick house.

Run Blender 4.2.23: blender -b --threads 2 --python-exit-code 1
--python prepare_far_lod.py. The GLB and editable source remain unchanged.
"""
from pathlib import Path
import hashlib
import bpy

root = Path(__file__).resolve().parent
source = root / 'BrickHouse.fbx'
expected = 'cd09dbe64fc2865705e4f624b9067ef8f5685c9dadecD228d084c0e7b3e60364'.lower()
if hashlib.sha256(source.read_bytes()).hexdigest() != expected:
    raise RuntimeError('Prepared house changed; review before deriving the LOD')
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=str(source))
meshes = [obj for obj in bpy.context.scene.objects if obj.type == 'MESH']
faces = sum(len(obj.data.polygons) for obj in meshes)
if not meshes or not faces: raise RuntimeError('Source house has no geometry')
ratio = min(1.0, 30000.0 / faces)
for obj in meshes:
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    if ratio < 1.0:
        modifier = obj.modifiers.new('Background face budget', 'DECIMATE')
        modifier.ratio = ratio
        bpy.ops.object.modifier_apply(modifier=modifier.name)
    obj.select_set(False)
print('FAR_BRICK_HOUSE_FACES', faces, sum(len(obj.data.polygons) for obj in meshes))
bpy.ops.object.select_all(action='SELECT')
bpy.ops.export_scene.fbx(filepath=str(root / 'BrickHouseFar.fbx'),
    use_selection=True, add_leaf_bones=False, bake_anim=False,
    axis_forward='-Z', axis_up='Y', path_mode='AUTO', embed_textures=False)
