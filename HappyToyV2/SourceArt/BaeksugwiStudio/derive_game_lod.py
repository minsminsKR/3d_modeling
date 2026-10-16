"""Blender 4.0.2: derive game-only LODs from the retained generated rig.

From the repository root:
blender --background --factory-startup --python-exit-code 1 --python
  HappyToyV2/SourceArt/BaeksugwiStudio/derive_game_lod.py

Writes only this SourceArt folder. The original blend/model/atlas, Unity Assets,
prefab and title remain untouched. The saved editable derived blend contains
only its approved game surface, the original rig/actions and packed PBR images.
"""
from pathlib import Path
import hashlib
import json
import math
import struct
import sys

import bpy
import bmesh
import numpy as np
from mathutils import Vector
from mathutils.bvhtree import BVHTree

HERE = Path(__file__).resolve().parent
RECIPE_PATH = HERE / 'game-lod-recipe.json'
RECIPE = json.loads(RECIPE_PATH.read_text(encoding='utf-8'))


def sha(path):
    digest = hashlib.sha256()
    with path.open('rb') as stream:
        for chunk in iter(lambda: stream.read(8 * 1024 * 1024), b''):
            digest.update(chunk)
    return digest.hexdigest()


def action_hash(action):
    digest = hashlib.sha256()
    for curve in action.fcurves:
        digest.update(curve.data_path.encode())
        digest.update(struct.pack('<i', curve.array_index))
        for key in curve.keyframe_points:
            digest.update(struct.pack('<6f', *key.co, *key.handle_left, *key.handle_right))
            digest.update(key.interpolation.encode())
    return digest.hexdigest()


def select(obj):
    bpy.ops.object.select_all(action='DESELECT')
    obj.hide_set(False)
    obj.hide_render = False
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj


def points(mesh):
    result = np.empty(len(mesh.vertices) * 3, dtype=np.float32)
    mesh.vertices.foreach_get('co', result)
    return result.reshape(-1, 3)


def groups(obj, names, minimum=None):
    wanted = {group.index: group.name for group in obj.vertex_groups if group.name in names}
    result = {name: [] for name in names}
    for vertex in obj.data.vertices:
        for entry in vertex.groups:
            name = wanted.get(entry.group)
            if name and entry.weight >= (RECIPE['headRigidWeight'] if minimum is None else minimum):
                result[name].append(vertex.index)
    return {name: np.asarray(indices, dtype=np.int32) for name, indices in result.items()}


