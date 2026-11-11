"""Export the isolated review candidate, not the game's active dog prefab."""
import bpy
from pathlib import Path

root = Path(__file__).resolve().parent
bpy.ops.wm.open_mainfile(filepath=str(root / 'RetargetedCandidate.blend'))
rig = bpy.data.objects['dogArmature']
dog = bpy.data.objects['geometry_0']
carrier = bpy.data.objects['Canine motion alignment']
# The downloaded reference hid its armature in the view layer. Hidden objects
# cannot be selected by the FBX exporter, which silently produces a static mesh.
rig.hide_viewport = False
rig.hide_set(False)
rig.data.pose_position = 'POSE'
bpy.context.scene.frame_start = 1
bpy.context.scene.frame_end = 23
bpy.context.scene.render.fps = 24
bpy.ops.object.select_all(action='DESELECT')
for obj in (rig, dog, carrier): obj.select_set(True)
bpy.context.view_layer.objects.active = rig
bpy.ops.export_scene.fbx(filepath=str(root / 'Candidate.fbx'), use_selection=True,
                         object_types={'EMPTY', 'ARMATURE', 'MESH'}, add_leaf_bones=False,
                         bake_anim=True, bake_anim_use_all_actions=False,
                         bake_anim_use_nla_strips=False, bake_anim_simplify_factor=0,
                         axis_forward='-Z', axis_up='Y', path_mode='COPY', embed_textures=True)
