"""Neutral CPU previews of source-derived temple LODs in Blender 4.0.2.

Run in background with -- --output <new-directory>. These pictures do not
establish game-camera placement, visibility, lighting or performance.
"""
import argparse
import sys
from pathlib import Path
import bpy
from mathutils import Vector

parser = argparse.ArgumentParser()
parser.add_argument('--output', required=True)
args = parser.parse_args(sys.argv[sys.argv.index('--') + 1:])
output = Path(args.output).resolve()
output.mkdir(parents=True, exist_ok=False)
here = Path(__file__).resolve().parent
bpy.ops.wm.open_mainfile(filepath=str(here / 'temple-hall-v2.blend'))
scene = bpy.context.scene
for collection in bpy.data.collections:
    collection.hide_render = False
    collection.hide_viewport = False
for obj in scene.objects:
    obj.hide_render = True
    obj.hide_set(False)
scene.render.engine = 'CYCLES'
scene.cycles.device = 'CPU'
scene.cycles.samples = 8
scene.cycles.use_denoising = False
scene.render.threads_mode = 'FIXED'
scene.render.threads = 2
scene.render.resolution_x = scene.render.resolution_y = 640
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = 'PNG'
scene.render.image_settings.color_mode = 'RGBA'
scene.render.film_transparent = True
scene.view_settings.view_transform = 'Standard'
scene.view_settings.exposure = .15
world = bpy.data.worlds.new('Neutral Korean hall preview')
world.use_nodes = True
world.node_tree.nodes.get('Background').inputs['Color'].default_value = (.35, .36, .38, 1)
world.node_tree.nodes.get('Background').inputs['Strength'].default_value = .8
scene.world = world

def aim(obj, point):
    obj.rotation_euler = (Vector(point) - obj.location).to_track_quat('-Z', 'Y').to_euler()

light = bpy.data.lights.new('Hall preview softbox', 'AREA')
light.energy = 4500
light.size = 14
lamp = bpy.data.objects.new('Hall preview softbox', light)
scene.collection.objects.link(lamp)
lamp.hide_render = False
camera = bpy.data.cameras.new('Hall preview camera')
cam = bpy.data.objects.new('Hall preview camera', camera)
scene.collection.objects.link(cam)
scene.camera = cam
camera.type = 'ORTHO'
camera.ortho_scale = 11.6
near = bpy.data.objects['CE_temple-hall-v2_LOD0']
far = bpy.data.objects['CE_temple-hall-v2_LOD1']
height = max((near.matrix_world @ Vector(corner)).z for corner in near.bound_box)
for angle, position in (('front', (0, -23, height * .75)),
                        ('right', (23, 0, height * .75)),
                        ('rear', (0, 23, height * .75)),
                        ('three-quarter', (17, -17, height))):
    target = (0, 0, height * .48)
    cam.location = position
    aim(cam, target)
    lamp.location = Vector(position) * .7 + Vector((0, 0, height))
    aim(lamp, target)
    for name, obj in (('lod0', near), ('lod1', far)):
        near.hide_render = far.hide_render = True
        obj.hide_render = False
        scene.render.filepath = str(output / (angle + '-' + name + '.png'))
        bpy.ops.render.render(write_still=True)
        print('TEMPLE_HALL_PREVIEW', angle, name, flush=True)
