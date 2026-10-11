"""Read-only, silent inspection of exported creature poses in an editable .blend.

Run with Blender --background --python this-file -- --blend path --output path.
It samples real evaluated skin rather than certifying only the skeleton. The
optional previews are diagnostic outputs; the opened source is never saved.
"""
import argparse
import json
import math
import sys
from pathlib import Path

import bpy
import numpy as np
from mathutils import Vector


def vertices(obj, evaluated=False):
    owner = obj.evaluated_get(bpy.context.evaluated_depsgraph_get()) if evaluated else obj
    mesh = owner.to_mesh() if evaluated else owner.data
    data = np.empty(len(mesh.vertices) * 3, dtype=np.float32)
    mesh.vertices.foreach_get('co', data)
    matrix = np.asarray(obj.matrix_world, dtype=np.float64)
    result = data.reshape(-1, 3) @ matrix[:3, :3].T + matrix[:3, 3]
    if evaluated:
        owner.to_mesh_clear()
    return result


def masks(obj, rig):
    semantic = {bone.name.rsplit(':', 1)[-1]: bone for bone in rig.data.bones}
    sets = {}
    for name in ('Head', 'Hips', 'Spine', 'LeftHand', 'RightHand', 'LeftFoot',
                 'RightFoot', 'LeftLeg', 'RightLeg'):
        if name not in semantic:
            continue
        bone = semantic[name]
        descendants = {bone.name} | {child.name for child in bone.children_recursive}
        if name in ('Hips', 'Spine'):
            descendants = {bone.name}
            if name == 'Spine':
                descendants |= {b.name for key, b in semantic.items() if key.startswith('Spine') or key == 'Neck'}
        group_ids = {group.index for group in obj.vertex_groups if group.name in descendants}
        selection = np.zeros(len(obj.data.vertices), dtype=bool)
        for vertex in obj.data.vertices:
            if sum(entry.weight for entry in vertex.groups if entry.group in group_ids) >= .6:
                selection[vertex.index] = True
        sets[name] = selection
    return sets


def floor_stat(values, floor):
    return {'minimum_z': float(values[:, 2].min()),
            'maximum_floor_penetration': max(0.0, float(floor - values[:, 2].min())),
            'vertices_under_2mm': int(np.count_nonzero(values[:, 2] < floor - .002)),
            'vertices_under_1cm': int(np.count_nonzero(values[:, 2] < floor - .01)),
            'bounds': [values.min(0).tolist(), values.max(0).tolist()]}


