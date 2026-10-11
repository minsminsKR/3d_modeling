"""Rebuild the supplied Baeksugwi rig and bake both sprint clips in Blender 4.0.2.

Run: blender --background --factory-startup --python author_blender.py --
       [--preview-dir PATH] [--skip-preview] [--skip-export]

All required inputs/outputs default to this repository folder. No downloaded
cache paths, external Python packages, audio or game launch are required.
"""
import argparse
import hashlib
import json
import math
import sys
import time
from pathlib import Path

import bpy
import numpy as np
from mathutils import Matrix, Vector
from mathutils.kdtree import KDTree

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
from run_motion import apply_run_animation
from glb_io import read as read_glb, accessor as glb_accessor


def say(message):
    print('[Baeksugwi author] ' + message, flush=True)


def cv(point):
    return Vector((point[0], -point[2], point[1]))


def digest(path):
    result = hashlib.sha256()
    with Path(path).open('rb') as stream:
        for chunk in iter(lambda: stream.read(8 * 1024 * 1024), b''):
            result.update(chunk)
    return result.hexdigest()


def dump(path, report):
    Path(path).write_text(json.dumps(report, indent=2, ensure_ascii=False), encoding='utf-8')


def mesh_points(obj, evaluated=False):
    evaluated_obj = obj.evaluated_get(bpy.context.evaluated_depsgraph_get()) if evaluated else obj
    mesh = evaluated_obj.to_mesh() if evaluated else obj.data
    values = np.empty(len(mesh.vertices) * 3, dtype=np.float32)
    mesh.vertices.foreach_get('co', values)
    values = values.reshape((-1, 3))
    matrix = np.asarray(evaluated_obj.matrix_world, dtype=np.float32)
    values = values @ matrix[:3, :3].T + matrix[:3, 3]
    if evaluated:
        evaluated_obj.to_mesh_clear()
    return values


def mesh_snapshot(obj):
    mesh = obj.data
    mesh.calc_loop_triangles()
    normals = np.empty(len(mesh.loops) * 3, dtype=np.float32)
    mesh.loops.foreach_get('normal', normals)
    uv_hashes = {}
    for layer in mesh.uv_layers:
        values = np.empty(len(layer.data) * 2, dtype=np.float32)
        layer.data.foreach_get('uv', values)
        uv_hashes[layer.name] = hashlib.sha256(values.tobytes()).hexdigest()
    return {'vertices': len(mesh.vertices), 'triangles': len(mesh.loop_triangles),
            'loops': len(mesh.loops), 'uv': uv_hashes,
            'normal_sha256': hashlib.sha256(normals.tobytes()).hexdigest(),
            'materials': [m.name if m else None for m in mesh.materials]}


def quantize_weights(raw):
    values = np.asarray(raw, dtype=np.float64)
    sums = values.sum(axis=1)
    if np.min(sums) < 1e-6 or not np.isfinite(values).all() or np.min(values) < -1e-7:
        raise ValueError('Invalid/zero/negative input skin weights')
    normalized = values / sums[:, None]
    scaled = normalized * 1024
    quantized = np.floor(scaled).astype(np.uint16)
    remainder = 1024 - quantized.sum(axis=1).astype(np.int32)
    priority = np.argsort(-(scaled - quantized), axis=1, kind='stable')
    for order in range(4):
        rows = np.flatnonzero(remainder > order)
        quantized[rows, priority[rows, order]] += 1
    if not np.all(quantized.sum(axis=1) == 1024):
        raise AssertionError('Weight quantization lost normalization')
    max_error = float(np.max(np.abs(normalized - quantized / 1024)))
    if max_error > 0.001:
        raise AssertionError(f'Weight quantization exceeds 1e-3: {max_error}')
    return quantized, {'input_max_sum_error': float(np.max(np.abs(sums - 1))),
                       'quantization_max_error': max_error, 'quantized_max_sum_error': 0,
                       'weight_bins': 1024, 'zero_weight_vertices': 0}


