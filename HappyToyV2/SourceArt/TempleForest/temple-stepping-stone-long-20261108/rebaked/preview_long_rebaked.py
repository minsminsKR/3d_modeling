from pathlib import Path
import bpy, math, sys
from mathutils import Matrix, Vector
out=Path(sys.argv[sys.argv.index('--')+1]).resolve()
key='temple-stepping-stone-long-20261108'
bpy.ops.wm.open_mainfile(filepath=str(out/(key+'-rebaked.blend')),load_ui=False)
orig_col=bpy.data.collections['Complete unchanged webapp original']; orig=next(o for o in orig_col.objects if o.type=='MESH')
corners=[orig.matrix_world@Vector(c) for c in orig.bound_box]; low=Vector(tuple(min(p[i] for p in corners) for i in range(3))); high=Vector(tuple(max(p[i] for p in corners) for i in range(3))); center=Vector(((low.x+high.x)*.5,(low.y+high.y)*.5,low.z)); norm=Matrix.Scale(1/(high.y-low.y),4)@Matrix.Translation(-center)
orig_col.hide_render=True;bpy.data.collections['Editable source-derived game LODs'].hide_render=True;bpy.data.collections['Rebaked review LODs'].hide_render=True
models=[(orig,norm@orig.matrix_world),*[(bpy.data.objects['REB_'+key+'_LOD'+str(i)],bpy.data.objects['REB_'+key+'_LOD'+str(i)].matrix_world.copy()) for i in range(3)]]
for i,(source,matrix) in enumerate(models):
 obj=source.copy();obj.data=source.data;bpy.context.scene.collection.objects.link(obj);obj.hide_render=False;obj.hide_viewport=False;obj.hide_set(False);obj.matrix_world=Matrix.Translation(Vector(((-.65 if i%2==0 else .65),(.65 if i<2 else -.65),0)))@matrix
bpy.ops.object.light_add(type='AREA',location=(0,-2.5,5));keylight=bpy.context.object;keylight.data.energy=600;keylight.data.shape='DISK';keylight.data.size=5
bpy.ops.object.camera_add(location=(0,-4.8,6));camera=bpy.context.object;camera.rotation_euler=(Vector((0,0,.05))-camera.location).to_track_quat('-Z','Y').to_euler();camera.data.type='ORTHO';camera.data.ortho_scale=2.8;scene=bpy.context.scene;scene.camera=camera
scene.render.engine='CYCLES';scene.cycles.device='CPU';scene.cycles.samples=8;scene.render.threads_mode='FIXED';scene.render.threads=2;scene.render.resolution_x=1024;scene.render.resolution_y=1024;scene.render.resolution_percentage=100;scene.render.image_settings.file_format='PNG';scene.render.film_transparent=False;scene.world=bpy.data.worlds.new('Rebake review world');scene.world.color=(.18,.18,.18)
for label,location in [('top',(0,-4.8,6)),('low',(0,-6,3))]:
 camera.location=location;camera.rotation_euler=(Vector((0,0,.05))-camera.location).to_track_quat('-Z','Y').to_euler();scene.render.filepath=str(out/('rebaked-comparison-'+label+'.png'));bpy.ops.render.render(write_still=True)
