"""Blender 4.0.2: original measured corridor joinery, parquet and boarded ceilings.

Run blender --background --python SourceArt/GraphicsUpgrade/author_corridor_architecture.py.
Constructors use Unity metres (X right, Y up, front +Z). Exported FBX is render-only;
Unity keeps its original physics/navigation and combines modules per cell/material.
Editable bevel and weighted-normal modifiers survive in the .blend source. Export
applies them and generates measured UVs; no workstation paths are needed to rebuild.
"""
import bpy
import hashlib
import json
import math
import random
from pathlib import Path
from mathutils import Vector

PROJECT = Path(__file__).resolve().parents[2]
SOURCE = PROJECT / 'SourceArt/GraphicsUpgrade/Architecture'
DEST = PROJECT / 'Assets/Resources/CorridorArchitecture'
SOURCE.mkdir(parents=True, exist_ok=True)
DEST.mkdir(parents=True, exist_ok=True)
bpy.context.preferences.filepaths.save_version = 0
SPANS = {'CA_timber': .55, 'CA_paper': .6, 'CA_floor': .55, 'CA_iron': 1., 'CA_plaster': 1.8}
COLORS = {'CA_timber': (.12,.074,.036,1), 'CA_paper': (.61,.55,.41,1),
          'CA_floor': (.23,.17,.10,1), 'CA_iron': (.057,.061,.053,1), 'CA_plaster': (.48,.46,.39,1)}

def coord(v): return (v[0], -v[2], v[1])
def unity(v): return Vector((v[0], v[2], -v[1]))

def material(slot):
    if slot in bpy.data.materials: return bpy.data.materials[slot]
    m = bpy.data.materials.new(slot); m.diffuse_color = COLORS[slot]; m.use_nodes = True
    p = m.node_tree.nodes.get('Principled BSDF'); p.inputs['Base Color'].default_value = COLORS[slot]
    p.inputs['Roughness'].default_value = .74 if slot != 'CA_iron' else .42
    p.inputs['Metallic'].default_value = .8 if slot == 'CA_iron' else 0
    return m

def bevel_box(name, at, size, slot, bevel=.002, detailed=True):
    bpy.ops.mesh.primitive_cube_add(size=1, location=coord(at)); o = bpy.context.object; o.name = name
    o.dimensions = (size[0], size[2], size[1]); bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    o.data.materials.append(material(slot)); o.data.use_auto_smooth = True
    b = o.modifiers.new('Measured softened tool-cut edges', 'BEVEL'); b.width = bevel; b.segments = 2 if detailed else 1
    b.affect = 'EDGES'
    n = o.modifiers.new('Face-area weighted split normals', 'WEIGHTED_NORMAL'); n.keep_sharp = True
    for p in o.data.polygons: p.use_smooth = True
    return o

def cupped_board_geometry(size, detailed=True):
    # Five transverse samples include the centre, so the top is an actual
    # shallow curve rather than a translated cube cap. The bottom remains flat,
    # and end/side faces close the board. LOD1 retains the same 0.65 mm valley.
    columns = 5 if detailed else 3
    half_width, half_height, half_length = (value * .5 for value in size)
    vertices = []
    for layer in range(2):
        for end in (-1, 1):
            for ix in range(columns):
                fraction = ix / (columns - 1) * 2 - 1
                y = -half_height if layer == 0 else half_height - .00065 * (1 - fraction * fraction)
                vertices.append(coord((half_width * fraction, y, half_length * end)))
    def index(layer, end, ix): return (layer * 2 + end) * columns + ix
    faces = []
    for ix in range(columns - 1):
        faces.append((index(1, 0, ix), index(1, 1, ix), index(1, 1, ix + 1), index(1, 0, ix + 1)))
        faces.append((index(0, 0, ix), index(0, 0, ix + 1), index(0, 1, ix + 1), index(0, 1, ix)))
        faces.append((index(0, 0, ix), index(1, 0, ix), index(1, 0, ix + 1), index(0, 0, ix + 1)))
        faces.append((index(0, 1, ix), index(0, 1, ix + 1), index(1, 1, ix + 1), index(1, 1, ix)))
    faces.append((index(0, 0, 0), index(0, 1, 0), index(1, 1, 0), index(1, 0, 0)))
    faces.append((index(0, 0, columns - 1), index(1, 0, columns - 1), index(1, 1, columns - 1), index(0, 1, columns - 1)))
    return vertices, faces

