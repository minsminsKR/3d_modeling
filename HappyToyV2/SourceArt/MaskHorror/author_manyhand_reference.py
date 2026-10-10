"""Blender 4.0.2 original eight-pair human-arm wraith; private reference is not redistributed."""
from pathlib import Path
import hashlib,json,math,random,sys,argparse
import bpy,bmesh
from mathutils import Vector
HERE=Path(__file__).resolve().parent;PROJECT=HERE.parents[1];OUT=PROJECT/'Assets/Resources/MaskHorror'
sys.path.insert(0,str(HERE))
REFERENCE_SHA='0667ecacad32d2904108bc26f79616732babcf66f4d825253a3720c2d7190295'
def coord(p):return (p[0],-p[2],p[1])
def unity(p):return (p[0],p[2],-p[1])
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def select(objects,active=None):
    bpy.ops.object.select_all(action='DESELECT')
    for o in objects:o.select_set(True)
    bpy.context.view_layer.objects.active=active or objects[0]
def material(key):
    m=bpy.data.materials.get(key)
    if m:return m
    m=bpy.data.materials.new(key);m.use_nodes=True
    from sculpt_and_bake import shader_sculpt
    shader_sculpt(m);return m
def mesh(name,vertices,faces,slot,parent=None):
    data=bpy.data.meshes.new(name);data.from_pydata([coord(p) for p in vertices],[],faces);data.update()
    bm=bmesh.new();bm.from_mesh(data);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(data);bm.free()
    o=bpy.data.objects.new(name,data);bpy.context.collection.objects.link(o);data.materials.append(material(slot))
    uv=data.uv_layers.new(name='UVMap')
    for p in data.polygons:
        p.use_smooth=True;n=Vector(unity(p.normal));a=max(range(3),key=lambda i:abs(n[i]))
        for loop in p.loop_indices:
            v=unity(data.vertices[data.loops[loop].vertex_index].co);q=(v[0],v[2]) if a==1 else (v[2],v[1]) if a==0 else (v[0],v[1]);uv.data[loop].uv=(q[0]/.2,q[1]/.2)
    if parent:o.parent=parent;o.matrix_parent_inverse=parent.matrix_world.inverted()
    return o
def empty(name,at,parent=None):
    o=bpy.data.objects.new(name,None);bpy.context.collection.objects.link(o);o.location=coord(at)
    if parent:o.parent=parent;o.matrix_parent_inverse=parent.matrix_world.inverted()
    bpy.context.view_layer.update();return o
def tube(name,path,radii,slot='MW_skin',parent=None,sides=8,ellipse=1):
    if len(path)>2:
        smooth,sizes=[],[]
        for i in range(len(path)-1):
            a,b,c,d=(Vector(path[max(0,i-1)]),Vector(path[i]),Vector(path[i+1]),Vector(path[min(len(path)-1,i+2)]))
            for j in range(3):
                t=j/3;smooth.append(tuple(.5*((2*b)+(-a+c)*t+(2*a-5*b+4*c-d)*t*t+(-a+3*b-3*c+d)*t*t*t)));sizes.append(radii[i]*(1-t)+radii[i+1]*t)
        path=smooth+[path[-1]];radii=sizes+[radii[-1]]
    vs,fs=[],[]
    for i,p in enumerate(path):
        tangent=Vector(path[min(len(path)-1,i+1)])-Vector(path[max(0,i-1)]);tangent.normalize();a=tangent.cross(Vector((0,0,1)))
        if a.length<.001:a=tangent.cross(Vector((0,1,0)))
        a.normalize();b=tangent.cross(a).normalized()
        for j in range(sides):
            angle=j*math.pi*2/sides;vs.append(tuple(Vector(p)+radii[i]*(a*math.cos(angle)+b*math.sin(angle)*ellipse)))
        if i:
            for j in range(sides):k=(j+1)%sides;fs.append(((i-1)*sides+j,(i-1)*sides+k,i*sides+k,i*sides+j))
    for j in range(1,sides-1):fs.append((0,j+1,j));k=(len(path)-1)*sides;fs.append((k,k+j,k+j+1))
    return mesh(name,vs,fs,slot,parent)
def ellipsoid(name,at,s,slot='MW_skin',parent=None,rings=8,sides=14):
    vs,fs=[],[]
    for i in range(rings+1):
        a=math.pi*i/rings
        for j in range(sides):b=j*math.pi*2/sides;vs.append((at[0]+s[0]*math.sin(a)*math.cos(b),at[1]+s[1]*math.cos(a),at[2]+s[2]*math.sin(a)*math.sin(b)))
    for i in range(rings):
        for j in range(sides):k=(j+1)%sides;fs.append((i*sides+j,i*sides+k,(i+1)*sides+k,(i+1)*sides+j))
    return mesh(name,vs,fs,slot,parent)
