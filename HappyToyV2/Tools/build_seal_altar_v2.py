"""Original bevelled joinery asset. Export to review staging, never replace source art."""
import bpy, math, json
from pathlib import Path
from mathutils import Vector

out=Path(__file__).resolve().parent/'altar-art'
out.mkdir(parents=True,exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.context.scene.unit_settings.system='METRIC'
materials=[]
for name,color,metal,rough in [('Aged joined oak',(.18,.125,.075),0,.86),
    ('Recess shadow oak',(.055,.035,.02),0,.92),('Tarnished iron pegs',(.12,.10,.075),.65,.62),
    ('Frayed seal binding',(.24,.065,.035),0,.9)]:
    m=bpy.data.materials.new(name);m.use_nodes=True;m.diffuse_color=(*color,1)
    node=m.node_tree.nodes.get('Principled BSDF');node.inputs['Base Color'].default_value=(*color,1)
    node.inputs['Metallic'].default_value=metal;node.inputs['Roughness'].default_value=rough
    materials.append(m)

def finish(obj,mat,bevel=.004):
    obj.data.materials.append(materials[mat])
    if bevel:
        mod=obj.modifiers.new('Worn eased joinery edges','BEVEL');mod.width=bevel;mod.segments=3
        bpy.context.view_layer.objects.active=obj;bpy.ops.object.modifier_apply(modifier=mod.name)
    for poly in obj.data.polygons: poly.use_smooth=True
    obj.data.use_auto_smooth=True
    obj.data.auto_smooth_angle=math.radians(60)
    normal=obj.modifiers.new('Weighted hard-surface normals','WEIGHTED_NORMAL');normal.keep_sharp=True;normal.weight=35
    bpy.ops.object.modifier_apply(modifier=normal.name)
    # Physical planar UVs instead of stretch-to-fit islands. Smart unwrapping reserves bevel islands.
    bpy.ops.object.select_all(action='DESELECT');obj.select_set(True);bpy.context.view_layer.objects.active=obj
    bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.uv.smart_project(angle_limit=math.radians(65),island_margin=.015)
    bpy.ops.object.mode_set(mode='OBJECT')
    return obj

def box(name,p,s,mat=0,bevel=.004):
    bpy.ops.mesh.primitive_cube_add(size=1,location=p)
    obj=bpy.context.object;obj.name=name;obj.scale=s
    bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    return finish(obj,mat,bevel)

# Dimensions stay inside the preserved .85 x .72 x .65 m collider.
for z,height,width,depth in [(.027,.036,.81,.61),(.071,.045,.77,.57),(.64,.042,.79,.59),(.685,.039,.838,.638)]:
    box('Stepped solid frame', (0,0,z),(width,depth,height))
for x in [-.355,.355]:
    for y in [-.255,.255]:
        box('Mortised vertical stile',(x,y,.347),(.070,.068,.53),0,.009)
        box('Worn foot socket',(x,y,.10),(.088,.084,.085),1,.006)
for side in [-1,1]:
    box('Inset recessed front panel',(0,side*.254,.36),(.625,.020,.47),1,.005)
    for x in [-.294,.294]: box('Panel edge rail',(x,side*.268,.36),(.027,.024,.47),0,.003)
    for z in [.145,.565]: box('Panel cross rail',(0,side*.268,z),(.610,.024,.027),0,.003)
    for x in [-.17,0,.17]: box('Narrow grain board',(x,side*.270,.36),(.16,.017,.365),0,.006)
    box('Crossgrain apron',(0,side*.29,.605),(.70,.035,.054),0,.005)
    box('Binding recessed band',(0,side*.286,.535),(.68,.006,.034),3,.002)
    for x in [-.332,.332]:
        for z in [.175,.547]:
            bpy.ops.mesh.primitive_uv_sphere_add(segments=12,ring_count=6,radius=1,location=(x,side*.290,z))
            peg=bpy.context.object;peg.name='Hand-forged flush peg';peg.scale=(.012,.004,.012)
            bpy.ops.object.transform_apply(location=False,rotation=False,scale=True);finish(peg,2,0)
for side in [-1,1]:
    box('Recessed end panel',(side*.354,0,.36),(.02,.45,.46),1,.004)
    for y in [-.192,0,.192]: box('End vertical board',(side*.366,y,.36),(.018,.165,.365),0,.005)
# Staggered worn plank tabletop and inset endgrain lip.
for i in range(4):
    box('Top oak plank '+str(i),(0,(i-1.5)*.15,.711),(.826,.148,.011),0,.002)

bpy.ops.object.select_all(action='SELECT')
bpy.context.view_layer.objects.active=next(o for o in bpy.context.scene.objects if o.type=='MESH')
bpy.ops.object.join();obj=bpy.context.object;obj.name='Original seal altar v2'
bpy.ops.object.transform_apply(location=False,rotation=True,scale=True)
bpy.context.scene.cursor.location=(0,0,0);bpy.ops.object.origin_set(type='ORIGIN_CURSOR')
obj.data.update()
bounds=[obj.matrix_world@Vector(p) for p in obj.bound_box]
size=[max(p[i] for p in bounds)-min(p[i] for p in bounds) for i in range(3)]
assert size[0]<.85 and size[1]<.65 and size[2]<.72,size
report={'original_asset':True,'preserved_originals':True,'parts_joined':True,'vertices':len(obj.data.vertices),
    'triangles':sum(len(p.vertices)-2 for p in obj.data.polygons),'uv_layers':len(obj.data.uv_layers),
    'size_blender_xyz':size,'normal_modifier_applied':True,'bevel_segments':3,'materials':[m.name for m in obj.data.materials],
    'scope':'Authoring/export proof. Unity import, runtime scale/collision and camera acceptance still required.'}
bpy.ops.wm.save_as_mainfile(filepath=str(out/'seal-altar-v2.blend'))
bpy.ops.export_scene.fbx(filepath=str(out/'seal-altar-v2.fbx'),use_selection=True,
    object_types={'MESH'},axis_forward='-Z',axis_up='Y',add_leaf_bones=False,bake_anim=False,
    apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',mesh_smooth_type='FACE')
(out/'source-audit.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
print('SEAL_ALTAR_V2_READY',report)
