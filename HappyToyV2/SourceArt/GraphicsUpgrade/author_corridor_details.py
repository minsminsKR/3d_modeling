"""Original render-only corridor details; Blender 4.0.2, editable metres sources.

Run blender --background --python SourceArt/GraphicsUpgrade/author_corridor_details.py.
Independent from the wall/floor author; no imported geometry or texture generation.
Unity constructors use X right, Y up, front +Z. Export applies the editable bevel,
weighted-normal and solidify modifiers and retains measured UVs. No physics files.
"""
import bpy
import hashlib
import json
import math
from pathlib import Path
from mathutils import Vector

PROJECT = Path(__file__).resolve().parents[2]
SOURCE = PROJECT / 'SourceArt/GraphicsUpgrade/Details'
DEST = PROJECT / 'Assets/Resources/CorridorDetails'
SOURCE.mkdir(parents=True, exist_ok=True)
DEST.mkdir(parents=True, exist_ok=True)
bpy.context.preferences.filepaths.save_version = 0
COLORS = {'CD_timber': (.12, .074, .036, 1), 'CD_paper': (.61, .55, .41, 1),
          'CD_binding': (.26, .055, .025, 1), 'CD_ink': (.06, .026, .008, 1),
          'CD_iron': (.057, .061, .053, 1), 'CD_enamel': (.32, .30, .24, 1),
          'CD_glass': (.015, .018, .019, 1)}
SPANS = {'CD_timber': .55, 'CD_paper': .6, 'CD_binding': .6, 'CD_ink': .6, 'CD_iron': 1., 'CD_enamel': .9, 'CD_glass': 1.}


def coord(v): return (v[0], -v[2], v[1])
def unity(v): return Vector((v[0], v[2], -v[1]))


def material(slot):
    if slot in bpy.data.materials: return bpy.data.materials[slot]
    m = bpy.data.materials.new(slot)
    m.diffuse_color = COLORS[slot]
    m.use_nodes = True
    p = m.node_tree.nodes.get('Principled BSDF')
    p.inputs['Base Color'].default_value = COLORS[slot]
    p.inputs['Roughness'].default_value = .78 if slot != 'CD_iron' else .44
    p.inputs['Metallic'].default_value = .8 if slot == 'CD_iron' else 0
    return m


def box(name, at, size, slot, bevel, detailed):
    bpy.ops.mesh.primitive_cube_add(size=1, location=coord(at))
    o = bpy.context.object; o.name = name
    o.dimensions = (size[0], size[2], size[1])
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    o.data.materials.append(material(slot)); o.data.use_auto_smooth = True
    b = o.modifiers.new('Editable eased edge profile', 'BEVEL')
    b.width = bevel; b.segments = 3 if detailed else 1
    n = o.modifiers.new('Area weighted split normals', 'WEIGHTED_NORMAL'); n.keep_sharp = True
    for p in o.data.polygons: p.use_smooth = True
    return o


def mesh_object(name, points, faces, slot, thickness=0):
    m = bpy.data.meshes.new(name); m.from_pydata([coord(p) for p in points], [], faces); m.update()
    o = bpy.data.objects.new(name, m); bpy.context.collection.objects.link(o)
    m.materials.append(material(slot)); m.use_auto_smooth = True
    for p in m.polygons: p.use_smooth = True
    if thickness:
        s = o.modifiers.new('Physical sheet thickness', 'SOLIDIFY'); s.thickness = thickness
    return o


def tube(name, points, radius, slot, detailed):
    sides = 8 if detailed else 5; vertices = []; faces = []
    pts = [Vector(p) for p in points]
    for i, p in enumerate(pts):
        tangent = (pts[min(i+1, len(pts)-1)]-pts[max(0, i-1)]).normalized()
        ref = Vector((0, 1, 0)) if abs(tangent.y) < .9 else Vector((1, 0, 0))
        u = tangent.cross(ref).normalized(); v = tangent.cross(u).normalized()
        for j in range(sides):
            angle = 2*math.pi*j/sides
            vertices.append(p+radius*(u*math.cos(angle)+v*math.sin(angle)))
    for i in range(len(pts)-1):
        for j in range(sides):
            a = i*sides+j; b = i*sides+(j+1)%sides
            faces.append((a, b, b+sides, a+sides))
    faces.extend((tuple(reversed(range(sides))), tuple((len(pts)-1)*sides+j for j in range(sides))))
    return mesh_object(name, vertices, faces, slot)


