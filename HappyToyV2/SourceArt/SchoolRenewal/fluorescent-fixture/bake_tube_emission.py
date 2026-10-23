"""Blender 4.0.2: bake emission only from the generated physical tube surfaces.

Run after finish_asset.py. tube-emission-regions.json records inspected game-space
boxes and an original-colour threshold. Original geometry/materials stay intact.
The generated source, rather than added primitive tubes, supplies every texel.
"""
from pathlib import Path
import json
import math
import runpy
import bpy
import numpy as np
from mathutils import Matrix, Vector

SOURCE = Path(__file__).resolve().parent
PROJECT = SOURCE.parents[2]
OUTPUT = PROJECT / 'Assets/Resources/SchoolRenewal/fluorescent-fixture'


def math_node(nodes, links, operation, left, right):
    node = nodes.new('ShaderNodeMath')
    node.operation = operation
    for socket, value in zip(node.inputs, (left, right)):
        if isinstance(value, (int, float)):
            socket.default_value = value
        else:
            links.new(value, socket)
    return node.outputs[0]


def run():
    if bpy.app.version[:3] != (4, 0, 2):
        raise RuntimeError('Use pinned Blender 4.0.2')
    bpy.ops.wm.open_mainfile(filepath=str(SOURCE / 'fluorescent-fixture.blend'))
    report = json.loads((OUTPUT / 'asset.json').read_text(encoding='utf-8'))
    config = json.loads((SOURCE / 'tube-emission-regions.json').read_text(encoding='utf-8'))
    # The actual reconstructed reflector sits behind the two front tube arcs.
    # Keep the emission region in front of that measured plane so an ivory
    # reflector cannot pass the pale-albedo gate and glow with the tubes.
    reflector = report.get('geometryFinishing', {}).get('separatingReflectorZ')
    if reflector is not None:
        for box in config['boxes']:
            box['max'][2] = min(box['max'][2], reflector - .003)
        config['measuredReflectorExclusionZ'] = reflector - .003
        (SOURCE / 'tube-emission-regions.json').write_text(json.dumps(config, indent=2) + '\n', encoding='utf-8')
    helper = runpy.run_path(str(PROJECT / 'SourceArt/CorridorEntrance/finish_entrance_assets.py'),
                            run_name='tube_emission_helpers')
    originals = [o for c in bpy.data.collections
                 if c.name.startswith('Complete unchanged designated-service original')
                 for o in c.objects if o.type == 'MESH']
    if len(originals) != 1:
        raise RuntimeError('Expected one preserved generated tube/housing source')
    original = originals[0]
    rotation = Matrix.Identity(4)
    settings = report['settings']
    for axis in ('X', 'Y', 'Z'):
        rotation = Matrix.Rotation(math.radians(float(settings.get('rotation_' + axis.lower(), 0))),
                                   4, axis) @ rotation
    source = original.copy()
    source.data = original.data.copy()
    source.parent = None
    source.matrix_world = rotation @ original.matrix_world
    bpy.context.scene.collection.objects.link(source)
    source.hide_set(False)
    source.hide_render = False
    lo, hi = helper['bounds']([source])
    axis = {'x': 0, 'y': 1, 'z': 2}[settings['axis']]
    scale = settings['metres'] / (hi[axis] - lo[axis])
    centre = Vector(((lo.x + hi.x) * .5, (lo.y + hi.y) * .5, lo.z))
    source.matrix_world = Matrix.Scale(scale, 4) @ Matrix.Translation(-centre) @ source.matrix_world
    material = original.data.materials[0].copy()
    source.data.materials.clear()
    source.data.materials.append(material)
    nodes, links = material.node_tree.nodes, material.node_tree.links
    shader = next(n for n in nodes if n.type == 'BSDF_PRINCIPLED')
    base = shader.inputs['Base Color']
    if not base.is_linked:
        raise RuntimeError('Physical tubes need the actual generated albedo')
    luma = nodes.new('ShaderNodeRGBToBW')
    links.new(base.links[0].from_socket, luma.inputs[0])
    colour_gate = math_node(nodes, links, 'GREATER_THAN', luma.outputs[0], config['minimumAlbedoLuma'])
    geometry = nodes.new('ShaderNodeNewGeometry')
    split = nodes.new('ShaderNodeSeparateXYZ')
    links.new(geometry.outputs['Position'], split.inputs[0])
    mask = 0.0
    for box in config['boxes']:
        value = 1.0
        for index, axis in enumerate(('X', 'Y', 'Z')):
            point = split.outputs[axis]
            low = math_node(nodes, links, 'GREATER_THAN', point, box['min'][index])
            high = math_node(nodes, links, 'LESS_THAN', point, box['max'][index])
            value = math_node(nodes, links, 'MULTIPLY', value,
                              math_node(nodes, links, 'MULTIPLY', low, high))
        mask = math_node(nodes, links, 'MAXIMUM', mask, value)
    mask = math_node(nodes, links, 'MULTIPLY', mask, colour_gate)
    output = next(n for n in nodes if n.type == 'OUTPUT_MATERIAL')
    emission = nodes.new('ShaderNodeEmission')
    links.new(mask, emission.inputs['Color'])
    links.new(emission.outputs[0], output.inputs['Surface'])
    scene = bpy.context.scene
    scene.render.engine = 'CYCLES'
    scene.cycles.device = 'CPU'
    scene.cycles.samples = 1
    scene.render.bake.use_selected_to_active = True
    scene.render.bake.cage_extrusion = .015
    scene.render.bake.max_ray_distance = .05
    scene.render.bake.margin = 8
    scene.render.bake.use_clear = True
    results = []
    for index, record in enumerate(report['materials']):
        game = bpy.data.objects.get('SR_fluorescent-fixture_LOD' + str(index))
        if game is None:
            raise RuntimeError('Source-derived tube LOD is missing')
        game.hide_render = False
        game.hide_set(False)
        size = record['textureMaxSize']
        filename = record['name'] + '-emission.png'
        image = bpy.data.images.new(filename, width=size, height=size, alpha=True)
        image.colorspace_settings.name = 'Non-Color'
        target_material = game.data.materials[0]
        node = target_material.node_tree.nodes.new('ShaderNodeTexImage')
        node.image = image
        target_material.node_tree.nodes.active = node
        bpy.ops.object.select_all(action='DESELECT')
        source.select_set(True)
        game.select_set(True)
        bpy.context.view_layer.objects.active = game
        bpy.ops.object.bake(type='EMIT')
        pixels = np.empty(size * size * 4, dtype=np.float32)
        image.pixels.foreach_get(pixels)
        lit = int(np.count_nonzero(pixels.reshape(-1, 4)[:, 0] > .25))
        if lit < 32:
            raise RuntimeError('Tube mask is empty; inspect generated geometry/boxes')
        image.file_format = 'PNG'
        image.filepath_raw = str(OUTPUT / filename)
        image.save()
        node.name = 'Generated physical tube emission mask'
        target = next(n for n in target_material.node_tree.nodes if n.type == 'BSDF_PRINCIPLED')
        target_material.node_tree.links.new(node.outputs['Color'], target.inputs['Emission Color'])
        target.inputs['Emission Strength'].default_value = 1.0
        record['emission'] = filename
        record['emissionColor'] = config.get('emissionColor', [1, .94, .78, 1])
        results.append(dict(lod=index, file=filename, litPixels=lit,
                            pixels=size * size, sha256=helper['sha'](OUTPUT / filename)))
        game.hide_render = index != 0
        game.hide_set(index != 0)
    bpy.data.objects.remove(source, do_unlink=True)
    report['tubeEmission'] = dict(method='Inspected physical generated-tube boxes AND original albedo luma; '
                                         'selected-source-to-game-UV bake, no replacement geometry',
                                 regions=config, atlases=results)
    report['front'] = ('Physical tube emitting face Blender -Z / Unity -Y after standard FBX; '
                       'long axis Blender X / Unity X, ceiling attachments Blender +Z / Unity +Y')
    for path in (SOURCE / 'export.json', OUTPUT / 'asset.json'):
        path.write_text(json.dumps(report, indent=2) + '\n', encoding='utf-8')
    bpy.data.texts.new('SOURCE - bake_tube_emission.py').write(Path(__file__).read_text(encoding='utf-8'))
    bpy.data.texts.new('SETTINGS - tube-emission-regions.json').write(json.dumps(config, indent=2))
    bpy.ops.file.pack_all()
    bpy.context.preferences.filepaths.save_version = 0
    bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE / 'fluorescent-fixture.blend'), compress=True)
    if helper['sha'](SOURCE / 'original.glb') != report['originalSha256']:
        raise RuntimeError('Preserved service original changed')
    print('SCHOOL_PHYSICAL_TUBE_EMISSION_PASS', json.dumps(results), flush=True)


if __name__ == '__main__':
    run()
