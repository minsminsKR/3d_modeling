"""Derive the static background LOD from the already prepared 100k-face FBX.

Run with Blender 4.2.23: blender -b --threads 2 --python-exit-code 1
--python prepare_far_lod.py. Original editable source stays untouched.
"""
from pathlib import Path
import bpy

root = Path(__file__).resolve().parent
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=str(root / 'SlateHouse.fbx'))
meshes = [obj for obj in bpy.context.scene.objects if obj.type == 'MESH']
faces = sum(len(obj.data.polygons) for obj in meshes)
if not meshes or not faces:
    raise RuntimeError('Source house has no geometry')
ratio = min(1.0, 30000.0 / faces)
for obj in meshes:
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    if ratio < 1.0:
        modifier = obj.modifiers.new('Background face budget', 'DECIMATE')
        modifier.ratio = ratio
        bpy.ops.object.modifier_apply(modifier=modifier.name)
    obj.select_set(False)
print('FAR_SLATE_HOUSE_FACES', faces, sum(len(obj.data.polygons) for obj in meshes))
bpy.ops.object.select_all(action='SELECT')
bpy.ops.export_scene.fbx(filepath=str(root / 'SlateHouseFar.fbx'),
    use_selection=True, add_leaf_bones=False, bake_anim=False,
    axis_forward='-Z', axis_up='Y', path_mode='AUTO', embed_textures=False)
