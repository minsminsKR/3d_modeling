import bpy,bmesh,json
from pathlib import Path
from mathutils import Vector
root=Path(__file__).resolve().parent
bpy.ops.wm.open_mainfile(filepath=str(root/'hand-rig-prototype.blend'))
h=bpy.data.objects['Blender anatomical hand candidate']; rig=next(o for o in bpy.context.scene.objects if o.type=='ARMATURE')
# Translate the little-finger chain to the new base's visible finger centerline.
bpy.ops.object.select_all(action='DESELECT');rig.select_set(True);bpy.context.view_layer.objects.active=rig
bpy.ops.object.mode_set(mode='EDIT')
for name in ('little_1','little_2','little_3','little_tip'):
 b=rig.data.edit_bones[name];b.head += Vector((.020,0,-.020));b.tail += Vector((.020,0,-.020))
bpy.ops.object.mode_set(mode='OBJECT')
# Trim the closed wrist cap and extend its boundary toward the elbow. This is a
# drafting mesh, not an approved anatomical surface or finalized UV layout.
bm=bmesh.new();bm.from_mesh(h.data)
cut=-.025
res=bmesh.ops.bisect_plane(bm,geom=list(bm.verts)+list(bm.edges)+list(bm.faces),dist=1e-6,plane_co=(0,0,cut),plane_no=(0,0,1),clear_inner=True,clear_outer=False)
boundary=[e for e in bm.edges if e.is_boundary and all(abs(v.co.z-cut)<1e-4 for v in e.verts)]
if len(boundary)<8: raise RuntimeError('Unexpected wrist boundary: '+str(len(boundary)))
for target,scale in [(-.10,1.08),(-.18,1.18),(-.26,1.25)]:
 ext=bmesh.ops.extrude_edge_only(bm,edges=boundary)
 verts=[x for x in ext['geom'] if isinstance(x,bmesh.types.BMVert)]
 prev=sum(v.co.z for v in verts)/len(verts)
 for v in verts: v.co.x*=scale;v.co.y*=scale;v.co.z=target
 boundary=[x for x in ext['geom'] if isinstance(x,bmesh.types.BMEdge) and all(v in verts for v in x.verts)]
 if len(boundary)<8: raise RuntimeError('Extrusion boundary lost')
bm.to_mesh(h.data);bm.free();h.data.update()
# Restore transfer across non-finger skin including newly extruded vertices.
old=bpy.data.objects['hand']
bpy.ops.object.select_all(action='DESELECT');h.select_set(True);old.select_set(True);bpy.context.view_layer.objects.active=old
bpy.ops.object.data_transfer(data_type='VGROUP_WEIGHTS',use_create=True,vert_mapping='POLYINTERP_NEAREST',layers_select_src='ALL',layers_select_dst='NAME',mix_mode='REPLACE')
# Distal little finger: its new axis is displaced from the old source; use
# explicit longitudinal blending on the isolated outer digit.
groups=[h.vertex_groups[n] for n in ('little_1','little_2','little_3')]
for v in h.data.vertices:
 x,y,z=v.co
 if x < -.041 and z > .142:
  for g in h.vertex_groups:
   try:g.remove([v.index])
   except RuntimeError:pass
  t=max(0,min(2,(z-.142)/.025)); i=min(2,int(t)); f=t-i
  groups[i].add([v.index],max(0.00001,1-f),'REPLACE')
  if i<2 and f>1e-5:groups[i+1].add([v.index],f,'REPLACE')
bpy.context.view_layer.objects.active=h
bpy.ops.object.vertex_group_normalize_all(group_select_mode='ALL',lock_active=False)
for p in h.data.polygons:p.use_smooth=True
weights=[sum(g.weight for g in v.groups) for v in h.data.vertices]
counts={g.name:sum(any(e.group==g.index and e.weight>.01 for e in v.groups) for v in h.data.vertices) for g in h.vertex_groups}
report={'vertices':len(h.data.vertices),'polygons':len(h.data.polygons),'unweighted':sum(w<.999 for w in weights),'maxError':max(abs(w-1) for w in weights),'maxInfluences':max(sum(g.weight>1e-6 for g in v.groups) for v in h.data.vertices),'littleCounts':{k:v for k,v in counts.items() if k.startswith('little_')},'draft':True,'skinTextureReady':False,'uvExtrusionReady':False,'contactVerified':False}
(root/'prototype-v2-report.json').write_text(json.dumps(report,indent=2),encoding='utf8')
bpy.ops.wm.save_as_mainfile(filepath=str(root/'hand-rig-prototype-v2.blend'))
print('REPORT',json.dumps(report))
