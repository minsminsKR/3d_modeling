"""Derive an inspectable three-part gate from the preserved generated mesh.

Blender 4.0.2, background, CPU. This is an authoring prototype: rectangular
cut bounds and pivots MUST be visually reviewed before game integration.
The existing corridor gate and source .blend are never modified.
"""
import hashlib
import json
import math
from pathlib import Path
import sys

import bmesh
import bpy
from mathutils import Matrix, Vector

here = Path(__file__).resolve().parent
sys.path.insert(0, str(here))
from gate_edge_finish import finish_edges
source = here.parent / 'CorridorEntrance/temple-gate/temple-gate.blend'
output = here / 'gate-opening'
output.mkdir(exist_ok=True)
editable = output / 'gate-opening.blend'
export = output / 'gate-opening.fbx'
thin_export = output / 'gate-opening-thin.fbx'
report_path = output / 'geometry-report.json'
sha = lambda path: hashlib.sha256(path.read_bytes()).hexdigest()
source_hash = sha(source)
expected_source = 'bdd3cdbda2030a1895cf09c9b12883c055dc8af483e23b5b9a06e41dcf6c2623'
if source_hash != expected_source:
    raise RuntimeError('Gate source changed; re-evaluate the measured UV repair before rebuilding')
bpy.ops.wm.open_mainfile(filepath=str(source))
bpy.context.preferences.filepaths.save_version = 0
original = bpy.data.objects.get('CE_temple-gate_LOD0')
if not original or original.type != 'MESH' or not original.data.uv_layers.active:
    raise RuntimeError('Expected preserved source-derived gate mesh and UV atlas')
if max(abs(original.matrix_world[r][c] - Matrix.Identity(4)[r][c]) for r in range(4) for c in range(4)) > 1e-6:
    raise RuntimeError('Expected the preserved gate in its authored local coordinate frame')

# Blender +Z is up and -Y is the front. These initial rectangular bounds were
# inferred from geometry only; decorative studs and pillar borders require
# explicit camera review and possibly adjusted cuts. The entire depth is kept
# so the complete, unsimplified original remains reconstructible when closed.
half_width, bottom, top, half_depth = 3.05, .80, 6.05, 1.95
cuts = [
    (Vector((-half_width, 0, 0)), Vector((1, 0, 0))),
    (Vector((0, 0, 0)), Vector((1, 0, 0))),
    (Vector((half_width, 0, 0)), Vector((1, 0, 0))),
    (Vector((0, -half_depth, 0)), Vector((0, 1, 0))),
    (Vector((0, half_depth, 0)), Vector((0, 1, 0))),
    (Vector((0, 0, bottom)), Vector((0, 0, 1))),
    (Vector((0, 0, top)), Vector((0, 0, 1))),
]
bm = bmesh.new()
bm.from_mesh(original.data)
area_before = sum(face.calc_area() for face in bm.faces)
for point, normal in cuts:
    bmesh.ops.bisect_plane(bm, geom=list(bm.verts) + list(bm.edges) + list(bm.faces),
                           dist=1e-6, plane_co=point, plane_no=normal,
                           clear_inner=False, clear_outer=False)
bmesh.ops.triangulate(bm, faces=list(bm.faces))
part_layer = bm.faces.layers.int.new('gate_part')
counts = [0, 0, 0]
for face in bm.faces:
    p = face.calc_center_median()
    inside = abs(p.x) < half_width and abs(p.y) < half_depth and bottom < p.z < top
    part = (1 if p.x < 0 else 2) if inside else 0
    face[part_layer] = part
    counts[part] += 1
if min(counts) < 100:
    raise RuntimeError('A derived gate part is unexpectedly small: ' + str(counts))

