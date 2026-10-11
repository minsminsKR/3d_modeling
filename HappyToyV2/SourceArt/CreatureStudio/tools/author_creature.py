"""Author a NEW generated humanoid rig and its game LOD in Blender 4.0.2.

blender --background --factory-startup --python author_creature.py --
  --creature Uncat --source ../Uncat/source-rigged.glb --map ../Uncat/joint-map.json

An explicit reviewed semantic mapping is required for unnamed AI bones. No old
HappyToy mesh or material is imported. The original high-resolution source stays
editable; the game mesh alone is decimated and receives a real geometry normal bake.
"""
import argparse
import hashlib
import importlib.util
import json
import math
import sys
from pathlib import Path
import bpy
import bmesh
import numpy as np
from mathutils import Matrix, Vector

HERE = Path(__file__).resolve().parent
STUDIO = HERE.parent
PROJECT = STUDIO.parents[1]
REQUIRED = ['Hips', 'Spine', 'Spine1', 'Head', 'LeftArm', 'LeftForeArm', 'LeftHand',
            'RightArm', 'RightForeArm', 'RightHand', 'LeftUpLeg', 'LeftLeg', 'LeftFoot',
            'RightUpLeg', 'RightLeg', 'RightFoot']
NEXT = {'LeftArm': 'LeftForeArm', 'LeftForeArm': 'LeftHand', 'RightArm': 'RightForeArm',
        'RightForeArm': 'RightHand', 'LeftUpLeg': 'LeftLeg', 'LeftLeg': 'LeftFoot',
        'RightUpLeg': 'RightLeg', 'RightLeg': 'RightFoot', 'LeftFoot': 'LeftToeBase',
        'RightFoot': 'RightToeBase'}


def say(message):
    print('[Creature Studio] '+message, flush=True)


def digest(path):
    result = hashlib.sha256()
    with Path(path).open('rb') as stream:
        for block in iter(lambda: stream.read(8*1024*1024), b''):
            result.update(block)
    return result.hexdigest()


def points(obj):
    result = np.empty(len(obj.data.vertices)*3, dtype=np.float32)
    obj.data.vertices.foreach_get('co', result)
    result = result.reshape(-1, 3)
    matrix = np.asarray(obj.matrix_world)
    return result@matrix[:3, :3].T+matrix[:3, 3]


def renamed_rig(rig, meshes, mapping):
    explicit = mapping.get('map', {})
    if not explicit:
        explicit = {bone.name.rsplit(':', 1)[-1]: bone.name for bone in rig.data.bones
                    if bone.name.rsplit(':', 1)[-1] in REQUIRED+list(NEXT.values())+
                    ['Neck', 'Spine2', 'LeftShoulder', 'RightShoulder']}
    missing = [name for name in REQUIRED if name not in explicit]
    if missing:
        raise ValueError('Reviewed semantic bone mapping is missing: '+', '.join(missing))
    if len(set(explicit.values())) != len(explicit):
        raise ValueError('Two semantic joints cannot reference the same source bone')
    unknown = sorted(set(explicit.values())-set(rig.data.bones.keys()))
    if unknown:
        raise ValueError('Source rig lacks mapped bones: '+', '.join(unknown))
    source_to_target = {source: 'mixamorig:'+semantic for semantic, source in explicit.items()}
    for bone in rig.data.bones:
        source_to_target.setdefault(bone.name, 'Aux_'+bone.name.replace(':', '_'))
    original = [(bone, bone.name) for bone in rig.data.bones]
    for index, (bone, _) in enumerate(original):
        bone.name = '_StudioTemporary_'+str(index)
    for bone, old in original:
        bone.name = source_to_target[old]
    for mesh in meshes:
        for group in mesh.vertex_groups:
            if group.name in source_to_target:
                group.name = source_to_target[group.name]
    rig.animation_data_clear()
    for action in list(bpy.data.actions):
        bpy.data.actions.remove(action)
    bpy.context.view_layer.objects.active = rig
    bpy.ops.object.select_all(action='DESELECT')
    rig.select_set(True)
    bpy.ops.object.mode_set(mode='EDIT')
    for start, end in NEXT.items():
        first, last = rig.data.edit_bones.get('mixamorig:'+start), rig.data.edit_bones.get('mixamorig:'+end)
        if first and last:
            if (last.head-first.head).length < 1e-5:
                raise ValueError('Zero length anatomical link: '+start+' -> '+end)
            first.tail = last.head
            if last.parent == first:
                last.use_connect = True
    bpy.ops.object.mode_set(mode='OBJECT')
    rig.name = mapping.get('creature', 'Creature')+'_Rig'
    return source_to_target


