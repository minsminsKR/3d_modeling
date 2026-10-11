"""Bake a 12-arm horror sprint into ordinary Blender pose bone keyframes.

All coordinates and axes are ARMATURE LOCAL, Blender Z up.  A normally imported
glTF Y-up model maps (x,y,z) to (x,-z,y), hence forward is (0,-1,0).

Public contract::

    limb_specs = [{"shoulder": "Arm1L_Shoulder", "elbow": "Arm1L_Elbow",
                   "wrist": "Arm1L_Wrist", "index": 1, "side": "L"}, ...]
    report = apply_run_animation(armature, limb_specs, fps=60,
                                 ground_height=floor_z)

Optional per-limb fields:
  anchor: armature-local XY location under a hand, default rest wrist head.
  wrist_clearance: wrist height above floor when palm touches, default 0.
  contact_point: measured rest palm floor point. If supplied, clearance and
      anchor derive from rest wrist minus this point unless explicitly set.
  pole: armature-local bend direction (not a location), default outward/back.
  stride, lift, phase: override gait parameters in model units/cycles.
  upper_length, lower_length: override head-to-head segment lengths.
  rest_shoulder, rest_elbow, rest_wrist: override joint points (usually omit).
  contact: false for short secondary arms; keep near their rest height and
      twitch/swing rather than trying to plant them on the common floor.
  hand_rotation: optional armature-local quaternion in (w,x,y,z) order for
      a corrected rigid palm orientation (default original rest orientation).

Each shoulder/elbow/wrist HEAD must represent that anatomical joint. Rebuild
the shoulder/elbow tails to the next joint for the cleanest editable rig.
This module also tolerates imported tails that do not point at the next joint.
It solves anatomical head-to-head links, never scales a limb, and keys exact
joint translations where the imported rest rig requires them.

Outputs are bpy Actions Run_InPlace and Run_Forward, both with fake users.
Run_Forward contains BodyRoot root motion. No IK constraints, controllers,
drivers or audio are created; both actions can be exported directly to GLB.
"""
import json
import math
import re

import bpy
from mathutils import Matrix, Quaternion, Vector

TAU = math.tau
IDENTITY = Matrix.Identity(4)
UP = Vector((0.0, 0.0, 1.0))


def _v(value):
    return value.copy() if isinstance(value, Vector) else Vector(value)


def _unit(value, fallback=(1, 0, 0)):
    result = _v(value)
    return result.normalized() if result.length > 1e-9 else Vector(fallback)


def _smootherstep(t):
    t = min(1.0, max(0.0, t))
    return t * t * t * (t * (6.0 * t - 15.0) + 10.0)


def _pose_matrix(point, rotation):
    return Matrix.Translation(point) @ rotation.to_matrix().to_4x4()


def solve_two_bone(shoulder, target, upper_length, lower_length, pole,
                   extension_limit=0.995):
    """Return exact elbow, reachable wrist and reach correction.

    Positive elbow height always uses the same projected pole. No branch flips,
    straight-leg singularities or scaling occur, including unreachable targets.
    Length is bounded away from full extension and from fully folded states.
    """
    shoulder, target = _v(shoulder), _v(target)
    delta = target - shoulder
    distance = delta.length
    upper_length, lower_length = float(upper_length), float(lower_length)
    total = upper_length + lower_length
    if min(upper_length, lower_length) <= 1e-6:
        raise ValueError('Two-bone IK needs two nonzero segment lengths')
    minimum = max(abs(upper_length - lower_length) + total * 0.005, total * 0.06)
    maximum = total * extension_limit
    solved_distance = min(max(distance, minimum), maximum)
    direction = _unit(delta, (0, 0, -1))
    wrist = shoulder + direction * solved_distance
    along = (upper_length ** 2 - lower_length ** 2 + solved_distance ** 2) / (2 * solved_distance)
    height = math.sqrt(max(0.0, upper_length ** 2 - along ** 2))
    bend = _v(pole) - direction * _v(pole).dot(direction)
    if bend.length < 1e-7:
        bend = direction.cross(UP)
        if bend.length < 1e-7:
            bend = direction.cross(Vector((1, 0, 0)))
    elbow = shoulder + direction * along + bend.normalized() * height
    return elbow, wrist, (wrist - target).length


