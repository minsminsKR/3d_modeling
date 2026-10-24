"""Read-only Blender 4.0.2 inspection of the actual designated-service GLB.

Run from the repository using its pinned Blender, with no GUI or Cycles GPU:
  blender -b --factory-startup --threads 2 --python-exit-code 1 --python
  HappyToyV2/SourceArt/SchoolRenewal/inspect_generated_prop.py -- --key radiator
  --render
  # Inspect one actual finished game LOD from its packed editable source:
  blender ... --python this.py -- --key radiator --finished --lod 1 --render

The default writes only a small geometry/material/bounds report. --render also
writes four exact cardinal orthographic PNGs with neutral lighting and unchanged
source PBR. All outputs stay under Builds; the GLB, its materials and editable
sources are never saved or rewritten. View names specify the camera position in
Blender coordinates, not an assumed model front. Determine the actual front,
mounting bracket, gaps and connections by inspecting those images.
"""
from pathlib import Path
import argparse
import hashlib
import json
import sys

import bpy
import numpy as np
from mathutils import Vector


SOURCE = Path(__file__).resolve().parent
PROJECT = SOURCE.parents[1]


def sha256(path):
    digest = hashlib.sha256()
    with path.open('rb') as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b''):
            digest.update(block)
    return digest.hexdigest()


def vertex_bounds(obj):
    """Use actual transformed vertices, not inflated rotated bound-box corners."""
    vertices = np.empty(len(obj.data.vertices) * 3, dtype=np.float64)
    obj.data.vertices.foreach_get('co', vertices)
    vertices = vertices.reshape(-1, 3)
    matrix = np.asarray(obj.matrix_world, dtype=np.float64)
    vertices = vertices @ matrix[:3, :3].T + matrix[:3, 3]
    if not vertices.size or not np.isfinite(vertices).all():
        raise ValueError('Source mesh has empty or nonfinite geometry: ' + obj.name)
    low, high = vertices.min(axis=0), vertices.max(axis=0)
    return low.tolist(), high.tolist()


def material_report(material):
    if material is None:
        return {'name': None, 'missing': True}
    images = []
    if material.use_nodes:
        for node in material.node_tree.nodes:
            if node.type != 'TEX_IMAGE' or node.image is None:
                continue
            image = node.image
            images.append(dict(node=node.name, image=image.name,
                               width=image.size[0], height=image.size[1],
                               colorSpace=image.colorspace_settings.name,
                               packed=bool(image.packed_file)))
    return dict(name=material.name, useNodes=material.use_nodes,
                blendMethod=material.blend_method,
                backfaceCulling=material.use_backface_culling, images=images)


