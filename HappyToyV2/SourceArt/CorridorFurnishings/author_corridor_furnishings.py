"""Original measured Japanese archive furniture, authored with Blender 4.0.2.

Run from any directory:
  blender --background --python SourceArt/CorridorFurnishings/author_corridor_furnishings.py
  blender --background --python <script> -- --preview-dir <optional outside-source directory>

Every constructor uses a measured source plan: Y up, front -Z. Unity's FBX
handedness conversion mirrors the asymmetric source-plan X placements. Mount
contracts are explicit runtime owner-local metres and already account for that
conversion. The exporter preserves a separate, correctly pivoted Drawer mesh.
No third-party meshes, downloaded textures, editor scene or runtime is changed.
"""
import argparse
import hashlib
import json
import math
import random
import sys
from pathlib import Path

import bmesh
import bpy
from mathutils import Vector

HERE = Path(__file__).resolve().parent
PROJECT = HERE.parents[1]
MODELS = PROJECT / 'Assets/Resources/CorridorFurnishings'
SOURCE = HERE / 'Models'
RNG = random.Random(271007)

# Existing PBR source sets remain the sole texture source. New slots only tint
# those physical surfaces; runtime resolves the identical surface/tint contract.
PALETTE = {
    'GU_dark_timber': ('wood-aged', (.62, .58, .49), 0, .28),
    'GU_aged_iron': ('metal-rust', (.75, .76, .72), .72, .18),
    'GU_tarnished_brass': ('brass-tarnished', (1, 1, 1), .86, .30),
    'GU_washi': ('paper-aged', (.94, .90, .78), 0, .13),
    'GU_charred_wick': ('cloth-charred', (1, 1, 1), 0, .08),
    'CF_ledger_blue': ('paper-aged', (.20, .30, .32), 0, .13),
    'CF_ledger_red': ('paper-aged', (.45, .20, .12), 0, .13),
    'CF_celadon': ('ceramic-tile', (.62, .76, .66), 0, .52),
    'CF_dust': ('paper-aged', (.45, .42, .34), 0, .06),
}
SPANS = {'wood-aged': .55, 'metal-rust': 1, 'brass-tarnished': .30,
         'paper-aged': .6, 'cloth-charred': .15, 'ceramic-tile': 3}


def coord(v):
    return (v[0], -v[2], v[1])


def unity(v):
    # Recover measured constructor coordinates from Blender; this is the source
    # plan, before Unity's FBX handedness conversion of asymmetric X placements.
    return (v[0], v[2], -v[1])


def material(slot):
    existing = bpy.data.materials.get(slot)
    if existing:
        return existing
    key, tint, metallic, smooth = PALETTE[slot]
    result = bpy.data.materials.new(slot)
    result.diffuse_color = (*tint, 1)
    result.use_nodes = True
    nodes, links = result.node_tree.nodes, result.node_tree.links
    bsdf = nodes.get('Principled BSDF')
    bsdf.inputs['Metallic'].default_value = metallic
    bsdf.inputs['Roughness'].default_value = 1 - smooth
    for name in ('albedo', 'normal', 'metallic-smoothness'):
        path = PROJECT / 'Assets/Resources/GraphicsPbr' / key / (name + '.png')
        if not path.exists():
            raise FileNotFoundError('Tracked physical surface missing: ' + str(path.relative_to(PROJECT)))
        tex = nodes.new('ShaderNodeTexImage')
        tex.label = name
        tex.image = bpy.data.images.load(str(path), check_existing=True)
        if name != 'albedo':
            tex.image.colorspace_settings.name = 'Non-Color'
        if name == 'albedo':
            multiply = nodes.new('ShaderNodeMixRGB')
            multiply.blend_type = 'MULTIPLY'
            multiply.inputs[0].default_value = 1
            multiply.inputs[2].default_value = (*tint, 1)
            links.new(tex.outputs['Color'], multiply.inputs[1])
            links.new(multiply.outputs['Color'], bsdf.inputs['Base Color'])
        elif name == 'normal':
            normal = nodes.new('ShaderNodeNormalMap')
            normal.inputs['Strength'].default_value = .65
            links.new(tex.outputs['Color'], normal.inputs['Color'])
            links.new(normal.outputs['Normal'], bsdf.inputs['Normal'])
        else:
            separate = nodes.new('ShaderNodeSeparateColor')
            links.new(tex.outputs['Color'], separate.inputs[0])
            links.new(separate.outputs['Red'], bsdf.inputs['Metallic'])
            inverse = nodes.new('ShaderNodeMath')
            inverse.operation = 'SUBTRACT'
            inverse.inputs[0].default_value = 1
            links.new(tex.outputs['Alpha'], inverse.inputs[1])
            links.new(inverse.outputs[0], bsdf.inputs['Roughness'])
    return result


def select(objects):
    bpy.ops.object.select_all(action='DESELECT')
    for obj in objects:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = objects[0]


