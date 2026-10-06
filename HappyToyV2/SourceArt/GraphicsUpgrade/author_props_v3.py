"""Original modeled prop candidates. Run only with Blender 4.0.x --background.
All output stays in this staging directory; no project assets are overwritten.
Coordinates in constructors are Unity metres (X right, Y up, front -Z).
"""
import bpy, bmesh, math, random, json, hashlib, os, sys
from pathlib import Path
from mathutils import Vector

HERE=Path(__file__).resolve().parent
OUT=HERE/'proposed'
MODELS=OUT/'Assets/Resources/GraphicsUpgrade/Props'
SOURCE=OUT/'SourceArt/GraphicsUpgrade/Props'
PREVIEWS=HERE/'previews'
for directory in (MODELS,SOURCE,PREVIEWS): directory.mkdir(parents=True,exist_ok=True)
random.seed(36102)
bpy.context.preferences.filepaths.save_version=0

def coord(v): return (v[0],-v[2],v[1])
def unity(v): return (v[0],v[2],-v[1])
PALETTE={
 'GU_aged_iron':((.063,.071,.066,1),.72,.18,'metal-rust'),
 'GU_tarnished_brass':((.49,.31,.10,1),.86,.30,'brass-tarnished'),
 'GU_tallow':((.78,.66,.45,1),0,.45,'wax-tallow'),
 'GU_wax_pool':((.60,.42,.20,1),0,.64,'wax-pool'),
 'GU_charred_wick':((.022,.018,.014,1),0,.08,'cloth-charred'),
 'GU_wick_ash':((.19,.17,.14,1),0,.04,'cloth-charred'),
 'GU_battery_paper':((.32,.24,.10,1),0,.12,'paper-aged'),
 'GU_battery_metal':((.38,.40,.38,1),.9,.45,'brass-tarnished'),
 'GU_washi':((.63,.49,.31,1),0,.13,'paper-aged'),
 'GU_dark_timber':((.12,.065,.027,1),0,.25,'wood-aged'),
 'GU_scarred_enamel':((.10,.13,.105,1),.12,.32,'painted-metal')
}

def image_material(name):
    color,metal,smooth,key=PALETTE[name]
    if bpy.data.materials.get(name):return bpy.data.materials[name]
    mat=bpy.data.materials.new(name)
    mat.diffuse_color=color; mat.use_nodes=True
    nodes=mat.node_tree.nodes; bsdf=nodes.get('Principled BSDF')
    bsdf.inputs['Base Color'].default_value=color
    bsdf.inputs['Metallic'].default_value=metal
    bsdf.inputs['Roughness'].default_value=1-smooth
    if name=='GU_tallow':
        bsdf.inputs['Subsurface Weight'].default_value=.055
        bsdf.inputs['Subsurface Radius'].default_value=(.003,.0014,.00065)
    path=OUT/'Assets/Resources/GraphicsPbr'/key
    if (path/'albedo.png').exists() and name!='GU_wick_ash':
        for kind in ('albedo','normal','metallic-smoothness'):
            tex=nodes.new('ShaderNodeTexImage');tex.label=kind
            tex.image=bpy.data.images.load(str(path/(kind+'.png')),check_existing=True)
            if kind!='albedo':tex.image.colorspace_settings.name='Non-Color'
            if kind=='albedo':mat.node_tree.links.new(tex.outputs['Color'],bsdf.inputs['Base Color'])
            elif kind=='normal':
                n=nodes.new('ShaderNodeNormalMap');mat.node_tree.links.new(tex.outputs['Color'],n.inputs['Color'])
                mat.node_tree.links.new(n.outputs['Normal'],bsdf.inputs['Normal'])
            else:
                separate=nodes.new('ShaderNodeSeparateColor');mat.node_tree.links.new(tex.outputs['Color'],separate.inputs[0])
                mat.node_tree.links.new(separate.outputs['Red'],bsdf.inputs['Metallic'])
                invert=nodes.new('ShaderNodeMath');invert.operation='SUBTRACT';invert.inputs[0].default_value=1
                mat.node_tree.links.new(tex.outputs['Alpha'],invert.inputs[1]);mat.node_tree.links.new(invert.outputs[0],bsdf.inputs['Roughness'])
    else:
        # Preview-only physical variation for architecture maps supplied by root.
        noise=nodes.new('ShaderNodeTexNoise');noise.inputs['Scale'].default_value=46
        noise.inputs['Detail'].default_value=3.5
        ramp=nodes.new('ShaderNodeValToRGB')
        ramp.color_ramp.elements[0].color=tuple(c*.47 for c in color[:3])+(1,)
        ramp.color_ramp.elements[1].color=tuple(min(1,c*1.5) for c in color[:3])+(1,)
        mat.node_tree.links.new(noise.outputs['Fac'],ramp.inputs[0]);mat.node_tree.links.new(ramp.outputs['Color'],bsdf.inputs['Base Color'])
        bump=nodes.new('ShaderNodeBump');bump.inputs['Strength'].default_value=.19;bump.inputs['Distance'].default_value=.001
        mat.node_tree.links.new(noise.outputs['Fac'],bump.inputs['Height']);mat.node_tree.links.new(bump.outputs[0],bsdf.inputs['Normal'])
    return mat