collection = bpy.data.collections.new('PROTOTYPE - separable gate parts')
bpy.context.scene.collection.children.link(collection)
names = ('Gate frame', 'Left gate leaf', 'Right gate leaf')
hinges = (Vector((0, 0, 0)), Vector((-half_width, -1.6, 0)), Vector((half_width, -1.6, 0)))
objects = []
total_area = 0
for index, (name, hinge) in enumerate(zip(names, hinges)):
    part = bm.copy()
    tag = part.faces.layers.int.get('gate_part')
    bmesh.ops.delete(part, geom=[face for face in part.faces if face[tag] != index], context='FACES_ONLY')
    bmesh.ops.delete(part, geom=[edge for edge in part.edges if not edge.link_faces], context='EDGES')
    bmesh.ops.delete(part, geom=[vert for vert in part.verts if not vert.link_edges], context='VERTS')
    total_area += sum(face.calc_area() for face in part.faces)
    for vertex in part.verts: vertex.co -= hinge
    mesh = bpy.data.meshes.new(name + ' source-derived mesh')
    part.to_mesh(mesh); part.free(); mesh.update()
    if not mesh.uv_layers.active or len(mesh.uv_layers.active.data) != len(mesh.loops):
        raise RuntimeError('Part lost its per-loop atlas UVs: ' + name)
    for slot in original.data.materials: mesh.materials.append(slot)
    obj = bpy.data.objects.new(name, mesh); collection.objects.link(obj); obj.location = hinge
    objects.append(obj)
bm.free()
if not math.isclose(area_before, total_area, rel_tol=2e-5, abs_tol=1e-4):
    raise RuntimeError('Closed split did not preserve total surface area')

# A review-only three-second opening: each source-derived leaf rotates about
# its provisional outer hinge away from the camera-facing front. This lives
# in the editable .blend; the FBX remains a static three-part export.
bpy.context.scene.render.fps = 24
for leaf, degrees in ((objects[1], 80), (objects[2], -80)):
    leaf.rotation_mode = 'XYZ'
    leaf.rotation_euler.z = 0
    leaf.keyframe_insert(data_path='rotation_euler', index=2, frame=1)
    leaf.rotation_euler.z = math.radians(degrees)
    leaf.keyframe_insert(data_path='rotation_euler', index=2, frame=73)
bpy.context.scene.frame_set(1)

