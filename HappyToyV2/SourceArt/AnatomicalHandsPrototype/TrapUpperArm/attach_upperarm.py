import bpy,math,sys
from pathlib import Path
from mathutils import Vector,Matrix
work=Path(sys.argv[sys.argv.index('--')+1]).resolve()
base=Path(__file__).resolve().parent.parent/'hand-rig-corrective.blend'
bpy.ops.wm.open_mainfile(filepath=str(base))
h=bpy.data.objects['Blender anatomical hand candidate'];rig=next(o for o in bpy.context.scene.objects if o.type=='ARMATURE')
with bpy.data.libraries.load(str(work/'upper-arm-final.blend'),link=False) as (src,dst):dst.objects=['GEO-body_male_realistic']
arm=dst.objects[0];bpy.context.scene.collection.objects.link(arm);arm.name='Trap anatomical upper arm source';arm.location=(0,0,0);arm.rotation_euler=(0,0,0);arm.scale=(1,1,1)
for v in arm.data.vertices:v.co.z=-.26-v.co.z
arm.data.update()
# Reflection changes winding, so restore outward normals.
import bmesh
bm=bmesh.new();bm.from_mesh(arm.data);bmesh.ops.reverse_faces(bm,faces=list(bm.faces));bm.to_mesh(arm.data);bm.free();arm.data.update()
uv=arm.data.uv_layers.get('NewSkinUV') or arm.data.uv_layers.new(name='NewSkinUV')
for loop in arm.data.loops:
 c=arm.data.vertices[loop.vertex_index].co
 uv.data[loop.index].uv=(.100+.025*(c.x+.08)/.16,.90+.08*(c.y+.08)/.16)
for layer in list(arm.data.uv_layers):
 if layer!=uv:arm.data.uv_layers.remove(layer)
arm.data.uv_layers.active=uv
for face in arm.data.polygons:face.use_smooth=True
g=arm.vertex_groups.new(name='upperarm');g.add(list(range(len(arm.data.vertices))),1,'REPLACE')
# Add a free shoulder segment without changing the existing forearm parent or bind.
bpy.ops.object.select_all(action='DESELECT');rig.select_set(True);bpy.context.view_layer.objects.active=rig;bpy.ops.object.mode_set(mode='EDIT')
b=rig.data.edit_bones.new('upperarm');b.head=(0,0,-.26);b.tail=(0,0,-.455);b.align_roll(Vector((0,-1,0)))
bpy.ops.object.mode_set(mode='OBJECT')
# Preserve corrective shape keys and material slots on the hand when joining.
bpy.ops.object.select_all(action='DESELECT');h.select_set(True);arm.select_set(True);bpy.context.view_layer.objects.active=h
print('BEFORE',len(h.data.vertices),'shapes',len(h.data.shape_keys.key_blocks) if h.data.shape_keys else 0)
bpy.ops.object.join()
print('AFTER',len(h.data.vertices),'shapes',len(h.data.shape_keys.key_blocks) if h.data.shape_keys else 0,'uv',[x.name for x in h.data.uv_layers])
bm=bmesh.new();bm.from_mesh(h.data)
edgeLoops=[e for e in bm.edges if e.is_boundary and (all(abs(v.co.z+.26)<.0001 for v in e.verts) or all(abs(v.co.z+.30)<.0001 for v in e.verts))]
print('BRIDGE EDGES',len(edgeLoops))
result=bmesh.ops.bridge_loops(bm,edges=edgeLoops,use_pairs=True)
print('BRIDGE FACES',len(result['faces']))
uvLayer=bm.loops.layers.uv.get('NewSkinUV')
for face in result['faces']:
 face.smooth=True
 for loop in face.loops:
  p=loop.vert.co;loop[uvLayer].uv=(.100+.025*(p.x+.08)/.16,.90+.08*(p.y+.08)/.16)
bridgeEdges=[e for e in bm.edges if (abs(e.verts[0].co.z+.26)<.0001 and abs(e.verts[1].co.z+.30)<.0001) or (abs(e.verts[1].co.z+.26)<.0001 and abs(e.verts[0].co.z+.30)<.0001)]
print('BLEND EDGES',len(bridgeEdges))
if bridgeEdges:bmesh.ops.subdivide_edges(bm,edges=bridgeEdges,cuts=2,use_grid_fill=True)
bm.to_mesh(h.data);bm.free();h.data.update()
print('AFTERBRIDGE',len(h.data.vertices),'shape sizes',[len(x.data) for x in h.data.shape_keys.key_blocks])
rig.animation_data.action=None
for bone in rig.pose.bones:bone.matrix_basis=Matrix.Identity(4)
bpy.context.view_layer.update()
bpy.ops.wm.save_as_mainfile(filepath=str(work/'bridged-smooth-upper-arm.blend'))
uv=h.data.uv_layers.get('NewSkinUV')
for layer in list(h.data.uv_layers):
 if layer!=uv:h.data.uv_layers.remove(layer)
h.data.uv_layers.active=uv;uv.active_render=True
bpy.ops.object.select_all(action='DESELECT');h.select_set(True);rig.select_set(True);bpy.context.view_layer.objects.active=rig
bpy.ops.export_scene.fbx(filepath=str(work/'bridged-smooth-upper-arm.fbx'),use_selection=True,add_leaf_bones=False,bake_anim=False,axis_forward='-Z',axis_up='Y')
print('UNIFIED DONE')
