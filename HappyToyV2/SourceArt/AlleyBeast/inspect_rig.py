"""Import the original AI-rigged GLB and save an editable Blender source."""
import bpy
from pathlib import Path

root = Path(__file__).resolve().parent
rig = next((root / 'rigged').glob('outputs/rigging/*/rigged.glb'))
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=str(rig))
meshes = [o for o in bpy.context.scene.objects if o.type == 'MESH']
arms = [o for o in bpy.context.scene.objects if o.type == 'ARMATURE']
print('RIG', len(arms), 'armatures', len(meshes), 'meshes')
for arm in arms:
    for bone in arm.data.bones:
        print('BONE', bone.name, tuple(round(x, 4) for x in bone.head_local),
              tuple(round(x, 4) for x in bone.tail_local), 'parent', bone.parent.name if bone.parent else '-')
for obj in meshes:
    print('SKIN', obj.name, 'verts', len(obj.data.vertices), 'groups', len(obj.vertex_groups),
          'unweighted', sum(not v.groups for v in obj.data.vertices))
bpy.ops.file.pack_all()
bpy.ops.wm.save_as_mainfile(filepath=str(root / 'AlleyDogRiggedEditable.blend'))
