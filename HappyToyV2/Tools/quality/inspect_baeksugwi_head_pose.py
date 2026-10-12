"""Blender 4.0.2: read-only source geometry envelope at every head-cock target."""
import json
from pathlib import Path
import bpy
import numpy as np

PROJECT = Path(__file__).resolve().parents[2]
SOURCE = PROJECT / 'SourceArt/BaeksugwiStudio'
bpy.ops.wm.open_mainfile(filepath=str(SOURCE / 'Baeksugwi_Run.blend'))
rig = bpy.data.objects['Baeksugwi_Rig']
rig.animation_data.action = bpy.data.actions['Run_InPlace']
bpy.context.scene.frame_set(1)
rig.animation_data.action = bpy.data.actions['Head_Writhe']
report = json.loads((SOURCE / 'head-motion-report.json').read_text(encoding='utf-8'))
times = sorted(set([0.0] + [beat[0] for beat in report['beatsSecondsYawRollPitch']]))
samples = []
for time in times:
    bpy.context.scene.frame_set(round(time * 60) + 1)
    low, high = float('inf'), float('-inf')
    for obj in bpy.context.scene.objects:
        if obj.type != 'MESH' or not any(m.type == 'ARMATURE' and m.object == rig for m in obj.modifiers):
            continue
        evaluated = obj.evaluated_get(bpy.context.evaluated_depsgraph_get())
        mesh = evaluated.to_mesh()
        try:
            coords = np.empty(len(mesh.vertices) * 3, dtype=np.float32)
            mesh.vertices.foreach_get('co', coords)
            matrix = np.asarray(evaluated.matrix_world)
            heights = coords.reshape((-1, 3)) @ matrix[2, :3] + matrix[2, 3]
            low, high = min(low, float(heights.min())), max(high, float(heights.max()))
        finally:
            evaluated.to_mesh_clear()
    samples.append({'seconds': time, 'minimumZ': low, 'maximumZ': high})
result = {'scope': 'Source skin envelope at authored targets; actual Unity floor/roof checked separately.',
          'neutralMinimumZ': samples[0]['minimumZ'], 'lowest': min(samples, key=lambda s: s['minimumZ']),
          'samples': samples}
output = PROJECT / 'Verification/baeksugwi-roll-20261012/source-envelope.json'
output.parent.mkdir(parents=True, exist_ok=True)
output.write_text(json.dumps(result, indent=2), encoding='utf-8')
print(json.dumps({key: result[key] for key in ('neutralMinimumZ', 'lowest')}), flush=True)
