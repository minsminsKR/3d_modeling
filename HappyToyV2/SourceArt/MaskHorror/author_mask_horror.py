"""Original grotesque corridor wraith, Blender 4.0.2, no add-ons.

Source plan metres, Y up/front -Z; identical conversion to CorridorFurnishings.
Runtime keeps imported axes and original animated monster/attachment pivots.
"""
from pathlib import Path
import argparse
import hashlib
import json
import math
import random
import sys
import bpy
from mathutils import Vector

HERE = Path(__file__).resolve().parent
PROJECT = HERE.parents[1]
OUT = PROJECT / 'Assets/Resources/MaskHorror'
PALETTE = {
    'MW_bone': ('plaster-damp', (.42, .48, .31), .0, .18),
    'MW_flesh': ('cloth-charred', (.39, .43, .31), .0, .12),
    'MW_mask': ('plaster-damp', (.61, .66, .48), .0, .21),
    'MW_veil': ('cloth-charred', (.30, .35, .27), .0, .11),
    'MW_scar': ('cloth-charred', (.095, .065, .055), .0, .05),
    'MW_iron': ('metal-rust', (.18, .23, .16), .65, .13),
    'MW_eye': (None, (.055, .001, .001), .0, .20),
}

def coord(p): return (p[0], -p[2], p[1])
def unity(p): return (p[0], p[2], -p[1])
def digest(p): return hashlib.sha256(p.read_bytes()).hexdigest()

def material(key):
    if bpy.data.materials.get(key): return bpy.data.materials[key]
    surface, tint, metal, smooth = PALETTE[key]
    m = bpy.data.materials.new(key); m.diffuse_color = (*tint, 1); m.use_nodes = True
    n, links = m.node_tree.nodes, m.node_tree.links
    shader = n.get('Principled BSDF'); shader.inputs['Base Color'].default_value = (*tint, 1)
    shader.inputs['Metallic'].default_value = metal; shader.inputs['Roughness'].default_value = 1-smooth
    if surface:
        for typ in ('albedo', 'normal', 'metallic-smoothness'):
            p = PROJECT / 'Assets/Resources/GraphicsPbr' / surface / (typ+'.png')
            if not p.exists(): raise FileNotFoundError(p)
            tex = n.new('ShaderNodeTexImage'); tex.image = bpy.data.images.load(str(p), check_existing=True)
            if typ != 'albedo': tex.image.colorspace_settings.name = 'Non-Color'
            if typ == 'albedo':
                mult = n.new('ShaderNodeMixRGB'); mult.blend_type = 'MULTIPLY'; mult.inputs[0].default_value = 1
                mult.inputs[2].default_value = (*tint, 1)
                links.new(tex.outputs['Color'], mult.inputs[1]); links.new(mult.outputs[0], shader.inputs['Base Color'])
            elif typ == 'normal':
                normal = n.new('ShaderNodeNormalMap'); normal.inputs['Strength'].default_value = .7
                links.new(tex.outputs['Color'], normal.inputs['Color']); links.new(normal.outputs[0], shader.inputs['Normal'])
    else:
        shader.inputs['Emission Color'].default_value = (.65, .002, .001, 1)
        shader.inputs['Emission Strength'].default_value = 1.1
    return m

def mesh(name, vertices, faces, slot, parent=None):
    data = bpy.data.meshes.new(name); data.from_pydata([coord(p) for p in vertices], [], faces); data.update()
    obj = bpy.data.objects.new(name, data); bpy.context.collection.objects.link(obj)
    data.materials.append(material(slot)); uv = data.uv_layers.new(name='UVMap')
    for polygon in data.polygons:
        polygon.use_smooth = True
        normal = Vector(unity(polygon.normal)); dominant = max(range(3), key=lambda i: abs(normal[i]))
        for loop in polygon.loop_indices:
            p = unity(data.vertices[data.loops[loop].vertex_index].co)
            pair = (p[0], p[2]) if dominant == 1 else (p[2], p[1]) if dominant == 0 else (p[0], p[1])
            uv.data[loop].uv = (pair[0]/.27, pair[1]/.27)
    if parent:
        obj.parent = parent; obj.matrix_parent_inverse = parent.matrix_world.inverted()
    return obj

