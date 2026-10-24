"""Blender 4.0.2: derive school renewal game LODs from unchanged service GLB.

Run from the repository using the pinned Blender:
  blender -b --factory-startup --threads 2 --python-exit-code 1 --python
  HappyToyV2/SourceArt/SchoolRenewal/finish_asset.py -- --key school-desk
  --axis z --metres .73 --budgets 12000,4000 --texture 2048

Full generated PBR and geometry remain packed in the hidden original collection.
Game LODs receive coherent UVs, source colour/ORM and high-to-low tangent normals.
"""
from pathlib import Path
import argparse
import json
import math
import runpy
import sys

import bpy
import bmesh
import numpy as np
from mathutils import Matrix, Vector

SOURCE = Path(__file__).resolve().parent
PROJECT = SOURCE.parents[1]
OUTPUT = PROJECT / 'Assets/Resources/SchoolRenewal'


def remove_tiny_islands(game, relative_limit):
    """Remove generated floating specks only from derived LODs, never the source.

    Collapse decimation cannot simplify hundreds of isolated triangle islands.
    Connected structural pieces stay intact; tiny scraps become baked detail.
    """
    extent = max((max(v.co[a] for v in game.data.vertices) -
                  min(v.co[a] for v in game.data.vertices)) for a in range(3))
    threshold = max(.0001, extent * relative_limit)
    bm = bmesh.new()
    bm.from_mesh(game.data)
    visited, discard = set(), []
    removed_components = 0
    for seed in bm.verts:
        if seed in visited:
            continue
        pending, component = [seed], []
        visited.add(seed)
        while pending:
            vertex = pending.pop()
            component.append(vertex)
            for edge in vertex.link_edges:
                other = edge.other_vert(vertex)
                if other not in visited:
                    visited.add(other)
                    pending.append(other)
        # Dense connected structural meshes cannot be tiny floating scraps.
        if len(component) > 512:
            continue
        diameter = max(max(v.co[a] for v in component) - min(v.co[a] for v in component)
                       for a in range(3))
        if diameter < threshold:
            discard.extend(component)
            removed_components += 1
    removed_vertices = len(discard)
    if discard:
        bmesh.ops.delete(bm, geom=discard, context='VERTS')
        bm.to_mesh(game.data)
        game.data.update()
    bm.free()
    return dict(relativeLimit=relative_limit, maxExtentMetres=threshold,
                components=removed_components, vertices=removed_vertices)


def bounded_simplify(game, budget, helpers):
    """Allow a few useful additional collapse passes on constrained open frames."""
    for attempt in range(5):
        before = helpers['triangles'](game)
        try:
            helpers['simplify'](game, budget)
            return attempt + 1
        except RuntimeError as error:
            if not str(error).startswith('Source-derived LOD exceeded its triangle budget: '):
                raise
            after = helpers['triangles'](game)
            # Never spin on an actual nonmanifold/topological collapse floor.
            if attempt == 4 or after >= before - 4:
                raise


