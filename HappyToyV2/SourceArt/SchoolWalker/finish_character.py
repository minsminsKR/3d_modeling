"""Finish the designated service's actual rigged flower man, never a substitute.

Pinned Blender 4.0.2, repository-relative input and output paths:
  blender -b --factory-startup --threads 2 --python-exit-code 1 --python
  HappyToyV2/SourceArt/SchoolWalker/finish_character.py -- --plan rig-plan.json

The reviewed plan supplies actual source bone names, canonical facing and any
necessary source-derived surface repair. AI originals remain packed and unchanged.
"""
from pathlib import Path
from array import array
import argparse
import hashlib
import importlib.util
import json
import math
import runpy
import sys

import bpy
import bmesh
import numpy as np
from mathutils import Matrix, Vector
from mathutils.bvhtree import BVHTree
from mathutils.geometry import barycentric_transform

HERE = Path(__file__).resolve().parent
PROJECT = HERE.parents[1]
OUTPUT = PROJECT / 'Assets/Resources/SchoolWalker'
REQUIRED = ['Hips', 'Spine', 'Head'] + [side + part for side in ('Left', 'Right')
    for part in ('Arm', 'ForeArm', 'Hand', 'UpLeg', 'Leg', 'Foot')]


def digest(path):
    checksum = hashlib.sha256()
    with Path(path).open('rb') as stream:
        for block in iter(lambda: stream.read(8 * 1024 * 1024), b''):
            checksum.update(block)
    return checksum.hexdigest()


def load_module(path, name):
    spec = importlib.util.spec_from_file_location(name, path)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def world_points(mesh):
    values = np.empty(len(mesh.data.vertices) * 3, dtype=np.float32)
    mesh.data.vertices.foreach_get('co', values)
    matrix = np.asarray(mesh.matrix_world)
    return values.reshape(-1, 3) @ matrix[:3, :3].T + matrix[:3, 3]


def triangles(mesh):
    mesh.data.calc_loop_triangles()
    return len(mesh.data.loop_triangles)


def source_snapshot(mesh, rig):
    positions = np.empty(len(mesh.data.vertices) * 3, dtype=np.float32)
    mesh.data.vertices.foreach_get('co', positions)
    uv = array('f', [0.]) * (len(mesh.data.uv_layers.active.data) * 2)
    mesh.data.uv_layers.active.data.foreach_get('uv', uv)
    joints, weights = array('I'), array('f')
    for vertex in mesh.data.vertices:
        joints.append(len(vertex.groups))
        for entry in vertex.groups:
            joints.append(entry.group)
            weights.append(entry.weight)
    return dict(positions=hashlib.sha256(positions.tobytes()).hexdigest(),
                uv=hashlib.sha256(uv.tobytes()).hexdigest(),
                skin=hashlib.sha256(joints.tobytes() + weights.tobytes()).hexdigest(),
                bones=[dict(name=bone.name, parent=bone.parent.name if bone.parent else None,
                            matrix=[list(row) for row in bone.matrix_local]) for bone in rig.data.bones])


def select(obj):
    bpy.ops.object.select_all(action='DESELECT')
    obj.hide_set(False)
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj


def normalize(rig, mesh, rotation_degrees):
    rig.data.pose_position = 'REST'
    values = world_points(mesh)
    minimum, maximum = values.min(0), values.max(0)
    scale = 1.77 / float(maximum[2] - minimum[2])
    centre = Vector(((minimum[0] + maximum[0]) * .5,
                     (minimum[1] + maximum[1]) * .5, minimum[2]))
    rotation = Matrix.Rotation(math.radians(rotation_degrees), 4, 'Z')
    change = Matrix.Scale(scale, 4) @ rotation @ Matrix.Translation(-centre)
    transforms = {obj: change @ obj.matrix_world.copy() for obj in (rig, mesh)}
    mesh.parent = None
    rig.parent = None
    for obj in (rig, mesh):
        obj.matrix_world = transforms[obj]
        select(obj)
        bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    mesh.parent = rig
    mesh.matrix_parent_inverse = Matrix.Identity(4)
    for modifier in mesh.modifiers:
        if modifier.type == 'ARMATURE':
            modifier.object = rig
            modifier.show_viewport = False
            modifier.show_render = False
            modifier.use_deform_preserve_volume = False
    return dict(heightMetres=1.77, uniformScale=scale, sourceBounds=[minimum.tolist(), maximum.tolist()],
                rotationZDegrees=rotation_degrees, coordinates='Blender Z-up, front -Y; Unity Y-up, front +Z; sole 0')


