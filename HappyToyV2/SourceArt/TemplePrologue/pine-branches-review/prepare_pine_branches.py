"""Try preserving all wood-classified source vertices, without modifying source.

Based on SourceArt/TemplePrologue/prepare_pine_trunk_review.py. The input atlas
classification is approximate; the output is a CPU review candidate only.
"""
import bmesh
import bpy
import argparse
import hashlib
import json
import numpy as np
import sys
from pathlib import Path
from mathutils import Matrix, Vector

here=Path(__file__).resolve().parent
src=here.parent
parser=argparse.ArgumentParser()
parser.add_argument('--output',required=True,help='New output directory; existing sources are never overwritten')
args=parser.parse_args(sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else [])
destination=Path(args.output).resolve();destination.mkdir(parents=True,exist_ok=False)
source=src/'pine/pine.blend'; output=destination/'pine-branches-review.blend'; export=destination/'pine-branches-review.fbx'
digest=lambda p: hashlib.sha256(p.read_bytes()).hexdigest()
original_hash=digest(source)
bpy.ops.wm.open_mainfile(filepath=str(source));bpy.context.preferences.filepaths.save_version=0
original=bpy.data.objects['geometry_0']
corners=[original.matrix_world@Vector(p) for p in original.bound_box]
lo=Vector(tuple(min(p[i] for p in corners) for i in range(3)))
hi=Vector(tuple(max(p[i] for p in corners) for i in range(3)))
center=Vector(((lo.x+hi.x)/2,(lo.y+hi.y)/2,lo.z))
normalization=Matrix.Scale(10/(hi.z-lo.z),4)@Matrix.Translation(-center)@original.matrix_world
candidate=original.copy(); candidate.data=original.data.copy(); candidate.name='Pine preserve all wood candidate'
bpy.context.scene.collection.objects.link(candidate)
candidate.data.transform(normalization); candidate.matrix_world=Matrix.Identity(4)
mesh=bmesh.new();mesh.from_mesh(candidate.data)
bmesh.ops.remove_doubles(mesh,verts=list(mesh.verts),dist=.00002)
bmesh.ops.recalc_face_normals(mesh,faces=list(mesh.faces))
nonmanifold=[edge for edge in mesh.edges if len(edge.link_faces)>2]
bmesh.ops.split_edges(mesh,edges=nonmanifold)
mesh.to_mesh(candidate.data);mesh.free();candidate.data.update()
image=bpy.data.images.load(str(src/'game-export/pine/CE_pine_0-baseColor.png'),check_existing=True)
w,h=image.size;pixels=np.empty(w*h*4,dtype=np.float32);image.pixels.foreach_get(pixels);pixels=pixels.reshape(h,w,4)
loop=np.empty(len(candidate.data.loops),dtype=np.int32);candidate.data.loops.foreach_get('vertex_index',loop)
uv=np.empty(len(candidate.data.loops)*2,dtype=np.float32);candidate.data.uv_layers.active.data.foreach_get('uv',uv);uv=uv.reshape(-1,2)
x=(np.mod(uv[:,0],1)*w).astype(np.int32);y=(np.mod(uv[:,1],1)*h).astype(np.int32);colors=pixels[y,x,:3]
wood=(colors[:,0]>colors[:,1]*1.05)&(colors[:,0]>colors[:,2]*1.10)&(colors[:,0]>.005)
totals=np.bincount(loop,minlength=len(candidate.data.vertices));votes=np.bincount(loop,weights=wood,minlength=len(candidate.data.vertices))
wood_vertices=votes/np.maximum(totals,1)>.5
protected=[v.index for v in candidate.data.vertices if v.co.z<4.6 or wood_vertices[v.index]]
group=candidate.vertex_groups.new(name='Protect source trunk and all wood-coloured branches')
group.add(protected,1,'REPLACE')
bpy.ops.object.select_all(action='DESELECT');candidate.select_set(True);bpy.context.view_layer.objects.active=candidate
modifier=candidate.modifiers.new('Review preserve branch surfaces','DECIMATE')
ratio=.075
modifier.ratio=ratio;modifier.vertex_group=group.name;modifier.vertex_group_factor=10;modifier.invert_vertex_group=True
bpy.ops.object.modifier_apply(modifier=modifier.name)
for face in candidate.data.polygons: face.use_smooth=True
candidate.data.calc_loop_triangles()
candidate.data.calc_normals_split()
invalid={loop.index for loop in candidate.data.loops if loop.normal.length<1e-6}
if len(invalid)>100:raise RuntimeError('Unexpected number of zero normals')
for face in candidate.data.polygons:
    if any(i in invalid for i in face.loop_indices):
        if face.normal.length<1e-6:raise RuntimeError('Cannot repair degenerate face normal')
        face.use_smooth=False
if invalid:
    candidate.data.update()
    candidate.data.calc_normals_split()
    if any(loop.normal.length<1e-6 for loop in candidate.data.loops):raise RuntimeError('Normal repair failed')
if not candidate.data.uv_layers.active:raise RuntimeError('Atlas lost')
for obj in bpy.data.objects:
    if obj.type=='MESH':obj.hide_render=obj!=candidate
candidate.hide_set(False)
bpy.ops.wm.save_as_mainfile(filepath=str(output),compress=True)
bpy.ops.export_scene.fbx(filepath=str(export),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',
    bake_anim=False,add_leaf_bones=False,path_mode='STRIP',use_mesh_modifiers=True)
if digest(source)!=original_hash:raise RuntimeError('Source changed')
report={'scope':'CPU candidate; sampled surface and actual Unity gameplay remain to be checked',
    'source':'../pine/pine.blend','protectedVertices':len(protected),'vertices':len(candidate.data.vertices),
    'triangles':len(candidate.data.loop_triangles),'nonmanifoldEdgesSplit':len(nonmanifold),'decimateRatio':ratio,
    'zeroNormalsRepaired':len(invalid),
    'sourceSha256':original_hash,'candidateSha256':digest(output),'exportSha256':digest(export)}
(destination/'generation-report.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
print('PINE_PRESERVE_BRANCHES_PASS',json.dumps(report),flush=True)
