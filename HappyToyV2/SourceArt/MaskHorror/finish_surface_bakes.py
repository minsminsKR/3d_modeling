"""Clean direct-surface color/AO/roughness bakes; retain sculpted tangent normals.

Extreme torn shell edges can miss a selected-high color bake ray. Color,
roughness and AO are consequently baked directly from the same original
procedural sculpt materials on the atlas low surface. Only pure-black failed
normal ray texels are neutralized; no fabricated normal detail replaces them.
"""
from pathlib import Path
import argparse
import hashlib
import json
import sys
import bpy
import numpy as np

HERE=Path(__file__).resolve().parent;PROJECT=HERE.parents[1]
sys.path.insert(0,str(HERE))
from sculpt_and_bake import shader_sculpt,select,SIZE
parser=argparse.ArgumentParser();parser.add_argument('--color-only',action='store_true')
args=parser.parse_args(sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else [])

p=HERE/'model-manifest.json';manifest=json.loads(p.read_text(encoding='utf-8'))
bpy.context.preferences.filepaths.save_version=0
for record in manifest['assets']:
    key=record['key'];bpy.ops.wm.open_mainfile(filepath=str(PROJECT/record['source']))
    scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.samples=8
    scene.render.bake.use_pass_direct=False;scene.render.bake.use_pass_indirect=False;scene.render.bake.use_pass_color=True
    meshes=[obj for obj in scene.objects if obj.type=='MESH' and not obj.name.startswith(('SCULPT HIGH |','LOD1 |'))]
    mats={m for obj in meshes for m in obj.data.materials if m}
    for mat in mats:shader_sculpt(mat)
    by_kind={}
    stages=[('albedo','DIFFUSE')] if args.color_only else [('albedo','DIFFUSE'),('ao','AO'),('roughness','ROUGHNESS')]
    for kind,typ in stages:
        image=bpy.data.images.new(key+' clean '+kind+' direct surface bake',width=SIZE,height=SIZE,alpha=True)
        image.colorspace_settings.name='sRGB' if kind=='albedo' else 'Non-Color'
        for mat in mats:
            node=mat.node_tree.nodes.new('ShaderNodeTexImage');node.image=image;mat.node_tree.nodes.active=node
        first=True
        for obj in meshes:
            select([obj]);bpy.ops.object.bake(type=typ,use_selected_to_active=False,margin=16,use_clear=first);first=False
        image.filepath_raw=str(PROJECT/next(t['path'] for t in record['textures'] if t['kind']==kind))
        image.file_format='PNG';image.save();by_kind[kind]=image
        print('MASK_DIRECT_SURFACE_BAKE',key,kind,flush=True)
    normal_path=PROJECT/next(t['path'] for t in record['textures'] if t['kind']=='normal')
    normal=bpy.data.images.load(str(normal_path),check_existing=False);normal.colorspace_settings.name='Non-Color'
    values=np.empty(SIZE*SIZE*4,dtype=np.float32);normal.pixels.foreach_get(values);values=values.reshape((-1,4))
    misses=values[:,:3].sum(axis=1)<.025;values[misses,:3]=(.5,.5,1);values[misses,3]=1
    normal.pixels.foreach_set(values.ravel());normal.filepath_raw=str(normal_path);normal.file_format='PNG';normal.save()
    by_kind['normal']=normal
    if 'roughness' not in by_kind:
        rough_path=PROJECT/next(t['path'] for t in record['textures'] if t['kind']=='roughness')
        by_kind['roughness']=bpy.data.images.load(str(rough_path),check_existing=False)
        by_kind['roughness'].colorspace_settings.name='Non-Color'
    rough=by_kind['roughness'];values=np.empty(SIZE*SIZE*4,dtype=np.float32);rough.pixels.foreach_get(values)
    values=values.reshape((-1,4));packed=np.zeros_like(values);packed[:,3]=1-values[:,0]
    smooth=bpy.data.images.new(key+' clean packed smoothness',width=SIZE,height=SIZE,alpha=True)
    smooth.colorspace_settings.name='Non-Color';smooth.pixels.foreach_set(packed.ravel())
    smooth.filepath_raw=str(PROJECT/next(t['path'] for t in record['textures'] if t['kind']=='metallic-smoothness'))
    smooth.file_format='PNG';smooth.save()
    for mat in mats:
        shader=next(n for n in mat.node_tree.nodes if n.type=='BSDF_PRINCIPLED')
        for kind in ('albedo','normal','roughness'):
            node=mat.node_tree.nodes.new('ShaderNodeTexImage');node.image=by_kind[kind]
            if kind=='normal':
                normal_node=mat.node_tree.nodes.new('ShaderNodeNormalMap');normal_node.inputs['Strength'].default_value=1
                mat.node_tree.links.new(node.outputs[0],normal_node.inputs[1]);mat.node_tree.links.new(normal_node.outputs[0],shader.inputs['Normal'])
            else:mat.node_tree.links.new(node.outputs[0],shader.inputs['Base Color' if kind=='albedo' else 'Roughness'])
    bpy.ops.wm.save_as_mainfile(filepath=str(PROJECT/record['source']),compress=True)
    bpy.ops.file.make_paths_relative();bpy.ops.wm.save_as_mainfile(filepath=str(PROJECT/record['source']),compress=True)
    record['sourceSha256']=hashlib.sha256((PROJECT/record['source']).read_bytes()).hexdigest()
    for texture in record['textures']:texture['sha256']=hashlib.sha256((PROJECT/texture['path']).read_bytes()).hexdigest()
    record['bake']='Actual Cycles atlas diffuse-color/AO/roughness surface bake; multires sculpt selected-to-active tangent normals'
    record['normalMissPolicy']='Only pure-black ray misses/background neutralized; all valid sculpt normal texels retained'
    print('MASK_SURFACE_FINISH_PASS',key,int(misses.sum()),flush=True)
manifest['materialContract']={'all':'Original procedural skin/porcelain shaders baked to the per-resource tracked UV-atlas maps; MW_eye retains recessed red surface emission'}
p.write_text(json.dumps(manifest,indent=2)+'\n',encoding='utf-8')
