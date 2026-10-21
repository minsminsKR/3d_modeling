"""Blender 4.0.2: finish preserved webapp cavern surfaces; no new base geometry.

blender -b --factory-startup --python-exit-code 1 --python <this-file> -- --only temple-gate
Originals and packed full-resolution PBR remain in each editable compressed blend.
"""
from pathlib import Path
import argparse
import hashlib
import json
import math
import struct
import sys

import bpy
import bmesh
import numpy as np
from mathutils import Matrix, Vector

SOURCE = Path(__file__).resolve().parent
PROJECT = SOURCE.parents[1]
OUTPUT = PROJECT / 'Assets/Resources/CorridorEntrance'
SETTINGS = {
    'temple-gate': dict(axis=0, metres=15.0, texture=4096, budgets=(100000,)),
    'cave-rock': dict(axis=2, metres=6.0, texture=2048, budgets=(30000, 8000)),
    'ritual-floor': dict(axis=0, metres=8.0, texture=4096, budgets=(18000, 8000),
                         square=True, max_height=.14, roughness_multiplier=.36),
    'guardian-statue': dict(axis=2, metres=2.8, texture=4096, budgets=(30000, 8000)),
    'buddhist-lantern': dict(axis=2, metres=1.0, texture=2048, budgets=(20000, 12000), paper_emission=True, rebake_atlas=True),
}


def sha(path):
    digest = hashlib.sha256()
    with path.open('rb') as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b''):
            digest.update(block)
    return digest.hexdigest()


def source_materials(path):
    with path.open('rb') as stream:
        magic, version, _ = struct.unpack('<4sII', stream.read(12))
        if magic != b'glTF' or version != 2:
            raise RuntimeError('Original must be a hydrated glTF 2 GLB: ' + str(path))
        while header := stream.read(8):
            size, kind = struct.unpack('<II', header)
            if kind == 0x4E4F534A:
                return json.loads(stream.read(size)).get('materials', [])
            stream.seek(size, 1)
    raise RuntimeError('Original GLB JSON is missing: ' + str(path))


def image_for(socket, seen=None):
    """Find the image and scalar channel behind a glTF Principled input."""
    seen = set() if seen is None else seen
    for link in socket.links:
        node = link.from_node
        identity = (node.as_pointer(), link.from_socket.identifier)
        if identity in seen:
            continue
        seen.add(identity)
        if node.type == 'TEX_IMAGE' and node.image:
            return node.image, None
        channel = {'Red': 0, 'Green': 1, 'Blue': 2, 'R': 0, 'G': 1, 'B': 2,
                   'Alpha': 3, 'A': 3}.get(link.from_socket.name)
        for upstream in node.inputs:
            result = image_for(upstream, seen)
            if result:
                image, upstream_channel = result
                return image, channel if channel is not None else upstream_channel
    return None


def sample(image, size):
    """Read a reduced copy, keeping the full original image packed and unchanged."""
    copy = image.copy()
    try:
        if tuple(copy.size) != tuple(size):
            copy.scale(*size)
        pixels = np.empty(size[0] * size[1] * 4, dtype=np.float32)
        copy.pixels.foreach_get(pixels)
        return pixels.reshape((-1, 4))
    finally:
        bpy.data.images.remove(copy)


def image_size(image, limit):
    factor = min(1.0, limit / max(image.size))
    return tuple(max(1, round(value * factor)) for value in image.size)


def save_pixels(pixels, size, destination, color_space):
    image = bpy.data.images.new('Entrance derived map', width=size[0], height=size[1], alpha=True)
    try:
        image.colorspace_settings.name = color_space
        image.pixels.foreach_set(pixels.ravel())
        image.file_format = 'PNG'
        image.filepath_raw = str(destination)
        image.save()
    finally:
        bpy.data.images.remove(image)


def save_image(image, destination, limit):
    size = image_size(image, limit)
    save_pixels(sample(image, size), size, destination, image.colorspace_settings.name)


