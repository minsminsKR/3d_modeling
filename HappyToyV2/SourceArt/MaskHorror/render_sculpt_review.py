"""Optional silent studio renders of the actual baked runtime meshes, not Unity evidence."""
from pathlib import Path
import argparse
import math
import sys
import bpy
from mathutils import Vector

HERE=Path(__file__).resolve().parent
parser=argparse.ArgumentParser();parser.add_argument('--output',type=Path,required=True)
args=parser.parse_args(sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else [])
args.output.mkdir(parents=True,exist_ok=True)

def runtime_meshes():return [o for o in bpy.context.scene.objects if o.type=='MESH' and not o.hide_render]
def vertices(objects):return [o.matrix_world@v.co for o in objects for v in o.data.vertices]
def bounds(objects):
    p=vertices(objects)
    return Vector(tuple(min(v[i] for v in p) for i in range(3))),Vector(tuple(max(v[i] for v in p) for i in range(3)))

def render(file,target,extent,combined=False):
    scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.samples=40;scene.cycles.use_denoising=True
    scene.render.resolution_x=1200;scene.render.resolution_y=1100;scene.render.resolution_percentage=100
    scene.view_settings.view_transform='AgX';scene.world.color=(.012,.014,.011)
    bpy.ops.object.camera_add(location=target+Vector((extent*.24,extent*2.0,extent*.10)))
    camera=bpy.context.object;camera.rotation_euler=(target-camera.location).to_track_quat('-Z','Y').to_euler()
    camera.data.type='ORTHO';camera.data.ortho_scale=extent*1.27;scene.camera=camera
    for offset,energy,color in [((-1,.8,1.4),280,(.86,.84,.79)),((.9,.7,.1),55,(.64,.66,.61)),((0,-1,.8),190,(.49,.57,.51))]:
        bpy.ops.object.light_add(type='AREA',location=target+Vector(offset)*extent)
        light=bpy.context.object;light.data.energy=energy*extent*extent;light.data.color=color;light.data.size=extent*.65
        light.rotation_euler=(target-light.location).to_track_quat('-Z','Y').to_euler()
    scene.render.filepath=str(args.output/file);bpy.ops.render.render(write_still=True)

for key in ('wraith-body','wraith-mask'):
    bpy.ops.wm.open_mainfile(filepath=str(HERE/(key+'.blend')))
    lo,hi=bounds(runtime_meshes());center=(lo+hi)/2;extent=max(hi-lo)
    render(key+'-sculpt-baked-studio.png',center,extent)

bpy.ops.wm.open_mainfile(filepath=str(HERE/'wraith-body.blend'))
body=[o for o in bpy.context.scene.objects if o.type=='EMPTY' and o.parent is None][0]
lo,hi=bounds(runtime_meshes());body.scale=Vector((2.44/(hi.x-lo.x),1.61/(hi.z-lo.z),1.61/(hi.z-lo.z)))
body.location.z=-lo.z*body.scale.z;bpy.context.view_layer.update()
with bpy.data.libraries.load(str(HERE/'wraith-mask.blend'),link=False) as (source,target):
    target.objects=[name for name in source.objects if not name.startswith(('SCULPT HIGH |','LOD1 |'))]
for obj in target.objects:
    if obj:bpy.context.scene.collection.objects.link(obj)
mask=next(o for o in target.objects if o and o.type=='EMPTY' and o.parent is None)
mask_meshes=[o for o in target.objects if o and o.type=='MESH'];lo,hi=bounds(mask_meshes)
scale=.72/(hi.z-lo.z);mask.scale=Vector((scale,scale,scale))
mask.location=Vector((-(lo.x+hi.x)*.5*scale,-(lo.y+hi.y)*.5*scale,1.61-.035-lo.z*scale))
bpy.context.view_layer.update();lo,hi=bounds(runtime_meshes())
render('wraith-combined-sculpt-baked-studio.png',(lo+hi)/2,max(hi-lo),True)
print('MASK_SCULPT_REVIEW_READY',args.output,flush=True)
