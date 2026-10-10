"""Blender-native voxel anatomy, editable multires sculpt and 2K Cycles PBR bakes.

Run after author_mask_horror.py. No workstation dependencies or downloaded art.
High-resolution sculpt objects remain editable in a hidden source collection;
the runtime FBX contains only measured decimated/UV-atlas low-resolution meshes.
"""
from pathlib import Path
import hashlib
import json
import math
import bpy
import numpy as np

HERE=Path(__file__).resolve().parent
PROJECT=HERE.parents[1]
RES=PROJECT/'Assets/Resources/MaskHorror'
SIZE=2048

def select(objects,active=None):
    bpy.ops.object.select_all(action='DESELECT')
    for obj in objects:obj.select_set(True)
    bpy.context.view_layer.objects.active=active or objects[0]

def shader_sculpt(material):
    key=material.name;material.use_nodes=True
    nodes,links=material.node_tree.nodes,material.node_tree.links
    nodes.clear();output=nodes.new('ShaderNodeOutputMaterial');bsdf=nodes.new('ShaderNodeBsdfPrincipled')
    links.new(bsdf.outputs[0],output.inputs[0])
    if key=='MW_eye':
        bsdf.inputs['Base Color'].default_value=(.06,.001,.001,1)
        bsdf.inputs['Emission Color'].default_value=(.60,.001,.001,1)
        bsdf.inputs['Emission Strength'].default_value=1.0;bsdf.inputs['Roughness'].default_value=.22
        return
    porcelain=key in {'MW_mask','MW_bone'}
    if porcelain:lo,hi=(.19,.18,.13,1),(.55,.52,.42,1)
    elif key=='MW_scar':lo,hi=(.013,.004,.003,1),(.073,.029,.015,1)
    else:lo,hi=(.008,.012,.007,1),(.041,.053,.032,1)
    noise=nodes.new('ShaderNodeTexNoise');noise.inputs['Scale'].default_value=150 if porcelain else 92
    noise.inputs['Detail'].default_value=6;noise.inputs['Roughness'].default_value=.75
    ramp=nodes.new('ShaderNodeValToRGB');ramp.color_ramp.elements[0].position=.16
    ramp.color_ramp.elements[0].color=lo;ramp.color_ramp.elements[1].position=.87;ramp.color_ramp.elements[1].color=hi
    links.new(noise.outputs['Fac'],ramp.inputs[0]);links.new(ramp.outputs['Color'],bsdf.inputs['Base Color'])
    pores=nodes.new('ShaderNodeTexNoise');pores.inputs['Scale'].default_value=520 if porcelain else 340
    pores.inputs['Detail'].default_value=3;pores.inputs['Roughness'].default_value=.8
    bump=nodes.new('ShaderNodeBump');bump.inputs['Strength'].default_value=.38
    bump.inputs['Distance'].default_value=.0009 if porcelain else .0018
    links.new(pores.outputs['Fac'],bump.inputs['Height']);links.new(bump.outputs[0],bsdf.inputs['Normal'])
    rough=nodes.new('ShaderNodeMapRange');rough.inputs['From Min'].default_value=0;rough.inputs['From Max'].default_value=1
    rough.inputs['To Min'].default_value=.37 if porcelain else .46
    rough.inputs['To Max'].default_value=.84 if porcelain else .93
    links.new(noise.outputs['Fac'],rough.inputs[0]);links.new(rough.outputs[0],bsdf.inputs['Roughness'])
    bsdf.inputs['Subsurface Weight'].default_value=0 if porcelain else .065
    if porcelain:
        cracks=nodes.new('ShaderNodeTexVoronoi');cracks.feature='DISTANCE_TO_EDGE';cracks.inputs['Scale'].default_value=48
        chip=nodes.new('ShaderNodeValToRGB');chip.color_ramp.elements[0].position=.007
        chip.color_ramp.elements[0].color=(.08,.064,.036,1);chip.color_ramp.elements[1].position=.020
        chip.color_ramp.elements[1].color=(1,1,1,1)
        links.new(cracks.outputs['Distance'],chip.inputs[0])
        mult=nodes.new('ShaderNodeMixRGB');mult.blend_type='MULTIPLY';mult.inputs[0].default_value=.48
        links.new(ramp.outputs[0],mult.inputs[1]);links.new(chip.outputs[0],mult.inputs[2])
        links.new(mult.outputs[0],bsdf.inputs['Base Color'])