def render_views(scene, meshes, low, high, output, image_prefix, resolution):
    centre = Vector(tuple((a + b) * .5 for a, b in zip(low, high)))
    extent = max(b - a for a, b in zip(low, high))
    if extent <= 0:
        raise ValueError('Degenerate source bounds')
    for obj in scene.objects:
        obj.hide_render = obj not in meshes
    scene.render.engine = 'BLENDER_EEVEE'
    scene.render.threads_mode = 'FIXED'
    scene.render.threads = 2
    scene.eevee.taa_render_samples = 32
    scene.eevee.use_gtao = True
    scene.eevee.gtao_distance = .10 * extent
    scene.render.resolution_x = scene.render.resolution_y = resolution
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = 'PNG'
    scene.render.image_settings.color_mode = 'RGBA'
    scene.render.film_transparent = False
    scene.view_settings.view_transform = 'Standard'
    scene.view_settings.look = 'Medium High Contrast'
    scene.view_settings.exposure = 0
    scene.view_settings.gamma = 1
    world = bpy.data.worlds.new('Neutral generated-source review world')
    world.use_nodes = True
    world.node_tree.nodes['Background'].inputs['Color'].default_value = (.16, .16, .16, 1)
    world.node_tree.nodes['Background'].inputs['Strength'].default_value = .8
    scene.world = world
    # Broad white lights on both sides keep the rear bracket and narrow radiator
    # gaps readable without changing the source textures, roughness or normals.
    for name, offset, power in (
            ('Neutral top key', (2, 3, 4), 450),
            ('Neutral rear fill', (-3, -2, 2), 300),
            ('Neutral lateral fill', (3, -2, 1), 160)):
        light_data = bpy.data.lights.new(name, 'AREA')
        light_data.energy = power * extent * extent
        light_data.color = (1, 1, 1)
        light_data.shape = 'DISK'
        light_data.size = 3 * extent
        light = bpy.data.objects.new(name, light_data)
        scene.collection.objects.link(light)
        light.location = centre + Vector(offset) * extent
        light.rotation_euler = (centre - light.location).to_track_quat('-Z', 'Y').to_euler()
    camera_data = bpy.data.cameras.new('Cardinal source orthographic camera')
    camera_data.type = 'ORTHO'
    camera_data.ortho_scale = extent * 1.22
    camera_data.clip_start = max(.0001, extent * .001)
    camera_data.clip_end = extent * 20
    camera = bpy.data.objects.new(camera_data.name, camera_data)
    scene.collection.objects.link(camera)
    scene.camera = camera
    views = []
    for name, direction in (
            ('plus-y', (0, 1, 0)), ('minus-y', (0, -1, 0)),
            ('plus-x', (1, 0, 0)), ('minus-x', (-1, 0, 0))):
        camera.location = centre + Vector(direction) * extent * 3
        camera.rotation_euler = (centre - camera.location).to_track_quat('-Z', 'Y').to_euler()
        filename = image_prefix + '-' + name + '.png'
        scene.render.filepath = str(output / filename)
        bpy.ops.render.render(write_still=True)
        views.append(dict(file=filename, cameraFromBlenderAxis=name,
                          cameraPosition=list(camera.location), target=list(centre),
                          projection='orthographic', orthographicScale=camera_data.ortho_scale))
    return views


def uv_report(mesh):
    layers = []
    for layer in mesh.uv_layers:
        values = np.empty(len(layer.data) * 2, dtype=np.float64)
        layer.data.foreach_get('uv', values)
        values = values.reshape(-1, 2)
        finite = bool(np.isfinite(values).all())
        layers.append(dict(name=layer.name, loopCount=len(values), finite=finite,
                           minimum=values.min(axis=0).tolist() if values.size and finite else None,
                           maximum=values.max(axis=0).tolist() if values.size and finite else None))
    return layers


