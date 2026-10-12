"""Proportion-aware in-place horror locomotion for new CreatureStudio rigs.

Blender 4.0.2. Public entry point:
  apply_creature_animation(rig, 'Uncat'|'Cyclopse'|'Baby', contact_specs)

Bone names stay Mixamo compatible, including the mixamorig: namespace. Exact
actions: idle, patrol, chase; Baby additionally cry. No audio, Unity mutation,
IK constraints, controllers, drivers or baked root travel are introduced.

contact_specs uses keys left_hand/right_hand/left_foot/right_foot/left_knee/
right_knee. Each value may provide armature-local Blender Z-up:
  contact_point: measured sole/palm point in rest mesh coordinates;
  clearance: override posed wrist/ankle/knee floor clearance in model units;
  palm_normal, finger_direction: original rest mesh palm plane/finger axes;
  rotation: desired support bone quaternion (w,x,y,z), overrides axis fitting;
  anchor: override XY support placement, with Z replaced by measured clearance.
The rest pose must be upright, floor aligned, facing Blender -Y. Meshes and
bind poses are untouched; only ordinary pose-bone actions are generated.
"""
import json
import math
import re

import bpy
from mathutils import Matrix, Quaternion, Vector

UP = Vector((0, 0, 1))
FORWARD = Vector((0, -1, 0))
RIGHT = Vector((1, 0, 0))
TAU = math.tau


def vec(value):
    return value.copy() if isinstance(value, Vector) else Vector(value)


def unit(value, fallback=(1, 0, 0)):
    value = vec(value)
    return value.normalized() if value.length > 1e-8 else vec(fallback).normalized()


def smooth(value):
    value = max(0.0, min(1.0, value))
    return value ** 3 * (10 - 15 * value + 6 * value ** 2)


def support_frame(finger, normal):
    finger = unit(finger)
    normal = vec(normal) - finger * vec(normal).dot(finger)
    normal = unit(normal, (0, -1, 0))
    across = unit(finger.cross(normal))
    normal = across.cross(finger).normalized()
    return Matrix((across, finger, normal)).transposed()


def bone_lookup(armature):
    result = {}
    for bone in armature.pose.bones:
        semantic = bone.name.rsplit(':', 1)[-1]
        result[semantic] = bone.name
    required = ['Hips', 'Head'] + [side + suffix for side in ('Left', 'Right')
                                  for suffix in ('Arm', 'ForeArm', 'Hand', 'UpLeg', 'Leg', 'Foot')]
    missing = [name for name in required if name not in result]
    if missing:
        raise ValueError('Missing anatomical Mixamo bones: ' + ', '.join(missing))
    return result


def pose_matrix(point, rotation):
    return Matrix.Translation(point) @ rotation.to_matrix().to_4x4()


def rest_under_parent(pb, posed):
    if pb.parent:
        return posed[pb.parent.name] @ pb.parent.bone.matrix_local.inverted() @ pb.bone.matrix_local
    return pb.bone.matrix_local.copy()


def store_pose(pb, desired, posed, previous, frame):
    kwargs = {}
    if pb.parent:
        kwargs = {'parent_matrix': posed[pb.parent.name],
                  'parent_matrix_local': pb.parent.bone.matrix_local}
    basis = pb.bone.convert_local_to_pose(desired, pb.bone.matrix_local, invert=True, **kwargs)
    location, rotation, scale = basis.decompose()
    if pb.name in previous and rotation.dot(previous[pb.name]) < 0:
        rotation.negate()
    pb.rotation_mode = 'QUATERNION'
    pb.location, pb.rotation_quaternion, pb.scale = location, rotation, scale
    for property_name in ('location', 'rotation_quaternion', 'scale'):
        pb.keyframe_insert(data_path=property_name, frame=frame, group=pb.name)
    previous[pb.name] = rotation.copy()
    posed[pb.name] = desired