def protection(obj, rig, envelope_vertices):
    vertices = points(obj.data)
    wrists = [bone.name for bone in rig.data.bones if bone.name.endswith('_Wrist')]
    assigned = groups(obj, ['Head'] + wrists)
    if len(wrists) != 18 or len(assigned['Head']) < 100:
        raise ValueError('The reviewed source lost its head or eighteen rigid hand surfaces')
    mask = np.full(len(vertices), RECIPE['bodyPriority'], dtype=np.float32)
    arm_names = [bone.name for bone in rig.data.bones if bone.name.startswith('Arm')]
    arm_regions = groups(obj, arm_names, RECIPE['armInfluenceThreshold'])
    arm_ids = np.unique(np.concatenate([ids for ids in arm_regions.values() if len(ids)]))
    mask[arm_ids] = RECIPE['armPriority']
    full_hands = groups(obj, wrists, RECIPE['handInfluenceThreshold'])
    head_ids = assigned['Head']
    mask[head_ids] = RECIPE['headPriority']
    # The preserved source declares front +glTF Z / -Blender Y. Head ->
    # MaskTip points down the chin, so it cannot stand in for face direction.
    inverse = obj.matrix_world.inverted().to_3x3() @ rig.matrix_world.to_3x3()
    forward = np.asarray((inverse @ Vector(RECIPE['frontAxisBlender'])).normalized())
    projected = vertices[head_ids] @ forward
    head_center = obj.matrix_world.inverted() @ rig.matrix_world @ rig.data.bones['Head'].head_local
    face_ids = head_ids[(projected >= np.quantile(projected, RECIPE['frontFaceQuantile'])) &
                       (np.abs(vertices[head_ids, 0]-head_center.x) <= RECIPE['frontFaceHalfWidthMetres'])]
    mask[face_ids] = 1
    hand_probes = {}
    for name in wrists:
        ids = assigned[name]
        if len(ids) < 4:
            raise ValueError('Missing rigid hand surface: ' + name)
        low = float(vertices[ids, 2].min())
        bottom = ids[vertices[ids, 2] <= low + RECIPE['contactBandMetres']]
        # The entire hand surface needs its curved silhouette, finger gaps and
        # wrist transition. Underside extrema alone permit huge spanning plates.
        mask[full_hands[name]] = 1
        mask[bottom] = 2
        for axis in range(3):
            mask[ids[np.argmin(vertices[ids, axis])]] = 2
            mask[ids[np.argmax(vertices[ids, axis])]] = 2
        hand_probes[name] = {'sourceMinimumZ': low, 'protectedVertices': len(bottom),
                            'fullSurfaceVertices': len(full_hands[name])}
    # Preserve the original animated envelope itself. These are source topology
    # indices observed in evaluated run poses, rather than fitted bounds proxies.
    mask[np.asarray(sorted(envelope_vertices), dtype=np.int32)] = 2
    group = obj.vertex_groups.new(name='GameLOD_FaceAndContactPriority')
    exact = obj.vertex_groups.new(name='GameLOD_ExactPins')
    exact.add(np.flatnonzero(mask == 2).tolist(), 1, 'REPLACE')
    for value in np.unique(mask):
        ids = np.flatnonzero(mask == value)
        if len(ids):
            group.add(ids.tolist(), 1 if value == 2 else min(value, 1-RECIPE['protectedCollapseWeight']), 'REPLACE')
    protected = np.flatnonzero(mask >= 1)
    return group.name, vertices[protected].copy(), hand_probes, {
        'headVertices': len(head_ids), 'frontHeadVertices': len(face_ids),
        'priorityVertices': len(protected), 'softArmVertices': len(arm_ids),
        'fullHandSurfaceVertices': sum(len(ids) for ids in full_hands.values()),
        'animatedEnvelopePins': len(envelope_vertices), 'forwardAxis': forward.tolist(),
        'method': 'Original front face and complete eighteen wrist-influenced hand surfaces; soft priority on every arm; unchanged per-corner source UVs.'}


def normalize_weights(obj, rig):
    bone_groups = {group.index for group in obj.vertex_groups if group.name in rig.data.bones}
    batches = {}
    maximum_error = 0.0
    for vertex in obj.data.vertices:
        weighted = sorted(((entry.group, entry.weight) for entry in vertex.groups
                           if entry.group in bone_groups and entry.weight > 1e-8),
                          key=lambda entry: (-entry[1], entry[0]))[:4]
        total = sum(weight for _, weight in weighted)
        if total < 1e-7:
            raise ValueError('Unweighted derived vertex: ' + str(vertex.index))
        scaled = np.asarray([weight / total * 1024 for _, weight in weighted])
        bins = np.floor(scaled).astype(int)
        for index in np.argsort(-(scaled-bins), kind='stable')[:1024-int(bins.sum())]:
            bins[index] += 1
        maximum_error = max(maximum_error, abs(float(sum(bins))/1024-1))
        for (group, _), amount in zip(weighted, bins):
            if amount:
                batches.setdefault((group, int(amount)), []).append(vertex.index)
    for group in obj.vertex_groups:
        if group.index in bone_groups:
            group.remove(range(len(obj.data.vertices)))
    for (group, amount), ids in batches.items():
        obj.vertex_groups[group].add(ids, amount/1024, 'REPLACE')
    return {'maximumInfluences': 4, 'weightBins': 1024, 'zeroWeightVertices': 0,
            'maximumWeightSumError': maximum_error}