def cupped_board(name, at, size, detailed=True):
    vertices, faces = cupped_board_geometry(size, detailed)
    data = bpy.data.meshes.new(name); data.from_pydata(vertices, [], faces); data.update()
    o = bpy.data.objects.new(name, data); bpy.context.collection.objects.link(o); o.location = coord(at)
    data.materials.append(material('CA_floor')); data.use_auto_smooth = True
    b = o.modifiers.new('Measured softened board perimeter', 'BEVEL')
    b.width = .0005; b.segments = 2 if detailed else 1; b.affect = 'EDGES'; b.limit_method = 'ANGLE'
    n = o.modifiers.new('Face-area weighted split normals', 'WEIGHTED_NORMAL'); n.keep_sharp = True
    for face in data.polygons: face.use_smooth = True
    return o

def shaped_sheet(name, xmin, xmax, ymin, ymax, rng, detailed=True):
    nx,ny = (6,10) if detailed else (2,3); vertices=[]; faces=[]
    for iy in range(ny+1):
        for ix in range(nx+1):
            x=xmin+(xmax-xmin)*ix/nx; y=ymin+(ymax-ymin)*iy/ny
            # Millimetre scale slack and curled edges, no silhouette intrusion into passages.
            z=.013 + .0018*math.sin(ix*1.8+iy*.61) + rng.uniform(-.0008,.0008)
            if iy==0: y += rng.uniform(0,.008)
            vertices.append(coord((x,y,z)))
    for iy in range(ny):
        for ix in range(nx):
            a=iy*(nx+1)+ix; faces.append((a,a+1,a+nx+2,a+nx+1))
    mesh=bpy.data.meshes.new(name); mesh.from_pydata(vertices,[],faces); mesh.update()
    o=bpy.data.objects.new(name,mesh); bpy.context.collection.objects.link(o); mesh.materials.append(material('CA_paper'))
    s=o.modifiers.new('Physical paper thickness', 'SOLIDIFY'); s.thickness=.0007
    for p in mesh.polygons: p.use_smooth=True
    return o

def nail(at, slot='CA_iron'):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=8, ring_count=4, radius=1, location=coord(at))
    o=bpy.context.object; o.name='Recessed hand-forged nail'; o.scale=(.0035,.0012,.0035)
    bpy.ops.object.transform_apply(location=False,rotation=False,scale=True); o.data.materials.append(material(slot))
    for p in o.data.polygons: p.use_smooth=True

def wall(rng,detailed):
    for i in range(8):
        x=(i-3.5)*.125
        bevel_box('Tongue-and-groove lower board', (x,.443,.014), (.122,.676,.018), 'CA_timber', .002,detailed)
        if detailed:
            for y in (.145,.72): nail((x,y,.025))
    for y,w,h in ((.08,1,.056),(.824,1,.052),(2.564,1,.072)):
        bevel_box('Profiled horizontal rail',(0,y,.014),(w,h,.036),'CA_timber',.004,detailed)
    for x in (-.482,.482):
        bevel_box('Profiled upright',(x,1.694,.019),(.036,1.7,.038),'CA_timber',.003,detailed)
    for bay in range(2):
        xmin=-.464+bay*.464; xmax=xmin+.457
        shaped_sheet('Slack fibre infill',xmin,xmax,.854,2.526,rng,detailed)
        if bay:
            bevel_box('Set-back centre muntin',(xmin,1.691,.032),(.022,1.672,.019),'CA_timber',.002,detailed)
        for y in (1.394,2.10):
            bevel_box('Set-back shoji rail',((xmin+xmax)*.5,y,.031),(.458,.017,.017),'CA_timber',.0016,detailed)

