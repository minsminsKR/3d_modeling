"""CPU-render two angles of the normalized original and its candidate LODs.

Blender 4.0.2; output must be a new directory. Not an in-game visual review.
"""
import argparse
from pathlib import Path
import sys
import bpy
from mathutils import Matrix, Vector

parser = argparse.ArgumentParser()
parser.add_argument('--output', required=True)
args = parser.parse_args(sys.argv[sys.argv.index('--')+1:])
output = Path(args.output).resolve()
output.mkdir(parents=True, exist_ok=False)
root = Path(__file__).resolve().parent
bpy.ops.wm.open_mainfile(filepath=str(root / 'temple-stone-cluster.blend'))
scene = bpy.context.scene
for collection in bpy.data.collections:
    collection.hide_render = False
    collection.hide_viewport = False
for obj in scene.objects:
    obj.hide_render = True
    obj.hide_set(False)
source = bpy.data.objects['geometry_0']
near = bpy.data.objects['CE_temple-stone-cluster_LOD0']
far = bpy.data.objects['CE_temple-stone-cluster_LOD1']
corners = [source.matrix_world @ Vector(corner) for corner in source.bound_box]
lo = Vector(tuple(min(point[i] for point in corners) for i in range(3)))
hi = Vector(tuple(max(point[i] for point in corners) for i in range(3)))
centre = Vector(((lo.x+hi.x)*.5, (lo.y+hi.y)*.5, lo.z))
sx, sy = 1.8/(hi.x-lo.x), 1.8/(hi.y-lo.y)
sz = min(sx, .12/(hi.z-lo.z))
source.matrix_world = Matrix.Diagonal((sx,sy,sz,1)) @ Matrix.Translation(-centre) @ source.matrix_world

scene.render.engine='CYCLES'
scene.cycles.device='CPU';scene.cycles.samples=8;scene.cycles.use_denoising=False
scene.render.threads_mode='FIXED';scene.render.threads=2
scene.render.resolution_x=scene.render.resolution_y=512;scene.render.resolution_percentage=100
scene.render.image_settings.file_format='PNG';scene.render.image_settings.color_mode='RGBA';scene.render.film_transparent=True
scene.view_settings.view_transform='Standard';scene.view_settings.exposure=.1
world=bpy.data.worlds.new('Stone preview world');world.use_nodes=True
world.node_tree.nodes['Background'].inputs['Color'].default_value=(.20,.23,.26,1)
world.node_tree.nodes['Background'].inputs['Strength'].default_value=.9;scene.world=world


def aim(obj, point):
    obj.rotation_euler = (Vector(point) - obj.location).to_track_quat('-Z','Y').to_euler()


light=bpy.data.lights.new('Softbox','AREA');light.energy=180;light.size=4
lamp=bpy.data.objects.new('Softbox',light);scene.collection.objects.link(lamp);lamp.hide_render=False
camera=bpy.data.cameras.new('Stone preview camera');cam=bpy.data.objects.new('Stone preview camera',camera)
scene.collection.objects.link(cam);scene.camera=cam;camera.type='ORTHO';camera.ortho_scale=2.8
models=(('source',source),('near',near),('far',far))
for name, position in (('high',(2.7,-3.0,3.2)), ('low',(2.9,-3.0,1.25))):
    cam.location=position;aim(cam,(0,0,.06));lamp.location=Vector(position)*1.1+Vector((0,0,2));aim(lamp,(0,0,.06))
    for label,obj in models:
        for _,other in models:other.hide_render=True
        obj.hide_render=False
        scene.render.filepath=str(output/(name+'-'+label+'.png'))
        bpy.ops.render.render(write_still=True)
        print('TEMPLE_STONE_CLUSTER_PREVIEW',name,label,flush=True)
