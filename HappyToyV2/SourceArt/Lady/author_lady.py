"""Finish the generated Lady: preserve AI rig, author horror action and game LODs.

Blender 4.0.2 --background --factory-startup --python-exit-code 1 --python
HappyToyV2/SourceArt/Lady/author_lady.py
"""
import hashlib
import json
import math
import sys
from pathlib import Path
import bpy
import bmesh
import numpy as np
from mathutils import Euler, Matrix, Vector, Quaternion

HERE=Path(__file__).resolve().parent
PROJECT=HERE.parents[1]
sys.path.insert(0,str(HERE))
from lady_motion import beats, sample, head_pose

NAMES=['Hips','Spine','Spine1','Spine2','Neck','Head',
       'LeftShoulder','LeftArm','LeftForeArm','LeftHand','LeftFinger1','LeftFinger2','LeftFinger3',
       'RightShoulder','RightArm','RightForeArm','RightHand','RightFinger1','RightFinger2','RightFinger3',
       'LeftUpLeg','LeftLeg','LeftFoot','LeftToeBase','RightUpLeg','RightLeg','RightFoot','RightToeBase']


def say(text):print('[Lady] '+text,flush=True)


def world_points(obj):
    points=np.empty(len(obj.data.vertices)*3,dtype=np.float32)
    obj.data.vertices.foreach_get('co',points)
    points=points.reshape(-1,3)
    m=np.asarray(obj.matrix_world)
    return points@m[:3,:3].T+m[:3,3]


def copy_lod(source,level,ratio):
    obj=source.copy();obj.data=source.data.copy();bpy.context.collection.objects.link(obj)
    obj.name='Lady_LOD'+str(level);obj.data.name=obj.name+'Mesh'
    # The continuous game surface already shares ordinary vertices. Its
    # deliberate hair/sleeve/robe/foot seams must survive every game LOD.
    bpy.context.view_layer.objects.active=obj
    bpy.ops.object.select_all(action='DESELECT');obj.select_set(True)
    modifier=obj.modifiers.new('Source topology collapse with UVs','DECIMATE')
    modifier.ratio=ratio;modifier.use_collapse_triangulate=True
    if level==0:modifier.vertex_group='Face and hand protection';modifier.vertex_group_factor=.5
    bpy.ops.object.modifier_apply(modifier=modifier.name)
    say(obj.name+' vertices='+str(len(obj.data.vertices))+' triangles='+str(len(obj.data.polygons)))
    return obj


def author_action(rig,profile):
    action=bpy.data.actions.new('chase');rig.animation_data_create();rig.animation_data.action=action
    limits={'Hips':(5,8,7),'Spine1':(13,19,15),'Spine2':(9,16,19),'Neck':(10,13,17),
            'Head':profile['head_degrees']}
    for side in ('Left','Right'):
        limits.update({side+'Shoulder':(17,13,19),side+'Arm':(42,22,28),
                       side+'ForeArm':(82,19,24),side+'Hand':(64,61,64),
                       side+'UpLeg':(12,9,9),side+'Leg':(24,12,12),side+'Foot':(13,11,14)})
    schedules={name:beats(profile,profile['seed']+i*191,value,name=='Head')
               for i,(name,value) in enumerate(limits.items())}
    count=round(profile['duration_seconds']*profile['fps'])
    for pose in rig.pose.bones:pose.rotation_mode='QUATERNION'
    # AI bone rolls do not share a forward hinge. Resolve the actual world
    # left/right axis into each bone's rest frame for the running stride.
    hinges={name:(rig.matrix_world@rig.data.bones[name].matrix_local).to_quaternion().inverted()@Vector((1,0,0))
            for name in limits if name.endswith('UpLeg') or name.endswith('Leg')}
    for frame in range(count+1):
        time=frame/profile['fps'];phase=time*math.tau/profile['stride_seconds']
        envelope=min(1,time/.2,(profile['duration_seconds']-time)/.2)
        for name,sequence in schedules.items():
            values=head_pose(profile,sequence,time) if name=='Head' else sample(sequence,time)
            if name.endswith('Arm') and not name.endswith('ForeArm'):
                values[0]+=24*math.sin(phase+(math.pi if name.startswith('Left') else 0))*envelope
            bone=rig.pose.bones[name]
            q=Euler(tuple(math.radians(v) for v in values),'XYZ').to_quaternion()
            stride=math.sin(phase+(0 if name.startswith('Left') else math.pi))
            if name.endswith('UpLeg'):q=Quaternion(hinges[name],math.radians(-profile['stride_hip_degrees']*stride*envelope))@q
            elif name.endswith('Leg'):q=Quaternion(hinges[name],math.radians(profile['stride_knee_degrees']*max(0,stride)*envelope))@q
            bone.rotation_quaternion=q;bone.keyframe_insert('rotation_quaternion',frame=frame+1,group=name)
        if frame%300==0:say('authored frame '+str(frame)+'/'+str(count))
    for curve in action.fcurves:
        for key in curve.keyframe_points:key.interpolation='LINEAR'
    return action,schedules


