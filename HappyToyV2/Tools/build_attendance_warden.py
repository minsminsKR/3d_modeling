"""Original articulated horror character. Blender source and Unity FBX, no downloaded art."""
import bpy, math, json
from pathlib import Path
from mathutils import Vector

root=Path(__file__).resolve().parent.parent
out=root/'Assets/Art/Finished'
bpy.ops.wm.read_factory_settings(use_empty=True)

def mat(name,color,metal=0,rough=.8):
 m=bpy.data.materials.new(name);m.diffuse_color=(*color,1);m.use_nodes=True
 bs=m.node_tree.nodes.get('Principled BSDF');bs.inputs['Base Color'].default_value=(*color,1)
 bs.inputs['Metallic'].default_value=metal;bs.inputs['Roughness'].default_value=rough
 return m
cloth=mat('Funeral indigo',(.025,.037,.044));edge=mat('Frayed linen',(.15,.14,.11))
skin=mat('Old carved wood',(.24,.17,.105));mask=mat('Chalk porcelain',(.65,.62,.49),0,.56)
black=mat('Ink recess',(.007,.008,.007));red=mat('Sealing thread',(.22,.018,.018))
brass=mat('Tarnished bell',(.32,.23,.09),.72,.4)

def parent(o,p):
 if p:
  matrix=o.matrix_world.copy();o.parent=p;o.matrix_world=matrix
 return o
def pivot(name,pos,p=None):
 o=bpy.data.objects.new(name,None);bpy.context.collection.objects.link(o);o.location=pos
 bpy.context.view_layer.update();return parent(o,p)
def ellipsoid(name,pos,scale,m,p=None):
 bpy.ops.mesh.primitive_uv_sphere_add(segments=16,ring_count=10,location=pos)
 o=bpy.context.object;o.name=name;o.scale=scale;bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
 o.data.materials.append(m)
 for f in o.data.polygons:f.use_smooth=True
 return parent(o,p)
def rod(name,a,b,r,m,p=None):
 a,b=Vector(a),Vector(b);bpy.ops.mesh.primitive_cone_add(vertices=12,radius1=r,radius2=r*.78,depth=(b-a).length,location=(a+b)/2)
 o=bpy.context.object;o.name=name;o.rotation_euler=(b-a).to_track_quat('Z','Y').to_euler();o.data.materials.append(m)
 return parent(o,p)
def garment(name,rings,m,p):
 # Rings (height, x-radius, y-radius) with alternating folds and irregular hem.
 verts=[];n=32
 for j,(z,rx,ry) in enumerate(rings):
  for i in range(n):
   a=i*math.tau/n;fold=1+(.06 if i%2 else -.04)
   verts.append((math.cos(a)*rx*fold,math.sin(a)*ry*fold,z+(math.sin(i*2.7)*.025 if j==0 else 0)))
 faces=[(j*n+i,j*n+(i+1)%n,(j+1)*n+(i+1)%n,(j+1)*n+i) for j in range(len(rings)-1) for i in range(n)]
 mesh=bpy.data.meshes.new(name);mesh.from_pydata(verts,[],faces);mesh.update()
 o=bpy.data.objects.new(name,mesh);bpy.context.collection.objects.link(o);o.data.materials.append(m)
 mod=o.modifiers.new('Double sided cloth','SOLIDIFY');mod.thickness=.006
 bpy.context.view_layer.objects.active=o;o.select_set(True);bpy.ops.object.modifier_apply(modifier=mod.name);o.select_set(False)
 return parent(o,p)

