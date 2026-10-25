"""Blender 4.0.2 CPU authoring of the retained player hand, not a new base model.

Run from repository root:
  blender --background --threads 2 --python-exit-code 1 --python this_file.py
The editable Actions are the source of the sampled Unity pose/clip JSON. No GPU,
desktop input, live user scene, or rejected PlayerHandsV2 resources are used.
"""
from __future__ import annotations
import hashlib
import json
import math
from pathlib import Path
import bpy
from mathutils import Euler, Matrix, Vector

ROOT = Path(__file__).resolve().parent
PROJECT = ROOT.parents[1]
SOURCE = ROOT.parent / 'PlayerItems' / 'hand.blend'
OUT = PROJECT / 'Assets/Resources/PlayerHandAnimation'
OUT.mkdir(parents=True, exist_ok=True)
ROOT.mkdir(parents=True, exist_ok=True)
DIGITS = ('thumb', 'index', 'middle', 'ring', 'little')
BONE_NAMES = ('forearm', 'wrist', 'palm') + tuple(f'{d}_{i}' for d in DIGITS for i in range(1, 4))
FPS = 60
SAMPLE_FPS = 30

def sha(path): return hashlib.sha256(path.read_bytes()).hexdigest()
def vector(value): return dict(zip(('x', 'y', 'z'), (round(float(x), 7) for x in value)))
def angles(curl=0., spread=0., twist=0.):
    return {'curl': float(curl), 'spread': float(spread), 'twist': float(twist)}
def lerp(a, b, t): return {k: a[k] + (b[k] - a[k]) * t for k in a}
def pose_copy(p): return {n: dict(v) for n, v in p.items()}
def bone_values(p): return [{'name': n, **{k: round(v, 5) for k, v in p[n].items()}} for n in BONE_NAMES]

def limits(name):
    if name == 'forearm': return dict(curlMin=-12., curlMax=20., spreadMin=-12., spreadMax=12., twistMin=-28., twistMax=28.)
    if name == 'wrist': return dict(curlMin=-22., curlMax=25., spreadMin=-15., spreadMax=15., twistMin=-12., twistMax=12.)
    if name == 'palm': return dict(curlMin=-5., curlMax=7., spreadMin=-3., spreadMax=3., twistMin=-3., twistMax=3.)
    digit, joint = name.rsplit('_', 1); joint = int(joint)
    if digit == 'thumb':
        return dict(curlMin=-32. if joint == 1 else 0., curlMax=(48., 68., 48.)[joint-1],
                    spreadMin=-60. if joint == 1 else 0., spreadMax=26. if joint == 1 else 0.,
                    twistMin=-68. if joint == 1 else 0., twistMax=68. if joint == 1 else 0.)
    return dict(curlMin=0., curlMax=(70., 82., 52.)[joint-1], spreadMin=-15. if joint == 1 else 0.,
                spreadMax=15. if joint == 1 else 0., twistMin=0., twistMax=0.)

def clamp_pose(p):
    for name in p:
        bound = limits(name)
        for key in ('curl', 'spread', 'twist'):
            p[name][key] = max(bound[key+'Min'], min(bound[key+'Max'], p[name][key]))
    return p

def neutral():
    p = {name: angles() for name in BONE_NAMES}
    for digit in DIGITS:
        for i, curl in enumerate((8., 16., 9.), 1):
            p[f'{digit}_{i}']['curl'] = curl
        p[f'{digit}_1']['spread'] = {'thumb': -25., 'index': -2., 'middle': 0., 'ring': 2., 'little': 4.}[digit]
    p['thumb_1'].update(curl=12., twist=-12.)
    p['thumb_2']['curl'] = 22.
    p['thumb_3']['curl'] = 12.
    return p

def blend_pose(a, b, t): return clamp_pose({n: lerp(a[n], b[n], t) for n in BONE_NAMES})

