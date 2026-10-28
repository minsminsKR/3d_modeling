"""Create source-preserving skinned LODs from the actual A100 creature rig.
Run with Blender 4.0.2 in background; the originals are never modified.
"""
from pathlib import Path
import bpy, json

HERE=Path(__file__).resolve().parent
PROJECT=HERE.parents[1]
source=HERE/'Generated/rigged.glb'
out=PROJECT/'Assets/Resources/SchoolFlickerCreature'
out.mkdir(parents=True,exist_ok=True)
if bpy.app.version[:3] != (4,0,2): raise RuntimeError('Blender 4.0.2 required')
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=str(source))
meshes=[x for x in bpy.context.scene.objects if x.type=='MESH' and x.vertex_groups]
rigs=[x for x in bpy.context.scene.objects if x.type=='ARMATURE']
if len(meshes)!=1 or len(rigs)!=1: raise RuntimeError('Unexpected source rig or skin layout')
original=meshes[0]; rig=rigs[0]
if len(rig.data.bones)!=52: raise RuntimeError('Unexpected source skeleton')
original.name='Original_Source'; original.hide_render=True; original.hide_set(True)
lods=[]
for index,budget in enumerate((120000,45000,15000)):
    mesh=original.copy(); mesh.data=original.data.copy(); bpy.context.collection.objects.link(mesh)
    mesh.name='Creature_LOD'+str(index); mesh.hide_set(False); mesh.hide_render=False
    bpy.context.view_layer.objects.active=mesh
    mesh.select_set(True)
    mod=mesh.modifiers.new('Source-derived collapse','DECIMATE'); mod.ratio=budget/len(mesh.data.polygons); mod.use_collapse_triangulate=True
    bpy.ops.object.modifier_apply(modifier=mod.name)
    mesh.select_set(False)
    if len(mesh.data.polygons)>budget*1.05: raise RuntimeError('LOD budget exceeded')
    lods.append(mesh)
# Keep full-resolution source and all image datablocks in the editable packed file.
bpy.ops.file.pack_all()
bpy.ops.wm.save_as_mainfile(filepath=str(HERE/'SchoolFlickerCreature.blend'))
bpy.ops.object.select_all(action='DESELECT')
rig.select_set(True)
for mesh in lods: mesh.select_set(True)
bpy.context.view_layer.objects.active=rig
bpy.ops.export_scene.fbx(filepath=str(out/'creature.fbx'),use_selection=True,object_types={'MESH','ARMATURE'},axis_forward='-Z',axis_up='Y',global_scale=1,apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',add_leaf_bones=False,use_armature_deform_only=True,bake_anim=False,use_tspace=True,mesh_smooth_type='FACE',path_mode='COPY',embed_textures=True)
report={'source':'Generated/rigged.glb','bones':len(rig.data.bones),'lodFaces':[len(m.data.polygons) for m in lods]}
(out/'asset.json').write_text(json.dumps(report,indent=2)+'\n',encoding='utf-8')
print('CREATURE_EXPORT_PASS '+json.dumps(report),flush=True)
