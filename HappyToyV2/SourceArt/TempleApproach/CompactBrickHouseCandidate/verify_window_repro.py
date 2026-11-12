from pathlib import Path
import bpy, hashlib, numpy as np
root = Path(__file__).resolve().parent
project = root.parents[2]
paths = [project / 'Assets/Resources/AlleyCompactBrickHouse/geometry-mid-window.fbx',
         root / 'CompactHouseMidWindow.fbx']
def describe(path):
 bpy.ops.wm.read_factory_settings(use_empty=True)
 bpy.ops.import_scene.fbx(filepath=str(path))
 result={}
 for o in bpy.context.scene.objects:
  if o.type!='MESH':continue
  m=o.data;m.calc_loop_triangles()
  v=np.empty(len(m.vertices)*3,dtype=np.float32);m.vertices.foreach_get('co',v)
  f=np.empty(len(m.loop_triangles)*3,dtype=np.int32);m.loop_triangles.foreach_get('vertices',f)
  uv=np.empty(len(m.loops)*2,dtype=np.float32);m.uv_layers.active.data.foreach_get('uv',uv)
  h=hashlib.sha256(v.tobytes()+f.tobytes()+uv.tobytes()).hexdigest()
  result[o.name]=(len(m.vertices),len(m.loop_triangles),h)
 return result
a,b=map(describe,paths)
print('ASSET',a,'REBUILT',b,'GEOMETRY_IDENTICAL',a==b)
if a!=b:raise RuntimeError('Rebuilt geometry differs')