def material_maps(old, name, destination, limit, source, roughness_multiplier=1):
    shader = next((node for node in old.node_tree.nodes if node.type == 'BSDF_PRINCIPLED'), None)
    if shader is None:
        raise RuntimeError('Generated surface lacks Principled PBR: ' + old.name)
    pbr = source.get('pbrMetallicRoughness', {})
    entry = dict(name=name, roughness=float(pbr.get('roughnessFactor', 1 if source else shader.inputs['Roughness'].default_value)),
                 metallic=float(pbr.get('metallicFactor', 1 if source else shader.inputs['Metallic'].default_value)),
                 baseColorFactor=pbr.get('baseColorFactor', [1, 1, 1, 1]),
                 normalScale=source.get('normalTexture', {}).get('scale', 1),
                 occlusionStrength=source.get('occlusionTexture', {}).get('strength', 1))
    if roughness_multiplier != 1:
        entry['sourceRoughnessFactor'] = entry['roughness']
        entry['wetFinishRoughnessMultiplier'] = roughness_multiplier
        entry['roughness'] *= roughness_multiplier
    albedo = image_for(shader.inputs['Base Color'])
    if not albedo:
        raise RuntimeError('Generated surface lacks original base colour: ' + old.name)
    entry['baseColor'] = name + '-baseColor.png'
    save_image(albedo[0], destination / entry['baseColor'], limit)
    roughness = image_for(shader.inputs['Roughness'])
    metallic = image_for(shader.inputs['Metallic'])
    if roughness or metallic:
        source = (roughness or metallic)[0]
        size = image_size(source, limit)
        packed = np.zeros((size[0] * size[1], 4), dtype=np.float32)
        packed[:, 0] = entry['metallic']
        packed[:, 3] = 1.0 - entry['roughness']
        if roughness:
            pixels = sample(roughness[0], size)
            packed[:, 3] = 1.0 - pixels[:, roughness[1] if roughness[1] is not None else 1] * entry['roughness']
        if metallic:
            pixels = sample(metallic[0], size)
            packed[:, 0] = pixels[:, metallic[1] if metallic[1] is not None else 2] * entry['metallic']
        np.clip(packed, 0, 1, out=packed)
        entry['metallicSmoothness'] = name + '-metallicSmoothness.png'
        save_pixels(packed, size, destination / entry['metallicSmoothness'], 'Non-Color')
    normal = image_for(shader.inputs['Normal'])
    if normal:
        entry['normal'] = name + '-normal.png'
        save_image(normal[0], destination / entry['normal'], limit)
    # Blender's glTF importer retains the optional occlusion texture on this
    # auxiliary material-output group. Do not invent AO when it was absent.
    occlusion = next((image_for(node.inputs['Occlusion']) for node in old.node_tree.nodes
                      if node.type == 'GROUP' and 'Occlusion' in node.inputs and node.inputs['Occlusion'].is_linked), None)
    if occlusion:
        size = image_size(occlusion[0], limit)
        pixels = sample(occlusion[0], size)
        value = pixels[:, occlusion[1] if occlusion[1] is not None else 0].copy()
        pixels[:, :3] = value[:, None]
        pixels[:, 3] = 1
        entry['occlusion'] = name + '-occlusion.png'
        save_pixels(pixels, size, destination / entry['occlusion'], 'Non-Color')
    return entry


def bounds(objects):
    points = [obj.matrix_world @ Vector(corner) for obj in objects for corner in obj.bound_box]
    return (Vector(tuple(min(point[axis] for point in points) for axis in range(3))),
            Vector(tuple(max(point[axis] for point in points) for axis in range(3))))


def triangles(obj):
    return sum(len(face.vertices) - 2 for face in obj.data.polygons)