bpy.ops.wm.open_mainfile(filepath=str(SOURCE))
rig = next(o for o in bpy.context.scene.objects if o.type == 'ARMATURE')
skin = next(o for o in bpy.context.scene.objects if o.type == 'MESH' and any(m.type == 'ARMATURE' for m in o.modifiers))
for key in ('socketX','socketY','socketZ','socketRX','socketRY','socketRZ'):
    rig[key] = float(0.)
original_coordinates = [tuple(v.co) for v in skin.data.vertices]
original_uv = hashlib.sha256(b''.join(float(x).hex().encode() for loop in skin.data.uv_layers.active.data for x in loop.uv)).hexdigest()
old_wrist_head = rig.data.bones['wrist'].head_local.copy()
old_wrist_tail = rig.data.bones['wrist'].tail_local.copy()
palmar = Vector((0, -1, 0))

# The retained mesh already includes a forearm. Give its proximal and wrist
# portions independent control without manufacturing a replacement surface.
bpy.ops.object.select_all(action='DESELECT')
rig.select_set(True); bpy.context.view_layer.objects.active = rig
bpy.ops.object.mode_set(mode='EDIT')
wrist = rig.data.edit_bones['wrist']
forearm = rig.data.edit_bones.new('forearm')
forearm.head = old_wrist_head
forearm.tail = Vector((old_wrist_tail.x, old_wrist_tail.y, -.035))
forearm.align_roll(palmar)
wrist.head = forearm.tail
wrist.parent = forearm
wrist.align_roll(palmar)
bpy.ops.object.mode_set(mode='OBJECT')
forearm_group = skin.vertex_groups.new(name='forearm')
wrist_group = skin.vertex_groups['wrist']
for v in skin.data.vertices:
    weight = next((g.weight for g in v.groups if g.group == wrist_group.index), 0.)
    if weight <= 0.: continue
    t = max(0., min(1., (v.co.z + .065) / .065)); t = t*t*(3.-2.*t)
    proximal = weight * (1.-t)
    if proximal > 1e-8: forearm_group.add([v.index], proximal, 'REPLACE')
    if weight*t > 1e-8: wrist_group.add([v.index], weight*t, 'REPLACE')
    else: wrist_group.remove([v.index])

for modifier in skin.modifiers:
    if modifier.type == 'ARMATURE': modifier.use_deform_preserve_volume = False
rest = {b.name: b.matrix_local.copy() for b in rig.data.bones}
local_rest = {b.name: rest[b.parent.name].inverted() @ rest[b.name] if b.parent else rest[b.name] for b in rig.data.bones}

def local_rotation(value):
    # Same order as Unity: rest * Rz(spread) * Ry(opposition) * Rx(flexion).
    # FBX's handedness changes the signs of Y/Z; X flexion is preserved.
    return Euler(tuple(math.radians(x) for x in (value['curl'], -value['twist'], -value['spread'])), 'XYZ').to_matrix().to_4x4()

def matrices(p):
    result = {}
    for b in rig.data.bones:
        matrix = local_rest[b.name] @ local_rotation(p.get(b.name, angles()))
        result[b.name] = result[b.parent.name] @ matrix if b.parent else matrix
    return result

def fingertip(p, digit):
    n = digit+'_3'
    return matrices(p)[n] @ Vector((0, rig.data.bones[n].length, 0))

def solve(p, digit, target):
    variables = [(f'{digit}_{i}', 'curl') for i in range(1, 4)]
    if digit == 'thumb': variables += [('thumb_1', 'spread'), ('thumb_1', 'twist')]
    else: variables += [(digit+'_1', 'spread')]
    prior = pose_copy(p)
    def cost():
        posture = sum((p[n][k]-prior[n][k])**2 for n, k in variables)
        return (fingertip(p, digit)-target).length_squared + posture*2e-10
    for step in (20., 10., 5., 2., 1., .4, .15):
        for _ in range(5):
            changed = False
            for name, key in variables:
                origin = p[name][key]; best = origin; lowest = cost(); bound = limits(name)
                for sign in (-1., 1.):
                    p[name][key] = max(bound[key+'Min'], min(bound[key+'Max'], origin+sign*step))
                    score = cost()
                    if score < lowest: lowest = score; best = p[name][key]
                p[name][key] = best; changed |= abs(best-origin) > 1e-8
            if not changed: break
    return (fingertip(p, digit)-target).length

