import bpy,bmesh,json
from mathutils.bvhtree import BVHTree
from pathlib import Path
p=Path(__file__).resolve().parent;bpy.ops.wm.open_mainfile(filepath=str(p/'hand-rig-prototype-v4.blend'))
h=bpy.data.objects['Blender anatomical hand candidate'];rig=next(o for o in bpy.context.scene.objects if o.type=='ARMATURE')
bm=bmesh.new();bm.from_mesh(h.data)
meshinfo={'boundaryEdges':sum(e.is_boundary for e in bm.edges),'nonManifoldEdges':sum(not e.is_manifold and not e.is_boundary for e in bm.edges),'looseVertices':sum(not v.link_faces for v in bm.verts)};bm.free()
results={}
for a in ['Grip_Open','Grip_Flashlight','Grip_Snapper','Grip_Incense','Grip_Talisman','Grip_Trap']:
 rig.animation_data.action=bpy.data.actions[a];bpy.context.scene.frame_set(1);bpy.context.view_layer.update()
 ev=h.evaluated_get(bpy.context.evaluated_depsgraph_get());m=ev.to_mesh(); b=bmesh.new();b.from_mesh(m);tree=BVHTree.FromBMesh(b)
 d={}
 for digit in ('thumb','index','middle','ring','little'):
  tip=rig.pose.bones[digit+'_tip'].head
  nearest=tree.find_nearest(tip);d[digit]=round(nearest[3],5) if nearest else None
 b.free();ev.to_mesh_clear();results[a]=d
report={'mesh':meshinfo,'tipToSkinMetres':results,'meaning':'Nearest surface is a coarse correspondence signal; not proof of contact, shape quality, or absence of intersections.'}
(p/'pose-audit.json').write_text(json.dumps(report,indent=2),encoding='utf8');print(json.dumps(report))