def select(objects):
    bpy.ops.object.select_all(action='DESELECT')
    for obj in objects:obj.select_set(True)
    bpy.context.view_layer.objects.active=objects[0]

def mesh(name,verts,faces,slot,uv=None,smooth=True):
    data=bpy.data.meshes.new(name);data.from_pydata([coord(p) for p in verts],[],faces);data.update()
    obj=bpy.data.objects.new(name,data);bpy.context.collection.objects.link(obj)
    data.materials.append(image_material(slot))
    if uv:
        layer=data.uv_layers.new(name='UVMap')
        for face,poly in zip(uv,data.polygons):
            for loop,point in zip(poly.loop_indices,face):layer.data[loop].uv=point
    for face in data.polygons:face.use_smooth=smooth
    return obj

def lathe(name,profile,slot,segments=48,offset=(0,0,0),irregular=None):
    verts=[];faces=[];uv=[]
    for k,(radius,height) in enumerate(profile):
        for j in range(segments+1):
            angle=2*math.pi*j/segments
            r,y=(irregular(radius,height,angle,k) if irregular else (radius,height))
            verts.append((offset[0]+r*math.cos(angle),offset[1]+y,offset[2]+r*math.sin(angle)))
    for k in range(len(profile)-1):
        for j in range(segments):
            a=k*(segments+1)+j;b=a+segments+1
            faces.append((a,b,b+1,a+1))
            uv.append(((j/segments,k/(len(profile)-1)),(j/segments,(k+1)/(len(profile)-1)),((j+1)/segments,(k+1)/(len(profile)-1)),((j+1)/segments,k/(len(profile)-1))))
    return mesh(name,verts,faces,slot,uv)

def box(name,at,size,slot,bevel=.003):
    bpy.ops.mesh.primitive_cube_add(size=1,location=coord(at));obj=bpy.context.object;obj.name=name
    obj.dimensions=(size[0],size[2],size[1]);bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    obj.data.materials.append(image_material(slot))
    obj.data.use_auto_smooth=True;obj.data.auto_smooth_angle=math.radians(60)
    for p in obj.data.polygons:p.use_smooth=True
    if bevel:
        m=obj.modifiers.new('Measured rounded edge','BEVEL');m.width=min(bevel,min(size)*.22);m.segments=3
        m.affect='EDGES';bpy.ops.object.modifier_apply(modifier=m.name)
        m=obj.modifiers.new('Face weighted bevel normals','WEIGHTED_NORMAL');m.keep_sharp=True
        bpy.ops.object.modifier_apply(modifier=m.name)
    return obj