def measured_uv(obj, slot):
    """Planar face metres, coherent plank grain, independent of overall scale."""
    layer = obj.data.uv_layers.get('UVMap') or obj.data.uv_layers.new(name='UVMap')
    span = SPANS[PALETTE[slot][0]]
    for face in obj.data.polygons:
        normal = Vector(unity(face.normal))
        dominant = max(range(3), key=lambda a: abs(normal[a]))
        for loop in face.loop_indices:
            p = unity(obj.data.vertices[obj.data.loops[loop].vertex_index].co)
            # Longitudinal grain follows the largest structural axis on each face.
            pair = (p[0], p[2]) if dominant == 1 else (p[2], p[1]) if dominant == 0 else (p[0], p[1])
            layer.data[loop].uv = (pair[0] / span, pair[1] / span)


def box(name, at, size, slot='GU_dark_timber', bevel=.004):
    bpy.ops.mesh.primitive_cube_add(size=1, location=coord(at))
    obj = bpy.context.object
    obj.name = name
    obj.dimensions = (size[0], size[2], size[1])
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    obj.data.materials.append(material(slot))
    obj.data.use_auto_smooth = True
    obj.data.auto_smooth_angle = math.radians(55)
    for p in obj.data.polygons:
        p.use_smooth = True
    if bevel:
        mod = obj.modifiers.new('Three-segment hand-worn rounded edge', 'BEVEL')
        mod.width = min(bevel, min(size) * .24)
        mod.segments = 3
        bpy.ops.object.modifier_apply(modifier=mod.name)
        mod = obj.modifiers.new('Area-weighted joinery normals', 'WEIGHTED_NORMAL')
        mod.keep_sharp = True
        bpy.ops.object.modifier_apply(modifier=mod.name)
    measured_uv(obj, slot)
    return obj


def mesh(name, vertices, faces, slot, uv=None):
    data = bpy.data.meshes.new(name)
    data.from_pydata([coord(p) for p in vertices], [], faces)
    data.update()
    obj = bpy.data.objects.new(name, data)
    bpy.context.collection.objects.link(obj)
    data.materials.append(material(slot))
    for face in data.polygons:
        face.use_smooth = True
    data.use_auto_smooth = True
    data.auto_smooth_angle = math.radians(55)
    if uv:
        layer = data.uv_layers.new(name='UVMap')
        for points, poly in zip(uv, data.polygons):
            for loop, point in zip(poly.loop_indices, points):
                layer.data[loop].uv = point
    else:
        measured_uv(obj, slot)
    return obj


def tube(name, path, radius, slot='GU_tarnished_brass', sides=10):
    verts, faces = [], []
    for i, at in enumerate(path):
        at = Vector(at)
        tangent = (Vector(path[min(len(path) - 1, i + 1)]) - Vector(path[max(0, i - 1)])).normalized()
        reference = Vector((0, 1, 0)) if abs(tangent.y) < .9 else Vector((1, 0, 0))
        u = tangent.cross(reference).normalized()
        v = tangent.cross(u).normalized()
        r = radius[i] if isinstance(radius, list) else radius
        for j in range(sides):
            t = math.tau * j / sides
            verts.append(tuple(at + (u * math.cos(t) + v * math.sin(t)) * r))
    for i in range(len(path) - 1):
        for j in range(sides):
            a = i * sides + j
            b = i * sides + (j + 1) % sides
            faces.append((a, b, b + sides, a + sides))
    faces += [tuple(reversed(range(sides))), tuple((len(path) - 1) * sides + j for j in range(sides))]
    return mesh(name, verts, faces, slot)


def lathe(name, profile, at, slot='GU_tarnished_brass', segments=36):
    verts, faces, uv = [], [], []
    span = SPANS[PALETTE[slot][0]]
    circumference = math.tau * max(p[0] for p in profile) / span
    y_min = min(p[1] for p in profile)
    for i, (radius, y) in enumerate(profile):
        for j in range(segments + 1):
            angle = math.tau * j / segments
            verts.append((at[0] + radius * math.cos(angle), at[1] + y, at[2] + radius * math.sin(angle)))
    for i in range(len(profile) - 1):
        for j in range(segments):
            a = i * (segments + 1) + j
            b = a + segments + 1
            faces.append((a, b, b + 1, a + 1))
            uv.append(((circumference * j / segments, (profile[i][1] - y_min) / span),
                       (circumference * j / segments, (profile[i + 1][1] - y_min) / span),
                       (circumference * (j + 1) / segments, (profile[i + 1][1] - y_min) / span),
                       (circumference * (j + 1) / segments, (profile[i][1] - y_min) / span)))
    return mesh(name, verts, faces, slot, uv)


def screws(out, x, y, z, name='Slotted hand-driven brass fixing'):
    # Real head plus groove on front -Z; tiny features only on focal hardware.
    out.append(tube(name, [(x, y, z), (x, y, z - .003)], .0042, sides=12))
    out.append(box(name + ' incised slot', (x, y, z - .0032), (.006, .0008, .0005), 'GU_charred_wick', .0001))


