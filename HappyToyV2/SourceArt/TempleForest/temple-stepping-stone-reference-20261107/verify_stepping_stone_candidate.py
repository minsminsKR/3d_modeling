"""Verify generated stone review candidates without changing the saved source.

Blender 4.0.2, background CPU only. Reimport all FBXs and sample bidirectional
surface distances against the normalized full-resolution source. Numeric samples
are evidence, not visual approval, seamlessness, game performance or proof of
absence of all geometry defects. Writes a new review report, never overwrites it.

blender -b -t 2 --factory-startup --python-exit-code 1 --python <this file>
"""
from pathlib import Path
import hashlib
import json
import math

import bpy
import bmesh
import numpy as np
from mathutils import Matrix, Vector
from mathutils.bvhtree import BVHTree

HERE = Path(__file__).resolve().parent
KEY = HERE.name
ORIGINAL = HERE / 'original.glb'
BLEND = HERE / (KEY + '.blend')
REPORT = HERE / 'export.json'
OUTPUT = HERE / 'game-export'
PROOF = HERE / 'candidate-verification.json'
SAMPLES = 2048
SEED = 873156


def sha(path):
    digest = hashlib.sha256()
    with path.open('rb') as stream:
        for part in iter(lambda: stream.read(1024 * 1024), b''):
            digest.update(part)
    return digest.hexdigest()


def mesh_arrays(obj, transform):
    mesh = obj.data
    mesh.calc_loop_triangles()
    vertices = np.empty(len(mesh.vertices) * 3, dtype=np.float64)
    mesh.vertices.foreach_get('co', vertices)
    vertices = vertices.reshape((-1, 3))
    matrix = np.asarray(transform, dtype=np.float64)
    vertices = vertices @ matrix[:3, :3].T + matrix[:3, 3]
    indices = np.empty(len(mesh.loop_triangles) * 3, dtype=np.int32)
    mesh.loop_triangles.foreach_get('vertices', indices)
    indices = indices.reshape((-1, 3))
    if not len(vertices) or not len(indices) or not np.isfinite(vertices).all():
        raise RuntimeError('Missing or nonfinite mesh vertices: ' + obj.name)
    if indices.min() < 0 or indices.max() >= len(vertices):
        raise RuntimeError('Out-of-range triangle index: ' + obj.name)
    return vertices, indices


def surface_area(vertices, indices):
    p = vertices[indices]
    areas = .5 * np.linalg.norm(np.cross(p[:, 1] - p[:, 0], p[:, 2] - p[:, 0]), axis=1)
    if not np.isfinite(areas).all() or areas.sum() <= 0:
        raise RuntimeError('Invalid surface area')
    return float(areas.sum())


def normal_stats(obj):
    normals = np.empty(len(obj.data.vertices) * 3, dtype=np.float64)
    obj.data.vertices.foreach_get('normal', normals)
    normals = normals.reshape((-1, 3))
    if not len(normals) or not np.isfinite(normals).all():
        raise RuntimeError('Missing or nonfinite vertex normals: ' + obj.name)
    lengths = np.linalg.norm(normals, axis=1)
    return {'count': len(normals), 'minimumLength': float(lengths.min()),
            'zeroLengthCount': int(np.count_nonzero(lengths < 1e-8))}


def sample_surface(vertices, indices, count, seed):
    # Area-weighted barycentric samples, stable for the pinned environment.
    p = vertices[indices]
    areas = .5 * np.linalg.norm(np.cross(p[:, 1] - p[:, 0], p[:, 2] - p[:, 0]), axis=1)
    if not np.isfinite(areas).all() or areas.sum() <= 0:
        raise RuntimeError('Invalid source surface areas')
    rng = np.random.default_rng(seed)
    selected = rng.choice(len(indices), size=count, replace=True, p=areas / areas.sum())
    u = rng.random(count); v = rng.random(count)
    root = np.sqrt(u)
    weights = np.column_stack((1 - root, root * (1 - v), root * v))
    points = (p[selected] * weights[:, :, None]).sum(axis=1)
    return points, int(np.count_nonzero(areas < 1e-12))