def simplify(obj, budget):
    bpy.ops.object.select_all(action='DESELECT')
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    modifier = obj.modifiers.new('Source-derived triangle budget', 'DECIMATE')
    modifier.ratio = min(1.0, budget / max(1, triangles(obj)))
    bpy.ops.object.modifier_apply(modifier=modifier.name)
    modifier = obj.modifiers.new('Explicit game triangles', 'TRIANGULATE')
    bpy.ops.object.modifier_apply(modifier=modifier.name)
    for face in obj.data.polygons:
        face.use_smooth = True
    # Collapse can round above its requested face count on open generated
    # surfaces. Correct that bounded overshoot from this same derived mesh.
    for _ in range(2):
        count = triangles(obj)
        if count <= budget + 4:
            break
        modifier = obj.modifiers.new('Source-derived budget rounding correction', 'DECIMATE')
        modifier.ratio = max(1, budget - 12) / count
        bpy.ops.object.modifier_apply(modifier=modifier.name)
    if triangles(obj) > budget + 4:
        raise RuntimeError('Source-derived LOD exceeded its triangle budget: '
                           + obj.name + ' ' + str(triangles(obj)) + ' > ' + str(budget))


def prepare(key, yaw, keep_editor_session=False):
    settings = SETTINGS[key]
    source = SOURCE / key
    original = source / 'original.glb'
    if not original.is_file():
        raise RuntimeError('Preserved webapp original is required: ' + str(original))
    original_materials = source_materials(original)
    destination = OUTPUT / key
    destination.mkdir(parents=True, exist_ok=True)
    if keep_editor_session:
        # Only use in a disposable MCP editor; preserve its live addon/connection.
        for obj in list(bpy.context.scene.objects):
            bpy.data.objects.remove(obj,do_unlink=True)
        bpy.data.orphans_purge(do_recursive=True)
    else:
        bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.context.preferences.filepaths.save_version = 0
    bpy.ops.import_scene.gltf(filepath=str(original))
    originals = list(bpy.context.scene.objects)
    meshes = [obj for obj in originals if obj.type == 'MESH']
    if not meshes:
        raise RuntimeError('Generated original has no mesh: ' + key)
    original_faces = sum(len(obj.data.polygons) for obj in meshes)
    original_triangles = sum(triangles(obj) for obj in meshes)
    original_collection = bpy.data.collections.new('Complete unchanged webapp original')
    bpy.context.scene.collection.children.link(original_collection)
    for obj in originals:
        for collection in list(obj.users_collection):
            collection.objects.unlink(obj)
        original_collection.objects.link(obj)
    game_collection = bpy.data.collections.new('Editable source-derived game LODs')
    bpy.context.scene.collection.children.link(game_collection)
    clones = []
    rotation = Matrix.Rotation(math.radians(yaw if key == 'temple-gate' else 0), 4, 'Z')
    for obj in meshes:
        clone = obj.copy()
        clone.data = obj.data.copy()
        clone.parent = None
        clone.matrix_world = rotation @ obj.matrix_world
        game_collection.objects.link(clone)
        clones.append(clone)
    low, high = bounds(clones)
    factor = settings['metres'] / max(.00001, high[settings['axis']] - low[settings['axis']])
    centre = Vector(((low.x + high.x) * .5, (low.y + high.y) * .5, low.z))
    normalization = Matrix.Scale(factor, 4) @ Matrix.Translation(-centre)
    bpy.ops.object.select_all(action='DESELECT')
    for clone in clones:
        clone.matrix_world = normalization @ clone.matrix_world
        clone.select_set(True)
    bpy.context.view_layer.objects.active = clones[0]
    bpy.ops.object.join()
    game = bpy.context.object
    game.name = 'CE_' + key + '_LOD0'
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    # A repeatable, shallow walking tile is derived from the actual generated
    # rock surface. Its full unmodified source stays in the hidden collection.
    if settings.get('square'):
        low, high = bounds([game])
        game.scale.y = settings['metres'] / max(.00001, high.y - low.y)
    if 'max_height' in settings:
        low, high = bounds([game])
        game.scale.z = min(1, settings['max_height'] / max(.00001, high.z - low.z))
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    # Preserve per-loop atlas UVs while welding glTF's coincident seam vertices.
    bm = bmesh.new()
    bm.from_mesh(game.data)
    bmesh.ops.remove_doubles(bm, verts=list(bm.verts), dist=.00002)
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    bm.to_mesh(game.data)
    bm.free()
    if game.data.has_custom_normals:
        bpy.ops.mesh.customdata_custom_splitnormals_clear()
    surfaces, mapped = [], {}
    for slot in game.material_slots:
        old = slot.material
        if not old:
            raise RuntimeError('Original mesh has an empty material slot')
        if old.name not in mapped:
            new = old.copy()
            new.name = 'CE_' + key + '_' + str(len(surfaces))
            definition = next((item for item in original_materials if item.get('name') == old.name),
                              original_materials[0] if len(original_materials) == 1 else {})
            surfaces.append(material_maps(old, new.name, destination, settings['texture'], definition,
                                          settings.get('roughness_multiplier', 1)))
            if settings.get('paper_emission'):
                # Derived from the original atlas: pale hanji emits softly, dark
                # brush ink/wood remain dark. Never replace the source PBR.
                shader = next(node for node in old.node_tree.nodes if node.type == 'BSDF_PRINCIPLED')
                image = image_for(shader.inputs['Base Color'])[0]
                size = image_size(image, settings['texture'])
                pixels = sample(image, size)
                paper = np.clip((pixels[:,1]-.12)/.28,0,1) * np.clip((pixels[:,2]-.055)/.16,0,1)
                emission = np.ones_like(pixels)
                emission[:,:3] = paper[:,None]
                entry = surfaces[-1]
                entry['emission'] = new.name+'-paperEmission.png'
                entry['emissionColor'] = [.16,.072,.023,1]
                entry['emissionMaskRecipe'] = 'linear green smooth ramp .12-.40 multiplied by blue ramp .055-.215; original atlas ink retained dark'
                save_pixels(emission,size,destination/entry['emission'],'Non-Color')
                mask = bpy.data.images.load(str(destination/entry['emission']), check_existing=False)
                mask.colorspace_settings.name = 'Non-Color'
                derived = next(node for node in new.node_tree.nodes if node.type == 'BSDF_PRINCIPLED')
                node = new.node_tree.nodes.new('ShaderNodeTexImage');node.image = mask
                warm = new.node_tree.nodes.new('ShaderNodeMixRGB')
                warm.blend_type='MULTIPLY';warm.inputs[0].default_value=1
                warm.inputs[2].default_value=(.16,.072,.023,1)
                new.node_tree.links.new(node.outputs['Color'],warm.inputs[1])
                new.node_tree.links.new(warm.outputs[0],derived.inputs['Emission Color'])
                derived.inputs['Emission Strength'].default_value = 1
            wet = settings.get('roughness_multiplier', 1)
            if wet != 1:
                # Match the editable game material to the exported wet PBR;
                # leave the complete original material in its source collection.
                shader = next(node for node in new.node_tree.nodes if node.type == 'BSDF_PRINCIPLED')
                rough = shader.inputs['Roughness']
                if rough.is_linked:
                    upstream = rough.links[0].from_socket
                    multiply = new.node_tree.nodes.new('ShaderNodeMath')
                    multiply.name = 'Editable wet surface roughness'
                    multiply.operation = 'MULTIPLY'
                    multiply.inputs[1].default_value = wet
                    new.node_tree.links.new(upstream, multiply.inputs[0])
                    new.node_tree.links.new(multiply.outputs[0], rough)
                else:
                    rough.default_value *= wet
            mapped[old.name] = new
        slot.material = mapped[old.name]
    simplify(game, settings['budgets'][0])
    if settings.get('rebake_atlas'):
        import runpy
        atlas=runpy.run_path(str(SOURCE/'rebake_lantern_atlas.py'),run_name='lantern_atlas_recipe')
        if len(meshes)!=1:
            raise RuntimeError('Lantern atlas recipe requires one preserved original mesh')
        surfaces=[atlas['bake'](game,meshes[0],normalization@rotation,destination,
                                'CE_'+key+'_0',settings['texture'],save_pixels)]
    lods = [game]
    for index, budget in enumerate(settings['budgets'][1:], 1):
        clone = game.copy()
        clone.data = game.data.copy()
        clone.name = 'CE_' + key + '_LOD' + str(index)
        game_collection.objects.link(clone)
        simplify(clone, budget)
        lods.append(clone)
    reports = []
    for index, obj in enumerate(lods):
        bpy.ops.object.select_all(action='DESELECT')
        obj.select_set(True)
        bpy.context.view_layer.objects.active = obj
        filename = key + ('' if key == 'temple-gate' else '-lod' + str(index)) + '.fbx'
        path = OUTPUT / filename
        bpy.ops.export_scene.fbx(filepath=str(path), use_selection=True, object_types={'MESH'},
                                 axis_forward='-Z', axis_up='Y', bake_anim=False,
                                 add_leaf_bones=False, path_mode='RELATIVE', use_mesh_modifiers=True)
        low, high = bounds([obj])
        reports.append(dict(file=filename, targetTriangles=settings['budgets'][index],
                            triangles=triangles(obj), vertices=len(obj.data.vertices),
                            boundsMin=list(low), boundsMax=list(high), sha256=sha(path)))
    # The imported full-size images and original geometry are stored once. Game
    # LOD materials reference those same packed maps; PNGs are derived exports.
    bpy.ops.file.pack_all()
    text = bpy.data.texts.new('SOURCE - finish_entrance_assets.py')
    text.write(Path(__file__).read_text(encoding='utf-8'))
    if settings.get('rebake_atlas'):
        bpy.data.texts.new('SOURCE - rebake_lantern_atlas.py').write((SOURCE/'rebake_lantern_atlas.py').read_text(encoding='utf-8'))
    original_collection.hide_render = True
    original_collection.hide_viewport = True
    for obj in lods[1:]:
        obj.hide_render = True
        obj.hide_set(True)
    blend = source / (key + '.blend')
    bpy.ops.wm.save_as_mainfile(filepath=str(blend), compress=True)
    low, high = bounds([game])
    report = dict(key=key, tool='Blender 4.0.2', original='original.glb', originalSha256=sha(original),
                  editable=blend.relative_to(SOURCE).as_posix(), widthMetres=float(high.x - low.x),
                  heightMetres=float(high.z - low.z), depthMetres=float(high.y - low.y),
                  normalizedAxis='width' if settings['axis'] == 0 else 'height',
                  normalizedMetres=settings['metres'], gateYawDegrees=yaw if key == 'temple-gate' else 0,
                  finishing=settings,
                  front='Blender -Y / Unity +Z; confirm generated facing in Unity preview',
                  textureMaxSize=settings['texture'], originalFaces=original_faces,
                  originalTriangles=original_triangles, lods=reports, materials=surfaces,
                  textures='Full original PBR packed once in editable blend; game PNGs derived at the configured size')
    for path in (source / 'export.json', destination / 'asset.json'):
        path.write_text(json.dumps(report, indent=2) + '\n', encoding='utf-8', newline='\n')
    print('CORRIDOR_ENTRANCE_ASSET ' + json.dumps(report), flush=True)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--only', choices=SETTINGS)
    parser.add_argument('--gate-yaw', type=float, default=0,
                        help='Source gate rotation around Blender Z before width normalization')
    args = parser.parse_args(sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else [])
    for requested in ([args.only] if args.only else SETTINGS):
        prepare(requested, args.gate_yaw)
