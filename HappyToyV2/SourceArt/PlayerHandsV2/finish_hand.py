"""Blender 4.0.2: finish the retained A100 hand, preserving original UV/PBR.
Run in a disposable Blender MCP session or blender -b --python finish_hand.py.
No primitive replacement hand or external generator is used.
"""
from pathlib import Path
import bpy, bmesh, numpy as np, json, math, hashlib
from mathutils import Vector

ROOT = Path(__file__).resolve().parent
PROJECT = ROOT.parents[1]
OUT = PROJECT/'Assets/Resources/PlayerItems/hand'
SCALE = .375
SOURCE_CHAINS = {
    'little':[(-.166,.060,.218),(-.199,.055,.291),(-.218,.043,.345),(-.226,.035,.382)],
    'ring':[(-.102,.080,.247),(-.127,.071,.350),(-.139,.058,.417),(-.145,.052,.466)],
    'middle':[(-.043,.085,.250),(-.049,.073,.367),(-.052,.061,.441),(-.053,.050,.499)],
    'index':[(.022,.085,.240),(.040,.078,.347),(.048,.065,.418),(.053,.058,.475)],
    'thumb':[(.022,.040,.030),(.126,.077,.159),(.191,.086,.240),(.223,.090,.287)]}

def smooth(a,b,t):
    x=np.clip((t-a)/(b-a),0,1)
    return x*x*(3-2*x)

def finish_points(p):
    p=np.asarray(p,dtype=float).copy()
    # The generated palm is too thick. Sculpt only depth about the measured
    # palm centre, preserving webbing, nails, UVs, length and finger silhouette.
    z=p[...,2];x=p[...,0]
    palm=smooth(-.15,-.015,z)*(1-smooth(.20,.28,z))
    thumb=smooth(.055,.145,x)*smooth(.04,.19,z)
    factor=.78-.25*palm*(1-thumb)
    p[...,0]=(x+.026)*SCALE
    p[...,1]=(p[...,1]-.04)*factor*SCALE
    p[...,2]=z*SCALE+.03175
    return p

def digest(p):return hashlib.sha256(p.read_bytes()).hexdigest()