def tube(name,path,radii,slot,sides=8):
    verts=[];faces=[]
    for i,p in enumerate(path):
        p=Vector(p);a=Vector(path[max(0,i-1)]);b=Vector(path[min(len(path)-1,i+1)])
        tangent=(b-a).normalized();reference=Vector((0,1,0))
        if abs(tangent.dot(reference))>.9:reference=Vector((1,0,0))
        u=tangent.cross(reference).normalized();v=tangent.cross(u).normalized()
        radius=radii[i] if isinstance(radii,list) else radii
        if not isinstance(radius,tuple):radius=(radius,radius)
        for j in range(sides):
            q=p+u*math.cos(2*math.pi*j/sides)*radius[0]+v*math.sin(2*math.pi*j/sides)*radius[1]
            verts.append(tuple(q))
    for i in range(len(path)-1):
        for j in range(sides):
            a=i*sides+j;b=i*sides+(j+1)%sides;c=b+sides;d=a+sides
            faces.append((a,b,c,d))
    faces.append(tuple(reversed(range(sides))));faces.append(tuple((len(path)-1)*sides+j for j in range(sides)))
    return mesh(name,verts,faces,slot)

def ring(name,at,radius,thickness,slot,segments=40,vertical=False):
    path=[]
    for i in range(segments+1):
        t=i*2*math.pi/segments
        path.append((at[0]+radius*math.cos(t),at[1]+(radius*math.sin(t) if vertical else 0),at[2]+(0 if vertical else radius*math.sin(t))))
    return tube(name,path,thickness,slot,6)

def screw(name,at,slot='GU_aged_iron',radius=.0038):
    x,y,z=at
    # Real chamfered head and incised-looking dark slot. Hardware lies in XY, front -Z.
    obj=lathe(name,[(0,0),(radius*.77,0),(radius,.001),(radius,.003),(radius*.77,.004),(0,.004)],slot,16)
    obj.rotation_euler[0]=math.pi/2;obj.location=coord(at)
    return [obj,box(name+' slot',(x,y,z-.0041),(radius*1.25,.00075,.0003),'GU_charred_wick',.0001)]

def forged_stand(base_y=-1.06):
    out=[]
    out.append(lathe('Fluted forged foot',[(0,base_y),(.087,base_y),(.103,base_y+.005),(.104,base_y+.018),(.093,base_y+.031),(.043,base_y+.048),(.017,base_y+.054),(.013,base_y+.071)],'GU_aged_iron',48,
        irregular=lambda r,y,a,k:(r*(1+.012*math.sin(9*a)),y)))
    out.append(lathe('Hand forged taper and collar',[(.013,base_y+.061),(.014,base_y+.089),(.012,base_y+.105),(.009,base_y+.135),(.0085,-.17),(.013,-.153),(.014,-.144),(.011,-.133),(.009,-.062),(.023,-.054)],'GU_aged_iron',32,
        irregular=lambda r,y,a,k:(r*(1+.035*math.sin(5*a+y*11)),y)))
    out.append(lathe('Spun brass drip catch tray',[(0,-.064),(.022,-.064),(.028,-.050),(.086,-.047),(.109,-.030),(.112,-.019),(.108,-.012),(.103,-.014),(.102,-.026),(.083,-.038),(.025,-.040),(0,-.040)],'GU_tarnished_brass',48))
    out.append(ring('Rolled tray rim',(0,-.014,0),.107,.0023,'GU_tarnished_brass',48))
    return out