def book(out, at, width=.062, height=.32, depth=.23, color='CF_ledger_blue'):
    x, y, z = at
    # Bound volume: recessed rag-paper page block, curved/rounded spine, overhang.
    out.append(box('Recessed uneven ledger page block', (x, y + height / 2, z),
                   (width - .012, height - .014, depth - .013), 'GU_washi', .002))
    for side in (-1, 1):
        out.append(box('Thick rag-paper ledger cover', (x + side * (width / 2 - .002), y + height / 2, z),
                       (.004, height, depth), color, .0012))
    out.append(box('Rounded sewn ledger spine', (x, y + height / 2, z - depth / 2),
                   (width, height, .019), color, .005))
    for band in (.17, .83):
        out.append(box('Sewn spine raised binding ridge', (x, y + height * band, z - depth / 2 - .01),
                       (width + .001, .009, .003), color, .001))
    out.append(box('Aged blank spine inventory label', (x, y + height * .62, z - depth / 2 - .0118),
                   (width * .66, height * .16, .0008), 'GU_washi', .0001))
    for j in range(3):
        out.append(box('Faded inventory strokes', (x - width * .17 + j * width * .17, y + height * .62,
                                                 z - depth / 2 - .0123), (.0016, height * (.06 + j * .012), .0004),
                       'GU_charred_wick', 0))
    for j in range(3):
        out.append(box('Visible deckle page edge', (x, y + height * (.25 + j * .23), z + depth / 2 - .005),
                       (width - .013, .0006, .0005), 'CF_dust', 0))


def scroll(out, at, length=.24, radius=.035):
    x, y, z = at
    # Horizontal roll: real ring silhouette/open core and exposed wooden end rods.
    obj = lathe('Rolled archive washi with open core', [(.010, -length / 2), (radius, -length / 2),
                (radius + .001, -length / 2 + .009), (radius, length / 2 - .008), (radius, length / 2),
                (.010, length / 2), (.010, -length / 2)], (0, 0, 0), 'GU_washi', 32)
    obj.rotation_euler[1] = math.pi / 2
    obj.location = coord((x, y + radius, z))
    out.append(obj)
    out.append(tube('Scroll exposed dark bamboo rod', [(x - length / 2 - .015, y + radius, z),
                                                     (x + length / 2 + .015, y + radius, z)], .010, 'GU_dark_timber', 12))
    for sign in (-1, 1):
        out.append(tube('Carved scroll finial', [(x + sign * (length / 2 + .009), y + radius, z),
                                              (x + sign * (length / 2 + .022), y + radius, z)],
                        .016, 'GU_dark_timber', 16))
    tie = []
    for j in range(33):
        a = math.tau * j / 32
        tie.append((x, y + radius + (radius + .002) * math.sin(a), z + (radius + .002) * math.cos(a)))
    out.append(tube('Twisted hemp scroll binding', tie, .0022, 'CF_dust', 6))


