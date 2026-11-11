"""Estimate how many imported triangle+UV records survive the cut unchanged.

This is not a substitute for visual/material review. Quantization is 1e-5.
"""
from pathlib import Path
import bpy
import numpy as np

work = Path(__file__).resolve().parent
repo = next(p for p in (work, *work.parents) if (p / 'HappyToyV2/Assets').is_dir())
paths = [repo / 'HappyToyV2/Assets/Resources/AlleyStorefront/geometry.fbx',
         work / 'Storefront-door-candidate.fbx']

def triangle_records(path):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(path))
    bpy.context.view_layer.update()
    obj=next(o for o in bpy.context.scene.objects if o.type=='MESH')
    mesh=obj.data; mesh.calc_loop_triangles()
    if not mesh.uv_layers: raise RuntimeError('UV layer missing')
    vertex_coords=np.empty(len(mesh.vertices)*3,dtype=np.float32)
    mesh.vertices.foreach_get('co',vertex_coords); vertex_coords=vertex_coords.reshape(-1,3)
    matrix=np.array(obj.matrix_world,dtype=np.float64)
    vertex_coords=vertex_coords @ matrix[:3,:3].T + matrix[:3,3]
    tri_vertices=np.empty(len(mesh.loop_triangles)*3,dtype=np.int32)
    tri_loops=np.empty(len(mesh.loop_triangles)*3,dtype=np.int32)
    mesh.loop_triangles.foreach_get('vertices',tri_vertices)
    mesh.loop_triangles.foreach_get('loops',tri_loops)
    uv=np.empty(len(mesh.loops)*2,dtype=np.float32)
    mesh.uv_layers.active.data.foreach_get('uv',uv); uv=uv.reshape(-1,2)
    pairs=np.concatenate((vertex_coords[tri_vertices],uv[tri_loops]),axis=1).reshape(-1,3,5)
    pairs=np.rint(pairs*100000).astype(np.int32)
    order=np.lexsort((pairs[:,:,4],pairs[:,:,3],pairs[:,:,2],pairs[:,:,1],pairs[:,:,0]),axis=1)
    canonical=np.take_along_axis(pairs,order[:,:,None],axis=1)
    records=np.ascontiguousarray(canonical.reshape(len(canonical),15)).view(np.dtype((np.void,60))).reshape(-1)
    print('TRIANGLES',path.name,len(records),flush=True)
    return np.unique(records)

original=triangle_records(paths[0]); candidate=triangle_records(paths[1])
matched=np.intersect1d(original,candidate,assume_unique=True).size
print('UV_AUDIT original_unique',len(original),'candidate_unique',len(candidate),
      'matching',matched,'candidate_mismatch',len(candidate)-matched,
      'matching_pct',round(100*matched/max(1,len(candidate)),5),flush=True)
