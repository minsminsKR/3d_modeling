"""Rebake updated procedural motion against an unchanged saved surface rig.

Run in Blender 4.0.2 background. Use --refresh-weights to apply a new NPZ to the
identical source mesh and bone plan. Geometry/topology or bone plan changes
still require author_blender.py. --preview-dir PATH adds pose previews.
"""
import argparse
import json
import sys
from pathlib import Path
import bpy
import numpy as np

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
from author_blender import (prepare_limb_specs, quantize_weights, validate_rig,
                            verify_export, dump, digest, source_mapping, bind_mesh,
                            mesh_snapshot, render_previews)
from run_motion import apply_run_animation

parser = argparse.ArgumentParser()
parser.add_argument('--refresh-weights', action='store_true')
parser.add_argument('--preview-dir', type=Path)
parser.add_argument('--focused-preview', action='store_true', help='Only three-quarter frame1 and side frame12')
arguments = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
args = parser.parse_args(arguments)
bpy.ops.wm.open_mainfile(filepath=str(HERE / 'Baeksugwi_Run.blend'))
plan = json.loads((HERE / 'skeleton-plan.json').read_text(encoding='utf-8'))
rig = bpy.data.objects['Baeksugwi_Rig']
meshes = [o for o in bpy.data.objects if o.type == 'MESH']
with np.load(HERE / 'surface-weights.npz', allow_pickle=False) as data:
    positions, joints = data['positions'].copy(), data['joints'].copy()
    quantized, weight_report = quantize_weights(data['weights'])
    names = list(data['bone_names'].astype(str))
for b in plan['bones']:
    from author_blender import cv
    existing = rig.data.bones[b['name']]
    if (existing.head_local - cv(b['head'])).length > 1e-6 or (existing.tail_local - cv(b['tail'])).length > 1e-6:
        raise ValueError('Saved bone plan changed; run full author_blender.py')
if args.refresh_weights:
    before = {obj.name: mesh_snapshot(obj) for obj in meshes}
    mapping = source_mapping(meshes, positions)
    for obj in meshes:
        source_ids, error, method = mapping[obj.name]
        binding = bind_mesh(obj, rig, source_ids, joints, quantized, names)
        after = mesh_snapshot(obj)
        if before[obj.name] != after:
            raise AssertionError('Rest geometry/UV/normals/material changed during weight refresh')
    print('SURFACE_WEIGHTS_REFRESHED', flush=True)
rig.animation_data.action = None
for action in list(bpy.data.actions):
    if action.name.startswith(('Run_InPlace', 'Run_Forward')):
        bpy.data.actions.remove(action)
specs, measurements = prepare_limb_specs(plan, positions, joints, quantized, names)
motion = apply_run_animation(rig, specs, fps=60, cycle_seconds=plan['cycle_seconds'],
                             ground_height=plan['floor'], body_height_offset=plan['body_height_offset'],
                             stride=.12, report_path=str(HERE / 'run-motion-report.json'))
validation = validate_rig(rig, meshes, motion, plan, positions, joints, quantized, names)
report_path = HERE / 'authoring-report.json'
report = json.loads(report_path.read_text(encoding='utf-8')) if report_path.exists() else {}
report.update({'motion': motion, 'validation': validation, 'palm_measurements': measurements,
               'weights': weight_report, 'body_height_offset': plan['body_height_offset'],
               'rebake_source_weights_sha256': digest(HERE / 'surface-weights.npz'), 'status': 'REBaked_SOURCE'})
bpy.context.view_layer.objects.active = rig
bpy.ops.object.select_all(action='DESELECT')
rig.select_set(True)
for mesh in meshes:
    mesh.select_set(True)
bpy.ops.file.pack_all()
bpy.context.preferences.filepaths.save_version = 0
bpy.ops.wm.save_as_mainfile(filepath=str(HERE / 'Baeksugwi_Run.blend'), compress=True, check_existing=False)
dump(report_path, report)
bpy.ops.export_scene.gltf(filepath=str(HERE / 'Baeksugwi_Run.glb'), export_format='GLB', use_selection=True,
                          export_yup=True, export_animations=True, export_animation_mode='ACTIONS',
                          export_anim_single_armature=True, export_frame_range=True, export_frame_step=1,
                          export_force_sampling=True, export_anim_slide_to_zero=True, export_def_bones=True,
                          export_optimize_animation_size=False, export_apply=False, export_texcoords=True,
                          export_normals=True, export_materials='EXPORT', export_image_format='AUTO',
                          export_cameras=False, export_lights=False, export_all_influences=False, export_skins=True)
report['export'] = verify_export(HERE / 'Baeksugwi_Run.glb')
report['blend'] = {'file': 'Baeksugwi_Run.blend', 'bytes': (HERE / 'Baeksugwi_Run.blend').stat().st_size}
report['status'] = 'EXPORTED'
dump(report_path, report)
print('FINAL_GLB_SAVED', HERE / 'Baeksugwi_Run.glb', flush=True)
if args.preview_dir:
    report['previews'] = render_previews(rig, meshes, motion, plan['floor'], args.preview_dir,
                                        samples=[(1, 'threequarter'), (12, 'side')] if args.focused_preview else None)
report['status'] = 'COMPLETE'
dump(report_path, report)
print('REBAKE_COMPLETE', flush=True)
