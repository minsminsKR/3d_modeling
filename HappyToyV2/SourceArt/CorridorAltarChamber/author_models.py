"""Original chamber-specific measured models, pinned Blender 4.0.2.

Run: blender --background --python-exit-code 1 --python SourceArt/CorridorAltarChamber/author_models.py
Optional: -- --resource-project <read-only texture root> --preview-dir <outside-source output>
Constructors use Y up / front -Z in metres; FBX conversion is retained at import.
Only static visual meshes are exported. Physics belongs to CorridorAltarChamber.
"""
import argparse
import hashlib
import json
import math
import os
import random
import sys
from pathlib import Path
import bpy
from mathutils import Vector

HERE = Path(__file__).resolve().parent
PROJECT = HERE.parents[1]
SOURCE = HERE / 'Models'
OUTPUT = PROJECT / 'Assets/Resources/CorridorAltarChamber'
RESOURCE_PROJECT = PROJECT
RNG = random.Random(810084)
PALETTE = {
    'GU_dark_timber': ('wood-aged', (.65, .61, .53), 0, .22),
    'GU_aged_iron': ('metal-rust', (.73, .75, .72), .65, .17),
    'GU_tarnished_brass': ('brass-tarnished', (1, 1, 1), .80, .28),
    'GU_washi': ('paper-aged', (.76, .73, .64), 0, .10),
    'GU_charred_wick': ('cloth-charred', (1, 1, 1), 0, .035),
    'CA_BloodCloth': ('paper-aged', (.40, .15, .11), 0, .06),
    'CA_Dust': ('paper-aged', (.45, .43, .37), 0, .06),
    'CA_IvoryEnamel': ('painted-metal', (2.3, 1.9, 1.5), .12, .27),
    'CA_DeadGlass': ('wax-tallow', (.44, .52, .48), 0, .39),
    'CA_Chalkboard': ('chalkboard', (1, 1, 1), 0, .28),
}
SPANS = {'wood-aged': .55, 'metal-rust': 1, 'brass-tarnished': .30,
         'paper-aged': .6, 'cloth-charred': .15, 'painted-metal': .6, 'wax-tallow': .2, 'chalkboard': 1}


def coord(v): return (v[0], -v[2], v[1])
def unity(v): return (v[0], v[2], -v[1])


def material(slot):
    if bpy.data.materials.get(slot): return bpy.data.materials[slot]
    key, tint, metallic, smooth = PALETTE[slot]
    result = bpy.data.materials.new(slot); result.use_nodes = True; result.diffuse_color = (*tint, 1)
    nodes, links = result.node_tree.nodes, result.node_tree.links
    bsdf = nodes.get('Principled BSDF'); bsdf.inputs['Metallic'].default_value = metallic
    bsdf.inputs['Roughness'].default_value = 1 - smooth
    root = (PROJECT / 'Assets/Resources/CorridorAltarChamber/Chalkboard' if key == 'chalkboard' else
            RESOURCE_PROJECT / 'Assets/Resources/GraphicsPbr' / key)
    for kind in ('albedo', 'normal', 'metallic-smoothness'):
        path = root / (kind + '.png')
        if not path.is_file(): raise FileNotFoundError('Required tracked texture absent: ' + str(path))
        tex = nodes.new('ShaderNodeTexImage'); tex.label = kind
        tex.image = bpy.data.images.load(str(path), check_existing=True)
        if kind != 'albedo': tex.image.colorspace_settings.name = 'Non-Color'
        if kind == 'albedo':
            multiply = nodes.new('ShaderNodeMixRGB'); multiply.blend_type = 'MULTIPLY'
            multiply.inputs[0].default_value = 1; multiply.inputs[2].default_value = (*tint, 1)
            links.new(tex.outputs['Color'], multiply.inputs[1]); links.new(multiply.outputs['Color'], bsdf.inputs['Base Color'])
        elif kind == 'normal':
            normal = nodes.new('ShaderNodeNormalMap'); normal.inputs['Strength'].default_value = .60
            links.new(tex.outputs['Color'], normal.inputs['Color']); links.new(normal.outputs['Normal'], bsdf.inputs['Normal'])
        else:
            separate = nodes.new('ShaderNodeSeparateColor'); links.new(tex.outputs['Color'], separate.inputs[0])
            links.new(separate.outputs['Red'], bsdf.inputs['Metallic'])
            inverse = nodes.new('ShaderNodeMath'); inverse.operation = 'SUBTRACT'; inverse.inputs[0].default_value = 1
            links.new(tex.outputs['Alpha'], inverse.inputs[1]); links.new(inverse.outputs[0], bsdf.inputs['Roughness'])
    return result


