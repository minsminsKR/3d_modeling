"""Continuous Blender game-only surface reduction; source GLB is untouched."""
import argparse,json,sys,hashlib
from pathlib import Path
import bpy,bmesh,numpy as np
sys.path.insert(0,str(Path(__file__).resolve().parent))
from glb_io import read,accessor

p=argparse.ArgumentParser();p.add_argument('--creature',required=True);p.add_argument('--triangles',type=int,default=30000)
a=p.parse_args(sys.argv[sys.argv.index('--')+1:]);folder=Path(__file__).resolve().parent.parent/a.creature;source=folder/'source-original.glb'
bpy.ops.wm.read_factory_settings(use_empty=True);bpy.ops.import_scene.gltf(filepath=str(source),merge_vertices=False)
o=next(o for o in bpy.context.scene.objects if o.type=='MESH');bpy.context.view_layer.objects.active=o
bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
height=max(v.co.z for v in o.data.vertices)-min(v.co.z for v in o.data.vertices)
# Reconstruct the outer game volume to remove overlapping micro-sheets before collapse.
o.data.remesh_voxel_size=height/650
o.data.remesh_voxel_adaptivity=0
bpy.ops.object.voxel_remesh()
print('VOXEL_GAME_SURFACE',len(o.data.vertices),flush=True)
mesh=bmesh.new();mesh.from_mesh(o.data);bmesh.ops.remove_doubles(mesh,verts=list(mesh.verts),dist=height*1e-5)
seen=set();duplicates=[]
for face in mesh.faces:
    key=frozenset(face.verts)
    if key in seen or face.calc_area()<1e-15:duplicates.append(face)
    else:seen.add(key)
if duplicates:bmesh.ops.delete(mesh,geom=duplicates,context='FACES_ONLY')
bmesh.ops.dissolve_degenerate(mesh,edges=list(mesh.edges),dist=height*1e-5)
bmesh.ops.recalc_face_normals(mesh,faces=list(mesh.faces));mesh.to_mesh(o.data);mesh.free()
for layer in list(o.data.uv_layers):o.data.uv_layers.remove(layer)
o.data.calc_loop_triangles();count=len(o.data.loop_triangles)
print('BLENDER_CONTINUOUS_REDUCTION',len(o.data.vertices),count,flush=True)
m=o.modifiers.new('Continuous game surface only','DECIMATE');m.ratio=min(1,a.triangles/count);m.use_collapse_triangulate=True
bpy.ops.object.modifier_apply(modifier=m.name);o.data.validate(clean_customdata=True);o.data.calc_loop_triangles()
points=np.empty(len(o.data.vertices)*3,dtype='f4');o.data.vertices.foreach_get('co',points);points=points.reshape(-1,3)[:,[0,2,1]];points[:,2]*=-1
faces=np.empty(len(o.data.loop_triangles)*3,dtype='i4');o.data.loop_triangles.foreach_get('vertices',faces);faces=faces.reshape(-1,3)
if len(faces)>100000 or len(faces)==0:raise ValueError('Actual reduction exceeds game limit')
np.savez_compressed(folder/'game-surface.npz',positions=points,triangles=faces)
doc,b=read(source);v=accessor(doc,b,doc['meshes'][0]['primitives'][0]['attributes']['POSITION'])
report=dict(source=source.name,sourceSHA256=hashlib.sha256(source.read_bytes()).hexdigest(),
    tool='Blender 4.0.2 game-only voxel outer surface and collapse',sourceBoundsGLTF=[v.min(0).tolist(),v.max(0).tolist()],
    originalVertices=len(v),gameVertices=len(points),gameTriangles=len(faces),originalUnchanged=True)
(folder/'game-surface.json').write_text(json.dumps(report,indent=2),encoding='utf8');print(json.dumps(report),flush=True)