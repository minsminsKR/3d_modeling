"""Blender 4.0.2 selected-to-active atlas finishing for the generated lantern.

Retains the original object's geometry/material/image data unchanged. A temporary
normalized source copy transfers original colour and ORM to coherent game UVs.
"""
import math
import bpy
import numpy as np


def bake(game, original, matrix, destination, name, limit, save_pixels):
    scene=bpy.context.scene
    scene.render.engine='CYCLES'
    scene.cycles.device='CPU'
    scene.cycles.samples=1
    scene.render.bake.use_selected_to_active=True
    scene.render.bake.cage_extrusion=.015
    scene.render.bake.max_ray_distance=.05
    scene.render.bake.margin=16
    scene.render.bake.use_clear=True
    source=original.copy()
    source.data=original.data.copy()
    source.matrix_world=matrix@original.matrix_world
    bpy.context.scene.collection.objects.link(source)
    source.name='Temporary normalized original for atlas transfer'
    # The designated job is one PBR-atlased primitive. Fail rather than silently
    # lose surfaces if a future original has a different material layout.
    if len(source.material_slots)!=1:
        raise RuntimeError('Lantern atlas transfer expects one original PBR surface')
    original_material=source.material_slots[0].material
    material=original_material.copy()
    source.material_slots[0].material=material
    shader=next(n for n in material.node_tree.nodes if n.type=='BSDF_PRINCIPLED')
    output=next(n for n in material.node_tree.nodes if n.type=='OUTPUT_MATERIAL')
    emitter=material.node_tree.nodes.new('ShaderNodeEmission')
    material.node_tree.links.new(emitter.outputs[0],output.inputs['Surface'])
    base=shader.inputs['Base Color']
    if not base.is_linked:
        raise RuntimeError('Original lantern colour texture is missing')
    base_socket=base.links[0].from_socket
    channels=material.node_tree.nodes.new('ShaderNodeCombineRGB')
    for component,property_name in (('R','Metallic'),('G','Roughness')):
        value=shader.inputs[property_name]
        if value.is_linked:
            material.node_tree.links.new(value.links[0].from_socket,channels.inputs[component])
        else:
            channels.inputs[component].default_value=value.default_value
    game_material=bpy.data.materials.new(name)
    game_material.use_nodes=True
    game.data.materials.clear();game.data.materials.append(game_material)
    for face in game.data.polygons:face.material_index=0
    bpy.ops.object.select_all(action='DESELECT')
    game.select_set(True);bpy.context.view_layer.objects.active=game
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.uv.smart_project(angle_limit=math.radians(66),island_margin=.012,area_weight=.2)
    bpy.ops.object.mode_set(mode='OBJECT')
    active=game_material.node_tree.nodes.new('ShaderNodeTexImage')
    game_material.node_tree.nodes.active=active
    result=dict(name=name,roughness=1.0,metallic=1.0,baseColorFactor=[1,1,1,1],normalScale=1,occlusionStrength=1)
    images={}
    try:
        for key,socket,colour in (('baseColor',base_socket,'sRGB'),('metallicSmoothness',channels.outputs[0],'Non-Color')):
            image=bpy.data.images.new(name+'-'+key,width=limit,height=limit,alpha=True)
            image.colorspace_settings.name=colour
            active.image=image
            material.node_tree.links.new(socket,emitter.inputs['Color'])
            bpy.ops.object.select_all(action='DESELECT')
            source.select_set(True);game.select_set(True);bpy.context.view_layer.objects.active=game
            bpy.ops.object.bake(type='EMIT')
            filename=name+'-'+key+'.png'
            path=destination/filename
            if key=='metallicSmoothness':
                pixels=np.empty(limit*limit*4,dtype=np.float32)
                image.pixels.foreach_get(pixels)
                pixels=pixels.reshape(-1,4)
                roughness=pixels[:,1].copy()
                pixels[:,1:3]=0
                pixels[:,3]=1-roughness
                image.pixels.foreach_set(pixels.ravel())
            image.file_format='PNG';image.filepath_raw=str(path);image.save()
            result[key]=filename;images[key]=image
        pixels=np.empty(limit*limit*4,dtype=np.float32)
        images['baseColor'].pixels.foreach_get(pixels)
        pixels=pixels.reshape(-1,4)
        paper=np.clip((pixels[:,1]-.12)/.28,0,1)*np.clip((pixels[:,2]-.055)/.16,0,1)
        glow=np.ones_like(pixels);glow[:,:3]=paper[:,None]
        result['emission']=name+'-paperEmission.png'
        result['emissionColor']=[.16,.072,.023,1]
        result['emissionMaskRecipe']='linear green ramp .12-.40 times blue ramp .055-.215 on the rebaked original colour; ink and wood stay dark'
        result['atlasRecipe']='Cycles CPU selected-to-active EMIT, 1 sample, 16px margin, .015m extrusion/.05m ray; Smart UV 66deg/.012 margin; original colour and ORM retained'
        save_pixels(glow,(limit,limit),destination/result['emission'],'Non-Color')
        nodes=game_material.node_tree.nodes
        target=next(n for n in nodes if n.type=='BSDF_PRINCIPLED')
        colour=nodes.new('ShaderNodeTexImage');colour.image=images['baseColor']
        game_material.node_tree.links.new(colour.outputs['Color'],target.inputs['Base Color'])
        orm=nodes.new('ShaderNodeTexImage');orm.image=images['metallicSmoothness']
        split=nodes.new('ShaderNodeSeparateRGB');game_material.node_tree.links.new(orm.outputs['Color'],split.inputs[0])
        game_material.node_tree.links.new(split.outputs['R'],target.inputs['Metallic'])
        reverse=nodes.new('ShaderNodeMath');reverse.operation='SUBTRACT';reverse.inputs[0].default_value=1
        game_material.node_tree.links.new(orm.outputs['Alpha'],reverse.inputs[1])
        game_material.node_tree.links.new(reverse.outputs[0],target.inputs['Roughness'])
        mask=nodes.new('ShaderNodeTexImage');mask.image=bpy.data.images.load(str(destination/result['emission']),check_existing=False)
        mask.image.colorspace_settings.name='Non-Color'
        warm=nodes.new('ShaderNodeMixRGB');warm.blend_type='MULTIPLY';warm.inputs[0].default_value=1;warm.inputs[2].default_value=(.16,.072,.023,1)
        game_material.node_tree.links.new(mask.outputs['Color'],warm.inputs[1])
        game_material.node_tree.links.new(warm.outputs[0],target.inputs['Emission Color'])
        target.inputs['Emission Strength'].default_value=1
        nodes.remove(active)
        return result
    finally:
        bpy.data.objects.remove(source,do_unlink=True)
        bpy.data.orphans_purge(do_recursive=True)