def source_mapping(meshes, source_positions):
    # Never weld or reorder the imported mesh. Position mapping makes seam
    # duplicates receive identical surface-derived weights independently.
    tree = None
    result = {}
    for obj in meshes:
        points = mesh_points(obj)
        gltf_points = points[:, (0, 2, 1)].copy()
        gltf_points[:, 2] *= -1
        if len(gltf_points) == len(source_positions):
            direct_error = np.linalg.norm(gltf_points - source_positions, axis=1)
            if float(direct_error.max()) < 2e-6:
                result[obj.name] = (np.arange(len(points), dtype=np.int32),
                                    float(direct_error.max()), 'identity-order')
                continue
        if tree is None:
            say('Building position KD tree for UV seam-safe correspondence')
            tree = KDTree(len(source_positions))
            for i, point in enumerate(source_positions):
                tree.insert(point, i)
            tree.balance()
        mapping = np.empty(len(points), dtype=np.int32)
        max_error = 0.0
        for i, point in enumerate(gltf_points):
            _, source_id, error = tree.find(point)
            mapping[i] = source_id
            max_error = max(max_error, error)
        if max_error > 2e-6:
            raise AssertionError(f'{obj.name}: source coordinate mismatch {max_error}')
        result[obj.name] = (mapping, max_error, 'KD-correspondence')
    return result


def create_rig(plan):
    data = bpy.data.armatures.new('Baeksugwi_AnatomicalRig')
    rig = bpy.data.objects.new('Baeksugwi_Rig', data)
    bpy.context.collection.objects.link(rig)
    bpy.context.view_layer.objects.active = rig
    rig.select_set(True)
    bpy.ops.object.mode_set(mode='EDIT')
    created = {}
    for spec in plan['bones']:
        bone = data.edit_bones.new(spec['name'])
        bone.head, bone.tail = cv(spec['head']), cv(spec['tail'])
        if bone.length < 1e-5:
            raise ValueError(f'Zero length bone: {bone.name}')
        created[bone.name] = bone
    for spec in plan['bones']:
        bone = created[spec['name']]
        if spec['parent']:
            bone.parent = created[spec['parent']]
            if bone.name.endswith(('_Elbow', '_Wrist')):
                distance = (bone.head - bone.parent.tail).length
                if distance > 1e-6:
                    raise ValueError(f'Nonmatching limb chain tail/head: {bone.name} gap={distance}')
                bone.use_connect = True
    bpy.ops.object.mode_set(mode='OBJECT')
    rig.show_in_front = True
    rig.data.display_type = 'OCTAHEDRAL'
    rig['coordinate_system'] = 'Blender Z-up, front -Y; imported glTF (x,-z,y)'
    rig['animation_description'] = '18 anatomical arms: 12 alternating planted runners and 6 independent secondary arms'
    return rig


def bind_mesh(obj, rig, indices, joints, quantized, bone_names):
    bpy.ops.object.select_all(action='DESELECT')
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    determinant = obj.matrix_world.to_3x3().determinant()
    if determinant <= 0 or max(abs(s - 1) for s in obj.matrix_world.to_scale()) > 1e-5:
        raise ValueError(f'Unexpected source transform on {obj.name}: determinant={determinant}')
    # The armature and all points use identity object transforms after binding.
    obj.parent = None
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    for modifier in list(obj.modifiers):
        if modifier.type == 'ARMATURE':
            obj.modifiers.remove(modifier)
    obj.vertex_groups.clear()
    groups = [obj.vertex_groups.new(name=name) for name in bone_names]
    selected_joints = joints[indices]
    selected_weights = quantized[indices]
    vertices = np.repeat(np.arange(len(indices), dtype=np.int32), 4)
    flat_weights = selected_weights.reshape(-1)
    mask = flat_weights > 0
    keys = selected_joints.reshape(-1)[mask].astype(np.int32) * 1025 + flat_weights[mask]
    vertices = vertices[mask]
    order = np.argsort(keys, kind='stable')
    sorted_keys, sorted_vertices = keys[order], vertices[order]
    changes = np.r_[0, np.flatnonzero(sorted_keys[1:] != sorted_keys[:-1]) + 1, len(sorted_keys)]
    for start, end in zip(changes[:-1], changes[1:]):
        key = int(sorted_keys[start])
        bone, quantized_value = divmod(key, 1025)
        groups[bone].add(sorted_vertices[start:end].tolist(), quantized_value / 1024.0, 'REPLACE')
    obj.parent = rig
    obj.matrix_parent_inverse = Matrix.Identity(4)
    modifier = obj.modifiers.new('Baeksugwi_LinearSkin', 'ARMATURE')
    modifier.object = rig
    modifier.use_deform_preserve_volume = False
    return {'groups': len(groups), 'batch_calls': len(changes) - 1,
            'influences': int(mask.sum()), 'preserve_volume': False}