poses = {'Relaxed': neutral()}
poses['Open'] = neutral()
for d in DIGITS:
    for i, curl in enumerate((3., 7., 4.), 1): poses['Open'][f'{d}_{i}']['curl'] = curl
poses['Open']['thumb_1'].update(curl=6., spread=-32., twist=-8.)
poses['Open']['thumb_2']['curl'] = 10.; poses['Open']['thumb_3']['curl'] = 6.
contacts = {}
for item in ('Flashlight', 'Snapper', 'Incense', 'Talisman', 'Trap'):
    p = neutral()
    if item == 'Flashlight':
        for d in DIGITS[1:]:
            for i,curl in enumerate((28., 48., 28.),1): p[f'{d}_{i}']['curl']=curl
        p['thumb_1'].update(curl=-12., spread=-28., twist=-35.)
        p['thumb_2']['curl']=42.;p['thumb_3']['curl']=26.
        center = rig.data.bones['palm_contact'].head_local + Vector((0,-.025,.020))
        targets = {d: Vector((rig.data.bones[d+'_1'].head_local.x,center.y-.031,center.z-.006)) for d in DIGITS[1:]}
        targets['thumb'] = center + Vector((.030,.016,.025))
    elif item in ('Snapper','Incense','Talisman'):
        pinch = Vector((.018,-.067,.150 if item=='Snapper' else .162))
        separation=.010 if item=='Snapper' else .007 if item=='Incense' else .006
        targets={'thumb':pinch+Vector((separation,0,0)), 'index':pinch+Vector((-separation,0,0))}
        if item=='Incense':targets['middle']=pinch+Vector((-.012,.003,-.009))
        elif item=='Snapper':targets['middle']=pinch+Vector((-.012,.004,-.014))
        for d in ('ring','little'):
            for i,curl in enumerate((18.,30.,18.),1):p[f'{d}_{i}']['curl']=curl+(3. if d=='little' else 0.)
        if item=='Talisman':
            for i,curl in enumerate((18.,30.,18.),1):p[f'middle_{i}']['curl']=curl
        p['thumb_1'].update(curl=5.,spread=-35.,twist=-25.)
        p['thumb_2']['curl']=35.;p['thumb_3']['curl']=22.
    else:
        targets={}
        for d in DIGITS[1:]:
            cup={'index':0,'middle':2,'ring':4,'little':6}[d]
            for i,curl in enumerate((8.,31.,22.),1):p[f'{d}_{i}']['curl']=curl+cup
        p['thumb_1'].update(curl=14.,spread=-30.,twist=-10.)
        p['thumb_2']['curl']=25.;p['thumb_3']['curl']=17.
        p['palm']['curl']=2.
    residuals={d:solve(p,d,t) for d,t in targets.items()}
    poses[item]=clamp_pose(p)
    contacts[item]={'tipResidualMetres':residuals,
        'targets':{d:vector((t.x,t.z,-t.y)) for d,t in targets.items()},
        'actualTips':{d:vector((fingertip(p,d).x,fingertip(p,d).z,-fingertip(p,d).y)) for d in DIGITS}}

def set_pose(p):
    for name in BONE_NAMES:
        bone=rig.pose.bones[name]; value=p[name]
        bone.rotation_mode='XYZ'
        bone.rotation_euler=tuple(math.radians(x) for x in (value['curl'],-value['twist'],-value['spread']))

