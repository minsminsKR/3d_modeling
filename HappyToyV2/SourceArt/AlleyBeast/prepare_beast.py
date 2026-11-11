"""Prepare the untouched generated GLB as an editable static Unity candidate.

Run Blender 4.2.23 in background with --python-exit-code 1 --python this_file.
Rigging, animal motion, visual quality and runtime performance require review.
"""
import bpy
from pathlib import Path
from mathutils import Vector

root = Path(__file__).resolve().parent
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=str(root / 'generation/model.glb'))
objects = [o for o in bpy.context.scene.objects if o.type == 'MESH']
if not objects:
    raise RuntimeError('Generated source contains no mesh')
bpy.context.view_layer.update()
points = [o.matrix_world @ Vector(corner) for o in objects for corner in o.bound_box]
low = Vector(tuple(min(v[i] for v in points) for i in range(3)))
high = Vector(tuple(max(v[i] for v in points) for i in range(3)))
height = high.z - low.z
if height < .001:
    raise RuntimeError('Generated animal has no height')
root_obj = bpy.data.objects.new('Alley dog', None)
bpy.context.scene.collection.objects.link(root_obj)
for obj in [o for o in bpy.context.scene.objects if o != root_obj and o.parent is None]:
    obj.parent = root_obj
root_obj.scale = (1.0 / height,) * 3
root_obj.location = (-((low.x + high.x) / 2) / height, -((low.y + high.y) / 2) / height, -low.z / height)
bpy.context.view_layer.update()
print('DOG_SOURCE', len(objects), 'meshes', sum(len(o.data.polygons) for o in objects), 'faces', 'height', height)
bpy.ops.file.pack_all()
bpy.ops.wm.save_as_mainfile(filepath=str(root / 'AlleyDogEditable.blend'))
# Keep the editable source intact. Derive a lighter static runtime candidate.
source_faces = sum(len(o.data.polygons) for o in objects)
ratio = min(1.0, 120000 / max(1, source_faces))
for obj in objects:
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    if ratio < 1:
        decimate = obj.modifiers.new('Runtime face budget', 'DECIMATE')
        decimate.ratio = ratio
        bpy.ops.object.modifier_apply(modifier=decimate.name)
    obj.select_set(False)
print('DOG_RUNTIME', sum(len(o.data.polygons) for o in objects), 'faces')
bpy.ops.object.select_all(action='SELECT')
bpy.ops.export_scene.fbx(filepath=str(root / 'AlleyDog.fbx'), use_selection=True,
                         add_leaf_bones=False, bake_anim=False, axis_forward='-Z', axis_up='Y',
                         path_mode='COPY', embed_textures=True)
