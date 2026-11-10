"""Generate the ring MCP volume corrective from the editable v7 hand.

Run Blender 4.2.23 in background. The difference between dual-quaternion and
linear skinning at a 60-degree ring bend is transformed back to bind space,
so Unity can apply it as a normal blendshape on top of linear skinning.
"""
import math
from pathlib import Path

import bpy
from mathutils import Matrix

root = Path(__file__).resolve().parent
bpy.ops.wm.open_mainfile(filepath=str(root / 'hand-rig-prototype-v7.blend'))
hand = bpy.data.objects['Blender anatomical hand candidate']
rig = next(obj for obj in bpy.context.scene.objects if obj.type == 'ARMATURE')
if hand.matrix_world != rig.matrix_world:
    raise RuntimeError('Corrective calculation expects a shared hand/armature space')
rig.animation_data.action = None
for bone in rig.pose.bones:
    bone.matrix_basis.identity()
modifier = next(item for item in hand.modifiers if item.type == 'ARMATURE')
hand.shape_key_add(name='Basis')
bone = rig.pose.bones['ring_1']
bone.rotation_mode = 'XYZ'
bone.rotation_euler.x = math.radians(60)
bpy.context.view_layer.update()

def deformed(preserve_volume):
    modifier.use_deform_preserve_volume = preserve_volume
    hand.update_tag()
    bpy.context.view_layer.update()
    evaluated = hand.evaluated_get(bpy.context.evaluated_depsgraph_get())
    mesh = evaluated.to_mesh()
    try:
        return [vertex.co.copy() for vertex in mesh.vertices]
    finally:
        evaluated.to_mesh_clear()

linear = deformed(False)
volume = deformed(True)
names = [group.name for group in hand.vertex_groups]
matrices = {group.name: rig.pose.bones[group.name].matrix @ rig.data.bones[group.name].matrix_local.inverted()
            for group in hand.vertex_groups if group.name in rig.pose.bones}
shape = hand.shape_key_add(name='Correct_ring_1')
affected = 0
for vertex, linear_point, volume_point in zip(hand.data.vertices, linear, volume):
    difference = volume_point - linear_point
    if difference.length < .000001:
        continue
    blend = Matrix(((0, 0, 0), (0, 0, 0), (0, 0, 0)))
    for weight in vertex.groups:
        blend += matrices[names[weight.group]].to_3x3() * weight.weight
    try:
        offset = blend.inverted() @ difference
    except ValueError as exc:
        raise RuntimeError('Singular skinning matrix at vertex ' + str(vertex.index)) from exc
    if offset.length > .02:
        raise RuntimeError('Unreasonably large corrective at vertex ' + str(vertex.index))
    shape.data[vertex.index].co = vertex.co + offset
    affected += 1
if affected < 20:
    raise RuntimeError('Ring corrective did not reach the expected skin')
for pose_bone in rig.pose.bones:
    pose_bone.matrix_basis.identity()
modifier.use_deform_preserve_volume = False
bpy.context.view_layer.update()
bpy.ops.wm.save_as_mainfile(filepath=str(root / 'hand-rig-corrective.blend'))
print('RING_CORRECTIVE_VERTICES', affected)