def tree_for(obj, transform):
    bm = bmesh.new()
    try:
        bm.from_mesh(obj.data)
        bm.transform(transform)
        bm.normal_update()
        return BVHTree.FromBMesh(bm, epsilon=0.0)
    finally:
        bm.free()


def nearest_stats(points, tree):
    distances = []
    for point in points:
        hit = tree.find_nearest(Vector(point))
        if not hit or hit[3] is None or not math.isfinite(hit[3]):
            raise RuntimeError('Could not find nearest surface point')
        distances.append(hit[3])
    values = np.asarray(distances, dtype=np.float64)
    return {'samples': len(values), 'mean_m': float(values.mean()),
            'p95_m': float(np.percentile(values, 95)),
            'p99_m': float(np.percentile(values, 99)), 'max_m': float(values.max())}


def bounds(points):
    return {'min': points.min(axis=0).tolist(), 'max': points.max(axis=0).tolist(),
            'size': (points.max(axis=0) - points.min(axis=0)).tolist()}


def uv_stats(obj):
    mesh = obj.data
    if not mesh.uv_layers.active:
        raise RuntimeError('Missing active UV layer: ' + obj.name)
    uv = np.empty(len(mesh.loops) * 2, dtype=np.float64)
    mesh.uv_layers.active.data.foreach_get('uv', uv)
    uv = uv.reshape((-1, 2))
    if len(uv) != len(mesh.loops) or not np.isfinite(uv).all():
        raise RuntimeError('Missing or nonfinite UV coordinates: ' + obj.name)
    return {'loops': len(uv), 'min': uv.min(axis=0).tolist(), 'max': uv.max(axis=0).tolist()}


if bpy.app.version[:3] != (4, 0, 2):
    raise RuntimeError('Expected Blender 4.0.2, got ' + bpy.app.version_string)
if PROOF.exists():
    raise RuntimeError('Verification report exists; preserve it and use a fresh copy')
if not all(p.is_file() for p in (ORIGINAL, BLEND, REPORT)):
    raise RuntimeError('Original, editable blend and export report are required')
report = json.loads(REPORT.read_text(encoding='utf-8'))
if sha(ORIGINAL) != report['originalSha256'] or sha(BLEND) != report['editableSha256']:
    raise RuntimeError('Original or editable file digest differs from export record')
if len(report['lods']) != 3:
    raise RuntimeError('Exactly three independent candidate LODs are required')

bpy.ops.wm.open_mainfile(filepath=str(BLEND), load_ui=False)
source_collection = bpy.data.collections.get('Complete unchanged webapp original')
if not source_collection:
    raise RuntimeError('Packed original collection is missing')
originals = [obj for obj in source_collection.objects if obj.type == 'MESH']
if len(originals) != 1:
    raise RuntimeError('Expected exactly one original mesh for this generated stone')
source = originals[0]
corners = [source.matrix_world @ Vector(corner) for corner in source.bound_box]
lo = Vector(tuple(min(p[i] for p in corners) for i in range(3)))
hi = Vector(tuple(max(p[i] for p in corners) for i in range(3)))
if hi.x - lo.x <= .00001:
    raise RuntimeError('Original source width is invalid')
centre = Vector(((lo.x + hi.x) * .5, (lo.y + hi.y) * .5, lo.z))
normalization = Matrix.Scale(1.0 / (hi.x - lo.x), 4) @ Matrix.Translation(-centre)
source_matrix = normalization @ source.matrix_world
source_vertices, source_indices = mesh_arrays(source, source_matrix)
source_points, source_degenerate = sample_surface(source_vertices, source_indices, SAMPLES, SEED)
source_tree = tree_for(source, source_matrix)
if not source_tree:
    raise RuntimeError('Cannot build original surface tree')

proof = {'tool': 'Blender 4.0.2', 'originalSha256': sha(ORIGINAL), 'blendSha256': sha(BLEND),
         'sourceBoundsBlenderZUp': bounds(source_vertices), 'sourceTriangles': len(source_indices),
         'sourceDegenerateTrianglesBelow1e-12Area': source_degenerate, 'samplesPerDirection': SAMPLES,
         'seed': SEED, 'lods': [], 'maps': [],
         'limitations': ('Area-weighted random surface samples cannot prove absence of every error. '
                         'Imported axes and materials require Unity review. No texture appearance, '
                         'mesh contact, repeated-stone appearance, actual draw counts or FPS is certified.')}