def memory_seal(detailed):
    box('Soft folded paper packet', (0, 0, 0), (.258, .272, .105), 'CD_paper', .006, detailed)
    # Several real folds form a projecting envelope flap, clear of the base surface.
    for side in (-1, 1):
        points = [(-.127, .132, side*.056), (.127, .132, side*.056),
                  (.12, .099, side*.061), (-.12, .099, side*.061),
                  (.066, .054, side*.067), (-.038, .06, side*.067)]
        faces = [(0, 1, 2, 3), (3, 2, 4, 5)]
        if side > 0: faces = [tuple(reversed(f)) for f in faces]
        mesh_object('Layered folded envelope flap', points, faces, 'CD_paper', .0008)
        tube('Cord across seal face', [(-.131, -.048, side*.058), (-.065, -.045, side*.062),
             (0, -.048, side*.062), (.065, -.05, side*.061), (.131, -.048, side*.058)], .004, 'CD_binding', detailed)
        for stroke in range(3):
            o = box('Stamped raised ink stroke', (.018 if stroke == 1 else -.012, .048-stroke*.028, side*.070),
                    (.062 if stroke == 1 else .08, .006, .001), 'CD_ink', .0003, detailed)
            o.rotation_euler.y = math.radians(-8 if stroke == 1 else 5)
        box('Stamped vertical ink stroke', (-.022, .034, side*.070), (.005, .089, .001), 'CD_ink', .0003, detailed)
    for side in (-1, 1):
        tube('Continuous packet edge binding', [(side*.131, -.048, -.058), (side*.134, -.048, -.027),
             (side*.134, -.048, .027), (side*.131, -.048, .058)], .004, 'CD_binding', detailed)
    n = 12 if detailed else 7
    tube('Hand tied cord loop', [(.015+.015*math.cos(i*2*math.pi/n), -.048+.012*math.sin(i*2*math.pi/n), .068)
         for i in range(n+1)], .003, 'CD_binding', detailed)


def entrance_panel(detailed):
    # Front face ends at +0.082 m; live return text stays in front at +0.085 m.
    for i in range(8):
        box('Individual entrance boards', ((i-3.5)*.186, 0, .060), (.183, 2.13, .014), 'CD_timber', .002, detailed)
    for x in (-.744, .744):
        box('Planed entrance stile', (x, 0, .065), (.055, 2.17, .021), 'CD_timber', .003, detailed)
        box('Inset narrow stile bead', (x, 0, .077), (.027, 2.105, .008), 'CD_timber', .0015, detailed)
    for y in (-1.047, 1.047):
        box('Entrance top and foot rail', (0, y, .068), (1.52, .076, .019), 'CD_timber', .004, detailed)
    # A gently undulating physical paper plaque and eased timber surround.
    nx, ny = (8, 4) if detailed else (3, 2); points = []; faces = []
    for iy in range(ny+1):
        for ix in range(nx+1):
            x = -.56+1.12*ix/nx; y = -.145+.69*iy/ny
            z = .078+.0006*math.sin(ix*1.37+iy*.61)
            points.append((x, y, z))
    for iy in range(ny):
        for ix in range(nx):
            a = iy*(nx+1)+ix; faces.append((a, a+1, a+nx+2, a+nx+1))
    mesh_object('Slack paper return plaque', points, faces, 'CD_paper', .0008)
    for x in (-.576, .576):
        box('Plaque eased upright', (x, .2, .078), (.021, .735, .008), 'CD_timber', .002, detailed)
    for y in (-.16, .56):
        box('Plaque eased horizontal bead', (0, y, .078), (1.174, .021, .008), 'CD_timber', .002, detailed)
    for mark in range(5):
        # Notches and bowed edges replace the old flat binding boxes.
        x = (mark-2)*.18
        mesh_object('Vermilion entrance seal strip', [(x-.041, -.155, .081), (x+.041, -.155, .081),
            (x+.042, -.31, .082), (x-.042, -.31, .082), (x+.037, -.51, .080), (x-.038, -.485, .080)],
            [(0, 1, 2, 3), (3, 2, 4, 5)], 'CD_binding', .001)