def prepare_limb_specs(plan, positions, joints, quantized, bone_names):
    result, measurements = [], {}
    for raw in plan['limbs']:
        spec = dict(raw)
        for key in ('wrist_position', 'tip', 'contact_point', 'anchor', 'pole',
                    'rest_shoulder', 'rest_elbow', 'rest_wrist'):
            if key in spec:
                spec[key] = list(cv(spec[key]))
        if 'contact_point' not in raw:
            wrist_index = bone_names.index(spec['wrist'])
            is_rigid_hand = np.any((joints == wrist_index) & (quantized >= 870), axis=1)
            wrist = np.asarray(raw['wrist_position'])
            # Restrict to the lower hand segment; do not sample another appendage.
            near = np.linalg.norm(positions - wrist, axis=1) < max(.08, raw.get('hand_radius', .03) * 2.5)
            hand_ids = np.flatnonzero(is_rigid_hand & near)
            if len(hand_ids) < 10:
                hand_ids = np.flatnonzero(np.any((joints == wrist_index) & (quantized >= 512), axis=1) & near)
            # For the secondary clearance, include the full rigid hand region;
            # long fingers can extend beyond the station fitting radius.
            if not spec.get('contact', True):
                whole_hand = np.flatnonzero(is_rigid_hand)
                if len(whole_hand):
                    hand_ids = whole_hand
            if len(hand_ids):
                minimum_y = float(np.min(positions[hand_ids, 1]))
                contact = list(wrist)
                contact[1] = minimum_y
                spec['contact_point'] = list(cv(contact))
                measurements[spec['wrist']] = {'source_min_y': minimum_y, 'samples': len(hand_ids),
                                              'wrist_clearance': float(wrist[1] - minimum_y),
                                              'method': 'rigid weighted hand underside minimum'}
            elif 'tip' in spec:
                spec['contact_point'] = spec['tip']
                measurements[spec['wrist']] = {'method': 'fallback planned hand tip'}
        result.append(spec)
    return result, measurements


