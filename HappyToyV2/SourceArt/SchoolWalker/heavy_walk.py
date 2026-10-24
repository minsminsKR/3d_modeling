"""Bake a measured, planted human walk onto the preserved app-inferred rig.

This authors ordinary bone keys; it is not a motion capture or AI motion claim.
The foot stance follows the requested world travel exactly in an in-place clip.
Two-link analytic IK keeps the measured sole clearances and limb lengths. The
runtime owns translation and must use report['travelSpeedMetresPerSecond'].
"""
from pathlib import Path
import importlib.util
import math

import bpy
from mathutils import Matrix, Quaternion, Vector

HERE = Path(__file__).resolve().parent
PROJECT = HERE.parents[1]


def _helpers():
    path = PROJECT / 'SourceArt/CreatureStudio/tools/creature_animation.py'
    spec = importlib.util.spec_from_file_location('school_walker_analytic_ik', path)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def _weight_vertices(mesh, names, threshold=.45):
    indices = {group.index for group in mesh.vertex_groups if group.name in names}
    return [vertex.co.copy() for vertex in mesh.data.vertices
            if sum(entry.weight for entry in vertex.groups if entry.group in indices) >= threshold]


def measure_chains(rig, high, mapping):
    result = []
    for side, phase in (('Left', 0.), ('Right', .5)):
        for kind, suffixes in (('foot', ('UpLeg', 'Leg', 'Foot')),
                               ('hand', ('Arm', 'ForeArm', 'Hand'))):
            bones = [rig.data.bones[mapping[side + suffix]] for suffix in suffixes]
            points = [bone.head_local.copy() for bone in bones]
            lengths = [(points[index + 1] - points[index]).length for index in (0, 1)]
            if min(lengths) < .025:
                raise RuntimeError('Implausibly short reviewed anatomical chain: ' + side + kind)
            distal = bones[2]
            surface = _weight_vertices(high, {distal.name} | {bone.name for bone in distal.children_recursive})
            if len(surface) < 8:
                raise RuntimeError('No measured source skin for ' + side + kind)
            source_floor = min(point.z for point in surface)
            sole = [point for point in surface if point.z < source_floor + .018]
            clearance = points[2].z - source_floor
            hips = rig.data.bones[mapping['Hips']].head_local
            result.append(dict(side=side, kind=kind, phase=phase,
                names=[bone.name for bone in bones], rest=points, lengths=lengths,
                rotations=[bone.matrix_local.to_quaternion() for bone in bones],
                sign=1 if points[0].x > hips.x else -1,
                clearance=clearance, sourceSoleVertices=len(sole),
                heel=max((point.y for point in sole), default=points[2].y) - points[2].y,
                toe=min((point.y for point in sole), default=points[2].y) - points[2].y))
    return result


