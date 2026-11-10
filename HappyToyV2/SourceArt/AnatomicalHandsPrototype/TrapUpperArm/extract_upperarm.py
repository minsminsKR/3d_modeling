import bpy,bmesh,math,collections,sys,zipfile,shutil
from mathutils import Vector
from pathlib import Path
work=Path(sys.argv[sys.argv.index('--')+1]).resolve();work.mkdir(parents=True,exist_ok=True)
source=Path(__file__).resolve().parent.parent/'blender-human-base-meshes-v1.4.1.zip'
p=work/'human_base_meshes_bundle.blend'
with zipfile.ZipFile(source) as archive, archive.open('human-base-meshes-bundle-v1.4.1/human_base_meshes_bundle.blend') as src, p.open('wb') as dst:
 shutil.copyfileobj(src,dst)
with bpy.data.libraries.load(str(p),link=False) as (src,dst):dst.objects=['GEO-body_male_realistic']
o=dst.objects[0];bpy.context.scene.collection.objects.link(o);o.location=(0,0,0);bpy.context.view_layer.objects.active=o;o.select_set(True)
for m in list(o.modifiers):
 if m.type=='MULTIRES':
  m.levels=1;bpy.ops.object.modifier_apply(modifier=m.name)
elbow=Vector((.33,-.012,1.015));shoulder=Vector((.2708,.01,1.20));direction=(shoulder-elbow).normalized();length=(shoulder-elbow).length
bm=bmesh.new();bm.from_mesh(o.data)
for point,normal,inner,outer in [(elbow,direction,True,False),(shoulder,direction,False,True),(Vector((.19,0,0)),Vector((1,0,0)),True,False)]:
 bmesh.ops.bisect_plane(bm,geom=list(bm.verts)+list(bm.edges)+list(bm.faces),dist=.00001,plane_co=point,plane_no=normal,clear_inner=inner,clear_outer=outer)
bm.verts.ensure_lookup_table();bm.faces.ensure_lookup_table()
print('CANDIDATE',len(bm.verts),len(bm.faces),'open edges',sum(e.is_boundary for e in bm.edges),'length',length)
components=[];unseen=set(bm.verts)
while unseen:
 seed=unseen.pop();q=[seed];group={seed}
 while q:
  a=q.pop()
  for e in a.link_edges:
   b=e.other_vert(a)
   if b in unseen:unseen.remove(b);group.add(b);q.append(b)
 components.append(group)
print('COMPONENTS',sorted([len(x) for x in components],reverse=True)[:20])
if len(components)>1:
 keep=max(components,key=len);bmesh.ops.delete(bm,geom=[x for x in bm.verts if x not in keep],context='VERTS')
xaxis=Vector((0,1,0)).cross(direction).normalized();yaxis=direction.cross(xaxis).normalized()
for v in bm.verts:
 d=v.co-elbow;v.co=Vector((d.dot(xaxis),d.dot(yaxis),d.dot(direction)))
bm.to_mesh(o.data);bm.free();o.data.update()
for poly in o.data.polygons:poly.use_smooth=True
for k in range(3):
 vals=[v.co[k] for v in o.data.vertices];print('AXIS',k,min(vals),max(vals))
bpy.ops.wm.save_as_mainfile(filepath=str(work/'upper-arm-candidate.blend'))
print('DONE')