def normal_atlas(game, original, matrix, destination, name, limit):
    """Bake actual dense source surface normals into the low LOD tangent frame."""
    scene = bpy.context.scene
    scene.render.engine = 'CYCLES'
    scene.cycles.device = 'CPU'
    scene.cycles.samples = 1
    scene.render.bake.use_selected_to_active = True
    scene.render.bake.cage_extrusion = .015
    scene.render.bake.max_ray_distance = .05
    scene.render.bake.margin = 16
    scene.render.bake.use_clear = True
    scene.render.bake.normal_space = 'TANGENT'
    scene.render.bake.normal_r = 'POS_X'
    scene.render.bake.normal_g = 'POS_Y'
    scene.render.bake.normal_b = 'POS_Z'
    source = original.copy()
    source.data = original.data.copy()
    source.matrix_world = matrix @ original.matrix_world
    scene.collection.objects.link(source)
    source.hide_render = False
    source.hide_set(False)
    source.name = 'Temporary unchanged source for tangent normal transfer'
    material = game.data.materials[0]
    nodes = material.node_tree.nodes
    active = nodes.new('ShaderNodeTexImage')
    nodes.active = active
    normal = bpy.data.images.new(name + '-normal', width=limit, height=limit, alpha=True)
    normal.colorspace_settings.name = 'Non-Color'
    active.image = normal
    try:
        bpy.ops.object.select_all(action='DESELECT')
        source.select_set(True)
        game.select_set(True)
        bpy.context.view_layer.objects.active = game
        bpy.ops.object.bake(type='NORMAL')
        pixels = np.empty(limit * limit * 4, dtype=np.float32)
        normal.pixels.foreach_get(pixels)
        values = pixels.reshape(-1, 4)
        if not np.isfinite(values).all() or np.count_nonzero(values[:, 2] > .5) < 64:
            raise RuntimeError('Tangent normal bake produced no usable source surface: ' + name)
        normal.file_format = 'PNG'
        normal.filepath_raw = str(destination / (name + '-normal.png'))
        normal.save()
        active.name = 'Source-derived tangent detail'
        converter = nodes.new('ShaderNodeNormalMap')
        converter.space = 'TANGENT'
        converter.inputs['Strength'].default_value = 1
        shader = next(n for n in nodes if n.type == 'BSDF_PRINCIPLED')
        material.node_tree.links.new(active.outputs['Color'], converter.inputs['Color'])
        material.node_tree.links.new(converter.outputs['Normal'], shader.inputs['Normal'])
        return name + '-normal.png'
    except Exception:
        # A missing normal bake is a quality failure, never an optional fallback.
        if active.name in nodes:
            nodes.remove(active)
        raise
    finally:
        bpy.data.objects.remove(source, do_unlink=True)