def writing_desk():
    body, drawer = [], []
    for j in range(5):
        body.append(box('Joined longitudinal desktop plank', ((j - 2) * .27, .818, 0),
                        (.268, .044, .493), bevel=.005))
    for z in (-.285, .285):
        body.append(box('Desktop end-grain breadboard cap', (0, .818, z), (1.35, .044, .083), bevel=.005))
    body.append(box('Hand-routed desktop lower bead', (0, .783, 0), (1.29, .029, .604), bevel=.005))
    for x in (-.585, .585):
        for z in (-.245, .245):
            body.append(box('Square mortised tapered leg', (x, .405, z), (.074, .756, .074), bevel=.005))
            body.append(box('Worn stepped leg foot', (x, .041, z), (.094, .082, .094), bevel=.005))
            body.append(box('Raised shoulder joinery collar', (x, .731, z), (.091, .050, .091), bevel=.004))
            # End grain peg is a real round mesh, proud by just 2 mm.
            body.append(tube('Through-tenon end-grain peg', [(x, .72, z - .038), (x, .72, z - .041)],
                             .007, 'GU_dark_timber', 12))
    for x in (-.595, .595):
        body.append(box('Recessed apron end panel', (x, .686, 0), (.025, .174, .43), bevel=.003))
        body.append(box('Low mortised side stretcher', (x, .24, 0), (.040, .060, .47), bevel=.003))
        for z in (-.213, .213):
            body.append(box('Apron end raised frame', (x, .688, z), (.030, .165, .025), bevel=.002))
    body.append(box('Recessed back apron', (0, .691, .246), (1.14, .17, .026), bevel=.003))
    body.append(box('Long through-mortise low rear rail', (0, .24, .246), (1.13, .06, .038), bevel=.003))
    for side in (-1, 1):
        body.append(box('Short front apron outside true drawer opening', (side * .545, .709, -.255),
                        (.112, .151, .052), bevel=.003))
        body.append(box('Drawer hardwood runner', (side * .48, .629, -.033), (.035, .025, .447), bevel=.002))
    body.append(box('Front opening lower rail', (0, .615, -.255), (.97, .021, .040), bevel=.002))
    # Real open-top cavity, not a solid drawer-shaped block.
    drawer.append(box('Drawer bottom fitted ragwood panel', (0, .642, -.052), (.922, .014, .426), bevel=.0015))
    for side in (-1, 1):
        drawer.append(box('Dovetailed drawer side', (side * .462, .696, -.052), (.020, .121, .438), bevel=.002))
        for k in range(3):
            drawer.append(box('Exposed drawer dovetail end', (side * .468, .656 + k * .035, -.278),
                              (.020, .024, .012), 'CF_dust', .001))
    drawer.append(box('Drawer rear wall', (0, .696, .167), (.925, .121, .020), bevel=.002))
    drawer.append(box('Raised single drawer front', (0, .708, -.297), (.973, .151, .044), bevel=.004))
    drawer.append(box('Recessed central drawer field', (0, .708, -.321), (.831, .089, .009), bevel=.003))
    for x in (-.425, .425):
        drawer.append(box('Front raised frame stile', (x, .708, -.324), (.027, .112, .015), bevel=.002))
    for y in (.656, .760):
        drawer.append(box('Front raised frame rail', (0, y, -.324), (.868, .020, .015), bevel=.002))
    for x in (-.075, .075):
        drawer.append(box('Stamped brass pull escutcheon', (x, .711, -.333), (.041, .046, .003), 'GU_tarnished_brass', .005))
        screws(drawer, x, .711, -.335)
    path = []
    for j in range(25):
        t = math.pi * j / 24
        path.append((.075 * math.cos(t), .709 - .039 * math.sin(t), -.347 - .014 * math.sin(t)))
    drawer.append(tube('Hanging oval brass bail pull', path, .0048, sides=10))
    drawer.append(box('Dark keyhole plate', (0, .753, -.334), (.018, .025, .003), 'GU_aged_iron', .003))
    drawer.append(tube('Keyhole upper circle', [(0, .758, -.336), (0, .758, -.337)], .004, 'GU_charred_wick', 12))
    drawer.append(box('Keyhole lower cutout', (0, .750, -.337), (.003, .010, .001), 'GU_charred_wick', .0005))
    for x in (-.58, .58):
        body.append(box('Aged brass desktop corner guard', (x, .842, -.263), (.115, .0015, .09), 'GU_tarnished_brass', .002))
        # Irregular thin particles trapped by the end-board groove, not a bright
        # rectangular fake dust decal lying on the tabletop.
        for j in range(4):
            center = x + (j - 1.5) * .014
            body.append(mesh('Irregular groove-trapped dust fragment',
                [(center - .006, .8402, .241), (center + .005, .8402, .244),
                 (center + .003, .8402, .249), (center - .005, .8402, .247)],
                [(0, 1, 2, 3)], 'CF_dust'))
    return [('Case', body, (0, 0, 0)), ('Drawer', drawer, (0, .708, -.052))]


def archive_shelf():
    out = []
    for x in (-.672, .672):
        for z in (-.145, .145):
            out.append(box('Archive cabinet mortised corner post', (x, 1.074, z), (.067, 2.148, .067), bevel=.004))
            out.append(box('Stepped archive foot', (x, .061, z), (.086, .122, .083), bevel=.004))
    for j in range(9):
        out.append(box('Separate tongue-and-groove rear plank', ((j - 4) * .144, 1.089, .183),
                       (.141, 1.962, .022), bevel=.002))
    for y in (.128, .480, .980, 1.480, 2.020):
        out.append(box('Thick rounded archive shelf', (0, y, -.015), (1.367, .040, .362), bevel=.004))
        out.append(box('Long shelf front routed bead', (0, y - .004, -.193), (1.378, .045, .021), bevel=.004))
        for x in (-.595, .595):
            out.append(box('Shelf support mortise block', (x, y - .039, -.030), (.049, .050, .278), bevel=.002))
    out.append(box('Shallow crown cornice cap', (0, 2.119, -.002), (1.45, .062, .400), bevel=.006))
    out.append(box('Crown inset molding', (0, 2.070, -.003), (1.422, .036, .386), bevel=.005))
    out.append(box('Bottom cabinet kick molding', (0, .094, -.201), (1.398, .064, .030), bevel=.004))
    for side in (-1, 1):
        out.append(box('Raised cabinet side field panel', (side * .699, 1.097, .017), (.014, 1.936, .237), bevel=.003))
        for z in (-.095, .124):
            out.append(box('Side joinery fine bead', (side * .708, 1.096, z), (.009, 1.930, .011), bevel=.0015))
    # Leave the right half of two shelves clear for finite runtime pickups.
    for i in range(7):
        book(out, (-.544 + i * .074, .501, -.012), width=.066, height=.300 + .020 * (i % 4),
             depth=.228, color='CF_ledger_red' if i in (1, 4) else 'CF_ledger_blue')
    for i in range(3):
        book(out, (-.508 + i * .091, 1.001, -.015), width=.082, height=.381 - i * .032,
             depth=.260, color='CF_ledger_red' if i == 1 else 'CF_ledger_blue')
    scroll(out, (.377, 1.001, -.013), .26, .044)
    scroll(out, (.377, 1.089, -.013), .22, .036)
    for i in range(2):
        scroll(out, (.293 + i * .203, .149, -.008), .17, .047)
    # Hollow, turned celadon vase: open rim, interior lip, foot ring and glaze wear.
    out.append(lathe('Hollow celadon storage vessel', [(0, 0), (.063, 0), (.069, .012), (.058, .032),
        (.082, .085), (.092, .170), (.084, .240), (.047, .292), (.043, .340), (.049, .351),
        (.048, .359), (.039, .359), (.036, .350), (.033, .309), (.068, .226), (.077, .170),
        (.061, .076), (.043, .044), (0, .044)], (-.429, 1.501, -.010), 'CF_celadon', 48))
    out.append(lathe('Unglazed ceramic vessel foot', [(0, 0), (.060, 0), (.063, .006), (.060, .015), (0, .015)],
                      (-.429, 1.501, -.010), 'CF_dust', 40))
    for i in range(3):
        out.append(box('Stacked folded cloth-bound folio', (-.194, 1.503 + i * .052, -.012),
                       (.240 - i * .013, .048, .253), 'CF_ledger_red' if i == 1 else 'CF_ledger_blue', .005))
        out.append(box('Exposed folded folio rag-paper fore edge', (-.194, 1.505 + i * .052, -.140),
                       (.207 - i * .013, .032, .003), 'GU_washi', .001))
    for side in (-1, 1):
        out.append(box('Corner dust accumulation on bottom shelf', (side * .612, .150, .083),
                       (.075, .001, .089), 'CF_dust', .0001))
    return [('Archive', out, (0, 0, 0))]


