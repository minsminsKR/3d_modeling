"""Blender 4.0.2: source-derived game meshes, editable hand rig and PBR maps.
Run: blender -b --python HappyToyV2/SourceArt/PlayerItems/finish_player_items.py
No GPU model generation: all base surfaces are the preserved GPU 0 webapp GLBs.
"""
import bpy, bmesh, json, pathlib, math, sys, hashlib, struct, zlib
import numpy as np
from mathutils import Vector
from mathutils.bvhtree import BVHTree

ROOT=pathlib.Path(__file__).resolve().parent
OUT=ROOT.parents[1]/'Assets'/'Resources'/'PlayerItems'
OUT.mkdir(parents=True,exist_ok=True)

def save_texture(image,destination):
    # Blender image.save can leave an incomplete PNG without raising an error
    # when disk space runs out. Certify its chunks before replacing valid input.
    temporary=destination.with_name(destination.stem+'-export.png')
    image.filepath_raw=str(temporary);image.file_format='PNG'
    try:
        image.save()
        with temporary.open('rb') as stream:
            if stream.read(8)!=b'\x89PNG\r\n\x1a\n':raise RuntimeError('Invalid exported PNG: '+destination.name)
            while True:
                encoded=stream.read(4)
                if len(encoded)!=4:raise RuntimeError('Incomplete exported PNG: '+destination.name)
                length=struct.unpack('>I',encoded)[0];kind=stream.read(4);data=stream.read(length);crc=stream.read(4)
                if len(data)!=length or len(crc)!=4 or zlib.crc32(kind+data)&0xffffffff!=int.from_bytes(crc,'big'):
                    raise RuntimeError('Incomplete exported PNG chunk: '+destination.name)
                if kind==b'IEND':break
        temporary.replace(destination)
    finally:
        image.filepath_raw=str(destination)
        temporary.unlink(missing_ok=True)

def hand_point(p):
    x,y,z=p
    def ease(a,b,t):
        t=max(0,min(1,(t-a)/(b-a)));return t*t*(3-2*t)
    # Sculpt the preserved webapp surface, with the same UVs. Its palm was
    # almost as deep as it was wide; retain volume at the forearm and round
    # phalanges while removing that inflated palm silhouette.
    palm=.65-.13*ease(.005,.05,z)
    fingers=1-ease(.03,.07,x)
    digit=max(ease(.105,.145,z)*fingers,ease(.03,.065,x)*ease(.045,.085,z))
    thickness=palm+(.92-palm)*digit
    # The generated fingers were longer than their palm. Finish them locally
    # beyond the MCP fan; do not lengthen the distal surface or flatten digits.
    return Vector(((x+.02)*.88,y*thickness,z-max(0,z-.12)*.25*fingers))

def finish_hand_surface(obj):
    for v in obj.data.vertices:v.co=hand_point(v.co)

def finish_hand_nails(obj,chains):
    # Thin keratin caps conform to the actual generated distal skin. These are
    # editable finishing surfaces, not replacement fingers or a new base hand.
    bm=bmesh.new();bm.from_mesh(obj.data);surface=BVHTree.FromBMesh(bm)
    mat=bpy.data.materials.new('hand_nail');mat.use_nodes=True
    bsdf=mat.node_tree.nodes.get('Principled BSDF');bsdf.inputs['Base Color'].default_value=(.63,.42,.36,1)
    bsdf.inputs['Roughness'].default_value=.32
    verts=[];faces=[]
    for digit,points in chains.items():
        a,b=points[-2:];direction=(b-a).normalized();side=direction.cross(Vector((0,1,0))).normalized()
        center=a.lerp(b,.61);width=.0065 if digit!='little' else .005
        half_length=min(.012,(b-a).length*.32)
        start=len(verts)
        for ring in range(5):
            radius=ring/4
            for segment in range(24):
                theta=segment/24*math.tau
                point=center+direction*(math.cos(theta)*half_length*radius)+side*(math.sin(theta)*width*radius)
                origin=point+Vector((0,.045,0))
                hit,normal,_,_=surface.ray_cast(origin,Vector((0,-1,0)),.09)
                if hit is None:
                    hit,normal,_,gap=surface.find_nearest(point)
                    if hit is None or gap>.012:raise RuntimeError('Nail finishing lost original '+digit+' skin')
                verts.append(hit+normal*.00035)
        for ring in range(4):
            for j in range(24):
                p=start+ring*24+j;q=start+ring*24+(j+1)%24
                faces.append((p,q,q+24,p+24))
    bm.free()
    mesh=bpy.data.meshes.new('Source-conforming keratin');mesh.from_pydata(verts,[],faces);mesh.materials.append(mat)
    nails=bpy.data.objects.new('Anatomical fingernails',mesh);bpy.context.collection.objects.link(nails)
    for p in mesh.polygons:p.use_smooth=True
    bpy.ops.object.select_all(action='DESELECT');obj.select_set(True);nails.select_set(True);bpy.context.view_layer.objects.active=obj
    bpy.ops.object.join()