def solve_ik(start, target, upper, lower, pole):
    """Constant-length analytic IK with floor-preserving minimum/maximum reach."""
    start, target = vec(start), vec(target)
    original = target.copy()
    total = upper + lower
    minimum = max(abs(upper - lower) + total * .006, total * .075)
    maximum = total * .994
    dz = target.z - start.z
    if abs(dz) < maximum:
        horizontal = target - start
        horizontal.z = 0
        minimum_xy = math.sqrt(max(0, minimum ** 2 - dz ** 2))
        maximum_xy = math.sqrt(max(0, maximum ** 2 - dz ** 2))
        length = min(max(horizontal.length, minimum_xy), maximum_xy)
        horizontal = unit(horizontal, pole) * length
        target.x, target.y = start.x + horizontal.x, start.y + horizontal.y
    direction = unit(target - start, (0, 0, -1))
    distance = min(max((target - start).length, minimum), maximum)
    endpoint = start + direction * distance
    along = (upper ** 2 - lower ** 2 + distance ** 2) / (2 * distance)
    height = math.sqrt(max(0, upper ** 2 - along ** 2))
    bend = vec(pole) - direction * vec(pole).dot(direction)
    bend = unit(bend, direction.cross(RIGHT))
    elbow = start + direction * along + bend * height
    return elbow, endpoint, (endpoint - original).length


def profiles(creature):
    if creature == 'Cyclopse':
        return {
            'idle': dict(duration=2.8, duty=.64, stride=.0, lift=.0, pitch=.025, sway=.008, bob=.005),
            'patrol': dict(duration=1.25, duty=.65, stride=.30, lift=.055, pitch=.055, sway=.016, bob=.012),
            'chase': dict(duration=1.30, gait_cycles=2, duty=.43, stride=.52, lift=.115, pitch=.19, sway=.030, bob=.029, head_yaw=.22, head_roll=.34, head_snap=.16, body_lurch=.018),
        }
    if creature == 'Uncat':
        return {
            'idle': dict(duration=2.7, duty=.70, stride=.0, lift=.0, pitch=.92, sway=.008, bob=.004),
            'patrol': dict(duration=1.30, duty=.71, stride=.29, lift=.065, pitch=.94, sway=.016, bob=.010),
            'chase': dict(duration=1.12, gait_cycles=2, duty=.43, stride=.52, lift=.15, pitch=.98, sway=.032, bob=.027, head_yaw=.23, head_roll=.32, head_snap=.14, body_lurch=.024),
        }
    if creature == 'Baby':
        return {
            'idle': dict(duration=2.5, duty=.72, stride=.0, lift=.0, pitch=1.12, sway=.006, bob=.003),
            'patrol': dict(duration=1.45, duty=.72, stride=.26, lift=.075, pitch=1.14, sway=.012, bob=.008),
            'chase': dict(duration=1.18, gait_cycles=2, duty=.44, stride=.44, lift=.16, pitch=1.18, sway=.027, bob=.022, head_yaw=.21, head_roll=.33, head_snap=.22, body_lurch=.022),
            'cry': dict(duration=2.0, duty=1.0, stride=.0, lift=.0, pitch=1.10, sway=.008, bob=.006),
        }
    raise ValueError('Unknown creature: ' + creature)


def build_chains(armature, names, creature, specs, floor):
    chains = []
    for side, sign in [('Left', -1), ('Right', 1)]:
        for category, suffixes in [('hand', ('Arm', 'ForeArm', 'Hand')),
                                    ('foot', ('UpLeg', 'Leg', 'Foot'))]:
            semantic = side.lower() + '_' + category
            spec = dict(specs.get(semantic, {}))
            bones = [armature.data.bones[names[side + suffix]] for suffix in suffixes]
            rest = [b.head_local.copy() for b in bones]
            # Semantic Left/Right is not assumed to imply a particular global
            # X sign. Imported Mixamo rigs and fitted image rigs may differ.
            hips_x = armature.data.bones[names['Hips']].head_local.x
            anatomical_side = rest[0].x - hips_x
            if abs(anatomical_side) > 1e-6:
                sign = 1 if anatomical_side > 0 else -1
            lengths = [(rest[i + 1] - rest[i]).length for i in (0, 1)]
            if min(lengths) <= 1e-5:
                raise ValueError('Zero-length limb chain: ' + semantic)
            rotation = bones[2].matrix_local.to_quaternion()
            if 'rotation' in spec:
                rotation = Quaternion(spec['rotation']).normalized()
            elif category == 'hand' and creature != 'Cyclopse':
                rest_finger = vec(spec.get('finger_direction', bones[2].tail_local - bones[2].head_local))
                rest_normal = vec(spec.get('palm_normal', (0, -1, 0)))
                target_basis = support_frame(FORWARD, -UP)
                rest_basis = support_frame(rest_finger, rest_normal)
                rotation = (target_basis @ rest_basis.inverted()).to_quaternion() @ rotation
            delta = rotation @ bones[2].matrix_local.to_quaternion().inverted()
            if 'clearance' in spec:
                clearance = float(spec['clearance'])
            elif 'contact_point' in spec:
                clearance = max(0.0, -(delta @ (vec(spec['contact_point']) - rest[2])).z)
            elif category == 'foot':
                toe_name = names.get(side + 'ToeBase')
                toe_floor = armature.data.bones[toe_name].tail_local.z if toe_name else floor
                clearance = max(sum(lengths) * .025, rest[2].z - min(toe_floor, floor))
            else:
                clearance = sum(lengths) * .055
            chains.append({'names': [b.name for b in bones], 'semantic': semantic,
                           'category': category, 'side': side, 'sign': sign, 'rest': rest,
                           'lengths': lengths, 'rotations': [b.matrix_local.to_quaternion() for b in bones],
                           'support_rotation': rotation, 'clearance': clearance,
                           'anchor_override': spec.get('anchor'),
                           'phase': 0.0 if side == 'Left' else .5,
                           'knee_spec': dict(specs.get(side.lower() + '_knee', {}))})
    return chains


