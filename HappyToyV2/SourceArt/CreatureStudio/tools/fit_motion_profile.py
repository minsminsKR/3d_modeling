"""Silent read-only numeric posture search on manually fitted source surface skin.

Creates a disposable rig from the recorded source plan. It never modifies the
source asset, joint plan or skin weights. The selected profile still needs the
actual authored mesh/normal/render audit before exporting a game asset.
"""
import argparse
import importlib.util
import json
import sys
from pathlib import Path

import bpy
import numpy as np
from mathutils import Vector

HERE = Path(__file__).resolve().parent


def load_motion():
    spec = importlib.util.spec_from_file_location('creature_motion_fit', HERE / 'creature_animation.py')
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def contacts(rig, positions, joints, weights, names, creature, finger_overrides=None):
    result = {}
    for side in ('Left', 'Right'):
        for kind in ('Hand', 'Foot'):
            root = rig.data.bones['mixamorig:' + side + kind]
            branch = {root.name} | {child.name for child in root.children_recursive}
            ids = [i for i, name in enumerate(names) if name in branch]
            strength = (weights * np.isin(joints, ids)).sum(1)
            subset = positions[strength >= .5]
            if len(subset) < 8:
                raise ValueError('Insufficient source support skin: ' + side + kind)
            joint = np.array(root.head_local)
            finger = np.array(root.tail_local - root.head_local)
            key = side.lower() + '_' + kind.lower()
            if kind == 'Hand' and key in (finger_overrides or {}):
                finger = np.asarray(finger_overrides[key], dtype=float)
            finger /= np.linalg.norm(finger)
            if kind == 'Hand':
                along = (subset - joint) @ finger
                palm = subset[(along > 0) & (along < max(root.length * .8, .03))]
                if len(palm) < 8:
                    raise ValueError('Insufficient actual palm surface')
                _, vectors = np.linalg.eigh(np.cov(palm.T))
                normal = vectors[:, 0]
                normal -= finger * np.dot(normal, finger)
                normal /= np.linalg.norm(normal)
                if normal[1] > 0:
                    normal *= -1
                plane = (palm - joint) @ normal
                surface = palm[plane >= np.percentile(plane, 85)]
                entry = {'contact_point': surface.mean(0).tolist(), 'palm_normal': normal.tolist(),
                         'finger_direction': finger.tolist()}
            else:
                underside = subset[subset[:, 2] <= subset[:, 2].min() + .006]
                entry = {'contact_point': underside.mean(0).tolist()}
            result[key] = entry
    if creature == 'Baby':
        for side in ('Left', 'Right'):
            thigh = rig.data.bones['mixamorig:' + side + 'UpLeg']
            knee = rig.data.bones['mixamorig:' + side + 'Leg']
            ankle = rig.data.bones['mixamorig:' + side + 'Foot']
            center = np.array(knee.head_local)
            axis = np.array(ankle.head_local - knee.head_local)
            length = np.linalg.norm(axis)
            axis /= length
            branch = {thigh.name} | {child.name for child in thigh.children_recursive}
            ids = [i for i, name in enumerate(names) if name in branch]
            strength = (weights * np.isin(joints, ids)).sum(1)
            subset = positions[(strength >= .5) & (np.linalg.norm(positions - center, axis=1) < length * .45)]
            if len(subset) < 8:
                raise ValueError('No actual source knee volume')
            offset = subset - center
            perpendicular = offset - (offset @ axis)[:, None] * axis
            result[side.lower() + '_knee'] = {'clearance': float(np.percentile(np.linalg.norm(perpendicular, axis=1), 95))}
    return result


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--plan', type=Path, required=True)
    parser.add_argument('--surface', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--height', type=float)
    parser.add_argument('--pitches', type=float, nargs='+', default=[.94, 1.25, 1.35, 1.45, 1.50, 1.55])
    parser.add_argument('--height-ratios', type=float, nargs='+', default=[.26, .28, .30, .32])
    parser.add_argument('--spine-bends', type=float, nargs='+', default=[0.0])
    parser.add_argument('--hand-clearance-offset', type=float, default=0.0)
    parser.add_argument('--foot-clearance-offset', type=float, default=0.0)
    parser.add_argument('--finger-directions', type=Path, help='Recorded actual palm-to-main-finger axes in Blender coordinates')
    parser.add_argument('--sample-step', type=int, default=12)
    args = parser.parse_args(sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else [])
    plan = json.loads(args.plan.read_text(encoding='utf8'))
    surface = np.load(args.surface)
    names = list(surface['bone_names'])
    source = surface['positions'].astype(np.float64)
    source = source[:, [0, 2, 1]]
    source[:, 1] *= -1
    low, high = source.min(0), source.max(0)
    height = args.height or plan['target_height_m']
    scale = height / (high[2] - low[2])
    center = np.array([(low[0] + high[0]) * .5, (low[1] + high[1]) * .5, low[2]])
    positions = (source - center) * scale
    joints, weights = surface['joints'], surface['weights']
    bpy.ops.wm.read_factory_settings(use_empty=True)
    data = bpy.data.armatures.new('Disposable profile-fit rig')
    rig = bpy.data.objects.new(data.name, data)
    bpy.context.scene.collection.objects.link(rig)
    bpy.context.view_layer.objects.active = rig
    rig.select_set(True)
    bpy.ops.object.mode_set(mode='EDIT')
    for entry in plan['bones']:
        bone = data.edit_bones.new(entry['name'])
        for field in ('head', 'tail'):
            original = entry[field]
            value = np.array([original[0], -original[2], original[1]])
            setattr(bone, field, Vector((value - center) * scale))
    for entry in plan['bones']:
        bone = data.edit_bones[entry['name']]
        if entry['parent']:
            bone.parent = data.edit_bones[entry['parent']]
            bone.use_connect = (bone.parent.tail - bone.head).length < 1e-6
    bpy.ops.object.mode_set(mode='OBJECT')
    finger_overrides = json.loads(args.finger_directions.read_text(encoding='utf8')) if args.finger_directions else None
    contact = contacts(rig, positions, joints, weights, names, plan['creature'], finger_overrides)
    module = load_motion()
    chains = module.build_chains(rig, module.bone_lookup(rig), plan['creature'], contact, 0)
    for chain in chains:
        offset = args.hand_clearance_offset if chain['category'] == 'hand' else args.foot_clearance_offset
        if offset:
            contact[chain['semantic']]['clearance'] = chain['clearance'] + offset
    chosen = np.arange(0, len(positions), max(1, args.sample_step))
    sampled = positions[chosen]
    sampled_joints = joints[chosen]
    sampled_weights = weights[chosen]
    dominant = sampled_joints[np.arange(len(chosen)), sampled_weights.argmax(1)]
    body_ids = [i for i, name in enumerate(names) if name.rsplit(':', 1)[-1] in ('Hips', 'Spine', 'Spine1', 'Spine2', 'Neck')]
    body_mask = np.isin(dominant, body_ids)
    head_mask = dominant == names.index('mixamorig:Head')
    inverse = np.array([np.asarray(data.bones[name].matrix_local.inverted()) for name in names])
    report = {'creature': plan['creature'], 'sourcePlan': str(args.plan), 'height': height,
              'originalVertices': len(source), 'sampledVertices': len(sampled),
              'actualSourceSkinUsed': True, 'readOnly': True, 'contacts': contact, 'candidates': []}
    for pitch, ratio, spine_bend in ((p, r, s) for p in args.pitches for r in args.height_ratios
                                   for s in args.spine_bends):
        height_value = height * ratio
        overrides = {action: {'pitch': pitch, 'pelvis_height': height_value, 'spine_bend': spine_bend}
                     for action in ('patrol', 'chase')}
        result = module.apply_creature_animation(rig, plan['creature'], contact, fps=16,
                                                 include_idle=False, profile_overrides=overrides)
        candidate = {'pitch': pitch, 'spine_bend': spine_bend, 'pelvis_height': height_value, 'height_ratio': ratio,
                     'minimum_body_z': float('inf'), 'minimum_head_z': float('inf'),
                     'minimum_all_skin_z': float('inf'), 'reach': {}, 'samples': []}
        candidate['minimum_by_bone'] = {name: None for name in names}
        for action_name in ('patrol', 'chase'):
            action = bpy.data.actions[action_name]
            rig.animation_data.action = action
            start, end = action.frame_range
            reach = result['actions'][action_name]['max_target_correction']
            candidate['reach'][action_name] = reach
            for phase in (0.0, .25, .5, .75):
                frame = start + phase * (end - start)
                whole = int(frame)
                bpy.context.scene.frame_set(whole, subframe=frame - whole)
                bpy.context.view_layer.update()
                palette = np.array([np.asarray(rig.pose.bones[name].matrix) for name in names]) @ inverse
                selected = palette[sampled_joints]
                transformed = np.einsum('nkij,nj->nki', selected[:, :, :3, :3], sampled) + selected[:, :, :3, 3]
                skin = (transformed * sampled_weights[:, :, None]).sum(1)
                body_min = float(skin[body_mask, 2].min())
                head_min = float(skin[head_mask, 2].min())
                all_min = float(skin[:, 2].min())
                candidate['minimum_body_z'] = min(candidate['minimum_body_z'], body_min)
                candidate['minimum_head_z'] = min(candidate['minimum_head_z'], head_min)
                candidate['minimum_all_skin_z'] = min(candidate['minimum_all_skin_z'], all_min)
                for index, bone_name in enumerate(names):
                    subset = skin[dominant == index]
                    if len(subset):
                        value = float(subset[:, 2].min())
                        previous = candidate['minimum_by_bone'][bone_name]
                        candidate['minimum_by_bone'][bone_name] = value if previous is None else min(previous, value)
                lowest = int(np.argmin(skin[:, 2]))
                candidate['samples'].append({'action': action_name, 'phase': phase,
                                              'body_minimum_z': body_min, 'head_minimum_z': head_min,
                                              'all_skin_minimum_z': all_min,
                                              'lowest_source_vertex': int(chosen[lowest]),
                                              'lowest_dominant_bone': names[dominant[lowest]],
                                              'lowest_world_point': skin[lowest].tolist()})
        candidate['maximum_reach_correction'] = max(value for action in candidate['reach'].values() for value in action.values())
        candidate['score'] = max(0.0, -candidate['minimum_body_z']) * 4 + candidate['maximum_reach_correction']
        report['candidates'].append(candidate)
        print(f"FIT pitch={pitch:.3f} spine={spine_bend:.3f} pelvis={height_value:.3f} bodyMin={candidate['minimum_body_z']:.4f} allMin={candidate['minimum_all_skin_z']:.4f} reach={candidate['maximum_reach_correction']:.4f}", flush=True)
    report['candidates'].sort(key=lambda item: item['score'])
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(report, indent=2), encoding='utf8')
    print('PROFILE_FIT_SAVED ' + str(args.output), flush=True)


if __name__ == '__main__':
    main()