def finish(settings):
    if bpy.app.version[:3] != (4, 0, 2):
        raise RuntimeError('Use the pinned Blender 4.0.2')
    key = settings['key']
    if not key or any(c not in 'abcdefghijklmnopqrstuvwxyz0123456789-' for c in key):
        raise ValueError('Asset key must contain lowercase ASCII letters, numbers and hyphens')
    source = SOURCE / key
    original_path = source / 'original.glb'
    if not original_path.is_file():
        raise RuntimeError('Preserved designated-service original missing: ' + str(original_path))
    destination = OUTPUT / key
    destination.mkdir(parents=True, exist_ok=True)
    helpers = runpy.run_path(str(PROJECT / 'SourceArt/CorridorEntrance/finish_entrance_assets.py'),
                             run_name='school_renewal_source_helpers')
    bake = runpy.run_path(str(PROJECT / 'SourceArt/SchoolLoop/rebake_music_lod.py'),
                          run_name='school_renewal_coherent_uv')['bake']
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.context.preferences.filepaths.save_version = 0
    bpy.ops.import_scene.gltf(filepath=str(original_path))
    original_objects = list(bpy.context.scene.objects)
    originals = [obj for obj in original_objects if obj.type == 'MESH']
    if len(originals) != 1 or len(originals[0].material_slots) != 1:
        raise RuntimeError('School renewal finisher requires one original PBR primitive; '
                           + key + ' has ' + str(len(originals)) + ' mesh objects')
    original = originals[0]
    original_hash = helpers['sha'](original_path)
    original_triangles = helpers['triangles'](original)
    preserved = bpy.data.collections.new('Complete unchanged designated-service original')
    bpy.context.scene.collection.children.link(preserved)
    for obj in original_objects:
        for owner in list(obj.users_collection):
            owner.objects.unlink(obj)
        preserved.objects.link(obj)
    game_collection = bpy.data.collections.new('Editable source-derived school game LODs')
    bpy.context.scene.collection.children.link(game_collection)
    rotation = Matrix.Identity(4)
    for axis in ('X', 'Y', 'Z'):
        rotation = Matrix.Rotation(math.radians(float(settings.get('rotation_' + axis.lower(), 0))),
                                   4, axis) @ rotation
    near = original.copy()
    near.data = original.data.copy()
    near.parent = None
    near.matrix_world = rotation @ original.matrix_world
    game_collection.objects.link(near)
    near.hide_render = False
    near.hide_set(False)
    low, high = helpers['bounds']([near])
    axis = settings.get('axis', 'z')
    axis_index = {'x': 0, 'y': 1, 'z': 2}[axis]
    factor = float(settings['metres']) / max(.000001, high[axis_index] - low[axis_index])
    floor_centre = Vector(((low.x + high.x) * .5, (low.y + high.y) * .5, low.z))
    normalization = Matrix.Scale(factor, 4) @ Matrix.Translation(-floor_centre)
    source_to_game = normalization @ rotation
    near.matrix_world = normalization @ near.matrix_world
    bpy.ops.object.select_all(action='DESELECT')
    near.select_set(True)
    bpy.context.view_layer.objects.active = near
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    bm = bmesh.new()
    bm.from_mesh(near.data)
    bmesh.ops.remove_doubles(bm, verts=list(bm.verts), dist=.00002)
    bmesh.ops.dissolve_degenerate(bm, edges=list(bm.edges), dist=.000001)
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    bm.to_mesh(near.data)
    bm.free()
    near.data.update()
    if near.data.has_custom_normals:
        bpy.ops.mesh.customdata_custom_splitnormals_clear()
    geometry_finishing = None
    transfer_source = original
    transfer_finishing = None
    geometry_recipe = source / 'finish_geometry.py'
    if geometry_recipe.is_file():
        recipe = runpy.run_path(str(geometry_recipe), run_name='school_renewal_' + key + '_geometry')
        preprocess = recipe.get('preprocess')
        if not callable(preprocess):
            raise RuntimeError('Asset geometry recipe must expose preprocess(near): ' + key)
        geometry_finishing = preprocess(near)
        if not isinstance(geometry_finishing, dict):
            raise RuntimeError('Asset geometry recipe must return a portable report dictionary: ' + key)
        # Validate serializable provenance immediately, before expensive bakes.
        json.dumps(geometry_finishing)
        if not near.data.vertices or not near.data.polygons:
            raise RuntimeError('Generated-derived geometry recipe removed the whole asset: ' + key)
        prepare_transfer = recipe.get('prepare_transfer_source')
        if callable(prepare_transfer):
            transfer_source, transfer_finishing = prepare_transfer(original)
            if transfer_source is original or transfer_source.type != 'MESH':
                raise RuntimeError('Material finishing must return a separate working source: ' + key)
            json.dumps(transfer_finishing)
            # Retain the explicit source-derived material repair beside the
            # unchanged original; only its UV transfer is used for colour/ORM.
            for owner in list(transfer_source.users_collection):
                owner.objects.unlink(transfer_source)
            preserved.objects.link(transfer_source)
            transfer_source.hide_render = False
            transfer_source.hide_set(False)
            original_objects.append(transfer_source)
    far_template = None
    if settings.get('far_from_source', False):
        # A second collapse of the already collapsed near mesh can hit newly
        # formed nonmanifold junctions. Derive far independently when requested.
        far_template = near.copy()
        far_template.data = near.data.copy()
        far_template.name = 'Temporary unchanged normalized derived geometry for far LOD'
        game_collection.objects.link(far_template)
        far_template.hide_set(True)
        far_template.hide_render = True
    budgets = tuple(int(value) for value in settings['budgets'])
    if len(budgets) != 2 or min(budgets) < 12 or budgets[1] > budgets[0]:
        raise ValueError('Exactly two decreasing positive triangle budgets are required')
    texture = int(settings.get('texture', 2048))
    lods, materials, reports = [], [], []
    for index, budget in enumerate(budgets):
        if index == 0:
            game = near
        else:
            basis = far_template if far_template else near
            game = basis.copy()
            game.data = basis.data.copy()
            game.parent = None
            game_collection.objects.link(game)
        game.name = 'SR_' + key + '_LOD' + str(index)
        game.hide_render = False
        game.hide_set(False)
        cleanup = remove_tiny_islands(game, .0015 if index == 0 else .008)
        collapse_attempts = bounded_simplify(game, budget, helpers)
        name = 'SR_' + key + '_LOD' + str(index)
        size = texture if index == 0 else min(texture, 1024)
        material = bake(game, transfer_source, source_to_game, destination, name, size,
                        helpers['save_pixels'])
        material['normal'] = normal_atlas(game, original, source_to_game, destination, name, size)
        material['textureMaxSize'] = size
        materials.append(material)
        bpy.ops.object.select_all(action='DESELECT')
        game.select_set(True)
        bpy.context.view_layer.objects.active = game
        filename = key + '-lod' + str(index) + '.fbx'
        path = destination / filename
        bpy.ops.export_scene.fbx(filepath=str(path), use_selection=True, object_types={'MESH'},
                                 axis_forward='-Z', axis_up='Y', bake_anim=False,
                                 add_leaf_bones=False, path_mode='RELATIVE', use_mesh_modifiers=True)
        a, b = helpers['bounds']([game])
        reports.append(dict(file=filename, targetTriangles=budget,
                            triangles=helpers['triangles'](game), vertices=len(game.data.vertices),
                            boundsMin=list(a), boundsMax=list(b), sha256=helpers['sha'](path),
                            removedGeneratedSpecks=cleanup, collapseAttempts=collapse_attempts))
        lods.append(game)
    a, b = helpers['bounds']([near])
    if far_template:
        temporary_mesh = far_template.data
        bpy.data.objects.remove(far_template, do_unlink=True)
        if temporary_mesh.users == 0:
            bpy.data.meshes.remove(temporary_mesh)
    report = dict(key=key, tool='Blender 4.0.2', original='original.glb',
                  originalSha256=original_hash, editable=key + '.blend',
                  normalizedAxis=axis, normalizedMetres=float(settings['metres']),
                  widthMetres=b.x-a.x, heightMetres=b.z-a.z, depthMetres=b.y-a.y,
                  originalTriangles=original_triangles, textureMaxSize=texture,
                  settings=settings, lods=reports, materials=materials,
                  geometryFinishing=geometry_finishing,
                  materialFinishing=transfer_finishing,
                  front='Authored front Blender +Y / Unity -Z after standard FBX conversion; '
                        'confirm asset-specific orientation in actual Unity integration.',
                  finishing='Dense generated geometry retained unchanged; duplicate LODs receive '
                            'bounded collapse, coherent UV atlas transfer of original colour/ORM, '
                            'and real selected-high-to-low tangent-space normal bake.',
                  physics='Visual assets contain no generated collision or animation; school '
                          'assembly owns simple stable collision and navigation.')
    for path in (source / 'export.json', destination / 'asset.json'):
        path.write_text(json.dumps(report, indent=2) + '\n', encoding='utf-8')
    for obj in original_objects:
        obj.hide_render = True
        obj.hide_set(True)
    for obj in lods[1:]:
        obj.hide_render = True
        obj.hide_set(True)
    for title, path in (
            ('finish_asset.py', Path(__file__)),
            ('finish_entrance_assets.py', PROJECT / 'SourceArt/CorridorEntrance/finish_entrance_assets.py'),
            ('rebake_music_lod.py', PROJECT / 'SourceArt/SchoolLoop/rebake_music_lod.py')):
        bpy.data.texts.new('SOURCE - ' + title).write(path.read_text(encoding='utf-8'))
    if geometry_recipe.is_file():
        bpy.data.texts.new('SOURCE - ' + key + '/finish_geometry.py').write(
            geometry_recipe.read_text(encoding='utf-8'))
        for path in sorted(source.glob('*.json')):
            bpy.data.texts.new('SOURCE - ' + key + '/' + path.name).write(path.read_text(encoding='utf-8-sig'))
    bpy.ops.file.pack_all()
    bpy.ops.wm.save_as_mainfile(filepath=str(source / (key + '.blend')), compress=True)
    if helpers['sha'](original_path) != original_hash:
        raise RuntimeError('Preserved generated original unexpectedly changed')
    print('SCHOOL_RENEWAL_ASSET_PASS ' + json.dumps(report), flush=True)
    return report


def arguments():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--key', required=True)
    parser.add_argument('--axis', choices=('x', 'y', 'z'), default='z')
    parser.add_argument('--metres', type=float, required=True)
    parser.add_argument('--budgets', required=True, help='Near,far triangle budgets')
    parser.add_argument('--texture', type=int, default=2048)
    parser.add_argument('--far-from-source', action='store_true',
                        help='Derive far from the normalized post-hook source copy rather than near')
    for axis in ('x', 'y', 'z'):
        parser.add_argument('--rotation-' + axis, type=float, default=0)
    settings = vars(parser.parse_args(sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []))
    settings['budgets'] = [int(value.strip()) for value in settings['budgets'].split(',')]
    return settings


if __name__ == '__main__':
    finish(arguments())