def measured_uv(obj, slot):
    layer = obj.data.uv_layers.get('UVMap') or obj.data.uv_layers.new(name='UVMap')
    span = SPANS[PALETTE[slot][0]]
    for poly in obj.data.polygons:
        normal = Vector(unity(poly.normal)); axis = max(range(3), key=lambda a: abs(normal[a]))
        for index in poly.loop_indices:
            p = unity(obj.data.vertices[obj.data.loops[index].vertex_index].co)
            uv = (p[0], p[2]) if axis == 1 else (p[2], p[1]) if axis == 0 else (p[0], p[1])
            layer.data[index].uv = (uv[0] / span, uv[1] / span)


def box(name, at, size, slot='GU_dark_timber', bevel=.004):
    bpy.ops.mesh.primitive_cube_add(size=1, location=coord(at)); obj = bpy.context.object; obj.name = name
    obj.dimensions = (size[0], size[2], size[1]); bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    obj.data.materials.append(material(slot)); obj.data.use_auto_smooth = True; obj.data.auto_smooth_angle = math.radians(55)
    for p in obj.data.polygons: p.use_smooth = True
    if bevel:
        mod = obj.modifiers.new('Hand-worn three segment edge', 'BEVEL'); mod.width = min(bevel, min(size) * .22); mod.segments = 3
        bpy.ops.object.modifier_apply(modifier=mod.name)
        mod = obj.modifiers.new('Area-weighted hard surface normals', 'WEIGHTED_NORMAL'); mod.keep_sharp = True
        bpy.ops.object.modifier_apply(modifier=mod.name)
    measured_uv(obj, slot); return obj


def mesh(name, points, faces, slot, uv=None):
    data = bpy.data.meshes.new(name); data.from_pydata([coord(p) for p in points], [], faces); data.update()
    obj = bpy.data.objects.new(name, data); bpy.context.collection.objects.link(obj); data.materials.append(material(slot))
    if uv:
        layer = data.uv_layers.new(name='UVMap')
        for poly, values in zip(data.polygons, uv):
            for index, value in zip(poly.loop_indices, values): layer.data[index].uv = value
    else: measured_uv(obj, slot)
    return obj


def tube(name, points, radius, slot, sides=8):
    verts, faces, uv = [], [], []; distances = [0]
    for i in range(1, len(points)): distances.append(distances[-1] + (Vector(points[i]) - Vector(points[i - 1])).length)
    span = SPANS[PALETTE[slot][0]]
    for i, p in enumerate(points):
        normal = Vector(points[min(i + 1, len(points) - 1)]) - Vector(points[max(0, i - 1)])
        normal.normalize(); across = normal.cross(Vector((0, 1, 0)))
        if across.length < .01: across = normal.cross(Vector((1, 0, 0)))
        across.normalize(); up = normal.cross(across).normalized()
        for j in range(sides + 1):
            angle = j * math.pi * 2 / sides; pos = Vector(p) + (across * math.cos(angle) + up * math.sin(angle)) * radius
            verts.append(tuple(pos))
    for i in range(len(points) - 1):
        for j in range(sides):
            a, b = i * (sides + 1) + j, (i + 1) * (sides + 1) + j
            faces.append((a, b, b + 1, a + 1))
            uv.append(((j / sides * 2 * math.pi * radius / span, distances[i] / span),
                       (j / sides * 2 * math.pi * radius / span, distances[i + 1] / span),
                       ((j + 1) / sides * 2 * math.pi * radius / span, distances[i + 1] / span),
                       ((j + 1) / sides * 2 * math.pi * radius / span, distances[i] / span)))
    return mesh(name, verts, faces, slot, uv)


def screw(name, at, radius=.010):
    x, y, z = at
    bpy.ops.mesh.primitive_uv_sphere_add(segments=12, ring_count=6, radius=1, location=coord(at)); obj = bpy.context.object
    obj.name = name; obj.scale = (radius, radius * .26, radius); bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    obj.data.materials.append(material('GU_aged_iron')); measured_uv(obj, 'GU_aged_iron')
    slot = box(name + ' original cut slot', (x, y, z - radius * .29), (radius * 1.35, .0018, .001), 'GU_charred_wick', .0002)
    return [obj, slot]


