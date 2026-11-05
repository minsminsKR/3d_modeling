"""Check source/LOD stone geometry in both directions and reimport FBX.

Blender 4.0.2, CPU only. Sampled mesh checks do not establish visible quality,
tile seam quality, Unity import behavior, collision or hardware performance.
"""
import hashlib
import json
import math
from pathlib import Path

import bpy
from mathutils import Vector
from mathutils.bvhtree import BVHTree

root = Path(__file__).resolve().parent
report = json.loads((root / 'export.json').read_text(encoding='utf8'))
sha = lambda path: hashlib.sha256(path.read_bytes()).hexdigest()
if sha(root / 'original.glb') != report['originalSha256']:
    raise RuntimeError('Generated original differs from the export provenance')
blend = root / 'temple-stone-cluster.blend'
bpy.ops.wm.open_mainfile(filepath=str(blend))
source = bpy.data.objects['geometry_0']
source.data.calc_loop_triangles()
corners = [source.matrix_world @ Vector(corner) for corner in source.bound_box]
low = Vector(tuple(min(point[i] for point in corners) for i in range(3)))
high = Vector(tuple(max(point[i] for point in corners) for i in range(3)))
sx = 1.8 / (high.x - low.x)
sy = 1.8 / (high.y - low.y)
sz = min(sx, .12 / (high.z - low.z))
centre = Vector(((low.x + high.x) / 2, (low.y + high.y) / 2, low.z))


def normalize_source(point):
    point = source.matrix_world @ point - centre
    return Vector((point.x * sx, point.y * sy, point.z * sz))


source_points = [normalize_source(vertex.co) for vertex in source.data.vertices]
source_faces = [tuple(triangle.vertices) for triangle in source.data.loop_triangles]
source_tree = BVHTree.FromPolygons(source_points, source_faces, all_triangles=True)
source_samples = source_points[::211]
source_top = [point for point in source_samples if point.z >= .06]


def distances(tree, points):
    values = sorted(tree.find_nearest(point)[3] for point in points)
    if not values or not all(math.isfinite(value) for value in values):
        raise RuntimeError('Invalid source/LOD surface distance')
    return dict(samples=len(values), p95=values[math.ceil((len(values)-1)*.95)], maximum=values[-1])


def mesh_data(obj):
    mesh = obj.data
    mesh.calc_loop_triangles()
    mesh.calc_normals_split()
    if not mesh.uv_layers.active or len(mesh.uv_layers.active.data) != len(mesh.loops):
        raise RuntimeError('Stone model UV atlas missing')
    if any(not math.isfinite(value) for loop in mesh.uv_layers.active.data for value in loop.uv):
        raise RuntimeError('Non-finite stone UV')
    points = [obj.matrix_world @ vertex.co for vertex in mesh.vertices]
    faces = [tuple(triangle.vertices) for triangle in mesh.loop_triangles]
    if not points or not faces:
        raise RuntimeError('Empty candidate stone model')
    area = sum((points[b]-points[a]).cross(points[c]-points[a]).length*.5 for a,b,c in faces)
    tree = BVHTree.FromPolygons(points, faces, all_triangles=True)
    top_samples = [point for point in points[::11] if point.z >= .06]
    return dict(triangles=len(faces), vertices=len(points), area=area,
        zeroNormalLoops=sum(loop.normal.length < 1e-6 for loop in mesh.loops),
        sourceToCandidate=distances(tree, source_samples), sourceTopToCandidate=distances(tree, source_top),
        candidateToSource=distances(source_tree, points[::11]), candidateTopToSource=distances(source_tree, top_samples)),tree


def projected_top(tree):
    hits = []
    for iz in range(25):
        for ix in range(25):
            x = -.9 + 1.8 * (ix+.5) / 25
            y = -.9 + 1.8 * (iz+.5) / 25
            point, normal, index, distance = tree.ray_cast(Vector((x,y,1)), Vector((0,0,-1)))
            hits.append(None if point is None else float(point.z))
    return hits


source_heights = projected_top(source_tree)
editable = []
for index, entry in enumerate(report['lods']):
    obj = bpy.data.objects['CE_temple-stone-cluster_LOD' + str(index)]
    item, tree = mesh_data(obj)
    if item['triangles'] != entry['triangles']:
        raise RuntimeError('Editable LOD triangle count disagrees with export')
    heights = projected_top(tree)
    lost = sum(before is not None and after is None for before,after in zip(source_heights, heights))
    added = sum(before is None and after is not None for before,after in zip(source_heights, heights))
    height_differences = sorted(abs(before-after) for before,after in zip(source_heights,heights)
                                if before is not None and after is not None)
    item['projection'] = dict(samples=len(heights), sourceHits=sum(value is not None for value in source_heights),
        candidateHits=sum(value is not None for value in heights), sourceOnlyHoles=lost, candidateOnlyHits=added,
        heightDifferenceP95=height_differences[math.ceil((len(height_differences)-1)*.95)])
    editable.append(item)

reimported = []
for index, entry in enumerate(report['lods']):
    path = root / 'game-export' / entry['file']
    if sha(path) != entry['sha256']:
        raise RuntimeError('Candidate FBX hash mismatch')
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(path))
    models = [obj for obj in bpy.data.objects if obj.type == 'MESH']
    if len(models) != 1:
        raise RuntimeError('Expected a single FBX mesh')
    item, _ = mesh_data(models[0])
    item['areaRelativeLoss'] = (editable[index]['area'] - item['area']) / editable[index]['area']
    item['triangleLoss'] = editable[index]['triangles'] - item['triangles']
    if item['zeroNormalLoops'] or abs(item['areaRelativeLoss']) > .01:
        raise RuntimeError('FBX reimport has missing normals or excessive surface loss')
    reimported.append(item)

proof = dict(scope='Sampled source-to-LOD and LOD-to-source, top projection and Blender FBX reimport; not Unity/GPU approval',
    originalSha256=report['originalSha256'], editableSha256=sha(blend),
    exportSha256=[entry['sha256'] for entry in report['lods']], editable=editable, reimported=reimported)
(root / 'surface-proof.json').write_text(json.dumps(proof, indent=2) + '\n', encoding='utf8')
print('TEMPLE_STONE_CLUSTER_PROOF', json.dumps(proof), flush=True)