def empty(name, at, parent=None):
    obj = bpy.data.objects.new(name, None); bpy.context.collection.objects.link(obj)
    obj.location = coord(at)
    if parent:
        obj.parent = parent; obj.matrix_parent_inverse = parent.matrix_world.inverted()
    bpy.context.view_layer.update()
    return obj

def tube(name, path, radii, slot='MW_flesh', sides=12, parent=None, ellipse=1):
    # Cubic continuous centerline: crooked anatomy without cylindrical elbow seams.
    if len(path)>2:
        smooth, sizes=[],[]
        for segment in range(len(path)-1):
            a=Vector(path[max(0,segment-1)]);b=Vector(path[segment])
            c=Vector(path[segment+1]);d=Vector(path[min(len(path)-1,segment+2)])
            for step in range(3):
                t=step/3
                p=.5*((2*b)+(-a+c)*t+(2*a-5*b+4*c-d)*t*t+(-a+3*b-3*c+d)*t*t*t)
                smooth.append(tuple(p));sizes.append(radii[segment]*(1-t)+radii[segment+1]*t)
        path=smooth+[path[-1]];radii=sizes+[radii[-1]]
    verts, faces = [], []
    for i, at in enumerate(path):
        p = Vector(at)
        tangent = Vector(path[min(len(path)-1, i+1)]) - Vector(path[max(0, i-1)])
        tangent.normalize(); a = tangent.cross(Vector((0, 0, 1)))
        if a.length < .001: a = tangent.cross(Vector((0, 1, 0)))
        a.normalize(); b = tangent.cross(a).normalized()
        for j in range(sides):
            angle = j*2*math.pi/sides
            q = p + radii[i]*(a*math.cos(angle)+b*math.sin(angle)*ellipse)
            verts.append(tuple(q))
        if i:
            for j in range(sides):
                k = (j+1)%sides; faces.append(((i-1)*sides+j, (i-1)*sides+k, i*sides+k, i*sides+j))
    # Explicit triangle fans export actual tangent space on every capped tube.
    for j in range(1,sides-1):
        faces.append((0,j+1,j))
        base=(len(path)-1)*sides;faces.append((base,base+j,base+j+1))
    return mesh(name, verts, faces, slot, parent)

def ellipsoid(name, at, scale, slot, parent=None):
    verts, faces = [], []
    rings, sides = 12, 20
    for i in range(rings+1):
        theta = math.pi*i/rings
        for j in range(sides):
            phi = math.pi*2*j/sides
            verts.append((at[0]+scale[0]*math.sin(theta)*math.cos(phi),
                          at[1]+scale[1]*math.cos(theta), at[2]+scale[2]*math.sin(theta)*math.sin(phi)))
    for i in range(rings):
        for j in range(sides):
            k = (j+1)%sides; faces.append((i*sides+j, i*sides+k, (i+1)*sides+k, (i+1)*sides+j))
    return mesh(name, verts, faces, slot, parent)