def protect_head_skin(mesh, head_name, threshold):
    """The actual skull/hair/ear flower must follow one head, never a limb."""
    selected = [vertex.index for vertex in mesh.data.vertices if vertex.co.z >= threshold]
    if not selected:
        raise RuntimeError('Reviewed rigid head selection is empty')
    for group in mesh.vertex_groups:
        group.remove(selected)
    group = mesh.vertex_groups.get(head_name) or mesh.vertex_groups.new(name=head_name)
    group.add(selected, 1., 'REPLACE')
    return dict(method='Rigid game-copy skull/hair/ear-flower weights above reviewed source plane',
                thresholdMetres=threshold, selectedVertices=len(selected), headBone=head_name,
                originalAIWeightsUnchanged=True)


def repair_relaxed_hands(mesh, rig, mapping, settings):
    reports = []
    radius = float(settings['radiusMetres'])
    start = float(settings['wristBlendStartMetres'])
    end = float(settings['wristBlendEndMetres'])
    for side in ('Left', 'Right'):
        hand = rig.data.bones[mapping[side + 'Hand']]
        forearm = rig.data.bones[mapping[side + 'ForeArm']]
        sign = 1 if hand.head_local.x > 0 else -1
        # The actual app wrist joint sits behind the visible palm. Measure the
        # real hand volume rather than centring a tiny mask on that bad Y value.
        lateral_minimum = abs(hand.head_local.x) - .075
        candidates = [vertex.co for vertex in mesh.data.vertices
            if vertex.co.x * sign > lateral_minimum
            and hand.head_local.z - .30 < vertex.co.z < hand.head_local.z + .10]
        wrist_surface = [point for point in candidates if abs(point.z - hand.head_local.z) < .018]
        palm_surface = [point for point in candidates if hand.head_local.z - .16 < point.z < hand.head_local.z - .06]
        if len(wrist_surface) < 20 or len(palm_surface) < 20:
            raise RuntimeError('Cannot measure actual visible wrist/palm surface: ' + side)
        origin = Vector((float(np.median([point.x for point in wrist_surface])),
                         float(np.median([point.y for point in wrist_surface])), hand.head_local.z))
        palm = Vector(tuple(float(np.median([point[axis] for point in palm_surface])) for axis in range(3)))
        axis = (palm - origin).normalized()
        selected, batches = [], {}
        for vertex in mesh.data.vertices:
            relative = vertex.co - origin
            along = relative.dot(axis)
            radial = (relative - axis * along).length
            if -.04 <= along <= .28 and radial < radius and vertex.co.x * sign > lateral_minimum:
                value = max(0., min(1., (along - start) / (end - start)))
                value = value * value * (3 - 2 * value)
                selected.append(vertex.index)
                batches.setdefault(round(value * 65535), []).append(vertex.index)
        if len(selected) < 100:
            raise RuntimeError('Actual reviewed wrist volume contains too few source vertices: ' + side)
        for group in mesh.vertex_groups:
            group.remove(selected)
        hand_group = mesh.vertex_groups.get(hand.name) or mesh.vertex_groups.new(name=hand.name)
        forearm_group = mesh.vertex_groups.get(forearm.name) or mesh.vertex_groups.new(name=forearm.name)
        for amount, ids in batches.items():
            if amount:
                hand_group.add(ids, amount / 65535, 'REPLACE')
            if amount < 65535:
                forearm_group.add(ids, 1 - amount / 65535, 'REPLACE')
        reports.append(dict(side=side, selectedVertices=len(selected), handBone=hand.name,
            forearmBone=forearm.name, originalAIWrist=list(hand.head_local), measuredVisibleWrist=list(origin),
            measuredPalm=list(palm), axis=list(axis), lateralMinimumMetres=lateral_minimum,
            radiusMetres=radius, blendStartMetres=start, blendEndMetres=end))
    return dict(method='Actual wrist-volume anatomical forearm blend, rigid relaxed palm/fingers',
                reason='Observed original AI palm/finger branch weight wedges during real arm motion',
                originalAIWeightsUnchanged=True, hands=reports)