if len(source_indices) != report['originalTriangles']:
    raise RuntimeError('Packed original triangle count differs from export report')

for index, entry in enumerate(report['lods']):
    name = 'CE_' + KEY + '_LOD' + str(index)
    obj = bpy.data.objects.get(name)
    if not obj or obj.type != 'MESH':
        raise RuntimeError('Missing review LOD object: ' + name)
    vertices, triangles = mesh_arrays(obj, obj.matrix_world)
    if len(triangles) != entry['triangles'] or len(triangles) > entry['targetTriangles'] + 4:
        raise RuntimeError('Candidate triangle count differs from export report: ' + name)
    points, degenerate = sample_surface(vertices, triangles, SAMPLES, SEED + index + 1)
    tree = tree_for(obj, obj.matrix_world)
    if not tree:
        raise RuntimeError('Cannot build candidate surface tree: ' + name)
    path = OUTPUT / entry['file']
    if not path.is_file() or sha(path) != entry['sha256']:
        raise RuntimeError('Candidate FBX missing or hash differs: ' + str(path))
    item = {'lod': index, 'triangles': len(triangles), 'vertices': len(vertices),
            'boundsBlenderZUp': bounds(vertices), 'uv': uv_stats(obj), 'normals': normal_stats(obj),
            'surfaceArea': surface_area(vertices, triangles),
            'degenerateTrianglesBelow1e-12Area': degenerate,
            'sourceToLod': nearest_stats(source_points, tree),
            'lodToSource': nearest_stats(points, source_tree), 'fbxSha256': sha(path)}
    # Check round-trip in Blender without saving imported objects or modifying the source.
    before = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=str(path), use_anim=False)
    imported = [x for x in bpy.data.objects if x not in before and x.type == 'MESH']
    if len(imported) != 1:
        raise RuntimeError('Expected one mesh on FBX reimport: ' + str(path))
    iv, it = mesh_arrays(imported[0], imported[0].matrix_world)
    area_delta = (surface_area(iv, it) - item['surfaceArea']) / item['surfaceArea']
    bound_delta = max(float(np.max(np.abs(iv.min(axis=0) - vertices.min(axis=0)))),
                      float(np.max(np.abs(iv.max(axis=0) - vertices.max(axis=0)))))
    triangle_loss = len(triangles) - len(it)
    item['fbxReimport'] = {'triangles': len(it), 'vertices': len(iv),
                           'triangleLoss': triangle_loss, 'areaRelativeDifference': area_delta,
                           'maxBoundsDifferenceM': bound_delta, 'normals': normal_stats(imported[0]),
                           'boundsBlenderZUp': bounds(iv), 'uv': uv_stats(imported[0])}
    if abs(triangle_loss) > max(10, round(len(triangles) * .01)) or abs(area_delta) > .01 or bound_delta > .002:
        raise RuntimeError('FBX round-trip changed topology, area, scale or axes: ' + str(path))
    for temporary in set(bpy.data.objects) - before:
        bpy.data.objects.remove(temporary, do_unlink=True)
    proof['lods'].append(item)

for material in report['materials']:
    for channel in ('baseColor', 'metallicSmoothness', 'normal', 'occlusion'):
        filename = material.get(channel)
        if not filename:
            continue
        path = OUTPUT / KEY / filename
        if not path.is_file():
            raise RuntimeError('Missing derived texture: ' + str(path))
        image = bpy.data.images.load(str(path), check_existing=False)
        try:
            if max(image.size) > 2048 or min(image.size) <= 0:
                raise RuntimeError('Unexpected derived map dimensions: ' + str(path))
            proof['maps'].append({'channel': channel, 'file': filename,
                                  'size': list(image.size), 'sha256': sha(path)})
        finally:
            bpy.data.images.remove(image)

PROOF.write_text(json.dumps(proof, indent=2) + '\n', encoding='utf-8', newline='\n')
print('STEPPING_STONE_CANDIDATE_VERIFICATION ' + json.dumps(proof), flush=True)