def candle():
    out=forged_stand()
    rim_samples=[.0035*math.sin(2*2*math.pi*j/64+.8)+.002*math.cos(3*2*math.pi*j/64) for j in range(64)]
    rim_scale=.008358248/max(rim_samples)
    def rim_wave(a):return (.0035*math.sin(2*a+.8)+.002*math.cos(3*a))*rim_scale
    def uneven(r,y,a,k):
        side=.00065*math.sin(5*a+y*13)+.00035*math.sin(9*a+y*21)
        lip=rim_wave(a) if y>.16 else 0
        # Burnt top slopes into a true recessed well; not a cylinder cap.
        strength=1 if k in (8,9,10) else .5 if k in (7,11) else .18 if k==12 else 0
        return max(.0005,r+side*(1 if r>.025 else .1)),y+lip*strength
    profile=[(.001,-.037),(.047,-.037),(.058,-.031),(.059,-.022),(.057,.002),(.055,.092),(.052,.158),(.052,.186),(.0535,.201),(.052,.208),(.0485,.210),(.044,.206),(.038,.197),(.030,.191),(.008,.189),(.001,.190)]
    out.append(lathe('Irregular tallow body with recessed melt well',profile,'GU_tallow',64,irregular=uneven))
    out.append(lathe('Recessed glossy liquid wax pool',[(.001,.191),(.008,.191),(.024,.1907),(.029,.1912),(.030,.1905)],'GU_wax_pool',48))
    # Flowing drips follow the skin, overlap the body, and end in unequal rounded bulbs.
    for d,length in enumerate((.101,.067,.139,.080,.121,.045,.093)):
        angle=d*2*math.pi/7+.18*math.sin(d*3.1);path=[];radii=[]
        for step in range(15):
            t=step/14
            rim=.207+rim_wave(angle)
            y=rim-length*t
            a=angle+.047*math.sin(t*4+d)
            radius=.052+.003*(1-t)+.0016*math.sin(t*8+d)
            if step==0:radius=.047
            elif step==1:radius=.052
            path.append((radius*math.cos(a),y,radius*math.sin(a)))
            width=.0056*(.62+.36*math.sin(math.pi*t))
            if step>=11:width*=1+.5*math.sin((t-.785)*math.pi/.215)
            if step==14:width=.0018
            radii.append((width,.0037 if step<13 else .003))
        out.append(tube('Solidified wax drip %02d'%d,path,radii,'GU_tallow',8))
    out.append(lathe('Thin pooled wax with curled edge',[(.001,-.037),(.063,-.037),(.085,-.032),(.091,-.029),(.089,-.025),(.067,-.027),(.056,-.022)],'GU_tallow',56,
        irregular=lambda r,y,a,k:(r*(1+.09*math.sin(3*a+.8)+.04*math.sin(9*a)),y+.0012*math.sin(5*a))))
    for strand in range(3):
        path=[]
        for i in range(17):
            t=i/16;a=t*math.pi*4+strand*math.pi*2/3
            path.append((.0011*math.cos(a)+t*t*.0024,.188+t*.030,.0011*math.sin(a)))
        out.append(tube('Charred braided wick %d'%strand,path,.00085,'GU_charred_wick',6))
    out.append(tube('Only exposed tip ash',[(.0021,.216,0),(.0024,.2175,-.0001),(.0026,.218,.0001)],[.0011,.0009,.0003],'GU_wick_ash',7))
    return out

def battery():
    out=forged_stand(-1.12)
    for side in (-1,1):
        x=side*.055
        body=[(.001,-.035),(.032,-.035),(.045,-.028),(.046,-.018),(.045,.008),(.044,.161),(.045,.181),(.041,.193),(.031,.199),(.001,.199)]
        out.append(lathe('Rolled zinc cell %d'%side,body,'GU_battery_paper',48,(x,0,0),
            lambda r,y,a,k:(r*(1+.005*math.sin(a*9+y*30)),y)))
        for y in (-.025,.187):
            out.append(lathe('Crimped end cap',[(.001,y),(.040,y),(.046,y+.001),(.046,y+.005),(.041,y+.008),(.032,y+.008),(.001,y+.008)],'GU_battery_metal',40,(x,0,0)))
        out.append(lathe('Positive contact nipple',[(.001,.196),(.018,.196),(.020,.2),(.019,.21),(.014,.214),(.001,.214)],'GU_tarnished_brass',32,(x,0,0)))
        # Rolled-paper seam, paired identification collars, and actual embossed positive sign.
        out.append(box('Wrapper folded overlap',(x,.077,-.0445),(.007,.175,.0017),'GU_battery_paper',.0005))
        out.append(lathe('Printed faded band',[(.0453,.018),(.0455,.039)],'GU_washi',48,(x,0,0)))
        out.append(box('Embossed positive horizontal',(x,.161,-.0457),(.016,.0026,.0014),'GU_tarnished_brass',.0004))
        out.append(box('Embossed positive vertical',(x,.161,-.0457),(.0026,.016,.0014),'GU_tarnished_brass',.0004))
    # Bent metal keeper holds the paired cells instead of floating props.
    out.append(tube('Paired-cell spring keeper',[(-.093,.074,-.026),(-.090,.081,-.047),(-.055,.081,-.054),(0,.08,-.044),(.055,.081,-.054),(.090,.081,-.047),(.093,.074,-.026)],.0028,'GU_aged_iron',8))
    return out