# A second, non-default review variant retains the generated front and back
# surfaces. They are disconnected in this source; translating the intact back
# avoids collapsing its ornament into a plane. A few source triangles on the
# front wood point into unrelated atlas islands. Repair only those planar,
# large outliers using a fit to neighbouring intact wooden faces.
thin_collection = bpy.data.collections.new('PROTOTYPE - thin leaf alternative')
bpy.context.scene.collection.children.link(thin_collection)
thin_objects = []
rear_shift = 2.8
uv_coefficients = (
    ((.0168578873, -.0000304534), (-.0000597289, -.0169111336), (.3350465271, .8638267676)),
    ((.0168607593, .0004335815), (.0003426450, -.0169427375), (.2250884295, .9899461657)),
)
repair_details = []
for original_leaf, name, coefficients, expected_faces in (
    (objects[1], 'Left thin leaf', uv_coefficients[0], 31),
    (objects[2], 'Right thin leaf', uv_coefficients[1], 33),
):
    reference_uv = [tuple(item.uv) for item in original_leaf.data.uv_layers.active.data]
    leaf = original_leaf.copy(); leaf.data = original_leaf.data.copy(); leaf.name = name
    thin_collection.objects.link(leaf)
    # Mesh.copy can retain shared UV CustomData until an explicit layer is
    # rebuilt. Detach it before any UV edits so the full-depth reference and
    # other copies cannot silently inherit the thin variant's changes.
    if len(leaf.data.uv_layers) != 1:
        raise RuntimeError('Expected one source UV layer on ' + name)
    source_uv = leaf.data.uv_layers.active
    uv_name = source_uv.name
    uv_values = [tuple(item.uv) for item in source_uv.data]
    leaf.data.uv_layers.remove(source_uv)
    detached_uv = leaf.data.uv_layers.new(name=uv_name)
    for item, coordinate in zip(detached_uv.data, uv_values): item.uv = coordinate
    for face in leaf.data.polygons:
        ys = [leaf.data.vertices[index].co.y + leaf.location.y for index in face.vertices]
        if min(ys) < 0 < max(ys): raise RuntimeError('Front/back leaf surfaces are no longer disconnected')
    for vertex in leaf.data.vertices:
        world_y = vertex.co.y + leaf.location.y
        if world_y > 0: vertex.co.y -= rear_shift
    leaf.data.update()
    uv = leaf.data.uv_layers.active.data
    repaired = []
    for face in leaf.data.polygons:
        position = face.center + leaf.location
        if not (-1.64 < position.y < -1.54 and face.normal.y < -.95 and face.area > .01): continue
        expected, errors = [], []
        for loop_index in face.loop_indices:
            point = leaf.data.vertices[leaf.data.loops[loop_index].vertex_index].co + leaf.location
            u = coefficients[0][0] * point.x + coefficients[1][0] * point.z + coefficients[2][0]
            v = coefficients[0][1] * point.x + coefficients[1][1] * point.z + coefficients[2][1]
            expected.append((u, v))
            errors.append(math.hypot(uv[loop_index].uv.x-u, uv[loop_index].uv.y-v))
        if min(errors) <= .02: continue
        for loop_index, coordinate in zip(face.loop_indices, expected): uv[loop_index].uv = coordinate
        repaired.append(face.index)
    if len(repaired) != expected_faces:
        raise RuntimeError('Unexpected planar UV repair count: ' + name + ' ' + str(len(repaired)))
    # Three source-derived front triangles sample a yellow-green speck in the
    # original atlas beside a stud. Move only their V coordinates to nearby
    # wood; keep the source image and all other atlas coordinates unchanged.
    spot_faces = (127, 133, 2976) if name == 'Left thin leaf' else ()
    if spot_faces and (not {127, 133}.issubset(repaired) or 2976 in repaired):
        raise RuntimeError('Unexpected source UV layout around gate stud')
    for face_index in spot_faces:
        face = leaf.data.polygons[face_index]
        centre = face.center + leaf.location
        if not (-3.0 < centre.x < -2.5 and 3.7 < centre.z < 4.2 and face.normal.y < -.95):
            raise RuntimeError('Gate stud UV patch no longer matches source geometry')
        for loop_index in face.loop_indices: uv[loop_index].uv.y += .005
    repair_details.append(dict(name=name, uvFaces=repaired, expectedFaces=expected_faces,
                               coefficients=coefficients, rearTranslation=rear_shift,
                               spotFaces=list(spot_faces), spotVOffset=.005))
    repair_details[-1]['edgeFinish'] = finish_edges(leaf, half_width, bottom, top, coefficients)
    # Move the provisional pivot towards the rear surface without moving the
    # closed leaf. The original cut pivot places the finished edge inside the
    # post throughout the swing; sampled CPU intersection checks found this
    # smaller offset clears the mid-height seam at the tested opening angles.
    # Keep it review-only until the in-game camera can inspect the hinge.
    hinge_offset = .075
    closed_positions = [vertex.co.copy() + leaf.location for vertex in leaf.data.vertices]
    leaf.data.transform(Matrix.Translation((0, -hinge_offset, 0)))
    leaf.location.y += hinge_offset
    leaf.data.update()
    closed_error = max((vertex.co + leaf.location - original).length
                       for vertex, original in zip(leaf.data.vertices, closed_positions))
    if closed_error > 1e-5: raise RuntimeError('Pivot adjustment moved the closed leaf: ' + name)
    repair_details[-1]['hingeDepthOffset'] = hinge_offset
    repair_details[-1]['closedPivotAdjustmentMaxError'] = closed_error
    reference_error = max(math.hypot(item.uv.x-u, item.uv.y-v)
                          for item, (u, v) in zip(original_leaf.data.uv_layers.active.data, reference_uv))
    if reference_error > 1e-7: raise RuntimeError('Thin repair changed full-depth reference UVs: ' + name)
    repair_details[-1]['referenceUvMaxDifference'] = reference_error
    thin_objects.append(leaf)