def writing_set():
    out = []
    # Desk-local small still life, intentionally reserves desktop right for pickup.
    out.append(box('Closed archive ledger lower cover', (-.111, .006, .010), (.253, .012, .306), 'CF_ledger_blue', .003))
    out.append(box('Visible rag-paper ledger page block', (-.110, .025, .007), (.237, .027, .285), 'GU_washi', .002))
    out.append(box('Closed archive ledger upper cover', (-.111, .045, .010), (.253, .012, .306), 'CF_ledger_blue', .003))
    out.append(box('Horizontal ledger sewn spine', (-.233, .027, .012), (.014, .054, .305), 'CF_ledger_blue', .004))
    out.append(box('Old calligraphy ledger label', (-.112, .052, .025), (.07, .001, .124), 'GU_washi', .0005))
    for i in range(5):
        out.append(box('Faded vertical calligraphy ink stroke', (-.128 + i * .008, .0528, .025),
                       (.0020, .0005, .041 + .007 * (i % 3)), 'GU_charred_wick', 0))
    for i in range(6):
        page = box('Unequal loose washi manuscript leaf', (.128 + .0018 * i, .002 + i * .0013, -.032),
                   (.178, .0011, .232), 'GU_washi', .0001)
        page.rotation_euler[2] = .012 * (i - 2)
        out.append(page)
    for i in range(7):
        out.append(box('Manuscript uneven black ink line', (.079 + i * .014, .0108, -.032),
                       (.0018, .0006, .107 - (i % 3) * .009), 'GU_charred_wick', 0))
    # Carved inkstone with real raised rim around recessed well.
    out.append(box('Carved black inkstone foot', (.061, .018, .178), (.155, .035, .084), 'GU_charred_wick', .008))
    for side in (-1, 1):
        out.append(box('Inkstone long raised rim', (.061, .039, .178 + side * .036), (.150, .020, .012), 'GU_charred_wick', .004))
        out.append(box('Inkstone short raised rim', (.061 + side * .069, .039, .178), (.014, .020, .061), 'GU_charred_wick', .004))
    out.append(box('Recessed old ink well', (.061, .037, .178), (.112, .003, .052), 'GU_charred_wick', .003))
    out.append(tube('Long tapered bamboo writing brush', [(-.075, .059, -.094), (.063, .059, .072),
                                                      (.099, .059, .115)], [.0046, .0041, .001], 'GU_dark_timber', 12))
    out.append(tube('Ink-dark brush hairs', [(.094, .059, .109), (.112, .058, .132), (.122, .055, .148)],
                     [.0045, .003, .0003], 'GU_charred_wick', 12))
    out.append(lathe('Small open ceramic water cup', [(0, 0), (.026, 0), (.031, .010), (.036, .044),
        (.037, .061), (.032, .064), (.029, .061), (.027, .014), (0, .014)],
                      (-.169, 0, .201), 'CF_celadon', 36))
    return [('Writing set', out, (0, 0, 0))]


