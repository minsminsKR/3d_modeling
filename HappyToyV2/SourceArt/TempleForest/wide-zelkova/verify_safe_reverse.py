"""Identify LOD triangles whose interiors leave the generated source surface."""
import json
from pathlib import Path
import bpy
import numpy as np
from mathutils import Vector
from mathutils.bvhtree import BVHTree

root=Path(__file__).resolve().parent
bpy.ops.wm.open_mainfile(filepath=str(root/'wide-zelkova-safe.blend'))
source=next(o for o in bpy.data.collections['Complete unchanged webapp original'].objects if o.type=='MESH')
points=[source.matrix_world @ v.co for v in source.data.vertices]
lo=Vector(tuple(min(p[i] for p in points) for i in range(3)))
hi=Vector(tuple(max(p[i] for p in points) for i in range(3)))
scale=8/(hi.z-lo.z); centre=Vector(((lo.x+hi.x)*.5,(lo.y+hi.y)*.5,lo.z))
points=[(point-centre)*scale for point in points]
source.data.calc_loop_triangles()
tree=BVHTree.FromPolygons(points,[tuple(t.vertices) for t in source.data.loop_triangles],all_triangles=True)
results={}
for key in ('LOD0','LOD1'):
    obj=bpy.data.objects['CE_wide-zelkova-safe_'+key];mesh=obj.data;mesh.calc_loop_triangles()
    arr=np.empty(len(mesh.vertices)*3,np.float32);mesh.vertices.foreach_get('co',arr);arr=arr.reshape(-1,3)
    mat=np.asarray(obj.matrix_world,dtype=np.float32);arr=arr@mat[:3,:3].T+mat[:3,3]
    face=np.empty(len(mesh.loop_triangles)*3,np.int32);mesh.loop_triangles.foreach_get('vertices',face);face=face.reshape(-1,3)
    a,b,c=arr[face[:,0]],arr[face[:,1]],arr[face[:,2]]
    longest=np.stack((np.linalg.norm(a-b,axis=1),np.linalg.norm(b-c,axis=1),np.linalg.norm(c-a,axis=1)),axis=1).max(axis=1)
    candidates=np.where(longest>.25)[0];bad=[];distances=[];areas=[]
    for i in candidates:
        av,bv,cv=Vector(a[i]),Vector(b[i]),Vector(c[i])
        samples=((av+bv)*.5,(bv+cv)*.5,(cv+av)*.5,(av+bv+cv)/3)
        deviation=max(tree.find_nearest(p)[3] for p in samples)
        distances.append(deviation)
        areas.append(float(np.linalg.norm(np.cross(b[i]-a[i],c[i]-a[i]))*.5))
        if deviation>.03: bad.append((int(i),float(longest[i]),float(deviation)))
    values=np.asarray(distances);surface_areas=np.asarray(areas)
    results[key]={'sampledLongFaces':len(candidates),'badAbove3cm':len(bad),
        'above5cm':int((values>.05).sum()),'above10cm':int((values>.1).sum()),
        'areaAbove3cm':float(surface_areas[values>.03].sum()),'areaAbove5cm':float(surface_areas[values>.05].sum()),
        'areaAbove10cm':float(surface_areas[values>.1].sum()),'deviationP95':float(np.percentile(distances,95)),
        'deviationMax':float(max(distances)),'longestBad':sorted(bad,key=lambda x:x[1],reverse=True)[:20]}
    print('ARCH_SAFE_STRAYS',key,json.dumps(results[key]),flush=True)
(root/'safe-reverse-proof.json').write_text(json.dumps(results,indent=2)+'\n',encoding='utf-8')
if any(result['badAbove3cm'] for result in results.values()):
    raise RuntimeError('A sampled long triangle lies more than 3 cm from the source')
