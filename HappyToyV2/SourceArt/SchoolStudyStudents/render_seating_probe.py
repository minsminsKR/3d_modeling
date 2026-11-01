"""Render a background-only review of an authored seating probe in Builds."""
from pathlib import Path
import math
import sys
import bpy
from mathutils import Vector

path=Path(sys.argv[sys.argv.index('--')+1])
if not path.is_absolute() or 'Builds' not in path.parts:
    raise RuntimeError('Preview output must be an absolute path under Builds')
path.parent.mkdir(parents=True,exist_ok=True)
scene=bpy.context.scene
scene.render.engine='BLENDER_EEVEE'
scene.render.resolution_x=800;scene.render.resolution_y=800;scene.render.resolution_percentage=100
scene.view_settings.view_transform='Standard'
if scene.world is None:scene.world=bpy.data.worlds.new('Seat review world')
scene.world.color=(.12,.12,.12)
for name,position,energy in [('key',(2,-3,4),500),('fill',(-3,-1,2),300),('rim',(0,3,3),350)]:
    light=bpy.data.lights.new(name,'AREA');obj=bpy.data.objects.new(name,light);scene.collection.objects.link(obj)
    obj.location=position;obj.rotation_euler=(Vector((0,0,.85))-obj.location).to_track_quat('-Z','Y').to_euler()
    light.energy=energy;light.size=3
camera_data=bpy.data.cameras.new('Seating review');camera=bpy.data.objects.new('Seating review',camera_data)
scene.collection.objects.link(camera);scene.camera=camera;camera_data.type='ORTHO';camera_data.ortho_scale=2.4
camera.location=(2,-4,2);camera.rotation_euler=(Vector((0,0,.85))-camera.location).to_track_quat('-Z','Y').to_euler()
scene.render.filepath=str(path);bpy.ops.render.render(write_still=True)
print('STUDENT_SEAT_REVIEW',path,flush=True)
