"""Check review pine hashes, original-source fidelity and standalone FBX.

Blender 4.0.2, background/CPU; this is not game-camera or FPS verification.
"""
import hashlib
import json
import math
from pathlib import Path

import bpy
import numpy as np
from mathutils import Matrix, Vector
from mathutils.bvhtree import BVHTree

here = Path(__file__).resolve().parent
root = here / 'pine-review'
report = json.loads((root / 'report.json').read_text(encoding='utf-8'))
sha = lambda path: hashlib.sha256(path.read_bytes()).hexdigest()
for path, expected in ((here / report['source'], report['sourceSha256']),
                       (here / report['woodAtlas'], report['woodAtlasSha256']),
                       (root / report['editable'], report['editableSha256']),
                       (root / report['export'], report['exportSha256'])):
    if sha(path) != expected: raise RuntimeError('Pine review source hash differs: ' + str(path))
bpy.ops.wm.open_mainfile(filepath=str(root / report['editable']))
original = bpy.data.objects['geometry_0']
candidate = bpy.data.objects['CE_pine_trunk_review']
candidate.data.calc_loop_triangles()
candidate.data.calc_normals_split()
editable_zero_normals = sum(loop.normal.length < 1e-6 for loop in candidate.data.loops)
if (len(candidate.data.loop_triangles) != report['triangles'] or
    not candidate.data.uv_layers.active or len(candidate.data.uv_layers.active.data) != len(candidate.data.loops)):
    raise RuntimeError('Editable review geometry or atlas differs from report')
corners = [original.matrix_world @ Vector(point) for point in original.bound_box]
low = Vector(tuple(min(point[i] for point in corners) for i in range(3)))
high = Vector(tuple(max(point[i] for point in corners) for i in range(3)))
centre = Vector(((low.x + high.x) / 2, (low.y + high.y) / 2, low.z))
normalization = Matrix.Scale(10 / (high.z - low.z), 4) @ Matrix.Translation(-centre) @ original.matrix_world
samples = [normalization @ original.data.vertices[index].co
           for index in range(0, len(original.data.vertices), 97)
           if (normalization @ original.data.vertices[index].co).z < report['protectedBelowMetres']]
if len(samples) != report['trunkSurfaceDistanceMetres']['samples']:
    raise RuntimeError('Pine source comparison sample count changed')

# Independently compare the sampled upper wood-colored surface, including
# branches outside the protected central region. This is a texture-based
# sample, not a semantic proof that every branch was identified.
sample_ids = np.arange(0, len(original.data.vertices), 97, dtype=np.int32)
loop_ids = np.empty(len(original.data.loops), dtype=np.int32)
original.data.loops.foreach_get('vertex_index', loop_ids)
uv = np.empty(len(original.data.loops) * 2, dtype=np.float32)
original.data.uv_layers.active.data.foreach_get('uv', uv); uv = uv.reshape(-1, 2)
selected = np.isin(loop_ids, sample_ids); ids = loop_ids[selected]; coordinates = uv[selected]
image = bpy.data.images.load(str(here / report['woodAtlas']), check_existing=True)
width, height = image.size; pixels = np.empty(width * height * 4, dtype=np.float32)
image.pixels.foreach_get(pixels); pixels = pixels.reshape(height, width, 4)
x = (np.mod(coordinates[:, 0], 1) * width).astype(np.int32)
y = (np.mod(coordinates[:, 1], 1) * height).astype(np.int32)
color = pixels[y, x, :3]
wood = (color[:, 0] > color[:, 1] * 1.05) & (color[:, 0] > color[:, 2] * 1.10) & (color[:, 0] > .005)
totals = np.bincount(ids, minlength=len(original.data.vertices))
votes = np.bincount(ids, weights=wood, minlength=len(original.data.vertices))
wood_vertices = votes / np.maximum(totals, 1) > .5
upper = [normalization @ original.data.vertices[int(index)].co for index in sample_ids
         if wood_vertices[index] and (normalization @ original.data.vertices[int(index)].co).z >= report['protectedBelowMetres']]
if len(upper) < 100: raise RuntimeError('Too few upper wood samples')

def errors(obj, points):
    obj.data.calc_loop_triangles()
    vertices = [obj.matrix_world @ v.co for v in obj.data.vertices]
    triangles = [tuple(t.vertices) for t in obj.data.loop_triangles]
    tree = BVHTree.FromPolygons(vertices, triangles, all_triangles=True)
    distances = sorted(tree.find_nearest(point)[3] for point in points)
    if not all(math.isfinite(value) for value in distances): raise RuntimeError('Invalid surface distance')
    return dict(p95=distances[math.ceil((len(distances)-1)*.95)], maximum=distances[-1])

editable = errors(candidate, samples)
upper_baseline = errors(bpy.data.objects['CE_pine_LOD0'], upper)
upper_candidate = errors(candidate, upper)
if editable['p95'] > .001 or editable['maximum'] > .002:
    raise RuntimeError('Editable trunk no longer matches the source')
if upper_candidate['p95'] >= upper_baseline['p95']:
    raise RuntimeError('Candidate did not improve the sampled upper wood surface')
def surface_area(obj):
    obj.data.calc_loop_triangles()
    total = 0.0
    for triangle in obj.data.loop_triangles:
        a, b, c = (obj.matrix_world @ obj.data.vertices[index].co for index in triangle.vertices)
        total += (b-a).cross(c-a).length * .5
    return total
editable_area = surface_area(candidate)
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=str(root / report['export']))
models = [obj for obj in bpy.data.objects if obj.type == 'MESH']
if len(models) != 1 or not models[0].data.uv_layers.active:
    raise RuntimeError('Review FBX must contain one atlased mesh')
models[0].data.calc_loop_triangles()
models[0].data.calc_normals_split()
export_zero_normals = sum(loop.normal.length < 1e-6 for loop in models[0].data.loops)
if export_zero_normals: raise RuntimeError('Review FBX contains zero-length normals')
export_area = surface_area(models[0])
print('PINE_REIMPORT_TRIANGLES', len(models[0].data.loop_triangles), 'EDITABLE', report['triangles'],
      'AREA', editable_area, export_area, flush=True)
exported = errors(models[0], samples)
upper_exported = errors(models[0], upper)
print('PINE_REIMPORT_TRUNK_ERRORS', exported, flush=True)
if not math.isclose(editable_area, export_area, rel_tol=.003, abs_tol=.001):
    raise RuntimeError('Review FBX lost substantial surface area')
if exported['p95'] > .001 or exported['maximum'] > .002:
    raise RuntimeError('Review FBX no longer matches the preserved trunk')
if upper_exported['p95'] > upper_candidate['p95'] + .002:
    raise RuntimeError('Review FBX degraded sampled upper wood surface')
proof = dict(scope='CPU Blender surface sampling and FBX reimport; not game-camera or FPS approval',
    editableSha256=report['editableSha256'], exportSha256=report['exportSha256'],
    samples=len(samples), editable=editable, exported=exported,
    upperWood=dict(samples=len(upper), baseline=upper_baseline, editable=upper_candidate, exported=upper_exported),
    editableTriangles=report['triangles'], exportedTriangles=len(models[0].data.loop_triangles),
    editableSurfaceArea=editable_area, exportedSurfaceArea=export_area,
    editableZeroNormalLoops=editable_zero_normals, exportedZeroNormalLoops=export_zero_normals)
(root / 'proof.json').write_text(json.dumps(proof, indent=2) + '\n', encoding='utf-8')
print('PINE_TRUNK_REVIEW_PROOF_PASS', json.dumps(proof), flush=True)