def passage_upright(detailed):
    box('Eased passage lining', (0, 0, 0), (.12, 2.45, .035), 'CD_timber', .003, detailed)
    for y in (-1.195, 1.195):
        box('Lining end shoulder', (0, y, .008), (.117, .053, .028), 'CD_timber', .002, detailed)
    box('Raised lining centre bead', (0, 0, .013), (.033, 2.335, .013), 'CD_timber', .002, detailed)


def passage_header(detailed):
    box('Eased passage header', (0, 0, 0), (2.95, .13, .038), 'CD_timber', .004, detailed)
    for y in (-.047, .047):
        box('Header small profiled bead', (0, y, .013), (2.88, .019, .018), 'CD_timber', .002, detailed)
    for x in (-1.36, 1.36):
        box('Header decorative end shoulder', (x, 0, .012), (.064, .098, .018), 'CD_timber', .002, detailed)


def ceiling_joist(detailed):
    box('Eased exposed ceiling joist', (0, 0, 0), (1, .055, .10), 'CD_timber', .004, detailed)
    # Slight inlaid shoulders are decorative; this module makes no hidden joint claim.
    for x in (-.36, .36):
        box('Flush joist shoulder', (x, -.019, 0), (.03, .012, .093), 'CD_timber', .0015, detailed)


def lantern_hardware(detailed):
    tube('Lantern suspension stem', [(0, .22, 0), (0, .33, 0), (0, .39, 0)], .006, 'CD_iron', detailed)
    count = 18 if detailed else 9
    hook = [(0, .37+.021*math.sin(i*math.pi*1.65/count), .021-.021*math.cos(i*math.pi*1.65/count)) for i in range(count+1)]
    tube('Bent iron suspension hook', hook, .0035, 'CD_iron', detailed)
    for x in (-.011, 0, .011):
        tube('Braided lantern tail cord', [(x, -.204, .001), (x*.85, -.235, .003), (x*.73, -.275, .005)], .003, 'CD_binding', detailed)
    for i in range(7 if detailed else 3):
        a = i*2*math.pi/(7 if detailed else 3)
        tube('Separate cloth tassel strands', [(.006*math.cos(a), -.265, .006*math.sin(a)),
             (.013*math.cos(a), -.302, .013*math.sin(a)), (.017*math.cos(a), -.33, .017*math.sin(a))], .0019, 'CD_binding', detailed)


def hanging_seal(detailed):
    # 1m normalized height/width; runtime scale retains the original safe bounds.
    nx, ny = (4, 10) if detailed else (2, 4); points = []; faces = []
    for iy in range(ny+1):
        t = iy/ny
        for ix in range(nx+1):
            u = ix/nx; x = u-.5
            # Lower edge is torn diagonally; top fixing edge remains straight.
            y = -1+t + (1-t)**8*(.09*u+.028*math.sin(u*math.pi*3))
            z = .004*math.sin(t*math.pi)*math.sin(u*math.pi)+.0025*math.sin(t*math.pi*3)*(1-t)
            points.append((x, y, z))
    for iy in range(ny):
        for ix in range(nx):
            a = iy*(nx+1)+ix; faces.append((a, a+1, a+nx+2, a+nx+1))
    mesh_object('Curled torn ceremonial strip', points, faces, 'CD_paper', .0006)


