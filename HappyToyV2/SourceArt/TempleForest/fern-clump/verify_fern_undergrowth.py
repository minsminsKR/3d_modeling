"""Check fern LOD surface samples and FBX reimport with Blender 4.0.2.

These are geometric checks, not in-game appearance or performance approval.
"""
import hashlib
import json
import math
from pathlib import Path

import bpy
from mathutils.bvhtree import BVHTree

ROOT = Path(__file__).resolve().parent
report = json.loads((ROOT / 'undergrowth-export.json').read_text(encoding='utf8'))
sha = lambda path: hashlib.sha256(path.read_bytes()).hexdigest()
if sha(ROOT / 'original.glb') != report['originalSha256'] or sha(ROOT / 'fern-clump.blend') != report['sourceSha256']:
    raise RuntimeError('The preserved generated fern or editable source changed')
if sha(ROOT / report['editable']) != report['editableSha256']:
    raise RuntimeError('The undergrowth blend does not match its export report')

bpy.ops.wm.open_mainfile(filepath=str(ROOT / 'fern-clump.blend'))
source = bpy.data.objects['CE_fern-clump_LOD0']
samples = [source.matrix_world @ source.data.vertices[i].co for i in range(0, len(source.data.vertices), 19)]


def measure(obj):
    mesh = obj.data
    mesh.calc_loop_triangles()
    mesh.calc_normals_split()
    if not mesh.uv_layers.active or len(mesh.uv_layers.active.data) != len(mesh.loops):
        raise RuntimeError('Fern atlas missing')
    if any(not math.isfinite(n) for loop in mesh.uv_layers.active.data for n in loop.uv):
        raise RuntimeError('Non-finite fern atlas coordinate')
    positions = [obj.matrix_world @ vertex.co for vertex in mesh.vertices]
    faces = [tuple(tri.vertices) for tri in mesh.loop_triangles]
    tree = BVHTree.FromPolygons(positions, faces, all_triangles=True)
    distance = sorted(tree.find_nearest(point)[3] for point in samples)
    if not all(math.isfinite(value) for value in distance):
        raise RuntimeError('Non-finite surface distance')
    area = sum((positions[b] - positions[a]).cross(positions[c] - positions[a]).length * .5
               for a, b, c in faces)
    return dict(triangles=len(faces), vertices=len(positions), area=area,
        zeroNormals=sum(loop.normal.length < 1e-6 for loop in mesh.loops),
        sourceSampleCount=len(distance), surfaceDistanceP95=distance[math.ceil((len(distance)-1)*.95)],
        surfaceDistanceMaximum=distance[-1])


bpy.ops.wm.open_mainfile(filepath=str(ROOT / report['editable']))
editable = []
for index, entry in enumerate(report['lods'], start=1):
    model = bpy.data.objects['Fern undergrowth LOD' + str(index)]
    item = measure(model)
    if item['triangles'] != entry['triangles'] or item['zeroNormals']:
        raise RuntimeError('Unexpected undergrowth mesh or zero normal')
    editable.append(item)

reimported = []
for index, entry in enumerate(report['lods']):
    path = ROOT / 'game-export' / entry['file']
    if sha(path) != entry['sha256']:
        raise RuntimeError('FBX checksum mismatch: ' + path.name)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(path))
    models = [obj for obj in bpy.data.objects if obj.type == 'MESH']
    if len(models) != 1:
        raise RuntimeError('Expected exactly one FBX model')
    item = measure(models[0])
    item['areaRelativeLoss'] = (editable[index]['area'] - item['area']) / editable[index]['area']
    item['triangleLoss'] = editable[index]['triangles'] - item['triangles']
    if item['zeroNormals'] or abs(item['areaRelativeLoss']) > .01:
        raise RuntimeError('FBX reimport lost normals or excessive surface area')
    reimported.append(item)
proof = dict(scope='Source mesh sampling and Blender FBX reimport; not Unity visual or FPS approval',
    sourceSha256=report['sourceSha256'], editableSha256=report['editableSha256'],
    exportSha256=[entry['sha256'] for entry in report['lods']], editable=editable, reimported=reimported)
(ROOT / 'undergrowth-proof.json').write_text(json.dumps(proof, indent=2) + '\n', encoding='utf8')
print('TEMPLE_UNDERGROWTH_VERIFIED', json.dumps(proof), flush=True)