def lantern():
    out=[]
    profile=[(.001,-.243),(.065,-.243),(.122,-.207),(.165,-.140),(.177,-.04),(.173,.055),(.152,.142),(.108,.205),(.059,.222),(.001,.222)]
    out.append(lathe('Hand folded washi lantern skin',profile,'GU_washi',64,
        irregular=lambda r,y,a,k:(r*(1+.020*math.cos(12*a)+.008*math.sin(25*a+y*23)),y)))
    for k in range(12):
        a=k*math.pi/6;path=[]
        for r,y in profile[1:-1]:
            radius=r*(1+.020*math.cos(12*a)+.008*math.sin(25*a+y*23))+.0017
            path.append((radius*math.cos(a),y,radius*math.sin(a)))
        out.append(tube('Curved bamboo lantern rib %02d'%k,path,.0025,'GU_dark_timber',6))
    for r,y in profile[2:-2]:
        path=[]
        for j in range(65):
            a=j*math.pi*2/64
            radius=r*(1+.020*math.cos(12*a)+.008*math.sin(25*a+y*23))+.0017
            path.append((radius*math.cos(a),y,radius*math.sin(a)))
        out.append(tube('Paper seam supporting hoop',path,.0018,'GU_dark_timber',6))
    for y in (-.243,.224):
        out.append(lathe('Hammered lantern end rim',[(.001,y),(.064,y),(.067,y+.007),(.057,y+.016),(.001,y+.016)],'GU_aged_iron',40))
    out.append(ring('Forged hanging loop',(0,.269,0),.024,.0036,'GU_aged_iron',32,True))
    out.append(tube('Hanging stem',[(0,.226,0),(0,.257,0),(0,.26,0)],.004,'GU_aged_iron',8))
    return out

def altar():
    out=[]
    # Mortised rails, recessed panels, pegged frames and flared feet: actual joinery.
    out.append(box('Five plank altar top',(0,.671,0),(.81,.036,.60),'GU_dark_timber',.005))
    for i in range(4):out.append(box('Top board seam insert',((i-1.5)*.16,.690,0),(.002,.001,.57),'GU_charred_wick',.0001))
    out.append(box('Under-top bead molding',(0,.639,0),(.795,.025,.585),'GU_dark_timber',.005))
    for x in (-.335,.335):
        for z in (-.235,.235):
            out.append(box('Joined upright',(x,.324,z),(.065,.60,.065),'GU_dark_timber',.005))
            foot=box('Flared altar foot',(x,.043,z),(.088,.084,.087),'GU_dark_timber',.006)
            out.append(foot)
            out.append(box('Tenon cross peg',(x,.596,z-.035),(.016,.020,.008),'GU_dark_timber',.003))
    for z in (-.267,.267):
        out.append(box('Lower mortise rail',(0,.117,z),(.705,.072,.042),'GU_dark_timber',.004))
        out.append(box('Upper mortise rail',(0,.590,z),(.705,.058,.040),'GU_dark_timber',.004))
        out.append(box('Recessed long field panel',(0,.35,z),(.59,.387,.014),'GU_dark_timber',.003))
        for x in (-.294,.294):out.append(box('Raised panel bead',(x,.35,z-.012),(.013,.379,.012),'GU_dark_timber',.002))
    for x in (-.36,.36):
        out.append(box('Recessed end field panel',(x,.35,0),(.014,.388,.403),'GU_dark_timber',.003))
        for z in (-.196,.196):out.append(box('End panel edge bead',(x,.35,z),(.015,.38,.015),'GU_dark_timber',.003))
    for x in (-.325,.325):
        for y in (.13,.595):
            out.append(box('Forged angle strap',(x,y,-.286),(.063,.037,.004),'GU_aged_iron',.0014))
            out.extend(screw('Square strap nail',(x,y,-.289),radius=.0045))
    return out

