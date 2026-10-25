"""Opt-in Cycles CPU stills of the actual retained skin and authored grip Actions.

This is an isolated Blender review, not a Unity gameplay/FPS certification.
No GPU device, visible window, user scene, OS input or native player is used.
"""
import bpy
import json
import math
import sys
from pathlib import Path
from mathutils import Vector, Euler, Matrix

ROOT=Path(__file__).resolve().parent
PROJECT=ROOT.parents[1]
OUTPUT=PROJECT/'Builds/HandAnimationAuthoring/review'
OUTPUT.mkdir(parents=True,exist_ok=True)
bpy.ops.wm.open_mainfile(filepath=str(ROOT/'hand-animation.blend'))
library=json.loads((PROJECT/'Assets/Resources/PlayerHandAnimation/poses.json').read_text())
poses={p['name']:p for p in library['poses']}
rig=next(o for o in bpy.context.scene.objects if o.type=='ARMATURE')
rig.animation_data.action=None
skin=next(o for o in bpy.context.scene.objects if o.type=='MESH' and any(m.type=='ARMATURE' for m in o.modifiers))
for obj in list(bpy.context.scene.objects):
    if obj.type in ('CAMERA','LIGHT'):bpy.data.objects.remove(obj,do_unlink=True)

scene=bpy.context.scene
scene.render.engine='CYCLES';scene.cycles.device='CPU';scene.cycles.samples=12
scene.cycles.use_denoising=True
scene.render.threads_mode='FIXED';scene.render.threads=2
scene.render.resolution_x=640;scene.render.resolution_y=640;scene.render.resolution_percentage=100
scene.render.image_settings.file_format='PNG';scene.render.film_transparent=False
if not scene.world:scene.world=bpy.data.worlds.new('CPU review world')
scene.world.use_nodes=True;scene.world.node_tree.nodes['Background'].inputs['Color'].default_value=(.035,.045,.06,1)
scene.world.node_tree.nodes['Background'].inputs['Strength'].default_value=.25
scene.view_settings.view_transform='AgX'
camdata=bpy.data.cameras.new('CPU grip review');cam=bpy.data.objects.new('CPU grip review',camdata);scene.collection.objects.link(cam)
camdata.type='ORTHO';camdata.ortho_scale=.36;camdata.clip_start=.005;camdata.clip_end=10;scene.camera=cam

def aim(obj,target):obj.rotation_euler=(Vector(target)-obj.location).to_track_quat('-Z','Y').to_euler()
def area(name,position,power,size,colour):
    data=bpy.data.lights.new(name,'AREA');data.energy=power;data.shape='DISK';data.size=size;data.color=colour
    obj=bpy.data.objects.new(name,data);scene.collection.objects.link(obj);obj.location=position;aim(obj,(0,-.03,.1))
area('Softbox key',(.3,-.35,.48),16.,.45,(1.,.91,.83))
area('Softbox fill',(-.3,-.15,.20),5.,.4,(.73,.83,1.))
area('Dorsal rim',(.1,.30,.38),10.,.30,(.9,.95,1.))

def append_meshes(path,prefix=None,names=None):
    with bpy.data.libraries.load(str(path),link=False) as (source,destination):
        destination.objects=list(source.objects)
    objects=[]
    for obj in destination.objects:
        if obj and obj.type=='MESH' and not obj.hide_render and (prefix is None or obj.name.startswith(prefix)) and (names is None or obj.name in names):
            scene.collection.objects.link(obj);obj.hide_render=False;obj.hide_set(False);objects.append(obj)
    return objects

def pose(name):
    for val in poses[name]['bones']:
        b=rig.pose.bones[val['name']];b.rotation_mode='XYZ'
        b.rotation_euler=tuple(math.radians(x) for x in (val['curl'],-val['twist'],-val['spread']))
    bpy.context.view_layer.update()