def prepare_render(scene, center, height, size, floor):
    scene.render.engine = 'BLENDER_EEVEE'
    scene.eevee.use_gtao = True
    scene.eevee.gtao_distance = height * .12
    scene.eevee.taa_render_samples = 32
    scene.render.resolution_x = scene.render.resolution_y = size
    scene.render.resolution_percentage = 100
    scene.view_settings.view_transform = 'Standard'
    if scene.world is None:
        scene.world = bpy.data.worlds.new('Pose audit world')
    scene.world.use_nodes = True
    scene.world.node_tree.nodes['Background'].inputs['Color'].default_value = (.10, .11, .13, 1)
    scene.world.node_tree.nodes['Background'].inputs['Strength'].default_value = .6
    for obj in list(scene.objects):
        if obj.type in {'CAMERA', 'LIGHT'}:
            bpy.data.objects.remove(obj, do_unlink=True)
    for name, offset, power in [('Key', (1.3, -2, 2), 130), ('Fill', (-1.4, -1, 1), 90),
                                 ('Rim', (0, 2, 2), 110)]:
        data = bpy.data.lights.new('Pose audit ' + name, 'AREA')
        obj = bpy.data.objects.new(data.name, data)
        scene.collection.objects.link(obj)
        obj.location = center + Vector(offset) * height
        obj.rotation_euler = (center - obj.location).to_track_quat('-Z', 'Y').to_euler()
        data.energy = power * height * height
        data.size = height * 2
    bpy.ops.mesh.primitive_plane_add(size=height * 6, location=(center.x, center.y, floor))
    ground = bpy.context.object
    ground.name = 'Pose audit floor'
    material = bpy.data.materials.new('Pose audit neutral ground')
    material.diffuse_color = (.22, .23, .26, 1)
    ground.data.materials.append(material)
    data = bpy.data.cameras.new('Pose audit camera')
    camera = bpy.data.objects.new(data.name, data)
    scene.collection.objects.link(camera)
    scene.camera = camera
    data.type = 'ORTHO'
    data.ortho_scale = height * 1.22
    return camera


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--blend', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--mesh', help='Explicit game mesh object; default visible rigged mesh')
    parser.add_argument('--actions', nargs='+', default=['patrol', 'chase'])
    parser.add_argument('--floor', type=float, default=0.0)
    parser.add_argument('--render', action='store_true')
    parser.add_argument('--render-phases', type=float, nargs='+', choices=[0.0, .25, .5, .75],
                        default=[0.0, .25, .5])
    parser.add_argument('--disable-normal-maps', action='store_true',
                        help='Diagnostic preview only; does not save or change source materials')
    parser.add_argument('--size', type=int, default=1000)
    args = parser.parse_args(sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else [])
    args.output.mkdir(parents=True, exist_ok=True)
    bpy.ops.wm.open_mainfile(filepath=str(args.blend))
    rigs = [obj for obj in bpy.context.scene.objects if obj.type == 'ARMATURE']
    if len(rigs) != 1:
        raise ValueError('Expected exactly one creature rig')
    rig = rigs[0]
    candidates = [obj for obj in bpy.context.scene.objects if obj.type == 'MESH'
                  and not obj.hide_render and any(mod.type == 'ARMATURE' for mod in obj.modifiers)]
    mesh = bpy.data.objects.get(args.mesh) if args.mesh else max(candidates, key=lambda obj: len(obj.data.vertices))
    if mesh is None:
        raise ValueError('Missing game skin mesh')
    if args.disable_normal_maps:
        for material in mesh.data.materials:
            if not material or not material.use_nodes:
                continue
            for node in material.node_tree.nodes:
                if node.type == 'BSDF_PRINCIPLED':
                    for link in list(node.inputs['Normal'].links):
                        material.node_tree.links.remove(link)
    original_visible = {obj for obj in bpy.context.scene.objects if obj.type == 'MESH' and not obj.hide_render}
    for obj in bpy.context.scene.objects:
        if obj.type == 'MESH':
            obj.hide_render = obj != mesh and (args.mesh is not None or obj not in original_visible)
    mesh.hide_set(False)
    scene = bpy.context.scene
    rig.data.pose_position = 'REST'
    bpy.context.view_layer.update()
    rest = vertices(mesh, evaluated=True)
    # Game mesh is already decimated. Its evaluated topology should stay fixed.
    edges = np.array([(edge.vertices[0], edge.vertices[1]) for edge in mesh.data.edges], dtype=np.int32)
    rest_lengths = np.linalg.norm(rest[edges[:, 0]] - rest[edges[:, 1]], axis=1)
    valid_edges = rest_lengths > max(np.ptp(rest[:, 2]) * 1e-5, 1e-7)
    partitions = masks(mesh, rig)
    report = {'source': str(args.blend), 'mesh': mesh.name, 'rig': rig.name,
              'vertices': len(rest), 'read_only': True, 'floor': args.floor,
              'coordinateSystem': 'Blender Z-up/front -Y', 'rest': floor_stat(rest, args.floor),
              'diagnostics': {'normalMapsDisabled': args.disable_normal_maps},
              'visibleAttachments': [obj.name for obj in original_visible if obj != mesh] if not args.mesh else [],
              'actions': {}, 'outputs': []}
    rig.data.pose_position = 'POSE'
    all_samples = []
    sample_cache = {}
    for action_name in args.actions:
        action = bpy.data.actions.get(action_name)
        if action is None:
            raise ValueError('Missing generated action: ' + action_name)
        rig.animation_data.action = action
        start, end = action.frame_range
        samples = []
        first = None
        for phase in (0.0, .25, .5, .75, 1.0):
            frame = start + phase * (end - start)
            whole = math.floor(frame)
            scene.frame_set(whole, subframe=frame - whole)
            bpy.context.view_layer.update()
            skin = vertices(mesh, evaluated=True)
            if skin.shape != rest.shape or not np.isfinite(skin).all():
                raise AssertionError('Nonfinite skin or changing evaluated vertex topology')
            if first is None:
                first = skin.copy()
            ratios = np.linalg.norm(skin[edges[:, 0]] - skin[edges[:, 1]], axis=1)[valid_edges] / rest_lengths[valid_edges]
            item = {'phase': phase, 'frame': frame, **floor_stat(skin, args.floor),
                    'edge_strain': {'maximum': float(ratios.max()),
                                    'percentile_99': float(np.percentile(ratios, 99)),
                                    'edges_over_2x': int(np.count_nonzero(ratios > 2)),
                                    'edges_over_5x': int(np.count_nonzero(ratios > 5))},
                    'partitions': {key: floor_stat(skin[mask], args.floor)
                                   for key, mask in partitions.items() if np.any(mask)}}
            semantic = {bone.name.rsplit(':', 1)[-1]: bone for bone in rig.pose.bones}
            head = semantic['Head']
            delta = rig.matrix_world @ head.matrix @ head.bone.matrix_local.inverted() @ rig.matrix_world.inverted()
            facing = delta.to_3x3() @ Vector((0, -1, 0))
            item['head_forward_world'] = list(facing)
            item['head_front_alignment'] = float(facing.dot(Vector((0, -1, 0))))
            item['joints'] = {key: list(rig.matrix_world @ semantic[key].head)
                              for key in ('Hips', 'Head', 'LeftHand', 'RightHand', 'LeftFoot', 'RightFoot')}
            samples.append(item)
            if phase < 1:
                all_samples.append(skin)
                sample_cache[(action_name, phase)] = frame
            else:
                item['loop_maximum_vertex_error'] = float(np.linalg.norm(skin - first, axis=1).max())
        report['actions'][action_name] = {'range': [start, end], 'samples': samples}
    combined = np.concatenate(all_samples)
    low, high = combined.min(0), combined.max(0)
    center = Vector((low + high) * .5)
    height = max(float(high[2] - low[2]), float(np.ptp(rest[:, 2])) * .75)
    if args.render:
        camera = prepare_render(scene, center, height, args.size, args.floor)
        for action_name in args.actions:
            rig.animation_data.action = bpy.data.actions[action_name]
            for phase in args.render_phases:
                frame = sample_cache[(action_name, phase)]
                scene.frame_set(math.floor(frame), subframe=frame - math.floor(frame))
                for view, offset in [('threequarter', (.9, -2, .7)), ('side', (-3, 0, .25))]:
                    camera.location = center + Vector(offset) * height
                    camera.rotation_euler = (center - camera.location).to_track_quat('-Z', 'Y').to_euler()
                    output = args.output / f'{action_name}-{view}-phase{round(phase*100):02d}.png'
                    scene.render.filepath = str(output)
                    bpy.ops.render.render(write_still=True)
                    report['outputs'].append(str(output))
    (args.output / 'pose-audit.json').write_text(json.dumps(report, indent=2), encoding='utf8')
    print('POSE_AUDIT_SAVED ' + str(args.output / 'pose-audit.json'), flush=True)


if __name__ == '__main__':
    main()
