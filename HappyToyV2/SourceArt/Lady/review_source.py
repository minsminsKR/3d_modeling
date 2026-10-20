"""Silent offline comparison of the retained full-resolution AI skin."""
from pathlib import Path
import sys
import bpy
HERE=Path(__file__).resolve().parent
sys.path.insert(0,str(HERE))
from author_lady import render_preview,PROJECT
bpy.ops.wm.open_mainfile(filepath=str(HERE/'Lady_Editable.blend'))
for obj in bpy.context.scene.objects:
    if obj.type=='MESH':obj.hide_render=True
source=bpy.data.objects['Lady_SourceHigh'];source.hide_set(False);source.hide_render=False
render_preview(bpy.data.objects['Lady_Fit'],bpy.data.objects['Lady_Rig'],PROJECT/'Builds/LadyReview-20261018/source')