def render_preview(fit,rig,directory):
    scene=bpy.context.scene;scene.render.engine='BLENDER_EEVEE'
    scene.eevee.taa_render_samples=32
    scene.render.resolution_x=640;scene.render.resolution_y=960;scene.render.resolution_percentage=100
    scene.world.color=(.09,.09,.09)
    scene.view_settings.view_transform='Standard';scene.view_settings.look='Medium High Contrast'
    def aim(obj,point):obj.rotation_euler=(Vector(point)-obj.location).to_track_quat('-Z','Y').to_euler()
    data=bpy.data.cameras.new('Lady review camera');camera=bpy.data.objects.new(data.name,data)
    scene.collection.objects.link(camera);scene.camera=camera;data.type='ORTHO';data.ortho_scale=2.7
    for name,at,power,size in [('Key',(3,-4,5),450,4),('Fill',(-3,-2,3),180,3),('Rim',(0,3,4),350,3)]:
        light=bpy.data.lights.new(name,'AREA');light.energy=power;light.shape='DISK';light.size=size
        obj=bpy.data.objects.new(name,light);scene.collection.objects.link(obj);obj.location=at;aim(obj,(0,0,1.2))
    directory.mkdir(parents=True,exist_ok=True)
    for label,at,frame in [('front',(0,-4,1.15),1),('side',(4,0,1.15),1),('back',(0,4,1.15),1),
                           ('writhe',(0,-4,1.15),28),('writhe-side',(3,-3,1.15),59)]:
        scene.frame_set(frame);camera.location=at;aim(camera,(0,0,1.06))
        scene.render.filepath=str(directory/(label+'.png'));bpy.ops.render.render(write_still=True)


def export_game(fit,rig,lods):
    game=(Path(sys.argv[sys.argv.index('--export-dir')+1]).resolve() if '--export-dir' in sys.argv
          else PROJECT/'Assets/Resources/Lady')
    game.mkdir(parents=True,exist_ok=True)
    bpy.context.scene.frame_set(1)
    bpy.ops.object.select_all(action='DESELECT');fit.select_set(True);rig.select_set(True)
    for obj in lods:obj.hide_set(False);obj.select_set(True)
    bpy.context.view_layer.objects.active=rig
    modifiers=[(mod,mod.show_viewport) for obj in bpy.context.scene.objects if obj.type=='MESH'
               for mod in obj.modifiers if mod.type=='ARMATURE']
    try:
        # FBX exports rest mesh + skin and bakes bone curves. Evaluating a million
        # source vertices on every one of the 2239 frames is unnecessary here.
        for mod,_ in modifiers:mod.show_viewport=False
        bpy.ops.export_scene.fbx(filepath=str(game/'Lady.fbx'),use_selection=True,object_types={'EMPTY','ARMATURE','MESH'},
            add_leaf_bones=False,bake_anim=True,bake_anim_use_all_bones=True,bake_anim_use_nla_strips=False,
            bake_anim_use_all_actions=False,bake_anim_simplify_factor=0,use_mesh_modifiers=False,
            axis_forward='-Z',axis_up='Y',path_mode='STRIP')
    finally:
        for mod,visible in modifiers:mod.show_viewport=visible
    say('FBX exported')