def validate_rig(rig, meshes, motion, plan, source_positions, joints, quantized, names):
    details = {'clips': {}, 'pose_samples': []}
    floor = float(plan['floor'])
    full_weights = quantized.astype(np.float32) / 1024
    blender_points = source_positions[:, (0, 2, 1)].copy()
    blender_points[:, 1] *= -1
    for clip_name, info in motion['actions'].items():
        rig.animation_data.action = bpy.data.actions[info['action']]
        first_pose = None
        min_determinant, max_scale_error = float('inf'), 0.0
        for frame in range(1, info['frames'] + 1):
            bpy.context.scene.frame_set(frame)
            for pb in rig.pose.bones:
                if not all(math.isfinite(v) for row in pb.matrix for v in row):
                    raise AssertionError(f'Invalid bone matrix {clip_name}/{frame}/{pb.name}')
                min_determinant = min(min_determinant, pb.matrix.to_3x3().determinant())
                max_scale_error = max(max_scale_error, max(abs(s - 1) for s in pb.matrix.to_scale()))
            if first_pose is None:
                first_pose = {b.name: b.matrix.copy() for b in rig.pose.bones}
        delta = Vector(motion['forward_axis']) * info['distance_per_cycle']
        seam_error = max((b.matrix.translation - first_pose[b.name].translation - delta).length
                         for b in rig.pose.bones)
        rotation_seam = max(b.matrix.to_quaternion().rotation_difference(first_pose[b.name].to_quaternion()).angle
                            for b in rig.pose.bones)
        if seam_error > 2e-5 or min_determinant < .999 or max_scale_error > 1e-4:
            raise AssertionError(f'Bone loop/scale failed: {seam_error}/{min_determinant}/{max_scale_error}')
        details['clips'][clip_name] = {'translation_loop_error': seam_error,
                                      'rotation_loop_error_radians': rotation_seam,
                                      'min_matrix_determinant': min_determinant,
                                      'max_matrix_scale_error': max_scale_error}
    rig.animation_data.action = bpy.data.actions[motion['actions']['Run_InPlace']['action']]
    sample_frames = sorted(set([1] + [1 + round((motion['actions']['Run_InPlace']['frames'] - 1) * f)
                                     for f in (.25, .5, .75)]))
    for frame in sample_frames:
        bpy.context.scene.frame_set(frame)
        posed = np.zeros_like(blender_points)
        palette = [np.asarray((rig.pose.bones[name].matrix @ rig.data.bones[name].matrix_local.inverted()),
                              dtype=np.float32) for name in names]
        for slot in range(4):
            for bone_index, matrix in enumerate(palette):
                ids = np.flatnonzero((joints[:, slot] == bone_index) & (quantized[:, slot] > 0))
                if len(ids):
                    posed[ids] += (blender_points[ids] @ matrix[:3, :3].T + matrix[:3, 3]) * full_weights[ids, slot, None]
        per_hand = {}
        for limb in plan['limbs']:
            bone_index = names.index(limb['wrist'])
            ids = np.flatnonzero(np.any((joints == bone_index) & (quantized >= 870), axis=1))
            if len(ids):
                per_hand[limb['wrist']] = {'contact': limb.get('contact', True),
                                         'lowest_surface_z': float(posed[ids, 2].min()),
                                         'floor_intrusion': max(0.0, floor - float(posed[ids, 2].min()))}
        head_index = names.index('Head')
        head_ids = np.flatnonzero(np.any((joints == head_index) & (quantized >= 870), axis=1))
        mask_bounds = [posed[head_ids].min(axis=0).tolist(), posed[head_ids].max(axis=0).tolist()] if len(head_ids) else None
        details['pose_samples'].append({'frame': frame, 'bounds': [posed.min(axis=0).tolist(), posed.max(axis=0).tolist()],
                                        'floor': floor, 'lowest_surface_z': float(posed[:, 2].min()),
                                        'below_floor_vertices': int(np.sum(posed[:, 2] < floor - .002)),
                                        'max_floor_intrusion': max(0.0, floor - float(posed[:, 2].min())),
                                        'mask_bounds': mask_bounds, 'hands': per_hand})
    bpy.context.scene.frame_set(1)
    return details


def verify_export(path):
    document, binary = read_glb(path)
    info = {'bytes': path.stat().st_size, 'sha256': digest(path),
            'skins': len(document.get('skins', [])),
            'joints': [len(s['joints']) for s in document.get('skins', [])],
            'images': len(document.get('images', [])), 'animations': []}
    for animation in document.get('animations', []):
        sample_counts, durations = [], []
        targets = []
        for channel in animation['channels']:
            sampler = animation['samplers'][channel['sampler']]
            times = glb_accessor(document, binary, sampler['input']).reshape(-1)
            values = glb_accessor(document, binary, sampler['output'])
            if not np.isfinite(values).all():
                raise AssertionError('Nonfinite exported animation key')
            sample_counts.append(len(times))
            durations.append(float(times[-1] - times[0]))
            targets.append(channel['target']['path'])
        info['animations'].append({'name': animation.get('name'), 'channels': len(animation['channels']),
                                    'max_samples': max(sample_counts), 'duration': max(durations),
                                    'paths': sorted(set(targets))})
    names = {a['name'] for a in info['animations']}
    if names != {'Run_InPlace', 'Run_Forward'}:
        raise AssertionError(f'Expected exactly two named sprint clips, got {names}')
    if info['joints'] != [63]:
        raise AssertionError(f'Wrong exported skeleton size: {info["joints"]}')
    return info