def offering_table():
    parts = []
    # Broad old school teacher's chest: six planks, open mortise understructure,
    # shallow closed drawers and hand-forged corner iron retain convincing scale.
    for i in range(6):
        y = 1.000 + (i % 3 - 1) * .0007
        parts.append(box('Unequal worn desktop plank %02d' % i, (0, y, (i - 2.5) * .18), (2.16, .038, .178), bevel=.007))
    for x in (-1.017, 1.017): parts.append(box('Endgrain breadboard', (x, .999, 0), (.095, .043, 1.092), bevel=.009))
    parts.append(box('Teacher table under-top molding', (0, .947, 0), (2.09, .047, 1.036), bevel=.006))
    for x in (-.905, .905):
        for z in (-.421, .421):
            parts.append(box('Square mortised upright', (x, .464, z), (.099, .907, .103), bevel=.011))
            parts.append(box('Worn flared foot', (x, .043, z), (.127, .086, .132), bevel=.012))
            for y in (.18, .824): parts += screw('Through mortise flush peg', (x, y, z - .054), .014)
    for z in (-.449, .449):
        parts.append(box('Low crossgrain rail', (0, .185, z), (1.866, .079, .061), bevel=.006))
        parts.append(box('Deep upper apron rail', (0, .819, z), (1.874, .205, .049), bevel=.009))
        parts.append(box('Recessed long oak field', (0, .459, z + (.012 if z > 0 else -.012)), (1.706, .558, .029), bevel=.007))
        for x in (-.814, .814): parts.append(box('Raised field panel bead', (x, .459, z - .016), (.024, .521, .018), bevel=.003))
        for y in (.207, .711): parts.append(box('Recessed field cross bead', (0, y, z - .016), (1.645, .026, .018), bevel=.003))
        for x in (-.542, 0, .542): parts.append(box('Hand-fitted face plank', (x, .459, z - .019), (.534, .488, .013), bevel=.003))
    for x in (-.939, .939):
        parts.append(box('Inset end field panel', (x, .449, 0), (.031, .561, .737), bevel=.006))
        for z in (-.352, .352): parts.append(box('End field vertical bead', (x - .013, .449, z), (.02, .523, .023), bevel=.003))
    for x in (-.615, 0, .615):
        parts.append(box('Closed recessed school drawer face', (x, .821, -.487), (.582, .147, .037), bevel=.008))
        parts.append(box('Drawer inset oak field', (x, .821, -.509), (.528, .093, .009), bevel=.003))
        for dx in (-.083, .083): parts += screw('Drawer pull slotted bolt', (x + dx, .832, -.526), .008)
        parts.append(tube('Cold forged bail pull', [(x - .083, .829, -.531), (x - .074, .803, -.551),
                                                    (x + .074, .803, -.551), (x + .083, .829, -.531)], .007, 'GU_tarnished_brass', 10))
    for x in (-.902, .902):
        for y in (.287, .679):
            parts.append(box('Hammered corner strap', (x, y, -.503), (.136, .047, .005), 'GU_aged_iron', .0014))
            for dx in (-.048, .048): parts += screw('Square iron strap nail', (x + dx, y, -.508), .007)
    # A creased old crimson ritual textile, geometrically draped over the front,
    # has variable hems and torn fringe. The wood remains mostly exposed.
    points, faces = [], []; columns, rows = 22, 16
    for j in range(rows + 1):
        t = j / rows
        for i in range(columns + 1):
            u = i / columns; x = (u - .5) * .79
            if t < .47:
                y, z = 1.023 + .0035 * math.sin(u * 19), .29 - t / .47 * .77
            else:
                y, z = 1.023 - (t - .47) / .53 * (.81 + .04 * math.sin(u * 21)), -.484 - .028 * math.sin(u * 29) * math.sin(t * 4)
            points.append((x, y, z))
    for j in range(rows):
        for i in range(columns):
            if j == rows - 1 and i in (1, 6, 7, 14, 19): continue
            a = j * (columns + 1) + i; faces.append((a, a + 1, a + columns + 2, a + columns + 1))
    drape = mesh('Creased and torn ceremonial desk cloth', points, faces, 'CA_BloodCloth')
    solid = drape.modifiers.new('Actual cloth backface thickness', 'SOLIDIFY'); solid.thickness = .0015
    bpy.context.view_layer.objects.active = drape; bpy.ops.object.modifier_apply(modifier=solid.name); parts.append(drape)
    # A lateral writing ledger and inkstone reuse the same truthful school materials.
    parts.append(box('Untouched attendance ledger cloth cover', (.737, 1.049, .107), (.36, .026, .43), 'CA_Dust', .006))
    parts.append(box('Ledger worn page block', (.737, 1.068, .107), (.334, .014, .402), 'GU_washi', .002))
    parts.append(box('Attendance ledger spine', (.554, 1.057, .107), (.029, .04, .434), 'CA_BloodCloth', .003))
    return parts