def inspect(key, output=None, render=False, resolution=1024, finished=False, lod=0):
    if bpy.app.version[:3] != (4, 0, 2):
        raise RuntimeError('Use the pinned Blender 4.0.2')
    original = SOURCE / key / 'original.glb'
    if not original.is_file():
        raise FileNotFoundError('Await the preserved designated-service original: ' + str(original))
    input_path = SOURCE / key / (key + '.blend') if finished else original
    if not input_path.is_file():
        raise FileNotFoundError('Await the completed packed editable source: ' + str(input_path))
    stage = 'finished-' + key + '-lod' + str(lod) if finished else 'source-' + key
    image_prefix = key + '-finished-lod' + str(lod) if finished else key + '-source'
    output = Path(output).resolve() if output else (PROJECT / 'Builds/SchoolWayfindingReview' / stage).resolve()
    if (PROJECT / 'Builds').resolve() not in output.parents:
        raise ValueError('Inspection outputs must be a child of this project Builds directory')
    before = sha256(original)
    input_before = sha256(input_path) if finished else before
    if finished:
        bpy.ops.wm.open_mainfile(filepath=str(input_path), load_ui=False, use_scripts=False)
    else:
        bpy.ops.wm.read_factory_settings(use_empty=True)
        bpy.ops.import_scene.gltf(filepath=str(original))
    bpy.context.view_layer.update()
    scene = bpy.context.scene
    if finished:
        name = 'SR_' + key + '_LOD' + str(lod)
        mesh = scene.objects.get(name)
        if not mesh or mesh.type != 'MESH':
            raise ValueError('Exact finished game mesh missing: ' + name)
        mesh.hide_set(False)
        mesh.hide_render = False
        meshes = [mesh]
        inspected_objects = meshes
    else:
        meshes = [obj for obj in scene.objects if obj.type == 'MESH']
        inspected_objects = list(scene.objects)
    if not meshes:
        raise ValueError('The preserved GLB has no mesh geometry')
    objects = []
    for obj in inspected_objects:
        entry = dict(name=obj.name, type=obj.type,
                     matrixWorld=[list(row) for row in obj.matrix_world])
        if obj.type == 'MESH':
            obj.data.calc_loop_triangles()
            low, high = vertex_bounds(obj)
            entry.update(vertices=len(obj.data.vertices), polygons=len(obj.data.polygons),
                         triangles=len(obj.data.loop_triangles),
                         uvLayers=uv_report(obj.data),
                         boundsMinBlenderWorld=low, boundsMaxBlenderWorld=high,
                         sizeBlenderXYZ=[b - a for a, b in zip(low, high)],
                         materials=[material_report(slot.material) for slot in obj.material_slots])
        objects.append(entry)
    geometry = [entry for entry in objects if entry['type'] == 'MESH']
    low = [min(entry['boundsMinBlenderWorld'][axis] for entry in geometry) for axis in range(3)]
    high = [max(entry['boundsMaxBlenderWorld'][axis] for entry in geometry) for axis in range(3)]
    report = dict(key=key, source=(Path('SourceArt/SchoolRenewal') / key / 'original.glb').as_posix(),
                  sourceSha256=before, inputFile=input_path.relative_to(PROJECT).as_posix(),
                  inputSha256=input_before, inputKind='packed editable Blender source' if finished else 'unchanged original GLB',
                  finished=finished, selectedLod=lod if finished else None, blender=bpy.app.version_string,
                  coordinateSpace=('Blender world of the exact finished SR mesh; +Z is up; saved finishing transforms retained' if finished else
                                   'Blender world after the standard glTF import; +Z is up; no normalization or source rotation applied'),
                  axes=dict(x='Blender X horizontal', y='Blender Y horizontal/depth', z='Blender Z vertical'),
                  front='Not assumed; inspect the labelled cardinal views before choosing finishing rotation',
                  boundsMinBlenderWorld=low, boundsMaxBlenderWorld=high,
                  sizeBlenderXYZ=[b - a for a, b in zip(low, high)],
                  totalTriangles=sum(entry['triangles'] for entry in geometry), objects=objects,
                  renderEngine='EEVEE, 32 samples, 2 threads; no Cycles render or model inference' if render else None,
                  reviewOnly=True, sourceModified=False, views=[])
    output.mkdir(parents=True, exist_ok=True)
    if render:
        report['views'] = render_views(scene, meshes, low, high, output, image_prefix, resolution)
    if sha256(original) != before:
        raise RuntimeError('Preserved source GLB changed during read-only inspection')
    if finished and sha256(input_path) != input_before:
        raise RuntimeError('Packed editable source changed during read-only inspection')
    path = output / (image_prefix + '-inspection.json')
    path.write_text(json.dumps(report, indent=2) + '\n', encoding='utf-8')
    print('SCHOOL_GENERATED_PROP_INSPECTION_PASS ' + json.dumps(dict(
        key=key, finished=finished, lod=lod if finished else None,
        totalTriangles=report['totalTriangles'], sizeBlenderXYZ=report['sizeBlenderXYZ'],
        report=path.relative_to(PROJECT).as_posix(), rendered=len(report['views']), sourceModified=False)), flush=True)
    return report


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--key', choices=('radiator', 'room-sign'), required=True)
    parser.add_argument('--output', help='Optional output directory, strictly inside HappyToyV2/Builds')
    parser.add_argument('--render', action='store_true', help='Also render four neutral exact cardinal views')
    parser.add_argument('--finished', action='store_true', help='Inspect only the exact selected SR mesh from the packed editable .blend')
    parser.add_argument('--lod', type=int, choices=(0, 1), default=0, help='Finished game LOD to inspect (default 0)')
    parser.add_argument('--resolution', type=int, default=1024)
    args = parser.parse_args(sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else [])
    if not 128 <= args.resolution <= 2048:
        parser.error('--resolution must be between 128 and 2048')
    inspect(args.key, args.output, args.render, args.resolution, args.finished, args.lod)
