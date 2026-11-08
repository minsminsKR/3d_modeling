from pathlib import Path
import bpy, numpy as np, json, sys, hashlib
folder=Path(sys.argv[sys.argv.index('--')+1]).resolve(); key='temple-stepping-stone-long-20261108'; out=[]
for i,expected in enumerate((5999,1999,599)):
 bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
 path=folder/(key+'-rebaked-lod%d.fbx'%i);bpy.ops.import_scene.fbx(filepath=str(path))
 meshes=[o for o in bpy.context.scene.objects if o.type=='MESH'];assert len(meshes)==1; obj=meshes[0];mesh=obj.data;mesh.calc_loop_triangles();assert len(mesh.loop_triangles)==expected
 assert mesh.uv_layers.active is not None; uv=np.empty(len(mesh.loops)*2,dtype=np.float32);mesh.uv_layers.active.data.foreach_get('uv',uv);norm=np.empty(len(mesh.vertices)*3,dtype=np.float32);mesh.vertices.foreach_get('normal',norm);assert np.isfinite(uv).all() and np.isfinite(norm).all();lens=np.linalg.norm(norm.reshape((-1,3)),axis=1);assert (lens>0.99).all();assert uv.min()>=-0.0001 and uv.max()<=1.0001
 out.append({'lod':i,'triangles':len(mesh.loop_triangles),'uvLoops':len(mesh.loops),'uvMin':float(uv.min()),'uvMax':float(uv.max()),'minimumNormalLength':float(lens.min())})
(folder/'fbx-reimport-proof.json').write_text(json.dumps(out,indent=2)+'\n',encoding='utf-8');print('REBAKED_FBX_REIMPORT_OK',json.dumps(out),flush=True)