def lesson_blackboard():
    parts = []
    parts.append(box('Blackboard solid rear backing', (0, 0, .037), (4.80, 1.66, .053), bevel=.006))
    # A real 10mm enamel-faced board carries the original chalk image on every
    # exposed panel face. Its front at Z=-.013 is clear of the wood backing's
    # Z=+.0105 front. It does not depend on one imported thin quad's backface.
    enamel = box('Last lesson enamel front', (0, 0, -.008), (4.62, 1.48, .010), 'CA_Chalkboard', .0007)
    layer = enamel.data.uv_layers[0]
    for polygon in enamel.data.polygons:
        for index in polygon.loop_indices:
            p = unity(enamel.data.vertices[enamel.data.loops[index].vertex_index].co)
            # Unity's FBX handedness conversion mirrors source-plan X. Map the
            # final room-facing text explicitly, instead of mirroring glyphs.
            layer.data[index].uv = (.5 - p[0] / 4.62, .5 + p[1] / 1.48)
    parts.append(enamel)
    for x in (-2.374, 2.374):
        parts.append(box('Split varnished outer stile', (x, 0, -.014), (.09, 1.70, .09), bevel=.010))
        parts.append(box('Inset worn stile molding', (x * .973, 0, -.031), (.024, 1.50, .021), bevel=.003))
    for y in (-.806, .806): parts.append(box('Original chalkboard crossgrain rail', (0, y, -.014), (4.70, .09, .09), bevel=.011))
    parts.append(box('Long chalk tray rolled ledge', (0, -.855, -.100), (4.72, .041, .226), 'CA_IvoryEnamel', .007))
    parts.append(box('Chalk tray raised front rim', (0, -.824, -.213), (4.72, .038, .023), 'CA_IvoryEnamel', .003))
    for x in (-2.15, 2.15):
        parts.append(box('Chalk tray forged mounting ear', (x, -.90, -.043), (.043, .151, .076), 'GU_aged_iron', .003))
        parts += screw('Blackboard rusty mounting screw', (x, .795, -.064), .008)
    for i, x in enumerate((-1.62, -1.43, .54, 1.77)):
        chalk = box('Broken remaining chalk stub', (x, -.817, -.137), (.046 + i * .007, .012, .013), 'GU_washi', .004)
        chalk.rotation_euler.z = math.radians(i * 12 - 8); parts.append(chalk)
    parts.append(box('Hardwood eraser block', (1.44, -.794, -.127), (.16, .037, .075), bevel=.005))
    parts.append(box('Dust packed eraser felt', (1.44, -.819, -.127), (.158, .014, .073), 'GU_charred_wick', .003))
    return parts


