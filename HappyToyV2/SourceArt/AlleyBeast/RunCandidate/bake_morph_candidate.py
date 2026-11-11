"""Bake the editable rig to a single-coordinate-system morph animation.

This tool writes authoring outputs only; the original rigged .blend stays editable.
"""
import bpy
import sys
from pathlib import Path

root = Path(__file__).resolve().parent
bpy.ops.wm.open_mainfile(filepath=str(root/'RetargetedCandidate.blend'))
scene = bpy.context.scene
dog = bpy.data.objects['geometry_0']
lod = '--lod50k' in sys.argv
if lod:
    bpy.context.view_layer.objects.active = dog
    modifier = dog.modifiers.new('50k face comparison', 'DECIMATE')
    modifier.ratio = min(1, 50000 / max(1, len(dog.data.polygons)))
    while dog.modifiers.find(modifier.name) > 0:
        bpy.ops.object.modifier_move_up(modifier=modifier.name)
    bpy.ops.object.modifier_apply(modifier=modifier.name)
    print('DOG_LOD_FACES', len(dog.data.polygons), flush=True)
depsgraph = bpy.context.evaluated_depsgraph_get()
source = dog.data.copy()
result = bpy.data.objects.new('DogMorphCandidate', source)
scene.collection.objects.link(result)
result.matrix_world = dog.matrix_world.copy()
result.shape_key_add(name='Basis')
samples = list(range(1, 24, 2))
if samples[-1] != 23: samples.append(23)
for frame in samples:
    scene.frame_set(frame); bpy.context.view_layer.update()
    evaluated = dog.evaluated_get(depsgraph); mesh = evaluated.to_mesh()
    if len(mesh.vertices) != len(source.vertices):
        raise RuntimeError('Evaluated vertex order/count differs')
    coords = [0.0] * (len(mesh.vertices) * 3)
    mesh.vertices.foreach_get('co', coords)
    key = result.shape_key_add(name='Pose%02d' % frame)
    key.data.foreach_set('co', coords)
    evaluated.to_mesh_clear()
    for at in samples:
        key.value = 1 if at == frame else 0
        key.keyframe_insert('value', frame=at)
    print('BAKED_POSE', frame, flush=True)
scene.frame_start = 1; scene.frame_end = 23; scene.render.fps = 24
scene.frame_set(1)
name = 'CandidateMorph50k' if lod else 'CandidateMorph'
bpy.ops.wm.save_as_mainfile(filepath=str(root/(name + '.blend')))
bpy.ops.object.select_all(action='DESELECT'); result.select_set(True)
bpy.context.view_layer.objects.active = result
bpy.ops.export_scene.fbx(filepath=str(root/(name + '.fbx')), use_selection=True,
                         object_types={'MESH'}, add_leaf_bones=False, bake_anim=True,
                         bake_anim_use_all_actions=False, bake_anim_use_nla_strips=False,
                         bake_anim_simplify_factor=0, axis_forward='-Z', axis_up='Y',
                         path_mode='COPY', embed_textures=True)