def framed_plaque(detailed):
    box('Eased paper plaque backing', (0, 0, -.003), (.92, .45, .012), 'CD_paper', .002, detailed)
    for x in (-.475, .475):
        box('Small framed plaque upright', (x, 0, .001), (.021, .494, .019), 'CD_timber', .002, detailed)
        box('Plaque upright inner eased bead', (x-.008*math.copysign(1, x), 0, .008), (.008, .465, .009), 'CD_timber', .001, detailed)
    for y in (-.24, .24):
        box('Small framed plaque rail', (0, y, .001), (.96, .021, .019), 'CD_timber', .002, detailed)


def binding_stamp(detailed):
    # Original quiet geometric room marker, with an actual textile edge thickness.
    box('Worn short binding marker', (0, 0, 0), (.032, .035, .004), 'CD_binding', .001, detailed)


def memory_socket(detailed):
    nx, nz = (4, 6) if detailed else (2, 3); points = []; faces = []
    for iz in range(nz+1):
        for ix in range(nx+1):
            x = -.114+.228*ix/nx; z = -.147+.294*iz/nz
            y = .0006*math.sin(ix*1.41+iz*.66)
            if ix == nx and iz == nz: y += .002
            points.append((x, y, z))
    for iz in range(nz):
        for ix in range(nx):
            a = iz*(nx+1)+ix; faces.append((a, a+nx+1, a+nx+2, a+1))
    mesh_object('Gently curled returned memory page', points, faces, 'CD_paper', .0015)
    box('Laid memory binding strip', (0, .0036, -.013), (.031, .002, .247), 'CD_binding', .0004, detailed)
    for mark in range(3):
        box('Faded name field stroke', (-.047+mark*.044, .0031, .041), (.020, .001, .043+mark*.004), 'CD_ink', .0002, detailed)


def timber_plaque(detailed):
    box('Eased altar count plaque body', (0, 0, 0), (.94, .22, .025), 'CD_timber', .004, detailed)
    for x in (-.451, .451):
        box('Count plaque decorative edge shoulder', (x, 0, -.010), (.018, .197, .010), 'CD_timber', .0015, detailed)


def classroom_photo(detailed):
    box('Aged attendance backing paper', (0, 0, 0), (.81, 1.03, .024), 'CD_paper', .003, detailed)
    for x in (-.415, .415):
        box('Eased photograph frame side', (x, 0, -.023), (.055, 1.11, .052), 'CD_timber', .004, detailed)
        box('Recessed inner photo frame bead', (x-.019*math.copysign(1, x), 0, -.043), (.012, 1.04, .014), 'CD_timber', .002, detailed)
    for y in (-.535, .535):
        box('Eased photograph frame rail', (0, y, -.023), (.84, .055, .052), 'CD_timber', .004, detailed)
    for row in range(7):
        box('Faded attendance charcoal mark', ((row % 3-1)*.067, .32-row*.10, -.014),
            (.42-(row % 3)*.054, .008, .002), 'CD_ink', .0004, detailed)


def window_recess(detailed):
    box('Opaque night and grime window backing', (0, 0, 0), (1.71, 1.76, .028), 'CD_glass', .003, detailed)
    for x in (-.873, 0, .873):
        box('Eased enamel school window mullion', (x, 0, -.031), (.043, 1.86, .052), 'CD_enamel', .004, detailed)
        box('Narrow inner window stop', (x+.013, 0, -.045), (.012, 1.82, .018), 'CD_enamel', .002, detailed)
    for y in (-.899, 0, .899):
        box('Eased school window enamel rail', (0, y, -.034), (1.79, .043, .052), 'CD_enamel', .004, detailed)
    box('Worn projecting timber school sill', (0, -.94, -.103), (1.88, .066, .27), 'CD_timber', .008, detailed)
    box('Sill small supporting profile', (0, -.987, -.051), (1.80, .028, .11), 'CD_timber', .003, detailed)


