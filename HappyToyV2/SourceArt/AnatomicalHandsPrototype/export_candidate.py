import bpy
from pathlib import Path
from mathutils import Matrix
root=Path(__file__).resolve().parent
bpy.ops.wm.open_mainfile(filepath=str(root/'hand-rig-prototype-v6.blend'))
h=bpy.data.objects['Blender anatomical hand candidate'];rig=next(o for o in bpy.context.scene.objects if o.type=='ARMATURE')
rig.animation_data.action=None
for bone in rig.pose.bones:bone.matrix_basis=Matrix.Identity(4)
rig.location=(0,0,0);rig.rotation_euler=(0,0,0)
bpy.context.view_layer.update()
bpy.ops.object.select_all(action='DESELECT');h.select_set(True);rig.select_set(True);bpy.context.view_layer.objects.active=rig
bpy.ops.export_scene.fbx(filepath=str(root/'candidate-geometry.fbx'),use_selection=True,add_leaf_bones=False,bake_anim=False,axis_forward='-Z',axis_up='Y')
print('EXPORTED_CANDIDATE')
