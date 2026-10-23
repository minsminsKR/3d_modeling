"""Optional quiet Blender presentation review; PNGs go to explicit Builds folder.

blender -b --threads 2 --python-exit-code 1 --python this.py -- --key school-desk
  --output HappyToyV2/Builds/SchoolRenewalDownloads/furniture/views
Does not save or modify the packed editable source.
"""
from pathlib import Path
import argparse
import math
import sys
import bpy
from mathutils import Vector

SOURCE = Path(__file__).resolve().parent
PROJECT = SOURCE.parents[1]


def review(key, output, original=False, views=None):
    if original:
        bpy.ops.wm.read_factory_settings(use_empty=True)
        bpy.ops.import_scene.gltf(filepath=str(SOURCE / key / 'original.glb'))
        meshes = [obj for obj in bpy.context.scene.objects if obj.type == 'MESH']
        if len(meshes) != 1:
            raise ValueError('Furniture review expects one source primitive')
        meshes[0].name = 'SR_' + key + '_LOD0'
    else:
        bpy.ops.wm.open_mainfile(filepath=str(SOURCE / key / (key + '.blend')))
    scene = bpy.context.scene
    for obj in list(scene.objects):
        if obj.type != 'MESH' or not obj.name.startswith('SR_' + key + '_LOD0'):
            obj.hide_render = True
    item = bpy.data.objects['SR_' + key + '_LOD0']
    corners = [item.matrix_world @ Vector(point) for point in item.bound_box]
    low = Vector(tuple(min(point[axis] for point in corners) for axis in range(3)))
    high = Vector(tuple(max(point[axis] for point in corners) for axis in range(3)))
    centre = (low + high) * .5
    diameter = max(high - low)
    scene.render.engine = 'BLENDER_EEVEE'
    scene.eevee.use_gtao = True
    scene.eevee.gtao_distance = diameter * .15
    scene.render.resolution_x = 768
    scene.render.resolution_y = 768
    scene.render.resolution_percentage = 100
    scene.view_settings.view_transform = 'Standard'
    scene.view_settings.look = 'Medium High Contrast'
    scene.view_settings.exposure = 0
    scene.world = bpy.data.worlds.new('Neutral review world')
    scene.world.use_nodes = True
    scene.world.node_tree.nodes['Background'].inputs[0].default_value = (.16, .16, .16, 1)
    scene.world.node_tree.nodes['Background'].inputs[1].default_value = .8
    for name, offset, power, size in (
            ('Large soft key', (2, 3, 4), 400, 3),
            ('Large soft fill', (-3, -1, 2), 180, 3)):
        data = bpy.data.lights.new(name, 'AREA')
        data.energy = power * diameter * diameter
        data.shape = 'DISK'
        data.size = size * diameter
        obj = bpy.data.objects.new(name, data)
        scene.collection.objects.link(obj)
        obj.location = centre + Vector(offset) * diameter
        obj.rotation_euler = (centre - obj.location).to_track_quat('-Z', 'Y').to_euler()
    data = bpy.data.cameras.new('Furniture orientation review')
    camera = bpy.data.objects.new('Furniture orientation review', data)
    scene.collection.objects.link(camera)
    scene.camera = camera
    data.type = 'ORTHO'
    data.ortho_scale = diameter * 1.5
    output = Path(output).resolve()
    builds = (PROJECT / 'Builds').resolve()
    if builds not in output.parents:
        raise ValueError('Optional review output must be a child of this project Builds directory')
    output.mkdir(parents=True, exist_ok=True)
    for view, position in (views or (('plus-x', (3, .3, 1.6)), ('minus-x', (-3, -.3, 1.6)),
                                    ('plus-y', (.3, 3, 1.6)), ('minus-y', (-.3, -3, 1.6)))):
        camera.location = centre + Vector(position) * diameter
        camera.rotation_euler = (centre - camera.location).to_track_quat('-Z', 'Y').to_euler()
        scene.render.filepath = str(output / (key + '-' + view + '.png'))
        bpy.ops.render.render(write_still=True)
    print('SCHOOL_RENEWAL_PRESENTATION_REVIEW_RENDERED', key)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--key', required=True)
    parser.add_argument('--output', required=True)
    parser.add_argument('--original', action='store_true')
    args = parser.parse_args(sys.argv[sys.argv.index('--') + 1:])
    review(args.key, args.output, args.original)
