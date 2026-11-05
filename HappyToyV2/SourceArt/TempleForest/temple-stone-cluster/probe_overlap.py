"""Sample projected overlapping front-facing triangles in the FBX LODs.

Blender 4.0.2, CPU only. The 0.04-metre height matches the visual comparison
fixture; an asset-space sample is not proof of the actual GPU depth buffer.
"""
from pathlib import Path
import json
import bpy
from mathutils import Vector
from mathutils.bvhtree import BVHTree

root = Path(__file__).resolve().parent
report = []
for level in (0, 1):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(root / 'game-export' / ('temple-stone-cluster-lod%d.fbx' % level)))
    models = [obj for obj in bpy.data.objects if obj.type == 'MESH']
    if len(models) != 1:
        raise RuntimeError('Expected one candidate mesh')
    obj = models[0]; obj.data.calc_loop_triangles()
    vertices = [obj.matrix_world @ vertex.co for vertex in obj.data.vertices]
    faces = [tuple(tri.vertices) for tri in obj.data.loop_triangles]
    lo = Vector(tuple(min(v[i] for v in vertices) for i in range(3)))
    hi = Vector(tuple(max(v[i] for v in vertices) for i in range(3)))
    front = []
    for a,b,c in faces:
        normal = (vertices[b] - vertices[a]).cross(vertices[c] - vertices[a])
        if normal.length > 1e-12 and normal.z > normal.length * .02:
            front.append((a,b,c))
    tree = BVHTree.FromPolygons(vertices, front, all_triangles=True)
    count, multi, tiny, pairs = 64, 0, 0, 0
    eps = (hi.z - lo.z) * 1e-6
    for iy in range(count):
        for ix in range(count):
            x = lo.x + (hi.x - lo.x) * (ix+.5)/count
            y = lo.y + (hi.y - lo.y) * (iy+.5)/count
            position = Vector((x,y,hi.z+eps*2))
            hits = []
            for _ in range(32):
                point, normal, triangle, distance = tree.ray_cast(position,Vector((0,0,-1)))
                if point is None: break
                hits.append(point.z)
                position = point - Vector((0,0,eps))
            if len(hits) > 1: multi += 1
            for upper, lower in zip(hits,hits[1:]):
                pairs += 1
                if (upper-lower)/(hi.z-lo.z)*.04 < .0001: tiny += 1
    item = dict(lod=level, triangles=len(faces), frontTriangles=len(front), sampledRays=count*count,
                raysWithMultipleSurfaces=multi, adjacentPairs=pairs,
                adjacentPairsUnderPointOneMillimetreAtFourCmHeight=tiny)
    report.append(item)
    print('TEMPLE_STONE_OVERLAP',json.dumps(item),flush=True)
(root/'overlap-proof.json').write_text(json.dumps(dict(scope='Sampled FBX geometry, not GPU approval',lods=report),indent=2)+'\n',encoding='utf8')