def hanging_seals():
    parts = []; points = [(-2.55 + i * .17, -.071 * math.sin(i / 30 * math.pi), 0) for i in range(31)]
    parts.append(tube('Sagging altar suspension cord', points, .009, 'GU_charred_wick', 8))
    for index in range(25):
        x = -2.4 + index * .2
        # A ragged window through the middle lets the focal altar/board remain readable.
        length = RNG.uniform(.43, .81) if abs(x) < .70 else RNG.uniform(.70, 1.22)
        width = RNG.uniform(.083, .138); top = -.071 * math.sin((x + 2.55) / 5.1 * math.pi)
        points, faces = [], []
        for row in range(9):
            t = row / 8; y = top - length * t
            for column in range(3):
                u = column / 2
                z = .022 * math.sin(t * 10 + index * .41) + .011 * math.sin(u * 8 + t * 7)
                points.append((x + (u - .5) * width, y + (.018 if row == 8 and column == 1 else 0), z))
        for row in range(8):
            for col in range(2):
                a = row * 3 + col; faces.append((a, a + 1, a + 4, a + 3))
        seal = mesh('Creased suspended washi seal %02d' % index, points, faces, 'GU_washi' if index % 6 else 'CA_BloodCloth')
        solid = seal.modifiers.new('Paper edge and visible rear', 'SOLIDIFY'); solid.thickness = .0009
        bpy.context.view_layer.objects.active = seal; bpy.ops.object.modifier_apply(modifier=solid.name); parts.append(seal)
        parts.append(box('Folded paper cord keeper', (x, top + .008, .005), (width * .45, .037, .017), 'CA_Dust', .001))
        # Unequal ink strokes sit on the actual creased face; no external glyph/clipart.
        for mark in range(5):
            t = .18 + mark * .115; y = top - length * t; z = .022 * math.sin(t * 10 + index * .41) - .0016
            parts.append(box('Faded talisman graphite stroke', (x + (.009 if mark % 2 else -.005), y, z),
                             (width * (.31 if mark % 3 else .59), .006 + (mark % 2) * .003, .0015), 'GU_charred_wick', .0004))
        parts.append(tube('Faded vertical seal ink', [(x, top - length * .15, -.007), (x, top - length * .72, -.007)], .0018, 'GU_charred_wick', 5))
    return parts


def fluorescent_fixture():
    parts = []
    parts.append(box('Old fluorescent folded casing', (0, 0, 0), (1.50, .083, .255), 'CA_IvoryEnamel', .009))
    parts.append(box('Recessed dead reflector trough', (0, -.047, 0), (1.35, .016, .207), 'GU_aged_iron', .004))
    for x in (-.667, .667):
        for z in (-.061, .061):
            parts.append(box('Ceramic fluorescent socket', (x, -.064, z), (.043, .049, .039), 'CA_DeadGlass', .005))
            parts += screw('Fixture casing captive screw', (x, -.045, -.109), .006)
    for z in (-.061, .061):
        tube_points = [(-.648, -.067, z), (.648, -.067, z)]
        parts.append(tube('Dust dimmed dead fluorescent tube', tube_points, .015, 'CA_DeadGlass', 20))
    for x in (-.518, .518): parts.append(box('Ceiling fixture hanger', (x, .046, 0), (.052, .021, .274), 'GU_aged_iron', .004))
    parts.append(tube('Cut school fixture electrical cable', [(.7, .041, .041), (.80, .14, .048), (.89, .109, .045), (.91, .044, .041)], .006, 'GU_charred_wick', 8))
    return parts


def save_model(key, parts, preview_dir):
    bpy.ops.object.select_all(action='DESELECT')
    for part in parts: part.select_set(True)
    bpy.context.view_layer.objects.active = parts[0]
    # Retain hand-editable individual named parts in .blend. Runtime FBX also keeps
    # those materials/parts; presentation combines them by physical material.
    tris = 0; bounds = []
    for obj in parts:
        obj.data.calc_loop_triangles(); tris += len(obj.data.loop_triangles)
        bounds += [unity(obj.matrix_world @ Vector(point)) for point in obj.bound_box]
    lo = [min(p[i] for p in bounds) for i in range(3)]; hi = [max(p[i] for p in bounds) for i in range(3)]
    source = SOURCE / (key + '.blend'); runtime = OUTPUT / (key + '.fbx')
    # Source .blend image references point to their final project destinations,
    # even when the authoring is executed inside a review staging mirror.
    for image in bpy.data.images:
        if image.source != 'FILE': continue
        path = Path(image.filepath).resolve()
        relative = path.relative_to(PROJECT) if PROJECT in path.parents else path.relative_to(RESOURCE_PROJECT)
        image.filepath = '//' + os.path.relpath(PROJECT / relative, SOURCE).replace('\\', '/')
    bpy.ops.wm.save_as_mainfile(filepath=str(source))
    bpy.ops.export_scene.fbx(filepath=str(runtime), use_selection=True, object_types={'MESH'}, axis_forward='-Z', axis_up='Y',
                             add_leaf_bones=False, bake_anim=False, use_mesh_modifiers=True, apply_unit_scale=True,
                             apply_scale_options='FBX_SCALE_UNITS', mesh_smooth_type='FACE', use_tspace=True, path_mode='STRIP')
    record = dict(key=key, source=source.relative_to(PROJECT).as_posix(), fbx=runtime.relative_to(PROJECT).as_posix(),
                  sourceSha256=hashlib.sha256(source.read_bytes()).hexdigest(), fbxSha256=hashlib.sha256(runtime.read_bytes()).hexdigest(),
                  boundsMin=lo, boundsMax=hi, triangles=tris, meshParts=[part.name for part in parts],
                  materialSlots=sorted({m.name for obj in parts for m in obj.data.materials}),
                  units='metres, source Y up / front -Z; imported transform retained')
    if preview_dir: preview(parts, key, lo, hi, preview_dir)
    return record