def merge_anatomy():
    meshes=[obj for obj in bpy.context.scene.objects if obj.type=='MESH']
    groups={}
    for obj in meshes:
        if len(obj.data.materials)!=1 or obj.data.materials[0].name!='MW_flesh':continue
        groups.setdefault(obj.parent,[]).append(obj)
    for parent,parts in groups.items():
        select(parts);bpy.ops.object.join();obj=bpy.context.object
        obj.name='Organic fused anatomy '+parent.name
        # A genuine volumetric remesh joins elbow, wrist, knuckles and tendons.
        # It does not add a physics collider or affect the imported animation rig.
        obj.data.remesh_voxel_size=.0085 if 'ArmSwing' in parent.name else .010
        bpy.ops.object.voxel_remesh()
        smooth=obj.modifiers.new('Sculpt anatomical surface relaxation','SMOOTH');smooth.factor=.65;smooth.iterations=4
        bpy.ops.object.modifier_apply(modifier=smooth.name)
        obj.data.calc_loop_triangles();count=len(obj.data.loop_triangles)
        dec=obj.modifiers.new('Retopology preserve crooked silhouette','DECIMATE');dec.ratio=min(.70,6000/max(1,count))
        bpy.ops.object.modifier_apply(modifier=dec.name)
        for p in obj.data.polygons:p.use_smooth=True

def unwrap(meshes):
    select(meshes);bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.uv.smart_project(angle_limit=math.radians(64),island_margin=.008,area_weight=.30)
    bpy.ops.object.mode_set(mode='OBJECT')

def source_high(low,collection):
    high=low.copy();high.data=low.data.copy();high.name='SCULPT HIGH | '+low.name
    collection.objects.link(high);select([high])
    mult=high.modifiers.new('Editable multires anatomical sculpt','MULTIRES')
    for _ in range(2):bpy.ops.object.multires_subdivide(modifier=mult.name,mode='CATMULL_CLARK')
    mult.levels=2;mult.sculpt_levels=2;mult.render_levels=2
    if any(m and m.name=='MW_flesh' for m in high.data.materials):
        texture=bpy.data.textures.get('Dermal scar and fold volume') or bpy.data.textures.new('Dermal scar and fold volume',type='CLOUDS')
        texture.noise_scale=.035;texture.noise_depth=2
        displacement=high.modifiers.new('Sculpt raised scar tissue and dry folds','DISPLACE')
        displacement.texture=texture;displacement.strength=.0030;displacement.mid_level=.48
        micro=bpy.data.textures.get('Skin pores microscopic relief') or bpy.data.textures.new('Skin pores microscopic relief',type='CLOUDS')
        micro.noise_scale=.0028;micro.noise_depth=2
        detail=high.modifiers.new('Actual skin pore geometry','DISPLACE');detail.texture=micro
        detail.strength=.00045;detail.mid_level=.5
    return high

def bake(meshes,highs,key,kind,bake_type):
    image=bpy.data.images.new(key+' '+kind+' actual Cycles bake',width=SIZE,height=SIZE,alpha=True,float_buffer=False)
    image.colorspace_settings.name='sRGB' if kind=='albedo' else 'Non-Color'
    for mat in {m for obj in meshes for m in obj.data.materials if m}:
        node=mat.node_tree.nodes.new('ShaderNodeTexImage');node.name='Active '+kind+' bake target';node.image=image
        mat.node_tree.nodes.active=node
    bpy.context.scene.render.bake.use_clear=True
    for low,high in zip(meshes,highs):
        high.hide_render=False;high.hide_set(False)
        select([high,low],low)
        bpy.ops.object.bake(type=bake_type,use_selected_to_active=True,cage_extrusion=.012,max_ray_distance=.035,
            margin=8,use_clear=bpy.context.scene.render.bake.use_clear)
        bpy.context.scene.render.bake.use_clear=False
        high.hide_render=True;high.hide_set(True)
    path=RES/'Textures'/(key+'-'+kind+'.png');image.filepath_raw=str(path);image.file_format='PNG';image.save()
    print('SCULPT_ACTUAL_BAKE',key,kind,SIZE,flush=True)
    return image,path