def derive_near(high, rig, budget, voxel):
    game = high.copy()
    game.data = high.data.copy()
    game.parent = None
    game.matrix_world = Matrix.Identity(4)
    game.name = 'SW_LOD0'
    bpy.context.scene.collection.objects.link(game)
    game.modifiers.clear()
    select(game)
    mesh = game.data
    if mesh.has_custom_normals:
        bpy.ops.mesh.customdata_custom_splitnormals_clear()
    before = triangles(game)
    editable = bmesh.new()
    editable.from_mesh(mesh)
    bmesh.ops.remove_doubles(editable, verts=list(editable.verts), dist=.000025)
    bmesh.ops.dissolve_degenerate(editable, edges=list(editable.edges), dist=.0000005)
    editable.to_mesh(mesh)
    editable.free()
    if voxel:
        # Only use an explicitly reviewed resolution. This changes generated
        # game copies; the preserved source and its identity detail stay intact.
        mesh.remesh_voxel_size = voxel
        mesh.remesh_voxel_adaptivity = 0
        mesh.use_remesh_preserve_volume = True
        bpy.ops.object.voxel_remesh()
    priority = game.vertex_groups.new(name='Source-derived face flower and hand detail priority')
    for vertex in game.data.vertices:
        value = .85 if vertex.co.z > 1.43 else (.65 if .62 < vertex.co.z < 1.02 and abs(vertex.co.x) > .34 else .05)
        priority.add([vertex.index], value, 'REPLACE')
    collapse(game, budget, priority.name)
    for face in game.data.polygons:
        face.use_smooth = True
    return game, dict(originalTriangles=before, gameTriangles=triangles(game),
                      weldMetres=.000025, sourceDerivedVoxelMetres=voxel,
                      originalGeometryUnchanged=True)


def collapse(mesh, budget, priority=None):
    for _ in range(5):
        before = triangles(mesh)
        if before <= budget:
            return
        select(mesh)
        modifier = mesh.modifiers.new('Source-derived bounded game collapse', 'DECIMATE')
        modifier.ratio = max(.001, (budget - 8) / before)
        modifier.use_collapse_triangulate = True
        if priority:
            modifier.vertex_group = priority
            modifier.invert_vertex_group = True
            modifier.vertex_group_factor = 5
        bpy.ops.object.modifier_apply(modifier=modifier.name)
        if triangles(mesh) >= before - 4:
            break
    if triangles(mesh) > budget:
        raise RuntimeError('Source topology cannot meet reviewed game LOD budget: ' + str(triangles(mesh)))