def floor(rng,detailed):
    # One 1 x 3 m course. Individual boards have staggered butt joints, softened
    # edges and grain-aligned measured UVs; support surface stays at Y=0.
    # Continuous dark timber below the walking boards. Thin gaps expose real
    # support wood rather than the unlit void beneath the original hidden slab.
    bevel_box('Continuous timber subfloor',(0,-.027,0),(1,.004,3),'CA_timber',.0003,detailed)
    for col in range(6):
        x=(col-2.5)/6; cuts=[-1.5,-.53+(col%3)*.20,.62-(col%2)*.25,1.5]
        for j in range(3):
            length=cuts[j+1]-cuts[j]-.0008
            cupped_board('Staggered cupped floorboard',(x,-.011,(cuts[j+1]+cuts[j])*.5),
                         (1/6-.0008,.022,length),detailed)
            # Old boards have occasional exposed fasteners, not a regular field
            # of high-contrast dots across every walking surface.
            if detailed and rng.random()<.025:
                for z in (cuts[j]+.034,cuts[j+1]-.034):
                    # Floor nail uses the same head rotated to lie against the board.
                    nail((x+.045,-.0008,z)); bpy.context.object.rotation_euler.x=math.pi/2

def ceiling(rng,detailed):
    for i in range(4):
        bevel_box('Boarded ceiling panel',((i-1.5)*.25,0,0),(.2492,.018,3),'CA_timber',.0004,detailed)
    bevel_box('Ceiling batten',(0,-.027,0),(1,.045,.06),'CA_timber',.003,detailed)

def door_leaf(rng,detailed):
    for side in (-1,1):
        for i in range(12):
            bevel_box('Raised lower door board',((i-5.5)*.213,-.764,side*.044),
                        (.210,.784,.021),'CA_timber',.002,detailed)
        for x in (-1.252,1.252):
            bevel_box('Profiled door stile',(x,0,side*.041),(.090,2.344,.030),'CA_timber',.003,detailed)
        for y in (-1.134,-.342,1.13):
            bevel_box('Profiled door rail',(0,y,side*.041),(2.59,.070,.032),'CA_timber',.003,detailed)
        for bay in range(4):
            x=(bay-1.5)*.622
            sheet=shaped_sheet('Framed door paper',x-.292,x+.292,-.296,1.088,rng,detailed)
            sheet.location.y=-side*.033
            if side<0:sheet.rotation_euler.z=math.pi
            if bay<3:
                bevel_box('Set-back door muntin',(x+.31,.39,side*.043),(.026,1.434,.024),'CA_timber',.0015,detailed)
            for y in (.185,.735):
                bevel_box('Door lattice crossbar',(x,y,side*.046),(.605,.022,.020),'CA_timber',.0015,detailed)

def door_post(rng,detailed):
    bevel_box('Planed structural upright',(0,0,0),(.14,2.4,.14),'CA_timber',.005,detailed)
    # Decorative shoulders remain inside the solid original post.
    for side in (-1,1):
        for y in (-.99,.99):
            bevel_box('Flush decorative shoulder',(0,y,side*.065),(.088,.054,.010),'CA_timber',.0016,detailed)
    if detailed:
        for y in (-.98,.98):nail((0,y,.069))

def door_lintel(rng,detailed):
    bevel_box('Profiled header beam',(0,0,0),(2.8,.56,.24),'CA_timber',.009,detailed)
    for side in (-1,1):
        for y in (-.204,.204):
            bevel_box('Recessed lintel moulding',(0,y,side*.113),(2.77,.046,.014),'CA_timber',.003,detailed)
        for x in (-1.23,1.23):
            bevel_box('Decorative beam shoulder',(x,0,side*.113),(.072,.32,.014),'CA_timber',.002,detailed)