def bind_reviewed_body_plan(plan, arrays, meshes):
    """Use the repository's reviewed seam-safe binder on original new geometry."""
    helper_path = PROJECT/'SourceArt'/'BaeksugwiStudio'/'author_blender.py'
    sys.path.insert(0, str(helper_path.parent))
    spec = importlib.util.spec_from_file_location('creature_surface_bind_helpers', helper_path)
    helper = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(helper)
    names = [str(value) for value in arrays['bone_names']]
    if names != [bone['name'] for bone in plan['bones']]:
        raise ValueError('Reviewed body plan and surface weight palette order differ')
    data = bpy.data.armatures.new(plan['creature']+'_ReviewedBodyRig')
    rig = bpy.data.objects.new(plan['creature']+'_Rig', data)
    bpy.context.collection.objects.link(rig)
    bpy.context.view_layer.objects.active = rig
    bpy.ops.object.select_all(action='DESELECT')
    rig.select_set(True)
    bpy.ops.object.mode_set(mode='EDIT')
    created = {}
    for entry in plan['bones']:
        bone = data.edit_bones.new(entry['name'])
        bone.head = Vector((entry['head'][0], -entry['head'][2], entry['head'][1]))
        bone.tail = Vector((entry['tail'][0], -entry['tail'][2], entry['tail'][1]))
        created[entry['name']] = bone
    for entry in plan['bones']:
        if entry['parent']:
            created[entry['name']].parent = created[entry['parent']]
    bpy.ops.object.mode_set(mode='OBJECT')
    rig.show_in_front = True
    quantized, quant_report = helper.quantize_weights(arrays['weights'])
    correspondences = helper.source_mapping(meshes, arrays['positions'])
    report = {'bones': len(names), 'quantization': quant_report, 'sourceAIWeightsReused': False, 'meshes': {}}
    for mesh in meshes:
        ids, error, method = correspondences[mesh.name]
        before = helper.mesh_snapshot(mesh)
        binding = helper.bind_mesh(mesh, rig, ids, arrays['joints'], quantized, names)
        after = helper.mesh_snapshot(mesh)
        for key in ('vertices', 'triangles', 'loops', 'uv', 'materials'):
            if before[key] != after[key]:
                raise AssertionError('Rebinding changed original source '+key)
        report['meshes'][mesh.name] = {'correspondence': method, 'maximumPositionError': error,
                                      'binding': binding, 'originalGeometryAndUVUnchanged': True}
    return rig, report


def normalize_rest(rig, meshes, height):
    rig.data.pose_position = 'REST'
    values = np.concatenate([points(mesh) for mesh in meshes])
    low, high = values.min(0), values.max(0)
    if high[2]-low[2] < .01:
        raise ValueError('Model has no upright vertical extent')
    scale = float(height/(high[2]-low[2]))
    center = Vector(((low[0]+high[0])/2, (low[1]+high[1])/2, low[2]))
    change = Matrix.Scale(scale, 4)@Matrix.Translation(-center)
    targets = {obj: change@obj.matrix_world.copy() for obj in meshes+[rig]}
    for obj in meshes:
        obj.parent = None
    for obj in meshes+[rig]:
        obj.matrix_world = targets[obj]
        bpy.ops.object.select_all(action='DESELECT')
        obj.select_set(True)
        bpy.context.view_layer.objects.active = obj
        bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    for mesh in meshes:
        mesh.parent = rig
        mesh.matrix_parent_inverse = Matrix.Identity(4)
        for modifier in mesh.modifiers:
            if modifier.type == 'ARMATURE':
                modifier.object = rig
                modifier.use_deform_preserve_volume = False
    rig.data.pose_position = 'POSE'
    return {'targetHeight': height, 'sourceBounds': [low.tolist(), high.tolist()], 'uniformScale': scale,
            'floor': 0, 'front': '-Y'}