def cabinet():
    out=[]
    out.append(box('Beveled storage cabinet back',(0,.95,.292),(.874,1.794,.032),'GU_scarred_enamel',.006))
    for x in (-.436,.436):out.append(box('Rolled steel side shell',(x,.94,0),(.027,1.82,.58),'GU_scarred_enamel',.006))
    for y in (.048,1.842):out.append(box('Lipped top and foot plinth',(0,y,0),(.89,.067,.601),'GU_scarred_enamel',.009))
    for x in (-.36,.36):
        for z in (-.228,.228):out.append(box('Cabinet folded foot',(x,.021,z),(.055,.042,.065),'GU_aged_iron',.005))
    for side in (-1,1):
        x=side*.216
        out.append(box('Inset door panel',(x,.944,-.302),(.421,1.711,.025),'GU_scarred_enamel',.006))
        for edge in (-1,1):out.append(box('Pressed raised door seam',(x+edge*.195,.944,-.320),(.012,1.65,.009),'GU_scarred_enamel',.002))
        for y in (.134,1.756):out.append(box('Pressed panel lower fold',(x,y,-.320),(.398,.015,.009),'GU_scarred_enamel',.002))
        for j in range(6):
            y=1.48-j*.042
            out.append(box('Dark actual vent recess',(x,y,-.318),(.195,.019,.005),'GU_charred_wick',.001))
            vent=box('Folded ventilation louver',(x,y+.007,-.324),(.206,.012,.012),'GU_scarred_enamel',.002)
            vent.rotation_euler[0]=math.radians(-18);out.append(vent)
        handle_x=side*.034
        out.append(box('Handle mounting plate',(handle_x,.978,-.328),(.046,.173,.006),'GU_aged_iron',.003))
        out.append(tube('Forged cabinet U pull',[(handle_x,1.037,-.332),(handle_x,1.031,-.364),(handle_x,.927,-.364),(handle_x,.920,-.332)],.006,'GU_tarnished_brass',8))
        out.extend(screw('Pull plate screw',(handle_x,1.044,-.333)))
        out.extend(screw('Pull plate screw',(handle_x,.910,-.333)))
        for y in (.394,1.438):
            out.append(box('Hinge mounting plate',(side*.414,y,-.325),(.048,.061,.005),'GU_aged_iron',.002))
            out.append(lathe('Rolled barrel hinge',[(.004,y-.032),(.008,y-.029),(.008,y+.029),(.004,y+.032)],'GU_aged_iron',20,(side*.433,0,-.325)))
        out.append(box('Old inventory paper label',(x,.536,-.323),(.150,.050,.0012),'GU_washi',.001))
    return out

def hardware():
    out=[]
    out.append(box('Forged recessed sliding-door plate',(0,0,0),(.061,.211,.008),'GU_aged_iron',.004))
    out.append(box('Inset dark finger well',(0,0,-.0048),(.034,.119,.001),'GU_charred_wick',.004))
    out.append(tube('Curved forged finger pull',[(-.012,.05,-.007),(-.013,.04,-.021),(-.013,-.04,-.021),(-.009,-.056,-.006)],.0042,'GU_tarnished_brass',10))
    out.append(tube('Opposite folded grip edge',[(.012,.05,-.007),(.012,.04,-.014),(.012,-.04,-.014),(.010,-.054,-.006)],.0026,'GU_aged_iron',8))
    for y in (-.083,.083):out.extend(screw('Slotted door plate screw',(0,y,-.0045),radius=.0045))
    return out