def render_previews(rig, meshes, motion, floor, directory, samples=None):
    directory.mkdir(parents=True, exist_ok=True)
    scene = bpy.context.scene
    scene.render.engine = 'BLENDER_EEVEE'
    scene.eevee.use_gtao = True
    scene.eevee.gtao_distance, scene.eevee.gtao_factor = .15, 1.2
    scene.eevee.taa_render_samples = 32
    scene.render.resolution_x = scene.render.resolution_y = 1000
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = 'PNG'
    if scene.world is None:
        scene.world = bpy.data.worlds.new('Baeksugwi_QA_World')
    scene.world.color = (.12, .12, .12)
    scene.view_settings.view_transform = 'Standard'
    scene.view_settings.look = 'Medium High Contrast'
    target = Vector((0, 0, -.08))
    temporary_objects = []
    for name, position, energy, size in [('Key', (1.5, -2, 2), 100, 2),
                                         ('Fill', (-1.5, -1, 1), 80, 2),
                                         ('Rim', (0, 2, 2), 120, 2)]:
        data = bpy.data.lights.new(name, 'AREA')
        obj = bpy.data.objects.new(name, data)
        scene.collection.objects.link(obj)
        obj.location = position
        obj.rotation_euler = (target - obj.location).to_track_quat('-Z', 'Y').to_euler()
        data.energy, data.size = energy, size
        temporary_objects.append(obj)
    camera_data = bpy.data.cameras.new('QA_Camera')
    camera = bpy.data.objects.new('QA_Camera', camera_data)
    scene.collection.objects.link(camera)
    temporary_objects.append(camera)
    scene.camera = camera
    camera_data.type, camera_data.ortho_scale = 'ORTHO', 1.35
    # Floor gives a clear contact reference while remaining outside exported GLB.
    bpy.ops.mesh.primitive_plane_add(size=200, location=(0, 0, floor - .001))
    plane = bpy.context.object
    plane.name = 'QA_Floor'
    material = bpy.data.materials.new('QA_FloorMaterial')
    material.diffuse_color = (.04, .045, .05, 1)
    plane.data.materials.append(material)
    temporary_objects.append(plane)
    outputs = []
    last = motion['actions']['Run_InPlace']['frames'] - 1
    views = {'threequarter': (.9, -1.8, .65), 'side': (-2.5, 0, .10)}
    if samples is None:
        samples = [(frame, name) for frame in [1, 1 + round(last * .25), 1 + round(last * .5)]
                   for name in views]
    for frame, name in samples:
        scene.frame_set(frame)
        camera.location = views[name]
        camera.rotation_euler = (target - camera.location).to_track_quat('-Z', 'Y').to_euler()
        destination = directory / f'run-{name}-frame{frame:03d}.png'
        scene.render.filepath = str(destination)
        bpy.ops.render.render(write_still=True)
        outputs.append(str(destination))
    for obj in temporary_objects:
        bpy.data.objects.remove(obj, do_unlink=True)
    scene.camera = None
    scene.frame_set(1)
    return outputs