def body():
    root = empty('WraithAuthoredBody', (0,0,0))
    # A hunch, open thorax and bent limbs occupy the hallway without inflating
    # the navigation capsule. Uneven shoulders and limbs remain actual geometry.
    tube('Crooked open vertebral column', [(.02,.36,.12),(-.12,.67,.17),(-.15,.98,.20),
        (-.07,1.30,.16),(.04,1.53,.02)], [.17,.14,.18,.24,.13], parent=root, ellipse=.8)
    ellipsoid('Emaciated offset thorax',(-.055,1.015,.065),(.235,.40,.15),'MW_flesh',root)
    ellipsoid('Hunched left trapezius',(-.26,1.395,.085),(.34,.16,.17),'MW_flesh',root)
    ellipsoid('Stretched right clavicle',(.26,1.33,.015),(.31,.10,.13),'MW_flesh',root)
    for i in range(9):
        y = .68+i*.084
        for side in (-1,1):
            width = .28+.20*math.sin(i/8*math.pi)
            path = [(-.10,y,.12),(side*width*.65,y+.025,.06),(side*width,y-.01,-.08),
                    (side*width*.78,y-.07,-.24),(side*.10,y-.10,-.30)]
            # Exposed bone alternates with narrow wet-looking fibrous folds.
            tube('Exposed uneven rib %d %d'%(i,side), path, [.028,.034,.024,.018,.008],
                 'MW_bone' if (i+side)%3==0 else 'MW_flesh', parent=root)
    tube('Dislocated collar arch', [(-.63,1.34,.035),(-.29,1.54,.05),(.09,1.57,-.015),
        (.43,1.39,-.025),(.58,1.25,-.03)], [.09,.12,.11,.11,.075], parent=root)
    # Named parent pivots survive FBX and are animated by actual locomotion.
    for side in (-1,1):
        suffix = 'L' if side < 0 else 'R'
        arm = empty('ArmSwing'+suffix, (side*.49,1.29,-.02), root)
        crooked=.11 if side<0 else -.04
        path = [(side*.49,1.29,-.02),(side*.86,1.08+crooked,.03),(side*1.04,.81+crooked,-.07),
                (side*1.13,.60,-.18)]
        tube('Long ruptured upper arm '+suffix,path[:3],[.16,.11,.077],parent=arm,ellipse=.65)
        tube('Hooked lower arm '+suffix,path[2:]+[(side*1.08,.37,-.21)], [.074,.08,.043],parent=arm)
        ellipsoid('Oversized claw palm '+suffix,(side*1.08,.34,-.20),(.10,.13,.045),'MW_flesh',arm)
        for finger in range(4 if side<0 else 3):
            x = side*(1.02+finger*.052); y = .30+(finger%2)*.025
            tube('Separated hooked finger %s%d'%(suffix,finger),[(x,y,-.22),(x+side*.045,y-.15,-.245),
                (x+side*.06,y-.235,-.20),(x+side*.04,y-.26,-.15)], [.025,.019,.014,.006],parent=arm,sides=8)
            ellipsoid('Swollen claw knuckle %s%d'%(suffix,finger),(x+side*.04,y-.145,-.245),
                (.029,.034,.025),'MW_flesh',arm)
            tube('Long split black fingernail %s%d'%(suffix,finger),[(x+side*.04,y-.24,-.17),
                (x+side*.032,y-.295,-.13)],[.010,.001],'MW_scar',parent=arm,sides=8)
        for tendon in range(4):
            x=side*(1.0+tendon*.022)
            tube('Raised forearm tendon %s%d'%(suffix,tendon),[(side*.88,1.02+crooked,-.02),
                (side*1.036,.78,-.12),(x,.52,-.225),(x,.37,-.25)],[.011,.012,.008,.005],
                 'MW_bone' if tendon==0 else 'MW_flesh',parent=arm,sides=8)
        # Torn gauze is double-sided actual surfaces with a ragged cut edge.
        verts, faces = [], []
        for row in range(8):
            t = row/7
            for col in range(10):
                u=col/9; x=side*(.22+.74*u)
                y=1.36-t*(.40+.32*u)-.14*t*t*math.sin(u*35)
                z=.12+.03*math.sin(u*22+t*9)
                verts.append((x,y,z))
        for row in range(7):
            for col in range(9):
                k=row*10+col
                if row>3 and (row+col)%7==0: continue
                f=(k,k+1,k+11,k+10); faces.extend([f,tuple(reversed(f))])
        mesh('Shredded shoulder veil '+suffix,verts,faces,'MW_veil',arm)
        leg = empty('LegSwing'+suffix,(side*.20,.66,.05),root)
        tube('Reverse bent thigh '+suffix,[(side*.20,.66,.05),(side*.34,.43+crooked*.4,.23),
             (side*.36,.21,.16)], [.105,.091,.06], parent=leg, ellipse=.8)
        tube('Thin running shin '+suffix,[(side*.36,.21,.16),(side*.27,.09,-.10),
             (side*.27,.05,-.22)], [.065,.05,.033], parent=leg)
        for toe in range(3):
            x=side*.27+(toe-1)*.052
            tube('Splayed long toe %s%d'%(suffix,toe),[(x,.055,-.19),(x,.044,-.33),
                 (x+(toe-1)*.026,.035,-.39)], [.028,.021,.009],parent=leg,sides=8)
    return root

