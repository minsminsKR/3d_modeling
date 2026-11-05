"""Inspect source-derived lantern LODs from three angles without the GPU.

Run Blender 4.0.2 in background with -- --output <new-directory>.
These are neutral CPU material/shape checks, not in-game lighting approval.
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
bpy.ops.wm.open_mainfile(filepath=str(here / 'stone-lantern.blend'))
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
scene.view_settings.exposure = .2
world = bpy.data.worlds.new('Neutral stone lantern preview')
world.use_nodes = True
world.node_tree.nodes.get('Background').inputs['Color'].default_value = (.34, .36, .38, 1)
world.node_tree.nodes.get('Background').inputs['Strength'].default_value = .8
scene.world = world

def aim(obj, point):
    obj.rotation_euler = (Vector(point) - obj.location).to_track_quat('-Z', 'Y').to_euler()

light = bpy.data.lights.new('Stone preview softbox', 'AREA')
light.energy = 250
light.size = 4
lamp = bpy.data.objects.new('Stone preview softbox', light)
scene.collection.objects.link(lamp)
lamp.hide_render = False
camera = bpy.data.cameras.new('Stone preview camera')
cam = bpy.data.objects.new('Stone preview camera', camera)
scene.collection.objects.link(cam)
scene.camera = cam
camera.type = 'ORTHO'
camera.ortho_scale = 2.15
near = bpy.data.objects['CE_stone-lantern_LOD0']
far = bpy.data.objects['CE_stone-lantern_LOD1']
for angle, position in (('front', (0, -4.2, 2.0)), ('side', (4.2, 0, 2.0)),
                        ('three-quarter', (3.1, -3.1, 2.4))):
    cam.location = position
    aim(cam, (0, 0, .70))
    lamp.location = Vector(position) * 1.1 + Vector((0, 0, 2))
    aim(lamp, (0, 0, .70))
    for name, obj in (('lod0', near), ('lod1', far)):
        near.hide_render = far.hide_render = True
        obj.hide_render = False
        scene.render.filepath = str(output / (angle + '-' + name + '.png'))
        bpy.ops.render.render(write_still=True)
        print('STONE_LANTERN_PREVIEW', angle, name, flush=True)
