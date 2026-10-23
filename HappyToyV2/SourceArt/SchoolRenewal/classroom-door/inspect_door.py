"""Measure normalized front/back of the unchanged generated classroom leaf."""
from pathlib import Path
import runpy
import bpy
from mathutils import Matrix,Vector
source=Path(__file__).resolve().parent;project=source.parents[2]
helper=runpy.run_path(str(project/'SourceArt/CorridorEntrance/finish_entrance_assets.py'),run_name='door_measure')
bpy.ops.wm.read_factory_settings(use_empty=True);bpy.ops.import_scene.gltf(filepath=str(source/'original.glb'))
objects=[o for o in bpy.context.scene.objects if o.type=='MESH']
lo,hi=helper['bounds'](objects);matrix=Matrix.Scale(2.44/(hi.z-lo.z),4)@Matrix.Translation(-Vector(((lo.x+hi.x)*.5,(lo.y+hi.y)*.5,lo.z)))
for obj in objects:obj.matrix_world=matrix@obj.matrix_world
scene=bpy.context.scene;scene.render.engine='BLENDER_EEVEE';scene.eevee.use_gtao=True;scene.eevee.gtao_distance=.1
scene.render.resolution_x=1200;scene.render.resolution_y=2400;scene.render.resolution_percentage=100
scene.view_settings.view_transform='Standard';scene.view_settings.look='Medium High Contrast'
scene.world=bpy.data.worlds.new('Neutral door inspection');scene.world.use_nodes=True
scene.world.node_tree.nodes['Background'].inputs[0].default_value=(.25,.25,.25,1);scene.world.node_tree.nodes['Background'].inputs[1].default_value=.5
data=bpy.data.cameras.new('Orthographic normalized door');data.type='ORTHO';data.ortho_scale=1.4
camera=bpy.data.objects.new(data.name,data);scene.collection.objects.link(camera);scene.camera=camera
for index,y in enumerate((4,-4)):
    lamp=bpy.data.lights.new('Soft door inspection'+str(index),'AREA');lamp.energy=120;lamp.size=4
    light=bpy.data.objects.new(lamp.name,lamp);scene.collection.objects.link(light);light.location=(0,y,2.3)
    light.rotation_euler=(Vector((0,0,1.22))-light.location).to_track_quat('-Z','Y').to_euler()
output=project/'Builds/ModelingDownloads/SchoolRenewal/classroom-door'
for label,y in (('front',4),('back',-4)):
    camera.location=(0,y,1.22);camera.rotation_euler=(Vector((0,0,1.22))-camera.location).to_track_quat('-Z','Y').to_euler()
    scene.render.filepath=str(output/('door-source-'+label+'.png'));bpy.ops.render.render(write_still=True)
