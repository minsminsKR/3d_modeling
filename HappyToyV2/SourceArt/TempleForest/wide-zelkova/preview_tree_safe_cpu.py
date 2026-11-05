"""Render four neutral views of the source-preserving near mesh on CPU.

Run with -- --output <new-directory>. This is not a Unity camera check.
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
bpy.ops.wm.open_mainfile(filepath=str(here / 'wide-zelkova-safe.blend'))
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
scene.render.resolution_x = 512
scene.render.resolution_y = 512
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = 'PNG'
scene.render.image_settings.color_mode = 'RGBA'
scene.render.film_transparent = True
scene.view_settings.view_transform = 'Standard'
scene.view_settings.exposure = .4
world = bpy.data.worlds.new('Tree preview neutral world')
world.use_nodes = True
world.node_tree.nodes.get('Background').inputs['Color'].default_value = (.25, .27, .28, 1)
world.node_tree.nodes.get('Background').inputs['Strength'].default_value = .8
scene.world = world


def aim(obj, point):
    obj.rotation_euler = (Vector(point) - obj.location).to_track_quat('-Z', 'Y').to_euler()


light = bpy.data.lights.new('Tree softbox', 'AREA')
light.energy = 3500
light.size = 10
lamp = bpy.data.objects.new('Tree softbox', light)
scene.collection.objects.link(lamp)
lamp.hide_render = False
camera = bpy.data.cameras.new('Tree preview camera')
cam = bpy.data.objects.new('Tree preview camera', camera)
scene.collection.objects.link(cam)
scene.camera = cam
camera.type = 'ORTHO'
camera.ortho_scale = 16
models = [('safe', bpy.data.objects['CE_wide-zelkova-safe_LOD0'])]
for angle, position in (('front', (0, -22, 6)), ('side', (22, 0, 6)),
                        ('rear', (0, 22, 6)), ('three-quarter', (16, -16, 6))):
    cam.location = position
    lamp.location = position
    aim(cam, (0, 0, 4))
    aim(lamp, (0, 0, 4))
    for name, obj in models:
        for _, other in models:
            other.hide_render = True
        obj.hide_render = False
        scene.render.filepath = str(output / (angle + '-' + name + '.png'))
        bpy.ops.render.render(write_still=True)
        print('WIDE_TREE_PREVIEW', angle, name, flush=True)