def split_vertex_estimate(mesh):
    mesh.calc_loop_triangles()
    count = len(mesh.loops)
    ids = np.empty(count, dtype=np.int32)
    normals = np.empty(count*3, dtype=np.float32)
    uv = np.empty(count*2, dtype=np.float32)
    mesh.loops.foreach_get('vertex_index', ids)
    mesh.loops.foreach_get('normal', normals)
    if not mesh.uv_layers.active:
        raise ValueError('Game mesh lost its source UV atlas')
    mesh.uv_layers.active.data.foreach_get('uv', uv)
    keys = np.column_stack((ids, np.round(normals.reshape(-1, 3)*1e5), np.round(uv.reshape(-1, 2)*1e6)))
    return len(np.unique(keys, axis=0))


def derive_missing_toes(rig, high):
    """Provide foley toe anchors from actual source forefoot surface, if needed."""
    values = points(high)
    derived = []
    for side in ('Left', 'Right'):
        target = 'mixamorig:'+side+'ToeBase'
        if target in rig.data.bones:
            continue
        foot = rig.data.bones['mixamorig:'+side+'Foot']
        branch = {foot.name} | {child.name for child in foot.children_recursive}
        groups = {group.index for group in high.vertex_groups if group.name in branch}
        ids = [vertex.index for vertex in high.data.vertices
               if sum(entry.weight for entry in vertex.groups if entry.group in groups) >= .5]
        if len(ids) < 8:
            raise ValueError('Cannot derive a real toe anchor from source foot skin: '+side)
        surface = values[ids]
        forefoot = surface[surface[:, 1] <= np.percentile(surface[:, 1], 12)]
        head = Vector(forefoot.mean(0))
        head = rig.matrix_world.inverted()@head
        length = max(.01, foot.length*.16)
        bpy.context.view_layer.objects.active = rig
        bpy.ops.object.select_all(action='DESELECT')
        rig.select_set(True)
        bpy.ops.object.mode_set(mode='EDIT')
        parent = rig.data.edit_bones['mixamorig:'+side+'Foot']
        bone = rig.data.edit_bones.new(target)
        bone.head = head
        bone.tail = head+Vector((0, -length, 0))
        bone.parent = parent
        bone.use_deform = True
        parent.tail = head
        bpy.ops.object.mode_set(mode='OBJECT')
        derived.append({'bone': target, 'head': list(head), 'sourceForefootVertices': len(forefoot),
                        'method': 'mean of front 12% actual weighted source foot surface; original Foot skin retained'})
    return derived


def limit_weights(obj, rig):
    bone_groups = {group.index for group in obj.vertex_groups if group.name in rig.data.bones}
    entries, unbound = [], []
    for vertex in obj.data.vertices:
        influence = sorted([(group.group, group.weight) for group in vertex.groups
                            if group.group in bone_groups and group.weight>1e-8], key=lambda item: (-item[1], item[0]))[:4]
        total = sum(weight for _, weight in influence)
        if total < 1e-7:
            unbound.append(vertex.index)
            continue
        scaled = np.array([weight/total*1024 for _, weight in influence])
        bins = np.floor(scaled).astype(int)
        remainder = 1024-int(bins.sum())
        for index in np.argsort(-(scaled-bins), kind='stable')[:remainder]:
            bins[index] += 1
        entries.extend((group, int(value), vertex.index) for (group, _), value in zip(influence, bins) if value)
    if unbound:
        raise ValueError(f'{obj.name}: {len(unbound)} game vertices have no valid source skin weight')
    for group in obj.vertex_groups:
        group.remove(range(len(obj.data.vertices)))
    batches = {}
    for group, value, vertex in entries:
        batches.setdefault((group, value), []).append(vertex)
    for (group, value), vertices in batches.items():
        obj.vertex_groups[group].add(vertices, value/1024, 'REPLACE')
    return {'maximumInfluences': 4, 'weightBins': 1024, 'unboundVertices': 0}


