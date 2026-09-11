import bpy, math, random, os
from mathutils import Vector
from mathutils.noise import noise_vector, noise
random.seed(21)
bpy.context.preferences.filepaths.save_version=0
OUT=os.path.abspath('happy_toy/models/mask-wraith')
os.makedirs(OUT,exist_ok=True)
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
# Artist coordinates: X lateral, Y height, Z forward.
def V(p): return Vector((p[0],-p[2],p[1]))
parts=[]
def ell(name,p,s):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=20,ring_count=12,location=V(p))
    o=bpy.context.object;o.name=name;o.scale=(s[0],s[2],s[1]);bpy.ops.object.transform_apply(location=False,rotation=False,scale=True);parts.append(o);return o

def limb(name,a,b,r1,r2):
    a,b=V(a),V(b);d=b-a
    bpy.ops.mesh.primitive_cone_add(vertices=16,radius1=r1,radius2=r2,depth=d.length,location=(a+b)/2)
    o=bpy.context.object;o.name=name;o.rotation_mode='QUATERNION';o.rotation_quaternion=d.to_track_quat('Z','Y');parts.append(o);return o
# Joined flesh volumes, deliberately uneven and bent forward.
ell('pelvis',(0,1.01,-.04),(.20,.18,.13))
ell('wasted_abdomen',(.025,1.25,-.08),(.145,.25,.12))
ell('arched_thorax',(0,1.57,-.13),(.285,.30,.16))
ell('hunched_spine',(-.10,1.79,-.25),(.29,.28,.22))
limb('sinewy_neck',(0,1.75,-.14),(0,2.08,.04),.115,.077)
for s in [-1,1]:
    shoulder=(s*.30,1.78,-.09);elbow=(s*.42,1.12,-.02);wrist=(s*.47,.53,.10)
    ell('deltoid',shoulder,(.105,.145,.105));limb('upper_arm',elbow,shoulder,.055,.095)
    ell('elbow',elbow,(.058,.068,.056));limb('forearm',wrist,elbow,.037,.060)
    ell('wrist',wrist,(.044,.063,.039));ell('long_palm',(s*.47,.44,.10),(.070,.115,.04))
    for f in range(5):
        x=s*.47+(f-2)*.029;length=.22+(f%3)*.033
        a=(x,.39,.10);b=(x+(f-2)*.01,.27,.13);c=(x+(f-2)*.016,.39-length,.22)
        limb('finger',a,b,.017,.014);limb('hooked_finger',b,c,.014,.008);ell('knuckle',b,(.018,.022,.017))
    hip=(s*.115,1.02,-.04);knee=(s*.15,.54,.07);ankle=(s*.145,.10,-.02)
    ell('thigh',(s*.13,.81,-.01),(.093,.24,.085));limb('thigh_tendon',knee,hip,.052,.085)
    ell('kneecap',knee,(.055,.066,.058));limb('shin',ankle,knee,.030,.054)
    ell('calf',(s*.148,.36,-.045),(.054,.14,.06));ell('foot',(s*.145,.067,.095),(.062,.062,.155))
    for f in range(3):limb('toe',(s*.145+(f-1)*.035,.063,.16),(s*.145+(f-1)*.035,.035,.27),.022,.010)
# A second, uneven pair of grasping limbs grows from the ribs.
for s in [-1,1]:
    shoulder=(s*.25,1.47,-.12);elbow=(s*.67,1.02,-.03);wrist=(s*.69,.42,.22)
    ell('secondary_shoulder',shoulder,(.075,.105,.08));limb('secondary_upper',elbow,shoulder,.035,.065)
    ell('secondary_elbow',elbow,(.045,.054,.044));limb('secondary_lower',wrist,elbow,.025,.035)
    ell('secondary_palm',wrist,(.065,.085,.035))
    for f in range(3):
        x=s*.69+(f-1)*.05
        limb('secondary_talon',(x,.40,.22),(x+(f-1)*.025,.23,.31),.017,.011)
        limb('secondary_hook',(x+(f-1)*.025,.23,.31),(x+(f-1)*.035,.19,.42),.011,.004)