def reduce_mesh(obj, target, maximum, strength, priority):
    select(obj)
    obj.data.calc_loop_triangles()
    before = len(obj.data.loop_triangles)
    modifier = obj.modifiers.new('Derived collapse: keep original face and contact surfaces', 'DECIMATE')
    modifier.ratio = min(1, target * .995 / before)
    modifier.use_collapse_triangulate = True
    modifier.vertex_group = priority
    modifier.invert_vertex_group = True
    modifier.vertex_group_factor = strength
    # Applying before the original armature preserves rest topology and weights.
    bpy.ops.object.modifier_move_to_index(modifier=modifier.name, index=0)
    bpy.ops.object.modifier_apply(modifier=modifier.name)
    obj.data.calc_loop_triangles()
    weighted_count = len(obj.data.loop_triangles)
    select(obj)
    mesh = bmesh.new()
    mesh.from_mesh(obj.data)
    loose = [vertex for vertex in mesh.verts if not vertex.link_faces]
    if loose:
        bmesh.ops.delete(mesh, geom=loose, context='VERTS')
    mesh.to_mesh(obj.data)
    mesh.free()
    obj.data.validate(clean_customdata=True)
    obj.data.update()
    obj.data.calc_loop_triangles()
    count = len(obj.data.loop_triangles)
    if count > maximum or count < 1:
        raise ValueError(f'{obj.name}: protected derived triangle count {count} exceeds allowed {maximum}')
    if not obj.data.uv_layers.active:
        raise ValueError('Collapse lost the original source UV atlas')
    uv = np.empty(len(obj.data.loops)*2, dtype=np.float32)
    obj.data.uv_layers.active.data.foreach_get('uv', uv)
    if not np.isfinite(uv).all() or not np.isfinite(points(obj.data)).all():
        raise ValueError('Nonfinite derived geometry or source UV interpolation')
    return {'inputTriangles': before, 'weightedPassTriangles': weighted_count, 'triangles': count, 'vertices': len(obj.data.vertices),
            'sourceAtlasRetained': True, 'looseVerticesRemoved': len(loose)}