def create_lod(high, rig, key, triangle_budget):
    lod = high.copy()
    lod.data = high.data.copy()
    lod.name = key+'_LOD0'
    bpy.context.collection.objects.link(lod)
    lod.hide_render = False
    lod.hide_set(False)
    bpy.ops.object.select_all(action='DESELECT')
    lod.select_set(True)
    bpy.context.view_layer.objects.active = lod
    lod.data.calc_loop_triangles()
    modifier = lod.modifiers.new('Game-only topology reduction', 'DECIMATE')
    modifier.ratio = min(1, triangle_budget/len(lod.data.loop_triangles))
    modifier.use_collapse_triangulate = True
    bpy.ops.object.modifier_move_up(modifier=modifier.name)
    while lod.modifiers[0] != modifier:
        bpy.ops.object.modifier_move_up(modifier=modifier.name)
    bpy.ops.object.modifier_apply(modifier=modifier.name)
    topology = clean_game_topology(lod)
    for polygon in lod.data.polygons:
        polygon.use_smooth = True
    skin_report = limit_weights(lod, rig)
    lod.data.calc_loop_triangles()
    triangles = len(lod.data.loop_triangles)
    split_vertices = split_vertex_estimate(lod.data)
    if triangles > triangle_budget*1.05 or split_vertices > 80000:
        raise ValueError(f'LOD budget exceeded: {triangles} triangles, estimated {split_vertices} Unity vertices')
    return lod, {'vertices': len(lod.data.vertices), 'triangles': triangles, 'topologyCleanup': topology,
                 'estimatedUnitySplitVertices': split_vertices, 'skin': skin_report}


def clean_game_topology(lod):
    """Remove decimation debris before any normals, bake, save or export caches.

    AI surface UV seams leave many loose points after collapse, and duplicate
    original microfaces can collapse onto a repeated triangle. Export validation
    after baking is too late: it invalidates cached loop-normal array lengths.
    The editable high-resolution source is never touched by this cleanup.
    """
    mesh = lod.data
    before = {'vertices': len(mesh.vertices), 'faces': len(mesh.polygons), 'loops': len(mesh.loops)}
    bpy.context.view_layer.objects.active = lod
    bpy.ops.object.select_all(action='DESELECT')
    lod.select_set(True)
    if mesh.has_custom_normals:
        bpy.ops.mesh.customdata_custom_splitnormals_clear()
    repaired = mesh.validate(verbose=False, clean_customdata=True)
    editable = bmesh.new()
    editable.from_mesh(mesh)
    loose = [vertex for vertex in editable.verts if not vertex.link_faces]
    if loose:
        bmesh.ops.delete(editable, geom=loose, context='VERTS')
    editable.to_mesh(mesh)
    editable.free()
    mesh.update()
    if mesh.validate(verbose=False, clean_customdata=True):
        raise ValueError('Game topology is still invalid after reviewed decimation cleanup')
    if any(not np.isfinite(vertex.co).all() for vertex in mesh.vertices):
        raise ValueError('Game mesh contains non-finite geometry')
    return {'before': before, 'after': {'vertices': len(mesh.vertices), 'faces': len(mesh.polygons), 'loops': len(mesh.loops)},
            'duplicateOrDegenerateFacesRemoved': repaired, 'looseVerticesRemoved': len(loose),
            'customNormalsRecomputedForNewTopology': True, 'editableHighUnchanged': True}


