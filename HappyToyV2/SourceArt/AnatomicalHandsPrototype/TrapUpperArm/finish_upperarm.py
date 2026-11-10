import bpy,bmesh,sys,math,json
from mathutils import Vector
from pathlib import Path
work=Path(sys.argv[sys.argv.index('--')+1]).resolve()
bpy.ops.wm.open_mainfile(filepath=str(work/'upper-arm-candidate.blend'))
o=bpy.data.objects['GEO-body_male_realistic'];mesh=o.data
ring=[v.co for v in mesh.vertices if v.co.z<.002]
cx=sum(v.x for v in ring)/len(ring);cy=sum(v.y for v in ring)/len(ring)
print('RING CENTER',cx,cy,'n',len(ring))
for v in mesh.vertices:
 v.co.x-=cx;v.co.y-=cy
 t=max(0,min(1,v.co.z/.065));scale=.86+.14*t*t*(3-2*t);v.co.x*=scale;v.co.y*=scale
mesh.update()
bm=bmesh.new();bm.from_mesh(mesh)
bmesh.ops.bisect_plane(bm,geom=list(bm.verts)+list(bm.edges)+list(bm.faces),dist=.00001,plane_co=(0,0,.04),plane_no=(0,0,1),clear_inner=True,clear_outer=False)
bm.to_mesh(mesh);bm.free();mesh.update();mesh.calc_loop_triangles()
bpy.ops.wm.save_as_mainfile(filepath=str(work/'upper-arm-final.blend'))
print('FINISHED UPPER ARM')