def bell(at,radius,parent,name):
    profile=[(0,.062),(.028,.060),(.050,.034),(.059,-.028),(.050,-.033),(.043,-.021),(.042,.022),(.021,.045),(0,.047)]
    vs,fs=[],[];sides=16;k=radius/.059
    for r,y in profile:
        for j in range(sides):a=j*math.pi*2/sides;vs.append((at[0]+r*k*math.cos(a),at[1]+y*k,at[2]+r*k*math.sin(a)))
    for i in range(len(profile)-1):
        for j in range(sides):q=(j+1)%sides;fs.append((i*sides+j,i*sides+q,(i+1)*sides+q,(i+1)*sides+j))
    mesh(name,vs,fs,'MW_bell',parent);tube(name+' clapper',[at,(at[0],at[1]-.030,at[2])],[.006,.009],'MW_bell',parent,6)
def merge_groups():
    groups={}
    for o in list(bpy.context.scene.objects):
        if o.type=='MESH':groups.setdefault((o.parent,o.data.materials[0].name),[]).append(o)
    for (parent,slot),objects in groups.items():
        if slot in {'MW_eye','MW_teeth'} or any(o.name=='Aged human smiling mask shell' for o in objects):continue
        select(objects);bpy.ops.object.join();o=bpy.context.object
        if slot=='MW_skin' and parent.name.startswith('ArmSwing'):o.name='Human five-finger arm '+parent.name[8:]
        elif slot=='MW_hair':o.name='Wet hair strands '+parent.name
        elif slot=='MW_bell':o.name='Hollow black bells '+parent.name
        elif slot=='MW_teeth':o.name='Yellowed human teeth '+parent.name
        else:o.name=slot+' anatomy '+parent.name
def arm(index,side,segment,height,z):
    suffix='%02d%s'%(index,'L' if side<0 else 'R');taper=1-index*.045
    shoulder=(side*.31,height,z);p=empty('ArmSwing'+suffix,shoulder,segment)
    asym=.022*math.sin(index*2.11+side)
    elbow=(side*(.67+.03*(index%2)+asym),height*(.58+asym),z-.17+.055*math.sin(index+side))
    wrist=(side*(.80+.03*(index%3)+asym),.23+asym,z-.50-(.08 if index==0 else 0)+asym)
    tube('Human biceps '+suffix,[shoulder,(side*.44,height-.12,z-.015),elbow],[.16*taper,.17*taper,.086*taper],parent=p,ellipse=.82)
    ellipsoid('Olecranon elbow '+suffix,elbow,(.093*taper,.12*taper,.095*taper),parent=p)
    tube('Human forearm '+suffix,[elbow,(side*.76,.48,z-.33),wrist],[.105*taper,.135*taper,.057*taper],parent=p,ellipse=.78)
    hand=(wrist[0],.12,wrist[2]-.10);ellipsoid('Human metacarpal palm '+suffix,hand,(.12*taper,.082,.165*taper),parent=p)
    for f in range(4):
        offset=side*(-.069+f*.047)*taper;length=[.24,.285,.265,.20][f]*taper;x=hand[0]+offset;start=hand[2]-.095
        path=[(x,.12,start),(x+offset*.2,.075,start-length*.45),(x+offset*.35,.036,start-length*.82),(x+offset*.3,.025,start-length)]
        tube('Human finger %s %d'%(suffix,f),path,[.026*taper,.023*taper,.018*taper,.012*taper],parent=p,sides=8,ellipse=.82)
        ellipsoid('Human finger knuckle %s %d'%(suffix,f),path[1],(.029*taper,.033*taper,.027*taper),parent=p,rings=6,sides=10)
        ellipsoid('Damaged human fingernail %s %d'%(suffix,f),(path[-1][0],.039,path[-1][2]+.024),(.012*taper,.003,.022*taper),'MW_rope',p,6,8)
    tube('Opposable human thumb '+suffix,[(hand[0]-side*.09,.13,hand[2]+.035),(hand[0]-side*.165,.077,hand[2]-.04),(hand[0]-side*.18,.033,hand[2]-.16)],[.036*taper,.027*taper,.016*taper],parent=p)
    for i in range(3):
        x=hand[0]+side*(-.05+i*.044);tube('Raised forearm tendon '+suffix+str(i),[(elbow[0],elbow[1]-.03,elbow[2]-.09),(x,.34,wrist[2]-.045),(x,.14,hand[2]-.065)],[.008,.008,.004],parent=p,sides=6)
    for band in range(2):
        path=[]
        for j in range(25):a=j*math.pi*2/24;path.append((wrist[0]+.073*math.cos(a),.32+band*.044+.027*math.sin(a),wrist[2]+.052*math.sin(a)))
        tube('Black wrist binding '+suffix+str(band),path,[.006]*len(path),'MW_rope',p,6)
    empty('HandContact'+suffix,(hand[0],.020,hand[2]-.25*taper),p)
