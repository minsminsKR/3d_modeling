"""Render authored and low-poly fern LODs from two angles (Blender CPU)."""
import argparse
from pathlib import Path
import sys

import bpy
from mathutils import Vector

parser = argparse.ArgumentParser()
parser.add_argument('--output', required=True)
args = parser.parse_args(sys.argv[sys.argv.index('--')+1:])
output = Path(args.output).resolve()
output.mkdir(parents=True, exist_ok=False)
root = Path(__file__).resolve().parent
bpy.ops.wm.open_mainfile(filepath=str(root / 'fern-clump.blend'))
scene = bpy.context.scene
with bpy.data.libraries.load(str(root / 'fern-undergrowth.blend'), link=False) as (source, target):
    target.objects = ['Fern undergrowth LOD1']
for obj in target.objects:
    scene.collection.objects.link(obj)
for collection in bpy.data.collections:
    collection.hide_render = False
    collection.hide_viewport = False
for obj in scene.objects:
    obj.hide_render = True
    obj.hide_set(False)
scene.render.engine = 'CYCLES'
scene.cycles.device = 'CPU'
scene.cycles.samples = 8
scene.render.threads_mode = 'FIXED'
scene.render.threads = 2
scene.render.resolution_x = scene.render.resolution_y = 512
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = 'PNG'
scene.render.image_settings.color_mode = 'RGBA'
scene.render.film_transparent = True
scene.view_settings.view_transform = 'Standard'
scene.view_settings.exposure = .4
world = bpy.data.worlds.new('Fern undergrowth preview')
world.use_nodes = True
world.node_tree.nodes['Background'].inputs['Color'].default_value = (.25, .27, .28, 1)
world.node_tree.nodes['Background'].inputs['Strength'].default_value = .8
scene.world = world


def aim(obj, point):
    obj.rotation_euler = (Vector(point) - obj.location).to_track_quat('-Z', 'Y').to_euler()


light = bpy.data.lights.new('Softbox', 'AREA')
light.energy = 70
light.size = 2
lamp = bpy.data.objects.new('Softbox', light)
scene.collection.objects.link(lamp)
lamp.hide_render = False
camera = bpy.data.cameras.new('Preview camera')
cam = bpy.data.objects.new('Preview camera', camera)
scene.collection.objects.link(cam)
scene.camera = cam
camera.type = 'ORTHO'
camera.ortho_scale = 1.5
models = [('source', bpy.data.objects['CE_fern-clump_LOD0']),
          ('near', bpy.data.objects['CE_fern-clump_LOD1']), ('far', target.objects[0])]
for name, position in (('front', (0,-2.3,1.1)), ('side', (2.3,0,1.1))):
    cam.location = position
    aim(cam, (0,0,.17))
    lamp.location = Vector(position) * 1.1 + Vector((0,0,1))
    aim(lamp, (0,0,.17))
    for label, obj in models:
        for _, other in models:
            other.hide_render = True
        obj.hide_render = False
        scene.render.filepath = str(output / (name + '-' + label + '.png'))
        bpy.ops.render.render(write_still=True)
        print('TEMPLE_UNDERGROWTH_PREVIEW', name, label, flush=True)