def insert_pose(p,time):
    set_pose(p)
    for name in BONE_NAMES:rig.pose.bones[name].keyframe_insert(data_path='rotation_euler',frame=1+time*FPS,group=name)

def insert_socket(position,rotation,time):
    for i,key in enumerate(('socketX','socketY','socketZ')):
        rig[key]=float(position[i]);rig.keyframe_insert(data_path='["'+key+'"]',frame=1+time*FPS,group='Socket offset')
    for i,key in enumerate(('socketRX','socketRY','socketRZ')):
        rig[key]=float(rotation[i]);rig.keyframe_insert(data_path='["'+key+'"]',frame=1+time*FPS,group='Socket rotation')
    # Preview the same translation/rotation on the armature object in Blender.
    # The custom fields retain exact Unity camera-axis values for the runtime.
    rig.location=(position[0],-position[2],position[1])
    rig.rotation_mode='XYZ'
    rig.rotation_euler=tuple(math.radians(x) for x in (rotation[0],-rotation[2],rotation[1]))
    rig.keyframe_insert(data_path='location',frame=1+time*FPS,group='Socket preview')
    rig.keyframe_insert(data_path='rotation_euler',frame=1+time*FPS,group='Socket preview')

def new_action(name):
    rig.animation_data_create();action=bpy.data.actions.new(name);action.use_fake_user=True;rig.animation_data.action=action
    return action

def smooth_curves(action):
    for curve in action.fcurves:
        for key in curve.keyframe_points:
            key.interpolation='BEZIER';key.handle_left_type='AUTO_CLAMPED';key.handle_right_type='AUTO_CLAMPED'

for name,p in poses.items():
    action=new_action('Grip_'+name);insert_pose(p,0);smooth_curves(action)