def body():
    root=empty('WraithAuthoredBody',(0,0,0));rng=random.Random(431)
    for i in range(8):
        z=i*.435;height=1.22-.076*i;s=empty('Segment%02d'%i,(0,0,z),root)
        ellipsoid('Low human thorax %02d'%i,(-.045*math.sin(i*1.4),height-.09,z+(.20 if i==0 else .05)),
            (.30-i*.014+.018*math.sin(i),.25-i*.009,.245 if i==0 else .31),parent=s)
        ellipsoid('Hunched trapezius %02d'%i,(-.10 if i%2 else .08,height+.12+.018*math.sin(i*2),z+.18),(.31-i*.012,.20,.27),parent=s)
        for side in (-1,1):arm(i,side,s,height,z)
        for line in range(3):
            path=[]
            for j in range(19):a=j*math.pi/18;path.append((.40*math.cos(a),height-.03+.36*math.sin(a),z-.14+line*.12+.025*math.sin(a*3)))
            tube('Interwoven body ligament %02d %d'%(i,line),path,[.012]*len(path),'MW_rope',s,6)
        for k in range(28):
            x=rng.uniform(-.34,.34);b=rng.uniform(-.07,.07);path=[(x,height+.23,z-.16),(x+b,height+.32,z-.02),(x+b*.8,height+.25,z+.23),(x+b*.4,height+.02,z+.47)]
            tube('Greasy body hair %02d %d'%(i,k),path,[.0045,.004,.003,.0015],'MW_hair',s,5)
        if i<3:
            for side in (-1,1):
                path=[(side*.26,height+.12,z-.12),(side*.34,height-.09,z-.20),(side*.30,height-.31,z-.17)]
                tube('Black shoulder bell cord',path,[.009]*3,'MW_rope',s,6);bell(path[-1],.046,s,'Small shoulder bell')
    s=bpy.data.objects['Segment00'];empty('HeadSocket',(0,1.12,-.22),s);empty('HeadFrontTarget',(0,1.45,-.48),s)
    merge_groups();return root
def width(y):return .255*math.sqrt(max(.001,1-((y-.005)/.443)**2))*(1+.11*math.exp(-((y+.035)/.09)**2))
def smile(x):return -.216+.037*(x/.15)**2+.008*math.sin(x*16)
def inside(x,y):
    if y<-.421 or y>.439 or abs(x)>width(y):return False
    a=((x+.112)/.080)**2+((y-(.080-.045*(x+.112)))/.0145)**2<1
    b=((x-.107)/.078)**2+((y-(.083+.05*(x-.107)))/.016)**2<1
    c=(x/.162)**2+((y-smile(x))/.046)**2<1
    d=any(((x-q)/.010)**2+((y+.062)/.008)**2<1 for q in (-.032,.034))
    return not(a or b or c or d)
def face_z(x,y):
    dome=math.sqrt(max(0,1-(x/max(.01,width(y)))**2));z=-.023-.138*dome-.038*math.exp(-((y-.26)/.19)**2)*dome
    z-=.052*math.exp(-((abs(x)-.139)/.060)**2-((y+.044)/.088)**2)
    z-=.032*math.exp(-((abs(x)-.106)/.09)**2-((y-.120)/.023)**2)
    z-=.106*math.exp(-((x+.006)/.034)**2-((y-.035)/.15)**2)
    z-=.073*math.exp(-((x-.003)/.045)**2-((y+.040)/.042)**2)
    return z-.031*math.exp(-((y+.347)/.072)**2)*dome+.005*math.sin(x*15+y*8)
