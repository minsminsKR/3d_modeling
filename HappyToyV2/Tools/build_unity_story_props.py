import bpy, math, json
from pathlib import Path
root=Path(__file__).resolve().parent.parent
out=root/'Assets/Art/Finished'
blend=root/'SourceArt';blend.mkdir(parents=True,exist_ok=True)
def material(name,color):
 m=bpy.data.materials.new(name);m.diffuse_color=(*color,1);m.use_nodes=True
 m.node_tree.nodes.get('Principled BSDF').inputs['Base Color'].default_value=(*color,1)
 return m
def box(name,p,s,m):
 bpy.ops.mesh.primitive_cube_add(size=1,location=p);o=bpy.context.object;o.name=name;o.scale=s
 bpy.ops.object.transform_apply(location=False,rotation=False,scale=True);o.data.materials.append(m)
 bevel=o.modifiers.new('Worn edges','BEVEL');bevel.width=.003;bevel.segments=2
 bpy.ops.object.modifier_apply(modifier=bevel.name);return o
def build(key):
 bpy.ops.wm.read_factory_settings(use_empty=True)
 paper=material('Yellowed paper',(.59,.54,.38));cover=material('Faded dark green',(.04,.09,.075));ink=material('Graphite',(.035,.03,.027));red=material('Faded crimson',(.25,.026,.035))
 if key in ['story-register','story-medical-record']:
  box('Bound cover',(0,0,.008),(.32,.24,.016),cover)
  box('Paper block',(0,0,.022),(.30,.22,.018),paper)
  for i in range(8):box('Ruled record line',(0,-.085+i*.023,.032),(.27,.001,.001),ink)
  for i in range(3):box('Record column',(-.095+i*.09,0,.032),(.001,.20,.001),ink)
  for i in range(9):
   o=box('Erased name scar',(-.06+i*.004,.007,.033),(.001,.019,.0015),ink);o.rotation_euler[2]=.25
  if key=='story-medical-record':box('False dismissal stamp',(.09,.07,.034),(.055,.032,.003),red)
 elif key=='story-ribbon':
  for side in [-1,1]:
   vertices=[]
   for i in range(25):
    t=i/24*math.tau;x=side*(.025+.045*(1-math.cos(t)));y=.032*math.sin(t);z=.018+.012*math.sin(t*2)
    vertices.extend([(x,y-.01,z),(x,y+.01,z)])
   mesh=bpy.data.meshes.new('Folded cloth');mesh.from_pydata(vertices,[],[(i*2,i*2+1,i*2+3,i*2+2) for i in range(24)]);mesh.update()
   o=bpy.data.objects.new('Ribbon loop',mesh);bpy.context.collection.objects.link(o);o.data.materials.append(red)
   modifier=o.modifiers.new('Cloth thickness','SOLIDIFY');modifier.thickness=.001
   box('Trailing ribbon',(side*.025,-.065,.007),(.025,.10,.004),red).rotation_euler[2]=side*.22
  box('Central knot',(0,0,.018),(.028,.024,.025),red)
 else:
  box('Preparation box base',(0,0,.025),(.40,.28,.05),cover)
  for x in [-.19,.19]:box('Box side',(x,0,.10),(.02,.28,.15),cover)
  for y in [-.13,.13]:box('Box end',(0,y,.10),(.40,.02,.15),cover)
  box('Open lid',(0,.15,.18),(.40,.018,.26),cover)
  box('Name card',(0,-.01,.055),(.18,.12,.008),paper)
 bpy.ops.wm.save_as_mainfile(filepath=str(blend/(key+'.blend')))
 bpy.ops.export_scene.fbx(filepath=str(out/(key+'.fbx')),axis_forward='-Z',axis_up='Y',apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',add_leaf_bones=False)
 records=[{'name':m.name,'color':list(m.diffuse_color),'metallic':0,'roughness':.85} for m in bpy.data.materials]
 (out/(key+'.materials.json')).write_text(json.dumps({'materials':records}),encoding='utf-8')
for key in ['story-register','story-medical-record','story-ribbon','story-preparation-box']:build(key)
print('STORY_PROPS_READY 4')
