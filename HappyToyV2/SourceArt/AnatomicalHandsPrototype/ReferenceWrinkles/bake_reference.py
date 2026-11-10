"""Bake manually aligned user-photo wrinkle detail to the existing hand UV.

Run in Blender 4.2.23 background mode with ``-- <work-directory>`` after
prepare_reference.py. The work directory contains only generated intermediates.
"""
import bpy, math, sys
from pathlib import Path
HERE=Path(__file__).resolve().parent
ROOT=HERE.parent
W=Path(sys.argv[sys.argv.index('--')+1]).resolve()
bpy.ops.wm.open_mainfile(filepath=str(ROOT/'hand-rig-corrective.blend'))
h=bpy.data.objects['Blender anatomical hand candidate'];rig=next(o for o in bpy.context.scene.objects if o.type=='ARMATURE');rig.animation_data.action=None
for b in rig.pose.bones:b.matrix_basis.identity()
bpy.context.view_layer.update();h.data.uv_layers.active=h.data.uv_layers['NewSkinUV']
mat=bpy.data.materials.new('reference joint detail');mat.use_nodes=True;n=mat.node_tree.nodes;n.clear();l=mat.node_tree.links
def op(kind,a,b=None):
 q=n.new('ShaderNodeMath');q.operation=kind
 for i,v in enumerate((a,b)):
  if v is None:continue
  if isinstance(v,(float,int)):q.inputs[i].default_value=v
  else:l.new(v,q.inputs[i])
 return q.outputs[0]
coord=n.new('ShaderNodeTexCoord');sep=n.new('ShaderNodeSeparateXYZ');l.new(coord.outputs['Object'],sep.inputs[0]);x=sep.outputs['X'];z=sep.outputs['Z']
geo=n.new('ShaderNodeNewGeometry');norm=n.new('ShaderNodeSeparateXYZ');l.new(geo.outputs['Normal'],norm.inputs[0]);dorsal=op('MINIMUM',1.,op('MAXIMUM',0.,op('MULTIPLY',op('SUBTRACT',norm.outputs['Y'],.2),2.5)))
base=n.new('ShaderNodeTexImage');base.image=bpy.data.images.load(str(ROOT/'draft-skin-2048-nails.png'));uvbase=n.new('ShaderNodeUVMap');uvbase.uv_map='NewSkinUV';l.new(uvbase.outputs[0],base.inputs['Vector'])
photo=bpy.data.images.load(str(W/'reference-detail.png'));photo.colorspace_settings.name='Non-Color'
# Model joint centers and neutral finger direction (x,z), then the photographed
# joint and tip (pixels from the top left). The patch is limited to the dorsal
# joint region and fades before reaching adjacent fingers or the nail beds.
patches=[
 ('thumb_ip',(.079,.112),(.009,.024),(72,690),(24,600),.0075,.0070,4300,4200),
 ('index_pip',(.039,.157),(.007,.031),(315,405),(265,190),.0080,.0075,4700,4300),
 ('index_dip',(.046,.188),(.006,.030),(283,273),(265,190),.0070,.0055,4700,3900),
 ('middle_pip',(.014,.166),(.002,.032),(500,338),(481,93),.0085,.0075,4800,4400),
 ('middle_dip',(.016,.198),(.002,.030),(486,192),(481,93),.0075,.0055,4700,4000),
 ('ring_pip',(-.012,.160),(0,.032),(654,409),(653,145),.0080,.0075,4700,4300),
 ('ring_dip',(-.012,.192),(0,.028),(660,232),(653,145),.0070,.0055,4700,3900),
 ('little_pip',(-.041,.140),(-.006,.026),(810,514),(849,316),.0070,.0065,4500,4000),
 ('little_dip',(-.047,.166),(-.003,.022),(835,395),(849,316),.0060,.0048,4400,3700)]
total=None
for name,(cx,cz),(ax,az),(px,py),(tx,ty),width,length,along_scale,across_scale in patches:
 size=math.hypot(ax,az);ax/=size;az/=size
 dx=op('SUBTRACT',x,cx);dz=op('SUBTRACT',z,cz)
 along=op('ADD',op('MULTIPLY',dx,ax),op('MULTIPLY',dz,az));across=op('SUBTRACT',op('MULTIPLY',dx,az),op('MULTIPLY',dz,ax))
 ad=op('DIVIDE',op('ABSOLUTE',along),length);ud=op('DIVIDE',op('ABSOLUTE',across),width)
 radius=op('SQRT',op('ADD',op('MULTIPLY',ad,ad),op('MULTIPLY',ud,ud)))
 mask=op('MINIMUM',1.,op('MAXIMUM',0.,op('MULTIPLY',op('SUBTRACT',1.,radius),4.)))
 mask=op('MULTIPLY',mask,dorsal)
 vx=tx-px;vy=ty-py;size=math.hypot(vx,vy);vx/=size;vy/=size
 sx=op('ADD',px,op('ADD',op('MULTIPLY',along,vx*along_scale),op('MULTIPLY',across,vy*across_scale)))
 sy=op('ADD',py,op('ADD',op('MULTIPLY',along,vy*along_scale),op('MULTIPLY',across,-vx*across_scale)))
 uv=n.new('ShaderNodeCombineXYZ');l.new(op('DIVIDE',sx,960.),uv.inputs['X']);l.new(op('SUBTRACT',1.,op('DIVIDE',sy,1280.)),uv.inputs['Y'])
 tex=n.new('ShaderNodeTexImage');tex.image=photo;tex.extension='EXTEND';l.new(uv.outputs[0],tex.inputs['Vector'])
 delta=op('MULTIPLY',mask,op('MULTIPLY',op('SUBTRACT',tex.outputs['Color'],.5),1.25))
 total=delta if total is None else op('ADD',total,delta)
factor=op('MAXIMUM',.65,op('MINIMUM',1.25,op('ADD',1.,total)))
comb=n.new('ShaderNodeCombineColor');l.new(factor,comb.inputs[0]);l.new(factor,comb.inputs[1]);l.new(factor,comb.inputs[2])
mix=n.new('ShaderNodeMixRGB');mix.blend_type='MULTIPLY';mix.inputs[0].default_value=1;l.new(base.outputs['Color'],mix.inputs[1]);l.new(comb.outputs[0],mix.inputs[2])
emit=n.new('ShaderNodeEmission');l.new(mix.outputs[0],emit.inputs[0]);out=n.new('ShaderNodeOutputMaterial');l.new(emit.outputs[0],out.inputs['Surface'])
img=bpy.data.images.new('joint-detail-trial',2048,2048,alpha=False);target=n.new('ShaderNodeTexImage');target.image=img;n.active=target
h.data.materials.clear();h.data.materials.append(mat)
for f in h.data.polygons:f.material_index=0
bpy.ops.object.select_all(action='DESELECT');h.select_set(True);bpy.context.view_layer.objects.active=h
scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.device='CPU';scene.cycles.samples=1;scene.render.threads_mode='FIXED';scene.render.threads=2;scene.render.bake.margin=8
bpy.ops.object.bake(type='EMIT')
img.filepath_raw=str(W/'joint-detail-trial.png');img.file_format='PNG';img.save();print('JOINT_DETAIL_BAKE_DONE')