def main():
    bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
    source=HERE/'webapp/source-ai-rig.glb'
    bpy.ops.import_scene.gltf(filepath=str(source))
    meshes=[o for o in bpy.context.scene.objects if o.type=='MESH' and
            any(mod.type=='ARMATURE' for mod in o.modifiers)]
    rigs=[o for o in bpy.context.scene.objects if o.type=='ARMATURE']
    assert len(rigs)==1 and len(meshes)==1,[(o.name,o.type) for o in bpy.context.scene.objects]
    for obj in bpy.context.scene.objects:
        if obj.type=='MESH' and obj not in meshes:obj.hide_render=True;obj.hide_set(True)
    rig=rigs[0];mesh=meshes[0];rig.name='Lady_Rig'
    for i,name in enumerate(NAMES):
        old='bone_'+str(i);assert old in rig.data.bones
        rig.data.bones[old].name=name
        for obj in meshes:
            group=obj.vertex_groups.get(old)
            if group:group.name=name
    rig.animation_data_clear()
    for action in list(bpy.data.actions):bpy.data.actions.remove(action)
    # AI export faces +X. Rotate into Blender -Y / Unity +Z; retain its original
    # bone rest transforms and weights beneath a single fitting root.
    fit=bpy.data.objects.new('Lady_Fit',None);bpy.context.collection.objects.link(fit)
    roots=[o for o in list(bpy.context.scene.objects) if o!=fit and o.parent is None]
    for obj in roots:obj.parent=fit
    fit.rotation_euler.z=-math.pi/2;fit.scale=(2.05,)*3;fit.location.z=.4992230534553528*2.05+.025
    bpy.context.view_layer.update()
    p=world_points(mesh)
    protection=mesh.vertex_groups.new(name='Face and hand protection')
    protection.add(list(range(len(mesh.data.vertices))),1.,'REPLACE')
    ids=np.where(((p[:,2]>1.73)&(p[:,1]<-.03))|
                 ((abs(p[:,0])>.23)&(p[:,2]>.78)&(p[:,2]<1.15)))[0].tolist()
    if ids:protection.add(ids,.01,'REPLACE')
    original_triangles=len(mesh.data.polygons)
    mesh.name='Lady_SourceHigh';mesh.data.name='Lady_GeneratedOriginalSkin'
    from surface_finish import build
    first=build(mesh,rig)
    lods=[first]+[copy_lod(first,i,limit/len(first.data.polygons)) for i,limit in ((1,45000),(2,14000))]
    mesh.hide_render=True;mesh.hide_set(True)
    for obj in lods[1:]:obj.hide_render=True;obj.hide_set(True)
    profile=json.loads((HERE/'motion-profile.json').read_text(encoding='utf-8'))
    action,schedules=author_action(rig,profile)
    scene=bpy.context.scene;scene.render.fps=profile['fps'];scene.frame_start=1
    scene.frame_end=round(profile['duration_seconds']*profile['fps'])+1
    scene.frame_set(1)
    bounds=[];deps=bpy.context.evaluated_depsgraph_get()
    for frame in range(1,scene.frame_end+1,max(1,scene.frame_end//64)):
        scene.frame_set(frame);evaluated=lods[0].evaluated_get(deps);points=world_points(evaluated)
        bounds.append([points.min(0).tolist(),points.max(0).tolist()])
    scene.frame_set(1)
    report={'sourceRigSha256':hashlib.sha256(source.read_bytes()).hexdigest(),'boneMapping':{f'bone_{i}':n for i,n in enumerate(NAMES)},
            'rigBones':len(rig.data.bones),'sourceTriangles':original_triangles,
            'lods':[{'name':o.name,'vertices':len(o.data.vertices),'triangles':len(o.data.polygons)} for o in lods],
            'duration':profile['duration_seconds'],'fps':profile['fps'],'sampledBounds':bounds,
            'motionSchedules':schedules,'motionProvenance':'Seeded authored Blender keyframes, not generated motion capture',
            'sourceAIWeightsRetained':True,'gameSkinTransferReport':'surface-report.json','originalSourceSeparate':True}
    (HERE/'authoring-report.json').write_text(json.dumps(report,indent=2)+'\n',encoding='utf-8')
    bpy.ops.file.pack_all()
    bpy.context.preferences.filepaths.save_version=0
    bpy.ops.wm.save_as_mainfile(filepath=str(HERE/'Lady_Editable.blend'))
    export_game(fit,rig,lods)
    for obj in lods[1:]:obj.hide_set(True);obj.hide_render=True
    render_preview(fit,rig,PROJECT/'Builds/LadyReview-20261018')
    say('LADY_AUTHORING_PASS')


def finish_existing():
    bpy.ops.wm.open_mainfile(filepath=str(HERE/'Lady_Editable.blend'))
    rig=bpy.data.objects['Lady_Rig'];fit=bpy.data.objects['Lady_Fit']
    for name in ('Lady_LOD0','Lady_LOD1','Lady_LOD2'):
        obj=bpy.data.objects.get(name)
        if obj:bpy.data.objects.remove(obj,do_unlink=True)
    bpy.context.scene.frame_set(1)
    from surface_finish import build
    first=build(bpy.data.objects['Lady_SourceHigh'],rig)
    lods=[first]+[copy_lod(first,i,limit/len(first.data.polygons)) for i,limit in ((1,45000),(2,14000))]
    report=json.loads((HERE/'authoring-report.json').read_text())
    report['lods']=[{'name':o.name,'vertices':len(o.data.vertices),'triangles':len(o.data.polygons)} for o in lods]
    (HERE/'authoring-report.json').write_text(json.dumps(report,indent=2)+'\n',encoding='utf-8')
    for obj in lods[1:]:obj.hide_render=True;obj.hide_set(True)
    bpy.context.preferences.filepaths.save_version=0
    bpy.ops.wm.save_as_mainfile(filepath=str(HERE/'Lady_Editable.blend'))
    export_game(fit,rig,lods)
    for obj in lods[1:]:obj.hide_render=True;obj.hide_set(True)
    render_preview(fit,rig,PROJECT/'Builds/LadyReview-20261018')
    say('LADY_AUTHORING_PASS')


def finish_lods():
    bpy.ops.wm.open_mainfile(filepath=str(HERE/'Lady_Editable.blend'))
    rig=bpy.data.objects['Lady_Rig'];fit=bpy.data.objects['Lady_Fit'];first=bpy.data.objects['Lady_LOD0']
    for i in (1,2):bpy.data.objects.remove(bpy.data.objects['Lady_LOD'+str(i)],do_unlink=True)
    bpy.context.scene.frame_set(1)
    lods=[first]+[copy_lod(first,i,limit/len(first.data.polygons)) for i,limit in ((1,45000),(2,14000))]
    report=json.loads((HERE/'authoring-report.json').read_text(encoding='utf-8'))
    report['lods']=[dict(name=o.name,vertices=len(o.data.vertices),triangles=len(o.data.polygons)) for o in lods]
    report['allLodsPreserveAuthoredSurfaceSeams']=True
    (HERE/'authoring-report.json').write_text(json.dumps(report,indent=2)+'\n',encoding='utf-8')
    for obj in lods[1:]:obj.hide_render=True;obj.hide_set(True)
    bpy.context.preferences.filepaths.save_version=0
    bpy.ops.wm.save_as_mainfile(filepath=str(HERE/'Lady_Editable.blend'))
    export_game(fit,rig,lods)
    for obj in lods[1:]:obj.hide_render=True;obj.hide_set(True)
    render_preview(fit,rig,PROJECT/'Builds/LadyReview-20261018')


if __name__=='__main__':
    if '--finish-lods' in sys.argv:finish_lods()
    elif '--refine-existing' in sys.argv:
        from refine_game import refine_existing
        refine_existing()
    elif '--finish-existing' in sys.argv:finish_existing()
    else:main()