def rig_hand(obj):
    arm=bpy.data.armatures.new('PlayerHandRig'); rig=bpy.data.objects.new('HandRig',arm)
    bpy.context.collection.objects.link(rig); bpy.context.view_layer.objects.active=rig
    rig.select_set(True); obj.select_set(False); bpy.ops.object.mode_set(mode='EDIT')
    # Centres measured on THIS original GLB, including its curved fingers.
    # The old symmetric template placed the index/middle bones outside skin.
    chains={'little':[(-.073,.005,.118),(-.091,-.007,.15),(-.106,-.018,.18),(-.105,-.026,.203)],
            'ring':[(-.047,.012,.135),(-.052,.003,.171),(-.061,-.01,.21),(-.069,-.022,.244)],
            'middle':[(-.019,.012,.138),(-.021,.004,.18),(-.022,-.006,.225),(-.026,-.018,.26)],
            'index':[(.009,.006,.129),(.015,-.01,.166),(.02,-.019,.204),(.024,-.029,.245)],
            'thumb':[(.026,0,.05),(.06,-.013,.09),(.091,-.028,.12),(.112,-.031,.148)]}
    chains={name:[hand_point(p) for p in points] for name,points in chains.items()}
    wrist=arm.edit_bones.new('wrist'); wrist.head=hand_point((-.02,0,-.26)); wrist.tail=hand_point((-.02,0,.025))
    palm=arm.edit_bones.new('palm'); palm.head=wrist.tail; palm.tail=hand_point((-.02,0,.13)); palm.parent=wrist
    # The skin's palmar side is -Y in this preserved GLB. Make every local X
    # hinge bend towards that surface; default rolls vary with the digit fan.
    palmar=Vector((0,-1,0));wrist.align_roll(palmar);palm.align_roll(palmar)
    segments=[]
    for name,points in chains.items():
        parent=palm
        for i,(a,b) in enumerate(zip(points,points[1:])):
            bone=arm.edit_bones.new(f'{name}_{i+1}'); bone.head=a; bone.tail=b; bone.parent=parent
            bone.align_roll(palmar)
            parent=bone; segments.append((bone.name,Vector(bone.head),Vector(bone.tail)))
        tip=arm.edit_bones.new(name+'_tip');tip.head=points[-1];tip.tail=points[-1]+Vector((0,0,.003));tip.parent=parent;tip.use_deform=False
    contact=arm.edit_bones.new('palm_contact');contact.head=hand_point((-.022,-.027,.085));contact.tail=contact.head+Vector((0,-.003,0));contact.parent=palm;contact.use_deform=False
    bpy.ops.object.mode_set(mode='OBJECT')
    finish_hand_nails(obj,chains)
    obj.parent=rig
    modifier=obj.modifiers.new('Anatomical hand skin','ARMATURE');modifier.object=rig
    # Unity uses linear skinning. Author and inspect that same deformation,
    # rather than hiding collapsed knuckles with Blender-only dual quaternions.
    modifier.use_deform_preserve_volume=False
    for bone in arm.bones:
        if bone.use_deform:obj.vertex_groups.new(name=bone.name)
    def smooth(a,b,t):
        t=max(0,min(1,(t-a)/(b-a)));return t*t*(3-2*t)
    # A digit owns only its own surface. Blend at knuckles longitudinally,
    # keeping the palm stable even when fingertips touch across a pinch.
    for v in obj.data.vertices:
        def closest(name):
            points=chains[name];best=(1e6,0,0)
            for i,(a,b) in enumerate(zip(points,points[1:])):
                t=max(0,min(1,(v.co-a).dot(b-a)/(b-a).length_squared))
                d=(v.co-a.lerp(b,t)).length
                if d<best[0]:best=(d,i,t)
            return best
        distances={name:closest(name) for name in chains}
        digit=min(distances,key=lambda name:distances[name][0]);d,i,t=distances[digit]
        points=chains[digit]
        gate=smooth(points[0].x-.005,points[0].x+.022,v.co.x) if digit=='thumb' else smooth(points[0].z-.012,points[0].z+.02,v.co.z)
        influence=gate*(1-smooth(.020,.034,d))
        if digit=='thumb' and i==0:influence*=.35+.65*smooth(0,.9,t)
        wrist_weight=1-smooth(.01,.045,v.co.z)
        weights={'wrist':(1-influence)*wrist_weight,'palm':(1-influence)*(1-wrist_weight)}
        u=i+t;weights[digit+'_'+str(i+1)]=influence
        if t<.32 and i>0:
            blend=(1-smooth(0,.32,t))*.5;weights[digit+'_'+str(i+1)]-=influence*blend;weights[digit+'_'+str(i)]=influence*blend
        elif t>.68 and i<2:
            blend=smooth(.68,1,t)*.5;weights[digit+'_'+str(i+1)]-=influence*blend;weights[digit+'_'+str(i+2)]=influence*blend
        for name,weight in weights.items():
            if weight>0:obj.vertex_groups[name].add([v.index],weight,'REPLACE')
    rig['handedness']='left; mirror root X for right hand'
    rig['flexion_axis']='local +X; local +Z faces palmar -Y; no unconstrained thumb roll'
    rig['runtime_skinning']='linear blend; same as Unity'
    for pose in ('relaxed','flashlight_grip','palm_support','talisman_pinch','throw_release'):
        rig.animation_data_create(); action=bpy.data.actions.new(pose); rig.animation_data.action=action
        for b in rig.pose.bones:
            if not b.name[-1:].isdigit(): continue
            digit,joint=b.name.rsplit('_',1);joint=int(joint)-1
            curls={'relaxed':(8,16,9),'flashlight_grip':(44,64,42),'palm_support':(10,52,34),
                   'talisman_pinch':(38,58,32),'throw_release':(4,8,5)}[pose]
            if digit=='thumb':curls=(14,28,20) if pose=='palm_support' else (-20,56,42) if pose=='flashlight_grip' else (20,30,18)
            elif pose=='talisman_pinch' and digit=='index':curls=(28,38,25)
            extra=(6 if digit=='little' else 4 if digit=='ring' else 2 if digit=='middle' else 0) if pose=='palm_support' else 0
            spread=(30 if digit=='thumb' else 4 if digit=='index' else -4 if digit=='ring' else -9 if digit=='little' else 0) if joint==0 else 0
            # FBX's handedness conversion reverses local Y/Z rotations in
            # Unity, while preserving the flexion X axis.
            twist=(-40 if pose=='flashlight_grip' else 10 if pose=='palm_support' else 18) if digit=='thumb' and joint==0 else 0
            b.rotation_mode='XYZ';b.rotation_euler=tuple(math.radians(x) for x in (curls[joint]+extra,twist,spread))
            b.keyframe_insert(data_path='rotation_euler',frame=1,group=b.name)
        for b in rig.pose.bones: b.rotation_euler=(0,0,0)
    rig.animation_data.action=None
    # Small reproducible checks on the actual exported rig, including nails.
    # Natural source curvature stays in the bind pose; actions add joint curls.
    sums=[sum(g.weight for g in v.groups) for v in obj.data.vertices]
    maximum_influences=max(sum(g.weight>1e-6 for g in v.groups) for v in obj.data.vertices)
    error=max(abs(total-1) for total in sums)
    if error>1e-5 or maximum_influences>4:raise RuntimeError('Hand skin weights must be normalized and use at most four bones')
    axis_dots={b.name:(b.matrix_local.to_3x3()@Vector((0,0,1))).dot(palmar) for b in arm.bones if b.name[-1:].isdigit()}
    if min(axis_dots.values())<.85:raise RuntimeError('Finger flexion hinge lost its palmar orientation')
    rig['validation']=json.dumps({'maxWeightSumError':error,'maxInfluences':maximum_influences,'palmarAxisDots':axis_dots,
        'digitLengthsMetres':{digit:sum((b-a).length for a,b in zip(points,points[1:])) for digit,points in chains.items()},
        'palmLengthMetres':(hand_point((-.02,0,.13))-hand_point((-.02,0,.025))).length,
        'neutralJointDegrees':{digit:[math.degrees((points[i+1]-points[i]).angle(points[i+2]-points[i+1])) for i in range(2)] for digit,points in chains.items()}})
    return rig