def transfer_skin(high, game, rig, tree):
    """Barycentric original AI weights on actual closest source triangles."""
    game.vertex_groups.clear()
    palette = {group.index: group.name for group in high.vertex_groups if group.name in rig.data.bones}
    groups = {name: game.vertex_groups.new(name=name) for name in palette.values()}
    source = high.data
    cached = {}
    maximum_distance, maximum_error = 0., 0.
    batches = {}
    for vertex in game.data.vertices:
        hit, _, polygon, distance = tree.find_nearest(vertex.co)
        if hit is None or distance > .035:
            raise RuntimeError('Game skin escaped original generated body surface: ' + str(distance))
        maximum_distance = max(maximum_distance, distance)
        ids = tuple(source.polygons[polygon].vertices)
        if len(ids) != 3:
            raise RuntimeError('Expected the actual triangulated app skin source')
        a, b, c = [source.vertices[index].co for index in ids]
        values = barycentric_transform(hit, a, b, c, Vector((1, 0, 0)), Vector((0, 1, 0)), Vector((0, 0, 1)))
        values = [max(0., float(value)) for value in values]
        total = sum(values)
        if total < 1e-8:
            raise RuntimeError('Source skin triangle is degenerate')
        amounts = {}
        for index, factor in zip(ids, values):
            if index not in cached:
                cached[index] = [(palette[entry.group], entry.weight) for entry in source.vertices[index].groups if entry.group in palette]
            for name, weight in cached[index]:
                amounts[name] = amounts.get(name, 0.) + weight * factor / total
        chosen = sorted(amounts.items(), key=lambda item: item[1], reverse=True)[:4]
        denominator = sum(weight for _, weight in chosen)
        if denominator < .0001:
            raise RuntimeError('Actual app skeleton has an unweighted body region')
        normalized = [(name, weight / denominator) for name, weight in chosen]
        maximum_error = max(maximum_error, abs(sum(weight for _, weight in normalized) - 1))
        for name, weight in normalized:
            batches.setdefault((name, round(weight * 65535)), []).append(vertex.index)
    for (name, amount), indices in batches.items():
        groups[name].add(indices, amount / 65535, 'REPLACE')
    modifier = game.modifiers.new('Preserved actual app bone palette', 'ARMATURE')
    modifier.object = rig
    modifier.use_deform_preserve_volume = False
    game.parent = rig
    game.matrix_parent_inverse = Matrix.Identity(4)
    return dict(method='Closest source triangle / barycentric AI skin transfer, normalized strongest four',
                maximumSourceDistanceMetres=maximum_distance, maximumNormalizationError=maximum_error,
                maximumInfluences=4, bones=len(groups))


def duplicate_far_material(near_material, destination, size):
    material = near_material.copy()
    material.name = 'SchoolWalker far generated PBR'
    for node in material.node_tree.nodes:
        if node.type != 'TEX_IMAGE' or not node.image:
            continue
        original = node.image
        image = original.copy()
        factor = min(1., size / max(image.size))
        image.scale(max(1, round(image.size[0] * factor)), max(1, round(image.size[1] * factor)))
        name = Path(original.filepath_raw).name.replace('SW_LOD0-', 'SW_LOD2-')
        image.filepath_raw = str(destination / name)
        image.file_format = 'PNG'
        image.save()
        node.image = image
    return material


def export_fbx(path, rig, lods, action):
    bpy.ops.object.select_all(action='DESELECT')
    rig.hide_set(False)
    rig.select_set(True)
    for mesh in lods:
        mesh.hide_set(False)
        mesh.select_set(True)
    bpy.context.view_layer.objects.active = rig
    rig.animation_data.action = action
    bpy.context.scene.frame_start = round(action.frame_range[0])
    bpy.context.scene.frame_end = round(action.frame_range[1])
    bpy.context.scene.frame_set(bpy.context.scene.frame_start)
    bpy.ops.export_scene.fbx(filepath=str(path), use_selection=True,
        object_types={'MESH', 'ARMATURE'}, axis_forward='-Z', axis_up='Y',
        global_scale=1, apply_unit_scale=True, apply_scale_options='FBX_SCALE_UNITS',
        add_leaf_bones=False, use_armature_deform_only=True, bake_anim=True,
        bake_anim_use_all_actions=False, bake_anim_use_nla_strips=False,
        bake_anim_simplify_factor=0, bake_anim_step=1, use_tspace=True,
        mesh_smooth_type='FACE', path_mode='RELATIVE', embed_textures=False)