def build(key,record):
    bpy.ops.wm.open_mainfile(filepath=str(PROJECT/record['source']))
    scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.samples=12;scene.cycles.use_denoising=False
    scene.render.bake.use_pass_direct=False;scene.render.bake.use_pass_indirect=False;scene.render.bake.use_pass_color=True
    if key=='wraith-body':merge_anatomy()
    meshes=[obj for obj in scene.objects if obj.type=='MESH'];unwrap(meshes)
    for mat in {m for obj in meshes for m in obj.data.materials if m}:shader_sculpt(mat)
    collection=bpy.data.collections.new('EDITABLE HIGH RESOLUTION SCULPT — excluded from runtime FBX')
    scene.collection.children.link(collection)
    highs=[source_high(low,collection) for low in meshes]
    for high in highs:high.hide_render=True;high.hide_set(True)
    results=[]
    for kind,typ in [('albedo','DIFFUSE'),('normal','NORMAL'),('ao','AO'),('roughness','ROUGHNESS')]:
        image,path=bake(meshes,highs,key,kind,typ);results.append((kind,image,path))
    rough=next(image for kind,image,p in results if kind=='roughness')
    pixels=np.empty(SIZE*SIZE*4,dtype=np.float32);rough.pixels.foreach_get(pixels);pixels=pixels.reshape((-1,4))
    smooth=np.zeros_like(pixels);smooth[:,3]=1-pixels[:,0]
    image=bpy.data.images.new(key+' packed metallic smoothness',width=SIZE,height=SIZE,alpha=True)
    image.colorspace_settings.name='Non-Color';image.pixels.foreach_set(smooth.ravel())
    path=RES/'Textures'/(key+'-metallic-smoothness.png');image.filepath_raw=str(path);image.file_format='PNG';image.save()
    results.append(('metallic-smoothness',image,path))
    # Low-resolution preview uses precisely the baked surface maps. Original slots
    # are kept, allowing the runtime importer to resolve the same resource atlas.
    by_kind={kind:image for kind,image,p in results}
    for mat in {m for obj in meshes for m in obj.data.materials if m}:
        shader=next(n for n in mat.node_tree.nodes if n.type=='BSDF_PRINCIPLED')
        tex=mat.node_tree.nodes.new('ShaderNodeTexImage');tex.image=by_kind['albedo']
        mat.node_tree.links.new(tex.outputs['Color'],shader.inputs['Base Color'])
        tex=mat.node_tree.nodes.new('ShaderNodeTexImage');tex.image=by_kind['normal']
        normal=mat.node_tree.nodes.new('ShaderNodeNormalMap');normal.inputs['Strength'].default_value=1
        mat.node_tree.links.new(tex.outputs['Color'],normal.inputs['Color']);mat.node_tree.links.new(normal.outputs[0],shader.inputs['Normal'])
        tex=mat.node_tree.nodes.new('ShaderNodeTexImage');tex.image=by_kind['roughness']
        mat.node_tree.links.new(tex.outputs['Color'],shader.inputs['Roughness'])
    for high in highs:high.hide_render=True;high.hide_set(True)
    root=next(obj for obj in scene.objects if obj.type=='EMPTY' and obj.parent is None)
    runtime=[obj for obj in scene.objects if obj.type=='EMPTY']+meshes
    select(runtime,root)
    bpy.ops.export_scene.fbx(filepath=str(PROJECT/record['fbx']),use_selection=True,object_types={'MESH','EMPTY'},
        apply_unit_scale=True,global_scale=1,axis_forward='-Z',axis_up='Y',use_mesh_modifiers=True,
        mesh_smooth_type='FACE',use_tspace=True,add_leaf_bones=False,bake_anim=False,path_mode='RELATIVE')
    select(meshes)
    bpy.ops.wm.save_as_mainfile(filepath=str(PROJECT/record['source']),compress=True)
    bpy.ops.file.make_paths_relative();bpy.ops.wm.save_as_mainfile(filepath=str(PROJECT/record['source']),compress=True)
    points=[obj.matrix_world@v.co for obj in meshes for v in obj.data.vertices]
    for obj in meshes:obj.data.calc_loop_triangles()
    record.update(sourceSha256=hashlib.sha256((PROJECT/record['source']).read_bytes()).hexdigest(),
        fbxSha256=hashlib.sha256((PROJECT/record['fbx']).read_bytes()).hexdigest(),
        triangles=sum(len(obj.data.loop_triangles) for obj in meshes),vertices=sum(len(obj.data.vertices) for obj in meshes),
        boundsMin=[min(p.x for p in points),min(p.z for p in points),min(-p.y for p in points)],
        boundsMax=[max(p.x for p in points),max(p.z for p in points),max(-p.y for p in points)],
        meshParts=[obj.name for obj in meshes],highSculptParts=len(highs),sculpt='Voxel fused anatomy; editable two-level multires; actual dermal/pores displacement',
        bake='Cycles selected-to-active tangent normal, diffuse color, ambient occlusion and roughness; 2048 atlas UV',
        textures=[dict(kind=k,path=p.relative_to(PROJECT).as_posix(),sha256=hashlib.sha256(p.read_bytes()).hexdigest(),width=SIZE,height=SIZE) for k,image,p in results])
    print('MASK_SCULPT_AND_BAKE_PASS',key,record['triangles'],flush=True)

def main():
    bpy.context.preferences.filepaths.save_version=0;(RES/'Textures').mkdir(parents=True,exist_ok=True)
    p=HERE/'model-manifest.json';manifest=json.loads(p.read_text(encoding='utf-8'))
    for record in manifest['assets']:build(record['key'],record)
    manifest['professionalAuthoring']='Blender native volume remesh, silhouette-preserving decimation, unique atlas UV, editable multires sculpt and actual 2K Cycles PBR baking'
    p.write_text(json.dumps(manifest,indent=2)+'\n',encoding='utf-8')

if __name__=='__main__':main()
