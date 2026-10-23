"""Blender 4.0.2 non-mutating front/back measurements of the actual window GLB."""
from pathlib import Path
import runpy
import bpy
from mathutils import Matrix, Vector
import math

SOURCE = Path(__file__).resolve().parent
PROJECT = SOURCE.parents[2]
helper = runpy.run_path(str(PROJECT / 'SourceArt/CorridorEntrance/finish_entrance_assets.py'), run_name='window_measure_helpers')
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=str(SOURCE / 'original.glb'))
objects = [o for o in bpy.context.scene.objects if o.type == 'MESH']
rotation = Matrix.Rotation(math.pi / 2, 4, 'Z')
for obj in objects:
    obj.matrix_world = rotation @ obj.matrix_world
lo, hi = helper['bounds'](objects)
normalization = Matrix.Scale(1.6 / (hi.z - lo.z), 4) @ Matrix.Translation(-Vector(((lo.x+hi.x)*.5, (lo.y+hi.y)*.5, lo.z)))
for obj in objects:
    obj.matrix_world = normalization @ obj.matrix_world
scene = bpy.context.scene
scene.render.engine = 'BLENDER_EEVEE'
scene.eevee.use_gtao = True
scene.eevee.gtao_distance = .25
scene.eevee.gtao_factor = 1
scene.render.resolution_x = 1800
scene.render.resolution_y = 1100
scene.render.resolution_percentage = 100
scene.view_settings.view_transform = 'Standard'
scene.view_settings.look = 'Medium High Contrast'
scene.world = bpy.data.worlds.new('Neutral inspection world')
scene.world.use_nodes = True
scene.world.node_tree.nodes['Background'].inputs[0].default_value = (.32,.32,.32,1)
scene.world.node_tree.nodes['Background'].inputs[1].default_value = .7
camera = bpy.data.cameras.new('Actual source orthographic inspection')
camera.type = 'ORTHO'
camera.ortho_scale = 3.2
obj = bpy.data.objects.new('Actual source orthographic inspection', camera)
scene.collection.objects.link(obj)
scene.camera = obj
for index, y in enumerate((4,-4)):
    light = bpy.data.lights.new('Neutral inspection area' + str(index), 'AREA')
    light.energy = 250
    light.size = 4
    light_obj = bpy.data.objects.new(light.name, light)
    scene.collection.objects.link(light_obj)
    light_obj.location = (0,y,2.8)
    light_obj.rotation_euler = (Vector((0,0,.8)) - light_obj.location).to_track_quat('-Z','Y').to_euler()
output = PROJECT / 'Builds/ModelingDownloads/SchoolRenewal/classroom-window'
output.mkdir(parents=True,exist_ok=True)
for name, y in (('front',4),('back',-4)):
    obj.location = (0,y,.8)
    obj.rotation_euler = (Vector((0,0,.8)) - obj.location).to_track_quat('-Z','Y').to_euler()
    scene.render.filepath = str(output / ('window-source-'+name+'.png'))
    bpy.ops.render.render(write_still=True)