def hip_height(armature, names, chains, creature, cfg, floor):
    legs = [c for c in chains if c['category'] == 'foot']
    arms = [c for c in chains if c['category'] == 'hand']
    leg_length = sum(sum(c['lengths']) for c in legs) / 2
    if creature == 'Cyclopse':
        return floor + leg_length * (.94 if cfg['stride'] else .965) + sum(c['clearance'] for c in legs) / 2
    if creature == 'Baby':
        return floor + sum(c['lengths'][0] for c in legs) / 2 * .80 + leg_length * .06
    hips = armature.data.bones[names['Hips']].head_local
    pitch = Quaternion(RIGHT, cfg['pitch'])
    requested = floor + leg_length * .57 + sum(c['clearance'] for c in legs) / 2
    for arm in arms:
        shoulder_height = (pitch @ (arm['rest'][0] - hips)).z
        requested = min(requested, floor + arm['clearance'] + sum(arm['lengths']) * .88 - shoulder_height)
    return requested


def chain_pose(armature, chain, points, hand_rotation, posed, previous, frame):
    for i, name in enumerate(chain['names'][:2]):
        direction = points[i + 1] - points[i]
        original = chain['rest'][i + 1] - chain['rest'][i]
        rotation = original.rotation_difference(direction) @ chain['rotations'][i]
        store_pose(armature.pose.bones[name], pose_matrix(points[i], rotation), posed, previous, frame)
    store_pose(armature.pose.bones[chain['names'][2]], pose_matrix(points[2], hand_rotation), posed, previous, frame)


def support_target(chain, shoulder, hip_pos, creature, cfg, phase, floor, limb_scale):
    contact_phase = (phase + chain['phase']) % 1
    if chain['category'] == 'foot' and creature != 'Cyclopse':
        contact_phase = (contact_phase + .5) % 1
    duty = cfg['duty']
    stride = cfg['stride'] * limb_scale
    lift = cfg['lift'] * limb_scale
    if creature == 'Cyclopse':
        anchor = chain['rest'][2].copy()
    elif chain['category'] == 'hand':
        anchor = shoulder + FORWARD * (sum(chain['lengths']) * .17) + RIGHT * chain['sign'] * sum(chain['lengths']) * .12
    else:
        anchor = hip_pos - FORWARD * sum(chain['lengths']) * .23 + RIGHT * chain['sign'] * sum(chain['lengths']) * .17
    if chain['anchor_override'] is not None:
        anchor = vec(chain['anchor_override'])
    anchor.z = floor + chain['clearance']
    recovery = 0.0
    if not stride:
        return anchor, recovery
    if contact_phase < duty:
        anchor += FORWARD * stride * (.5 - contact_phase / duty)
    else:
        recovery = (contact_phase - duty) / (1 - duty)
        anchor += FORWARD * stride * (-.5 + smooth(recovery))
        anchor.z += lift * math.sin(math.pi * recovery) ** 1.35
    return anchor, recovery