# Irregular dorsal protrusions are part of the joined sculpt, not clothing.
for i in range(7):
    y=1.22+i*.105
    limb('dorsal_growth',(-.04,y,-.24),(-.05+math.sin(i)*.04,y+.16,-.39-(i%3)*.04),.045,.005)
# Voxel sculpt joins the anatomy into a single continuous surface.
bpy.ops.object.select_all(action='DESELECT')
for o in parts:o.select_set(True)
bpy.context.view_layer.objects.active=parts[0];bpy.ops.object.join();body=bpy.context.object;body.name='Wraith_Continuous_Sculpt'
bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
rem=body.modifiers.new('Sculpt_union','REMESH');rem.mode='VOXEL';rem.voxel_size=.013;rem.use_smooth_shade=True;bpy.ops.object.modifier_apply(modifier=rem.name)
sm=body.modifiers.new('Organic_blend','SMOOTH');sm.factor=.65;sm.iterations=4;bpy.ops.object.modifier_apply(modifier=sm.name)
# Subtle irregular skin, ridges and old splits; baked into geometry and color.
for v in body.data.vertices:
    p=v.co.copy();v.co+=v.normal*(noise(p*33)*.004+noise(p*8)*.009)
dec=body.modifiers.new('Game_topology','DECIMATE');dec.ratio=.58;bpy.ops.object.modifier_apply(modifier=dec.name)
body.data.validate(clean_customdata=False);body.data.update()
for p in body.data.polygons:p.use_smooth=True

def material(name,base):
    m=bpy.data.materials.new(name);m.use_nodes=True;n=m.node_tree.nodes;bs=n.get('Principled BSDF');bs.inputs['Roughness'].default_value=.88
    attr=n.new('ShaderNodeVertexColor');attr.layer_name='Patina';m.node_tree.links.new(attr.outputs['Color'],bs.inputs['Base Color']);return m
skin=material('Weathered_ashen_flesh',(.2,.17,.13));cloth=material('Burial_shroud',(.08,.075,.06));bone=material('Old_sinew',(.35,.29,.20))

def paint(obj,mat,base):
    obj.data.materials.append(mat);a=obj.data.color_attributes.new(name='Patina',type='BYTE_COLOR',domain='CORNER')
    blood=obj.data.color_attributes.new(name='Blood',type='BYTE_COLOR',domain='CORNER')
    for i,loop in enumerate(obj.data.loops):
        p=obj.data.vertices[loop.vertex_index].co;v=.70+noise(p*21)*.22+noise(p*71)*.09
        # Dark, branching weathering follows the surface rather than flat bands.
        vein=max(0,1-abs(noise(p*12))/.055)*.2
        rgb=[base[k]*v*(1-vein) for k in range(3)];stain=0
        if mat==skin:
            x,h,z=p.x,p.z,-p.y
            hands=math.exp(-((abs(x)-.47)/.20)**2-((h-.38)/.32)**2)
            collar=math.exp(-((h-1.89)/.20)**2-((x+.03)/.24)**2)
            chest=max(0,min(1,z*14+.6))*math.exp(-(x/.20)**2-((h-1.37)/.36)**2)
            droplets=max(0,math.sin(x*92+noise(p*8)*2))**10*math.exp(-((h-1.05)/.5)**2)
            stain=max(0,min(1,(max(hands,collar,chest*.9,droplets*.7)+noise(p*29)*.20-.12)*1.4))
            fresh=.5+.5*noise(p*13);red=(.11+.14*fresh,.004+.006*fresh,.009+.008*fresh)
            rgb=[rgb[k]*(1-stain)+red[k]*stain for k in range(3)]
        a.data[i].color=(*rgb,1);blood.data[i].color=(stain,stain,stain,1)
paint(body,skin,(.31,.30,.27))
meshes=[body]
# No robe: a dark, recessed thoracic maw replaces the costume silhouette.
robe=None
# Helpers below add detail meshes without putting them back into the skin union.
def detail_oval(name,pos,scale,mat,color):
    o=ell(name,pos,scale);bpy.context.view_layer.objects.active=o
    bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
    paint(o,mat,color);meshes.append(o);o.select_set(False);return o
