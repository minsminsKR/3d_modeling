"""Silent front/side/face studio of actual source models, with anatomical socket fitting."""
from pathlib import Path
import argparse,sys
import bpy
from mathutils import Vector
HERE=Path(__file__).resolve().parent
parser=argparse.ArgumentParser();parser.add_argument('--output',type=Path,required=True);parser.add_argument('--samples',type=int,default=12)
args=parser.parse_args(sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else []);args.output.mkdir(parents=True,exist_ok=True)
def co(p):return Vector((p[0],-p[2],p[1]))
def runtime(o):return o.type=='MESH' and not o.name.startswith(('SCULPT HIGH |','LOD1 |'))
def bounds(objects):
    points=[o.matrix_world@v.co for o in objects for v in o.data.vertices]
    return Vector(tuple(min(p[i] for p in points) for i in range(3))),Vector(tuple(max(p[i] for p in points) for i in range(3)))
bpy.ops.wm.open_mainfile(filepath=str(HERE/'wraith-body.blend'))
body=next(o for o in bpy.context.scene.objects if o.type=='EMPTY' and not o.parent)
lo,hi=bounds([o for o in bpy.context.scene.objects if runtime(o)])
body.scale=(2.26/(hi.x-lo.x),3.95/(hi.y-lo.y),1.64/(hi.z-lo.z));body.location.z=-lo.z*body.scale.z
bpy.context.view_layer.update();socket=bpy.data.objects['HeadSocket']
with bpy.data.libraries.load(str(HERE/'wraith-mask.blend'),link=False) as (source,target):target.objects=[name for name in source.objects if not name.startswith(('SCULPT HIGH |','LOD1 |'))]
for o in target.objects:
    if o:bpy.context.scene.collection.objects.link(o)
mask=next(o for o in target.objects if o and o.type=='EMPTY' and not o.parent)
shell=next(o for o in target.objects if o and o.name.startswith('Aged human smiling mask shell'))
lo,hi=bounds([shell]);mask.scale=Vector((1,1,1))*(.74/(hi.z-lo.z));bpy.context.view_layer.update()
joint=next(o for o in target.objects if o and o.name=='FaceJoint');mask.location+=socket.matrix_world.translation-joint.matrix_world.translation
bpy.context.view_layer.update()
scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.samples=args.samples;scene.cycles.use_denoising=True
scene.render.resolution_x=1200;scene.render.resolution_y=1000;scene.render.resolution_percentage=100
scene.view_settings.view_transform='AgX';scene.world.color=(.022,.021,.018)
def plain(name,color,rough):
    m=bpy.data.materials.new(name);m.use_nodes=True;n=m.node_tree.nodes.get('Principled BSDF');n.inputs['Base Color'].default_value=(*color,1);n.inputs['Roughness'].default_value=rough;return m
bpy.ops.mesh.primitive_plane_add(size=20,location=co((0,-.015,1)));ground=bpy.context.object;ground.data.materials.append(plain('Studio floor',(.035,.030,.024),.65))
for at,power,size,color in [((-2,3,-2),600,3,(.93,.83,.70)),((2,1.8,-.5),200,2,(.70,.76,.79)),((0,3.2,3.5),700,3,(.78,.81,.72))]:
    bpy.ops.object.light_add(type='AREA',location=co(at));lamp=bpy.context.object;lamp.data.energy=power;lamp.data.size=size;lamp.data.color=color
    lamp.rotation_euler=(co((0,1.0,1.0))-lamp.location).to_track_quat('-Z','Y').to_euler()
bpy.ops.object.camera_add();camera=bpy.context.object;scene.camera=camera
def view(name,at,target,lens,orthographic=None):
    camera.location=co(at);camera.rotation_euler=(co(target)-camera.location).to_track_quat('-Z','Y').to_euler()
    camera.data.type='ORTHO' if orthographic else 'PERSP';camera.data.lens=lens
    if orthographic:camera.data.ortho_scale=orthographic
    scene.render.filepath=str(args.output/(name+'.png'));bpy.ops.render.render(write_still=True)
view('front',(1.55,1.18,-4.7),(0,1.07,.3),43)
view('side',(5.5,1.15,1.0),(0,.95,1.15),48,4.8)
view('face',(.47,1.48,-1.7),(0,1.44,-.32),68)
view('top',(0,6.5,1.0),(0,0,1.0),45,4.8)
print('MANYHAND_REFERENCE_REVIEW_READY',args.output,flush=True)