TIMINGS={'Flashlight':(.36,.16),'Snapper':(.85,.32),'Incense':(.65,.26),'Talisman':(.75,.30),'Trap':(1.15,.68)}
clip_specs=[]
for item,(duration,event) in TIMINGS.items():
    base=poses[item];name='Use'+item;action=new_action(name)
    insert_pose(base,0)
    if item=='Snapper':
        prepared=pose_copy(base);prepared['wrist']['curl']=-12.;prepared['forearm']['curl']=-5.
        insert_pose(prepared,.18);insert_pose(prepared,.25)
        # Different release times are authored independently; a single global
        # hand-open parameter would recreate the robotic simultaneous opening.
        for digit,delay in {'thumb':-.018,'index':0.,'middle':.018,'ring':.042,'little':.064}.items():
            for i in range(1,4):
                n=f'{digit}_{i}';b=rig.pose.bones[n]
                set_pose(prepared);b.keyframe_insert(data_path='rotation_euler',frame=1+(event+delay-.045)*FPS,group=n)
                val=poses['Open'][n];b.rotation_euler=tuple(math.radians(x) for x in (val['curl'],-val['twist'],-val['spread']))
                b.keyframe_insert(data_path='rotation_euler',frame=1+(event+delay+.05)*FPS,group=n)
        open_pose=pose_copy(poses['Open']);open_pose['wrist']['curl']=12.;open_pose['forearm']['curl']=12.
        insert_pose(open_pose,.47);insert_pose(base,duration)
        socket=[(0,(0,0,0),(0,0,0)),(.18,(-.012,-.022,-.05),(-12,0,6)),(.32,(.015,.012,.083),(9,0,-6)),(.47,(.023,-.008,.10),(12,0,-7)),(duration,(0,0,0),(0,0,0))]
    elif item=='Trap':
        place=pose_copy(base);place['forearm']['curl']=14.;place['wrist']['curl']=-7.
        insert_pose(place,event-.08);insert_pose(place,event)
        opened=blend_pose(place,poses['Open'],.65);insert_pose(opened,event+.13);insert_pose(base,duration)
        socket=[(0,(0,0,0),(0,0,0)),(.42,(0,-.095,.055),(12,0,-3)),(event,(0,-.185,.11),(21,0,-4)),(.84,(0,-.195,.105),(17,0,-2)),(duration,(0,0,0),(0,0,0))]
    elif item=='Flashlight':
        press=pose_copy(base);press['thumb_2']['curl']=min(68.,press['thumb_2']['curl']+7.);press['thumb_3']['curl']=min(48.,press['thumb_3']['curl']+3.)
        insert_pose(base,.06);insert_pose(press,event);insert_pose(base,duration)
        socket=[(0,(0,0,0),(0,0,0)),(event,(0,-.001,.001),(.4,0,.2)),(duration,(0,0,0),(0,0,0))]
    else:
        raised=pose_copy(base);raised['wrist']['curl']=-3.;raised['forearm']['curl']=3.
        insert_pose(raised,event);insert_pose(raised,event+.10);insert_pose(base,duration)
        height=.018 if item=='Incense' else .035
        socket=[(0,(0,0,0),(0,0,0)),(event,(.006,height,.009),(-3,2,-2)),(event+.10,(.006,height,.009),(-3,2,-2)),(duration,(0,0,0),(0,0,0))]
    for time,position,rotation in socket:insert_socket(position,rotation,time)
    smooth_curves(action);clip_specs.append((name,item,duration,event,action))
    for prefix,reverse in (('Equip',False),('Unequip',True)):
        action=new_action(prefix+item);length=.44
        open_pose=poses['Open'];insert_pose(base if reverse else open_pose,0)
        # Ring/little brace first, then index and finally thumb opposition.
        delays={'little':0.,'ring':.022,'middle':.043,'index':.064,'thumb':.095}
        for digit,delay in delays.items():
            for joint in range(1,4):
                n=f'{digit}_{joint}';b=rig.pose.bones[n]
                for time,value in ((.06+delay,base[n] if reverse else open_pose[n]),(.25+delay,open_pose[n] if reverse else base[n])):
                    b.rotation_euler=tuple(math.radians(x) for x in (value['curl'],-value['twist'],-value['spread']))
                    b.keyframe_insert(data_path='rotation_euler',frame=1+time*FPS,group=n)
        insert_pose(open_pose if reverse else base,length)
        for time,position,rotation in ((0,(0,-.095,0),(-8,0,5)),(length,(0,0,0),(0,0,0))):
            if reverse:time=length-time
            insert_socket(position,rotation,time)
        smooth_curves(action);clip_specs.append((prefix+item,item,length,-1.,action))

scene=bpy.context.scene;scene.render.fps=FPS
library={'version':1,'source':'SourceArt/PlayerItems/hand.blend','sourceSha256':sha(SOURCE),
         'angleConvention':'Unity rest-relative Rz(spread)*Ry(twist)*Rx(curl); degrees',
         'sampleRate':SAMPLE_FPS,'bones':[],'poses':[],'clips':[]}
for name in BONE_NAMES:
    bone=rig.data.bones[name]
    q=local_rest[name].to_quaternion()
    library['bones'].append({'name':name,'parent':bone.parent.name if bone.parent else '',
        'sourceBlenderRestLocalRotation':dict(zip(('x','y','z','w'),(q.x,q.y,q.z,q.w))),
        'expectedUnityRestLocalRotation':dict(zip(('x','y','z','w'),(q.x,-q.y,-q.z,q.w))),**limits(name)})
for name,p in poses.items():
    record={'name':name,'item':name if name not in ('Open','Relaxed') else '',
        'hand':'Left' if name=='Flashlight' else 'Right','bones':bone_values(p)}
    if name=='Flashlight':record['handleOffset']=vector((0,.020,.025));record['handleRadius']=.024
    library['poses'].append(record)
