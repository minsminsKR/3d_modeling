"""Render the actual derived webapp meshes in a neutral asset preview (no game launch).
Blender 4.0.2 --background --factory-startup --python render_preview.py
"""
from pathlib import Path
import bpy,math
from mathutils import Vector,Quaternion
SOURCE=Path(__file__).resolve().parent
PROJECT=SOURCE.parents[1]
bpy.ops.wm.read_factory_settings(use_empty=True)
for key,x in [('package',-.5),('pellet',.65)]:
    with bpy.data.libraries.load(str(SOURCE/(key+'.blend')),link=False) as (available,loaded):
        loaded.objects=[name for name in available.objects if name.startswith('SN_'+key)]
    for obj in loaded.objects:
        if obj is None:continue
        bpy.context.scene.collection.objects.link(obj)
        if key=="package":
            obj.rotation_mode='QUATERNION';obj.rotation_quaternion=Quaternion((0,0,1),math.pi)@obj.rotation_quaternion
        bpy.context.view_layer.update()
        points=[obj.matrix_world@Vector(c) for c in obj.bound_box]
        low=Vector(tuple(min(v[i] for v in points) for i in range(3)));high=Vector(tuple(max(v[i] for v in points) for i in range(3)))
        factor=(1.12 if key=='package' else .28)/max(.00001,high.z-low.z)
        obj.location=(obj.location-Vector(((low.x+high.x)/2,(low.y+high.y)/2,low.z)))*factor+Vector((x,0,0));obj.scale*=factor
        obj.hide_render=False;obj.hide_set(False)
mat=bpy.data.materials.new('Neutral preview ground');mat.diffuse_color=(.15,.17,.15,1)
bpy.ops.mesh.primitive_plane_add(size=200,location=(0,0,-.003));bpy.context.object.data.materials.append(mat)
world=bpy.data.worlds.new('Neutral preview world');bpy.context.scene.world=world;world.use_nodes=True;world.node_tree.nodes['Background'].inputs[0].default_value=(.38,.4,.38,1)
world.node_tree.nodes['Background'].inputs[1].default_value=.6
for loc,energy,size in [((1,-3,4),450,4),((-3,-1,2),200,3),((1,3,3),300,3)]:
    data=bpy.data.lights.new('Soft studio light','AREA');data.energy=energy;data.shape='DISK';data.size=size
    obj=bpy.data.objects.new(data.name,data);bpy.context.scene.collection.objects.link(obj);obj.location=loc
    obj.rotation_euler=(Vector((0,0,.5))-obj.location).to_track_quat('-Z','Y').to_euler()
data=bpy.data.cameras.new('Asset preview camera');camera=bpy.data.objects.new(data.name,data);bpy.context.scene.collection.objects.link(camera)
camera.location=(2.05,-3.35,1.85);camera.rotation_euler=(Vector((0,0,.53))-camera.location).to_track_quat('-Z','Y').to_euler();data.type='ORTHO';data.ortho_scale=2.15
scene=bpy.context.scene;scene.camera=camera;scene.render.engine='BLENDER_EEVEE';scene.eevee.use_gtao=True;scene.eevee.gtao_distance=3;scene.eevee.taa_render_samples=64
scene.render.resolution_x=1280;scene.render.resolution_y=960;scene.render.resolution_percentage=100
scene.view_settings.view_transform='Standard';scene.view_settings.look='None';scene.view_settings.exposure=0;scene.view_settings.gamma=1
out=PROJECT/'Builds/Snappers-20261015-preview';out.mkdir(parents=True,exist_ok=True)
scene.render.image_settings.file_format='PNG';scene.render.filepath=str(out/'generated-models.png');bpy.ops.render.render(write_still=True)
