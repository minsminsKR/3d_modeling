"""Opt-in two-sided architectural preview in Builds; never changes sources."""
from pathlib import Path
import argparse
import sys
import bpy
from mathutils import Vector

SOURCE = Path(__file__).resolve().parent
PROJECT = SOURCE.parents[1]
parser = argparse.ArgumentParser()
parser.add_argument('--key', required=True)
parser.add_argument('--output', required=True)
parser.add_argument('--original', action='store_true')
args = parser.parse_args(sys.argv[sys.argv.index('--')+1:])
if args.original:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=str(SOURCE/args.key/'original.glb'))
    meshes = [o for o in bpy.context.scene.objects if o.type == 'MESH']
else:
    bpy.ops.wm.open_mainfile(filepath=str(SOURCE/args.key/(args.key+'.blend')))
    meshes = [o for o in bpy.context.scene.objects if o.type == 'MESH' and o.name.startswith('SR_'+args.key+'_LOD0')]
for obj in bpy.context.scene.objects:
    obj.hide_render = obj not in meshes
points = [o.matrix_world@Vector(p) for o in meshes for p in o.bound_box]
low = Vector(tuple(min(p[a] for p in points) for a in range(3)))
high = Vector(tuple(max(p[a] for p in points) for a in range(3)))
centre = (low+high)*.5
extent = max(high-low)
scene = bpy.context.scene
scene.render.engine = 'BLENDER_EEVEE'
scene.eevee.use_gtao = True
scene.eevee.gtao_distance = .10*extent
scene.view_settings.view_transform = 'Standard'
scene.render.resolution_x = scene.render.resolution_y = 768
scene.render.resolution_percentage = 100
scene.world = bpy.data.worlds.new('Architectural review world')
scene.world.color = (.10,.10,.10)
for name, pos in [('top',(2,3,4)),('bottom',(-3,-2,-4))]:
    data = bpy.data.lights.new('Review '+name,'AREA')
    data.energy = 400*extent*extent
    data.shape = 'DISK'
    data.size = extent*3
    light = bpy.data.objects.new(data.name,data)
    scene.collection.objects.link(light)
    light.location = centre+Vector(pos)*extent
    light.rotation_euler = (centre-light.location).to_track_quat('-Z','Y').to_euler()
data = bpy.data.cameras.new('Architectural review')
camera = bpy.data.objects.new(data.name,data)
scene.collection.objects.link(camera)
scene.camera = camera
data.type = 'ORTHO'
data.ortho_scale = extent*1.35
output = Path(args.output).resolve()
if (PROJECT/'Builds').resolve() not in output.parents:
    raise ValueError('Review output must be inside Builds')
output.mkdir(parents=True,exist_ok=True)
for name,pos in [('top',(.1,-.2,4)),('bottom',(.1,-.2,-4)),('grazing',(1.8,-2.5,.6))]:
    camera.location = centre+Vector(pos)*extent
    camera.rotation_euler = (centre-camera.location).to_track_quat('-Z','Y').to_euler()
    scene.render.filepath = str(output/(args.key+'-'+name+'.png'))
    bpy.ops.render.render(write_still=True)
print('SCHOOL_ARCHITECTURE_REVIEW_RENDERED',args.key)