def preview(parts, key, lo, hi, directory):
    # Optional neutral studio evidence is deliberately outside the committed source.
    scene = bpy.context.scene; scene.render.engine = 'CYCLES'; scene.cycles.samples = 32
    scene.render.resolution_x = 1440; scene.render.resolution_y = 1000; scene.render.resolution_percentage = 100
    scene.view_settings.view_transform = 'AgX'; scene.world.color = (.11, .11, .11)
    centre = Vector(coord([(lo[i] + hi[i]) / 2 for i in range(3)])); extent = max(hi[i] - lo[i] for i in range(3))
    bpy.ops.object.camera_add(location=centre + Vector((extent * 1.1, extent * 1.7, extent * .8)))
    camera = bpy.context.object; camera.rotation_euler = (centre - camera.location).to_track_quat('-Z', 'Y').to_euler()
    camera.data.type = 'ORTHO'; camera.data.ortho_scale = extent * 1.3; scene.camera = camera
    for offset, power in (((-1.4, -1, 2.3), 500), ((1.3, 1.7, 1.5), 350)):
        bpy.ops.object.light_add(type='AREA', location=centre + Vector(offset) * extent)
        lamp = bpy.context.object; lamp.data.energy = power; lamp.data.shape = 'DISK'; lamp.data.size = extent * 2
        lamp.rotation_euler = (centre - lamp.location).to_track_quat('-Z', 'Y').to_euler()
    directory.mkdir(parents=True, exist_ok=True); scene.render.filepath = str(directory / (key + '-studio.png'))
    bpy.ops.render.render(write_still=True)


def main():
    global RESOURCE_PROJECT
    parser = argparse.ArgumentParser(description=__doc__); parser.add_argument('--resource-project', type=Path, default=PROJECT)
    parser.add_argument('--preview-dir', type=Path)
    builders = {'offering-table': offering_table, 'lesson-blackboard': lesson_blackboard,
                'hanging-seals': hanging_seals, 'fluorescent-fixture': fluorescent_fixture}
    parser.add_argument('--models', nargs='+', choices=list(builders), default=list(builders))
    args = parser.parse_args(sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else [])
    RESOURCE_PROJECT = args.resource_project.resolve(); preview_dir = args.preview_dir.resolve() if args.preview_dir else None
    SOURCE.mkdir(parents=True, exist_ok=True); OUTPUT.mkdir(parents=True, exist_ok=True)
    bpy.context.preferences.filepaths.save_version = 0
    report = HERE / 'model-manifest.json'
    previous = json.loads(report.read_text()) if report.is_file() else {'assets': []}
    records = [record for record in previous['assets'] if record['key'] not in args.models]
    for key in args.models:
        builder = builders[key]
        bpy.ops.wm.read_factory_settings(use_empty=True); bpy.context.scene.unit_settings.system = 'METRIC'
        parts = builder(); records.append(save_model(key, parts, preview_dir)); print('ALTAR_CHAMBER_MODEL_READY', key, records[-1]['triangles'], flush=True)
    report.write_text(json.dumps(dict(blender=bpy.app.version_string, authoring='Original hand-editable measured static meshes; no third-party mesh',
          assets=sorted(records, key=lambda record: list(builders).index(record['key'])),
          scope='Source/FBX export geometry proof; native Unity import, actual render and navigation acceptance require production checks'), indent=2), encoding='utf-8')


if __name__ == '__main__': main()
