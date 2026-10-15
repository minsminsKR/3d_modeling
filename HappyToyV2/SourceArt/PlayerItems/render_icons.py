"""Blender 4.0.2: transparent inventory icons rendered from the editable models."""
import bpy,pathlib
from mathutils import Vector
PROJECT=pathlib.Path(__file__).resolve().parents[2]
OUT=PROJECT/'Assets/Resources/Hud/Items'
OUT.mkdir(parents=True,exist_ok=True)
sources={'snapper':'SourceArt/Snappers/package.blend','trap':'SourceArt/Traps/Trap_Editable.blend',
         'incense':'SourceArt/PlayerItems/incense.blend','talisman':'SourceArt/PlayerItems/talisman.blend'}
for key,path in sources.items():
    bpy.ops.wm.open_mainfile(filepath=str(PROJECT/path));scene=bpy.context.scene
    if key=='snapper':
        for obj in scene.objects:
            if obj.type=='MESH' and not obj.name.startswith('SN_package'):obj.hide_render=True;obj.hide_set(True)
        from mathutils import Quaternion
        import math
        obj=bpy.data.objects['SN_package'];obj.rotation_mode='QUATERNION';obj.rotation_quaternion=Quaternion((0,0,1),math.pi)@obj.rotation_quaternion
        bpy.context.view_layer.update()
    points=[]
    for o in list(scene.objects):
        if o.type in ('CAMERA','LIGHT'):bpy.data.objects.remove(o,do_unlink=True);continue
        if o.type=='MESH' and not o.hide_render and not o.hide_get():points.extend(o.matrix_world@Vector(p) for p in o.bound_box)
    lo=Vector(tuple(min(p[i] for p in points) for i in range(3)));hi=Vector(tuple(max(p[i] for p in points) for i in range(3)))
    centre=(lo+hi)/2;span=max(hi-lo)
    if not scene.world:scene.world=bpy.data.worlds.new('Icon ambient')
    scene.world.color=(.15,.15,.15);scene.render.engine='BLENDER_EEVEE';scene.eevee.taa_render_samples=64
    scene.render.resolution_x=scene.render.resolution_y=512;scene.render.resolution_percentage=100
    scene.render.film_transparent=True;scene.render.image_settings.file_format='PNG';scene.render.image_settings.color_mode='RGBA'
    scene.view_settings.view_transform='Standard'
    direction=Vector((.55,-1,.6) if key in ('snapper','trap') else (.04,-1,.05)).normalized()
    data=bpy.data.cameras.new('Inventory icon');camera=bpy.data.objects.new('Inventory icon',data);scene.collection.objects.link(camera)
    camera.location=centre+direction*span*4;camera.rotation_euler=(centre-camera.location).to_track_quat('-Z','Y').to_euler()
    data.type='ORTHO';data.ortho_scale=span*1.25;scene.camera=camera
    for name,position,power,size in [('Key',(-1,-1,1.8),90,1.8),('Fill',(1,-.2,.4),35,1.7),('Rim',(1,.7,1),100,1.4)]:
        data=bpy.data.lights.new(name,'AREA');data.energy=power*span*span;data.size=size*span
        light=bpy.data.objects.new(name,data);scene.collection.objects.link(light);light.location=centre+Vector(position)*span
        light.rotation_euler=(centre-light.location).to_track_quat('-Z','Y').to_euler()
    scene.render.filepath=str(OUT/f'{key}.png');bpy.ops.render.render(write_still=True)
    print('ICON',key,'bounds',lo[:],hi[:])