maw=material('Lightless_thoracic_maw',(0,0,0))
detail_oval('Thoracic_maw',(0,1.52,.060),(.085,.29,.028),maw,(.003,.004,.003))
for i in range(22):
    side=-1 if i%2 else 1;row=i//2;y=1.27+row*.047+random.uniform(-.01,.01);x=side*(.065+random.uniform(0,.018))
    o=limb('Maw_tooth',(x,y,.090),(x*random.uniform(.25,.55),y+random.uniform(-.025,.025),.111),random.uniform(.009,.014),.001)
    bpy.context.view_layer.objects.active=o;bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
    paint(o,bone,(.32,.30,.23));meshes.append(o);o.select_set(False)
# Tangled strands follow the neck, giving the mask an uneven silhouette.
for i in range(19):
    a=i/19*math.tau;cu=bpy.data.curves.new('Matted_strand','CURVE');cu.dimensions='3D';cu.bevel_depth=.008+(i%3)*.003;cu.bevel_resolution=2
    sp=cu.splines.new('BEZIER');sp.bezier_points.add(3)
    pts=[(math.cos(a)*.08,2.00,-.08), (math.cos(a)*.11,1.93+math.sin(i)*.03,-.16), (math.cos(a)*.13,1.77,-.22), (math.cos(a)*.14,1.53-(i%4)*.06,-.24)]
    for pt,point in zip(sp.bezier_points,pts):pt.co=V(point);pt.handle_left_type=pt.handle_right_type='AUTO'
    o=bpy.data.objects.new('Matted_strand',cu);bpy.context.collection.objects.link(o);bpy.context.view_layer.objects.active=o;o.select_set(True);bpy.ops.object.convert(target='MESH');o=bpy.context.object;paint(o,cloth,(.025,.025,.02));meshes.append(o);o.select_set(False)
# Raised irregular ribs/tendons on the exposed front, curved into the chest.
for s in [-1,1]:
    for i in range(6):
        z=1.72-i*.085;w=.255-i*.016
        cu=bpy.data.curves.new('Subdermal_rib','CURVE');cu.dimensions='3D';cu.bevel_depth=.012;cu.bevel_resolution=2
        sp=cu.splines.new('BEZIER');sp.bezier_points.add(3)
        for pt,p in zip(sp.bezier_points,[(s*.025,z,-.23),(s*w,z-.02,-.08),(s*w*.8,z-.055,.027),(s*.045,z-.09,.032)]):pt.co=V(p);pt.handle_left_type=pt.handle_right_type='AUTO'
        o=bpy.data.objects.new('Rib_%s_%s'%(s,i),cu);bpy.context.collection.objects.link(o);bpy.context.view_layer.objects.active=o;o.select_set(True);body.select_set(False);bpy.ops.object.convert(target='MESH');o=bpy.context.object;paint(o,bone,(.32,.27,.19));meshes.append(o);o.select_set(False)
# Rig and deterministic smooth weights. No disconnected ball-joint primitives.
bpy.ops.object.armature_add();rig=bpy.context.object;rig.name='MaskWraith_Rig';bpy.ops.object.mode_set(mode='EDIT');rig.data.edit_bones.remove(rig.data.edit_bones[0]);bones={}
def B(name,a,b,parent=None):
    q=rig.data.edit_bones.new(name);q.head=V(a);q.tail=V(b)
    if parent:q.parent=rig.data.edit_bones[parent]
    bones[name]=(V(a),V(b));return q
B('root',(0,.93,0),(0,1.05,0));B('spine',(0,1.05,-.04),(0,1.72,-.12),'root');B('neck',(0,1.72,-.12),(0,2.09,.04),'spine')
for s,label in [(-1,'L'),(1,'R')]:
    B('arm_'+label,(s*.30,1.78,-.09),(s*.42,1.12,-.02),'spine');B('forearm_'+label,(s*.42,1.12,-.02),(s*.47,.53,.10),'arm_'+label);B('hand_'+label,(s*.47,.53,.10),(s*.47,.18,.18),'forearm_'+label)
    B('extra_arm_'+label,(s*.25,1.47,-.12),(s*.67,1.02,-.03),'spine');B('extra_forearm_'+label,(s*.67,1.02,-.03),(s*.69,.42,.22),'extra_arm_'+label)
    B('thigh_'+label,(s*.115,1.02,-.04),(s*.15,.54,.07),'root');B('shin_'+label,(s*.15,.54,.07),(s*.145,.10,-.02),'thigh_'+label);B('foot_'+label,(s*.145,.10,-.02),(s*.145,.05,.25),'shin_'+label)