body=pivot('WardenBody',(0,0,1.13))
garment('Pleated mourning coat',[(.48,.30,.20),(1.05,.22,.15),(1.38,.25,.16),(1.68,.31,.17),(1.78,.16,.12)],cloth,body)
for z in [1.12,1.27,1.42,1.57]:ellipsoid('Black fastening',(0,-.174,z),(.024,.012,.027),brass,body)
for side in [-1,1]:
 rod('Frayed collar',(0,-.14,1.58),(side*.14,-.13,1.77),.025,edge,body)
 leg=pivot('LegL' if side<0 else 'LegR',(side*.115,0,1.02))
 rod('Shin',(side*.115,0,.98),(side*.115,.025,.17),.057,black,leg)
 ellipsoid('Worn school shoe',(side*.115,-.075,.09),(.083,.17,.088),black,leg)
 arm=pivot('ArmL' if side<0 else 'ArmR',(side*.28,0,1.64),body)
 rod('Hanging sleeve',(side*.29,0,1.63),(side*.36,-.015,1.17),.10,cloth,arm)
 elbow=pivot('ForearmL' if side<0 else 'ForearmR',(side*.36,-.015,1.17),arm)
 ellipsoid('Wooden elbow',(side*.36,-.015,1.17),(.055,.052,.06),skin,elbow)
 rod('Long forearm',(side*.36,-.015,1.17),(side*.39,-.05,.77),.046,skin,elbow)
 ellipsoid('Carved palm',(side*.39,-.06,.71),(.072,.034,.105),skin,elbow)
 for finger in range(4):
  x=side*.39+(finger-1.5)*.033;end=.49+abs(finger-1.3)*.022
  rod('Long finger',(x,-.065,.67),(x,-.08,end),.014,skin,elbow)
  rod('Curled fingertip',(x,-.08,end),(x,-.115,end+.025),.011,black,elbow)
 rod('Thumb',(side*.33,-.065,.74),(side*.30,-.10,.62),.022,skin,elbow)

head=pivot('WardenHead',(0,0,1.78),body)
ellipsoid('Hood',(0,.018,1.985),(.18,.145,.255),cloth,head)
ellipsoid('Long death mask',(0,-.107,1.984),(.133,.071,.213),mask,head)
for side in [-1,1]:
 ellipsoid('Empty eye slit',(side*.054,-.174,2.035),(.037,.01,.016),black,head)
 rod('Tear stain',(side*.055,-.174,2.025),(side*.044,-.17,1.948),.006,black,head)
rod('Nasal ridge',(0,-.183,2.025),(0,-.193,1.965),.012,mask,head)
rod('Sealed mouth',(-.048,-.165,1.902),(.048,-.165,1.902),.006,black,head)
for x in [-.036,-.018,0,.018,.036]:rod('Mouth stitch',(x-.004,-.169,1.915),(x+.004,-.17,1.89),.003,red,head)
for i in range(7):
 x=(i-3)*.038
 rod('Trailing hood cord',(x,.11,2.04),(x*1.15,.13,1.64+(i%3)*.065),.014,cloth,head)
# Attendance tags and a hanging brass dismissal bell.
rod('Bell cord',(.17,-.16,1.38),(.20,-.20,1.08),.009,red,body)
bpy.ops.mesh.primitive_cone_add(vertices=20,radius1=.057,radius2=.022,depth=.08,location=(.20,-.20,1.045))
o=bpy.context.object;o.name='Dismissal bell';o.data.materials.append(brass);parent(o,body)
ellipsoid('Bell clapper',(.20,-.20,.995),(.015,.015,.02),black,body)

bpy.ops.wm.save_as_mainfile(filepath=str(root/'SourceArt/attendance-warden.blend'))
bpy.ops.export_scene.fbx(filepath=str(out/'attendance-warden.fbx'),axis_forward='-Z',axis_up='Y',apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',add_leaf_bones=False,bake_anim=False)
records=[]
for m in bpy.data.materials:
 b=m.node_tree.nodes.get('Principled BSDF')
 records.append(dict(name=m.name,color=list(m.diffuse_color),metallic=b.inputs['Metallic'].default_value,roughness=b.inputs['Roughness'].default_value))
(out/'attendance-warden.materials.json').write_text(json.dumps(dict(materials=records)),encoding='utf-8')
print('ATTENDANCE_WARDEN_READY')