def _store_pose(pb, desired, posed, previous_quaternions, frame):
    """Convert armature-space matrices with Blender's actual inheritance rules."""
    parent = pb.parent
    options = {}
    if parent:
        options['parent_matrix'] = posed[parent.name]
        options['parent_matrix_local'] = parent.bone.matrix_local
    basis = pb.bone.convert_local_to_pose(desired, pb.bone.matrix_local,
                                          invert=True, **options)
    location, rotation, scale = basis.decompose()
    previous = previous_quaternions.get(pb.name)
    if previous and rotation.dot(previous) < 0:
        rotation.negate()
    pb.rotation_mode = 'QUATERNION'
    pb.location = location
    pb.rotation_quaternion = rotation
    pb.scale = scale
    for property_name in ('location', 'rotation_quaternion', 'scale'):
        pb.keyframe_insert(data_path=property_name, frame=frame, group=pb.name)
    previous_quaternions[pb.name] = rotation.copy()
    posed[pb.name] = desired


def _unposed_child(pb, posed):
    if pb.parent:
        return (posed[pb.parent.name] @ pb.parent.bone.matrix_local.inverted()
                @ pb.bone.matrix_local)
    return pb.bone.matrix_local.copy()


def infer_limb_specs(armature):
    """Infer twelve Arm1L...Arm6R triplets by name; no topology mutation."""
    result = []
    for index in range(1, 7):
        for side in ('L', 'R'):
            prefix = f'Arm{index}{side}'
            names = [prefix + '_' + suffix for suffix in ('Shoulder', 'Elbow', 'Wrist')]
            if all(name in armature.pose.bones for name in names):
                result.append(dict(zip(('shoulder', 'elbow', 'wrist'), names),
                                   index=index, side=side))
    if not result:
        raise ValueError('No Arm{1..6}{L/R}_Shoulder/Elbow/Wrist chains found')
    return result


def _limb_data(armature, specs, forward, ground_height):
    result = []
    for supplied in specs:
        spec = dict(supplied)
        names = [spec[k] for k in ('shoulder', 'elbow', 'wrist')]
        bones = [armature.data.bones[name] for name in names]
        if bones[1].parent != bones[0] or bones[2].parent != bones[1]:
            raise ValueError(f'Expected shoulder > elbow > wrist parenting: {names}')
        rest = [_v(spec.get('rest_' + key, bone.head_local))
                for key, bone in zip(('shoulder', 'elbow', 'wrist'), bones)]
        l1 = float(spec.get('upper_length', (rest[1] - rest[0]).length))
        l2 = float(spec.get('lower_length', (rest[2] - rest[1]).length))
        match = re.search(r'Arm(\d+)([LR])', names[0])
        index = int(spec.get('index', match.group(1) if match else 1))
        side = str(spec.get('side', match.group(2) if match else ('L' if rest[0].x < 0 else 'R'))).upper()
        lateral = -1.0 if side == 'L' else 1.0
        anchor = _v(spec.get('anchor', rest[2]))
        measured_contact = _v(spec['contact_point']) if 'contact_point' in spec else None
        clearance = float(spec.get('wrist_clearance', rest[2].z - measured_contact.z
                                   if measured_contact is not None else 0.0))
        if measured_contact is not None and 'anchor' not in spec:
            anchor.x, anchor.y = measured_contact.x, measured_contact.y
        contact = bool(spec.get('contact', True))
        if contact:
            anchor.z = ground_height + max(0.0, clearance)
        elif 'anchor' not in spec:
            anchor = rest[2].copy()
        pole = _unit(spec.get('pole', Vector((lateral, 0, 0)) - forward * 0.30))
        result.append({'names': names, 'rest': rest, 'lengths': (l1, l2),
                       'rotations': [b.matrix_local.to_quaternion() for b in bones],
                       'anchor': anchor, 'pole': pole, 'index': index, 'side': side,
                       'phase': float(spec.get('phase', (index - 1) * 0.137 + (0.5 if side == 'R' else 0))),
                       'stride_override': spec.get('stride'), 'lift_override': spec.get('lift')})
        result[-1]['contact'] = contact
        result[-1]['wrist_clearance'] = max(0.0, clearance)
        result[-1]['secondary_floor_margin'] = float(spec.get('secondary_floor_margin', .035))
        result[-1]['hand_rotation'] = Quaternion(spec['hand_rotation']) if 'hand_rotation' in spec else None
    return result


