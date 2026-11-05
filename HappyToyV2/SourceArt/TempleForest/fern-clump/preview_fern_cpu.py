"""Transparent 4-angle fern source/LOD previews using Blender 4.0.2 CPU.

Run with -- --output <new-directory>; not an in-game camera quality review.
"""
import argparse,sys
from pathlib import Path
import bpy
from mathutils import Vector,Matrix

p=argparse.ArgumentParser();p.add_argument('--output',required=True)
a=p.parse_args(sys.argv[sys.argv.index('--')+1:]);output=Path(a.output).resolve();output.mkdir(parents=True,exist_ok=False)
here=Path(__file__).resolve().parent;bpy.ops.wm.open_mainfile(filepath=str(here/'fern-clump.blend'));scene=bpy.context.scene
for col in bpy.data.collections:col.hide_render=False;col.hide_viewport=False
for obj in scene.objects:obj.hide_render=True;obj.hide_set(False)
scene.render.engine='CYCLES';scene.cycles.device='CPU';scene.cycles.samples=8;scene.cycles.use_denoising=False
scene.render.threads_mode='FIXED';scene.render.threads=2;scene.render.resolution_x=512;scene.render.resolution_y=512;scene.render.resolution_percentage=100
scene.render.image_settings.file_format='PNG';scene.render.image_settings.color_mode='RGBA';scene.render.film_transparent=True
scene.view_settings.view_transform='Standard';scene.view_settings.exposure=.4
world=bpy.data.worlds.new('Fern preview world');world.use_nodes=True
world.node_tree.nodes.get('Background').inputs['Color'].default_value=(.25,.27,.28,1)
world.node_tree.nodes.get('Background').inputs['Strength'].default_value=.8;scene.world=world
def aim(obj,point):obj.rotation_euler=(Vector(point)-obj.location).to_track_quat('-Z','Y').to_euler()
light=bpy.data.lights.new('Fern softbox','AREA');light.energy=70;light.size=2
lamp=bpy.data.objects.new('Fern softbox',light);scene.collection.objects.link(lamp);lamp.location=(1,-2,3);aim(lamp,(0,0,.2));lamp.hide_render=False
camera=bpy.data.cameras.new('Fern preview camera');cam=bpy.data.objects.new('Fern preview camera',camera)
scene.collection.objects.link(cam);scene.camera=cam;camera.type='ORTHO';camera.ortho_scale=1.5
source=bpy.data.objects['geometry_0'];lod=bpy.data.objects['CE_fern-clump_LOD0'];far=bpy.data.objects['CE_fern-clump_LOD1']
corners=[source.matrix_world@Vector(x) for x in source.bound_box]
lo=Vector(tuple(min(x[i] for x in corners) for i in range(3)));hi=Vector(tuple(max(x[i] for x in corners) for i in range(3)))
centre=Vector(((lo.x+hi.x)/2,(lo.y+hi.y)/2,lo.z));source.matrix_world=Matrix.Scale(1/(hi.x-lo.x),4)@Matrix.Translation(-centre)@source.matrix_world
for angle,position in (('front',(0,-2.3,1.1)),('side',(2.3,0,1.1)),('rear',(0,2.3,1.1)),('three-quarter',(1.7,-1.7,1.3))):
    cam.location=position;aim(cam,(0,0,.17));lamp.location=Vector(position)*1.1+Vector((0,0,1));aim(lamp,(0,0,.17))
    for name,obj in (('source',source),('lod0',lod),('lod1',far)):
        source.hide_render=lod.hide_render=far.hide_render=True;obj.hide_render=False;scene.render.filepath=str(output/(angle+'-'+name+'.png'))
        bpy.ops.render.render(write_still=True);print('FERN_PREVIEW',angle,name,flush=True)