socket_curve_checks={}
for name,item,duration,event,action in clip_specs:
    rig.animation_data.action=action
    times=sorted(set([0.,duration]+[min(duration,i/SAMPLE_FPS) for i in range(math.ceil(duration*SAMPLE_FPS)+1)]+([event] if event>=0 else [])))
    frames=[]
    curve_error=0.
    for time in times:
        scene.frame_set(1+int(time*FPS),subframe=time*FPS-int(time*FPS))
        sampled={n:angles(math.degrees(rig.pose.bones[n].rotation_euler.x),-math.degrees(rig.pose.bones[n].rotation_euler.z),-math.degrees(rig.pose.bones[n].rotation_euler.y)) for n in BONE_NAMES}
        clamp_pose(sampled)
        frame=1+time*FPS
        def sample_curve(path,index=0):
            curve=action.fcurves.find(path,index=index)
            if curve is None:raise RuntimeError('Missing authored curve '+name+': '+path)
            return float(curve.evaluate(frame))
        # Sample saved floating FCurves directly. ID properties first authored
        # from integer zero would quantize all sub-metre motion to zero.
        position=[sample_curve('["socket'+c+'"]') for c in 'XYZ']
        rotation=[sample_curve('["socketR'+c+'"]') for c in 'XYZ']
        actual_location=[sample_curve('location',i) for i in range(3)]
        actual_rotation=[math.degrees(sample_curve('rotation_euler',i)) for i in range(3)]
        converted_location=(actual_location[0],actual_location[2],-actual_location[1])
        converted_rotation=(actual_rotation[0],actual_rotation[2],-actual_rotation[1])
        curve_error=max(curve_error,max(abs(a-b) for a,b in zip(position,converted_location)),
            max(abs(a-b) for a,b in zip(rotation,converted_rotation)))
        frames.append({'time':round(time,7),'position':vector(position),
            'rotationEuler':vector(rotation),'bones':bone_values(sampled)})
    socket_curve_checks[name]={'maximumTranslationMetres':max(abs(v) for f in frames for v in f['position'].values()),
        'maximumRotationDegrees':max(abs(v) for f in frames for v in f['rotationEuler'].values()),
        'maximumBlenderObjectCurveError':curve_error}
    if curve_error>1e-4:raise RuntimeError('Unity socket samples diverge from actual saved Blender object curves: '+name)
    library['clips'].append({'name':name,'item':item,'hand':'Left' if item=='Flashlight' else 'Right',
        'duration':duration,'releaseTime':event,'frames':frames})
if socket_curve_checks['UseSnapper']['maximumTranslationMetres']<.08:raise RuntimeError('Snapper authored reach was lost')
if socket_curve_checks['UseTrap']['maximumTranslationMetres']<.18:raise RuntimeError('Trap authored lowering was lost')
if socket_curve_checks['UseFlashlight']['maximumRotationDegrees']<.39:raise RuntimeError('Fractional flashlight press motion was lost')
for name in ('EquipFlashlight','EquipSnapper','EquipIncense','EquipTalisman','EquipTrap'):
    if socket_curve_checks[name]['maximumTranslationMetres']<.09:raise RuntimeError('Equip translation was lost: '+name)
rig.animation_data.action=None;set_pose(poses['Relaxed'])
for key in ('socketX','socketY','socketZ','socketRX','socketRY','socketRZ'):rig[key]=0.
rig.location=(0,0,0);rig.rotation_euler=(0,0,0)
scene.frame_set(1)
rig['animation_source']='PlayerHandAnimation authored Actions; saved keyframes sampled for Unity'
rig['anatomical_controls']='forearm (elbow-end swing/pronation), independent wrist, palm, MCP/PIP/DIP and thumb CMC/MCP/IP'

