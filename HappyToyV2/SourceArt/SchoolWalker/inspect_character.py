"""Read-only source rig inventory and optional actual PBR cardinal views.

The app source is never saved or modified. All outputs stay under project Builds.
"""
from pathlib import Path
import argparse
import hashlib
import json
import sys

import bpy
from mathutils import Vector

HERE = Path(__file__).resolve().parent
PROJECT = HERE.parents[1]


def digest(source):
    checksum = hashlib.sha256()
    with source.open('rb') as stream:
        for block in iter(lambda: stream.read(8 * 1024 * 1024), b''):
            checksum.update(block)
    return checksum.hexdigest()


def inspect(source, output, render):
    if (PROJECT / 'Builds').resolve() not in output.resolve().parents:
        raise RuntimeError('Review output must stay inside project Builds')
    if not source.is_file():
        raise RuntimeError('Collected designated source absent: ' + str(source))
    expected = digest(source)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=str(source))
    all_meshes = [obj for obj in bpy.context.scene.objects if obj.type == 'MESH']
    meshes = [obj for obj in all_meshes if len(obj.vertex_groups) and len(obj.material_slots)]
    for obj in all_meshes:
        if obj not in meshes:
            obj.hide_render = True
            obj.hide_set(True)
    rigs = [obj for obj in bpy.context.scene.objects if obj.type == 'ARMATURE']
    for rig in rigs:
        rig.data.pose_position = 'REST'
    points = [obj.matrix_world @ vertex.co for obj in meshes for vertex in obj.data.vertices]
    low = Vector(tuple(min(point[axis] for point in points) for axis in range(3)))
    high = Vector(tuple(max(point[axis] for point in points) for axis in range(3)))
    report = dict(source=source.name, sha256=expected, sourceUnchanged=True,
        coordinates='Actual Blender import world Z-up; facing unmodified',
        boundsMin=list(low), boundsMax=list(high),
        excludedRigDisplayHelpers=[obj.name for obj in all_meshes if obj not in meshes],
        meshes=[dict(name=obj.name, vertices=len(obj.data.vertices), faces=len(obj.data.polygons),
                     materialCount=len(obj.material_slots), weightedVertices=sum(bool(vertex.groups) for vertex in obj.data.vertices)) for obj in meshes],
        rigs=[dict(name=rig.name, bones=[dict(name=bone.name, parent=bone.parent.name if bone.parent else None,
            head=list(rig.matrix_world @ bone.head_local), tail=list(rig.matrix_world @ bone.tail_local),
            length=bone.length, children=[child.name for child in bone.children]) for bone in rig.data.bones]) for rig in rigs])
    output.mkdir(parents=True, exist_ok=True)
    (output / 'source-inspection.json').write_text(json.dumps(report, indent=2) + '\n', encoding='utf-8')
    if render:
        scene = bpy.context.scene
        scene.render.engine = 'BLENDER_EEVEE'
        scene.render.resolution_x = 1200
        scene.render.resolution_y = 1400
        scene.render.resolution_percentage = 100
        scene.view_settings.view_transform = 'Standard'
        scene.view_settings.look = 'Medium High Contrast'
        if scene.world is None:
            scene.world = bpy.data.worlds.new('Read-only neutral review world')
        scene.world.color = (.08, .08, .08)
        centre = (low + high) * .5
        height = high.z - low.z
        for name, offset, energy in [('key', (2, -3, 4), 500), ('fill', (-3, -1, 2), 300), ('rim', (0, 3, 3), 350)]:
            data = bpy.data.lights.new(name, 'AREA')
            light = bpy.data.objects.new(name, data)
            scene.collection.objects.link(light)
            light.location = centre + Vector(offset) * height
            light.rotation_euler = (centre - light.location).to_track_quat('-Z', 'Y').to_euler()
            data.energy = energy * height * height
            data.shape = 'DISK'
            data.size = height * 3
        camera_data = bpy.data.cameras.new('Read-only source review')
        camera = bpy.data.objects.new('Read-only source review', camera_data)
        scene.collection.objects.link(camera)
        scene.camera = camera
        camera_data.type = 'ORTHO'
        camera_data.ortho_scale = height * 1.12
        for name, direction in [('minus-y', (0, -1, 0)), ('plus-y', (0, 1, 0)),
                                ('minus-x', (-1, 0, 0)), ('plus-x', (1, 0, 0))]:
            camera.location = centre + Vector(direction) * height * 4
            camera.rotation_euler = (centre - camera.location).to_track_quat('-Z', 'Y').to_euler()
            scene.render.filepath = str(output / (name + '.png'))
            bpy.ops.render.render(write_still=True)
    (output / 'source-inspection.json').write_text(json.dumps(report, indent=2) + '\n', encoding='utf-8')
    if digest(source) != expected:
        raise RuntimeError('Read-only inspection altered original source')
    print('SCHOOL_WALKER_SOURCE_INSPECTION_PASS ' + json.dumps(report), flush=True)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--input', default='rigged-original.glb')
    parser.add_argument('--output', default='SchoolWalkerReview/source')
    parser.add_argument('--render', action='store_true')
    args = parser.parse_args(sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else [])
    inspect(HERE / args.input, PROJECT / 'Builds' / args.output, args.render)
