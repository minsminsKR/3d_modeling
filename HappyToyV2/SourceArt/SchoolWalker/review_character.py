"""Measure real skinned sole contact and render the finished packed character.

No original or Blender source is changed. Optional review outputs stay in Builds.
"""
from pathlib import Path
import argparse
import json
import math
import sys

import bpy
import bmesh
import numpy as np
from mathutils import Vector

HERE = Path(__file__).resolve().parent
PROJECT = HERE.parents[1]


def posed_points(mesh, graph):
    evaluated = mesh.evaluated_get(graph)
    temporary = evaluated.to_mesh()
    try:
        values = np.empty(len(temporary.vertices) * 3, dtype=np.float32)
        temporary.vertices.foreach_get('co', values)
        matrix = np.asarray(evaluated.matrix_world)
        return values.reshape(-1, 3) @ matrix[:3, :3].T + matrix[:3, 3]
    finally:
        evaluated.to_mesh_clear()


def sole_ids(mesh, rig, bone_name):
    bone = rig.data.bones[bone_name]
    palette = {bone_name} | {child.name for child in bone.children_recursive}
    groups = {group.index for group in mesh.vertex_groups if group.name in palette}
    ids = [vertex.index for vertex in mesh.data.vertices
           if sum(item.weight for item in vertex.groups if item.group in groups) >= .50]
    if len(ids) < 8:
        raise RuntimeError('Actual game LOD lost its weighted shoe surface: ' + bone_name)
    minimum = min(mesh.data.vertices[index].co.z for index in ids)
    return np.array([index for index in ids if mesh.data.vertices[index].co.z < minimum + .014], dtype=np.int32)


def render_setup(scene):
    scene.render.engine = 'BLENDER_EEVEE'
    scene.eevee.taa_render_samples = 64
    scene.render.resolution_x = 1200
    scene.render.resolution_y = 1400
    scene.render.resolution_percentage = 100
    scene.view_settings.view_transform = 'AgX'
    scene.view_settings.look = 'None'
    world = bpy.data.worlds.new('Neutral actual finished review')
    world.use_nodes = True
    world.node_tree.nodes['Background'].inputs['Color'].default_value = (.14, .14, .14, 1)
    world.node_tree.nodes['Background'].inputs['Strength'].default_value = .25
    scene.world = world
    for name, position, energy in [('key', (3, -4, 4), 800), ('fill', (-4, -2, 3), 500), ('rim', (2, 4, 4), 650)]:
        data = bpy.data.lights.new(name, 'AREA')
        light = bpy.data.objects.new(name, data)
        scene.collection.objects.link(light)
        light.location = position
        light.rotation_euler = (Vector((0, 0, 1)) - light.location).to_track_quat('-Z', 'Y').to_euler()
        data.energy = energy
        data.size = 4
    bpy.ops.mesh.primitive_plane_add(size=20, location=(0, 0, -.002))
    floor = bpy.context.object
    floor.name = 'Read-only ground reference'
    material = bpy.data.materials.new('Neutral rough review floor')
    material.diffuse_color = (.10, .10, .10, 1)
    floor.data.materials.append(material)
    data = bpy.data.cameras.new('Actual finished character review')
    camera = bpy.data.objects.new('Actual finished character review', data)
    scene.collection.objects.link(camera)
    data.type = 'ORTHO'
    scene.camera = camera
    return camera