def solve_baby_knee(chain, hip_joint, knee_target, knee_lift, floor):
    """Knee-ground sphere constraint plus trailing shin; never compress the thigh."""
    upper, lower = chain['lengths']
    dz = knee_target.z - hip_joint.z
    dz = min(max(dz, -upper * .99), upper * .99)
    horizontal = knee_target - hip_joint
    horizontal.z = 0
    radius = math.sqrt(max(0, upper ** 2 - dz ** 2))
    horizontal = unit(horizontal, FORWARD) * radius
    knee = hip_joint + horizontal + UP * dz
    ankle_height = max(floor + chain['clearance'] + lower * .04,
                       knee.z + lower * (.20 + .55 * math.sin(math.pi * knee_lift)))
    ankle_vertical = min(max(ankle_height - knee.z, -lower * .9), lower * .9)
    trailing = math.sqrt(max(0, lower ** 2 - ankle_vertical ** 2))
    ankle = knee - FORWARD * trailing + UP * ankle_vertical
    return knee, ankle


def apply_creature_animation(armature, creature, contact_specs=None, fps=60, *, ground_height=0.0,
                             pelvis_height=None, report_path=None, include_idle=True,
                             profile_overrides=None):
    """Generate fresh model-fitted actions and leave patrol active at frame one."""
    creature = {'uncat': 'Uncat', 'cyclopse': 'Cyclopse', 'cyclops': 'Cyclopse', 'baby': 'Baby'}.get(str(creature).lower(), creature)
    if armature.type != 'ARMATURE':
        raise TypeError('Expected anatomical armature')
    names = bone_lookup(armature)
    chains = build_chains(armature, names, creature, contact_specs or {}, ground_height)
    controlled = {name for chain in chains for name in chain['names']}
    parent_order = sorted(armature.pose.bones, key=lambda p: len(p.parent_recursive))
    hips = armature.pose.bones[names['Hips']]
    head = armature.pose.bones[names['Head']]
    mean_leg = sum(sum(c['lengths']) for c in chains if c['category'] == 'foot') / 2
    mean_arm = sum(sum(c['lengths']) for c in chains if c['category'] == 'hand') / 2
    scale = mean_leg if creature == 'Cyclopse' else mean_arm
    scene = bpy.context.scene
    scene.render.fps = int(fps)
    armature.animation_data_create()
    actions = profiles(creature)
    for name, overrides in (profile_overrides or {}).items():
        if name not in actions:
            raise ValueError('Unknown action profile override: ' + name)
        actions[name].update(overrides)
    if not include_idle:
        actions.pop('idle')
    # Preserve exact exported take names when a fitted model is rebaked.
    # Independent monsters should be authored in separate Blender scenes.
    for action_name in actions:
        previous_action = bpy.data.actions.get(action_name)
        if previous_action:
            if (previous_action.get('creature') == creature
                    and previous_action.get('in_place') is True):
                if armature.animation_data.action == previous_action:
                    armature.animation_data.action = None
                bpy.data.actions.remove(previous_action)
            else:
                raise ValueError('Action name collision; author/export each monster separately: ' + action_name)
    report = {'creature': creature, 'armature': armature.name, 'coordinate_system': 'Blender Z-up, front -Y',
              'in_place': True, 'ground_height': ground_height, 'fps': fps,
              'arm_lengths': mean_arm, 'leg_lengths': mean_leg, 'actions': {}}
    for name, cfg in actions.items():
        action = bpy.data.actions.new(name)
        action.use_fake_user = True
        armature.animation_data.action = action
        interval_count = max(12, round(cfg['duration'] * fps))
        explicit_height = cfg.get('pelvis_height', pelvis_height)
        chosen_height = ground_height + explicit_height if explicit_height is not None else hip_height(armature, names, chains, creature, cfg, ground_height)
        previous = {}
        max_reach = {chain['semantic']: 0.0 for chain in chains}
        max_length_error, max_scale_error = 0.0, 0.0
        first_pose = None
        for step in range(interval_count + 1):
            frame = 1 + step
            cycle = (step / interval_count) % 1
            phase = (cycle * cfg.get('gait_cycles', 1)) % 1
            cry = name == 'cry'
            pitch = cfg['pitch'] + (0.018 * math.sin(TAU * phase * 2) if cfg['stride'] else .009 * math.sin(TAU * phase))
            pitch += cfg.get('body_lurch', 0) * math.sin(TAU * cycle * 3)
            shudder = .006 * math.sin(TAU * phase * 8) if cry else 0.0
            position = hips.bone.head_local.copy()
            position.z = chosen_height
            position.z += cfg['bob'] * scale * math.sin(TAU * phase * (2 if cfg['stride'] else 1))
            position.x += cfg['sway'] * scale * math.sin(TAU * phase) + shudder * scale
            body_rotation = Quaternion(RIGHT, pitch) @ Quaternion(FORWARD, cfg['sway'] * math.sin(TAU * phase)) @ hips.bone.matrix_local.to_quaternion()
            posed = {}
            for pb in parent_order:
                if pb.name in controlled or any(parent.name in controlled for parent in pb.parent_recursive):
                    continue
                base = rest_under_parent(pb, posed)
                if pb == hips:
                    base = pose_matrix(position, body_rotation)
                elif pb.name in (names.get('Spine'), names.get('Spine1')) and cfg.get('spine_bend', 0):
                    # A pear-shaped short-armed source needs a flexed upper
                    # back, rather than rotating its large belly into the
                    # floor to make the palms reach. Joint links stay exact.
                    bend_fraction = .7 if pb.name == names.get('Spine') else .3
                    rotation = Quaternion(RIGHT, cfg['spine_bend'] * bend_fraction) @ base.to_quaternion()
                    base = pose_matrix(base.translation, rotation)
                elif pb.name == names.get('Neck') and cfg.get('neck_bend', 0):
                    base = pose_matrix(base.translation,
                                       Quaternion(RIGHT, cfg['neck_bend']) @ base.to_quaternion())
                elif pb == head:
                    head_pitch = cfg.get('head_pitch', -.18 if creature == 'Baby' else (-.05 if creature == 'Uncat' else .02))
                    if cry:
                        head_pitch += .055 * math.sin(TAU * phase * 4)
                    yaw = (.025 if name != 'chase' else .012) * math.sin(TAU * phase)
                    roll = .025 * math.sin(TAU * phase * (3 if cry else 1))
                    if name == 'chase':
                        # A two-stride supercycle decouples the face from the feet.
                        # Smooth, bounded impulses give neck snaps without random
                        # frame noise, root motion, bone stretching or loop seams.
                        accent = math.tanh(2.4 * math.sin(TAU * cycle * 3)) / math.tanh(2.4)
                        yaw = cfg.get('head_yaw', 0) * (.72 * math.sin(TAU * cycle) + .28 * math.sin(TAU * cycle * 3))
                        roll = cfg.get('head_roll', 0) * (.70 * accent + .30 * math.sin(TAU * cycle * 2))
                        head_pitch += cfg.get('head_snap', 0) * (.75 * math.sin(TAU * cycle * 4) + .25 * math.sin(TAU * cycle))
                    head_rotation = Quaternion(UP, yaw) @ Quaternion(RIGHT, head_pitch) @ Quaternion(FORWARD, roll) @ head.bone.matrix_local.to_quaternion()
                    base = pose_matrix(base.translation, head_rotation)
                store_pose(pb, base, posed, previous, frame)
            for chain in chains:
                pb = armature.pose.bones[chain['names'][0]]
                start = rest_under_parent(pb, posed).translation
                if creature == 'Cyclopse' and chain['category'] == 'hand':
                    local_phase = (phase + chain['phase'] + .5) % 1
                    swing = (.22 if name == 'patrol' else .62 if name == 'chase' else .015) * math.sin(TAU * local_phase)
                    upper_dir = unit(-UP + FORWARD * swing + RIGHT * chain['sign'] * .34)
                    lower_dir = unit(-UP + FORWARD * (.17 + swing * .75) + RIGHT * chain['sign'] * .15)
                    elbow = start + upper_dir * chain['lengths'][0]
                    endpoint = elbow + lower_dir * chain['lengths'][1]
                    forearm_delta = (chain['rest'][2] - chain['rest'][1]).rotation_difference(endpoint - elbow)
                    rotation = forearm_delta @ chain['rotations'][2]
                    correction = 0.0
                elif creature == 'Baby' and chain['category'] == 'foot':
                    local_phase = (phase + chain['phase'] + .5) % 1
                    duty = cfg['duty']
                    recovery = (local_phase - duty) / (1 - duty) if local_phase > duty and duty < 1 else 0
                    knee_spec = chain['knee_spec']
                    knee_clearance = float(knee_spec.get('clearance', chain['lengths'][0] * .14))
                    target = start + FORWARD * chain['lengths'][0] * (.52 - cfg['stride'] * .42 * math.sin(TAU * local_phase))
                    target.z = ground_height + knee_clearance + chain['lengths'][0] * cfg['lift'] * math.sin(math.pi * recovery) ** 1.35
                    elbow, endpoint = solve_baby_knee(chain, start, target, recovery, ground_height)
                    # Trailing feet point behind the crawling infant with the
                    # soles horizontal; inheriting the shin's 90-degree bend
                    # would send the toes down through the floor.
                    rotation = Quaternion(UP, math.pi) @ chain['rotations'][2]
                    correction = abs(elbow.z - target.z)
                else:
                    target, recovery = support_target(chain, start, position, creature, cfg, phase, ground_height, scale)
                    # Cry is stationary palm support with a tiny human-like shake.
                    if cry and chain['category'] == 'hand':
                        target.x += shudder * scale * .45
                    pole = FORWARD if chain['category'] == 'foot' else RIGHT * chain['sign'] + FORWARD * .25
                    elbow, endpoint, correction = solve_ik(start, target, *chain['lengths'], pole)
                    curl = (.12 if chain['category'] == 'foot' else .075) * math.sin(math.pi * recovery)
                    rotation = Quaternion(RIGHT, curl) @ chain['support_rotation']
                chain_pose(armature, chain, (start, elbow, endpoint), rotation, posed, previous, frame)
                max_reach[chain['semantic']] = max(max_reach[chain['semantic']], correction)
                max_length_error = max(max_length_error,
                    abs((elbow - start).length - chain['lengths'][0]),
                    abs((endpoint - elbow).length - chain['lengths'][1]))
            for pb in parent_order:
                if pb.name not in posed:
                    store_pose(pb, rest_under_parent(pb, posed), posed, previous, frame)
                max_scale_error = max(max_scale_error, max(abs(value - 1) for value in pb.scale))
            if first_pose is None:
                first_pose = {name: matrix.copy() for name, matrix in posed.items()}
        seam = max((posed[n].translation - first_pose[n].translation).length for n in posed)
        for curve in action.fcurves:
            for point in curve.keyframe_points:
                point.interpolation = 'LINEAR'
        action['loop'] = True
        action['in_place'] = True
        action['creature'] = creature
        action['support_style'] = 'hands and knees' if creature == 'Baby' else 'hands and feet' if creature == 'Uncat' else 'feet'
        action['duration_seconds'] = interval_count / fps
        action['gait_cycles'] = cfg.get('gait_cycles', 1)
        action['pursuit_style'] = 'two-stride off-beat head snaps' if name == 'chase' else 'calm authored locomotion'
        report['actions'][name] = {'action': action.name, 'frames': interval_count + 1,
                                   'duration_seconds': interval_count / fps, 'pelvis_height': chosen_height,
                                   'gait_cycles': cfg.get('gait_cycles', 1),
                                   'head_yaw_radians': cfg.get('head_yaw', 0),
                                   'head_roll_radians': cfg.get('head_roll', 0),
                                   'head_snap_radians': cfg.get('head_snap', 0),
                                   'body_pitch': cfg['pitch'], 'spine_bend': cfg.get('spine_bend', 0),
                                   'neck_bend': cfg.get('neck_bend', 0),
                                   'max_target_correction': max_reach, 'max_segment_length_error': max_length_error,
                                   'max_scale_error': max_scale_error, 'translation_loop_error': seam}
        if max_length_error > max(mean_arm, mean_leg) * 2e-5 or seam > 2e-5 or max_scale_error > 1e-4:
            raise AssertionError('Invalid baked limb length/loop/scale: ' + name)
    armature.animation_data.action = bpy.data.actions[report['actions']['patrol']['action']]
    scene.frame_start = 1
    scene.frame_end = report['actions']['patrol']['frames']
    scene.frame_set(1)
    if report_path:
        with open(report_path, 'w', encoding='utf-8') as stream:
            json.dump(report, stream, indent=2)
    return report