def cabinet_timber():
    out=cabinet()
    for obj in out:
        for i,material in enumerate(obj.data.materials):
            if material.name=='GU_scarred_enamel':obj.data.materials[i]=image_material('GU_dark_timber')
    for side in (-1,1):
        x=side*.216
        # The structural silhouette and original handles stay exact; shallow mortised frames
        # create an aged cupboard appropriate to the timber corridor rather than a school locker.
        for edge in (-1,1):out.append(box('Timber door stile',(x+edge*.169,.888,-.328),(.029,1.39,.017),'GU_dark_timber',.003))
        for y in (.212,.788,1.54):out.append(box('Timber door mortise rail',(x,y,-.328),(.347,.033,.017),'GU_dark_timber',.003))
    return out

def flame():
    # Three curved narrow ribbons. Runtime transparent photographic texture supplies feathered edge.
    out=[]
    for angle in (0,math.pi/3,math.pi*2/3):
        verts=[];faces=[];uv=[]
        for k in range(9):
            t=k/8;center=.0033*math.sin(t*math.pi*.85)*t;width=.019*math.sin(math.pi*(t*.94+.03))*(1-.50*t)
            for side in (-1,1):verts.append(((center+side*width)*math.cos(angle),t*.064,(center+side*width)*math.sin(angle)))
        for k in range(8):
            faces.append((2*k,2*k+1,2*k+3,2*k+2));uv.append(((0,k/8),(1,k/8),(1,(k+1)/8),(0,(k+1)/8)))
        out.append(mesh('Asymmetric flame ribbon',verts,faces,'GU_tallow',uv,False))
    return out

def finalize(key,objects):
    select(objects);bpy.ops.object.join();obj=bpy.context.object;obj.name=key
    bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
    # Consistent winding, clean packed non-overlapping UV islands, angle-preserving normals.
    bm=bmesh.new();bm.from_mesh(obj.data)
    bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=.0000001)
    bmesh.ops.dissolve_degenerate(bm,edges=list(bm.edges),dist=.00000001)
    bmesh.ops.recalc_face_normals(bm,faces=bm.faces)
    bmesh.ops.triangulate(bm,faces=[f for f in bm.faces if len(f.verts)>4])
    bm.to_mesh(obj.data);bm.free()
    if key!='candle-flame':
        bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT')
        bpy.ops.uv.smart_project(angle_limit=math.radians(68),island_margin=.012,area_weight=.8,correct_aspect=True,scale_to_bounds=True)
        bpy.ops.object.mode_set(mode='OBJECT')
    obj.data.update()
    obj.data.use_auto_smooth=True;obj.data.auto_smooth_angle=math.radians(60)
    if key!='candle-flame':
        modifier=obj.modifiers.new('Final area weighted edge normals','WEIGHTED_NORMAL');modifier.keep_sharp=True
        bpy.ops.object.modifier_apply(modifier=modifier.name)
    obj['original_authored_asset']=True;obj['coordinate_contract']='Unity metres; Y up; forward -Z; no colliders'
    bpy.ops.outliner.orphans_purge(do_local_ids=True,do_linked_ids=False,do_recursive=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/(key+'.blend')),compress=True)
    bpy.ops.file.pack_all()
    bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/(key+'.blend')),compress=True)
    select([obj])
    bpy.ops.export_scene.fbx(filepath=str(MODELS/(key+'.fbx')),use_selection=True,object_types={'MESH'},
        apply_unit_scale=True,global_scale=1,axis_forward='-Z',axis_up='Y',use_mesh_modifiers=True,
        mesh_smooth_type='FACE',use_tspace=True,add_leaf_bones=False,bake_anim=False,path_mode='AUTO')
    coords=[Vector(unity(obj.matrix_world@v.co)) for v in obj.data.vertices]
    minimum=[min(v[i] for v in coords) for i in range(3)];maximum=[max(v[i] for v in coords) for i in range(3)]
    obj.data.calc_loop_triangles()
    result={'key':key,'vertices':len(obj.data.vertices),'triangles':len(obj.data.loop_triangles),
        'boundsMin':minimum,'boundsMax':maximum,'dimensions':[maximum[i]-minimum[i] for i in range(3)],
        'materials':[m.name for m in obj.data.materials],'uvLayers':len(obj.data.uv_layers),'objects':1,'colliders':0}
    return obj,result

