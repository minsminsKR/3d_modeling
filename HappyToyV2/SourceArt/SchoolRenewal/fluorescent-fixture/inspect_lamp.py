"""Blender 4.0.2 non-mutating tube/casing orientation inspection."""
from pathlib import Path
import runpy
import bpy
from mathutils import Matrix, Vector

SOURCE = Path(__file__).resolve().parent
PROJECT = SOURCE.parents[2]
helper = runpy.run_path(str(PROJECT / 'SourceArt/CorridorEntrance/finish_entrance_assets.py'), run_name='lamp_measure_helpers')
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=str(SOURCE / 'original.glb'))
objects = [o for o in bpy.context.scene.objects if o.type == 'MESH']
lo, hi = helper['bounds'](objects)
normalization = Matrix.Scale(1.2 / (hi.x - lo.x), 4) @ Matrix.Translation(-Vector(((lo.x+hi.x)*.5, (lo.y+hi.y)*.5, lo.z)))
for obj in objects:
    obj.matrix_world = normalization @ obj.matrix_world
scene = bpy.context.scene
scene.render.engine = 'BLENDER_EEVEE'
scene.eevee.use_gtao = True
scene.eevee.gtao_distance = .12
scene.render.resolution_x = 1700
scene.render.resolution_y = 650
scene.render.resolution_percentage = 100
scene.view_settings.view_transform = 'Standard'
scene.view_settings.look = 'Medium High Contrast'
scene.world = bpy.data.worlds.new('Neutral inspection world')
scene.world.use_nodes = True
scene.world.node_tree.nodes['Background'].inputs[0].default_value = (.32,.32,.32,1)
scene.world.node_tree.nodes['Background'].inputs[1].default_value = .7
camera = bpy.data.cameras.new('Actual source orthographic inspection')
camera.type = 'ORTHO'
camera.ortho_scale = 1.4
obj = bpy.data.objects.new('Actual source orthographic inspection', camera)
scene.collection.objects.link(obj)
scene.camera = obj
for index, z in enumerate((3,-3)):
    light = bpy.data.lights.new('Neutral inspection area' + str(index), 'AREA')
    light.energy = 170
    light.size = 3
    light_obj = bpy.data.objects.new(light.name, light)
    scene.collection.objects.link(light_obj)
    light_obj.location = (0,0,z)
    light_obj.rotation_euler = (Vector((0,0,.08)) - light_obj.location).to_track_quat('-Z','Y').to_euler()
output = PROJECT / 'Builds/ModelingDownloads/SchoolRenewal/fluorescent-fixture'
output.mkdir(parents=True,exist_ok=True)
for name, z in (('top',3),('underside',-3)):
    obj.location = (0,0,z)
    obj.rotation_euler = (Vector((0,0,.08)) - obj.location).to_track_quat('-Z','Y').to_euler()
    scene.render.filepath = str(output / ('lamp-source-'+name+'.png'))
    bpy.ops.render.render(write_still=True)