def main():
    arguments = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
    parser = argparse.ArgumentParser()
    parser.add_argument('--input', type=Path, default=HERE / 'source-original.glb')
    parser.add_argument('--plan', type=Path, default=HERE / 'skeleton-plan.json')
    parser.add_argument('--weights', type=Path, default=HERE / 'surface-weights.npz')
    parser.add_argument('--blend', type=Path, default=HERE / 'Baeksugwi_Run.blend')
    parser.add_argument('--glb', type=Path, default=HERE / 'Baeksugwi_Run.glb')
    parser.add_argument('--report', type=Path, default=HERE / 'authoring-report.json')
    parser.add_argument('--preview-dir', type=Path, default=HERE.parent.parent / 'Verification' / 'baeksugwi-studio-preview')
    parser.add_argument('--skip-preview', action='store_true')
    parser.add_argument('--skip-export', action='store_true')
    args = parser.parse_args(arguments)
    started = time.time()
    plan = json.loads(args.plan.read_text(encoding='utf-8'))
    with np.load(args.weights, allow_pickle=False) as arrays:
        positions = arrays['positions'].copy()
        joints = arrays['joints'].copy()
        raw_weights = arrays['weights'].copy()
        names = list(arrays['bone_names'].astype(str))
    expected = [bone['name'] for bone in plan['bones']]
    if names != expected:
        raise AssertionError('Weight palette order does not match skeleton plan')
    quantized, weight_report = quantize_weights(raw_weights)
    report = {'tool': bpy.app.version_string, 'input': args.input.name,
              'source_sha256': digest(args.input), 'weights': weight_report,
              'bones': len(names), 'limbs': len(plan['limbs'])}
    bpy.ops.wm.read_factory_settings(use_empty=True)
    say('Importing original textured geometry')
    bpy.ops.import_scene.gltf(filepath=str(args.input), merge_vertices=False)
    meshes = [obj for obj in bpy.data.objects if obj.type == 'MESH']
    if not meshes:
        raise ValueError('Original source has no mesh')
    before = {obj.name: mesh_snapshot(obj) for obj in meshes}
    mapping = source_mapping(meshes, positions)
    rig = create_rig(plan)
    report['meshes'] = {}
    for obj in meshes:
        source_ids, error, method = mapping[obj.name]
        say(f'Binding {obj.name}: {len(source_ids)} vertices, {method}, error {error:.2g}')
        bind_info = bind_mesh(obj, rig, source_ids, joints, quantized, names)
        after = mesh_snapshot(obj)
        for key in ('vertices', 'triangles', 'loops', 'uv', 'materials'):
            if before[obj.name][key] != after[key]:
                raise AssertionError(f'Source {key} changed during binding: {obj.name}')
        report['meshes'][obj.name] = {'before': before[obj.name], 'after': after,
                                    'correspondence': method, 'max_position_error': error,
                                    'binding': bind_info}
    specs, report['palm_measurements'] = prepare_limb_specs(plan, positions, joints, quantized, names)
    say('Baking two analytical IK sprint clips')
    motion = apply_run_animation(rig, specs, fps=60, cycle_seconds=plan['cycle_seconds'],
                                 ground_height=plan['floor'], body_height_offset=plan['body_height_offset'],
                                 stride=.12, report_path=str(HERE / 'run-motion-report.json'))
    report['motion'] = motion
    report['validation'] = validate_rig(rig, meshes, motion, plan, positions, joints, quantized, names)
    bpy.context.view_layer.objects.active = rig
    bpy.ops.object.select_all(action='DESELECT')
    rig.select_set(True)
    for mesh in meshes:
        mesh.select_set(True)
    bpy.ops.file.pack_all()
    bpy.context.preferences.filepaths.save_version = 0
    say('Saving editable Blender source')
    bpy.ops.wm.save_as_mainfile(filepath=str(args.blend), compress=True, check_existing=False)
    report['blend'] = {'file': args.blend.name, 'bytes': args.blend.stat().st_size}
    report['status'] = 'SOURCE_SAVED'
    dump(args.report, report)
    if not args.skip_export:
        say('Exporting lossless geometry/PBR and both baked clips')
        bpy.ops.export_scene.gltf(filepath=str(args.glb), export_format='GLB', use_selection=True,
                                  export_yup=True, export_animations=True, export_animation_mode='ACTIONS',
                                  export_anim_single_armature=True, export_frame_range=True,
                                  export_frame_step=1, export_force_sampling=True,
                                  export_anim_slide_to_zero=True, export_def_bones=True,
                                  export_optimize_animation_size=False, export_apply=False,
                                  export_texcoords=True, export_normals=True, export_materials='EXPORT',
                                  export_image_format='AUTO', export_cameras=False, export_lights=False,
                                  export_all_influences=False, export_skins=True)
        report['export'] = verify_export(args.glb)
        report['status'] = 'EXPORTED'
        dump(args.report, report)
    if not args.skip_preview:
        say('Rendering quiet 1000px side and three-quarter pose previews')
        report['previews'] = render_previews(rig, meshes, motion, plan['floor'], args.preview_dir)
    report['elapsed_seconds'] = time.time() - started
    report['status'] = 'COMPLETE'
    dump(args.report, report)
    say(f'COMPLETE: {args.report}; {report["elapsed_seconds"]:.1f} seconds')


if __name__ == '__main__':
    main()
