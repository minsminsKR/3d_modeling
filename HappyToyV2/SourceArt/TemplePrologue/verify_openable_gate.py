"""Independently check the closed split-gate prototype against its source.

Blender 4.0.2, background, CPU. Surface/UV samples do not certify an open gate,
hinge position, interior thickness, game rendering, collision or performance.
"""
import hashlib
import json
import math
from pathlib import Path

import bpy
from mathutils import Vector
from mathutils.bvhtree import BVHTree
from mathutils.geometry import barycentric_transform

here = Path(__file__).resolve().parent
root = here / 'gate-opening'
report = json.loads((root / 'geometry-report.json').read_text(encoding='utf-8'))
source = here.parent / 'CorridorEntrance/temple-gate/temple-gate.blend'
sha = lambda path: hashlib.sha256(path.read_bytes()).hexdigest()
for path, expected in ((source, report['sourceSha256']), (root/'gate-opening.blend', report['editableSha256']),
                       (root/'gate-opening.fbx', report['exportSha256']),
                       (root/'gate-opening-thin.fbx', report['thinExportSha256'])):
    if sha(path) != expected: raise RuntimeError('Source or export hash differs: ' + str(path))
bpy.ops.wm.open_mainfile(filepath=str(root/'gate-opening.blend'))
bpy.context.scene.frame_set(1)
original = bpy.data.objects.get('CE_temple-gate_LOD0')
if not original: raise RuntimeError('Preserved original gate mesh is absent')
mesh = original.data
mesh.calc_loop_triangles()
points = [original.matrix_world @ v.co for v in mesh.vertices]
triangles = [list(tri.vertices) for tri in mesh.loop_triangles]
tree = BVHTree.FromPolygons(points, triangles, all_triangles=True)
uvs = mesh.uv_layers.active.data
distances, uv_differences, totals = [], [], {}
for name in ('Gate frame', 'Left gate leaf', 'Right gate leaf'):
    obj = bpy.data.objects.get(name)
    if not obj or obj.type != 'MESH': raise RuntimeError('Missing prototype part ' + name)
    part = obj.data; part.calc_loop_triangles()
    atlas = part.uv_layers.active
    if not atlas or len(atlas.data) != len(part.loops): raise RuntimeError('Missing UV atlas on ' + name)
    stride = max(1, len(part.loop_triangles) // 1500)
    totals[name] = dict(triangles=len(part.loop_triangles), vertices=len(part.vertices))
    if name != 'Gate frame':
        depth = [(obj.matrix_world @ v.co).y for v in part.vertices]
        # Temple uses a 5.5 m fitted gate; the original is 10.772 m high.
        totals[name]['depthAtGameScaleMetres'] = (max(depth)-min(depth))*5.5/10.771793316322146
    for triangle_index in range(0, len(part.loop_triangles), stride):
        triangle = part.loop_triangles[triangle_index]
        coords = [obj.matrix_world @ part.vertices[i].co for i in triangle.vertices]
        centre = (coords[0] + coords[1] + coords[2]) / 3
        hit, normal, index, distance = tree.find_nearest(centre)
        if index is None or not math.isfinite(distance): raise RuntimeError('Invalid nearest-source sample')
        distances.append(distance)
        original_tri = mesh.loop_triangles[index]
        a, b, c = (points[i] for i in original_tri.vertices)
        ta, tb, tc = (Vector((*uvs[i].uv, 0)) for i in original_tri.loops)
        expected_uv = barycentric_transform(hit, a, b, c, ta, tb, tc)
        actual_uv = sum((Vector((*atlas.data[i].uv, 0)) for i in triangle.loops), Vector()) / 3
        uv_differences.append((expected_uv - actual_uv).length)
if len(distances) < 1000: raise RuntimeError('Too few independent surface samples')
distances.sort(); uv_differences.sort()
percentile = lambda values, p: values[math.ceil((len(values)-1)*p)]
result = dict(status='PASS', scope='Closed surface/UV sampling only; open appearance and pivot quality unreviewed',
    samples=len(distances), surfaceDistanceP99=percentile(distances,.99), surfaceDistanceMax=max(distances),
    uvDifferenceP95=percentile(uv_differences,.95), uvDifferenceP99=percentile(uv_differences,.99), parts=totals)
thin_collection = bpy.data.collections.get('PROTOTYPE - thin leaf alternative')
if not thin_collection: raise RuntimeError('Thin leaf alternative missing')
thin_collection.hide_viewport = False
bpy.context.scene.frame_set(1)
result['thinLeaves'] = {}
repairs = {item['name']: item for item in report['thinRepair']}

def edge_ray_counts(tree, outer):
    hits_by_edge = {}
    for axis, plane, lo, hi in ((0, 0, report['bottom'], report['top']),
                                 (0, outer, report['bottom'], report['top']),
                                 (2, report['bottom'], min(outer, 0), max(outer, 0)),
                                 (2, report['top'], min(outer, 0), max(outer, 0))):
        hits = 0
        for n in range(1, 40):
            along = lo+(hi-lo)*n/40
            origin = Vector((along, -1.44, along))
            origin[axis] = plane-.1
            direction = Vector((0, 0, 0)); direction[axis] = 1
            if tree.ray_cast(origin, direction, .2)[2] is not None: hits += 1
        hits_by_edge[str(axis)+':'+str(plane)] = hits
    return hits_by_edge

for thick_name, thin_name in (('Left gate leaf', 'Left thin leaf'), ('Right gate leaf', 'Right thin leaf')):
    thick, thin = bpy.data.objects[thick_name], bpy.data.objects[thin_name]
    if len(thick.data.vertices) >= len(thin.data.vertices) or len(thick.data.loops) >= len(thin.data.loops):
        raise RuntimeError('Thin alternative has no added edge finishing')
    differences, rear_errors = [], []
    ys = []
    front_count = 0
    for old, new in zip(thick.data.vertices, thin.data.vertices[:len(thick.data.vertices)]):
        old_world = thick.matrix_world @ old.co; new_world = thin.matrix_world @ new.co
        if old_world.y < 0:
            front_count += 1; differences.append((old_world-new_world).length)
        else:
            rear_errors.append((new_world - (old_world + Vector((0, -2.8, 0)))).length)
        ys.append(new_world.y)
    old_uv = thick.data.uv_layers.active.data; new_uv = thin.data.uv_layers.active.data
    repair = repairs[thin_name]
    pivot_delta = thin.location - thick.location
    if (abs(pivot_delta.x) > 1e-5 or abs(pivot_delta.z) > 1e-5 or
        abs(pivot_delta.y - repair['hingeDepthOffset']) > 1e-5 or
        abs(repair['hingeDepthOffset'] - .075) > 1e-6):
        raise RuntimeError('Thin gate hinge offset differs from the reviewed candidate: ' + thin_name)
    faces = set(repair['uvFaces'])
    spot_faces = set(repair['spotFaces'])
    if len(faces) != repair['expectedFaces']: raise RuntimeError('UV repair face list is inconsistent')
    if spot_faces != ({127, 133, 2976} if thin_name.startswith('Left') else set()):
        raise RuntimeError('Unexpected gate stud UV patch')
    changed_loops = {i for face in thin.data.polygons if face.index in faces | spot_faces for i in face.loop_indices}
    unchanged_uv = max((old_uv[i].uv-new_uv[i].uv).length for i in range(len(old_uv)) if i not in changed_loops)
    coefficients = repair['coefficients']; corrected_errors = []
    for face in thin.data.polygons[:len(thick.data.polygons)]:
        if face.index not in faces: continue
        for i in face.loop_indices:
            point = thick.matrix_world @ thick.data.vertices[thick.data.loops[i].vertex_index].co
            expected = Vector((coefficients[0][0]*point.x + coefficients[1][0]*point.z + coefficients[2][0],
                               coefficients[0][1]*point.x + coefficients[1][1]*point.z + coefficients[2][1]))
            if face.index in spot_faces: expected.y += repair['spotVOffset']
            corrected_errors.append((new_uv[i].uv-expected).length)
    spot_errors = []
    reference_spot_errors = []
    for face in thin.data.polygons[:len(thick.data.polygons)]:
        if face.index not in spot_faces: continue
        if face.index not in faces:
            for i in face.loop_indices:
                spot_errors.append((new_uv[i].uv - (old_uv[i].uv + Vector((0, repair['spotVOffset'])))).length)
        old_face = thick.data.polygons[face.index]
        centre = sum((thick.matrix_world @ thick.data.vertices[i].co for i in old_face.vertices), Vector()) / len(old_face.vertices)
        hit, _, index, distance = tree.find_nearest(centre)
        if index is None or distance > .001: raise RuntimeError('Reference spot no longer matches source surface')
        source_tri = mesh.loop_triangles[index]
        a, b, c = (points[i] for i in source_tri.vertices)
        ta, tb, tc = (Vector((*uvs[i].uv, 0)) for i in source_tri.loops)
        expected_uv = barycentric_transform(hit, a, b, c, ta, tb, tc)
        actual_uv = sum((Vector((*old_uv[i].uv, 0)) for i in old_face.loop_indices), Vector()) / len(old_face.loop_indices)
        reference_spot_errors.append((expected_uv-actual_uv).length)
    depth = (max(ys)-min(ys))*5.5/10.771793316322146
    thin.data.calc_loop_triangles()
    degenerate = sum(1 for tri in thin.data.loop_triangles
        if (thin.data.vertices[tri.vertices[1]].co-thin.data.vertices[tri.vertices[0]].co).cross(
            thin.data.vertices[tri.vertices[2]].co-thin.data.vertices[tri.vertices[0]].co).length < 1e-8)
    result['thinLeaves'][thin_name] = dict(depthAtGameScaleMetres=depth, frontVertices=front_count,
        frontMaxDisplacement=max(differences), rearVertices=len(rear_errors), rearTranslationMaxError=max(rear_errors),
        repairedFaces=len(faces), hingeDepthOffset=pivot_delta.y, unchangedMaxUvDifference=unchanged_uv,
        repairedMaxUvError=max(corrected_errors), spotFaces=len(spot_faces),
        spotMaxUvError=max(spot_errors, default=0),
        referenceSpotSourceUvMaxError=max(reference_spot_errors, default=0), degenerateTriangles=degenerate)
    caps = list(thin.data.polygons[len(thick.data.polygons):])
    cap_tree = BVHTree.FromPolygons([thin.matrix_world @ v.co for v in thin.data.vertices],
                                   [list(face.vertices) for face in caps])
    outer = -report['halfWidth'] if thin_name.startswith('Left') else report['halfWidth']
    edge_hits = edge_ray_counts(cap_tree, outer)
    if any(hits != 39 for hits in edge_hits.values()): result['status'] = 'FAIL'
    result['thinLeaves'][thin_name]['edgeCapFaces'] = len(caps)
    result['thinLeaves'][thin_name]['edgeCapRayHits'] = edge_hits
    if (front_count < 100 or not rear_errors or max(differences) > 1e-5 or max(rear_errors) > 1e-5 or
        unchanged_uv > 1e-6 or max(corrected_errors) > 1e-6 or max(spot_errors, default=0) > 1e-6 or
        max(reference_spot_errors, default=0) > .001 or depth > .51):
        result['status'] = 'FAIL'
bpy.context.scene.frame_set(73)
left = bpy.data.objects['Left gate leaf']
right = bpy.data.objects['Right gate leaf']
result['openingDegrees'] = [math.degrees(left.rotation_euler.z), math.degrees(right.rotation_euler.z)]
result['openingFps'] = bpy.context.scene.render.fps
if abs(result['openingDegrees'][0]-80) > .01 or abs(result['openingDegrees'][1]+80) > .01 or result['openingFps'] != 24:
    result['status'] = 'FAIL'
result['thinOpeningDegrees'] = [math.degrees(bpy.data.objects['Left thin leaf'].rotation_euler.z),
                                math.degrees(bpy.data.objects['Right thin leaf'].rotation_euler.z)]
if abs(result['thinOpeningDegrees'][0]-80) > .01 or abs(result['thinOpeningDegrees'][1]+80) > .01:
    result['status'] = 'FAIL'
# Geometric sight rays through the centre must hit the closed leaves and be
# unobstructed at the final review pose. This is not character collision or a
# substitute for inspecting cut edges and surfaces from the actual camera.
ray_results = {}
for frame in (1, 73):
    bpy.context.scene.frame_set(frame)
    for variant, names in (
        ('full', ('Gate frame', 'Left gate leaf', 'Right gate leaf')),
        ('thin', ('Gate frame', 'Left thin leaf', 'Right thin leaf'))):
        trees = []
        for name in names:
            obj = bpy.data.objects[name]; part = obj.data; part.calc_loop_triangles()
            vertices = [obj.matrix_world @ vertex.co for vertex in part.vertices]
            faces = [list(tri.vertices) for tri in part.loop_triangles]
            trees.append(BVHTree.FromPolygons(vertices, faces, all_triangles=True))
        blocked = 0
        for x in (-1.2, -.6, 0, .6, 1.2):
            origin = Vector((x, -8, 3.2)); direction = Vector((0, 1, 0))
            if any(tree.ray_cast(origin, direction, 16)[2] is not None for tree in trees):
                blocked += 1
        ray_results[str(frame)+'-'+variant] = blocked
        if blocked != (5 if frame == 1 else 0): result['status'] = 'FAIL'
result['centreRaysBlocked'] = ray_results
bpy.context.scene.frame_set(1)
if result['surfaceDistanceP99'] > .0001 or result['surfaceDistanceMax'] > .001 or result['uvDifferenceP95'] > .005:
    result['status'] = 'FAIL'
# The standalone FBX must also carry the three visual meshes and their UVs.
# Blender's FBX exporter can discard degenerate faces, so validate surface area
# rather than claiming byte- or triangle-count identity after reimport.
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=str(root/'gate-opening.fbx'))
imported = {obj.name: obj for obj in bpy.data.objects if obj.type == 'MESH'}
fbx_area = 0
fbx_parts = {}
for name in ('Gate frame', 'Left gate leaf', 'Right gate leaf'):
    obj = imported.get(name)
    if not obj or not obj.data.uv_layers.active:
        raise RuntimeError('Static FBX lost a gate part or UVs: ' + name)
    mesh = obj.data; mesh.calc_loop_triangles()
    if len(mesh.uv_layers.active.data) != len(mesh.loops) or len(mesh.materials) != 1:
        raise RuntimeError('Static FBX lost atlas loops or material: ' + name)
    for tri in mesh.loop_triangles:
        a, b, c = (obj.matrix_world @ mesh.vertices[index].co for index in tri.vertices)
        fbx_area += (b-a).cross(c-a).length * .5
    fbx_parts[name] = dict(vertices=len(mesh.vertices), triangles=len(mesh.loop_triangles),
                           material=mesh.materials[0].name)