def head_width(y):
    # Cranial dome, projecting zygomatic bones, tapered jaw and a true chin.
    return .335*math.sqrt(max(.001,1-((y-.035)/.525)**2))*(1+.12*math.exp(-((y-.015)/.10)**2))

def inside(x,y):
    outside=abs(x)>head_width(y) or y<-.477 or y>.545
    left=((x+.142)/.092)**2+((y-.185+x*.11)/.046)**2<1
    right=((x-.127)/.076)**2+((y-.180+x*.12)/.073)**2<1
    mouth=((x-.014)/(.111+.009*math.sin(y*39)))**2+((y+.172-x*.24)/.158)**2<1
    nose=any(((x-a)/.011)**2+((y+.012)/.008)**2<1 for a in (-.035,.037))
    return not (outside or left or right or mouth or nose)

def mask():
    root=empty('WraithAuthoredMask',(0,0,0)); verts, faces=[],[]; lookup={}
    n,m=78,96
    def vertex(i,j,back):
        key=(i,j,back)
        if key in lookup: return lookup[key]
        x=-.37+i*.74/n; y=-.49+j*1.06/m
        width=head_width(y); dome=math.sqrt(max(0,1-(x/max(.01,width))**2))
        z=-.015-.175*dome
        # Actual volumetric forehead, asymmetric cheekbones, eyebrows and nose.
        z-=.050*math.exp(-((y-.34)/.18)**2)*dome
        z-=.070*math.exp(-((abs(x)-.19)/.07)**2-((y-.035)/.10)**2)
        z-=.058*math.exp(-((abs(x)-.15)/.10)**2-((y-.245)/.035)**2)
        z-=.135*math.exp(-((x+.012)/.042)**2-((y-.095)/.17)**2)
        z-=.038*math.exp(-((y+.385)/.07)**2)*dome
        z+=.013*math.sin(x*16+y*10)+(.026 if back else 0)
        vx=x+.022*math.sin(y*8)-.044*max(0,-y)/.49
        lookup[key]=len(verts); verts.append((vx,y,z)); return lookup[key]
    used=set()
    for j in range(m):
        for i in range(n):
            x=-.37+(i+.5)*.74/n; y=-.49+(j+.5)*1.06/m
            if not inside(x,y): continue
            used.add((i,j)); a,b,c,d=vertex(i,j,0),vertex(i+1,j,0),vertex(i+1,j+1,0),vertex(i,j+1,0)
            faces.append((d,c,b,a)); a,b,c,d=vertex(i,j,1),vertex(i+1,j,1),vertex(i+1,j+1,1),vertex(i,j+1,1)
            faces.append((a,b,c,d))
    boundary=set();adj={}
    for i,j in used:
        for neighbor,edge in [((i-1,j),((i,j+1),(i,j))),((i+1,j),((i+1,j),(i+1,j+1))),
                              ((i,j-1),((i,j),(i+1,j))),((i,j+1),((i+1,j+1),(i,j+1)))]:
            if neighbor in used: continue
            a,b=edge; faces.append((vertex(*a,0),vertex(*b,0),vertex(*b,1),vertex(*a,1)))
            for back in (0,1):
                va,vb=vertex(*a,back),vertex(*b,back);boundary.update((va,vb))
                adj.setdefault(va,set()).add(vb);adj.setdefault(vb,set()).add(va)
    # Smooth only true aperture/silhouette edge loops, preserving open holes and
    # shell thickness; avoid a coarse grid staircase on close-up silhouettes.
    for _ in range(5):
        new={k:tuple(Vector(verts[k])*.35+sum((Vector(verts[j]) for j in adj[k]),Vector())*.65/len(adj[k])) for k in boundary}
        for k,value in new.items():verts[k]=value
    mesh('Cracked porcelain shell with real hollow sockets',verts,faces,'MW_mask',root)
    # These sit behind open sockets. Tiny embedded red slits, no billboard glow.
    for x,y,scale in [(-.142,.20,(.014,.005,.004)),(.127,.166,(.006,.018,.004))]:
        ellipsoid('Buried red eye slit',(x,y,-.132),scale,'MW_eye',root)
        ellipsoid('Deep bruised socket backing',(x,y,-.103),(.087 if x<0 else .072,.047 if x<0 else .073,.022),'MW_scar',root)
    for x in (-.035,.037):ellipsoid('Recessed nostril',(x,-.012,-.24),(.012,.008,.007),'MW_scar',root)
    rng=random.Random(8011)
    for side in (-1,1):
        for k in range(7):
            x=-.066+k*.023; y=-.17+x*.24+side*.14
            length=rng.uniform(.023,.041)
            ellipsoid('Irregular old tooth %d %d'%(side,k),(x,y-side*length*.35,-.184),
                (.009,length*.50,.007),'MW_bone',root)
    # Thin modeled maroon lip/ripped gum follows the long open mouth.
    for side in (-1,1):
        path=[]
        for i in range(19):
            a=math.pi*i/18
            x=.014+.112*math.cos(a);y=-.172+x*.24+side*.157*math.sin(a)
            path.append((x-.020,y,-.184-.012*math.sin(a)))
        tube('Torn desiccated gum '+str(side),path,[.006+.002*math.sin(i*.9) for i in range(19)],
            'MW_scar',parent=root,sides=8)
    for path in [ [(-.20,.39,-.190),(-.14,.32,-.225),(-.16,.28,-.223),(-.14,.23,-.24)],
                  [(.09,.44,-.20),(.055,.37,-.23),(.078,.31,-.228),(.06,.25,-.25)],
                  [(.24,-.05,-.19),(.19,-.135,-.19),(.17,-.23,-.17)] ]:
        tube('Deep branching fracture',path,[.0025]*len(path),'MW_scar',parent=root,sides=6)
    mesh('Lifted delaminated forehead fragment',[(-.18,.32,-.227),(-.11,.38,-.257),(-.055,.42,-.228),
        (-.03,.34,-.225),(-.09,.29,-.272)],[(0,1,2),(0,2,3),(0,3,4)],'MW_mask',root)
    for i in range(5):
        x=-.21+i*.017
        tube('Dangling cheek ligament '+str(i),[(x,-.08,-.21),(x-.009,-.235,-.19),
            (x+.018,-.37+i*.018,-.15)],[.004,.003,.0015],'MW_scar',parent=root,sides=6)
    return root