def bake_geometry_normal(high, lod, destination, size, height):
    scene = bpy.context.scene
    scene.render.engine = 'CYCLES'
    scene.cycles.samples = 1
    scene.cycles.device = 'CPU'
    scene.render.bake.use_selected_to_active = True
    scene.render.bake.use_cage = False
    scene.render.bake.cage_extrusion = height*.004
    scene.render.bake.max_ray_distance = height*.008
    scene.render.bake.margin = 12
    image = bpy.data.images.new(lod.name+'_geometryNormal', width=size, height=size, alpha=False)
    image.colorspace_settings.name = 'Non-Color'
    originals = list(lod.data.materials)
    lod.data.materials.clear()
    for index, original in enumerate(originals):
        material = original.copy()
        material.name = lod.name+'_BakeMaterial'+str(index)
        lod.data.materials.append(material)
        nodes = material.node_tree.nodes
        target = nodes.new('ShaderNodeTexImage')
        target.image = image
        nodes.active = target
        for node in nodes:
            node.select = node == target
    bpy.ops.object.select_all(action='DESELECT')
    high.hide_set(False)
    high.hide_render = False
    high.select_set(True)
    lod.hide_set(False)
    lod.hide_render = False
    lod.select_set(True)
    bpy.context.view_layer.objects.active = lod
    rig = next(modifier.object for modifier in lod.modifiers if modifier.type == 'ARMATURE')
    rig.data.pose_position = 'REST'
    bpy.ops.object.bake(type='NORMAL', normal_space='TANGENT')
    image.filepath_raw = str(destination)
    image.file_format = 'PNG'
    image.save()
    rig.data.pose_position = 'POSE'
    return {'method': 'Cycles selected-high-to-active-LOD tangent normal geometry bake',
            'size': [size, size], 'rayDistance': height*.008, 'cageExtrusion': height*.004}


def prepare_game_materials(lod, destination, size):
    """Duplicate/downsample images only in the game material; keep source 8K."""
    copied = {}
    report = []
    normal = bpy.data.images.load(str(destination/'normal.png'), check_existing=True)
    normal.colorspace_settings.name = 'Non-Color'

    def linked_image(socket, visited=None):
        visited = visited or set()
        for link in socket.links:
            node = link.from_node
            if node in visited:
                continue
            visited.add(node)
            if node.type == 'TEX_IMAGE' and node.image:
                return node.image
            for entry in node.inputs:
                result = linked_image(entry, visited)
                if result:
                    return result
        return None

    for slot, material in enumerate(lod.data.materials):
        material = material.copy()
        lod.data.materials[slot] = material
        material.name = lod.name+'_GamePBR'+str(slot)
        for node in material.node_tree.nodes:
            if node.type != 'TEX_IMAGE' or not node.image:
                continue
            original = node.image
            if original not in copied:
                dimensions = tuple(original.size)
                image = original.copy()
                image.name = original.name+'_Game'+str(size)
                if max(image.size) > size:
                    scale = size/max(image.size)
                    image.scale(max(1, round(image.size[0]*scale)), max(1, round(image.size[1]*scale)))
                if tuple(original.size) != dimensions:
                    raise AssertionError('Downsampling unexpectedly modified the source image')
                copied[original] = image
            node.image = copied[original]
        bsdf = next(node for node in material.node_tree.nodes if node.type == 'BSDF_PRINCIPLED')
        albedo = linked_image(bsdf.inputs['Base Color'])
        if not albedo:
            raise ValueError('New source game material lacks its generated base-color image')
        name_prefix = '' if slot == 0 else f'material{slot}-'
        albedo.filepath_raw = str(destination/(name_prefix+'albedo.png'))
        albedo.file_format = 'PNG'
        albedo.save()
        packed = linked_image(bsdf.inputs['Metallic']) or linked_image(bsdf.inputs['Roughness'])
        if packed:
            pixels = np.empty(packed.size[0]*packed.size[1]*4, dtype=np.float32)
            packed.pixels.foreach_get(pixels)
            pixels = pixels.reshape(-1, 4)
            unity = np.zeros_like(pixels)
            unity[:, 0] = pixels[:, 2]
            unity[:, 3] = 1-pixels[:, 1]
            metallic = bpy.data.images.new(lod.name+'_UnityMetallic'+str(slot), width=packed.size[0], height=packed.size[1], alpha=True)
            metallic.colorspace_settings.name = 'Non-Color'
            metallic.pixels.foreach_set(unity.ravel())
        else:
            metallic = bpy.data.images.new(lod.name+'_UnityMetallic'+str(slot), width=size, height=size, alpha=True)
            metallic.colorspace_settings.name = 'Non-Color'
            pixels = np.zeros((size*size, 4), dtype=np.float32)
            pixels[:, 0] = bsdf.inputs['Metallic'].default_value
            pixels[:, 3] = 1-bsdf.inputs['Roughness'].default_value
            metallic.pixels.foreach_set(pixels.ravel())
        metallic.filepath_raw = str(destination/(name_prefix+'metallic-smoothness.png'))
        metallic.file_format = 'PNG'
        metallic.save()
        texture = material.node_tree.nodes.new('ShaderNodeTexImage')
        texture.image = normal
        normal_map = material.node_tree.nodes.new('ShaderNodeNormalMap')
        material.node_tree.links.new(texture.outputs['Color'], normal_map.inputs['Color'])
        material.node_tree.links.new(normal_map.outputs['Normal'], bsdf.inputs['Normal'])
        report.append({'material': slot, 'albedo': list(albedo.size), 'normal': list(normal.size),
                       'metallicSmoothness': list(metallic.size), 'source8KPreserved': True})
    return report


