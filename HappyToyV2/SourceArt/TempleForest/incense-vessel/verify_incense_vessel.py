"""Measure generated-source surface against editable LOD and FBX reimport.

Runs with Blender 4.0.2 on CPU. Sampling is not a full geometry or render proof.
"""
import hashlib
import json
import math
from pathlib import Path
import bpy
from mathutils import Matrix, Vector
from mathutils.bvhtree import BVHTree

root = Path(__file__).resolve().parent
report = json.loads((root / 'export.json').read_text(encoding='utf-8'))
def sha(path):
    digest = hashlib.sha256()
    with path.open('rb') as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b''):
            digest.update(block)
    return digest.hexdigest()

if sha(root / 'original.glb') != report['originalSha256']:
    raise RuntimeError('Generated original differs from the recorded source')
blend = root / 'incense-vessel.blend'
bpy.ops.wm.open_mainfile(filepath=str(blend))
originals = [obj for obj in bpy.data.collections['Complete unchanged webapp original'].objects if obj.type == 'MESH']
if len(originals) != 1:
    raise RuntimeError('Expected one preserved original mesh')
full = originals[0]
corners = [full.matrix_world @ Vector(x) for x in full.bound_box]
lo = Vector(tuple(min(p[i] for p in corners) for i in range(3)))
hi = Vector(tuple(max(p[i] for p in corners) for i in range(3)))
centre = Vector(((lo.x + hi.x) / 2, (lo.y + hi.y) / 2, lo.z))
normalize = Matrix.Scale(.44 / (hi.x - lo.x), 4) @ Matrix.Translation(-centre) @ full.matrix_world
step = max(1, len(full.data.vertices) // 1800)
points = [normalize @ full.data.vertices[i].co for i in range(0, len(full.data.vertices), step)]

def measure(obj):
    mesh = obj.data
    mesh.calc_loop_triangles()
    mesh.calc_normals_split()
    if not mesh.uv_layers.active or len(mesh.uv_layers.active.data) != len(mesh.loops):
        raise RuntimeError('Missing source-derived UV atlas')
    if any(not math.isfinite(v) for uv in mesh.uv_layers.active.data for v in uv.uv):
        raise RuntimeError('Non-finite UV')
    vertices = [obj.matrix_world @ v.co for v in mesh.vertices]
    faces = [tuple(t.vertices) for t in mesh.loop_triangles]
    tree = BVHTree.FromPolygons(vertices, faces, all_triangles=True)
    distances = sorted(tree.find_nearest(point)[3] for point in points)
    if not all(math.isfinite(d) for d in distances):
        raise RuntimeError('Non-finite sampled surface distance')
    area = sum((vertices[b] - vertices[a]).cross(vertices[c] - vertices[a]).length * .5 for a, b, c in faces)
    return dict(triangles=len(faces), vertices=len(vertices), area=area,
                zeroNormalLoops=sum(loop.normal.length < 1e-6 for loop in mesh.loops),
                sourceSampleCount=len(distances), sourceP95=distances[math.ceil((len(distances)-1)*.95)],
                sourceMaximum=distances[-1])

editable = []
for i, entry in enumerate(report['lods']):
    data = measure(bpy.data.objects['CE_incense-vessel_LOD' + str(i)])
    if data['triangles'] != entry['triangles']:
        raise RuntimeError('Editable triangles differ from manifest')
    editable.append(data)
exported = []
for i, entry in enumerate(report['lods']):
    path = root / 'game-export' / entry['file']
    if sha(path) != entry['sha256']:
        raise RuntimeError('Export hash differs: ' + path.name)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(path))
    models = [obj for obj in bpy.data.objects if obj.type == 'MESH']
    if len(models) != 1:
        raise RuntimeError('Expected one exported stone mesh')
    result = measure(models[0])
    result['areaRelativeLoss'] = (editable[i]['area'] - result['area']) / editable[i]['area']
    result['triangleLoss'] = editable[i]['triangles'] - result['triangles']
    exported.append(result)
proof = dict(scope='CPU original surface sampling and Blender FBX reimport only; Unity visual and performance are separate',
             sourceSha256=report['originalSha256'], editableSha256=sha(blend),
             exportSha256=[entry['sha256'] for entry in report['lods']], editable=editable, exported=exported)
(root / 'surface-proof.json').write_text(json.dumps(proof, indent=2) + '\n', encoding='utf-8')
print('INCENSE_VESSEL_SURFACE', json.dumps(proof), flush=True)