def finalize(key,root):
    bpy.context.view_layer.update(); objects=[o for o in bpy.context.scene.objects if o.type in {'MESH','EMPTY'}]
    root['asset_license']='CC0-1.0 original procedural geometry'
    source=HERE/(key+'.blend'); bpy.ops.wm.save_as_mainfile(filepath=str(source),compress=True)
    bpy.ops.file.make_paths_relative(); bpy.ops.wm.save_as_mainfile(filepath=str(source),compress=True)
    bpy.ops.object.select_all(action='DESELECT')
    for obj in objects: obj.select_set(True)
    bpy.context.view_layer.objects.active=root
    fbx=OUT/(key+'.fbx')
    bpy.ops.export_scene.fbx(filepath=str(fbx),use_selection=True,object_types={'MESH','EMPTY'},
        apply_unit_scale=True,global_scale=1,axis_forward='-Z',axis_up='Y',use_mesh_modifiers=True,
        mesh_smooth_type='FACE',use_tspace=True,add_leaf_bones=False,bake_anim=False,path_mode='RELATIVE')
    meshes=[o for o in objects if o.type=='MESH']; points=[unity(o.matrix_world@v.co) for o in meshes for v in o.data.vertices]
    lo=[min(p[i] for p in points) for i in range(3)]; hi=[max(p[i] for p in points) for i in range(3)]
    for obj in meshes: obj.data.calc_loop_triangles()
    tris=sum(len(o.data.loop_triangles) for o in meshes)
    if tris>60000: raise RuntimeError('Mesh budget exceeded')
    return dict(key=key,source=source.relative_to(PROJECT).as_posix(),fbx=fbx.relative_to(PROJECT).as_posix(),
        sourceSha256=digest(source),fbxSha256=digest(fbx),triangles=tris,vertices=sum(len(o.data.vertices) for o in meshes),
        boundsMin=lo,boundsMax=hi,meshParts=[o.name for o in meshes],
        pivotNames=[o.name for o in objects if o.type=='EMPTY'],
        materialSlots=sorted({m.name for o in meshes for m in o.data.materials}))