def mask():
    root=empty('WraithAuthoredMask',(0,0,0));vs,fs,lookup=[],[],{};n,m=92,124
    def v(i,j,back):
        key=(i,j,back)
        if key in lookup:return lookup[key]
        x=-.29+i*.58/n;y=-.438+j*.90/m;lookup[key]=len(vs);vs.append((x+.008*math.sin(y*7)-.012*max(0,-y)/.43,y,face_z(x,y)+(.016 if back else 0)));return lookup[key]
    used=set()
    for j in range(m):
        for i in range(n):
            if not inside(-.29+(i+.5)*.58/n,-.438+(j+.5)*.90/m):continue
            used.add((i,j));fs.append((v(i,j+1,0),v(i+1,j+1,0),v(i+1,j,0),v(i,j,0)));fs.append((v(i,j,1),v(i+1,j,1),v(i+1,j+1,1),v(i,j+1,1)))
    adj={}
    for i,j in used:
        for neighbor,edge in [((i-1,j),((i,j+1),(i,j))),((i+1,j),((i+1,j),(i+1,j+1))),((i,j-1),((i,j),(i+1,j))),((i,j+1),((i+1,j+1),(i,j+1)))]:
            if neighbor in used:continue
            a,b=edge;fs.append((v(*a,0),v(*b,0),v(*b,1),v(*a,1)))
            for back in (0,1):va,vb=v(*a,back),v(*b,back);adj.setdefault(va,set()).add(vb);adj.setdefault(vb,set()).add(va)
    for _ in range(4):
        changes={k:tuple(Vector(vs[k])*.3+sum((Vector(vs[j]) for j in neighbors),Vector())*.7/len(neighbors)) for k,neighbors in adj.items()}
        for k,p in changes.items():vs[k]=p
    mesh('Aged human smiling mask shell',vs,fs,'MW_mask',root)
    for x,y,label in [(-.112,.080,'L'),(.107,.083,'R')]:ellipsoid('Black eye slit '+label,(x,y,-.094),(.076,.012,.009),'MW_eye',root,6,16)
    ellipsoid('Dark recessed oral cavity',(0,-.205,-.070),(.16,.057,.037),'MW_rope',root,8,20)
    for i in range(10):
        x=-.115+i*.025;y=smile(x)+.018+.003*math.sin(i*1.7);h=.024+(i%3)*.002
        vertices=[(x+sx*.010,y+sy*h,-.187+sz*.007) for sx,sy,sz in [(-1,-.5,-1),(1,-.5,-1),(1,.5,-1),(-1,.5,-1),(-1,-.5,1),(1,-.5,1),(1,.5,1),(-1,.5,1)]]
        tooth=mesh('Yellowed human tooth %02d'%i,vertices,[(0,1,2,3),(4,7,6,5),(0,4,5,1),(1,5,6,2),(2,6,7,3),(3,7,4,0)],'MW_teeth',root)
        select([tooth]);mod=tooth.modifiers.new('Worn irregular incisor edge','BEVEL');mod.width=.002;mod.segments=2;bpy.ops.object.modifier_apply(modifier=mod.name)
    for side in (-1,1):
        path=[]
        for i in range(26):x=-.162+i*.324/25;y=smile(x)+side*.041*math.sqrt(max(.01,1-(x/.162)**2));path.append((x-.009,y,face_z(x,y)-.002))
        tube('Cracked dark smile lip',path,[.003]*len(path),'MW_rope',root,6)
    for number,x0 in enumerate((-.19,-.063,.085,.175)):
        path=[]
        for i in range(11+number):
            y=.37-number*.046-i*.045;x=x0+.013*math.sin(i*1.8+number)+.007*math.sin(i*.41)
            if abs(x)<=width(y):path.append((x,y,face_z(x,y)-.0015))
        if len(path)>2:
            tube('Old vertical porcelain fissure',path,[.0009]*len(path),'MW_rope',root,5)
            for k in (3,6,9):
                if k>=len(path):continue
                a=path[k];branch=[]
                for j in range(4):
                    x=a[0]+(-1 if k%2 else 1)*(.010*j+.003*math.sin(j*2+number));y=a[1]-.010*j
                    if abs(x)<width(y):branch.append((x,y,face_z(x,y)-.001))
                if len(branch)>1:tube('Branching porcelain hairline crack',branch,[.00055]*len(branch),'MW_rope',root,5)
    rng=random.Random(9127)
    for k in range(100):
        x=rng.uniform(-.22,.22);y=rng.uniform(.34,.445);z=rng.uniform(.015,.075);side=-1 if x<0 else 1
        tube('Greasy crown and rear scalp hair',[(x,y,z),(x+side*.05,y+.045,z-.045),(x+side*.10,y-.21,z+.12),(x+side*.16,y-.69,z+.18)],
             [.004,.004,.003,.0016],'MW_hair',root,5)
    for side in (-1,1):
        for k in range(120):
            x=side*rng.uniform(.19,.31);y=rng.uniform(.33,.49);z=rng.uniform(-.13,.055);length=rng.uniform(.66,1.02);b=rng.uniform(-.035,.035)
            tube('Wet facial hair',[(x*.76,y,z),(x,y-.18,z-.04),(x+side*b,y-.43,z+.035),(x+side*.03,y-length,z+.13)],
                 [.0035+rng.random()*.002,.004,.003,.0015],'MW_hair',root,5)
    for i in range(3):
        y=-.44-i*.073;path=[]
        for j in range(26):a=j*math.pi/25;path.append((.245*math.cos(a),y-.05*math.sin(a),.015-.095*math.sin(a)))
        tube('Black silk neck binding',path,[.009]*len(path),'MW_rope',root,7)
        for x in (-.19,-.065,.08,.19):bell((x,y-.046,-.057),.031+(.008 if i==1 else 0),root,'Small black neck bell')
    empty('FaceJoint',(0,-.395,.035),root);empty('FaceFront',(0,.04,-.32),root);empty('FaceRear',(0,.04,.06),root)
    empty('Human nose tip landmark',(.003,-.04,face_z(.003,-.04)),root);empty('Nose rear landmark',(.003,-.04,-.070),root)
    merge_groups();return root
