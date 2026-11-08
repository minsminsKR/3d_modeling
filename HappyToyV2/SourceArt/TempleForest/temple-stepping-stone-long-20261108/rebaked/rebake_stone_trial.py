"""CPU reprojection trial; preserves source and initial candidate. No gameplay approval."""
from pathlib import Path
from mathutils import Matrix, Vector
import bpy, math, json, hashlib, sys
import numpy as np

def sha(path):
 h=hashlib.sha256()
 with open(path,'rb') as f:
  for b in iter(lambda:f.read(1048576),b''): h.update(b)
 return h.hexdigest()

args=sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else []
if len(args)!=2: raise RuntimeError('Expected -- SOURCE_FOLDER OUTPUT_FOLDER')
root=Path(args[0]).resolve(); out=Path(args[1]).resolve(); key=root.name
if out.exists(): raise RuntimeError('Output exists; preserve it and choose a fresh directory')
out.mkdir(parents=True)
original=root/'original.glb'; blend=root/(key+'.blend'); previous=json.loads((root/'export.json').read_text(encoding='utf-8'))
assert sha(original)==previous['originalSha256'] and sha(blend)==previous['editableSha256']
bpy.ops.wm.open_mainfile(filepath=str(blend),load_ui=False)
scene=bpy.context.scene; originals=bpy.data.collections['Complete unchanged webapp original']; orig=next(o for o in originals.objects if o.type=='MESH')
corners=[orig.matrix_world@Vector(c) for c in orig.bound_box]; low=Vector(tuple(min(p[i] for p in corners) for i in range(3))); high=Vector(tuple(max(p[i] for p in corners) for i in range(3))); center=Vector(((low.x+high.x)*.5,(low.y+high.y)*.5,low.z)); norm=Matrix.Scale(1/(high.y-low.y),4)@Matrix.Translation(-center)
old=bpy.data.collections['Editable source-derived game LODs']; originals.hide_render=True;old.hide_render=True
source=orig.copy();source.data=orig.data.copy();scene.collection.objects.link(source);source.matrix_world=norm@orig.matrix_world;source.hide_render=False;source.hide_viewport=False;source.hide_set(False)
assert len(source.material_slots)==1
sm=source.material_slots[0].material.copy();source.data.materials.clear();source.data.materials.append(sm)
shader=next(n for n in sm.node_tree.nodes if n.type=='BSDF_PRINCIPLED'); outnode=next(n for n in sm.node_tree.nodes if n.type=='OUTPUT_MATERIAL'); assert shader.inputs['Base Color'].is_linked
base=shader.inputs['Base Color'].links[0].from_socket; emitter=sm.node_tree.nodes.new('ShaderNodeEmission'); sm.node_tree.links.new(emitter.outputs[0],outnode.inputs['Surface']); combined=sm.node_tree.nodes.new('ShaderNodeCombineRGB')
for component,prop in (('R','Metallic'),('G','Roughness')):
 inp=shader.inputs[prop]
 if inp.is_linked: sm.node_tree.links.new(inp.links[0].from_socket,combined.inputs[component])
 else: combined.inputs[component].default_value=inp.default_value
