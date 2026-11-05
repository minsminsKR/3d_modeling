"""Make an outer-forest fern LOD from the preserved fern-clump source.

Run with Blender 4.0.2 and fast-simplification 0.1.12 (Python 3.10).
The authored fern-clump.blend is read only. A separate editable blend, FBX
files and report are written beside it; no Unity assets are modified.
"""
import hashlib
import json
from pathlib import Path

import bpy
import fast_simplification
import numpy as np
from mathutils import Vector
from mathutils.bvhtree import BVHTree
from mathutils.geometry import barycentric_transform, closest_point_on_tri

ROOT = Path(__file__).resolve().parent
SOURCE = ROOT / 'fern-clump.blend'
BLEND = ROOT / 'fern-undergrowth.blend'
REPORT = ROOT / 'undergrowth-export.json'
TARGETS = (16000,)
sha = lambda path: hashlib.sha256(path.read_bytes()).hexdigest()

provenance = json.loads((ROOT / 'export.json').read_text(encoding='utf8'))
if sha(ROOT / 'original.glb') != provenance['originalSha256']:
    raise RuntimeError('The generated fern original no longer matches its record')
bpy.ops.wm.open_mainfile(filepath=str(SOURCE))
bpy.context.preferences.filepaths.save_version = 0
near = bpy.data.objects['CE_fern-clump_LOD0']
mesh = near.data
mesh.calc_loop_triangles()
if not mesh.uv_layers.active:
    raise RuntimeError('The authored fern has no active UV atlas')
vertices = np.empty(len(mesh.vertices) * 3, dtype=np.float32)
mesh.vertices.foreach_get('co', vertices)
vertices = vertices.reshape(-1, 3)
faces = np.empty(len(mesh.loop_triangles) * 3, dtype=np.int32)
mesh.loop_triangles.foreach_get('vertices', faces)
faces = faces.reshape(-1, 3)
source_vertices = [Vector(v) for v in vertices]
tree = BVHTree.FromPolygons(source_vertices, faces.tolist(), all_triangles=True)
atlas = mesh.uv_layers.active.data
if not np.isfinite(vertices).all():
    raise RuntimeError('Source fern geometry is not finite')