def firecracker_pack():
    out = []
    for i, (x, z) in enumerate(((-.037, -.024), (.037, -.024), (0, .041))):
        # Whole pack is reduced below to clear the actual .7685m underside of
        # the desk's full-width under-top bead, not only the .794m top planks.
        height = .106 + .004 * i
        out.append(lathe('Hand-rolled red firecracker paper tube', [(0, 0), (.030, 0), (.032, .005),
            (.032, height - .007), (.028, height), (0, height)], (x, 0, z), 'CF_ledger_red', 32))
        out.append(lathe('Crimped washi firecracker cap', [(0, height), (.026, height), (.023, height + .002),
            (0, height + .002)], (x, 0, z), 'GU_washi', 24))
        out.append(box('Narrow faded firecracker wrapper strip', (x, .060, z - .032), (.016, .061, .001), 'GU_washi', .0005))
        out.append(tube('Twisted exposed firecracker fuse', [(x, height, z), (x + .007, height + .010, z),
                        (x + .014, height + .019, z - .002)], [.0015, .0014, .0006], 'GU_charred_wick', 6))
    for y in (.029, .082):
        path = []
        for j in range(49):
            a = math.tau * j / 48
            path.append((.071 * math.cos(a), y + .001 * math.sin(3 * a), .012 + .065 * math.sin(a)))
        out.append(tube('Real continuous hemp bundle tie', path, .0020, 'CF_dust', 6))
    out.append(tube('Small knotted hemp tag loop', [(-.012, .082, -.055), (-.018, .098, -.060),
                    (0, .107, -.061), (.017, .096, -.059), (.012, .082, -.055)], .002, 'CF_dust', 6))
    # Preserve the floor origin and all modeled radii/joins as one proportionate
    # compact bundle. Measured max .111995 + mount .649 = .760995,
    # over 7mm below the real .7685 bead underside.
    for obj in out:
        obj.location *= .84
        for vertex in obj.data.vertices:
            vertex.co *= .84
    return [('Firecracker pack', out, (0, 0, 0))]


def battery_pack():
    out = []
    # Original re-authored paired zinc cells without the existing floor waymarker.
    for side in (-1, 1):
        x = side * .046
        out.append(lathe('Rolled zinc-cell aged paper wrapper', [(0, 0), (.031, 0), (.038, .008),
            (.039, .019), (.038, .197), (.037, .220), (.027, .227), (0, .227)],
                          (x, 0, 0), 'GU_washi', 40))
        for y in (.004, .213):
            out.append(lathe('Crimped zinc-cell steel end cap', [(0, y), (.031, y), (.039, y + .002),
                (.039, y + .006), (.034, y + .009), (0, y + .009)], (x, 0, 0), 'GU_aged_iron', 36))
        out.append(lathe('Raised battery brass positive terminal', [(0, .224), (.015, .224),
            (.018, .229), (.017, .239), (.012, .242), (0, .242)], (x, 0, 0), 'GU_tarnished_brass', 24))
        out.append(box('Embossed battery positive horizontal', (x, .182, -.039), (.015, .0022, .0015), 'GU_tarnished_brass', .0004))
        out.append(box('Embossed battery positive vertical', (x, .182, -.039), (.0022, .015, .0015), 'GU_tarnished_brass', .0004))
        out.append(box('Folded paper battery-wrapper seam', (x, .114, .038), (.005, .189, .0017), 'CF_dust', .0003))
        out.append(lathe('Faded dark battery identification band', [(.0382, .055), (.0382, .082)],
                          (x, 0, 0), 'CF_ledger_blue', 40))
    out.append(tube('Forged paired-cell carrying keeper', [(-.079, .095, -.020), (-.077, .103, -.041),
        (-.046, .103, -.049), (0, .103, -.037), (.046, .103, -.049), (.077, .103, -.041), (.079, .095, -.020)],
        .0027, 'GU_aged_iron', 8))
    return [('Battery pack', out, (0, 0, 0))]


