"""Blender 4.0.2: add a head-only action to the existing reviewed rig.

Run from repository root:
blender --background --python-exit-code 1 --python HappyToyV2/SourceArt/BaeksugwiStudio/author_head_motion.py
The original game mesh, skin/PBR and all Run action curves are preserved.
A separate armature-only FBX supplies Head_Writhe; Unity copies only Head rotation.
"""
import hashlib,json,math,struct,sys
from pathlib import Path
import bpy
from mathutils import Quaternion,Vector

HERE=Path(__file__).resolve().parent
PROJECT=HERE.parents[1]
sys.path.insert(0,str(HERE))
from head_motion import make_beats,sample_degrees

def action_hash(action):
    digest=hashlib.sha256()
    for curve in action.fcurves:
        digest.update(curve.data_path.encode());digest.update(struct.pack('<i',curve.array_index))
        for key in curve.keyframe_points:
            digest.update(struct.pack('<6f',*key.co,*key.handle_left,*key.handle_right))
            digest.update(key.interpolation.encode())
    return digest.hexdigest()

bpy.ops.wm.open_mainfile(filepath=str(HERE/'Baeksugwi_Run.blend'))
rig=bpy.data.objects['Baeksugwi_Rig'];head=rig.pose.bones['Head']
profile=json.loads((HERE/'head-motion-profile.json').read_text(encoding='utf-8'))
original={a.name:action_hash(a) for a in bpy.data.actions if a.name.startswith('Run_')}
scene=bpy.context.scene;old_start,old_end=scene.frame_start,scene.frame_end
rig.animation_data.action=bpy.data.actions['Run_InPlace'];scene.frame_set(1)
base=head.rotation_quaternion.copy()
prior=bpy.data.actions.get('Head_Writhe')
if prior:bpy.data.actions.remove(prior)
action=bpy.data.actions.new('Head_Writhe');action.use_fake_user=True
rig.animation_data.action=action;head.rotation_mode='QUATERNION'
count=round(profile['duration_seconds']*profile['fps']);previous=None
beats=make_beats(profile)
local_axes=[head.bone.matrix_local.to_quaternion().inverted()@Vector(axis) for axis in ((0,0,1),(0,-1,0),(1,0,0))]
for step in range(count+1):
    yaw,roll,pitch=map(math.radians,sample_degrees(beats,step/profile['fps']))
    rotation=base@Quaternion(local_axes[0],yaw)@Quaternion(local_axes[1],roll)@Quaternion(local_axes[2],pitch)
    if previous and rotation.dot(previous)<0:rotation.negate()
    head.rotation_quaternion=rotation
    head.keyframe_insert(data_path='rotation_quaternion',frame=step+1,group='Head')
    previous=rotation.copy()
for curve in action.fcurves:
    for key in curve.keyframe_points:key.interpolation='LINEAR'
action['head_only']=True;action['loop']=True;action['duration_seconds']=count/profile['fps']
assert len(action.fcurves)==4 and all(c.data_path=='pose.bones["Head"].rotation_quaternion' for c in action.fcurves)
assert original=={a.name:action_hash(a) for a in bpy.data.actions if a.name in original}
scene.render.fps=profile['fps'];scene.frame_start=1;scene.frame_end=count+1;scene.frame_set(1)
bpy.ops.object.select_all(action='DESELECT');rig.select_set(True);bpy.context.view_layer.objects.active=rig
output=PROJECT/'Assets/Resources/Baeksugwi/Head_Writhe_Source.fbx'
# The take is armature-only. Avoid re-skinning the full source mesh on each
# FBX bake frame; restore these authoring settings before saving the source.
modifiers=[m for obj in scene.objects for m in obj.modifiers if m.type=='ARMATURE']
visibility=[(m,m.show_viewport,m.show_render) for m in modifiers]
for m,_,_ in visibility:m.show_viewport=m.show_render=False
print('BAEKSUGWI_HEAD_EXPORT_START',flush=True)
bpy.ops.export_scene.fbx(filepath=str(output),use_selection=True,object_types={'ARMATURE'},
    axis_forward='-Z',axis_up='Y',apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',global_scale=1,
    add_leaf_bones=False,use_armature_deform_only=True,bake_anim=True,
    bake_anim_use_all_actions=False,bake_anim_use_nla_strips=False,bake_anim_simplify_factor=0,bake_anim_step=1,
    path_mode='AUTO',embed_textures=False)
for m,viewport,render in visibility:m.show_viewport=viewport;m.show_render=render
scene.frame_start=old_start;scene.frame_end=old_end
rig.animation_data.action=bpy.data.actions['Run_InPlace'];scene.frame_set(1)
bpy.context.preferences.filepaths.save_version=0
bpy.ops.wm.save_as_mainfile(filepath=str(HERE/'Baeksugwi_Run.blend'),compress=True,check_existing=False)
report={'status':'PASS','action':'Head_Writhe','source':'Baeksugwi_Run.blend','sourceSHA256':hashlib.sha256((HERE/'Baeksugwi_Run.blend').read_bytes()).hexdigest(),'headOnlyRotationCurves':4,'durationSeconds':count/profile['fps'],'seed':profile['seed'],'beatsSecondsYawRollPitch':beats,'maximumAuthoredRollDegrees':max(abs(sample_degrees(beats,i/profile['fps'])[1]) for i in range(count+1)),'runActionCurveHashesPreserved':original,'fbx':output.relative_to(PROJECT).as_posix(),'fbxSHA256':hashlib.sha256(output.read_bytes()).hexdigest(),'scope':'Existing mesh/weights/texture data untouched; only a Head quaternion action replaced. Unity imports Head rotation curves only.'}
(HERE/'head-motion-report.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
print('BAEKSUGWI_HEAD_ACTION_PASS',flush=True)
