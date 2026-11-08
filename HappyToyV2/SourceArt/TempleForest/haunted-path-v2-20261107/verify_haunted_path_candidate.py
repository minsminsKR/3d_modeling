"""Check candidate geometry, PBR links, source distances and FBX reimport.

Blender 4.0.2; background CPU only. The result is sampled evidence, not
Unity/player appearance, exact seam quality, or GPU performance certification.
"""
from pathlib import Path
import hashlib
import json
import math
import numpy as np
import bpy
from mathutils import Vector
from mathutils.bvhtree import BVHTree

here = Path(__file__).resolve().parent
key = here.name
report = json.loads((here / 'export.json').read_text(encoding='utf-8'))
proof_path = here / 'surface-proof.json'
if proof_path.exists(): raise RuntimeError('Refusing to overwrite existing verification')
def sha(path):
    h = hashlib.sha256()
    with path.open('rb') as f:
        for block in iter(lambda: f.read(1048576), b''): h.update(block)
    return h.hexdigest()
if sha(here / 'original.glb') != report['originalSha256']: raise RuntimeError('Original hash changed')
blend = here / (key + '.blend')
if sha(blend) != report['editableSha256']: raise RuntimeError('Editable blend hash changed')
for entry in report['lods'] + report['edgeReviewVariants']:
    if sha(here / 'game-export' / entry['file']) != entry['sha256']: raise RuntimeError('FBX hash changed')
bpy.ops.wm.open_mainfile(filepath=str(blend))
source_collection = bpy.data.collections['Complete unchanged webapp original']
originals = [obj for obj in source_collection.objects if obj.type == 'MESH']
if len(originals) != 1: raise RuntimeError('Expected one preserved source mesh')
source = originals[0]; source.data.calc_loop_triangles()
world_corners = [source.matrix_world @ Vector(corner) for corner in source.bound_box]
lo = Vector([min(point[i] for point in world_corners) for i in range(3)])
hi = Vector([max(point[i] for point in world_corners) for i in range(3)])
centre = Vector(((lo.x + hi.x) * .5, (lo.y + hi.y) * .5, lo.z))
sx = 2.0 / (hi.x - lo.x); sy = 2.0 / (hi.y - lo.y); sz = min(sx, .12 / (hi.z - lo.z))
source_points = []
for vertex in source.data.vertices:
    v = source.matrix_world @ vertex.co - centre
    source_points.append(Vector((v.x * sx, v.y * sy, v.z * sz)))
source_faces = [tuple(tri.vertices) for tri in source.data.loop_triangles]
source_tree = BVHTree.FromPolygons(source_points, source_faces, all_triangles=True)
source_samples = source_points[::191]

def mesh_arrays(obj):
    mesh = obj.data; mesh.calc_loop_triangles(); mesh.calc_normals_split()
    if not mesh.uv_layers.active or len(mesh.uv_layers.active.data) != len(mesh.loops):
        raise RuntimeError('Missing UVs: ' + obj.name)
    uv = np.empty(len(mesh.loops) * 2, dtype=np.float32); mesh.uv_layers.active.data.foreach_get('uv', uv)
    if not np.isfinite(uv).all(): raise RuntimeError('Nonfinite UVs: ' + obj.name)
    points = [obj.matrix_world @ vertex.co for vertex in mesh.vertices]
    faces = [tuple(triangle.vertices) for triangle in mesh.loop_triangles]
    if not points or not faces: raise RuntimeError('Empty mesh: ' + obj.name)
    normals = sum(loop.normal.length < 1e-6 for loop in mesh.loops)
    if normals: raise RuntimeError('Zero normals: ' + obj.name)
    xyz = np.asarray([tuple(v) for v in points], dtype=np.float64)
    indices = np.asarray(faces, dtype=np.int32)
    normals = np.cross(xyz[indices[:,1]]-xyz[indices[:,0]], xyz[indices[:,2]]-xyz[indices[:,0]])
    area = float(np.linalg.norm(normals,axis=1).sum()*.5)
    return mesh,points,faces,xyz,indices,uv,area

def metric(tree, points):
    values = sorted(tree.find_nearest(point)[3] for point in points)
    if not values or not all(math.isfinite(value) for value in values): raise RuntimeError('Invalid surface samples')
    return {'samples':len(values),'p95':values[math.ceil((len(values)-1)*.95)],'maximum':values[-1]}

def projection(tree):
    values=[]
    for iy in range(25):
        for ix in range(25):
            point,normal,index,distance=tree.ray_cast(Vector((-.95+1.9*(ix+.5)/25,-.95+1.9*(iy+.5)/25,1)),Vector((0,0,-1)))
            values.append(None if point is None else float(point.z))
    return values