def finalize(key,root):
    bpy.context.view_layer.update();objects=[o for o in bpy.context.scene.objects if o.type in {'MESH','EMPTY'}];source=HERE/(key+'.blend')
    bpy.ops.wm.save_as_mainfile(filepath=str(source),compress=True);bpy.ops.file.make_paths_relative();bpy.ops.wm.save_as_mainfile(filepath=str(source),compress=True)
    select(objects,root);fbx=OUT/(key+'.fbx');bpy.ops.export_scene.fbx(filepath=str(fbx),use_selection=True,object_types={'MESH','EMPTY'},apply_unit_scale=True,global_scale=1,
        axis_forward='-Z',axis_up='Y',use_mesh_modifiers=True,mesh_smooth_type='FACE',use_tspace=True,add_leaf_bones=False,bake_anim=False,path_mode='RELATIVE')
    meshes=[o for o in objects if o.type=='MESH'];points=[unity(o.matrix_world@v.co) for o in meshes for v in o.data.vertices]
    for o in meshes:o.data.calc_loop_triangles()
    return dict(key=key,source=source.relative_to(PROJECT).as_posix(),fbx=fbx.relative_to(PROJECT).as_posix(),sourceSha256=sha(source),fbxSha256=sha(fbx),
        triangles=sum(len(o.data.loop_triangles) for o in meshes),vertices=sum(len(o.data.vertices) for o in meshes),boundsMin=[min(p[i] for p in points) for i in range(3)],
        boundsMax=[max(p[i] for p in points) for i in range(3)],meshParts=[o.name for o in meshes],pivotNames=[o.name for o in objects if o.type=='EMPTY'],materialSlots=sorted({m.name for o in meshes for m in o.data.materials}))
def main():
    parser=argparse.ArgumentParser();parser.add_argument('--check-only',action='store_true');args=parser.parse_args(sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else [])
    bpy.context.preferences.filepaths.save_version=0;OUT.mkdir(parents=True,exist_ok=True);records=[]
    for key,build in [('wraith-body',body),('wraith-mask',mask)]:
        bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False);bpy.context.scene.unit_settings.system='METRIC';bpy.context.scene.unit_settings.scale_length=1;root=build()
        if args.check_only:print('MANYHAND_GEOMETRY_CHECK_PASS',key,len(bpy.context.scene.objects),flush=True);continue
        records.append(finalize(key,root));print('MANYHAND_GEOMETRY_AUTHORED',key,records[-1]['triangles'],flush=True)
    if args.check_only:return
    (HERE/'model-manifest.json').write_text(json.dumps(dict(schema=2,blender=bpy.app.version_string,license='CC0-1.0 original geometry/procedural maps',
        reference=dict(sha256=REFERENCE_SHA,provenance='Private user-provided visual reference, not redistributed or relicensed'),
        axes='metres Y-up front -Z; imported front -Z rotated by wrapper to actor +Z',runtime=dict(armPairs=8,bodyLength=3.95,bodyWidth=2.26,bodyHeight=1.64,maskFaceHeight=.74,
        attachment='Front HeadSocket to FaceJoint; face-only scale excludes long hair/bells'),assets=records),indent=2)+'\n',encoding='utf-8')
if __name__=='__main__':main()
