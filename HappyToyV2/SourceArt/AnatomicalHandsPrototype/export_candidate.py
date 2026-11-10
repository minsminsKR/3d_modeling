import bpy
from pathlib import Path
from mathutils import Matrix
root=Path(__file__).resolve().parent
bpy.ops.wm.open_mainfile(filepath=str(root/'TrapUpperArm/hand-with-upperarm.blend'))
h=bpy.data.objects['Blender anatomical hand candidate'];rig=next(o for o in bpy.context.scene.objects if o.type=='ARMATURE')
# FBX maps the first/render-active UV to Unity UV0. The draft retained an old
# transfer layer for comparison, while its skin was baked against NewSkinUV.
# Export only the baked layout so Unity cannot sample the wrong atlas.
uv=h.data.uv_layers.get('NewSkinUV')
if uv is None: raise RuntimeError('Missing UV layout for the baked hand texture')
for layer in list(h.data.uv_layers):
    if layer != uv: h.data.uv_layers.remove(layer)
h.data.uv_layers.active=uv
uv.active_render=True
rig.animation_data.action=None
for bone in rig.pose.bones:bone.matrix_basis=Matrix.Identity(4)
rig.location=(0,0,0);rig.rotation_euler=(0,0,0)
bpy.context.view_layer.update()
bpy.ops.object.select_all(action='DESELECT');h.select_set(True);rig.select_set(True);bpy.context.view_layer.objects.active=rig
bpy.ops.export_scene.fbx(filepath=str(root/'candidate-geometry.fbx'),use_selection=True,add_leaf_bones=False,bake_anim=False,axis_forward='-Z',axis_up='Y')
print('EXPORTED_CANDIDATE')