CONTRACTS = {
    'writing-desk': {
        'drawer': {'transform': 'Drawer', 'pivot': [0, .708, -.052], 'openAxis': [0, 0, -1], 'travel': .30,
                   'innerFloorTop': .649, 'innerBoundsMin': [-.451, .649, -.265],
                   'innerBoundsMax': [.451, .7685, .157], 'wallTop': .7565,
                   'closedCavityCeiling': .7685, 'pickupMount': [0, .649, -.030]},
        'pickupMounts': {'desktop': [.350, .840, -.02], 'drawer': [0, .649, -.030]},
        'collisionBoxes': [
            {'name': 'Countertop', 'center': [0, .816, 0], 'size': [1.35, .048, .653]},
            {'name': 'Solid under-top bead', 'center': [0, .783, 0], 'size': [1.29, .029, .604]},
            *[{'name': 'Leg', 'center': [x, .391, z], 'size': [.094, .782, .094]}
              for x in (-.585, .585) for z in (-.245, .245)],
            {'name': 'Back apron', 'center': [0, .691, .246], 'size': [1.14, .17, .026]},
        ],
        'drawerCollisionBoxes': [
            {'name': 'Bottom', 'center': [0, .642, -.052], 'size': [.922, .014, .426]},
            {'name': 'Front', 'center': [0, .708, -.297], 'size': [.973, .151, .044]},
            {'name': 'Back', 'center': [0, .696, .167], 'size': [.925, .121, .020]},
            *[{'name': 'Side', 'center': [x, .696, -.052], 'size': [.020, .121, .438]} for x in (-.462, .462)],
        ],
    },
    'archive-shelf': {
        'mountCoordinateSpace': 'Runtime furniture owner-local metres after preserved FBX conversion; source-plan X is mirrored',
        'pickupMounts': {'lowerLeft': [-.40, .504, -.09], 'middleLeft': [-.40, 1.504, -.09]},
        'placementEnvelope': {'center': [0, 1.075, 0], 'size': [1.45, 2.15, .4],
                              'usage': 'Placement/navigation clearance only; never a solid physics collider'},
        'collisionBoxes': [
            *[{'name': 'Corner post', 'center': [x, 1.074, z], 'size': [.067, 2.148, .067]}
              for x in (-.672, .672) for z in (-.145, .145)],
            {'name': 'Rear planks', 'center': [0, 1.089, .183], 'size': [1.293, 1.962, .022]},
            *[{'name': 'Bearing shelf', 'center': [0, y, -.015], 'size': [1.367, .040, .362]}
              for y in (.128, .480, .980, 1.480, 2.020)],
            {'name': 'Crown cornice', 'center': [0, 2.119, -.002], 'size': [1.45, .062, .400]},
            *[{'name': 'End side panel', 'center': [x, 1.097, .017], 'size': [.014, 1.936, .237]}
              for x in (-.699, .699)],
        ],
        'shelfSurfaceHeights': [.148, .50, 1.0, 1.5, 2.04],
        'emptyPickupRegions': [ {'min': [-.57, .50, -.17], 'max': [-.10, .85, .1]},
                                {'min': [-.57, 1.5, -.17], 'max': [-.06, 1.86, .1]}],
    },
    'writing-set': {'desktopOffset': [-.25, .84, .05]},
    'firecracker-pack': {'pickupOrigin': 'bottom center', 'closedDrawerFit': {'maximumHeight': .112, 'mountY': .649, 'ceilingY': .7685}},
    'battery-pack': {'pickupOrigin': 'bottom center'},
}


def join_part(name, objects, pivot):
    select(objects)
    bpy.ops.object.join()
    obj = bpy.context.object
    obj.name = name
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    bmesh.ops.remove_doubles(bm, verts=list(bm.verts), dist=.0000001)
    bmesh.ops.dissolve_degenerate(bm, edges=list(bm.edges), dist=.00000001)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    # FBX tangent export only supports triangles/quads. Preserve bevel quads,
    # triangulate circular caps and chamfered face n-gons deterministically.
    bmesh.ops.triangulate(bm, faces=[face for face in bm.faces if len(face.verts) > 4])
    bm.to_mesh(obj.data)
    bm.free()
    origin = Vector(coord(pivot))
    for vertex in obj.data.vertices:
        vertex.co -= origin
    obj.location = origin
    obj.data.update()
    obj['original_authored_asset'] = True
    return obj


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def finalize(key, parts):
    objects = [join_part(name, meshes, pivot) for name, meshes, pivot in parts]
    bpy.ops.object.empty_add(type='PLAIN_AXES')
    root = bpy.context.object
    root.name = key
    root['coordinate_contract'] = 'Unity metres; Y up; front -Z; imported conversion preserved'
    root['asset_license'] = 'CC0-1.0 original procedural authored geometry'
    for obj in objects:
        obj.parent = root
    bpy.ops.outliner.orphans_purge(do_local_ids=True, do_linked_ids=False, do_recursive=True)
    blend = SOURCE / (key + '.blend')
    # Repository-relative paths to tracked source textures: no installed cache or
    # workstation path is needed when this blend is opened on another computer.
    bpy.ops.wm.save_as_mainfile(filepath=str(blend), compress=True)
    bpy.ops.file.make_paths_relative()
    bpy.ops.wm.save_as_mainfile(filepath=str(blend), compress=True)
    select([root] + objects)
    fbx = MODELS / (key + '.fbx')
    bpy.ops.export_scene.fbx(filepath=str(fbx), use_selection=True, object_types={'MESH', 'EMPTY'},
        apply_unit_scale=True, global_scale=1, axis_forward='-Z', axis_up='Y', use_mesh_modifiers=True,
        mesh_smooth_type='FACE', use_tspace=True, add_leaf_bones=False, bake_anim=False, path_mode='RELATIVE')
    coords = [Vector(unity(obj.matrix_world @ v.co)) for obj in objects for v in obj.data.vertices]
    lo = [min(v[i] for v in coords) for i in range(3)]
    hi = [max(v[i] for v in coords) for i in range(3)]
    tris = 0
    for obj in objects:
        obj.data.calc_loop_triangles()
        tris += len(obj.data.loop_triangles)
        if not obj.data.uv_layers:
            raise RuntimeError('UV missing: ' + obj.name)
    if tris > 40000:
        raise RuntimeError('Prop budget exceeded: %s %d' % (key, tris))
    result = dict(key=key, resource='CorridorFurnishings/' + key, vertices=sum(len(o.data.vertices) for o in objects),
        triangles=tris, boundsMin=lo, boundsMax=hi, dimensions=[hi[i] - lo[i] for i in range(3)],
        meshParts=[o.name for o in objects], materialSlots=sorted({m.name for o in objects for m in o.data.materials}),
        uvLayers=1, colliders=0, source=blend.relative_to(PROJECT).as_posix(), fbx=fbx.relative_to(PROJECT).as_posix(),
        sourceSha256=sha(blend), fbxSha256=sha(fbx), **CONTRACTS[key])
    return objects, result