def create_lod(index, target):
    reduced, triangles = fast_simplification.simplify(vertices, faces, target_count=target, agg=5)
    # Some open fronds stall the first pass above target; simplifying its
    # resulting topology once more succeeds without deleting entire fronds.
    first_pass = len(triangles)
    if first_pass > target * 1.05:
        reduced, triangles = fast_simplification.simplify(reduced, triangles, target_count=target, agg=5)
    if not 300 <= len(triangles) <= target * 1.05 or not np.isfinite(reduced).all():
        raise RuntimeError('Undergrowth simplification missed budget %d: %d triangles' % (target, len(triangles)))
    uv = np.empty((len(triangles) * 3, 2), dtype=np.float32)
    projection = []
    for i, face in enumerate(triangles):
        points = [Vector(reduced[j]) for j in face]
        _, _, source_index, distance = tree.find_nearest(sum(points, Vector()) / 3)
        if source_index is None:
            raise RuntimeError('No fern surface for a reduced face')
        tri = mesh.loop_triangles[source_index]
        a, b, c = (source_vertices[j] for j in tri.vertices)
        ta, tb, tc = (Vector((*atlas[j].uv, 0)) for j in tri.loops)
        projection.append(distance)
        for corner, point in enumerate(points):
            mapped = barycentric_transform(closest_point_on_tri(point, a, b, c), a, b, c, ta, tb, tc)
            uv[3 * i + corner] = (mapped.x, mapped.y)
    if not np.isfinite(uv).all():
        raise RuntimeError('Projected undergrowth atlas contains invalid coordinates')

    normal_mesh = bpy.data.meshes.new('Temporary undergrowth normals')
    normal_mesh.from_pydata(reduced.tolist(), [], triangles.tolist())
    normal_mesh.update()
    for polygon in normal_mesh.polygons:
        polygon.use_smooth = True
    normal_mesh.calc_normals_split()
    normals = np.empty(len(normal_mesh.loops) * 3, dtype=np.float32)
    normal_mesh.loops.foreach_get('normal', normals)
    normals = normals.reshape(-1, 3)

    # FBX can merge coincident points with different atlas values. Remove
    # only completely identical faces and separate the remaining overlaps.
    seen, keep = set(), []
    for i, face in enumerate(triangles):
        corners = tuple(tuple(reduced[v]) + tuple(uv[3*i+j]) + tuple(normals[3*i+j])
                        for j, v in enumerate(face))
        signature = min(corners, corners[1:] + corners[:1], corners[2:] + corners[:2])
        if signature not in seen:
            seen.add(signature)
            keep.append(i)
    before = len(triangles)
    triangles = triangles[keep]
    uv = uv.reshape(-1, 3, 2)[keep].reshape(-1, 2)
    normals = normals.reshape(-1, 3, 3)[keep].reshape(-1, 3)
    split, positions, seen, isolated = triangles.copy(), reduced.tolist(), set(), 0
    for i, face in enumerate(triangles):
        key = tuple(sorted(int(v) for v in face))
        if key in seen:
            isolated += 1
            start = len(positions)
            positions.extend(reduced[face].tolist())
            split[i] = (start, start + 1, start + 2)
        else:
            seen.add(key)

    data = bpy.data.meshes.new('Fern undergrowth source-projected LOD ' + str(index))
    data.from_pydata(positions, [], split.tolist())
    data.update()
    data.uv_layers.new(name='Projected source atlas').data.foreach_set('uv', uv.ravel())
    for polygon in data.polygons:
        polygon.use_smooth = True
    data.use_auto_smooth = True
    data.normals_split_custom_set(normals.tolist())
    data.calc_normals_split()
    encoded = [loop.normal.copy() for loop in data.loops]
    invalid = {i for i, normal in enumerate(encoded) if normal.length < 1e-6}
    for polygon in data.polygons:
        for loop_index in polygon.loop_indices:
            if loop_index in invalid:
                if polygon.normal.length < 1e-6:
                    raise RuntimeError('Degenerate face cannot supply a normal')
                encoded[loop_index] = polygon.normal.copy()
    if invalid:
        data.normals_split_custom_set(encoded)
        data.calc_normals_split()
    if any(loop.normal.length < 1e-6 for loop in data.loops):
        raise RuntimeError('Zero-length normal remains')
    for material in mesh.materials:
        data.materials.append(material)
    obj = bpy.data.objects.new('Fern undergrowth LOD' + str(index), data)
    bpy.context.scene.collection.objects.link(obj)
    obj.matrix_world = near.matrix_world.copy()
    bpy.ops.object.select_all(action='DESELECT')
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    output = ROOT / 'game-export' / ('fern-undergrowth-lod' + str(index) + '.fbx')
    bpy.ops.export_scene.fbx(filepath=str(output), use_selection=True, object_types={'MESH'},
        axis_forward='-Z', axis_up='Y', bake_anim=False, add_leaf_bones=False,
        path_mode='STRIP', use_mesh_modifiers=True)
    bpy.data.meshes.remove(normal_mesh)
    return obj, dict(file=output.name, targetTriangles=target, triangles=len(triangles),
        vertices=len(positions), sha256=sha(output), firstPassTriangles=first_pass, identicalFacesRemoved=before-len(triangles),
        coincidentFacesIsolated=isolated, zeroNormalsRepaired=len(invalid),
        projectedCentroidDistanceP95=float(np.percentile(projection, 95)))


created, entries = [], []
for index, target in enumerate(TARGETS, start=1):
    obj, entry = create_lod(index, target)
    created.append(obj)
    entries.append(entry)
for obj in list(bpy.data.objects):
    if obj not in created:
        bpy.data.objects.remove(obj, do_unlink=True)
for text in list(bpy.data.texts):
    bpy.data.texts.remove(text)
embedded = bpy.data.texts.new('SOURCE - prepare_fern_undergrowth.py')
embedded.write(Path(__file__).read_text(encoding='utf8'))
bpy.ops.outliner.orphans_purge(do_local_ids=True, do_linked_ids=True, do_recursive=True)
bpy.ops.wm.save_as_mainfile(filepath=str(BLEND), compress=True)
report = dict(source='fern-clump.blend / CE_fern-clump_LOD0', sourceSha256=sha(SOURCE),
    originalSha256=provenance['originalSha256'], editable=BLEND.name, editableSha256=sha(BLEND),
    blender=bpy.app.version_string, simplifier='fast-simplification 0.1.12',
    atlas='game-export/fern-clump/CE_fern-clump_0-baseColor.png', lods=entries)
REPORT.write_text(json.dumps(report, indent=2) + '\n', encoding='utf8')
print('TEMPLE_UNDERGROWTH_EXPORT', json.dumps(report), flush=True)
