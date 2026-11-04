"""Render low-cost CPU material views of the editable gate review prototype.

Run on a private desktop with Blender 4.0.2 in background mode. The output is
for asset inspection, not Unity appearance, displayed FPS or device audio.
The source .blend is read only and the output directory must not already exist.
"""
import argparse
import hashlib
import json
from pathlib import Path
import sys

import bpy
from mathutils import Vector

parser = argparse.ArgumentParser()
parser.add_argument('--output', required=True)
parser.add_argument('--views', nargs='+', help='Render only these named views')
parser.add_argument('--asset-directory', help='Review a separate editable blend and its geometry report')
arguments = parser.parse_args(sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else [])
output = Path(arguments.output).resolve()
output.mkdir(parents=True, exist_ok=False)
root = Path(arguments.asset_directory).resolve() if arguments.asset_directory else Path(__file__).resolve().parent / 'gate-opening'
source = root / 'gate-opening.blend'
report = json.loads((root / 'geometry-report.json').read_text(encoding='utf-8'))

def digest(path):
    result = hashlib.sha256()
    with path.open('rb') as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b''): result.update(block)
    return result.hexdigest()

if digest(source) != report['editableSha256']:
    raise RuntimeError('Editable gate does not match its source report')
bpy.ops.wm.open_mainfile(filepath=str(source))
scene = bpy.context.scene
for collection in bpy.data.collections:
    collection.hide_viewport = False
    collection.hide_render = False
for obj in scene.objects:
    obj.hide_render = True
parts = {name: bpy.data.objects[name] for name in ('Gate frame', 'Left thin leaf', 'Right thin leaf')}
for obj in parts.values(): obj.hide_set(False)
scene.render.engine = 'CYCLES'
scene.cycles.device = 'CPU'
scene.cycles.samples = 8
scene.cycles.use_denoising = False
scene.render.threads_mode = 'FIXED'
scene.render.threads = 2
scene.render.resolution_x = scene.render.resolution_y = 512
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = 'PNG'
scene.render.film_transparent = False
world = bpy.data.worlds.new('CPU gate review world')
world.use_nodes = True
world.node_tree.nodes.get('Background').inputs['Color'].default_value = (.35, .38, .42, 1)
world.node_tree.nodes.get('Background').inputs['Strength'].default_value = .65
scene.world = world

def aim(obj, target):
    obj.rotation_euler = (Vector(target) - obj.location).to_track_quat('-Z', 'Y').to_euler()

for name, position in (('Front softbox', (-2, -10, 6)), ('Back softbox', (-2, 10, 6))):
    light = bpy.data.lights.new(name, 'AREA')
    light.energy = 3200; light.shape = 'DISK'; light.size = 7
    obj = bpy.data.objects.new(name, light); scene.collection.objects.link(obj)
    obj.location = position; aim(obj, (0, 0, 4.5))
camera_data = bpy.data.cameras.new('CPU gate review camera')
camera = bpy.data.objects.new('CPU gate review camera', camera_data)
scene.collection.objects.link(camera); scene.camera = camera
camera_data.type = 'ORTHO'
views = (
    ('closed-front', 1, (0, -20, 6), (0, 0, 5), 16, tuple(parts)),
    ('open-front', 73, (0, -20, 6), (0, 0, 5), 16, tuple(parts)),
    ('open-back-oblique', 73, (11, 16, 7), (0, 0, 5), 16, tuple(parts)),
    ('left-front', 1, (-1.5, -10, 3.6), (-1.5, -1.5, 3.4), 6.2, ('Left thin leaf',)),
    ('right-front', 1, (1.5, -10, 3.6), (1.5, -1.5, 3.4), 6.2, ('Right thin leaf',)),
    ('left-back', 1, (-1.5, 10, 3.6), (-1.5, -1.2, 3.4), 6.2, ('Left thin leaf',)),
    ('left-free-edge', 1, (8, -1.3, 3.4), (0, -1.3, 3.4), 6.2, ('Left thin leaf',)),
    ('right-free-edge', 1, (-8, -1.3, 3.4), (0, -1.3, 3.4), 6.2, ('Right thin leaf',)),
    ('left-hinge-half', 37, (-6, -8, 4), (-3.05, -1.3, 3.4), 4.0, tuple(parts)),
    ('right-hinge-half', 37, (6, -8, 4), (3.05, -1.3, 3.4), 4.0, tuple(parts)),
    ('left-hinge-open', 73, (-6, 5, 4), (-3.05, -1.3, 3.4), 4.0, tuple(parts)),
    ('right-hinge-open', 73, (6, 5, 4), (3.05, -1.3, 3.4), 4.0, tuple(parts)),
    ('left-hinge-inside-half', 37, (0, -6, 4), (-3.05, -1.3, 3.4), 4.0, tuple(parts)),
    ('right-hinge-inside-half', 37, (0, -6, 4), (3.05, -1.3, 3.4), 4.0, tuple(parts)),
    ('left-hinge-inside-open', 73, (0, 5, 4), (-3.05, -1.3, 3.4), 4.0, tuple(parts)),
    ('right-hinge-inside-open', 73, (0, 5, 4), (3.05, -1.3, 3.4), 4.0, tuple(parts)),
)
if arguments.views:
    missing = set(arguments.views) - {view[0] for view in views}
    if missing: raise ValueError('Unknown gate preview views: ' + ', '.join(sorted(missing)))
    views = tuple(view for view in views if view[0] in arguments.views)
results = []
for name, frame, position, target, scale, visible in views:
    for part_name, obj in parts.items(): obj.hide_render = part_name not in visible
    scene.frame_set(frame)
    camera.location = position; aim(camera, target); camera_data.ortho_scale = scale
    path = output / (name + '.png')
    scene.render.filepath = str(path)
    print('CPU_GATE_PREVIEW', name, 'device', scene.cycles.device, 'threads', scene.render.threads, flush=True)
    bpy.ops.render.render(write_still=True)
    results.append(dict(name=name, frame=frame, file=path.name, sha256=digest(path)))
if digest(source) != report['editableSha256']:
    raise RuntimeError('Editable gate changed during preview')
result = dict(scope='CPU Blender asset preview only; not a Unity camera or performance audit',
              blender=bpy.app.version_string, device=scene.cycles.device, threads=scene.render.threads,
              samples=scene.cycles.samples, resolution=[512, 512], sourceSha256=report['editableSha256'], views=results)
(output / 'preview.json').write_text(json.dumps(result, indent=2) + '\n', encoding='utf-8')
print('CPU_GATE_PREVIEW_PASS', flush=True)
