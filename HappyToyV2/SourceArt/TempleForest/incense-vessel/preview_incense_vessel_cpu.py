"""Neutral CPU previews of source-derived vessel LODs in Blender 4.0.2.

Run in background with -- --output <new-directory>. No game-camera approval.
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
bpy.ops.wm.open_mainfile(filepath=str(here / 'incense-vessel.blend'))
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
scene.render.resolution_x = scene.render.resolution_y = 512
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = 'PNG'
scene.render.image_settings.color_mode = 'RGBA'
scene.render.film_transparent = True
scene.view_settings.view_transform = 'Standard'
scene.view_settings.exposure = .1
world = bpy.data.worlds.new('Neutral vessel preview')
world.use_nodes = True
world.node_tree.nodes.get('Background').inputs['Color'].default_value = (.32, .32, .32, 1)
world.node_tree.nodes.get('Background').inputs['Strength'].default_value = .8
scene.world = world

def aim(obj, point):
    obj.rotation_euler = (Vector(point) - obj.location).to_track_quat('-Z', 'Y').to_euler()

light = bpy.data.lights.new('Vessel preview softbox', 'AREA')
light.energy = 20
light.size = .8
lamp = bpy.data.objects.new('Vessel preview softbox', light)
scene.collection.objects.link(lamp)
lamp.hide_render = False
camera = bpy.data.cameras.new('Vessel preview camera')
cam = bpy.data.objects.new('Vessel preview camera', camera)
scene.collection.objects.link(cam)
scene.camera = cam
camera.type = 'ORTHO'
camera.ortho_scale = .65
near = bpy.data.objects['CE_incense-vessel_LOD0']
far = bpy.data.objects['CE_incense-vessel_LOD1']
height = max((near.matrix_world @ Vector(corner)).z for corner in near.bound_box)
for angle, position in (('front', (0, -1.5, height * 1.5)),
                        ('side', (1.5, 0, height * 1.5)),
                        ('above', (.6, -.6, 1.6)),
                        ('three-quarter', (1.1, -1.1, height * 2.4))):
    target = (0, 0, height * .48)
    cam.location = position
    aim(cam, target)
    lamp.location = Vector(position) * .7 + Vector((0, 0, .7))
    aim(lamp, target)
    for name, obj in (('lod0', near), ('lod1', far)):
        near.hide_render = far.hide_render = True
        obj.hide_render = False
        scene.render.filepath = str(output / (angle + '-' + name + '.png'))
        bpy.ops.render.render(write_still=True)
        print('INCENSE_VESSEL_PREVIEW', angle, name, flush=True)
