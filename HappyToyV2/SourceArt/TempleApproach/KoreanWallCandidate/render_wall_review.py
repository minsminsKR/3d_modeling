"""Render a CPU-only visual check of the reduced storefront FBX."""
from pathlib import Path
import sys
import bpy
from mathutils import Vector

root = Path(__file__).resolve().parent
output = Path(sys.argv[sys.argv.index('--') + 1]).resolve()
arguments = sys.argv[sys.argv.index('--') + 2:]
bpy.ops.wm.read_factory_settings(use_empty=True)
if arguments and arguments[0] == 'original':
    bpy.ops.import_scene.gltf(filepath=str(root / 'model.glb'))
else:
    bpy.ops.import_scene.fbx(filepath=str(root / 'KoreanWall.fbx'))
objects = [o for o in bpy.context.scene.objects if o.type == 'MESH']
if not objects: raise RuntimeError('Missing storefront mesh')
bpy.context.view_layer.update()
points = [o.matrix_world @ Vector(c) for o in objects for c in o.bound_box]
lo = Vector(tuple(min(p[i] for p in points) for i in range(3)))
hi = Vector(tuple(max(p[i] for p in points) for i in range(3)))
center = (lo + hi) * .5
extent = max(hi - lo)
camera_data = bpy.data.cameras.new('Review camera')
camera = bpy.data.objects.new('Review camera', camera_data)
bpy.context.scene.collection.objects.link(camera)
direction = (-1.3, 1.8, 1.1) if 'rear' in arguments else (1.3, -1.8, 1.1)
camera.location = center + Vector(direction).normalized() * extent * 2.7
camera.rotation_euler = (center - camera.location).to_track_quat('-Z', 'Y').to_euler()
camera_data.type = 'ORTHO'; camera_data.ortho_scale = extent * 1.45
bpy.context.scene.camera = camera
for location, energy, size in [((3, -4, 5), 700, 5), ((-4, -2, 3), 300, 4)]:
    data = bpy.data.lights.new('Review area', 'AREA'); data.energy = energy; data.shape = 'DISK'; data.size = size
    light = bpy.data.objects.new('Review area', data); bpy.context.scene.collection.objects.link(light)
    light.location = center + Vector(location); light.rotation_euler = (center - light.location).to_track_quat('-Z', 'Y').to_euler()
scene = bpy.context.scene; scene.render.engine = 'CYCLES'; scene.cycles.device = 'CPU'; scene.cycles.samples = 24
scene.render.threads_mode = 'FIXED'; scene.render.threads = 2
scene.render.resolution_x = 700; scene.render.resolution_y = 540; scene.render.resolution_percentage = 100
scene.world = bpy.data.worlds.new('Review world'); scene.world.color = (.15, .15, .15)
scene.render.image_settings.file_format = 'PNG'
scene.render.filepath = str(output); bpy.ops.render.render(write_still=True)