def preview(obj,key):
    # Studio evidence only; actual Unity input/camera checks remain for root integration.
    scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.samples=64
    scene.cycles.use_denoising=True;scene.render.resolution_x=1000;scene.render.resolution_y=1000;scene.render.resolution_percentage=100
    scene.render.image_settings.file_format='PNG';scene.world.color=(.045,.045,.045)
    scene.view_settings.view_transform='AgX'
    coords=[obj.matrix_world@v.co for v in obj.data.vertices];lo=Vector(tuple(min(v[i] for v in coords) for i in range(3)));hi=Vector(tuple(max(v[i] for v in coords) for i in range(3)))
    center=(lo+hi)*.5;extent=max(hi-lo)
    if key=='candle-waymark':
        center=Vector(coord((0,.03,0)));extent=.40
    bpy.ops.object.camera_add(location=center+Vector((extent*1.8,extent*2.5,extent*1.2)))
    cam=bpy.context.object;cam.name='QA studio camera';cam.rotation_euler=(center-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.type='ORTHO';cam.data.ortho_scale=extent*1.26;scene.camera=cam
    for location,power,size in (((-2,-2,3),600,3),((2,-.5,1.2),320,2),((.2,2,2),700,2)):
        bpy.ops.object.light_add(type='AREA',location=center+Vector(location)*max(.35,extent))
        light=bpy.context.object;light.data.energy=power*max(.12,extent*extent);light.data.shape='DISK';light.data.size=size*max(.35,extent)
        light.rotation_euler=(center-light.location).to_track_quat('-Z','Y').to_euler()
    scene.render.filepath=str(PREVIEWS/(key+'-studio.png'));bpy.ops.render.render(write_still=True)
    if key=='candle-waymark':
        center=(lo+hi)*.5;extent=max(hi-lo)
        cam.location=center+Vector((extent*1.8,extent*2.5,extent*1.2));cam.rotation_euler=(center-cam.location).to_track_quat('-Z','Y').to_euler()
        cam.data.ortho_scale=extent*1.25
        scene.render.filepath=str(PREVIEWS/(key+'-full-stand.png'));bpy.ops.render.render(write_still=True)
    # Remove QA-only cameras/lights, source .blend contains exactly modeled static prop.
    for child in list(bpy.context.scene.objects):
        if child.type in {'CAMERA','LIGHT'}:bpy.data.objects.remove(child,do_unlink=True)

def main():
    builders={'candle-waymark':candle,'battery-supply':battery,'paper-lantern':lantern,'seal-altar':altar,'cabinet-shell':cabinet,'cabinet-timber':cabinet_timber,'door-hardware':hardware,'candle-flame':flame}
    requested=sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else list(builders)
    manifest=[]
    for key in requested:
        bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
        obj,record=finalize(key,builders[key]());manifest.append(record)
        print('PROP_MODEL_READY '+json.dumps(record),flush=True)
        if key!='candle-flame':preview(obj,key)
    report=HERE/'model-manifest.json'
    previous=json.loads(report.read_text()) if report.exists() else {'assets':[]}
    previous['assets']=[a for a in previous['assets'] if a['key'] not in requested]+manifest
    previous.update({'blender':bpy.app.version_string,'authoring':'Original procedural measured mesh modeling; no external meshes','units':'metres','sourceSceneModified':False,'sourceAssetsModified':False})
    report.write_text(json.dumps(previous,indent=2),encoding='utf-8')

if __name__=='__main__':main()