def prepare():
    OUT.mkdir(parents=True,exist_ok=True)
    # The caller must use a disposable session; retain its MCP addon/timers.
    bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
    bpy.ops.import_scene.gltf(filepath=str(ROOT/'Originals/model.glb'))
    objects=[o for o in bpy.context.scene.objects if o.type=='MESH']
    bpy.ops.object.select_all(action='DESELECT')
    for o in objects:o.select_set(True)
    bpy.context.view_layer.objects.active=objects[0];bpy.ops.object.join()
    obj=bpy.context.object;bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
    original_faces=len(obj.data.polygons)
    # Export the original 8K maps, not a colour-only substitute. Packed Blender
    # retains the same originals; Unity's existing importer selects a 4K game map.
    for mat in obj.data.materials:
        mat.name='hand_v2_surface'
        for node in mat.node_tree.nodes:
            if node.type=='TEX_IMAGE' and node.image:
                suffix='albedo' if node.image.colorspace_settings.name=='sRGB' else 'metalrough'
                node.image.filepath_raw=str(OUT/(suffix+'.png'))
                node.image.file_format='PNG';node.image.save()
    coordinates=np.array([v.co[:] for v in obj.data.vertices])
    coordinates=finish_points(coordinates)
    for v,p in zip(obj.data.vertices,coordinates):v.co=p
    bm=bmesh.new();bm.from_mesh(obj.data)
    bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=.000003)
    bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(obj.data);bm.free()
    if obj.data.has_custom_normals:bpy.ops.mesh.customdata_custom_splitnormals_clear()
    # Preserve the dense source contours but remove redundant coplanar triangles.
    dec=obj.modifiers.new('Viewmodel source reduction','DECIMATE')
    dec.ratio=min(1,140000/len(obj.data.polygons));bpy.ops.object.modifier_apply(modifier=dec.name)
    for poly in obj.data.polygons:poly.use_smooth=True
    obj.name='hand';obj.data.name='A100 hand finished surface'
    chains={name:[Vector(p) for p in finish_points(points)] for name,points in SOURCE_CHAINS.items()}
    arm=bpy.data.armatures.new('PlayerHandRig');rig=bpy.data.objects.new('HandRig',arm)
    bpy.context.collection.objects.link(rig)
    bpy.context.view_layer.objects.active=rig;rig.select_set(True);obj.select_set(False)
    bpy.ops.object.mode_set(mode='EDIT')
    palmar=Vector((0,-1,0))
    wrist=arm.edit_bones.new('wrist');wrist.head=Vector(finish_points((-.025,.039,-.5)));wrist.tail=Vector(finish_points((-.025,.039,-.02)))
    palm=arm.edit_bones.new('palm');palm.head=wrist.tail;palm.tail=Vector(finish_points((-.035,.07,.24)));palm.parent=wrist
    wrist.align_roll(palmar);palm.align_roll(palmar)
    for name,points in chains.items():
        parent=palm
        for i,(a,b) in enumerate(zip(points,points[1:])):
            bone=arm.edit_bones.new(f'{name}_{i+1}');bone.head=a;bone.tail=b;bone.parent=parent
            bone.align_roll(palmar);parent=bone
        tip=arm.edit_bones.new(name+'_tip');tip.head=points[-1];tip.tail=tip.head+Vector((0,0,.003));tip.parent=parent;tip.use_deform=False
    marker=arm.edit_bones.new('palm_contact');marker.head=Vector(finish_points((-.026,-.005,.14)));marker.tail=marker.head+Vector((0,-.003,0));marker.parent=palm;marker.use_deform=False
    bpy.ops.object.mode_set(mode='OBJECT')
    obj.parent=rig;modifier=obj.modifiers.new('Linear hand skin','ARMATURE');modifier.object=rig
    modifier.use_deform_preserve_volume=False
    for bone in arm.bones:
        if bone.use_deform:obj.vertex_groups.new(name=bone.name)
    p=np.array([v.co[:] for v in obj.data.vertices]);names=list(chains)
    distances=[];segments=[];parameters=[]
    for name in names:
        nearest=np.full(len(p),1e6);segment=np.zeros(len(p),dtype=int);parameter=np.zeros(len(p))
        for i,(a,b) in enumerate(zip(chains[name],chains[name][1:])):
            a=np.asarray(a);b=np.asarray(b);delta=b-a
            t=np.clip(((p-a)*delta).sum(axis=1)/(delta*delta).sum(),0,1)
            d=np.linalg.norm(p-(a+t[:,None]*delta),axis=1);closer=d<nearest
            nearest[closer]=d[closer];segment[closer]=i;parameter[closer]=t[closer]
        distances.append(nearest);segments.append(segment);parameters.append(parameter)
    ownership=np.argmin(np.array(distances),axis=0)
    for v in obj.data.vertices:
        n=ownership[v.index];name=names[n];i=segments[n][v.index];t=parameters[n][v.index]
        points=chains[name];d=distances[n][v.index]
        gate=smooth(points[0].x-.005,points[0].x+.022,v.co.x) if name=='thumb' else smooth(points[0].z-.012,points[0].z+.010,v.co.z)
        influence=float(gate*(1-smooth(.013,.025,d)))
        if name=='thumb' and i==0:influence*=.25+.75*float(smooth(.05,.9,t))
        wrist_amount=1-float(smooth(.016,.044,v.co.z))
        weights={'wrist':(1-influence)*wrist_amount,'palm':(1-influence)*(1-wrist_amount),name+'_'+str(i+1):influence}
        if t<.4 and i>0:
            blend=(1-float(smooth(0,.4,t)))*.5
            weights[name+'_'+str(i+1)]-=influence*blend;weights[name+'_'+str(i)]=influence*blend
        elif t>.6 and i<2:
            blend=float(smooth(.6,1,t))*.5
            weights[name+'_'+str(i+1)]-=influence*blend;weights[name+'_'+str(i+2)]=influence*blend
        for group,weight in weights.items():
            if weight>1e-7:obj.vertex_groups[group].add([v.index],weight,'REPLACE')
    obj.shape_key_add(name='Basis')
    # Pose-space radial expansion compensates the knuckle volume that ordinary
    # linear skinning loses during a closed grip. No duplicate fingers are added.
    for n,name in enumerate(names):
        for joint in (1,2):
            center=np.array(chains[name][joint]);axis=np.array(chains[name][joint+1]-chains[name][joint]);axis/=np.linalg.norm(axis)
            offset=p-center;along=(offset*axis).sum(axis=1);radial=offset-along[:,None]*axis
            mask=(ownership==n)*(1-smooth(.003,.013,np.abs(along)))
            change=radial*(mask*.24)[:,None]
            shape=obj.shape_key_add(name=f'bend_{name}_{joint+1}')
            for k in np.flatnonzero(mask>0):shape.data[int(k)].co=p[k]+change[k]
    # Editable rig actions are separate from the runtime socket/contact solver.
    for title,degrees in [('Neutral',0),('CylinderGrip',65),('LooseSnapperGrip',48),('Release',8)]:
        action=bpy.data.actions.new(title);rig.animation_data_create();rig.animation_data.action=action
        for name in names:
            for joint in (1,2,3):
                pose=rig.pose.bones[f'{name}_{joint}'];pose.rotation_mode='XYZ'
                pose.rotation_euler=(math.radians(degrees*(.65 if joint==3 else 1)),0,0)
                pose.keyframe_insert(data_path='rotation_euler',frame=1)
    rig.animation_data_clear()
    for pose in rig.pose.bones:pose.rotation_euler=(0,0,0)
    bpy.context.view_layer.update()
    weights=[sum(g.weight for g in v.groups) for v in obj.data.vertices]
    report={'original':'SourceArt/PlayerHandsV2/Originals/model.glb','originalSha256':digest(ROOT/'Originals/model.glb'),
            'originalTriangles':original_faces,'heightMetres':float(np.ptp(p[:,2])),
            'vertices':len(obj.data.vertices),'triangles':sum(len(q.vertices)-2 for q in obj.data.polygons),
            'rigged':True,'baseColor':'albedo.png','metallicRoughness':'metalrough.png',
            'materialNames':[m.name for m in obj.data.materials],
            'rigValidation':{'maxWeightSumError':max(abs(w-1) for w in weights),
                'maxInfluences':max(len(v.groups) for v in obj.data.vertices),
                'bones':len(arm.bones),'correctiveShapes':len(obj.data.shape_keys.key_blocks)-1,
                'digitLengthsMetres':{name:sum((b-a).length for a,b in zip(q,q[1:])) for name,q in chains.items()}},
            'generationRequest':'SourceArt/PlayerHandsV2/generation-request.json'}
    if report['rigValidation']['maxWeightSumError']>.0001:raise RuntimeError('Invalid normalized skin weights')
    source=bpy.data.texts.new('Reproduce hand finish.py');source.write(Path(__file__).read_text(encoding='utf-8'))
    bpy.ops.object.select_all(action='DESELECT');obj.select_set(True);rig.select_set(True);bpy.context.view_layer.objects.active=rig
    export=OUT/'geometry-export.fbx';packed=ROOT/'hand-export.blend'
    bpy.ops.export_scene.fbx(filepath=str(export),use_selection=True,add_leaf_bones=False,bake_anim=False,axis_forward='-Z',axis_up='Y')
    bpy.ops.file.pack_all();bpy.ops.wm.save_as_mainfile(filepath=str(packed))
    if export.stat().st_size<1000 or packed.stat().st_size<1000:raise RuntimeError('Incomplete hand export')
    export.replace(OUT/'geometry.fbx');packed.replace(ROOT/'hand.blend')
    report['outputSha256']={'geometry':digest(OUT/'geometry.fbx'),'packedBlend':digest(ROOT/'hand.blend'),'albedo':digest(OUT/'albedo.png'),'metallicRoughness':digest(OUT/'metalrough.png')}
    (OUT/'asset.json').write_text(json.dumps(report,indent=2)+'\n',encoding='utf-8')
    (ROOT/'finish-report.json').write_text(json.dumps(report,indent=2)+'\n',encoding='utf-8')
    print('HAND_FINISH_PASS',json.dumps(report))

if __name__=='__main__':prepare()