def tip(digit):return rig.matrix_world@rig.pose.bones[digit+'_3'].tail
def bounds(objects):
    points=[obj.matrix_world@Vector(c) for obj in objects for c in obj.bound_box]
    return Vector(tuple(min(p[i] for p in points) for i in range(3))),Vector(tuple(max(p[i] for p in points) for i in range(3)))

records=[]
only_trap='--only-trap' in sys.argv
for item in (('Trap',) if only_trap else ('Flashlight','Snapper','Incense','Talisman','Trap')):
    pose(item);pivot=bpy.data.objects.new('Review '+item,None);scene.collection.objects.link(pivot)
    if item in ('Flashlight','Incense','Talisman'):
        objects=append_meshes(ROOT.parent/'PlayerItems'/f'{item.lower()}.blend')
    elif item=='Snapper':objects=append_meshes(ROOT.parent/'Snappers/pellet.blend','SN_pellet')
    else:objects=append_meshes(ROOT.parent/'Traps/Trap_Editable.blend',names={'Base','JawFront','JawBack','PressurePlate'})
    for obj in objects:
        matrix=obj.matrix_world.copy();obj.parent=pivot;obj.matrix_world=matrix
        if obj.animation_data:obj.animation_data.action=None
    pin=(tip('thumb')+tip('index'))*.5
    low,high=bounds(objects)
    if item=='Snapper' and max(high-low)>.04:raise RuntimeError('Review selected the hidden generation original instead of the game pellet')
    if item=='Flashlight':
        center=rig.matrix_world@rig.pose.bones['palm_contact'].head+Vector((0,-.025,.020))
        pivot.rotation_euler=(0,math.pi/2,0);pivot.location=center-Vector((.035,0,0))
    elif item=='Snapper':
        pivot.scale=(.85,)*3;pivot.location=pin-(low+high)*.5*.85
    elif item=='Incense':pivot.location=pin+Vector((0,0,(high.z-low.z)*.5-.035))-(low+high)*.5
    elif item=='Talisman':
        # Thin paper is pinched by an upper corner rather than suspended from
        # an invisible central socket. Runtime presenter owns final camera pose.
        pivot.location=pin-Vector((low.x+.008,(low.y+high.y)*.5,high.z-.006))
    else:
        scale=.55/max(.0001,high.x-low.x);pivot.scale=(scale,)*3;pivot.rotation_euler=(math.pi/2,0,0)
        bpy.context.view_layer.update();lo,hi=bounds(objects)
        palm=rig.matrix_world@rig.pose.bones['palm_contact'].head
        # In this authoring view the palmar support normal is -Y.
        pivot.location+=palm-Vector(((lo.x+hi.x)*.5,hi.y,(lo.z+hi.z)*.5))
    bpy.context.view_layer.update()
    views=[('palmar',(.21,-.46,.28))]
    if item=='Flashlight':views.append(('dorsal',(.2,.46,.29)))
    if item in ('Snapper','Incense','Talisman'):views.append(('side',(-.32,-.25,.25)))
    for label,position in views:
        camdata.ortho_scale=.68 if item=='Trap' else .36
        cam.location=position;aim(cam,(0,-.025,.095))
        scene.render.filepath=str(OUTPUT/(item.lower()+('-physical-' if item=='Trap' else '-')+label+'.png'))
        bpy.ops.render.render(write_still=True)
        records.append({'item':item,'view':label,'path':scene.render.filepath,
            'thumbIndexSkeletonGap':(tip('thumb')-tip('index')).length,
            'actualMeshAndItem':True,'physicalWidthMetres':.55 if item=='Trap' else None,
            'renderEngine':'Cycles CPU','samples':scene.cycles.samples,'threads':scene.render.threads})
    for obj in objects:obj.hide_render=True
    pivot.hide_render=True
(OUTPUT/('review-physical-manifest.json' if only_trap else 'review-manifest.json')).write_text(json.dumps({'scope':'Authored Blender skin/item stills; not Unity gameplay','records':records},indent=2)+'\n')
print('HAND_CPU_REVIEW_PASS '+str(len(records)))
