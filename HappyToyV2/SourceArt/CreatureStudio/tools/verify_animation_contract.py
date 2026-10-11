"""Quiet background Blender test of animation contracts on a disposable rig."""
import json
import math
import sys
from pathlib import Path
import bpy
from mathutils import Vector

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
from creature_animation import apply_creature_animation


def synthetic_rig():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    data = bpy.data.armatures.new('SyntheticContractRig')
    rig = bpy.data.objects.new('SyntheticContractRig', data)
    bpy.context.collection.objects.link(rig)
    bpy.context.view_layer.objects.active = rig
    rig.select_set(True)
    bpy.ops.object.mode_set(mode='EDIT')
    definitions = []
    trunk = [('Hips', (0, 0, 1.0)), ('Spine', (0, 0, 1.12)), ('Spine1', (0, 0, 1.30)),
             ('Spine2', (0, 0, 1.48)), ('Neck', (0, 0, 1.62)), ('Head', (0, 0, 1.75))]
    for i, (name, point) in enumerate(trunk):
        tail = trunk[i + 1][1] if i + 1 < len(trunk) else (0, 0, 2.0)
        definitions.append((name, trunk[i - 1][0] if i else None, point, tail))
    for side, sign in [('Left', 1), ('Right', -1)]:
        # Reversed X convention explicitly exercises anatomical-side lookup.
        points = [('Shoulder', (.17 * sign, 0, 1.48)), ('Arm', (.30 * sign, 0, 1.48)),
                  ('ForeArm', (.48 * sign, 0, 1.08)), ('Hand', (.59 * sign, 0, .69))]
        for i, (suffix, point) in enumerate(points):
            parent = 'Spine2' if i == 0 else side + points[i - 1][0]
            tail = points[i + 1][1] if i + 1 < len(points) else (.59 * sign, 0, .57)
            definitions.append((side + suffix, parent, point, tail))
        points = [('UpLeg', (.14 * sign, 0, 1.0)), ('Leg', (.15 * sign, 0, .58)),
                  ('Foot', (.15 * sign, 0, .10)), ('ToeBase', (.15 * sign, -.16, .03))]
        for i, (suffix, point) in enumerate(points):
            parent = 'Hips' if i == 0 else side + points[i - 1][0]
            tail = points[i + 1][1] if i + 1 < len(points) else (.15 * sign, -.24, .02)
            definitions.append((side + suffix, parent, point, tail))
    made = {}
    for name, parent, point, tail in definitions:
        bone = data.edit_bones.new('mixamorig:' + name)
        bone.head, bone.tail = point, tail
        if parent:
            bone.parent = made[parent]
        made[name] = bone
    bpy.ops.object.mode_set(mode='OBJECT')
    return rig


report = {}
for creature in ('Uncat', 'Cyclopse', 'Baby'):
    rig = synthetic_rig()
    details = apply_creature_animation(rig, creature,
                    {'left_hand': {'clearance': .055}, 'right_hand': {'clearance': .055},
                     'left_foot': {'clearance': .10}, 'right_foot': {'clearance': .10}})
    for name, action_details in details['actions'].items():
        assert action_details['translation_loop_error'] < 1e-5
        rig.animation_data.action = bpy.data.actions[action_details['action']]
        for frame in (1, 1 + (action_details['frames'] - 1) // 4,
                      1 + (action_details['frames'] - 1) // 2, action_details['frames']):
            bpy.context.scene.frame_set(frame)
            for pb in rig.pose.bones:
                assert all(math.isfinite(v) for row in pb.matrix for v in row)
                assert abs(pb.matrix.to_3x3().determinant() - 1) < 1e-4
        assert action_details['max_segment_length_error'] < 1e-5
    report[creature] = details
output = Path(sys.argv[sys.argv.index('--') + 1]) if '--' in sys.argv else HERE / 'synthetic-animation-report.json'
output.parent.mkdir(parents=True, exist_ok=True)
output.write_text(json.dumps({'status': 'PASS', 'models': report}, indent=2), encoding='utf-8')
print('SYNTHETIC_ANIMATION_CONTRACT_PASS', output, flush=True)
