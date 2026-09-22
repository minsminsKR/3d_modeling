"""Rest-shape diagnostic renders, not edits to production assets."""
import bpy
import json, sys
from pathlib import Path
from mathutils import Vector

root=Path(__file__).resolve().parent.parent
variant=sys.argv[sys.argv.index('--')+1] if '--' in sys.argv else 'Candidate'
if variant not in ['Candidate','CandidateShape','CandidateShapeSkin']:raise ValueError('Unexpected model variant')
out=root/('Verification/cyclopse-geometry' if variant=='Candidate' else 'Verification/cyclopse-geometry-shape' if variant=='CandidateShape' else 'Verification/cyclopse-geometry-shape-skin')
out.mkdir(parents=True,exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=str(root/'Assets/Art'/variant/'Cyclopse/Walking.fbx'))
obj=next(o for o in bpy.context.scene.objects if o.type=='MESH')
rig=next(o for o in bpy.context.scene.objects if o.type=='ARMATURE')
rig.data.pose_position='REST'
bpy.context.view_layer.update()
points=[obj.matrix_world@v.co for v in obj.data.vertices]
lo=Vector([min(p[i] for p in points) for i in range(3)])
hi=Vector([max(p[i] for p in points) for i in range(3)])
center=(lo+hi)/2
size=max(hi-lo)
scene=bpy.context.scene
scene.render.engine='BLENDER_WORKBENCH'
scene.display.shading.light='STUDIO'
scene.display.shading.color_type='SINGLE'
scene.display.shading.single_color=(.55,.55,.55)
scene.display.shading.show_shadows=True
scene.display.shading.show_cavity=True
scene.display.shading.cavity_type='BOTH'
scene.render.resolution_x=768
scene.render.resolution_y=768
scene.render.resolution_percentage=100
scene.render.image_settings.file_format='PNG'
bpy.ops.object.camera_add()
camera=bpy.context.object
camera.data.type='ORTHO'
camera.data.ortho_scale=size*1.15
camera.data.clip_start=size*.001
camera.data.clip_end=size*100
scene.camera=camera
for name,direction in [('front',(0,-1,.12)),('rear',(0,1,.12)),('side',(1,0,.12))]:
    camera.location=center+Vector(direction).normalized()*size*3
    camera.rotation_euler=(center-camera.location).to_track_quat('-Z','Y').to_euler()
    scene.render.filepath=str(out/(name+'.png'))
    bpy.ops.render.render(write_still=True)
# Fixed coordinates from the baseline bounds: identical crop and scale across variants.
target=Vector((0,0,.0025))
camera.data.ortho_scale=.014
for name,direction in [('rear-close',(0,1,.10)),('shoulder-close',(.6,1,.25))]:
    camera.location=target+Vector(direction).normalized()*.06
    camera.rotation_euler=(target-camera.location).to_track_quat('-Z','Y').to_euler()
    scene.render.filepath=str(out/(name+'.png'))
    bpy.ops.render.render(write_still=True)
(out/'coordinates.json').write_text(json.dumps(dict(minimum=list(lo),maximum=list(hi),
    local_min=[min(v.co[i] for v in obj.data.vertices) for i in range(3)],
    local_max=[max(v.co[i] for v in obj.data.vertices) for i in range(3)]),indent=2))
