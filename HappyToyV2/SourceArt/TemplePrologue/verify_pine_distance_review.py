"""Independently check the distant pine candidate and its FBX reimport.

Blender 4.0.2, background/CPU. Does not certify game appearance or FPS.
"""
import hashlib
import json
import math
from collections import Counter
from pathlib import Path
import bpy
from mathutils import Matrix, Vector
from mathutils.bvhtree import BVHTree

root = Path(__file__).resolve().parent / 'pine-review'
report = json.loads((root/'distance-report.json').read_text(encoding='utf-8'))
sha = lambda p: hashlib.sha256(p.read_bytes()).hexdigest()
for name, digest in ((report['source'], report['sourceSha256']), (report['editable'], report['editableSha256']),
                     (report['export'], report['exportSha256'])):
    if sha(root/name) != digest: raise RuntimeError('Distant pine source hash differs: '+name)
bpy.ops.wm.open_mainfile(filepath=str(root/report['source']))
original = bpy.data.objects['geometry_0']; near = bpy.data.objects['CE_pine_trunk_review']
corners = [original.matrix_world @ Vector(point) for point in original.bound_box]
low = Vector(tuple(min(point[i] for point in corners) for i in range(3)))
high = Vector(tuple(max(point[i] for point in corners) for i in range(3)))
centre = Vector(((low.x+high.x)/2, (low.y+high.y)/2, low.z))
normalization = Matrix.Scale(10/(high.z-low.z),4) @ Matrix.Translation(-centre) @ original.matrix_world
samples = [normalization @ original.data.vertices[i].co for i in range(0,len(original.data.vertices),97)
           if (normalization @ original.data.vertices[i].co).z < 3.5]
if len(samples) != report['lowerTrunkDistanceMetres']['samples']:
    raise RuntimeError('Distant pine source sample count changed')
ground = min((near.matrix_world @ vertex.co).z for vertex in near.data.vertices)

def measure(obj):
    obj.data.calc_loop_triangles(); obj.data.calc_normals_split()
    vertices = [obj.matrix_world @ v.co for v in obj.data.vertices]
    triangles = [tuple(t.vertices) for t in obj.data.loop_triangles]
    tree = BVHTree.FromPolygons(vertices, triangles, all_triangles=True)
    values = sorted(tree.find_nearest(point)[3] for point in samples)
    if not all(math.isfinite(value) for value in values): raise RuntimeError('Invalid surface distance')
    area = sum((vertices[b]-vertices[a]).cross(vertices[c]-vertices[a]).length*.5 for a,b,c in triangles)
    return dict(p95=values[math.ceil((len(values)-1)*.95)], maximum=values[-1],
                ground=min(v.z for v in vertices), triangles=len(triangles), area=area,
                zeroNormalLoops=sum(loop.normal.length < 1e-6 for loop in obj.data.loops))

def signatures(obj):
    # Quantized multisets are independent of FBX face/vertex ordering. This
    # checks approximate per-corner UVs and normals, including repeated faces.
    obj.data.calc_loop_triangles(); obj.data.calc_normals_split()
    atlas = obj.data.uv_layers.active.data
    normal_matrix = obj.matrix_world.to_3x3().inverted().transposed()
    position_uv, with_normals = Counter(), Counter()
    for triangle in obj.data.loop_triangles:
        basic, full = [], []
        for vertex, loop in zip(triangle.vertices, triangle.loops):
            position = obj.matrix_world @ obj.data.vertices[vertex].co
            normal = (normal_matrix @ obj.data.loops[loop].normal).normalized()
            key = tuple(round(value, 3) for value in (*position, *atlas[loop].uv))
            basic.append(key); full.append(key + tuple(round(value, 2) for value in normal))
        position_uv[tuple(sorted(basic))] += 1; with_normals[tuple(sorted(full))] += 1
    return position_uv, with_normals

bpy.ops.wm.open_mainfile(filepath=str(root/report['editable']))
candidate = bpy.data.objects['CE_pine_distance_review']
if not candidate.data.uv_layers.active or len(candidate.data.uv_layers.active.data) != len(candidate.data.loops):
    raise RuntimeError('Distant review atlas is missing')
images = [node.image for material in candidate.data.materials if material and material.use_nodes
          for node in material.node_tree.nodes if node.type == 'TEX_IMAGE' and node.image]
if not images or any(not image.packed_file for image in images):
    raise RuntimeError('Distant editable source lacks packed texture inputs')
editable = measure(candidate)
editable_uv, editable_normals = signatures(candidate)
if (editable['zeroNormalLoops'] or editable['triangles'] != report['triangles'] or abs(editable['ground']-ground) > .001 or
    abs(editable['p95']-report['lowerTrunkDistanceMetres']['candidate']['p95']) > .001):
    raise RuntimeError('Distant editable geometry differs from its report')
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=str(root/report['export']))
models = [obj for obj in bpy.data.objects if obj.type == 'MESH']
if len(models) != 1 or not models[0].data.uv_layers.active: raise RuntimeError('Distant FBX atlas or mesh missing')
exported = measure(models[0])
exported_uv, exported_normals = signatures(models[0])
uv_match = sum((editable_uv & exported_uv).values()) / editable['triangles']
normal_match = sum((editable_normals & exported_normals).values()) / editable['triangles']
print('PINE_DISTANCE_REIMPORT',json.dumps(dict(editable=editable,exported=exported,ground=ground)),flush=True)
if (exported['zeroNormalLoops'] or exported['triangles'] != editable['triangles'] or uv_match < .99 or normal_match < .99 or
    abs(exported['ground']-ground) > .001 or abs(exported['p95']-editable['p95']) > .002 or
    not math.isclose(exported['area'],editable['area'],rel_tol=.005,abs_tol=.001)):
    raise RuntimeError('Distant FBX does not preserve sampled geometry')
proof = dict(scope='CPU Blender sample/FBX check; not game-camera or FPS approval',
             editableSha256=report['editableSha256'], exportSha256=report['exportSha256'],
             samples=len(samples), packedTextures=len(images), editable=editable, exported=exported,
             positionUvMatchFraction=uv_match, positionUvNormalMatchFraction=normal_match,
             signatureRounding='position/UV to 0.001, normals to 0.01')
(root/'distance-proof.json').write_text(json.dumps(proof,indent=2)+'\n',encoding='utf-8')
print('PINE_DISTANCE_REVIEW_PROOF_PASS',json.dumps(proof),flush=True)