result['fbxParts'] = fbx_parts
result['fbxSurfaceArea'] = fbx_area
material = imported['Gate frame'].data.materials[0]
images = [node.image for node in material.node_tree.nodes if node.type == 'TEX_IMAGE' and node.image] if material.use_nodes else []
result['fbxPackedImages'] = sum(1 for image in images if image.packed_file)
if result['fbxPackedImages'] < 1:
    result['status'] = 'FAIL'
if not math.isclose(fbx_area, report['areaBefore'], rel_tol=2e-5, abs_tol=.001):
    result['status'] = 'FAIL'
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=str(root/'gate-opening-thin.fbx'))
thin_import = {obj.name: obj for obj in bpy.data.objects if obj.type == 'MESH'}
result['thinFbxParts'] = {}
for name in ('Gate frame', 'Left thin leaf', 'Right thin leaf'):
    obj = thin_import.get(name)
    if not obj or not obj.data.uv_layers.active or len(obj.data.materials) != 1:
        raise RuntimeError('Unity review FBX lost a part, atlas or material: ' + name)
    mesh = obj.data
    if len(mesh.uv_layers.active.data) != len(mesh.loops):
        raise RuntimeError('Unity review FBX lost atlas loops: ' + name)
    result['thinFbxParts'][name] = dict(vertices=len(mesh.vertices), faces=len(mesh.polygons),
                                         material=mesh.materials[0].name)
    if name != 'Gate frame':
        thick_name = name.replace('thin', 'gate')
        expected_faces = totals[thick_name]['triangles'] + result['thinLeaves'][name]['edgeCapFaces']
        # The exporter can drop a few degenerate original triangles; it must
        # still retain the added edge surfaces in the actual review FBX.
        if len(mesh.polygons) < expected_faces - 25:
            raise RuntimeError('Review FBX dropped edge finishing: ' + name)
        mesh.calc_loop_triangles()
        tree = BVHTree.FromPolygons([obj.matrix_world @ vertex.co for vertex in mesh.vertices],
                                    [list(tri.vertices) for tri in mesh.loop_triangles], all_triangles=True)
        outer = -report['halfWidth'] if name.startswith('Left') else report['halfWidth']
        edge_hits = edge_ray_counts(tree, outer)
        result['thinFbxParts'][name]['edgeRayHits'] = edge_hits
        if any(hits != 39 for hits in edge_hits.values()): result['status'] = 'FAIL'
out = root/'surface-proof.json'
out.write_text(json.dumps(result, indent=2)+'\n',encoding='utf-8',newline='\n')
print('GATE_SURFACE_PROOF', json.dumps(result), flush=True)
if result['status'] != 'PASS': raise RuntimeError('Closed prototype differs from its source')