def plaster_wall(rng,detailed):
    nx,ny=(6,12) if detailed else (2,4);vertices=[];faces=[]
    for iy in range(ny+1):
        for ix in range(nx+1):
            x=ix/nx-.5;y=.08+iy/ny*3.32
            z=.013+math.sin(ix*1.46+iy*.73)*.0015+rng.uniform(-.0007,.0007)
            vertices.append(coord((x,y,z)))
    for iy in range(ny):
        for ix in range(nx):
            a=iy*(nx+1)+ix;faces.append((a,a+1,a+nx+2,a+nx+1))
    data=bpy.data.meshes.new('Undulating lime plaster');data.from_pydata(vertices,[],faces);data.update()
    o=bpy.data.objects.new('Undulating lime plaster',data);bpy.context.collection.objects.link(o)
    data.materials.append(material('CA_plaster'))
    for face in data.polygons:face.use_smooth=True
    for y in (.055,.745):
        bevel_box('Classroom wainscot rail',(0,y,.022),(1,.06,.035),'CA_timber',.003,detailed)
    for i in range(8):
        bevel_box('Classroom wainscot board',((i-3.5)*.125,.40,.024),(.122,.635,.020),'CA_timber',.002,detailed)

def metre_uv(obj):
    mesh=obj.data; layer=mesh.uv_layers.get('UVMap') or mesh.uv_layers.new(name='UVMap')
    span=SPANS[mesh.materials[0].name]
    for poly in mesh.polygons:
        n=unity(obj.matrix_world.to_3x3()@poly.normal); axis=max(range(3),key=lambda i:abs(n[i]))
        for li in poly.loop_indices:
            p=unity(obj.matrix_world@mesh.vertices[mesh.loops[li].vertex_index].co)
            layer.data[li].uv=((p.x,p.z) if axis==1 else (p.z,p.y) if axis==0 else (p.x,p.y))
            layer.data[li].uv/=span

def write(key,builder,seed,detailed):
    bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete(use_global=False)
    builder(random.Random(seed),detailed)
    objects=list(bpy.context.scene.objects)
    for o in objects: metre_uv(o)
    bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/(key+'.blend')))
    # Export split normals/UVs from evaluated modifiers, never primitive placeholders.
    for o in objects:
        bpy.context.view_layer.objects.active=o
        for modifier in list(o.modifiers): bpy.ops.object.modifier_apply(modifier=modifier.name)
        metre_uv(o)
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.export_scene.fbx(filepath=str(DEST/(key+'.fbx')),use_selection=True,
        object_types={'MESH'},axis_forward='-Z',axis_up='Y',apply_unit_scale=True,
        bake_space_transform=False,add_leaf_bones=False,bake_anim=False,use_mesh_modifiers=True,
        mesh_smooth_type='FACE',path_mode='STRIP')
    triangles=sum(sum(len(p.vertices)-2 for p in o.data.polygons) for o in objects)
    return dict(key=key,triangles=triangles,objects=len(objects),
        source='SourceArt/GraphicsUpgrade/Architecture/'+key+'.blend',
        asset='Assets/Resources/CorridorArchitecture/'+key+'.fbx',
        source_sha256=hashlib.sha256((SOURCE/(key+'.blend')).read_bytes()).hexdigest(),
        fbx_sha256=hashlib.sha256((DEST/(key+'.fbx')).read_bytes()).hexdigest())

records=[]
for variant in range(3):
    for lod in range(2): records.append(write(f'wall-{variant}-lod{lod}',wall,84010+variant,lod==0))
for key,builder in [('floor-course',floor),('ceiling-course',ceiling)]:
    for lod in range(2): records.append(write(key+f'-lod{lod}',builder,1462,lod==0))
for key,builder in [('door-leaf',door_leaf),('door-post',door_post),('door-lintel',door_lintel)]:
    for lod in range(2): records.append(write(key+f'-lod{lod}',builder,42061,lod==0))
for lod in range(2): records.append(write(f'plaster-wall-lod{lod}',plaster_wall,42065,lod==0))
(PROJECT/'SourceArt/GraphicsUpgrade/architecture-manifest.json').write_text(json.dumps(
    dict(blender=bpy.app.version_string,license='Original project authoring; no external geometry',
         units='metres; constructor Unity X right/Y up/front +Z',features=[
             'editable bevel profiles','weighted split normals','curved thin paper mesh',
             'staggered floorboard joints and 0.65 mm mesh cupping','measured PBR UVs','two physical mesh LODs'],models=records),
    indent=2)+'\n',encoding='utf8')
print('HAPPYTOY_CORRIDOR_ARCHITECTURE_AUTHORED',len(records),flush=True)