def preview(root,key,path):
    scene=bpy.context.scene; scene.render.engine='CYCLES';scene.cycles.samples=32;scene.cycles.use_denoising=True
    scene.render.resolution_x=1100;scene.render.resolution_y=1000;scene.render.resolution_percentage=100
    scene.world.color=(.016,.018,.015)
    target=Vector(coord((0,.78,0))) if key=='wraith-body' else Vector(coord((0,.01,0)))
    extent=2.1 if key=='wraith-body' else .94
    bpy.ops.object.camera_add(location=target+Vector((extent*.38,extent*1.65,extent*.22)))
    camera=bpy.context.object; camera.rotation_euler=(target-camera.location).to_track_quat('-Z','Y').to_euler()
    camera.data.type='ORTHO';camera.data.ortho_scale=extent*1.32;scene.camera=camera
    for offset,energy in [((-1,1,1.2),550),((1,0,.4),200),((0,-1,.8),220)]:
        bpy.ops.object.light_add(type='AREA',location=target+Vector(offset)*extent)
        light=bpy.context.object;light.data.energy=energy*extent*extent;light.data.size=extent*.75
        light.rotation_euler=(target-light.location).to_track_quat('-Z','Y').to_euler()
    path.mkdir(parents=True,exist_ok=True);scene.render.filepath=str(path/(key+'-studio.png'))
    bpy.ops.render.render(write_still=True)

def main():
    parser=argparse.ArgumentParser();parser.add_argument('--preview-dir',type=Path);parser.add_argument('--check-only',action='store_true')
    args=parser.parse_args(sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else [])
    bpy.context.preferences.filepaths.save_version=0; OUT.mkdir(parents=True,exist_ok=True)
    records=[]
    for key,build in [('wraith-body',body),('wraith-mask',mask)]:
        bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
        bpy.context.scene.unit_settings.system='METRIC';bpy.context.scene.unit_settings.scale_length=1
        root=build()
        if args.check_only:
            meshes=[o for o in bpy.context.scene.objects if o.type=='MESH']
            for obj in meshes:obj.data.calc_loop_triangles()
            triangles=sum(len(o.data.loop_triangles) for o in meshes)
            assert triangles<=60000
            assert all(o.data.uv_layers for o in meshes)
            print('MASK_ANATOMICAL_CONSTRUCTION_PASS',key,triangles,flush=True)
            continue
        records.append(finalize(key,root))
        print('MASK_HORROR_AUTHORED',key,records[-1]['triangles'],flush=True)
        if args.preview_dir:preview(root,key,args.preview_dir)
    if args.check_only:return
    (HERE/'model-manifest.json').write_text(json.dumps(dict(schema=1,blender=bpy.app.version_string,
        license='CC0-1.0 original geometry',axes='source-plan metres Y-up front -Z; Unity mirrors source-plan X',
        assets=records,materialContract={k:dict(surface=v[0],tint=v[1],metallic=v[2],smoothness=v[3]) for k,v in PALETTE.items()},
        runtime='Visual only; original rig, mask attachment, body growth, nav capsule and authored physics remain owners.'),indent=2)+'\n',encoding='utf-8')

if __name__=='__main__':main()
