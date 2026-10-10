"""Preserve an editable lower-detail running-body LOD and export only its visuals."""
from pathlib import Path
import hashlib
import json
import bpy

HERE=Path(__file__).resolve().parent
PROJECT=HERE.parents[1]
p=HERE/'model-manifest.json';manifest=json.loads(p.read_text(encoding='utf-8'))
record=next(item for item in manifest['assets'] if item['key']=='wraith-body')
bpy.context.preferences.filepaths.save_version=0
bpy.ops.wm.open_mainfile(filepath=str(PROJECT/record['source']))
scene=bpy.context.scene
original=[obj for obj in scene.objects if obj.type=='MESH' and not obj.name.startswith('SCULPT HIGH |')]
collection=bpy.data.collections.new('EDITABLE LOW DETAIL BODY LOD — independently exported')
scene.collection.children.link(collection)
copies=[]
for obj in original:
    copy=obj.copy();copy.data=obj.data.copy();copy.name='LOD1 | '+obj.name;collection.objects.link(copy)
    bpy.ops.object.select_all(action='DESELECT');copy.select_set(True);bpy.context.view_layer.objects.active=copy
    dec=copy.modifiers.new('Far-view topology simplification preserves UV atlas','DECIMATE');dec.ratio=.38
    bpy.ops.object.modifier_apply(modifier=dec.name);copy.data.calc_loop_triangles();copies.append(copy)
empties=[obj for obj in scene.objects if obj.type=='EMPTY']
bpy.ops.object.select_all(action='DESELECT')
for obj in copies+empties:obj.select_set(True)
bpy.context.view_layer.objects.active=empties[0]
fbx=PROJECT/'Assets/Resources/MaskHorror/wraith-body-lod1.fbx'
bpy.ops.export_scene.fbx(filepath=str(fbx),use_selection=True,object_types={'MESH','EMPTY'},
    apply_unit_scale=True,global_scale=1,axis_forward='-Z',axis_up='Y',use_mesh_modifiers=True,
    mesh_smooth_type='FACE',use_tspace=True,add_leaf_bones=False,bake_anim=False,path_mode='RELATIVE')
for obj in copies:obj.hide_render=True;obj.hide_set(True)
bpy.ops.object.select_all(action='DESELECT')
for obj in original:obj.select_set(True)
bpy.context.view_layer.objects.active=original[0]
bpy.ops.wm.save_as_mainfile(filepath=str(PROJECT/record['source']),compress=True)
record['sourceSha256']=hashlib.sha256((PROJECT/record['source']).read_bytes()).hexdigest()
record['lod1']=dict(fbx=fbx.relative_to(PROJECT).as_posix(),fbxSha256=hashlib.sha256(fbx.read_bytes()).hexdigest(),
    triangles=sum(len(obj.data.loop_triangles) for obj in copies),meshParts=[obj.name for obj in copies],
    source=record['source'],preserves='Four physical movement pivots, authored units, baked UV atlas and silhouette')
p.write_text(json.dumps(manifest,indent=2)+'\n',encoding='utf-8')
print('MASK_BODY_LOD_SOURCE_PASS',record['lod1']['triangles'],flush=True)