bpy.ops.object.mode_set(mode='OBJECT')
def distance(p,a,b):
    d=b-a;t=max(0,min(1,(p-a).dot(d)/d.length_squared));return (p-(a+t*d)).length
for obj in meshes:
    groups={name:obj.vertex_groups.new(name=name) for name in bones}
    for v in obj.data.vertices:
        p=v.co
        if obj==robe:choices=['spine','root']
        elif obj.name.startswith(('Rib','Thoracic','Maw')):choices=['spine']
        elif obj.name.startswith('Matted'):choices=['neck']
        else:choices=list(bones)
        nearest=sorted((distance(p,*bones[name]),name) for name in choices)[:3]
        weights=[1/max(.012,d)**5 for d,n in nearest];total=sum(weights)
        for (_,name),w in zip(nearest,weights):groups[name].add([v.index],w/total,'REPLACE')
    mod=obj.modifiers.new('Deform','ARMATURE');mod.object=rig;obj.parent=rig
# Share a single skinned mesh and material groups instead of one draw per tooth.
bpy.ops.object.select_all(action='DESELECT')
for obj in meshes:obj.select_set(True)
bpy.context.view_layer.objects.active=body;bpy.ops.object.join()
body=bpy.context.object;body.name='MaskWraith_SkinnedSculpt';body.data.validate(clean_customdata=False);body.data.update();meshes=[body]
rig.location.z=-2.07
exec(compile(open('happy_toy/tools/bake_wraith_materials.py',encoding='utf-8').read(),'bake_wraith_materials.py','exec'))
# Alternating sprint with violent, asymmetrical arm sweeps and wrist snaps.
scene=bpy.context.scene;scene.render.fps=30;scene.frame_start=1;scene.frame_end=25
for pb in rig.pose.bones:pb.rotation_mode='XYZ'
for frame in range(1,26):
    t=(frame-1)/24*math.tau
    for label,offset,s in [('L',0,-1),('R',math.pi,1)]:
        p=t+offset;stride=math.sin(p)
        vals={'thigh_'+label:(stride*.85,0,s*.05),'shin_'+label:(max(0,-stride)*1.45,0,0),'foot_'+label:(-.2*max(0,stride),0,0),'arm_'+label:(-stride*1.25,.15*s,math.sin(p*2)*.38+s*.2),'forearm_'+label:(-.7-.65*math.sin(p+.7),0,.13*math.cos(p)),'hand_'+label:(math.sin(p*3)*.4,0,math.cos(p*2)*.3)}
        vals['extra_arm_'+label]=(.5+math.sin(p+.8)*.9,s*.2,s*(.35+.2*math.sin(p)))
        vals['extra_forearm_'+label]=(-.6+math.sin(p*2)*.65,0,s*.25)
        for name,value in vals.items():pb=rig.pose.bones[name];pb.rotation_euler=value;pb.keyframe_insert('rotation_euler',frame=frame)
    pb=rig.pose.bones['spine'];pb.rotation_euler=(.46+.09*math.sin(t),.16*math.sin(t),-.12+.1*math.cos(t));pb.keyframe_insert('rotation_euler',frame=frame)
    pb=rig.pose.bones['neck'];pb.rotation_euler=(-.35,.1*math.sin(t),.3+.05*math.sin(t*3));pb.keyframe_insert('rotation_euler',frame=frame)
    pb=rig.pose.bones['root'];pb.location=(0,-.075+abs(math.sin(t))*.035,0);pb.keyframe_insert('location',frame=frame)
rig.animation_data.action.name='Frenzied_Run'
scene.frame_set(1)
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT,'mask-wraith.blend'))
bpy.ops.export_scene.gltf(filepath=os.path.join(OUT,'mask-wraith.glb'),export_format='GLB',export_animations=True,export_frame_range=True,export_force_sampling=True,export_yup=True)
print('WRAITH_EXPORT',len(body.data.vertices),'vertices',sum(len(o.data.polygons) for o in meshes),'faces',os.path.getsize(os.path.join(OUT,'mask-wraith.glb')))