source_projection=projection(source_tree)
bases=[]; entries=[]; interior=[]
for level,entry in enumerate(report['lods']):
    obj=bpy.data.objects['CE_'+key+'_LOD'+str(level)]
    mesh,points,faces,xyz,indices,uv,area=mesh_arrays(obj)
    if len(faces)!=entry['triangles']: raise RuntimeError('LOD face count mismatch')
    tree=BVHTree.FromPolygons(points,faces,all_triangles=True); heights=projection(tree)
    diffs=sorted(abs(a-b) for a,b in zip(source_projection,heights) if a is not None and b is not None)
    if not diffs:raise RuntimeError('No shared projection samples')
    item={'name':obj.name,'triangles':len(faces),'vertices':len(points),'area':area,
          'boundsMin':xyz.min(axis=0).tolist(),'boundsMax':xyz.max(axis=0).tolist(),
          'sourceToCandidate':metric(tree,source_samples),'candidateToSource':metric(source_tree,points[::11]),
          'projection':{'samples':len(heights),'sourceOnlyHoles':sum(a is not None and b is None for a,b in zip(source_projection,heights)),
                        'candidateOnlyHits':sum(a is None and b is not None for a,b in zip(source_projection,heights)),
                        'heightDifferenceP95':diffs[math.ceil((len(diffs)-1)*.95)]}}
    bases.append((xyz,indices,uv));entries.append(item)
    print('VERIFIED_BASE '+str(level),flush=True)
variants=[]
for entry in report['edgeReviewVariants']:
    level=entry['level']; obj=bpy.data.objects['CE_'+key+'_edge_'+entry['variant']+'_LOD'+str(level)]
    mesh,points,faces,xyz,indices,uv,area=mesh_arrays(obj);base_xyz,base_indices,base_uv=bases[level]
    if not np.array_equal(indices,base_indices) or not np.array_equal(uv,base_uv):raise RuntimeError('Variant changed triangle topology or UVs')
    delta=np.linalg.norm(xyz-base_xyz,axis=1);inside=np.max(np.abs(base_xyz[:,:2]),axis=1)<=.72
    if np.any(delta[inside]>1e-6):raise RuntimeError('Variant changed interior')
    n0=np.cross(base_xyz[indices[:,1]]-base_xyz[indices[:,0]],base_xyz[indices[:,2]]-base_xyz[indices[:,0]])
    n1=np.cross(xyz[indices[:,1]]-xyz[indices[:,0]],xyz[indices[:,2]]-xyz[indices[:,0]])
    flipped=int(np.count_nonzero(np.einsum('ij,ij->i',n0,n1)<0));zero=int(np.count_nonzero(np.linalg.norm(n1,axis=1)<1e-12))
    if flipped or zero:raise RuntimeError('Variant inverted or collapsed faces: '+obj.name+' '+str((flipped,zero)))
    if not np.allclose(xyz.min(axis=0),entry['boundsMin'],atol=1e-6) or not np.allclose(xyz.max(axis=0),entry['boundsMax'],atol=1e-6):raise RuntimeError('Variant bounds metadata is wrong')
    variants.append({'name':obj.name,'triangles':len(faces),'vertices':len(points),'area':area,
                     'boundsMin':xyz.min(axis=0).tolist(),'boundsMax':xyz.max(axis=0).tolist(),
                     'changedVertices':int(np.count_nonzero(delta>1e-6)),'maxVertexDisplacement':float(delta.max()),
                     'interiorMaxDisplacement':float(delta[inside].max()),'invertedFaces':flipped,'collapsedFaces':zero,
                     'uvAndTopologyExactlyMatchBase':True})
    print('VERIFIED_VARIANT '+entry['variant']+' '+str(level),flush=True)

# Reimport every actual FBX into a fresh disposable scene. Metadata and area
# are checked against its corresponding editable object, not just file names.
reimports=[]
all_exports=[(entry,entries[i]) for i,entry in enumerate(report['lods'])]+list(zip(report['edgeReviewVariants'],variants))
for entry,reference in all_exports:
    path=here/'game-export'/entry['file']
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(path))
    objs=[obj for obj in bpy.context.scene.objects if obj.type=='MESH']
    if len(objs)!=1:raise RuntimeError('Expected one mesh in '+entry['file'])
    mesh,points,faces,xyz,indices,uv,area=mesh_arrays(objs[0])
    triangle_loss=reference['triangles']-len(faces);area_diff=(area-reference['area'])/reference['area']
    if abs(triangle_loss)>max(10,round(reference['triangles']*.01)) or abs(area_diff)>.01:
        raise RuntimeError('FBX reimport geometry changed excessively: '+entry['file'])
    reimports.append({'file':entry['file'],'triangles':len(faces),'vertices':len(points),'areaRelativeDifference':area_diff,
                      'triangleLoss':triangle_loss,'boundsMin':xyz.min(axis=0).tolist(),'boundsMax':xyz.max(axis=0).tolist()})
    print('VERIFIED_FBX '+entry['file'],flush=True)

proof={'scope':'Sampled source/LOD surface distances, top projection, exact variant UV/topology and Blender FBX reimport; not Unity or GPU approval.',
       'originalSha256':report['originalSha256'],'editableSha256':report['editableSha256'],
       'baseLods':entries,'edgeVariants':variants,'fbxReimports':reimports,
       'limitations':'The edge deformations are intentional. Sparse rays and nearest samples do not prove no seams, self-intersection, temporal artifacts, loss of quality, or player performance.'}
proof_path.write_text(json.dumps(proof,indent=2)+'\n',encoding='utf-8',newline='\n')
print('HAUNTED_PATH_PROOF '+json.dumps({'bases':len(entries),'variants':len(variants),'fbx':len(reimports)}),flush=True)