def prepare(key):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=str(ROOT/'Originals'/f'{key}.glb'))
    objs=[o for o in bpy.context.scene.objects if o.type=='MESH']
    bpy.ops.object.select_all(action='DESELECT')
    for o in objs:o.select_set(True)
    bpy.context.view_layer.objects.active=objs[0]; bpy.ops.object.join(); obj=bpy.context.object
    bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
    obj.name=key; obj.data.name=key+'_surface'
    folder=OUT/key;folder.mkdir(parents=True,exist_ok=True)
    for i,mat in enumerate(obj.data.materials):
        mat.name=f'{key}_surface_{i}'
        for node in mat.node_tree.nodes:
            if node.type!='TEX_IMAGE' or not node.image:continue
            im=node.image
            # GLB packs roughness/metallic in one linear image; preserve it too.
            suffix='albedo' if im.colorspace_settings.name=='sRGB' else 'metalrough'
            save_texture(im,folder/f'{suffix}.png')
    height={'hand':.52,'incense':.24,'talisman':.20,'flashlight':.24}[key]
    axis=0 if key=='flashlight' else 2
    coords=np.array([v.co[:] for v in obj.data.vertices]); factor=height/(coords[:,axis].max()-coords[:,axis].min())
    obj.scale=(factor,)*3; bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    # Mesh remains upright in Blender Z; FBX converts to Unity Y.
    if key=='flashlight':
        obj.rotation_mode='XYZ';obj.rotation_euler.y=math.pi/2; bpy.ops.object.transform_apply(location=False,rotation=True,scale=False)
    centre=(min(v.co.z for v in obj.data.vertices)+max(v.co.z for v in obj.data.vertices))/2
    for v in obj.data.vertices: v.co.z-=centre
    # GLB repeats vertices at UV/material seams. Weld geometry while retaining
    # per-loop UVs, so simplification preserves continuous skin and paper normals.
    bm=bmesh.new();bm.from_mesh(obj.data)
    bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=.000002)
    bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(obj.data);bm.free()
    if obj.data.has_custom_normals:bpy.ops.mesh.customdata_custom_splitnormals_clear()
    if key=='hand':finish_hand_surface(obj)
    target={'hand':80000,'incense':8500,'talisman':9000,'flashlight':18000}[key]
    dec=obj.modifiers.new('Source surface game LOD','DECIMATE');dec.ratio=min(1,target/len(obj.data.polygons))
    bpy.ops.object.modifier_apply(modifier=dec.name)
    for p in obj.data.polygons:p.use_smooth=True
    rig=rig_hand(obj) if key=='hand' else None
    if key=='talisman':
        # Preserve UVs on five separately editable, slightly slanted torn strips.
        for i in range(5):
            frag=obj.copy();frag.data=obj.data.copy();bpy.context.collection.objects.link(frag);frag.name=f'fragment_{i}'
            bm=bmesh.new();bm.from_mesh(frag.data)
            for j,clear_inner,clear_outer in [(i,True,False),(i+1,False,True)]:
                if j==0 or j==5:continue
                bmesh.ops.bisect_plane(bm,geom=list(bm.verts)+list(bm.edges)+list(bm.faces),dist=.000001,
                    plane_co=(0,0,-.1+j*.04),plane_no=Vector((.13*(-1 if j%2 else 1),0,1)).normalized(),
                    clear_inner=clear_inner,clear_outer=clear_outer)
            bm.to_mesh(frag.data);bm.free()
            bpy.ops.object.select_all(action='DESELECT');frag.select_set(True);bpy.context.view_layer.objects.active=frag
            bpy.ops.export_scene.fbx(filepath=str(folder/f'fragment_{i}.fbx'),use_selection=True,add_leaf_bones=False,bake_anim=False,axis_forward='-Z',axis_up='Y')
            frag.hide_render=True;frag.hide_set(True)
    bpy.ops.object.select_all(action='DESELECT');obj.select_set(True)
    if rig:rig.select_set(True);bpy.context.view_layer.objects.active=rig
    else:bpy.context.view_layer.objects.active=obj
    # Export completely before replacing the existing Unity source; a failed
    # authoring run must not truncate the last valid imported model.
    export=folder/'geometry-export.fbx'
    packed=ROOT/f'{key}-export.blend'
    try:
        bpy.ops.export_scene.fbx(filepath=str(export),use_selection=True,add_leaf_bones=False,bake_anim=False,axis_forward='-Z',axis_up='Y')
        bpy.ops.file.pack_all()
        bpy.ops.wm.save_as_mainfile(filepath=str(packed))
        if not export.stat().st_size or not packed.stat().st_size:raise RuntimeError('Empty player item export: '+key)
        export.replace(folder/'geometry.fbx');packed.replace(ROOT/f'{key}.blend')
    finally:
        export.unlink(missing_ok=True);packed.unlink(missing_ok=True)
    report={'original':f'Originals/{key}.glb','heightMetres':height,'vertices':len(obj.data.vertices),'triangles':sum(len(p.vertices)-2 for p in obj.data.polygons),
            'rigged':bool(rig),'baseColor':'albedo.png','metallicRoughness':'metalrough.png','materialNames':[m.name for m in obj.data.materials]}
    if rig:
        report['rigValidation']=json.loads(rig['validation'])
        references=ROOT.parent/'CorridorEntrance'/'References'
        report['gripReviewInputs']={label:{'path':'SourceArt/CorridorEntrance/References/'+name,
            'sha256':hashlib.sha256((references/name).read_bytes()).hexdigest()}
            for label,name in [('target','user-target-20261016.png'),('before','user-before-20261016.png')]}
        report['originalSha256']=hashlib.sha256((ROOT/'Originals'/f'{key}.glb').read_bytes()).hexdigest()
        report['outputSha256']={name:hashlib.sha256(path.read_bytes()).hexdigest() for name,path in
            [('geometry',folder/'geometry.fbx'),('packedBlend',ROOT/f'{key}.blend'),('albedo',folder/'albedo.png'),('metallicRoughness',folder/'metalrough.png')]}
    temporary=folder/'asset-export.json'
    try:
        temporary.write_text(json.dumps(report,indent=2)+'\n');temporary.replace(folder/'asset.json')
    finally:temporary.unlink(missing_ok=True)
    print('PREPARED',key,report)

keys=('hand','incense','talisman','flashlight')
requested=next((x.split('=',1)[1] for x in sys.argv if x.startswith('--only=')),None)
if requested and requested not in keys:raise ValueError('Unknown player item: '+requested)
for key in ((requested,) if requested else keys):prepare(key)
