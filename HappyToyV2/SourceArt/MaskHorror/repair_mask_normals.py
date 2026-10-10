"""One-time read-only diagnosis plus genuine outward shell normal repair/rebake.

The main generator contains the corrected front/back winding. Fresh authoring
does not need this repair; it is retained as an explicit editable-source audit.
"""
from pathlib import Path
import hashlib
import json
import sys
import bmesh
import bpy
import numpy as np

HERE=Path(__file__).resolve().parent;PROJECT=HERE.parents[1]
sys.path.insert(0,str(HERE))
from sculpt_and_bake import select,source_high,shader_sculpt,SIZE

p=HERE/'model-manifest.json';manifest=json.loads(p.read_text(encoding='utf-8'))
record=next(item for item in manifest['assets'] if item['key']=='wraith-mask')
bpy.context.preferences.filepaths.save_version=0
bpy.ops.wm.open_mainfile(filepath=str(PROJECT/record['source']))
scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.samples=8
meshes=[obj for obj in scene.objects if obj.type=='MESH' and not obj.name.startswith('SCULPT HIGH |')]
shell=next(obj for obj in meshes if obj.name.startswith('Cracked porcelain shell'))
bm=bmesh.new();bm.from_mesh(shell.data);before=bm.calc_volume(signed=True)
bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));after=bm.calc_volume(signed=True)
assert after>0,'Shell must enclose a positive outward-oriented solid thickness'
bm.to_mesh(shell.data);bm.free();shell.data.update()
old=next(obj for obj in scene.objects if obj.name=='SCULPT HIGH | '+shell.name)
collection=old.users_collection[0];bpy.data.objects.remove(old,do_unlink=True)
high=source_high(shell,collection);high.hide_render=False;high.hide_set(False)
for mat in shell.data.materials:shader_sculpt(mat)
normal_path=PROJECT/next(t['path'] for t in record['textures'] if t['kind']=='normal')
normal=bpy.data.images.load(str(normal_path),check_existing=False);normal.colorspace_settings.name='Non-Color'
for mat in shell.data.materials:
    node=mat.node_tree.nodes.new('ShaderNodeTexImage');node.image=normal;mat.node_tree.nodes.active=node
select([high,shell],shell)
bpy.ops.object.bake(type='NORMAL',use_selected_to_active=True,cage_extrusion=.012,max_ray_distance=.035,margin=16,use_clear=False)
values=np.empty(SIZE*SIZE*4,dtype=np.float32);normal.pixels.foreach_get(values);values=values.reshape((-1,4))
misses=values[:,:3].sum(axis=1)<.025;values[misses,:3]=(.5,.5,1);values[misses,3]=1
normal.pixels.foreach_set(values.ravel());normal.filepath_raw=str(normal_path);normal.file_format='PNG';normal.save()
high.hide_render=True;high.hide_set(True)
for mat in shell.data.materials:
    shader=next(n for n in mat.node_tree.nodes if n.type=='BSDF_PRINCIPLED')
    for kind in ('albedo','normal','roughness'):
        texture_path=PROJECT/next(t['path'] for t in record['textures'] if t['kind']==kind)
        image=normal if kind=='normal' else bpy.data.images.load(str(texture_path),check_existing=False)
        image.colorspace_settings.name='sRGB' if kind=='albedo' else 'Non-Color'
        node=mat.node_tree.nodes.new('ShaderNodeTexImage');node.image=image
        if kind=='normal':
            convert=mat.node_tree.nodes.new('ShaderNodeNormalMap');mat.node_tree.links.new(node.outputs[0],convert.inputs[1])
            mat.node_tree.links.new(convert.outputs[0],shader.inputs['Normal'])
        else:mat.node_tree.links.new(node.outputs[0],shader.inputs['Base Color' if kind=='albedo' else 'Roughness'])
runtime=meshes+[obj for obj in scene.objects if obj.type=='EMPTY'];select(runtime)
bpy.ops.export_scene.fbx(filepath=str(PROJECT/record['fbx']),use_selection=True,object_types={'MESH','EMPTY'},
    apply_unit_scale=True,global_scale=1,axis_forward='-Z',axis_up='Y',use_mesh_modifiers=True,
    mesh_smooth_type='FACE',use_tspace=True,add_leaf_bones=False,bake_anim=False,path_mode='RELATIVE')
bpy.ops.wm.save_as_mainfile(filepath=str(PROJECT/record['source']),compress=True)
bpy.ops.file.make_paths_relative();bpy.ops.wm.save_as_mainfile(filepath=str(PROJECT/record['source']),compress=True)
record['sourceSha256']=hashlib.sha256((PROJECT/record['source']).read_bytes()).hexdigest()
record['fbxSha256']=hashlib.sha256((PROJECT/record['fbx']).read_bytes()).hexdigest()
for texture in record['textures']:texture['sha256']=hashlib.sha256((PROJECT/texture['path']).read_bytes()).hexdigest()
record['outwardShellVolume']=after
p.write_text(json.dumps(manifest,indent=2)+'\n',encoding='utf-8')
print('MASK_OUTWARD_NORMAL_REPAIR_PASS',before,after,flush=True)
