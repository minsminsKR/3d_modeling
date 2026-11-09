import bpy, math, json
from pathlib import Path
from mathutils import Matrix
root=Path(__file__).resolve().parents[2]
old=root/'HappyToyV2/SourceArt/PlayerHandAnimation/hand-animation.blend'
base=root/'HappyToyV2/SourceArt/AnatomicalHandsPrototype/base/human-base-meshes-bundle-v1.4.1/human_base_meshes_bundle.blend'
out=Path(__file__).resolve().parent
bpy.ops.wm.open_mainfile(filepath=str(old))
rig=next(o for o in bpy.context.scene.objects if o.type=='ARMATURE')
oldskin=next(o for o in bpy.context.scene.objects if o.type=='MESH' and any(m.type=='ARMATURE' for m in o.modifiers))
for p in rig.pose.bones: p.matrix_basis=Matrix.Identity(4)
rig.animation_data.action=None
with bpy.data.libraries.load(str(base),link=False) as (src,dst): dst.objects=['Hand  - Realistic']
hand=dst.objects[0]; bpy.context.scene.collection.objects.link(hand)
hand.name='Blender anatomical hand candidate';hand.location=(0,0,0);hand.rotation_euler=(math.pi,0,0)
# Translate source wrist near the game's wrist; keep source dimensions in metres.
hand.location.z=.030
bpy.ops.object.select_all(action='DESELECT');hand.select_set(True);bpy.context.view_layer.objects.active=hand
bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
# Evaluate authored multires geometry before skin transfer.
mod=hand.modifiers.get('Multires')
if mod: bpy.ops.object.modifier_apply(modifier=mod.name)
for poly in hand.data.polygons:poly.use_smooth=True
# Transfer the old surface's vertex weights by nearest surface interpolation.
bpy.ops.object.select_all(action='DESELECT');hand.select_set(True);oldskin.select_set(True);bpy.context.view_layer.objects.active=oldskin
bpy.ops.object.data_transfer(data_type='VGROUP_WEIGHTS',use_create=True,vert_mapping='POLYINTERP_NEAREST',layers_select_src='ALL',layers_select_dst='NAME',mix_mode='REPLACE')
bpy.context.view_layer.objects.active=hand
bpy.ops.object.vertex_group_normalize_all(group_select_mode='ALL',lock_active=False)
arm=hand.modifiers.new('Candidate skin','ARMATURE');arm.object=rig
hand.parent=rig
oldskin.hide_render=True;oldskin.hide_set(True)
weights=[sum(g.weight for g in v.groups) for v in hand.data.vertices]
report={'candidate':True,'baseVertices':len(hand.data.vertices),'faces':len(hand.data.polygons),'unweighted':sum(w<.999 for w in weights),'maxWeightError':max(abs(w-1) for w in weights),'textureReady':False,'forearmReady':False,'note':'Initial rigid alignment and nearest-surface weight transfer only; no runtime integration or visual quality claim.'}
(out/'prototype-report.json').write_text(json.dumps(report,indent=2),encoding='utf8')
bpy.ops.wm.save_as_mainfile(filepath=str(out/'hand-rig-prototype.blend'))
print('REPORT',json.dumps(report))

