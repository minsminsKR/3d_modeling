"""Render/validate an already baked .blend without rebuilding weights/export."""
import argparse
import json
import sys
from pathlib import Path
import bpy
import numpy as np

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
from author_blender import render_previews, validate_rig, quantize_weights

parser = argparse.ArgumentParser()
parser.add_argument('--preview-dir', type=Path, required=True)
arguments = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
args = parser.parse_args(arguments)
bpy.ops.wm.open_mainfile(filepath=str(HERE / 'Baeksugwi_Run.blend'))
motion = json.loads((HERE / 'run-motion-report.json').read_text(encoding='utf-8'))
plan = json.loads((HERE / 'skeleton-plan.json').read_text(encoding='utf-8'))
rig = bpy.data.objects['Baeksugwi_Rig']
meshes = [o for o in bpy.data.objects if o.type == 'MESH']
with np.load(HERE / 'surface-weights.npz', allow_pickle=False) as values:
    positions = values['positions'].copy()
    joints = values['joints'].copy()
    quantized, _ = quantize_weights(values['weights'])
    names = list(values['bone_names'].astype(str))
validation = validate_rig(rig, meshes, motion, plan, positions, joints, quantized, names)
args.preview_dir.mkdir(parents=True, exist_ok=True)
(args.preview_dir / 'draft-validation.json').write_text(json.dumps(validation, indent=2), encoding='utf-8')
render_previews(rig, meshes, motion, plan['floor'], args.preview_dir)
print('SAVED_RENDER_COMPLETE', flush=True)
