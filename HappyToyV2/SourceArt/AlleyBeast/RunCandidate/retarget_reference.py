import bpy,math
from pathlib import Path
from mathutils import Vector,Matrix
import tempfile, zipfile
root=Path(__file__).resolve().parent
source=root.parent/'AlleyDogRiggedEditable.blend'
work=Path(tempfile.mkdtemp(prefix='dog-run-ref-'))
with zipfile.ZipFile(root/'reference-dog-cc0.zip') as archive: archive.extractall(work)
bpy.ops.wm.open_mainfile(filepath=str(work/'low res dog/low res scary dog.blend'))
ref=next(o for o in bpy.context.scene.objects if o.type=='MESH');rig=next(o for o in bpy.context.scene.objects if o.type=='ARMATURE');rig.data.pose_position='REST';bpy.context.view_layer.update()
# Repair the reference source's absent left-foot weights by reflecting the
# authored right-hand side. Keep the source file itself unchanged.
from mathutils.kdtree import KDTree
neg=[v for v in ref.data.vertices if v.co.x < -.00001]
tree=KDTree(len(neg))
for i,v in enumerate(neg):tree.insert(v.co,i)
tree.balance()
for vertex in ref.data.vertices:
 if vertex.co.x <= .00001:continue
 co,idx,dist=tree.find(Vector((-vertex.co.x,vertex.co.y,vertex.co.z)))
 if dist > .0001:continue
 srcv=neg[idx]; weights=[]
 for g in srcv.groups:
  name=ref.vertex_groups[g.group].name
  if '_R' in name:name=name.replace('_R','_L')
  elif '_L' in name:name=name.replace('_L','_R')
  weights.append((name,g.weight))
 for group in ref.vertex_groups:
  try:group.remove([vertex.index])
  except RuntimeError:pass
 for name,weight in weights:
  if name in ref.vertex_groups:ref.vertex_groups[name].add([vertex.index],weight,'REPLACE')
with bpy.data.libraries.load(str(source),link=False) as (src,dst):
 dst.objects=[n for n in src.objects if n=='geometry_0']
dog=dst.objects[0];bpy.context.scene.collection.objects.link(dog);dog.parent=None
for m in list(dog.modifiers):dog.modifiers.remove(m)
dog.vertex_groups.clear();bpy.context.view_layer.objects.active=dog;dog.select_set(True)
mod=dog.modifiers.new('face budget','DECIMATE');mod.ratio=120000/len(dog.data.polygons);bpy.ops.object.modifier_apply(modifier=mod.name);dog.select_set(False)
def bounds(o):
 points=[o.matrix_world@Vector(c) for c in o.bound_box];return Vector(tuple(min(p[i] for p in points) for i in range(3))),Vector(tuple(max(p[i] for p in points) for i in range(3)))
rot=Matrix.Rotation(-math.pi/2,4,'Z'); q=[rot@ref.matrix_world@Vector(c) for c in ref.bound_box];rlo=Vector(tuple(min(p[i] for p in q) for i in range(3)));rhi=Vector(tuple(max(p[i] for p in q) for i in range(3)));dlo,dhi=bounds(dog)
scale=Vector(tuple((dhi[i]-dlo[i])/(rhi[i]-rlo[i]) for i in range(3)))
M=Matrix.Translation((dlo+dhi)/2) @ Matrix.Diagonal((scale.x,scale.y,scale.z,1)) @ Matrix.Translation(-(rlo+rhi)/2) @ rot
# Transform the reference armature; its child mesh follows automatically.
carrier=bpy.data.objects.new('Canine motion alignment',None);bpy.context.scene.collection.objects.link(carrier);carrier.matrix_world=M;rig.parent=carrier;rig.matrix_parent_inverse=Matrix.Identity(4);bpy.context.view_layer.update()
for g in ref.vertex_groups:dog.vertex_groups.new(name=g.name)
mod=dog.modifiers.new('Reference dog skin transfer','DATA_TRANSFER');mod.object=ref;mod.use_vert_data=True;mod.data_types_verts={'VGROUP_WEIGHTS'};mod.vert_mapping='POLYINTERP_NEAREST';mod.layers_vgroup_select_src='ALL';mod.layers_vgroup_select_dst='NAME'
bpy.context.view_layer.objects.active=dog;dog.select_set(True);bpy.ops.object.modifier_apply(modifier=mod.name);dog.select_set(False)
print('TRANSFER',len(dog.data.vertices),'unweighted',sum(not v.groups for v in dog.data.vertices),flush=True)
mod=dog.modifiers.new('Reference canine rig','ARMATURE');mod.object=rig;rig.data.pose_position='POSE';ref.hide_render=True;ref.hide_viewport=True
bpy.context.scene.frame_start=1;bpy.context.scene.frame_end=23;bpy.context.scene.frame_set(1)
bpy.ops.file.pack_all()
bpy.ops.wm.save_as_mainfile(filepath=str(root/'retarget-intermediate.blend'))
