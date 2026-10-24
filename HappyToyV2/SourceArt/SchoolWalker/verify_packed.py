"""Reopen the packed source and verify exact preserved AI data and all PBR."""
from pathlib import Path
import importlib.util
import json

import bpy

HERE = Path(__file__).resolve().parent
PROJECT = HERE.parents[1]

spec = importlib.util.spec_from_file_location('school_walker_preservation', HERE / 'finish_character.py')
finisher = importlib.util.module_from_spec(spec)
spec.loader.exec_module(finisher)
record = json.loads((HERE / 'export.json').read_text(encoding='utf-8'))
path = HERE / record['editable']
bpy.ops.wm.open_mainfile(filepath=str(path))
preserved = bpy.data.collections['Unchanged designated app AI rig, source mesh and full PBR']
rig = next(obj for obj in preserved.objects if obj.type == 'ARMATURE')
mesh = next(obj for obj in preserved.objects if obj.type == 'MESH' and len(obj.material_slots))
if finisher.source_snapshot(mesh, rig) != record['preservedAISourceSnapshot']:
    raise RuntimeError('Reopened packed source changed original AI geometry/UV/skin/bone rest')
required_images = {}
for material in [mesh.data.materials[0]] + [bpy.data.objects['SW_LOD' + str(level)].data.materials[0] for level in range(3)]:
    for node in material.node_tree.nodes:
        if node.type == 'TEX_IMAGE' and node.image:
            required_images[node.image.name] = node.image
if any(not image.packed_file for image in required_images.values()):
    raise RuntimeError('A required original/game image is not packed into the editable source')
if not any(max(image.size) == 8192 for image in required_images.values()):
    raise RuntimeError('Original full-resolution generated PBR is no longer in the packed source')
for name in ('walk', 'idle'):
    if name not in bpy.data.actions or not bpy.data.actions[name].fcurves:
        raise RuntimeError('Packed source lost an authored ordinary action: ' + name)
report = dict(status='PASS', editableSha256=finisher.digest(path),
              preservedAISnapshotMatchesExactly=True, originalBones=len(rig.data.bones),
              images=[dict(name=image.name, size=list(image.size), packed=True) for image in required_images.values()],
              actions=[dict(name=name, curves=len(bpy.data.actions[name].fcurves)) for name in ('walk', 'idle')],
              sourceUnchanged=True, scope='Reopened original mesh/UV/skin/bone matrices, full-resolution original/game PBR and clips; not Unity')
output = PROJECT / 'Builds/SchoolWalkerReview/packed-source-verification.json'
output.parent.mkdir(parents=True, exist_ok=True)
output.write_text(json.dumps(report, indent=2) + '\n', encoding='utf-8')
print('SCHOOL_WALKER_PACKED_SOURCE_PASS ' + json.dumps(report), flush=True)