sums=[sum(g.weight for g in v.groups) for v in skin.data.vertices]
report={'source':SOURCE.relative_to(PROJECT).as_posix(), 'sourceSha256':sha(SOURCE),
        'triangles':sum(len(p.vertices)-2 for p in skin.data.polygons),'vertices':len(skin.data.vertices),
        'meshCoordinatesUnchanged':all(tuple(v.co)==old for v,old in zip(skin.data.vertices,original_coordinates)),
        'uvHashUnchanged':hashlib.sha256(b''.join(float(x).hex().encode() for loop in skin.data.uv_layers.active.data for x in loop.uv)).hexdigest()==original_uv,
        'maxWeightSumError':max(abs(t-1.) for t in sums),'unweightedVertices':sum(t<.999 for t in sums),
        'maxInfluences':max(sum(g.weight>1e-6 for g in v.groups) for v in skin.data.vertices),
        'forearmLengthMetres':rig.data.bones['forearm'].length,'wristLengthMetres':rig.data.bones['wrist'].length,
        'boneCount':len(rig.data.bones),'deformingBones':sum(b.use_deform for b in rig.data.bones),
        'authoredActionCount':len(poses)+len(clip_specs),'sampleRate':SAMPLE_FPS,'gripContacts':contacts,
        'socketCurveChecks':socket_curve_checks,
        'limitations':['Retains existing generated skin topology and textures; no claim of scanned anatomy.',
            'Tip-target residuals measure skeleton contact, not proof of zero full-surface penetration.',
            'Source forearm is present but upper-arm/shoulder body geometry is outside this viewmodel.']}
if not report['meshCoordinatesUnchanged'] or not report['uvHashUnchanged'] or report['unweightedVertices'] or report['maxWeightSumError']>1e-5 or report['maxInfluences']>4:
    raise RuntimeError('Retained hand surface or skin weight certification failed')

bpy.ops.object.select_all(action='DESELECT');rig.select_set(True);skin.select_set(True);bpy.context.view_layer.objects.active=rig
fbx=OUT/'geometry.fbx'
# FBX bake_anim=False still exports current pose transforms as its imported
# local bone bind basis. Exporting the Relaxed preview therefore applies that
# curl a second time when Unity samples a grip. Clear ALL pose bases for FBX,
# then restore Relaxed only for the editable Blender preview after export.
for bone in rig.pose.bones:bone.matrix_basis=Matrix.Identity(4)
rig.location=(0,0,0);rig.rotation_euler=(0,0,0)
bpy.context.view_layer.update()
neutral_error=max(abs(bone.matrix_basis[r][c]-(1. if r==c else 0.))
    for bone in rig.pose.bones for r in range(4) for c in range(4))
if neutral_error>1e-7:raise RuntimeError('FBX export must use true neutral bone bases, not a preview/grip pose')
report['neutralExport']={'allBoneMatrixBasisIdentity':True,'maximumIdentityError':neutral_error,
    'expectedUnityRestRotations':{b['name']:b['expectedUnityRestLocalRotation'] for b in library['bones']},
    'expectedParents':{b['name']:b['parent'] for b in library['bones']},
    'convention':'FBX local rest equals source Blender bind quaternion with Y/Z signs reversed; do not preapply Relaxed'}
bpy.ops.export_scene.fbx(filepath=str(fbx),use_selection=True,add_leaf_bones=False,bake_anim=False,axis_forward='-Z',axis_up='Y')
set_pose(poses['Relaxed']);bpy.context.view_layer.update()
bpy.ops.file.pack_all()
blend=ROOT/'hand-animation.blend'
bpy.ops.wm.save_as_mainfile(filepath=str(blend))
library['sourceBlendSha256']=sha(blend)
(OUT/'poses.json').write_text(json.dumps(library,indent=2)+'\n',encoding='utf-8')
report['outputs']={p.relative_to(PROJECT).as_posix():sha(p) for p in (blend,fbx,OUT/'poses.json')}
(ROOT/'authoring-report.json').write_text(json.dumps(report,indent=2)+'\n',encoding='utf-8')
print('HAND_ANIMATION_AUTHORING_PASS '+json.dumps({k:report[k] for k in ('vertices','triangles','boneCount','maxWeightSumError','maxInfluences','authoredActionCount')}))