def review(output, render):
    if (PROJECT / 'Builds').resolve() not in output.resolve().parents:
        raise RuntimeError('Review output must be inside project Builds')
    export = json.loads((HERE / 'export.json').read_text(encoding='utf-8'))
    bpy.ops.wm.open_mainfile(filepath=str(HERE / 'SchoolWalker.blend'))
    scene = bpy.context.scene
    rig = bpy.data.objects['SchoolWalkerRig']
    rig.data.pose_position = 'POSE'
    graph = bpy.context.evaluated_depsgraph_get()
    output.mkdir(parents=True, exist_ok=True)
    results = []
    for level in range(3):
        mesh = bpy.data.objects['SW_LOD' + str(level)]
        topology = bmesh.new()
        topology.from_mesh(mesh.data)
        boundaries = sum(edge.is_boundary for edge in topology.edges)
        nonmanifold = sum(not edge.is_manifold for edge in topology.edges)
        topology.free()
        mesh.hide_set(False)
        for modifier in mesh.modifiers:
            if modifier.type == 'ARMATURE':
                modifier.show_viewport = True
        ids = {side: sole_ids(mesh, rig, export['bones'][side + 'Foot']) for side in ('Left', 'Right')}
        rest = np.array([tuple(vertex.co) for vertex in mesh.data.vertices])
        hand_mask = (np.abs(rest[:, 0]) > .34) & (rest[:, 2] > .60) & (rest[:, 2] < .90)
        edges = np.array([tuple(edge.vertices) for edge in mesh.data.edges], dtype=np.int32)
        edges = edges[hand_mask[edges[:, 0]] & hand_mask[edges[:, 1]]]
        rest_lengths = np.linalg.norm(rest[edges[:, 0]] - rest[edges[:, 1]], axis=1)
        edges, rest_lengths = edges[rest_lengths > .0015], rest_lengths[rest_lengths > .0015]
        maximum_hand_stretch = 1.
        rows = []
        for clip_name in ('walk', 'idle'):
            action = bpy.data.actions[clip_name]
            rig.animation_data.action = action
            frames = round(action.frame_range[1] - action.frame_range[0])
            first, last = None, None
            for step in range(frames + 1):
                scene.frame_set(round(action.frame_range[0]) + step)
                graph.update()
                points = posed_points(mesh, graph)
                if len(edges):
                    stretch = np.linalg.norm(points[edges[:, 0]] - points[edges[:, 1]], axis=1) / rest_lengths
                    maximum_hand_stretch = max(maximum_hand_stretch, float(stretch.max()))
                if first is None:
                    first = points.copy()
                last = points
                phase = step / frames
                for side, offset in (('Left', 0.), ('Right', .5)):
                    sole = points[ids[side]]
                    stance = clip_name == 'idle' or (phase + offset) % 1 < export['walk']['stanceFraction']
                    rows.append(dict(clip=clip_name, frame=step + 1, side=side, stance=stance,
                                     soleMinZ=float(sole[:, 2].min()), soleMaxZ=float(sole[:, 2].max()),
                                     shoeCentre=sole.mean(0).tolist()))
            endpoint = float(np.linalg.norm(first - last, axis=1).max())
            if endpoint > .0005:
                raise RuntimeError('Actual character clip fails the continuous loop endpoint: ' + str(endpoint))
        minimum = min(row['soleMinZ'] for row in rows)
        maximum_stance_gap = max(row['soleMinZ'] for row in rows if row['stance'])
        results.append(dict(lod=level, triangles=export['lods'][level]['triangles'],
            vertices=len(mesh.data.vertices), soleVertices={key: len(value) for key, value in ids.items()},
            boundaryEdges=boundaries, nonmanifoldEdges=nonmanifold,
            maximumHandEdgeStretchRatio=maximum_hand_stretch,
            maximumPenetrationMetres=max(0., -minimum), maximumStanceSoleGapMetres=maximum_stance_gap,
            clipEndpointContinuous=True, rows=rows))
    frames_rendered = []
    if render:
        camera = render_setup(scene)
        for level in range(3):
            for index in range(3):
                mesh = bpy.data.objects['SW_LOD' + str(index)]
                mesh.hide_render = index != level
                mesh.hide_set(index != level)
            rig.animation_data.action = bpy.data.actions['walk']
            total = export['walk']['frameEnd'] - export['walk']['frameStart']
            for phase in (0., .25, .5, .75):
                scene.frame_set(export['walk']['frameStart'] + round(total * phase))
                for view, direction in [('front', (0, -1, 0)), ('side', (1, 0, 0))]:
                    centre = Vector((0, 0, .86))
                    camera.location = centre + Vector(direction) * 5
                    camera.rotation_euler = (centre - camera.location).to_track_quat('-Z', 'Y').to_euler()
                    camera.data.ortho_scale = 1.95
                    name = 'lod' + str(level) + '-walk-' + str(round(phase * 100)) + '-' + view + '.png'
                    scene.render.filepath = str(output / name)
                    bpy.ops.render.render(write_still=True)
                    frames_rendered.append(name)
            rig.animation_data.action = bpy.data.actions['idle']
            scene.frame_set(1)
            centre = Vector((0, -.015, 1.51))
            camera.location = centre + Vector((0, -3, .03))
            camera.rotation_euler = (centre - camera.location).to_track_quat('-Z', 'Y').to_euler()
            camera.data.ortho_scale = .55
            name = 'lod' + str(level) + '-idle-face-flower.png'
            scene.render.filepath = str(output / name)
            bpy.ops.render.render(write_still=True)
            frames_rendered.append(name)
    accepted = all(entry['maximumPenetrationMetres'] < .008 and entry['maximumStanceSoleGapMetres'] < .018
                   and entry['boundaryEdges'] == 0 and entry['nonmanifoldEdges'] == 0 for entry in results)
    report = dict(status='PASS' if accepted else 'FAIL', scope='Actual packed Blender game skins, both ordinary clips; not Unity/frame-rate verification',
                  sourceUnchanged=True, source='SchoolWalker.blend', lods=results, rendered=frames_rendered)
    (output / 'contact-review.json').write_text(json.dumps(report, indent=2) + '\n', encoding='utf-8')
    print('SCHOOL_WALKER_CONTACT_REVIEW ' + json.dumps([{
        key: value for key, value in entry.items() if key != 'rows'} for entry in results]), flush=True)
    if not accepted:
        raise RuntimeError('Actual closed-skin / sole-contact bounds failed; retain report for repair')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output', default='SchoolWalkerReview/finished')
    parser.add_argument('--render', action='store_true')
    args = parser.parse_args(sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else [])
    review(PROJECT / 'Builds' / args.output, args.render)
