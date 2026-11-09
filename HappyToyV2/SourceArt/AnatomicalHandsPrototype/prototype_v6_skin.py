import bpy,json
from pathlib import Path
p=Path(__file__).resolve().parent
root=p.parents[2]
bpy.ops.wm.open_mainfile(filepath=str(p/'hand-rig-prototype-v5.blend'))
obj=bpy.data.objects['Blender anatomical hand candidate'];obj.data.uv_layers.active=obj.data.uv_layers['NewSkinUV']
mat=bpy.data.materials.new('Draft two-sided skin projection');mat.use_nodes=True;n=mat.node_tree.nodes;n.clear();l=mat.node_tree.links
coord=n.new('ShaderNodeTexCoord');sep=n.new('ShaderNodeSeparateXYZ');l.new(coord.outputs['Object'],sep.inputs[0])
def affine(socket,m,b):
 node=n.new('ShaderNodeMath');node.operation='MULTIPLY_ADD';l.new(socket,node.inputs[0]);node.inputs[1].default_value=m;node.inputs[2].default_value=b;return node.outputs[0]
u=affine(sep.outputs['X'],3.9375,.461609375);v=affine(sep.outputs['Z'],2.625,.4085)
def photo(name,uval):
 uv=n.new('ShaderNodeCombineXYZ');l.new(uval,uv.inputs['X']);l.new(v,uv.inputs['Y'])
 tex=n.new('ShaderNodeTexImage');tex.image=bpy.data.images.load(str(root/'HappyToyV2/SourceArt/PlayerHandsV2/Derived'/(name+'-projection.png')),check_existing=True);tex.extension='EXTEND';l.new(uv.outputs[0],tex.inputs['Vector']);return tex.outputs['Color']
front=photo('neutral-hand',u);back=photo('dorsal-hand',affine(u,-1,1))
geo=n.new('ShaderNodeNewGeometry');norm=n.new('ShaderNodeSeparateXYZ');l.new(geo.outputs['Normal'],norm.inputs[0]);blend=n.new('ShaderNodeMapRange');blend.interpolation_type='SMOOTHSTEP';l.new(norm.outputs['Y'],blend.inputs['Value']);blend.inputs['From Min'].default_value=-.45;blend.inputs['From Max'].default_value=.45
mix=n.new('ShaderNodeMixRGB');l.new(blend.outputs['Result'],mix.inputs[0]);l.new(front,mix.inputs[1]);l.new(back,mix.inputs[2])
em=n.new('ShaderNodeEmission');l.new(mix.outputs[0],em.inputs[0]);out=n.new('ShaderNodeOutputMaterial');l.new(em.outputs[0],out.inputs['Surface'])
image=bpy.data.images.new('draft skin projection',1024,1024,alpha=False);target=n.new('ShaderNodeTexImage');target.image=image;n.active=target
obj.data.materials.clear();obj.data.materials.append(mat)
for f in obj.data.polygons:f.material_index=0
bpy.ops.object.select_all(action='DESELECT');obj.select_set(True);bpy.context.view_layer.objects.active=obj
scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.device='CPU';scene.cycles.samples=1;scene.render.threads_mode='FIXED';scene.render.threads=2;scene.render.bake.margin=8
bpy.ops.object.bake(type='EMIT')
image.filepath_raw=str(p/'draft-skin-1024.png');image.file_format='PNG';image.save()
n.clear();tex=n.new('ShaderNodeTexImage');tex.image=image;bsdf=n.new('ShaderNodeBsdfPrincipled');l.new(tex.outputs['Color'],bsdf.inputs['Base Color']);bsdf.inputs['Roughness'].default_value=.56;out=n.new('ShaderNodeOutputMaterial');l.new(bsdf.outputs['BSDF'],out.inputs['Surface'])
bpy.ops.file.pack_all();bpy.ops.wm.save_as_mainfile(filepath=str(p/'hand-rig-prototype-v6.blend'))
print('DRAFT_CPU_BAKE_PASS')
