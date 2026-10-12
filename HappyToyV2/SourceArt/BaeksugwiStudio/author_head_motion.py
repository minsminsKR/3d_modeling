"""Blender 4.0.2: add a head-only action to the existing reviewed rig.

Run from repository root:
blender --background --python-exit-code 1 --python HappyToyV2/SourceArt/BaeksugwiStudio/author_head_motion.py
The original game mesh, skin/PBR and all Run action curves are preserved.
A separate armature-only FBX supplies Head_Writhe; Unity copies only Head rotation.
"""
import hashlib,json,math,struct,random
from pathlib import Path
import bpy
from mathutils import Quaternion,Vector

HERE=Path(__file__).resolve().parent
PROJECT=HERE.parents[1]

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
# This export contains armature only. Do not evaluate the full sculpt's skin at
# every FBX bake frame; preserve and restore all editable mesh visibility.
visibility=[(o,o.hide_viewport) for o in scene.objects if o.type=='MESH']
for obj,_ in visibility:obj.hide_viewport=True
rig.animation_data.action=bpy.data.actions['Run_InPlace'];scene.frame_set(1)
base=head.rotation_quaternion.copy()
prior=bpy.data.actions.get('Head_Writhe')
if prior:bpy.data.actions.remove(prior)
action=bpy.data.actions.new('Head_Writhe');action.use_fake_user=True
rig.animation_data.action=action;head.rotation_mode='QUATERNION'
count=round(profile['duration_seconds']*profile['fps']);previous=None
local_axes=[head.bone.matrix_local.to_quaternion().inverted()@Vector(axis) for axis in ((0,0,1),(0,-1,0),(1,0,0))]
# Deterministic, editable irregular targets; unlike a stack of sine waves these
# have unequal holds, sharp turns and wide roll on both sides of the face.
rng=random.Random(profile['seed']);targets=[(0.0,(0.0,0.0,0.0))]
time=.28
while time < profile['duration_seconds']-.75:
    roll=rng.choice((-1,1))*rng.uniform(.45,1.0)*profile['roll_radians']
    targets.append((time,(rng.uniform(-1,1)*profile['yaw_radians'],roll,rng.uniform(-1,1)*profile['pitch_radians'])))
    time+=rng.uniform(.18,.90)
targets.append((profile['duration_seconds'],(0.0,0.0,0.0)))
segment=0;peak_roll=0.0
for step in range(count+1):
    time=step/profile['fps']
    while segment+1<len(targets)-1 and time>targets[segment+1][0]:segment+=1
    start,first=targets[segment];end,last=targets[segment+1]
    u=min(1.0,max(0.0,(time-start)/(end-start)))
    # Move early then hold at the new uncomfortable angle. Holds vary with the
    # target spacing; cubic easing keeps rotations continuous even at the loop.
    u=min(1.0,u/.68);u=u*u*(3-2*u)
    yaw,roll,pitch=(a+(b-a)*u for a,b in zip(first,last))
    peak_roll=max(peak_roll,abs(roll))
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
output=PROJECT/'Assets/Resources/Baeksugwi/Head_Writhe.fbx'
bpy.ops.export_scene.fbx(filepath=str(output),use_selection=True,object_types={'ARMATURE'},
    axis_forward='-Z',axis_up='Y',apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',global_scale=1,
    add_leaf_bones=False,use_armature_deform_only=True,bake_anim=True,
    bake_anim_use_all_actions=False,bake_anim_use_nla_strips=False,bake_anim_simplify_factor=0,bake_anim_step=1,
    path_mode='AUTO',embed_textures=False)
scene.frame_start=old_start;scene.frame_end=old_end
rig.animation_data.action=bpy.data.actions['Run_InPlace'];scene.frame_set(1)
for obj,hidden in visibility:obj.hide_viewport=hidden
bpy.context.preferences.filepaths.save_version=0
bpy.ops.wm.save_as_mainfile(filepath=str(HERE/'Baeksugwi_Run.blend'),compress=True,check_existing=False)
report={'status':'PASS','action':'Head_Writhe','source':'Baeksugwi_Run.blend','sourceSHA256':hashlib.sha256((HERE/'Baeksugwi_Run.blend').read_bytes()).hexdigest(),'headOnlyRotationCurves':4,'durationSeconds':count/profile['fps'],'seed':profile['seed'],'peakRollDegrees':math.degrees(peak_roll),'irregularTargets':targets,'runActionCurveHashesPreserved':original,'fbx':output.relative_to(PROJECT).as_posix(),'fbxSHA256':hashlib.sha256(output.read_bytes()).hexdigest(),'scope':'Existing mesh/weights/texture data untouched; only a Head quaternion action added. Unity imports Head rotation curves only.'}
(HERE/'head-motion-report.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
print('BAEKSUGWI_HEAD_ACTION_PASS',flush=True)