def contact_specs(rig, lod):
    result = {'floor': 0, 'coordinateSystem': 'Blender Z-up / front -Y', 'measurements': {}}
    values = points(lod)
    for suffix in ('LeftHand', 'RightHand', 'LeftFoot', 'RightFoot'):
        group = lod.vertex_groups.get('mixamorig:'+suffix)
        if not group:
            raise ValueError('Missing contact group: '+suffix)
        root = rig.data.bones['mixamorig:'+suffix]
        group_names = {root.name} | {bone.name for bone in root.children_recursive}
        groups = {entry.index for entry in lod.vertex_groups if entry.name in group_names}
        ids = [vertex.index for vertex in lod.data.vertices
               if sum(entry.weight for entry in vertex.groups if entry.group in groups) >= .50]
        if len(ids) < 8:
            raise ValueError('Too few influenced source contact vertices: '+suffix)
        subset = values[ids]
        joint = np.array(root.head_local)
        finger = np.array(root.tail_local-root.head_local)
        finger /= np.linalg.norm(finger)
        if suffix.endswith('Hand'):
            # A dangling A-pose fingertip is not a crawling palm contact. Fit
            # the near-wrist palm volume, then measure its outward plane.
            along = (subset-joint)@finger
            palm = subset[(along > 0)&(along < max(root.length*.8, .03))]
            if len(palm) < 8:
                raise ValueError('No measurable near-wrist palm surface: '+suffix)
            covariance = np.cov(palm.T)
            _, vectors = np.linalg.eigh(covariance)
            normal = vectors[:, 0]
            normal -= finger*np.dot(normal, finger)
            normal /= np.linalg.norm(normal)
            if normal[1] > 0:
                normal *= -1
            plane = (palm-joint)@normal
            surface = palm[plane >= np.percentile(plane, 85)]
            entry = {'contact_point': surface.mean(0).tolist(), 'palm_normal': normal.tolist(),
                     'finger_direction': finger.tolist()}
            kind = 'near-wrist palm-plane estimate; inspect posed palm skin before accepting grounding'
        else:
            underside = subset[subset[:, 2] <= subset[:, 2].min()+.006]
            entry = {'contact_point': underside.mean(0).tolist()}
            kind = 'actual sole/toe underside in rest mesh'
        key = ('left_' if suffix.startswith('Left') else 'right_')+('hand' if suffix.endswith('Hand') else 'foot')
        result[key] = entry
        result['measurements'][key] = {'joint': joint.tolist(), 'method': kind,
                                      'surface_min_z': float(subset[:, 2].min()), 'vertices': len(ids)}
    for side in ('Left', 'Right'):
        knee = rig.data.bones['mixamorig:'+side+'Leg']
        thigh = rig.data.bones['mixamorig:'+side+'UpLeg']
        ankle = rig.data.bones['mixamorig:'+side+'Foot']
        center = np.array(knee.head_local)
        axis = np.array(ankle.head_local-knee.head_local)
        length = np.linalg.norm(axis)
        axis /= length
        branch = {thigh.name} | {bone.name for bone in thigh.children_recursive}
        groups = {group.index for group in lod.vertex_groups if group.name in branch}
        ids = [vertex.index for vertex in lod.data.vertices
               if sum(entry.weight for entry in vertex.groups if entry.group in groups) >= .5
               and np.linalg.norm(values[vertex.index]-center) < length*.45]
        if len(ids) < 8:
            raise ValueError('Actual weighted knee volume is not measurable: '+side)
        offset = values[ids]-center
        perpendicular = offset-(offset@axis)[:, None]*axis
        radius = float(np.percentile(np.linalg.norm(perpendicular, axis=1), 95))
        key = side.lower()+'_knee'
        result[key] = {'clearance': radius}
        result['measurements'][key] = {'joint': center.tolist(), 'vertices': len(ids),
                                      'method': '95th percentile radial actual weighted knee surface',
                                      'radius': radius}
    return result


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--creature', choices=['Uncat', 'Cyclopse', 'Baby'], required=True)
    parser.add_argument('--source', type=Path)
    parser.add_argument('--map', type=Path)
    parser.add_argument('--plan', type=Path)
    parser.add_argument('--weights', type=Path)
    parser.add_argument('--triangles', type=int, default=60000)
    parser.add_argument('--texture-size', type=int, choices=[2048, 4096], default=2048)
    parser.add_argument('--animation-module', type=Path)
    parser.add_argument('--game-glb', type=Path)
    parser.add_argument('--skip-export', action='store_true')
    parser.add_argument('--skip-normal', action='store_true', help='Inspection-only; marks the delivery incomplete')
    args = parser.parse_args(sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else [])
    folder = STUDIO/args.creature
    plan_path = args.plan or folder/'body-rig-plan.json'
    body_plan = json.loads(plan_path.read_text(encoding='utf8')) if plan_path.is_file() else None
    source = args.source or folder/('source-original.glb' if body_plan else 'source-rigged.glb')
    mapping_path = args.map or folder/'joint-map.json'
    mapping = body_plan if body_plan else json.loads(mapping_path.read_text(encoding='utf8'))
    mapping['creature'] = args.creature
    height = float(mapping.get('target_height_m', {'Uncat': 2.2, 'Cyclopse': 2.38, 'Baby': 1.48}[args.creature]))
    output = PROJECT/'Assets'/'Resources'/'EnemyStudio'/args.creature
    output.mkdir(parents=True, exist_ok=True)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    say('Importing NEW generated AI rig: '+source.name)
    bpy.ops.import_scene.gltf(filepath=str(source), merge_vertices=False)
    rigs = [obj for obj in bpy.context.scene.objects if obj.type == 'ARMATURE']
    high_meshes = [obj for obj in bpy.context.scene.objects if obj.type == 'MESH'
                   and (body_plan or any(modifier.type == 'ARMATURE' and modifier.object in rigs for modifier in obj.modifiers))]
    if len(high_meshes) != 1 or (not body_plan and len(rigs) != 1):
        raise ValueError('Review/merge multi-rig or multi-mesh source explicitly before using this authoring entry point')
    high = high_meshes[0]
    high.name = args.creature+'_EditableHigh'
    report = {'creature': args.creature, 'input': source.name, 'sourceSHA256': digest(source),
              'tool': bpy.app.version_string, 'oldMeshImported': False}
    if body_plan:
        weights_path = args.weights or folder/'body-surface-weights.npz'
        with np.load(weights_path, allow_pickle=False) as payload:
            arrays = {key: payload[key].copy() for key in payload}
        rig, report['reviewedBodyBinding'] = bind_reviewed_body_plan(body_plan, arrays, [high])
        report['bodyPlan'] = plan_path.name
        report['sourceAIUsedForSkin'] = False
    else:
        rig = rigs[0]
        report['semanticMap'] = renamed_rig(rig, [high], mapping)
        report['sourceAIUsedForSkin'] = True
    report['normalization'] = normalize_rest(rig, [high], height)
    report['derivedToeAnchors'] = derive_missing_toes(rig, high)
    say('Reducing only the game mesh to the triangle budget')
    rig.data.pose_position = 'REST'
    lod, report['gameLOD'] = create_lod(high, rig, args.creature, args.triangles)
    if not args.skip_normal:
        say('Baking real high-resolution surface normals onto the game LOD')
        report['normal'] = bake_geometry_normal(high, lod, output/'normal.png', args.texture_size, height)
        report['gamePBR'] = prepare_game_materials(lod, output, args.texture_size)
    report['contacts'] = contact_specs(rig, lod)
    for key, override in mapping.get('contact_overrides', {}).items():
        report['contacts'].setdefault(key, {}).update(override)
    (folder/'contact-specs.json').write_text(json.dumps(report['contacts'], indent=2), encoding='utf8')
    rig.data.pose_position = 'POSE'
    high.hide_render = True
    high.hide_set(True)
    if args.animation_module:
        module_spec = importlib.util.spec_from_file_location('creature_motion', args.animation_module)
        module = importlib.util.module_from_spec(module_spec)
        module_spec.loader.exec_module(module)
        report['animation'] = module.apply_creature_animation(rig, args.creature, report['contacts'],
            fps=60, ground_height=0, report_path=str(folder/'animation-report.json'),
            profile_overrides=mapping.get('animation_profile_overrides'))
    expected = {'patrol', 'chase'} | ({'cry'} if args.creature == 'Baby' else set())
    actions = {action.name for action in bpy.data.actions if action.users > 0 or action.use_fake_user}
    report['actions'] = sorted(actions)
    if not args.skip_export and (args.skip_normal or not expected.issubset(actions)):
        raise ValueError('Game export needs real normal bake and exact patrol/chase/(Baby cry) actions')
    bpy.context.preferences.filepaths.save_version = 0
    bpy.ops.file.pack_all()
    blend = folder/(args.creature+'_Editable.blend')
    blend.parent.mkdir(parents=True, exist_ok=True)
    say('Saving editable source '+repr(str(blend))+'; parentExists='+str(blend.parent.is_dir()))
    bpy.ops.wm.save_as_mainfile(filepath=str(blend), compress=True, check_existing=False, relative_remap=False)
    if not args.skip_export:
        # Editable high source stays fully rigged in the saved .blend. Exports
        # sample only the selected game LOD; disable hidden million-vertex skin
        # evaluation during every animation frame to avoid unnecessary work.
        for modifier in high.modifiers:
            if modifier.type == 'ARMATURE':
                modifier.show_viewport = False
        bpy.ops.object.select_all(action='DESELECT')
        rig.select_set(True)
        lod.select_set(True)
        bpy.context.view_layer.objects.active = rig
        rig.animation_data.action = bpy.data.actions['patrol']
        bpy.context.scene.frame_set(1)
        fbx = output/(args.creature+'.fbx')
        bpy.ops.export_scene.fbx(filepath=str(fbx), use_selection=True, object_types={'MESH', 'ARMATURE'},
            axis_forward='-Z', axis_up='Y', global_scale=1, apply_unit_scale=True,
            apply_scale_options='FBX_SCALE_UNITS', add_leaf_bones=False, use_armature_deform_only=True,
            bake_anim=True, bake_anim_use_all_actions=True, bake_anim_use_nla_strips=False,
            bake_anim_simplify_factor=0, bake_anim_step=1, mesh_smooth_type='FACE', use_tspace=True,
            path_mode='AUTO', embed_textures=False)
        report['fbx'] = {'file': fbx.relative_to(PROJECT).as_posix(), 'sha256': digest(fbx)}
        game_glb = args.game_glb or folder/'previews'/(args.creature+'_Game.glb')
        game_glb.parent.mkdir(parents=True, exist_ok=True)
        bpy.ops.export_scene.gltf(filepath=str(game_glb), export_format='GLB', use_selection=True,
            export_yup=True, export_animations=True, export_animation_mode='ACTIONS',
            export_anim_single_armature=True, export_frame_range=False, export_frame_step=1,
            export_force_sampling=True, export_anim_slide_to_zero=True,
            export_optimize_animation_size=False, export_def_bones=True, export_apply=False,
            export_texcoords=True, export_normals=True, export_materials='EXPORT',
            export_image_format='AUTO', export_cameras=False, export_lights=False,
            export_all_influences=False, export_skins=True)
        report['optionalGameGLB'] = {'file': game_glb.relative_to(STUDIO).as_posix(), 'sha256': digest(game_glb)}
    report['status'] = 'INSPECTION_SOURCE_ONLY' if args.skip_export else 'EXPORTED'
    (folder/'authoring-report.json').write_text(json.dumps(report, indent=2, ensure_ascii=False), encoding='utf8')
    say(report['status'])


if __name__ == '__main__':
    main()
