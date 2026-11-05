"""Sample source-to-LOD surface fidelity and Blender FBX reimport.

This is a CPU geometric check, not an approval of Unity appearance, LOD
switching, playback performance, or reference-image visual quality.
"""
import hashlib
import json
import math
from pathlib import Path

import bpy
from mathutils import Vector
from mathutils.bvhtree import BVHTree

root = Path(__file__).resolve().parent
report = json.loads((root / 'export.json').read_text(encoding='utf-8'))
sha = lambda p: hashlib.sha256(p.read_bytes()).hexdigest()
if sha(root / 'original.glb') != report['originalSha256']:
    raise RuntimeError('Original generated GLB checksum changed')
blend = root / 'wide-zelkova.blend'
bpy.ops.wm.open_mainfile(filepath=str(blend))
source_collection = bpy.data.collections['Complete unchanged webapp original']
source_objects = [obj for obj in source_collection.objects if obj.type == 'MESH']
if len(source_objects) != 1:
    raise RuntimeError('Expected one unchanged generated source mesh')
source = source_objects[0]
vertices = [source.matrix_world @ vertex.co for vertex in source.data.vertices]
low = [min(point[axis] for point in vertices) for axis in range(3)]
high = [max(point[axis] for point in vertices) for axis in range(3)]
factor = 8.0 / (high[2] - low[2])
centre = Vector(((low[0] + high[0]) * .5, (low[1] + high[1]) * .5, low[2]))
points = [(vertices[index] - centre) * factor for index in range(0, len(vertices), 353)]
trunk = [point for point in points if point.z < 2.2]
canopy = [point for point in points if point.z >= 2.2]
if len(trunk) < 20 or len(canopy) < 20:
    raise RuntimeError('Insufficient source surface samples')


def measure(obj):
    mesh = obj.data
    mesh.calc_loop_triangles()
    mesh.calc_normals_split()
    if not mesh.uv_layers.active or len(mesh.uv_layers.active.data) != len(mesh.loops):
        raise RuntimeError('LOD atlas is missing')
    if any(not math.isfinite(value) for loop in mesh.uv_layers.active.data for value in loop.uv):
        raise RuntimeError('LOD contains invalid UV values')
    verts = [obj.matrix_world @ vertex.co for vertex in mesh.vertices]
    faces = [tuple(triangle.vertices) for triangle in mesh.loop_triangles]
    tree = BVHTree.FromPolygons(verts, faces, all_triangles=True)

    def distances(samples):
        values = sorted(tree.find_nearest(point)[3] for point in samples)
        if not all(math.isfinite(value) for value in values):
            raise RuntimeError('Nonfinite source distance')
        return dict(samples=len(values), p95=values[math.ceil((len(values) - 1) * .95)], maximum=values[-1])

    area = sum((verts[b] - verts[a]).cross(verts[c] - verts[a]).length * .5 for a, b, c in faces)
    return dict(triangles=len(faces), vertices=len(verts), area=area,
                zeroNormalLoops=sum(loop.normal.length < 1e-6 for loop in mesh.loops),
                allSurfaceDistance=distances(points), rootDistance=distances(trunk),
                canopyDistance=distances(canopy))


editable = []
for index, entry in enumerate(report['lods']):
    obj = bpy.data.objects['CE_wide-zelkova_LOD' + str(index)]
    result = measure(obj)
    if result['triangles'] != entry['triangles']:
        raise RuntimeError('Editable triangle count differs from report')
    editable.append(result)
exported = []
for index, entry in enumerate(report['lods']):
    path = root / 'game-export' / entry['file']
    if sha(path) != entry['sha256']:
        raise RuntimeError('FBX checksum differs: ' + path.name)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(path))
    meshes = [obj for obj in bpy.data.objects if obj.type == 'MESH']
    if len(meshes) != 1:
        raise RuntimeError('Expected one mesh in LOD export')
    result = measure(meshes[0])
    print('WIDE_TREE_FBX_COMPARISON', json.dumps(dict(lod=index, editable=editable[index], exported=result)), flush=True)
    if not math.isclose(result['area'], editable[index]['area'], rel_tol=.005, abs_tol=.001):
        raise RuntimeError('FBX reimport loses over 0.5% surface area')
    exported.append(result)
proof = dict(scope='CPU original-source sampling and Blender FBX reimport only; no Unity or FPS approval',
             sourceSha256=report['originalSha256'], editableSha256=sha(blend),
             exportSha256=[entry['sha256'] for entry in report['lods']], editable=editable, exported=exported)
(root / 'surface-proof.json').write_text(json.dumps(proof, indent=2) + '\n', encoding='utf-8')
print('WIDE_TREE_SURFACE', json.dumps(proof), flush=True)
