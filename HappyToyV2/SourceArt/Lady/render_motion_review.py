"""Optional actual-model action renders, not synthetic animation mockups."""
from pathlib import Path
import sys
import bpy
from mathutils import Vector

HERE=Path(__file__).resolve().parent
sys.path.insert(0,str(HERE))
import author_lady as author

bpy.ops.wm.open_mainfile(filepath=str(HERE/'Lady_Editable.blend'))
rig=bpy.data.objects['Lady_Rig'];fit=bpy.data.objects['Lady_Fit']
output=HERE.parents[1]/'Builds/LadyReview-20261018'
author.render_preview(fit,rig,output)
scene=bpy.context.scene;scene.render.resolution_x=480;scene.render.resolution_y=720
camera=scene.camera;camera.location=(.9,-4,1.15)
camera.rotation_euler=(Vector((0,0,1.06))-camera.location).to_track_quat('-Z','Y').to_euler()
for i in range(64):
    scene.frame_set(13+i*3)
    scene.render.filepath=str(output/'motion'/('%03d.png'%i))
    bpy.ops.render.render(write_still=True)