def finish(plan_path):
    if bpy.app.version[:3] != (4, 0, 2):
        raise RuntimeError('Use pinned Blender 4.0.2')
    plan = json.loads(plan_path.read_text(encoding='utf-8-sig'))
    source = HERE / plan.get('riggedSource', 'rigged-original.glb')
    original = HERE / plan.get('generatedSource', 'original.glb')
    if not source.is_file() or not original.is_file():
        raise RuntimeError('Collect the actual preserved designated generation and working AI rig first')
    before_hashes = {path: digest(path) for path in (source, original)}
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.context.preferences.filepaths.save_version = 0
    bpy.ops.import_scene.gltf(filepath=str(source))
    imported = list(bpy.context.scene.objects)
    rigs = [obj for obj in imported if obj.type == 'ARMATURE']
    meshes = [obj for obj in imported if obj.type == 'MESH' and len(obj.vertex_groups) and len(obj.material_slots)]
    if len(rigs) != 1 or len(meshes) != 1 or len(meshes[0].material_slots) != 1:
        raise RuntimeError('Inspect actual rig structure before using this one-primitive finisher')
    mapping = plan['bones']
    missing = [name for name in REQUIRED if mapping.get(name) not in rigs[0].data.bones]
    if missing or len(set(mapping.values())) != len(mapping):
        raise RuntimeError('Reviewed source anatomical mapping is incomplete or ambiguous: ' + str(missing))
    preserved = bpy.data.collections.new('Unchanged designated app AI rig, source mesh and full PBR')
    bpy.context.scene.collection.children.link(preserved)
    for obj in imported:
        for collection in list(obj.users_collection):
            collection.objects.unlink(obj)
        preserved.objects.link(obj)
    source_rig, source_mesh = rigs[0], meshes[0]
    original_snapshot = source_snapshot(source_mesh, source_rig)
    source_rig.data.pose_position = 'REST'
    rig = source_rig.copy()
    rig.data = source_rig.data.copy()
    rig.animation_data_clear()
    rig.name = 'SchoolWalkerRig'
    bpy.context.scene.collection.objects.link(rig)
    high = source_mesh.copy()
    high.data = source_mesh.data.copy()
    high.name = 'SchoolWalker editable generated high'
    bpy.context.scene.collection.objects.link(high)
    normalized = normalize(rig, high, float(plan.get('rotationZDegrees', 0)))
    rigid = protect_head_skin(high, mapping['Head'], float(plan['rigidHeadAboveMetres'])) if 'rigidHeadAboveMetres' in plan else None
    hand_repair = repair_relaxed_hands(high, rig, mapping, plan['handWeightRepair']) if plan.get('handWeightRepair') else None
    for obj in imported:
        obj.hide_set(True)
        obj.hide_render = True
    OUTPUT.mkdir(parents=True, exist_ok=True)
    budgets = plan.get('triangleBudgets', [25000, 12000, 4000])
    if len(budgets) != 3 or not (budgets[0] <= 50000 and budgets[1] <= 12000 and budgets[2] <= 4000):
        raise RuntimeError('Exactly three reviewed bounded LOD budgets are required')
    near, geometry = derive_near(high, rig, budgets[0], float(plan.get('voxelMetres', 0)))
    tools = runpy.run_path(str(PROJECT / 'SourceArt/SchoolRenewal/finish_asset.py'), run_name='school_walker_normal_transfer')
    bake = runpy.run_path(str(PROJECT / 'SourceArt/SchoolLoop/rebake_music_lod.py'), run_name='school_walker_pbr_transfer')['bake']
    save_pixels = runpy.run_path(str(PROJECT / 'SourceArt/CorridorEntrance/finish_entrance_assets.py'), run_name='school_walker_image_transfer')['save_pixels']
    texture_size = int(plan.get('textureSize', 2048))
    pbr = bake(near, high, Matrix.Identity(4), OUTPUT, 'SW_LOD0', texture_size, save_pixels)
    pbr['normal'] = tools['normal_atlas'](near, high, Matrix.Identity(4), OUTPUT, 'SW_LOD0', texture_size)
    lods = [near]
    for index, budget in enumerate(budgets[1:], start=1):
        mesh = near.copy()
        mesh.data = near.data.copy()
        mesh.name = 'SW_LOD' + str(index)
        bpy.context.scene.collection.objects.link(mesh)
        collapse(mesh, budget)
        lods.append(mesh)
    # All three game meshes inherit the same coherent near UV atlas. LOD0/1
    # share the same 2K PBR material; far uses a 1K copy of those same images.
    far_material = duplicate_far_material(near.data.materials[0], OUTPUT, 1024)
    lods[2].data.materials[0] = far_material
    tree = BVHTree.FromObject(high, bpy.context.evaluated_depsgraph_get())
    skin = [transfer_skin(high, mesh, rig, tree) for mesh in lods]
    for obj in imported:
        obj.hide_set(True)
        obj.hide_render = True
    high.hide_set(True)
    high.hide_render = True
    motion = load_module(HERE / 'heavy_walk.py', 'school_walker_heavy_walk')
    walk = motion.bake(rig, high, mapping, {'name': 'walk', **plan.get('walk', {})})
    walk_action = rig.animation_data.action
    idle = motion.bake(rig, high, mapping, {'name': 'idle', **plan.get('idle', {})})
    idle_action = rig.animation_data.action
    export_fbx(OUTPUT / 'walker.fbx', rig, lods, walk_action)
    export_fbx(OUTPUT / 'idle.fbx', rig, [], idle_action)
    rig.animation_data.action = walk_action
    bpy.context.scene.frame_start, bpy.context.scene.frame_end = walk['frameStart'], walk['frameEnd']
    bpy.context.scene.frame_set(1)
    for mesh in lods[1:]:
        mesh.hide_set(True)
        mesh.hide_render = True
    report = dict(status='EXPORTED_SOURCE_DERIVED', tool='Blender 4.0.2 CLI',
        generatedSource=original.name, generatedSha256=before_hashes[original],
        riggedSource=source.name, riggedSha256=before_hashes[source], normalization=normalized,
        originalGeometryAndImagesUnchanged=True, canonicalForward='Unity +Z',
        sourceDerivedGeometry=geometry, rigidHead=rigid, handWeightRepair=hand_repair, bones=mapping,
        lods=[dict(name=mesh.name, triangles=triangles(mesh), vertices=len(mesh.data.vertices), skin=weights)
              for mesh, weights in zip(lods, skin)],
        material=dict(near=pbr, far={key: value.replace('SW_LOD0-', 'SW_LOD2-') for key, value in pbr.items() if isinstance(value, str)},
                      nearAndMiddleShared=True, commonAtlasUV=True, nearSize=texture_size, farSize=1024),
        walk=walk, idle=idle, fbx=[dict(file=path.name, sha256=digest(path)) for path in (OUTPUT / 'walker.fbx', OUTPUT / 'idle.fbx')],
        editable='SchoolWalker.blend', physics='No source collision; runtime owns stable actor capsule/navigation')
    if source_snapshot(source_mesh, source_rig) != original_snapshot:
        raise RuntimeError('Editable preserved AI original geometry/UV/weights/bone rest matrices changed')
    report['preservedAISourceSnapshot'] = original_snapshot
    for path in (HERE / 'export.json', OUTPUT / 'asset.json'):
        path.write_text(json.dumps(report, indent=2) + '\n', encoding='utf-8')
    for path in (Path(__file__), HERE / 'heavy_walk.py', plan_path):
        bpy.data.texts.new('SOURCE - ' + path.name).write(path.read_text(encoding='utf-8-sig'))
    bpy.ops.file.pack_all()
    bpy.ops.wm.save_as_mainfile(filepath=str(HERE / 'SchoolWalker.blend'), compress=True)
    for path, expected in before_hashes.items():
        if digest(path) != expected:
            raise RuntimeError('Preserved designated-service original unexpectedly changed')
    print('SCHOOL_WALKER_FINISH_PASS ' + json.dumps(dict(lods=report['lods'], metresPerCycle=walk['worldTravelPerCycleMetres'])), flush=True)
    return report


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--plan', default='rig-plan.json')
    args = parser.parse_args(sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else [])
    finish(HERE / args.plan)