scene.render.engine='CYCLES';scene.cycles.device='CPU';scene.cycles.samples=1;scene.render.threads_mode='FIXED';scene.render.threads=2;scene.render.bake.use_selected_to_active=True;scene.render.bake.cage_extrusion=.015;scene.render.bake.max_ray_distance=.05;scene.render.bake.margin=16;scene.render.bake.use_clear=True
collection=bpy.data.collections.new('Rebaked review LODs');scene.collection.children.link(collection); report={'source':str(key),'originalSha256':sha(original),'sourceBlendSha256':sha(blend),'quality':'review candidate; not gameplay approved','bake':'Cycles CPU, selected-to-active emission, 1 sample, 16px margin, .015m extrusion, .05m distance; Smart UV 66deg/.012 margin','lods':[]}
for index in range(3):
 obj=bpy.data.objects['CE_'+key+'_LOD'+str(index)];target=obj.copy();target.data=obj.data.copy();collection.objects.link(target);target.name='REB_'+key+'_LOD'+str(index);target.hide_render=False;target.hide_viewport=False;target.hide_set(False)
 mat=bpy.data.materials.new('REB_'+key+'_LOD'+str(index));mat.use_nodes=True;target.data.materials.clear();target.data.materials.append(mat)
 for face in target.data.polygons:face.material_index=0
 bpy.ops.object.select_all(action='DESELECT');target.select_set(True);bpy.context.view_layer.objects.active=target;bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT');bpy.ops.uv.smart_project(angle_limit=math.radians(66),island_margin=.012,area_weight=.2);bpy.ops.object.mode_set(mode='OBJECT')
 uvdata=target.data.uv_layers.active.data; triangles=[];target.data.calc_loop_triangles()
 for tri in target.data.loop_triangles: triangles.append([[float(uvdata[i].uv.x),float(uvdata[i].uv.y)] for i in tri.loops])
 (out/('lod%d-uv.json'%index)).write_text(json.dumps(triangles),encoding='utf-8')
 active=mat.node_tree.nodes.new('ShaderNodeTexImage');mat.node_tree.nodes.active=active; images={}; entry={'lod':index,'triangles':len(target.data.polygons),'textures':{}}
 for channel,socket,space in [('baseColor',base,'sRGB'),('metallicSmoothness',combined.outputs[0],'Non-Color'),('coverage',None,'Non-Color')]:
  image=bpy.data.images.new('REB_'+key+'_lod%d-%s'%(index,channel),width=2048,height=2048,alpha=True);image.colorspace_settings.name=space;active.image=image
  if emitter.inputs['Color'].is_linked:
   for link in list(emitter.inputs['Color'].links):sm.node_tree.links.remove(link)
  if socket is None: emitter.inputs['Color'].default_value=(1,1,1,1)
  else: sm.node_tree.links.new(socket,emitter.inputs['Color'])
  bpy.ops.object.select_all(action='DESELECT');source.select_set(True);target.select_set(True);bpy.context.view_layer.objects.active=target;bpy.ops.object.bake(type='EMIT')
  if channel=='metallicSmoothness':
   pixels=np.empty(2048*2048*4,dtype=np.float32);image.pixels.foreach_get(pixels);pixels=pixels.reshape(-1,4);rough=pixels[:,1].copy();pixels[:,1:3]=0;pixels[:,3]=1-rough;image.pixels.foreach_set(pixels.ravel())
  filename='CE_'+key+'_lod%d-%s.png'%(index,channel);path=out/filename;image.file_format='PNG';image.filepath_raw=str(path);image.save();images[channel]=image;entry['textures'][channel]={'file':filename,'sha256':sha(path)}
 nodes=mat.node_tree.nodes; bsdf=nodes.get('Principled BSDF');colour=nodes.new('ShaderNodeTexImage');colour.image=images['baseColor'];mat.node_tree.links.new(colour.outputs['Color'],bsdf.inputs['Base Color']); orm=nodes.new('ShaderNodeTexImage');orm.image=images['metallicSmoothness'];sep=nodes.new('ShaderNodeSeparateRGB');mat.node_tree.links.new(orm.outputs['Color'],sep.inputs[0]);mat.node_tree.links.new(sep.outputs['R'],bsdf.inputs['Metallic']);reverse=nodes.new('ShaderNodeMath');reverse.operation='SUBTRACT';reverse.inputs[0].default_value=1;mat.node_tree.links.new(orm.outputs['Alpha'],reverse.inputs[1]);mat.node_tree.links.new(reverse.outputs[0],bsdf.inputs['Roughness']);nodes.remove(active)
 bpy.ops.object.select_all(action='DESELECT');target.select_set(True);bpy.context.view_layer.objects.active=target;path=out/(key+'-rebaked-lod%d.fbx'%index);bpy.ops.export_scene.fbx(filepath=str(path),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',bake_anim=False,add_leaf_bones=False,path_mode='RELATIVE',use_mesh_modifiers=True);entry['fbx']={'file':path.name,'sha256':sha(path)};report['lods'].append(entry)
 print('LONG_REBAKE_LOD_DONE '+str(index),flush=True)
source.hide_render=True;bpy.data.objects.remove(source,do_unlink=True)
for obj in collection.objects:
 if obj.name.endswith('LOD0'):obj.hide_render=False
 else:obj.hide_render=True
bpy.ops.file.pack_all();targetblend=out/(key+'-rebaked.blend');bpy.ops.wm.save_as_mainfile(filepath=str(targetblend),compress=True);report['blend']={'file':targetblend.name,'sha256':sha(targetblend)}; (out/'rebake-report.json').write_text(json.dumps(report,indent=2)+'\n',encoding='utf-8'); print('LONG_REBAKE_DONE',flush=True)
