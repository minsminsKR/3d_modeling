"""Compare actual exported GLB joint matrices with the saved Blender sprint.

Blender 4.0.2 --background --python verify_export_pose.py
Does not re-import or retarget the asset. Evaluates the exported glTF node
hierarchy and baked TRS channels at the exact same sampled times.
"""
import json
import math
import sys
from pathlib import Path
import bpy
import numpy as np
from mathutils import Matrix, Quaternion, Vector

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
from glb_io import read, accessor


def matrix_from_trs(node):
    if 'matrix' in node:
        return Matrix(np.asarray(node['matrix']).reshape((4, 4)).T.tolist())
    translation = Vector(node.get('translation', (0, 0, 0)))
    x, y, z, w = node.get('rotation', (0, 0, 0, 1))
    rotation = Quaternion((w, x, y, z)).normalized()
    scale = node.get('scale', (1, 1, 1))
    return Matrix.Translation(translation) @ rotation.to_matrix().to_4x4() @ Matrix.Diagonal((*scale, 1))


bpy.ops.wm.open_mainfile(filepath=str(HERE / 'Baeksugwi_Run.blend'))
rig = bpy.data.objects['Baeksugwi_Rig']
motion = json.loads((HERE / 'run-motion-report.json').read_text(encoding='utf-8'))
document, binary = read(HERE / 'Baeksugwi_Run.glb')
nodes = document['nodes']
names = {node.get('name'): i for i, node in enumerate(nodes)}
parents = {child: i for i, node in enumerate(nodes) for child in node.get('children', [])}
conversion = Matrix(((1, 0, 0, 0), (0, 0, -1, 0), (0, 1, 0, 0), (0, 0, 0, 1)))
report = {'status': 'PASS', 'clips': {}, 'comparison': 'actual exported GLB matrices vs saved Blender pose'}
for animation in document['animations']:
    clip_name = animation['name']
    rig.animation_data.action = bpy.data.actions[motion['actions'][clip_name]['action']]
    keys = []
    for channel in animation['channels']:
        sampler = animation['samplers'][channel['sampler']]
        times = accessor(document, binary, sampler['input']).reshape(-1)
        values = accessor(document, binary, sampler['output'])
        keys.append((channel['target']['node'], channel['target']['path'], times, values))
    maximum_translation, maximum_rotation = 0.0, 0.0
    wrist_translation, wrist_rotation = 0.0, 0.0
    samples = []
    for frame in (1, 12, 23, 44):
        time = (frame - 1) / 60
        local = [dict(node) for node in nodes]
        for node_index, path, times, values in keys:
            row = int(np.argmin(np.abs(times - time)))
            if abs(float(times[row]) - time) > 1e-6:
                raise AssertionError('Expected exact baked 60 FPS exported sample')
            if path in ('translation', 'rotation', 'scale'):
                local[node_index].pop('matrix', None)
                local[node_index][path] = values[row].tolist()
        world = {}
        def world_matrix(index):
            if index not in world:
                world[index] = (world_matrix(parents[index]) if index in parents else Matrix.Identity(4)) @ matrix_from_trs(local[index])
            return world[index]
        bpy.context.scene.frame_set(frame)
        sample_translation, sample_rotation = 0.0, 0.0
        for bone in rig.pose.bones:
            # Blender's exporter retains each bone's own local axes and applies
            # the Y-up change to the armature-space/world side only. The
            # inverse-bind matrices compensate this local bone convention.
            actual = conversion @ world_matrix(names[bone.name])
            expected = rig.matrix_world @ bone.matrix
            translation_error = (actual.translation - expected.translation).length
            rotation_error = max(abs(actual[r][c] - expected[r][c]) for r in range(3) for c in range(3))
            maximum_translation = max(maximum_translation, translation_error)
            maximum_rotation = max(maximum_rotation, rotation_error)
            sample_translation = max(sample_translation, translation_error)
            sample_rotation = max(sample_rotation, rotation_error)
            if bone.name.endswith('_Wrist'):
                wrist_translation = max(wrist_translation, translation_error)
                wrist_rotation = max(wrist_rotation, rotation_error)
        samples.append({'frame': frame, 'max_translation_error': sample_translation,
                        'max_rotation_matrix_error': sample_rotation})
    if maximum_translation > 2e-5 or maximum_rotation > 2e-4:
        raise AssertionError(f'Exported joint matrices mismatch {clip_name}: {maximum_translation}/{maximum_rotation}')
    report['clips'][clip_name] = {'max_translation_error': maximum_translation,
                                'max_rotation_matrix_error': maximum_rotation,
                                'wrist_translation_error': wrist_translation,
                                'wrist_rotation_matrix_error': wrist_rotation,
                                'samples': samples}
(HERE / 'export-pose-verification.json').write_text(json.dumps(report, indent=2), encoding='utf-8')
print(json.dumps(report, indent=2), flush=True)
