"""Blender 4.0.2 CPU emission bake of front/back skin references to retained UVs.
Run after finish_hand.py and prepare_projection.py, then finish_skin.py.
The A100 original GLB and its original maps are preserved under Originals/.
"""
from pathlib import Path
import bpy,json,hashlib
ROOT=Path(__file__).resolve().parent
OUT=ROOT.parents[1]/'Assets/Resources/PlayerItems/hand'
obj=next(o for o in bpy.context.scene.objects if o.type=='MESH' and o.name=='hand')
material=bpy.data.materials.new('Hand photographic skin projection');material.use_nodes=True
nodes=material.node_tree.nodes;links=material.node_tree.links;nodes.clear()
coords=nodes.new('ShaderNodeTexCoord');separate=nodes.new('ShaderNodeSeparateXYZ')
links.new(coords.outputs['Object'],separate.inputs[0])
def affine(socket,m,b):
    node=nodes.new('ShaderNodeMath');node.operation='MULTIPLY_ADD'
    links.new(socket,node.inputs[0]);node.inputs[1].default_value=m;node.inputs[2].default_value=b
    return node.outputs[0]
u=affine(separate.outputs['X'],3.9375,.461609375)
v=affine(separate.outputs['Z'],2.625,.4085)
def photo(name,u_socket):
    uv=nodes.new('ShaderNodeCombineXYZ');links.new(u_socket,uv.inputs['X']);links.new(v,uv.inputs['Y'])
    texture=nodes.new('ShaderNodeTexImage');texture.image=bpy.data.images.load(str(ROOT/'Derived'/(name+'-projection.png')),check_existing=False)
    texture.extension='EXTEND';links.new(uv.outputs[0],texture.inputs['Vector'])
    return texture.outputs['Color']
front=photo('neutral-hand',u);back=photo('dorsal-hand',affine(u,-1,1))
geometry=nodes.new('ShaderNodeNewGeometry');normal=nodes.new('ShaderNodeSeparateXYZ')
links.new(geometry.outputs['Normal'],normal.inputs[0])
blend=nodes.new('ShaderNodeMapRange');blend.interpolation_type='SMOOTHSTEP'
links.new(normal.outputs['Y'],blend.inputs['Value'])
blend.inputs['From Min'].default_value=-.45;blend.inputs['From Max'].default_value=.45
mix=nodes.new('ShaderNodeMixRGB');links.new(blend.outputs['Result'],mix.inputs[0]);links.new(front,mix.inputs[1]);links.new(back,mix.inputs[2])
emission=nodes.new('ShaderNodeEmission');links.new(mix.outputs[0],emission.inputs[0])
output=nodes.new('ShaderNodeOutputMaterial');links.new(emission.outputs[0],output.inputs['Surface'])
image=bpy.data.images.new('Hand finished photographic base colour',4096,4096,alpha=False)
target=nodes.new('ShaderNodeTexImage');target.image=image;nodes.active=target
obj.data.materials.clear();obj.data.materials.append(material)
for p in obj.data.polygons:p.material_index=0
bpy.ops.object.select_all(action='DESELECT');obj.select_set(True);bpy.context.view_layer.objects.active=obj
scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.device='CPU';scene.cycles.samples=1
scene.render.threads_mode='FIXED';scene.render.threads=2;scene.render.bake.margin=12
bpy.ops.object.bake(type='EMIT')
image.filepath_raw=str(OUT/'albedo.png');image.file_format='PNG';image.save()
nodes.clear()
texture=nodes.new('ShaderNodeTexImage');texture.image=image
bsdf=nodes.new('ShaderNodeBsdfPrincipled');links.new(texture.outputs['Color'],bsdf.inputs['Base Color']);bsdf.inputs['Roughness'].default_value=.56
output=nodes.new('ShaderNodeOutputMaterial');links.new(bsdf.outputs[0],output.inputs['Surface'])
material.name='hand_v2_surface'
bpy.ops.file.pack_all();bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/'hand.blend'))
report=json.loads((OUT/'asset.json').read_text(encoding='utf-8'))
report['photographicSkin']={'front':'SourceArt/PlayerHandsV2/References/neutral-hand.png','back':'SourceArt/PlayerHandsV2/References/dorsal-hand.png','textureSize':4096,'method':'Two-sided neutral-reference emission projection to retained A100 UVs; soft side blend'}
for key,path in [('albedo',OUT/'albedo.png'),('packedBlend',ROOT/'hand.blend')]:report['outputSha256'][key]=hashlib.sha256(path.read_bytes()).hexdigest()
(OUT/'asset.json').write_text(json.dumps(report,indent=2)+'\n',encoding='utf-8')
(ROOT/'finish-report.json').write_text(json.dumps(report,indent=2)+'\n',encoding='utf-8')
print('HAND_PHOTOGRAPHIC_BAKE_PASS')