def _reachable_planar_target(shoulder, target, length, horizontal_axis,
                              minimum_distance=0.0, extension_limit=0.995):
    """Keep contact Z while fitting XY into the exact two-bone reach annulus.

    Unequal links also have a *minimum* reach. Radially clamping a close planted
    hand would push it below its intended floor. Moving the XY target outward
    keeps the entire rigid palm at its measured contact height instead.
    """
    target = target.copy()
    vertical = target.z - shoulder.z
    radius = length * extension_limit
    if abs(vertical) >= radius:
        return target
    available = math.sqrt(max(0.0, radius * radius - vertical * vertical))
    delta = target - shoulder
    delta.z = 0
    minimum_horizontal = math.sqrt(max(0.0, minimum_distance ** 2 - vertical ** 2))
    desired_horizontal = min(max(delta.length, minimum_horizontal), available)
    if abs(desired_horizontal - delta.length) > 1e-9:
        delta = _unit(delta, horizontal_axis) * desired_horizontal
        target.x, target.y = shoulder.x + delta.x, shoulder.y + delta.y
    return target


def apply_run_animation(armature, limb_specs=None, fps=60, *, cycle_seconds=0.72,
                        forward_axis=(0, -1, 0), ground_height=None,
                        body_root='BodyRoot', create_forward=True, start_frame=1,
                        report_path=None, duty=0.48, stride=None,
                        body_bob=None, body_surge=None, body_height_offset=0.0,
                        extension_limit=0.995):
    """Create sprint clips and return an exportable JSON diagnostic dictionary.

    Rest rig and meshes are never changed. Existing animation is retained and
    Blender gives the new action a suffix when that action name already exists.
    The active action at return is Run_InPlace. The scene range is one cycle.
    Ground Z should be supplied from the corrected mesh floor, not an arbitrary
    web coordinate. For correct palms, supply contact_point/wrist_clearance.
    """
    if armature.type != 'ARMATURE':
        raise TypeError('armature must be a Blender ARMATURE object')
    if body_root not in armature.pose.bones:
        roots = [b.name for b in armature.pose.bones if b.parent is None]
        if len(roots) != 1:
            raise ValueError('Supply body_root for a rig with multiple roots')
        body_root = roots[0]
    root = armature.pose.bones[body_root]
    if root.parent:
        raise ValueError('BodyRoot must be the top-level deform bone')
    if not (0.15 < duty < 0.8):
        raise ValueError('duty must be within (0.15, 0.8)')
    forward = _v(forward_axis)
    forward.z = 0
    forward = _unit(forward, (0, -1, 0))
    right = _unit(forward.cross(UP))
    specs = list(limb_specs) if limb_specs is not None else infer_limb_specs(armature)
    if ground_height is None:
        ground_height = min(armature.data.bones[s['wrist']].head_local.z for s in specs)
    limbs = _limb_data(armature, specs, forward, float(ground_height))
    locomotion_limbs = [limb for limb in limbs if limb['contact']] or limbs
    mean_length = sum(sum(limb['lengths']) for limb in locomotion_limbs) / len(locomotion_limbs)
    stride = float(stride if stride is not None else mean_length * 0.235)
    body_bob = float(body_bob if body_bob is not None else mean_length * 0.018)
    body_surge = float(body_surge if body_surge is not None else mean_length * 0.007)
    interval_count = max(12, round(cycle_seconds * fps))
    actual_duration = interval_count / fps
    distance_per_cycle = stride / duty
    limb_names = {name for limb in limbs for name in limb['names']}
    parent_sorted = sorted(armature.pose.bones, key=lambda b: len(b.parent_recursive))
    armature.animation_data_create()
    bpy.context.scene.render.fps = int(fps)
    bpy.context.scene.frame_start = start_frame
    bpy.context.scene.frame_end = start_frame + interval_count
    action_results = {}
    for name, use_root_motion in [('Run_InPlace', False)] + ([('Run_Forward', True)] if create_forward else []):
        action = bpy.data.actions.new(name)
        action.use_fake_user = True
        armature.animation_data.action = action
        previous_quaternions = {}
        maximum_reach_correction = 0.0
        maximum_length_error = 0.0
        minimum_wrist_z = float('inf')
        maximum_basis_scale_error = 0.0
        contact_events = []
        reach_corrected_samples = 0
        limb_results = {limb['names'][2]: {'contact': limb['contact'],
                        'max_reach_correction': 0.0, 'max_target_correction': 0.0,
                        'reach_corrected_samples': 0, 'minimum_wrist_z': float('inf'),
                        'maximum_secondary_floor_lift': 0.0}
                        for limb in limbs}
        for step in range(interval_count + 1):
            frame = start_frame + step
            time = step / interval_count
            phase = time % 1.0
            motion_offset = forward * (distance_per_cycle * time if use_root_motion else 0)
            bob = body_bob * math.sin(TAU * phase * 2)
            surge = body_surge * math.sin(TAU * phase * 2 + 0.6)
            sway = mean_length * 0.006 * math.sin(TAU * phase)
            root_position = (root.bone.head_local + motion_offset + forward * surge
                             + right * sway + UP * (bob + body_height_offset))
            pitch = 0.045 * math.sin(TAU * phase * 2 - 0.2)
            roll = 0.035 * math.sin(TAU * phase)
            root_rotation = (Quaternion(right, pitch) @ Quaternion(forward, roll)
                             @ root.bone.matrix_local.to_quaternion())
            posed = {}
            _store_pose(root, _pose_matrix(root_position, root_rotation), posed, previous_quaternions, frame)
            # Small spine waves retain the silhouette. The face is one rigid
            # head transform and has no expression or individual mask bending.
            for pb in parent_sorted:
                if (pb.name == body_root or pb.name in limb_names
                        or any(p.name in limb_names for p in pb.parent_recursive)):
                    continue
                base = _unposed_child(pb, posed)
                wave = 0.0
                if pb.name.startswith('Spine'):
                    wave = 0.022 * math.sin(TAU * phase * 2 + {'SpineRear': 0.5, 'SpineBack': 1.0}.get(pb.name, 0))
                elif pb.name == 'Tail':
                    wave = 0.065 * math.sin(TAU * phase + 1.0)
                elif pb.name == 'Neck':
                    wave = -pitch * 0.7
                if wave:
                    local_axis = pb.bone.matrix_local.to_quaternion().inverted() @ right
                    base = base @ Quaternion(local_axis, wave).to_matrix().to_4x4()
                _store_pose(pb, base, posed, previous_quaternions, frame)
            for limb in limbs:
                shoulder_pb, elbow_pb, wrist_pb = [armature.pose.bones[n] for n in limb['names']]
                shoulder = _unposed_child(shoulder_pb, posed).translation
                local_phase = (phase + limb['phase']) % 1.0
                limb_stride = float(limb['stride_override'] if limb['stride_override'] is not None else stride)
                lift = float(limb['lift_override'] if limb['lift_override'] is not None else sum(limb['lengths']) * 0.23)
                target = limb['anchor'] + motion_offset
                if not limb['contact']:
                    # Attached short arms must not reach for a distant floor.
                    # They twitch at the cadence of the locomotion instead.
                    swing_amplitude = min(0.035, max(0.020, sum(limb['lengths']) * 0.09))
                    secondary_lift = float(limb['lift_override'] if limb['lift_override'] is not None
                                           else min(0.040, max(0.025, sum(limb['lengths']) * 0.10)))
                    target += (UP * (body_height_offset + bob) + forward * surge + right * sway
                               + forward * swing_amplitude * math.sin(TAU * local_phase)
                               + UP * secondary_lift * (0.45 + 0.55 * math.sin(TAU * local_phase + 0.7)))
                    safe_height = ground_height + limb['wrist_clearance'] + limb['secondary_floor_margin']
                    floor_lift = max(0.0, safe_height - target.z)
                    target.z += floor_lift
                    limb_results[limb['names'][2]]['maximum_secondary_floor_lift'] = max(
                        limb_results[limb['names'][2]]['maximum_secondary_floor_lift'], floor_lift)
                    recovery = local_phase
                elif local_phase < duty:
                    progress = local_phase / duty
                    target += forward * limb_stride * (0.5 - progress)
                    recovery = 0.0
                else:
                    recovery = (local_phase - duty) / (1 - duty)
                    target += forward * limb_stride * (-0.5 + _smootherstep(recovery))
                    target += UP * lift * math.sin(math.pi * recovery) ** 1.35
                    lateral_sign = -1 if str(limb['side']).upper() == 'L' else 1
                    target += right * lateral_sign * mean_length * 0.035 * math.sin(math.pi * recovery)
                requested_target = target.copy()
                total_length = sum(limb['lengths'])
                minimum_distance = max(abs(limb['lengths'][0] - limb['lengths'][1])
                                       + total_length * .005, total_length * .06)
                target = _reachable_planar_target(shoulder, target, total_length, forward,
                                                   minimum_distance, extension_limit)
                elbow, wrist, correction = solve_two_bone(shoulder, target, *limb['lengths'],
                                                          limb['pole'], extension_limit)
                maximum_reach_correction = max(maximum_reach_correction, correction)
                reach_corrected_samples += int(correction > mean_length * 0.001)
                limb_result = limb_results[limb['names'][2]]
                limb_result['max_reach_correction'] = max(limb_result['max_reach_correction'], correction)
                limb_result['max_target_correction'] = max(limb_result['max_target_correction'],
                                                             (wrist - requested_target).length)
                limb_result['reach_corrected_samples'] += int(correction > mean_length * 0.001)
                limb_result['minimum_wrist_z'] = min(limb_result['minimum_wrist_z'], wrist.z)
                minimum_wrist_z = min(minimum_wrist_z, wrist.z)
                maximum_length_error = max(maximum_length_error,
                    abs((elbow - shoulder).length - limb['lengths'][0]),
                    abs((wrist - elbow).length - limb['lengths'][1]))
                points = (shoulder, elbow, wrist)
                desired_directions = (elbow - shoulder, wrist - elbow)
                for i, pb in enumerate((shoulder_pb, elbow_pb)):
                    rest_direction = limb['rest'][i + 1] - limb['rest'][i]
                    swing = rest_direction.rotation_difference(desired_directions[i])
                    rotation = swing @ limb['rotations'][i]
                    _store_pose(pb, _pose_matrix(points[i], rotation), posed, previous_quaternions, frame)
                # Keep the entire hand rigid and nearly flat during stance;
                # briefly curl it during recovery rather than crushing vertices.
                if limb['contact']:
                    curl = 0.20 * math.sin(math.pi * recovery) if local_phase >= duty else 0.0
                else:
                    curl = 0.25 * math.sin(TAU * local_phase + 0.3)
                rest_hand_rotation = limb['hand_rotation'] or limb['rotations'][2]
                hand_rotation = Quaternion(right, curl) @ rest_hand_rotation
                _store_pose(wrist_pb, _pose_matrix(wrist, hand_rotation), posed, previous_quaternions, frame)
                for pb in (shoulder_pb, elbow_pb, wrist_pb):
                    maximum_basis_scale_error = max(maximum_basis_scale_error,
                                                     max(abs(s - 1.0) for s in pb.scale))
                if limb['contact'] and step < interval_count:
                    previous_phase = ((phase - 1 / interval_count) + limb['phase']) % 1.0
                    if local_phase < previous_phase:
                        contact_events.append({'bone': limb['names'][2], 'frame': frame,
                                               'phase': round(phase, 6)})
            # Finger/accessory bones remain rigid beneath their animated hand.
            for pb in parent_sorted:
                if pb.name not in posed:
                    _store_pose(pb, _unposed_child(pb, posed), posed, previous_quaternions, frame)
        for curve in action.fcurves:
            for point in curve.keyframe_points:
                point.interpolation = 'LINEAR'
        action['loop'] = True
        action['cycle_seconds'] = actual_duration
        action['gait'] = 'Twelve-arm alternating sequential horror sprint'
        action['root_motion'] = use_root_motion
        action['ground_height'] = float(ground_height)
        action_results[name] = {'action': action.name, 'frames': interval_count + 1,
                               'duration_seconds': actual_duration, 'root_motion': use_root_motion,
                               'distance_per_cycle': distance_per_cycle if use_root_motion else 0,
                               'speed_model_units_per_second': distance_per_cycle / actual_duration,
                               'max_segment_length_error': maximum_length_error,
                               'max_reach_correction': maximum_reach_correction,
                               'reach_corrected_samples': reach_corrected_samples,
                               'minimum_wrist_z': minimum_wrist_z,
                               'max_pose_scale_error': maximum_basis_scale_error,
                               'limbs': limb_results,
                               'contact_events': contact_events}
    active = bpy.data.actions[action_results['Run_InPlace']['action']]
    armature.animation_data.action = active
    bpy.context.scene.frame_set(start_frame)
    report = {'armature': armature.name, 'body_root': body_root, 'fps': fps,
              'forward_axis': list(forward), 'ground_height': float(ground_height),
              'arms': len(limbs), 'contact_arms': sum(int(l['contact']) for l in limbs),
              'body_height_offset': body_height_offset, 'stride': stride, 'duty': duty,
              'actions': action_results}
    if report_path:
        with open(report_path, 'w', encoding='utf-8') as stream:
            json.dump(report, stream, indent=2)
    return report
