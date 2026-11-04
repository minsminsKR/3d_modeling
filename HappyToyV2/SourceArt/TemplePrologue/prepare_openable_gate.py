"""Derive an inspectable three-part gate from the preserved generated mesh.

Blender 4.0.2, background, CPU. This is an authoring prototype: rectangular
cut bounds and pivots MUST be visually reviewed before game integration.
The existing corridor gate and source .blend are never modified.
"""
import hashlib
import json
import math
from pathlib import Path

import bmesh
import bpy
from mathutils import Matrix, Vector

here = Path(__file__).resolve().parent
source = here.parent / 'CorridorEntrance/temple-gate/temple-gate.blend'
output = here / 'gate-opening'
output.mkdir(exist_ok=True)
editable = output / 'gate-opening.blend'
export = output / 'gate-opening.fbx'
thin_export = output / 'gate-opening-thin.fbx'
report_path = output / 'geometry-report.json'
sha = lambda path: hashlib.sha256(path.read_bytes()).hexdigest()
source_hash = sha(source)
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

# A second, non-default review variant retains the front relief and atlas but
# collapses the generated building's deep back toward the front. This provides
# an approximately door-thickness candidate without altering the complete
# closed-surface partition above. It has not been visually accepted.
thin_collection = bpy.data.collections.new('PROTOTYPE - thin leaf alternative')
bpy.context.scene.collection.children.link(thin_collection)
thin_objects = []
back_limit = -1.30
for original_leaf, name in ((objects[1], 'Left thin leaf'), (objects[2], 'Right thin leaf')):
    leaf = original_leaf.copy(); leaf.data = original_leaf.data.copy(); leaf.name = name
    thin_collection.objects.link(leaf)
    for vertex in leaf.data.vertices:
        world_y = vertex.co.y + leaf.location.y
        if world_y > back_limit:
            vertex.co.y = back_limit - leaf.location.y
    leaf.data.update()
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
report = dict(scope='Geometry-only prototype, not visually reviewed or integrated into Unity',
    source='SourceArt/CorridorEntrance/temple-gate/temple-gate.blend', sourceSha256=source_hash,
    editable='gate-opening.blend', editableSha256=sha(editable), export='gate-opening.fbx', exportSha256=sha(export),
    thinExport='gate-opening-thin.fbx', thinExportSha256=sha(thin_export),
    halfWidth=half_width, bottom=bottom, top=top, halfDepth=half_depth,
    areaBefore=area_before, areaAfter=total_area, triangles=counts,
    parts=[dict(name=o.name, hinge=list(o.location), vertices=len(o.data.vertices),
                triangles=len(o.data.polygons), uvLoops=len(o.data.uv_layers.active.data)) for o in objects],
    prototypeAnimation='Editable blend only: frames 1–73 at 24 fps; static FBX has no baked animation',
    thinAlternative='Hidden blend collection clamps back vertices to y=-1.30; front relief and UVs retained; static FBX uses the closed-surface parts',
    limitations=['cut bounds and hinge pivots are provisional', 'thick source depth and open borders need visual review',
                 'no runtime integration, collision or camera validation'])
report_path.write_text(json.dumps(report, indent=2) + '\n', encoding='utf-8', newline='\n')
print('GATE_PARTITION_PROTOTYPE_PASS', json.dumps(report['triangles']), flush=True)