def preview(objects, key, directory):
    scene = bpy.context.scene
    scene.render.engine = 'CYCLES'
    scene.cycles.samples = 40
    scene.cycles.use_denoising = True
    scene.render.resolution_x = 1200
    scene.render.resolution_y = 1000
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = 'PNG'
    scene.world.color = (.045, .045, .045)
    scene.view_settings.view_transform = 'AgX'
    coords = [o.matrix_world @ v.co for o in objects for v in o.data.vertices]
    lo = Vector(tuple(min(v[i] for v in coords) for i in range(3)))
    hi = Vector(tuple(max(v[i] for v in coords) for i in range(3)))
    center, extent = (lo + hi) / 2, max(hi - lo)
    bpy.ops.object.camera_add(location=center + Vector((extent * 1.25, extent * 2.00, extent * 1.05)))
    camera = bpy.context.object
    camera.rotation_euler = (center - camera.location).to_track_quat('-Z', 'Y').to_euler()
    camera.data.type = 'ORTHO'
    camera.data.ortho_scale = extent * 1.40
    scene.camera = camera
    for offset, energy in (((-1.5, 1.5, 2), 600), ((1.5, -.5, 1), 300), ((0, -2, 2), 500)):
        bpy.ops.object.light_add(type='AREA', location=center + Vector(offset) * max(.3, extent))
        lamp = bpy.context.object
        lamp.data.energy = energy * max(.09, extent * extent)
        lamp.data.shape = 'DISK'
        lamp.data.size = extent * 1.8
        lamp.rotation_euler = (center - lamp.location).to_track_quat('-Z', 'Y').to_euler()
    directory.mkdir(parents=True, exist_ok=True)
    scene.render.filepath = str(directory / (key + '-studio.png'))
    bpy.ops.render.render(write_still=True)
    if key == 'writing-desk':
        drawer = next(o for o in objects if o.name == 'Drawer')
        drawer.location += Vector(coord((0, 0, -.30)))
        scene.render.filepath = str(directory / (key + '-drawer-open-studio.png'))
        bpy.ops.render.render(write_still=True)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--preview-dir', type=Path)
    parser.add_argument('--keys', nargs='+')
    args = parser.parse_args(sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else [])
    bpy.context.preferences.filepaths.save_version = 0
    MODELS.mkdir(parents=True, exist_ok=True)
    SOURCE.mkdir(parents=True, exist_ok=True)
    builders = {'writing-desk': writing_desk, 'archive-shelf': archive_shelf,
                'writing-set': writing_set, 'firecracker-pack': firecracker_pack, 'battery-pack': battery_pack}
    records = []
    for key in args.keys or builders:
        bpy.ops.object.select_all(action='SELECT')
        bpy.ops.object.delete(use_global=False)
        bpy.context.scene.unit_settings.system = 'METRIC'
        bpy.context.scene.unit_settings.scale_length = 1
        objects, record = finalize(key, builders[key]())
        records.append(record)
        print('CORRIDOR_FURNISHING_READY ' + json.dumps(record), flush=True)
        if args.preview_dir:
            preview(objects, key, args.preview_dir)
    path = HERE / 'model-manifest.json'
    existing = json.loads(path.read_text()) if path.exists() else {'assets': []}
    existing['assets'] = [a for a in existing['assets'] if a['key'] not in (args.keys or builders)] + records
    existing.update(schema=1, blender=bpy.app.version_string, license='CC0-1.0 geometry',
        authoring='Original measured joinery, carved hardware, sewn ledgers, lathed ceramic and scroll meshes',
        units='metres; Y-up front -Z; constructor source-plan X mirrored by Unity FBX handedness conversion',
        boundsCoordinateSpace='Source constructor plan before Unity FBX conversion; asymmetric runtime X bounds are [-sourceMaxX,-sourceMinX]',
        runtimeMountCoordinateSpace='Furniture owner-local Unity metres; source-plan X sign conversion already reflected in shelf pickup mounts',
        textureSource='Tracked Assets/Resources/GraphicsPbr; source .blend relative links',
        materialContract={name: dict(surfaceKey=v[0], tint=list(v[1])) for name, v in PALETTE.items()},
        gameplay='Runtime adds finite existing pickups and collider proxies; source contains only visual geometry')
    path.write_text(json.dumps(existing, indent=2) + '\n', encoding='utf-8')


if __name__ == '__main__':
    main()
