# UV-bake the sculpt's skin/blood into engine-portable PBR textures.
# Executed by build_mask_wraith.py after skinning and before animation.
bpy.context.view_layer.objects.active=body
bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT')
bpy.ops.uv.smart_project(angle_limit=1.15,island_margin=.008)
bpy.ops.object.mode_set(mode='OBJECT')
scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.samples=1
scene.render.bake.margin=8;scene.render.bake.use_clear=True
materials=list(body.data.materials);sources={};rough_sources={}
for mat in materials:
    nodes=mat.node_tree.nodes;links=mat.node_tree.links
    bs=nodes.get('Principled BSDF');attr=next(n for n in nodes if n.type=='VERTEX_COLOR')
    coord=nodes.new('ShaderNodeTexCoord')
    pores=nodes.new('ShaderNodeTexNoise');pores.inputs['Scale'].default_value=135;pores.inputs['Detail'].default_value=4
    links.new(coord.outputs['Object'],pores.inputs['Vector'])
    cracks=nodes.new('ShaderNodeTexVoronoi');cracks.feature='DISTANCE_TO_EDGE';cracks.inputs['Scale'].default_value=48
    links.new(coord.outputs['Object'],cracks.inputs['Vector'])
    contrast=nodes.new('ShaderNodeValToRGB');contrast.color_ramp.elements[0].position=.34;contrast.color_ramp.elements[0].color=(.27,.27,.27,1)
    contrast.color_ramp.elements[1].position=.7;contrast.color_ramp.elements[1].color=(1,1,1,1);links.new(pores.outputs['Fac'],contrast.inputs[0])
    tint=nodes.new('ShaderNodeMixRGB');tint.blend_type='MULTIPLY';tint.inputs[0].default_value=.42
    links.new(attr.outputs['Color'],tint.inputs[1]);links.new(contrast.outputs[0],tint.inputs[2]);sources[mat]=tint.outputs[0]
    blood=nodes.new('ShaderNodeVertexColor');blood.layer_name='Blood'
    rough=nodes.new('ShaderNodeMapRange');rough.inputs['From Min'].default_value=0;rough.inputs['From Max'].default_value=1;rough.inputs['To Min'].default_value=.94;rough.inputs['To Max'].default_value=.32
    links.new(blood.outputs['Color'],rough.inputs['Value']);rough_sources[mat]=rough.outputs['Result']
    bump=nodes.new('ShaderNodeBump');bump.inputs['Strength'].default_value=.32;bump.inputs['Distance'].default_value=.012
    links.new(pores.outputs['Fac'],bump.inputs['Height'])
    fissure=nodes.new('ShaderNodeBump');fissure.inputs['Strength'].default_value=.22;fissure.inputs['Distance'].default_value=.006
    links.new(cracks.outputs['Distance'],fissure.inputs['Height']);links.new(bump.outputs['Normal'],fissure.inputs['Normal']);links.new(fissure.outputs['Normal'],bs.inputs['Normal'])
    links.new(tint.outputs[0],bs.inputs['Base Color']);links.new(rough.outputs[0],bs.inputs['Roughness'])

def bake(name,size,kind,outputs=None):
    image=bpy.data.images.new(name,width=size,height=size,alpha=False)
    image.colorspace_settings.name='sRGB' if kind=='color' else 'Non-Color'
    for mat in materials:
        nodes=mat.node_tree.nodes;links=mat.node_tree.links
        tex=nodes.new('ShaderNodeTexImage');tex.image=image;nodes.active=tex;tex.select=True
        out=next(n for n in nodes if n.type=='OUTPUT_MATERIAL')
        if outputs is not None:
            emit=nodes.new('ShaderNodeEmission');links.new(outputs[mat],emit.inputs['Color']);links.new(emit.outputs[0],out.inputs['Surface'])
        else:links.new(nodes.get('Principled BSDF').outputs[0],out.inputs['Surface'])
    bpy.ops.object.bake(type='NORMAL' if kind=='normal' else 'EMIT')
    image.filepath_raw=os.path.join(OUT,name+'.png');image.file_format='PNG';image.save();image.pack();return image
color=bake('wraith_basecolor',2048,'color',sources)
roughness=bake('wraith_roughness',1024,'roughness',rough_sources)
normal=bake('wraith_normal',1024,'normal')
# One PBR material across all UV islands, including teeth, hair and the maw.
pbr=bpy.data.materials.new('Wraith_Bloodied_PBR');pbr.use_nodes=True;n=pbr.node_tree.nodes;l=pbr.node_tree.links;bs=n.get('Principled BSDF')
for img,socket in [(color,'Base Color'),(roughness,'Roughness')]:
    tex=n.new('ShaderNodeTexImage');tex.image=img;l.new(tex.outputs['Color'],bs.inputs[socket])
tex=n.new('ShaderNodeTexImage');tex.image=normal;nm=n.new('ShaderNodeNormalMap');nm.inputs['Strength'].default_value=.75;l.new(tex.outputs['Color'],nm.inputs['Color']);l.new(nm.outputs['Normal'],bs.inputs['Normal'])
body.data.materials.clear();body.data.materials.append(pbr)
for poly in body.data.polygons:poly.material_index=0
# Color has been baked; exporting the old vertex colors would multiply it twice.
while len(body.data.color_attributes):body.data.color_attributes.remove(body.data.color_attributes[0])
print('PBR_BAKE_COMPLETE',flush=True)
