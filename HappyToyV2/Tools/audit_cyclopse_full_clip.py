"""Read-only all-frame deformation audit of original and currently shipped FBXs."""
import bpy
import json
import math
import sys
from pathlib import Path

root = Path(__file__).resolve().parent.parent
report = {}
variants=sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else ['V1', 'Refined', 'Candidate', 'CandidateShape']
if not variants or any(v not in ['V1','Refined','Candidate','CandidateShape','CandidateShapeSkin'] for v in variants):
    raise ValueError('Unexpected audit variant')
for variant in variants:
    for clip in ['Walking', 'Run']:
        bpy.ops.wm.read_factory_settings(use_empty=True)
        bpy.ops.import_scene.fbx(filepath=str(root / 'Assets/Art' / variant / 'Cyclopse' / (clip + '.fbx')))
        obj = next(o for o in bpy.context.scene.objects if o.type == 'MESH')
        rig = next(o for o in bpy.context.scene.objects if o.type == 'ARMATURE')
        mesh = obj.data
        lengths = [(mesh.vertices[e.vertices[0]].co - mesh.vertices[e.vertices[1]].co).length for e in mesh.edges]
        frames = []
        start, end = rig.animation_data.action.frame_range
        for frame in range(math.floor(start), math.ceil(end) + 1):
            bpy.context.scene.frame_set(frame)
            evaluated = obj.evaluated_get(bpy.context.evaluated_depsgraph_get())
            posed = evaluated.to_mesh()
            ratios = []
            for edge in posed.edges:
                a, b = edge.vertices
                if lengths[edge.index] > 1e-6:
                    ratios.append(((posed.vertices[a].co - posed.vertices[b].co).length / lengths[edge.index], edge.index))
            ratios.sort(reverse=True)
            frames.append(dict(frame=frame, maximum=ratios[0][0], worst_edge=ratios[0][1],
                over3=sum(r > 3 for r, _ in ratios), over6=sum(r > 6 for r, _ in ratios)))
            evaluated.to_mesh_clear()
        worst = max(frames, key=lambda row: row['maximum'])
        report[variant + '/' + clip] = dict(vertices=len(mesh.vertices), faces=len(mesh.polygons),
            frames=frames, worst=worst, sharp_edges=sum(e.use_edge_sharp for e in mesh.edges),
            flat_faces=sum(not p.use_smooth for p in mesh.polygons))
        print(variant, clip, 'worst', worst, flush=True)
out = root / 'Verification/cyclopse-full-clip'
out.mkdir(parents=True, exist_ok=True)
filename='deformation.json' if '--' not in sys.argv else 'deformation-'+'-'.join(variants)+'.json'
(out / filename).write_text(json.dumps(report, indent=2), encoding='utf-8')