def exercise_leaf(detailed):
    nx, nz = (4, 7) if detailed else (2, 3); points = []; faces = []
    for iz in range(nz+1):
        for ix in range(nx+1):
            x = -.109+.218*ix/nx; z = -.1495+.299*iz/nz
            # A raised dog-ear is real geometry, not a printed triangular mark.
            corner = max(0, (ix/nx-.65)/.35)*max(0, (iz/nz-.65)/.35)
            y = .0007*math.sin(ix*1.55+iz*.83)+corner*.008
            if iz == 0: z += .0018*math.sin(ix*2.8)
            points.append((x, y, z))
    for iz in range(nz):
        for ix in range(nx):
            a = iz*(nx+1)+ix; faces.append((a, a+nx+1, a+nx+2, a+1))
    mesh_object('Dog eared torn class exercise page', points, faces, 'CD_paper', .0008)


def metre_uv(o):
    m = o.data; layer = m.uv_layers.get('UVMap') or m.uv_layers.new(name='UVMap')
    span = SPANS[m.materials[0].name]
    for p in m.polygons:
        n = unity(o.matrix_world.to_3x3() @ p.normal); axis = max(range(3), key=lambda i: abs(n[i]))
        for li in p.loop_indices:
            point = unity(o.matrix_world @ m.vertices[m.loops[li].vertex_index].co)
            layer.data[li].uv = ((point.x, point.z) if axis == 1 else (point.z, point.y) if axis == 0 else (point.x, point.y))
            layer.data[li].uv /= span


def write(key, builder, detailed):
    bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete(use_global=False)
    builder(detailed); objects = list(bpy.context.scene.objects)
    for o in objects: metre_uv(o)
    bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE / (key+'.blend')))
    for o in objects:
        bpy.context.view_layer.objects.active = o
        for mod in list(o.modifiers): bpy.ops.object.modifier_apply(modifier=mod.name)
        metre_uv(o)
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.export_scene.fbx(filepath=str(DEST / (key+'.fbx')), use_selection=True, object_types={'MESH'},
        axis_forward='-Z', axis_up='Y', apply_unit_scale=True, bake_space_transform=False,
        add_leaf_bones=False, bake_anim=False, use_mesh_modifiers=True, mesh_smooth_type='FACE', path_mode='STRIP')
    vertices = [unity(o.matrix_world @ v.co) for o in objects for v in o.data.vertices]
    minimum = [min(p[i] for p in vertices) for i in range(3)]
    maximum = [max(p[i] for p in vertices) for i in range(3)]
    return dict(key=key, triangles=sum(sum(len(p.vertices)-2 for p in o.data.polygons) for o in objects),
        objects=len(objects), bounds_min=minimum, bounds_max=maximum,
        source='SourceArt/GraphicsUpgrade/Details/'+key+'.blend', asset='Assets/Resources/CorridorDetails/'+key+'.fbx',
        source_sha256=hashlib.sha256((SOURCE / (key+'.blend')).read_bytes()).hexdigest(),
        fbx_sha256=hashlib.sha256((DEST / (key+'.fbx')).read_bytes()).hexdigest())


records = []
for key, builder in [('memory-seal', memory_seal), ('entrance-panel', entrance_panel),
    ('passage-upright', passage_upright), ('passage-header', passage_header),
    ('ceiling-joist', ceiling_joist), ('lantern-hardware', lantern_hardware), ('hanging-seal', hanging_seal),
    ('framed-plaque', framed_plaque), ('binding-stamp', binding_stamp), ('memory-socket', memory_socket),
    ('timber-plaque', timber_plaque), ('classroom-photo', classroom_photo), ('window-recess', window_recess),
    ('exercise-leaf', exercise_leaf)]:
    for lod in range(2): records.append(write(key+f'-lod{lod}', builder, lod == 0))
(PROJECT / 'SourceArt/GraphicsUpgrade/details-manifest.json').write_text(json.dumps(dict(
    blender=bpy.app.version_string, license='Original project authoring; no external geometry',
    units='metres; constructor Unity X right/Y up/front +Z', features=[
        'editable eased edge bevel profiles', 'weighted split normals', 'physical folded and curved paper',
        'tube mesh binding and bent metal hooks', 'separate tassel strands', 'measured PBR UVs', 'two mesh LODs'], models=records),
    indent=2)+'\n', encoding='utf8')
print('HAPPYTOY_CORRIDOR_DETAILS_AUTHORED', len(records), flush=True)