def geometry_error(source_points, protected_points, obj, source_mesh):
    mesh = obj.data
    values = points(mesh)
    tree = BVHTree.FromPolygons(values.tolist(), [list(face.vertices) for face in mesh.polygons])
    # Check every source vertex, not a global P95 sample that can hide a whole
    # missing hand among the much denser head. Also check the opposite direction
    # inside every derived triangle: retained corner pins do not constrain the
    # intervening face, which can otherwise bridge empty space across fingers.
    errors = np.asarray([tree.find_nearest(Vector(point))[3] for point in source_points])
    protected = np.asarray([tree.find_nearest(Vector(point))[3] for point in protected_points])
    report = {'samples': len(errors), 'surfaceDistanceP95': float(np.quantile(errors, .95)),
              'surfaceDistanceP99': float(np.quantile(errors, .99)),
              'surfaceDistanceMaximum': float(errors.max()), 'protectedSamples': len(protected),
              'protectedDistanceMaximum': float(protected.max()),
              'maximumProtectedSourcePoint': protected_points[int(np.argmax(protected))].tolist()}
    def influences(vertex):
        return [(obj.vertex_groups[entry.group].name, float(entry.weight)) for entry in vertex.groups]
    worst_source = int(np.argmax(errors))
    report['maximumSourcePoint'] = source_points[worst_source].tolist()
    report['maximumSourcePointWeights'] = influences(source_mesh.vertices[worst_source])
    mesh.calc_loop_triangles()
    triangles = np.empty(len(mesh.loop_triangles)*3, dtype=np.int32)
    mesh.loop_triangles.foreach_get('vertices', triangles)
    triangles = triangles.reshape(-1, 3)
    corners = values[triangles]
    source_tree = BVHTree.FromPolygons(source_points.tolist(), [list(face.vertices) for face in source_mesh.polygons])
    reverse = []
    for label, interior in [('faceCentres', corners.mean(1))] + [
            ('edgeMidpoints' + str(a) + str(b), (corners[:, a]+corners[:, b])*.5) for a, b in ((0, 1), (1, 2), (2, 0))]:
        distances = np.asarray([source_tree.find_nearest(Vector(point))[3] for point in interior])
        reverse.append(distances)
        report[label + 'DistanceMaximum'] = float(distances.max())
        report[label + 'WorstPoint'] = interior[int(np.argmax(distances))].tolist()
    reverse = np.concatenate(reverse)
    report['reverseInteriorSamples'] = len(reverse)
    report['reverseInteriorDistanceP99'] = float(np.quantile(reverse, .99))
    report['reverseInteriorDistanceMaximum'] = float(reverse.max())
    edge_lengths = np.stack([np.linalg.norm(corners[:, a]-corners[:, b], axis=1)
                             for a, b in ((0, 1), (1, 2), (2, 0))], axis=1)
    longest_face = int(np.argmax(edge_lengths.max(1)))
    report['maximumTriangleEdge'] = float(edge_lengths.max())
    report['maximumEdgeTrianglePoints'] = corners[longest_face].tolist()
    report['maximumEdgeTriangleWeights'] = [influences(mesh.vertices[int(index)]) for index in triangles[longest_face]]
    kinds = np.zeros(len(values), dtype=np.int8)
    for vertex in mesh.vertices:
        weighted = influences(vertex)
        if any(name.endswith('_Wrist') and weight >= RECIPE['handInfluenceThreshold'] for name, weight in weighted):
            kinds[vertex.index] = 3
        elif any(name.startswith('Arm') and weight >= RECIPE['armInfluenceThreshold'] for name, weight in weighted):
            kinds[vertex.index] = 2
        elif any(name == 'Head' and weight >= .5 for name, weight in weighted):
            kinds[vertex.index] = 1
    face_kinds = kinds[triangles].max(1)
    report['regionalMaximumTriangleEdges'] = {
        name: float(edge_lengths[face_kinds == kind].max()) for kind, name in enumerate(('body', 'head', 'arms', 'hands'))
        if np.any(face_kinds == kind)}
    print('BAEKSUGWI_GAME_LOD_SURFACE_GUARD', json.dumps(report), flush=True)
    if (report['surfaceDistanceP95'] > RECIPE['maximumSurfaceErrorP95Metres'] or
            report['surfaceDistanceP99'] > RECIPE['maximumSurfaceErrorP99Metres'] or
            report['surfaceDistanceMaximum'] > RECIPE['maximumSurfaceErrorMetres']):
        raise ValueError('Derived surface changed too far from the generated source: ' + str(report))
    if (report['reverseInteriorDistanceP99'] > RECIPE['maximumReverseInteriorErrorP99Metres'] or
            report['reverseInteriorDistanceMaximum'] > RECIPE['maximumReverseInteriorErrorMetres'] or
            report['maximumTriangleEdge'] > RECIPE['maximumTriangleEdgeMetres']):
        raise ValueError('Derived triangles bridge or cut through the original curved surface: ' + str(report))
    if report['protectedDistanceMaximum'] > RECIPE['maximumProtectedSurfaceErrorMetres']:
        raise ValueError('Original eye/mouth/contact priority region moved too far: ' + str(report))
    return report


