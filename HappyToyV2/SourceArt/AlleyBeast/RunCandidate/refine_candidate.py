import bpy
from pathlib import Path
p=Path(__file__).resolve().parent;bpy.ops.wm.open_mainfile(filepath=str(p/'retarget-intermediate.blend'))
rig=bpy.data.objects['dogArmature'];mesh=bpy.data.objects['geometry_0'];action=rig.animation_data.action
for curve in action.fcurves:
 if 'pose.bones[' not in curve.data_path or not curve.data_path.endswith('rotation_quaternion'):continue
 for key in curve.keyframe_points:
  for handle in (key.co,key.handle_left,key.handle_right):
   handle.y=(1+(handle.y-1)*.6) if curve.array_index==0 else handle.y*.6
bpy.ops.object.select_all(action='DESELECT');mesh.select_set(True);bpy.context.view_layer.objects.active=mesh
bpy.ops.object.vertex_group_limit_total(group_select_mode='ALL',limit=4);bpy.ops.object.vertex_group_normalize_all(group_select_mode='ALL',lock_active=False)
totals=[sum(g.weight for g in v.groups) for v in mesh.data.vertices];print('NORMALIZED',min(totals),max(totals),'max_bones',max(len(v.groups) for v in mesh.data.vertices),flush=True)
bpy.context.scene.frame_set(1);bpy.ops.wm.save_as_mainfile(filepath=str(p/'RetargetedCandidate.blend'))