def bake(rig, high, mapping, settings):
    helper = _helpers()
    scene = bpy.context.scene
    fps = int(settings.get('fps', 60))
    name = settings.get('name', 'walk')
    idle = name == 'idle'
    duration = float(settings.get('duration', 2.8 if idle else 1.30))
    speed = 0. if idle else float(settings.get('speed', 1.0))
    duty = 1. if idle else float(settings.get('stance_fraction', .62))
    if not (40 <= fps <= 120 and ((idle and 2 <= duration <= 4) or
            (1.1 <= duration <= 1.9 and .55 <= speed <= 1.15 and .58 <= duty <= .72))):
        raise RuntimeError('Heavy-walk settings exceed the reviewed human gait envelope')
    frames = round(duration * fps)
    duration = frames / fps
    stride = speed * duration * duty
    chains = measure_chains(rig, high, mapping)
    legs = [chain for chain in chains if chain['kind'] == 'foot']
    hips = rig.data.bones[mapping['Hips']].head_local.copy()
    # A small knee bend must still reach both ends of the planted stance. This
    # is measured from this generated person's rig, not a generic body height.
    hip_height = min(chain['clearance'] + math.sqrt(max(.01,
        (sum(chain['lengths']) * .985) ** 2 - (stride * .5) ** 2))
        - (chain['rest'][0].z - hips.z) for chain in legs) - .009
    if not (.65 < hip_height < 1.18):
        raise RuntimeError('Reviewed leg dimensions cannot produce a grounded adult walk')
    action = bpy.data.actions.new(name)
    action.use_fake_user = True
    rig.animation_data_create()
    rig.animation_data.action = action
    rig.data.pose_position = 'POSE'
    scene.render.fps = fps
    scene.frame_start, scene.frame_end = 1, frames + 1
    previous = {}
    ordered = sorted(rig.pose.bones, key=lambda bone: len(bone.parent_recursive))
    foot_rows, maximum_error = [], 0.
    for frame in range(1, frames + 2):
        phase = (frame - 1) / frames
        wave = math.tau * phase
        # Lateral weight transfer is secondary to the actual planted leg cycle.
        displacement = Vector((0. if idle else .024 * math.sin(wave), 0,
                               hip_height - hips.z + (.002 * math.cos(wave) if idle else -.009 * math.cos(2 * wave))))
        hip_rotation = Quaternion((0, 1, 0), (0. if idle else .022) * math.sin(wave)) @ Quaternion((0, 0, 1), (0. if idle else .035) * math.sin(wave))
        posed = {}
        overrides = {}
        for bone in ordered:
            if bone.name in overrides:
                desired = overrides[bone.name]
            else:
                desired = helper.rest_under_parent(bone, posed)
                if bone.name == mapping['Hips']:
                    desired = Matrix.Translation(displacement) @ desired
                    desired = Matrix.Translation(desired.translation) @ hip_rotation.to_matrix().to_4x4() @ desired.to_quaternion().to_matrix().to_4x4()
                elif bone.name == mapping.get('Chest', mapping.get('Spine1')):
                    desired = desired @ Quaternion((0, 1, 0), (-.002 * math.cos(wave) if idle else -.014 * math.sin(wave))).to_matrix().to_4x4()
                chain = next((item for item in chains if item['names'][0] == bone.name), None)
                if chain:
                    start = desired.translation.copy()
                    local_phase = (phase + chain['phase']) % 1
                    if chain['kind'] == 'foot':
                        target = chain['rest'][2].copy()
                        # Front is Blender -Y. During stance the in-place sole
                        # moves +Y at exactly the runtime's forward speed.
                        if local_phase < duty:
                            progress = local_phase / duty
                            target.y += stride * (progress - .5)
                            lift = 0.
                            roll = 0. if idle else -.10 * max(0., 1 - progress / .16) + .14 * max(0., (progress - .79) / .21)
                        else:
                            progress = (local_phase - duty) / (1 - duty)
                            target.y += stride * (.5 - helper.smooth(progress))
                            lift = .043 * math.sin(math.pi * progress) ** 1.2
                            roll = .14 * (1 - progress) - .10 * progress
                        target.z = chain['clearance'] + lift
                        # Rotating around the sole contact instead of ankle
                        # keeps the heel/toe from sinking through the floor.
                        pivot_y = chain['heel'] if roll < 0 else chain['toe']
                        rotation = Quaternion((1, 0, 0), roll)
                        sole_offset = Vector((0, pivot_y, -chain['clearance']))
                        target += sole_offset - rotation @ sole_offset
                        end_rotation = rotation @ chain['rotations'][2]
                        pole = Vector((chain['sign'] * .09, -1, .02))
                        foot_rows.append(dict(frame=frame, side=chain['side'], stance=local_phase < duty,
                                              target=list(target), lift=lift))
                    else:
                        # Keep full arms outside this broad torso and use a
                        # restrained counter-swing rather than a rigid A pose.
                        target = Vector((start.x + chain['sign'] * .085,
                                         start.y + (.003 if idle else .075) * math.cos(wave + chain['phase'] * math.tau),
                                         start.z - sum(chain['lengths']) * .94))
                        pole = Vector((chain['sign'], -.23, -.12))
                        end_rotation = chain['rotations'][2]
                    bend, endpoint, error = helper.solve_ik(start, target, *chain['lengths'], pole)
                    maximum_error = max(maximum_error, error)
                    if chain['kind'] == 'hand':
                        # Preserve the generated relaxed wrist/finger relation
                        # while the A-pose arm lowers beside the heavy torso.
                        end_rotation = (chain['rest'][2] - chain['rest'][1]).rotation_difference(endpoint - bend) @ chain['rotations'][2]
                    points = [start, bend, endpoint]
                    for index in (0, 1):
                        rest_direction = chain['rest'][index + 1] - chain['rest'][index]
                        rotation = rest_direction.rotation_difference(points[index + 1] - points[index]) @ chain['rotations'][index]
                        overrides[chain['names'][index]] = helper.pose_matrix(points[index], rotation)
                    overrides[chain['names'][2]] = helper.pose_matrix(endpoint, end_rotation)
                    desired = overrides[bone.name]
            helper.store_pose(bone, desired, posed, previous, frame)
    for curve in action.fcurves:
        for key in curve.keyframe_points:
            key.interpolation = 'LINEAR'
    scene.frame_set(1)
    return dict(name=name, method='Authored measured-sole analytic IK; ordinary sampled bone keys',
        frameStart=1, frameEnd=frames + 1, fps=fps, durationSeconds=duration,
        travelSpeedMetresPerSecond=speed, worldTravelPerCycleMetres=speed * duration,
        stanceFraction=duty, doubleSupportFraction=2 * duty - 1,
        plantedStanceTravelMetres=stride, swingSoleLiftMetres=.043,
        measuredHipHeightMetres=hip_height, maximumIKTargetCorrectionMetres=maximum_error,
        rootMotion=False, measuredContacts=foot_rows,
        chainMeasurements=[{key: value for key, value in chain.items()
            if key in ('side', 'kind', 'names', 'lengths', 'clearance', 'sourceSoleVertices', 'heel', 'toe')} for chain in chains])