def pose_bounds(obj, rig, frames):
    result = []
    wrists = groups(obj, [bone.name for bone in rig.data.bones if bone.name.endswith('_Wrist')])
    old = rig.animation_data.action
    rig.animation_data.action = bpy.data.actions['Run_InPlace']
    for frame in frames:
        bpy.context.scene.frame_set(frame)
        evaluated = obj.evaluated_get(bpy.context.evaluated_depsgraph_get())
        mesh = evaluated.to_mesh()
        try:
            values = points(mesh)
            matrix = np.asarray(evaluated.matrix_world)
            world = values @ matrix[:3, :3].T + matrix[:3, 3]
            result.append({'frame': frame, 'minimum': world.min(0).tolist(), 'maximum': world.max(0).tolist(),
                           'extremeVertexIndices': np.concatenate((world.argmin(0), world.argmax(0))).tolist(),
                           'handMinimumZ': {name: float(world[ids, 2].min()) for name, ids in wrists.items() if len(ids)}})
        finally:
            evaluated.to_mesh_clear()
    rig.animation_data.action = old
    bpy.context.scene.frame_set(1)
    return result


def main():
    if bpy.app.version_string != RECIPE['blender']:
        raise RuntimeError('Use the pinned Blender ' + RECIPE['blender'])
    source = HERE / RECIPE['source']
    source_hash = sha(source)
    bpy.context.preferences.filepaths.save_version = 0
    bpy.ops.wm.open_mainfile(filepath=str(source))
    rig = next(obj for obj in bpy.context.scene.objects if obj.type == 'ARMATURE')
    originals = [obj for obj in bpy.context.scene.objects if obj.type == 'MESH' and
                 any(modifier.type == 'ARMATURE' and modifier.object == rig for modifier in obj.modifiers)]
    if len(originals) != 1 or len(rig.data.bones) != 63:
        raise ValueError('Expected the unchanged reviewed one-surface, 63-bone creature')
    high = originals[0]
    print('BAEKSUGWI_GAME_LOD_SOURCE_MODIFIERS', [(modifier.name, modifier.type) for modifier in high.modifiers], flush=True)
    actions = {action.name: action_hash(action) for action in bpy.data.actions
               if action.name.startswith('Run_') or action.name == 'Head_Writhe'}
    original_vertices = points(high.data)
    original_materials = list(high.data.materials)
    source_pose = pose_bounds(high, rig, RECIPE['poseFrames'])
    if '--inspect-source' in sys.argv:
        linked = np.zeros(len(high.data.vertices), dtype=np.int32)
        for face in high.data.polygons:
            linked[list(face.vertices)] += 1
        for pose in source_pose:
            print('SOURCE_POSE', json.dumps(pose), flush=True)
        for index in sorted({i for pose in source_pose for i in pose['extremeVertexIndices']}):
            vertex = high.data.vertices[index]
            print('SOURCE_EXTREME', index, 'faces', linked[index], 'co', list(vertex.co), 'weights',
                  [(high.vertex_groups[g.group].name, g.weight) for g in vertex.groups], flush=True)
        return
    derived = high.copy()
    derived.data = high.data.copy()
    bpy.context.collection.objects.link(derived)
    derived.name = 'Baeksugwi_LOD0'
    high.hide_set(True)
    high.hide_render = True
    select(derived)
    envelope_vertices = {index for pose in source_pose for index in pose['extremeVertexIndices']}
    priority, protected, contacts, protection_report = protection(derived, rig, envelope_vertices)
    identity = geometry_error(original_vertices, protected, derived, high.data)
    identity_pose = pose_bounds(derived, rig, RECIPE['poseFrames'])
    identity_error = max(float(np.max(np.abs(np.asarray(actual[key])-reference[key])))
                         for actual, reference in zip(identity_pose, source_pose) for key in ('minimum', 'maximum'))
    identity['poseEnvelopeMaximum'] = identity_error
    print('BAEKSUGWI_GAME_LOD_IDENTITY', json.dumps(identity), flush=True)
    if identity_error > .000001:
        raise ValueError('Identity copy failed the original pose-space control')
    (HERE/'game-lod-identity.json').write_text(json.dumps(identity, indent=2)+'\n', encoding='utf-8')
    if '--identity-only' in sys.argv:
        return
    # Weld only derived coincident seam vertices; loop UV corners remain distinct.
    editable = bmesh.new()
    editable.from_mesh(derived.data)
    distance = max(float(np.ptp(original_vertices, axis=0).max()) * RECIPE['weldRelativeDistance'], 1e-9)
    # Colocated UV corners can carry different original skin weights. Welding
    # those changes the animated surface even when rest geometry is identical.
    editable.verts.ensure_lookup_table()
    deform = editable.verts.layers.deform.active
    coincident = bmesh.ops.find_doubles(editable, verts=list(editable.verts), dist=distance)['targetmap']
    retained = {}
    weld_map = {}
    for vertex in editable.verts:
        anchor = coincident.get(vertex, vertex)
        signature = tuple(sorted((group, round(weight * 1048576)) for group, weight in vertex[deform].items()))
        key = (anchor.index, signature)
        if key in retained:
            weld_map[vertex] = retained[key]
        else:
            retained[key] = vertex
    if weld_map:
        bmesh.ops.weld_verts(editable, targetmap=weld_map)
    editable.to_mesh(derived.data)
    editable.free()
    print('BAEKSUGWI_GAME_LOD_WELD', len(original_vertices), '->', len(derived.data.vertices), 'distance=', distance, flush=True)
    results = []
    outputs = []
    previous = derived
    for level_index, level in enumerate(RECIPE['levels']):
        if level_index:
            derived = previous.copy()
            derived.data = previous.data.copy()
            bpy.context.collection.objects.link(derived)
            derived.name = 'Baeksugwi_' + level['name']
        if 'facePriorityWeight' in level:
            priority_index = derived.vertex_groups[priority].index
            exact_index = derived.vertex_groups['GameLOD_ExactPins'].index
            facial = [vertex.index for vertex in derived.data.vertices
                      if any(entry.group == priority_index and entry.weight >= .999 for entry in vertex.groups)
                      and not any(entry.group == exact_index and entry.weight > .5 for entry in vertex.groups)]
            if facial:
                derived.vertex_groups[priority].add(facial, level['facePriorityWeight'], 'REPLACE')
        result = reduce_mesh(derived, level['triangles'], level['maximumTriangles'], level['protectionStrength'], priority)
        result['level'] = level['name']
        print('BAEKSUGWI_GAME_LOD_REDUCED', json.dumps(result), flush=True)
        if '--inspect-weights' in sys.argv:
            pre = pose_bounds(derived, rig, RECIPE['poseFrames'])
            print('PRE_NORMALIZATION_ENVELOPE_ERROR', max(float(np.max(np.abs(np.asarray(a[k])-b[k])))
                  for a,b in zip(pre,source_pose) for k in ('minimum','maximum')), flush=True)
        result['skin'] = normalize_weights(derived, rig)
        result['surfaceError'] = geometry_error(original_vertices, protected, derived, high.data)
        result['smoothFaceFraction'] = sum(face.use_smooth for face in derived.data.polygons)/len(derived.data.polygons)
        if result['smoothFaceFraction'] < .99:
            raise ValueError('Derived export lost the original smooth surface shading')
        assignment = groups(derived, list(contacts))
        values = points(derived.data)
        contact_error = 0.0
        for name, probe in contacts.items():
            if len(assignment[name]) < 1:
                raise ValueError('Decimation lost an original hand: ' + name)
            contact_error = max(contact_error, abs(float(values[assignment[name], 2].min())-probe['sourceMinimumZ']))
        result['maximumRestHandUndersideError'] = contact_error
        if contact_error > RECIPE['maximumContactErrorMetres']:
            raise ValueError('Derived hand no longer preserves its original contact surface: ' + str(result))
        result['poseBounds'] = pose_bounds(derived, rig, RECIPE['poseFrames'])
        envelope_error = max(float(np.max(np.abs(np.asarray(actual[key])-reference[key])))
                             for actual, reference in zip(result['poseBounds'], source_pose)
                             for key in ('minimum', 'maximum'))
        posed_contact_error = max(abs(actual['handMinimumZ'][name]-reference['handMinimumZ'][name])
                                 for actual, reference in zip(result['poseBounds'], source_pose)
                                 for name in contacts)
        result['maximumPoseEnvelopeError'] = envelope_error
        result['maximumPosedHandUndersideError'] = posed_contact_error
        if envelope_error > RECIPE['maximumPoseEnvelopeErrorMetres'] or posed_contact_error > RECIPE['maximumContactErrorMetres']:
            raise ValueError('Derived run pose/contact differs from the original rig: ' + str(result))
        if list(derived.data.materials) != original_materials:
            raise ValueError('Derived mesh replaced the generated PBR material')
        outputs.append(derived)
        results.append(result)
        previous = derived
        print('BAEKSUGWI_GAME_LOD_LEVEL', json.dumps({key: value for key,value in result.items() if key != 'poseBounds'}), flush=True)
    if actions != {action.name: action_hash(action) for action in bpy.data.actions if action.name in actions}:
        raise ValueError('Original rig animation curves changed')
    # The full editable original already exists separately; do not duplicate it
    # inside the much smaller derived .blend, or export it accidentally.
    for obj in list(bpy.context.scene.objects):
        if obj not in outputs + [rig]:
            bpy.data.objects.remove(obj, do_unlink=True)
    for mesh in list(bpy.data.meshes):
        if mesh.users == 0:
            bpy.data.meshes.remove(mesh)
    for obj in outputs:
        for name in (priority, 'GameLOD_ExactPins'):
            group = obj.vertex_groups.get(name)
            if group:
                obj.vertex_groups.remove(group)
    rig.data.pose_position = 'POSE'
    rig.animation_data.action = bpy.data.actions['Run_InPlace']
    bpy.context.scene.frame_set(1)
    bpy.ops.object.select_all(action='DESELECT')
    for obj in outputs + [rig]:
        obj.hide_set(False)
        obj.hide_render = False
        obj.select_set(True)
    bpy.context.view_layer.objects.active = rig
    target = HERE / RECIPE['outputFbx']
    bpy.ops.export_scene.fbx(filepath=str(target), use_selection=True, object_types={'MESH', 'ARMATURE'},
        axis_forward='-Z', axis_up='Y', apply_unit_scale=True, apply_scale_options='FBX_SCALE_UNITS',
        global_scale=1, add_leaf_bones=False, use_armature_deform_only=True, bake_anim=False,
        mesh_smooth_type='FACE', use_tspace=True, path_mode='RELATIVE', embed_textures=False)
    # Existing Unity Run/Head clips bind to the same hierarchy. No re-exported
    # take may replace them; the editable derived blend still retains all actions.
    blend = HERE / RECIPE['outputBlend']
    bpy.ops.wm.save_as_mainfile(filepath=str(blend), compress=True, check_existing=False)
    if sha(source) != source_hash:
        raise ValueError('Original authoring source was unexpectedly modified')
    report = {'status': 'PASS', 'blender': bpy.app.version_string, 'source': source.name,
              'sourceSHA256': source_hash, 'recipeSHA256': sha(RECIPE_PATH), 'bones': len(rig.data.bones),
              'originalVertices': len(original_vertices), 'originalActionsPreserved': actions,
              'generatedPbrMaterialsUnchanged': [material.name for material in original_materials],
              'protection': protection_report, 'sourcePoseBounds': source_pose, 'levels': results,
              'blend': blend.name, 'blendSHA256': sha(blend), 'fbx': target.name, 'fbxSHA256': sha(target),
              'note': 'Derived authoring candidate only; Unity import, visual comparison and performance validation remain separate.'}
    (HERE / RECIPE['report']).write_text(json.dumps(report, indent=2) + '\n', encoding='utf-8')
    print('BAEKSUGWI_GAME_LOD_SOURCE_PASS', flush=True)


if __name__ == '__main__':
    main()
