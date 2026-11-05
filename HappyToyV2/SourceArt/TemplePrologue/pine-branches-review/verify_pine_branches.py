"""Independently reimport FBX and sample source fidelity and coverage.

Run with Blender 4.0.2 in background. This is not a native player review.
"""
import bpy
import argparse
import json
import numpy as np
import sys
from pathlib import Path
from mathutils import Matrix, Vector
from mathutils.bvhtree import BVHTree

here=Path(__file__).resolve().parent
parser=argparse.ArgumentParser()
parser.add_argument('--output',required=True,help='New JSON output path')
args=parser.parse_args(sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else [])
output=Path(args.output).resolve()
if output.exists():raise RuntimeError('Output already exists')
output.parent.mkdir(parents=True,exist_ok=True)
bpy.ops.wm.open_mainfile(filepath=str(here/'pine-branches-review.blend'))
original=bpy.data.objects['geometry_0'];candidate=bpy.data.objects['Pine preserve all wood candidate']
corners=[original.matrix_world@Vector(v) for v in original.bound_box]
low=Vector(tuple(min(v[i] for v in corners) for i in range(3)))
high=Vector(tuple(max(v[i] for v in corners) for i in range(3)))
center=Vector(((low.x+high.x)/2,(low.y+high.y)/2,low.z))
normalization=Matrix.Scale(10/(high.z-low.z),4)@Matrix.Translation(-center)@original.matrix_world
def arrays(obj, matrix):
    mesh=obj.data;mesh.calc_loop_triangles();mesh.calc_normals_split()
    if not mesh.uv_layers.active or len(mesh.uv_layers.active.data)!=len(mesh.loops):raise RuntimeError('Missing atlas')
    uv=np.empty(len(mesh.loops)*2,dtype=np.float32);mesh.uv_layers.active.data.foreach_get('uv',uv)
    normals=np.empty(len(mesh.loops)*3,dtype=np.float32);mesh.loops.foreach_get('normal',normals)
    if not np.isfinite(uv).all() or not np.isfinite(normals).all():raise RuntimeError('Invalid atlas or normals')
    xyz=np.empty(len(mesh.vertices)*3,dtype=np.float32);mesh.vertices.foreach_get('co',xyz)
    mat=np.array(matrix,dtype=np.float64);xyz=xyz.reshape(-1,3)@mat[:3,:3].T+mat[:3,3]
    faces=np.empty(len(mesh.loop_triangles)*3,dtype=np.int32);mesh.loop_triangles.foreach_get('vertices',faces);faces=faces.reshape(-1,3)
    a,b,c=(xyz[faces[:,i]] for i in range(3))
    area=.5*np.linalg.norm(np.cross(b-a,c-a),axis=1)
    return xyz,faces,{'triangles':len(faces),'area_m2':float(area.sum()),
        'zero_normal_loops':int((np.linalg.norm(normals.reshape(-1,3),axis=1)<1e-6).sum())}
def make_tree(xyz,faces):return BVHTree.FromPolygons(xyz.tolist(),faces.tolist(),all_triangles=True)
src,srcfaces,_=arrays(original,normalization);source_bvh=make_tree(src,srcfaces)
editable,editable_faces,editable_info=arrays(candidate,candidate.matrix_world);editable_bvh=make_tree(editable,editable_faces)
rng=np.random.default_rng(7391);samples={}
for region,mask in [('trunk',src[:,2]<4.6),('canopy',src[:,2]>=4.6)]:
    ids=np.flatnonzero(mask);samples[region]=src[rng.choice(ids,size=min(1200,len(ids)),replace=False)]
def distances(tree,points):
    out=[]
    for point in points:
        hit=tree.find_nearest(Vector(point))
        if hit is None or hit[0] is None:raise RuntimeError('Missing nearest surface')
        out.append(hit[3])
    a=np.array(out);return {'n':len(a),'p95_m':float(np.percentile(a,95)), 'max_m':float(a.max())}
def misses(reference,tree):
    count=both=0
    for x in np.linspace(-5,5,51):
        for y in np.linspace(-5,5,51):
            start=Vector((x,y,11));direction=Vector((0,0,-1))
            orig=reference.ray_cast(start,direction,20)[0];other=tree.ray_cast(start,direction,20)[0]
            if orig is not None:both+=1;count+=int(other is None)
    return {'source_hits':both,'source_hit_candidate_miss':count,'grid_size':2601}
editable_info['source_to_editable']={name:distances(editable_bvh,points) for name,points in samples.items()}
editable_info['top_projection']=misses(source_bvh,editable_bvh)
del editable_bvh,editable,editable_faces
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=str(here/'pine-branches-review.fbx'))
models=[obj for obj in bpy.data.objects if obj.type=='MESH']
if len(models)!=1:raise RuntimeError('FBX does not contain one mesh')
exported,exported_faces,exported_info=arrays(models[0],models[0].matrix_world)
if exported_info['zero_normal_loops']!=0:raise RuntimeError('Export has zero normals')
export_bvh=make_tree(exported,exported_faces)
exported_info['source_to_export']={name:distances(export_bvh,points) for name,points in samples.items()}
exported_info['top_projection']=misses(source_bvh,export_bvh)
exported_info['surface_area_loss_fraction']=1-exported_info['area_m2']/editable_info['area_m2']
if abs(exported_info['surface_area_loss_fraction'])>.003:raise RuntimeError('FBX surface area mismatch')
report={'scope':'Blender CPU samples; not proof of perceptual quality or player FPS',
        'editable':editable_info,'fbx_reimport':exported_info}
output.write_text(json.dumps(report,indent=2),encoding='utf-8')
print('PINE_BRANCH_EXPORT_PROOF_PASS',flush=True)