thin_collection.hide_viewport = True
thin_collection.hide_render = True

# Keep the entire authoring source in the packed .blend, but show only the
# editable prototype. Original and game LOD collections remain unmodified.
for col in bpy.data.collections:
    if col != collection and col.name in ('Complete unchanged webapp original', 'Editable source-derived game LODs'):
        col.hide_viewport = True; col.hide_render = True
        for obj in col.objects: obj.hide_set(True)
bpy.ops.object.select_all(action='DESELECT')
for obj in objects: obj.select_set(True)
bpy.context.view_layer.objects.active = objects[0]
text = bpy.data.texts.get('SOURCE - prepare_openable_gate.py') or bpy.data.texts.new('SOURCE - prepare_openable_gate.py')
text.clear(); text.write(Path(__file__).read_text(encoding='utf-8'))
bpy.ops.wm.save_as_mainfile(filepath=str(editable), compress=True)
bpy.ops.export_scene.fbx(filepath=str(export), use_selection=True, object_types={'MESH'},
    axis_forward='-Z', axis_up='Y', bake_anim=False, add_leaf_bones=False,
    path_mode='COPY', embed_textures=True, use_mesh_modifiers=True)
# The Unity review candidate uses the same source-derived material in the
# project and therefore does not duplicate the original packed texture maps.
thin_collection.hide_viewport = False
bpy.ops.object.select_all(action='DESELECT')
export_material = bpy.data.materials.new('CE_temple-gate_review_export')
for obj in (objects[0], *thin_objects):
    obj.data.materials.clear(); obj.data.materials.append(export_material)
    obj.select_set(True)
bpy.context.view_layer.objects.active = objects[0]
bpy.ops.export_scene.fbx(filepath=str(thin_export), use_selection=True, object_types={'MESH'},
    axis_forward='-Z', axis_up='Y', bake_anim=False, add_leaf_bones=False,
    path_mode='STRIP', use_mesh_modifiers=True)
if sha(source) != source_hash: raise RuntimeError('Original authoring file changed')
report = dict(scope='Optional Unity review asset; CPU asset preview does not certify game rendering',
    source='SourceArt/CorridorEntrance/temple-gate/temple-gate.blend', sourceSha256=source_hash,
    editable='gate-opening.blend', editableSha256=sha(editable), export='gate-opening.fbx', exportSha256=sha(export),
    thinExport='gate-opening-thin.fbx', thinExportSha256=sha(thin_export),
    halfWidth=half_width, bottom=bottom, top=top, halfDepth=half_depth,
    areaBefore=area_before, areaAfter=total_area, triangles=counts,
    parts=[dict(name=o.name, hinge=list(o.location), vertices=len(o.data.vertices),
                triangles=len(o.data.polygons), uvLoops=len(o.data.uv_layers.active.data)) for o in objects],
    prototypeAnimation='Editable blend only: frames 1–73 at 24 fps; static FBX has no baked animation',
    thinAlternative='Hidden blend collection translates the intact back by -2.8, repairs planar wood UV outliers and bridges cut edges; static FBX has no baked animation',
    thinRepair=repair_details,
    limitations=['cut bounds and hinge pivots are provisional', 'edge finishing and opening require Unity camera review',
                 'device audio and player performance remain unverified'])
report_path.write_text(json.dumps(report, indent=2) + '\n', encoding='utf-8', newline='\n')
print('GATE_PARTITION_PROTOTYPE_PASS', json.dumps(report['triangles']), flush=True)
