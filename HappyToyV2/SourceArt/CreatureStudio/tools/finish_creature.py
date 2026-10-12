"""Reopen, review and finish a saved editable creature without duplicating input.

This is the fast post-pose-audit route: regenerate exact own clips from the
reviewed source plan/contacts, optionally repair/rebake only the game topology,
save the editable source, then export and validate a nonempty derivative mesh.
"""
import argparse
import os
import uuid
import json
import sys
from pathlib import Path

import bpy
import importlib.util
import numpy as np

sys.path.insert(0, str(Path(__file__).resolve().parent))
import author_creature as author
import creature_animation
from glb_io import read


def export_game(rig, lod, high, folder, output, key, report):
    for modifier in high.modifiers:
        if modifier.type == 'ARMATURE':
            modifier.show_viewport = False
    bpy.ops.object.select_all(action='DESELECT')
    rig.select_set(True)
    lod.select_set(True)
    bpy.context.view_layer.objects.active = rig
    rig.animation_data.action = bpy.data.actions['patrol']
    bpy.context.scene.frame_set(1)
    if lod.data.validate(clean_customdata=True):
        raise ValueError('Mesh changed at final export validation; rebake required')
    fbx = output/(key+'.fbx')
    temporary_fbx=fbx.with_name(fbx.stem+'.export-'+uuid.uuid4().hex+'.fbx')
    bpy.ops.export_scene.fbx(filepath=str(temporary_fbx), use_selection=True, object_types={'MESH', 'ARMATURE'},
        axis_forward='-Z', axis_up='Y', global_scale=1, apply_unit_scale=True,
        apply_scale_options='FBX_SCALE_UNITS', add_leaf_bones=False, use_armature_deform_only=True,
        bake_anim=True, bake_anim_use_all_actions=True, bake_anim_use_nla_strips=False,
        bake_anim_simplify_factor=0, bake_anim_step=1, mesh_smooth_type='FACE', use_tspace=True,
        path_mode='AUTO', embed_textures=False)
    os.replace(temporary_fbx,fbx)
    game = folder/'previews'/(key+'_Game.glb')
    game.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.export_scene.gltf(filepath=str(game), export_format='GLB', use_selection=True,
        export_yup=True, export_animations=True, export_animation_mode='ACTIONS',
        export_anim_single_armature=True, export_frame_range=False, export_frame_step=1,
        export_force_sampling=True, export_anim_slide_to_zero=True,
        export_optimize_animation_size=False, export_def_bones=True, export_apply=False,
        export_texcoords=True, export_normals=True, export_materials='EXPORT',
        export_image_format='AUTO', export_cameras=False, export_lights=False,
        export_all_influences=False, export_skins=True)
    document, _ = read(game)
    triangles = sum(document['accessors'][primitive['indices']]['count']//3
                    for mesh in document.get('meshes', []) for primitive in mesh.get('primitives', []))
    vertices = sum(document['accessors'][primitive['attributes']['POSITION']]['count']
                   for mesh in document.get('meshes', []) for primitive in mesh.get('primitives', []))
    actions = [entry.get('name') for entry in document.get('animations', [])]
    required = {'patrol', 'chase'} | ({'cry'} if key == 'Baby' else set())
    if triangles == 0 or vertices > 80000 or not required.issubset(actions):
        raise ValueError(f'Invalid exported actual mesh/actions: {vertices} vertices, {triangles} triangles, {actions}')
    report['fbx'] = {'file': fbx.relative_to(author.PROJECT).as_posix(), 'sha256': author.digest(fbx)}
    report['optionalGameGLB'] = {'file': game.relative_to(author.STUDIO).as_posix(), 'sha256': author.digest(game),
                                 'actualVertices': vertices, 'actualTriangles': triangles, 'actualActions': actions}
    report['finalReopenInventory'] = {'armatureBones': len(rig.data.bones), 'gameVertices': len(lod.data.vertices),
                                      'gameTriangles': len(lod.data.loop_triangles), 'sourceHighVertices': len(high.data.vertices),
                                      'sourceHeadGroup': bool(high.vertex_groups.get('mixamorig:Head')),
                                      'topologyValidatedBeforeExport': True, 'derivedGLBActualGeometryValidated': True}
    return report


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--creature', choices=['Baby', 'Uncat', 'Cyclopse'], required=True)
    parser.add_argument('--repair-topology', action='store_true')
    parser.add_argument('--rebind-body', action='store_true', help='Reviewed unchanged source-order high geometry only')
    parser.add_argument('--skip-export', action='store_true')
    parser.add_argument('--triangles', type=int)
    parser.add_argument('--animation-only',action='store_true')
    args = parser.parse_args(sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else [])
    folder = author.STUDIO/args.creature
    output = author.PROJECT/'Assets'/'Resources'/'EnemyStudio'/args.creature
    blend = folder/(args.creature+'_Editable.blend')
    bpy.ops.wm.open_mainfile(filepath=str(blend))
    author.say('Reviewed editable pose file loaded')
    high = bpy.data.objects[args.creature+'_EditableHigh']
    lod = bpy.data.objects[args.creature+'_LOD0']
    rig = next(modifier.object for modifier in lod.modifiers if modifier.type == 'ARMATURE')
    plan_path = folder/'body-rig-plan.json'
    mapping = json.loads((plan_path if plan_path.is_file() else folder/'joint-map.json').read_text(encoding='utf8'))
    report_path = folder/'authoring-report.json'
    report = json.loads(report_path.read_text(encoding='utf8')) if report_path.is_file() else {'creature': args.creature}
    rig.data.pose_position = 'REST'
    if args.rebind_body:
        payload = np.load(folder/'body-surface-weights.npz', allow_pickle=False)
        names = [str(name) for name in payload['bone_names']]
        if set(names) != set(rig.data.bones.keys()):
            raise ValueError('Rebinding cannot silently change the reviewed skeleton palette')
        original = payload['positions']
        normalization = report['normalization']
        bounds = np.array(normalization['sourceBounds'])
        center = (bounds[0]+bounds[1])/2
        center[2] = bounds[0, 2]
        source_points = author.points(high)/normalization['uniformScale']+center
        source_points = source_points[:, (0, 2, 1)].copy()
        source_points[:, 2] *= -1
        if source_points.shape != original.shape or np.max(np.linalg.norm(source_points-original, axis=1)) > 2e-6:
            raise ValueError('Rebinding needs exact original high source vertex order/positions; rerun authoring for modified geometry')
        helper_path = author.PROJECT/'SourceArt'/'BaeksugwiStudio'/'author_blender.py'
        spec = importlib.util.spec_from_file_location('creature_rebind_helpers', helper_path)
        helper = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(helper)
        quantized, quant_report = helper.quantize_weights(payload['weights'])
        report['reviewedRebinding'] = helper.bind_mesh(high, rig, np.arange(len(original)),
                                                       payload['joints'], quantized, names)
        report['reviewedRebinding']['quantization'] = quant_report
        previous = lod.data
        bpy.data.objects.remove(lod, do_unlink=True)
        if previous.users == 0:
            bpy.data.meshes.remove(previous)
        lod, report['gameLOD'] = author.create_lod(high, rig, args.creature, mapping.get('game_triangle_budget', 60000))
    if args.triangles:
        previous=lod.data;bpy.data.objects.remove(lod,do_unlink=True)
        if previous.users==0:bpy.data.meshes.remove(previous)
        lod,report['gameLOD']=author.create_lod(high,rig,args.creature,args.triangles)
        mapping['game_triangle_budget']=args.triangles
        plan_path.write_text(json.dumps(mapping,indent=2),encoding='utf8')
    if args.creature == 'Uncat' and not args.animation_only:
        import uncat_eye_surface
        report['ocularSurface'] = {
            'editableHigh': uncat_eye_surface.apply_surface(high, normalization=report['normalization']),
            'gameLOD': uncat_eye_surface.apply_surface(lod, normalization=report['normalization'])}
    if args.repair_topology or args.rebind_body or (args.creature == 'Uncat' and not args.animation_only):
        report.setdefault('gameLOD', {})['topologyCleanup'] = author.clean_game_topology(lod)
        report['gameLOD']['skin'] = author.limit_weights(lod, rig)
        lod.data.calc_loop_triangles()
        report['gameLOD'].update(vertices=len(lod.data.vertices), triangles=len(lod.data.loop_triangles),
                                 estimatedUnitySplitVertices=author.split_vertex_estimate(lod.data))
        author.say('Preparing original-source PBR and surface normals')
        report['normal'],report['continuousAtlasBake'],report['gamePBR']=author.prepare_render_assets(high,lod,output,2048)
    contacts = author.contact_specs(rig, high)
    for name, override in mapping.get('contact_overrides', {}).items():
        contacts.setdefault(name, {}).update(override)
    (folder/'contact-specs.json').write_text(json.dumps(contacts, indent=2), encoding='utf8')
    report['contacts'] = contacts
    rig.data.pose_position = 'POSE'
    high.hide_set(True)
    high.hide_render = True
    report['animation'] = creature_animation.apply_creature_animation(rig, args.creature, contacts,
        fps=60, ground_height=0, report_path=str(folder/'animation-report.json'),
        profile_overrides=mapping.get('animation_profile_overrides'))
    rig.animation_data.action = bpy.data.actions['patrol']
    bpy.context.scene.frame_set(1)
    bpy.context.preferences.filepaths.save_version = 0
    bpy.ops.file.pack_all()
    bpy.ops.wm.save_as_mainfile(filepath=str(blend), compress=True, check_existing=False, relative_remap=False)
    if not args.skip_export:
        export_game(rig, lod, high, folder, output, args.creature, report)
    report['status'] = 'INSPECTION_SOURCE_ONLY' if args.skip_export else 'EXPORTED_VALIDATED'
    report_path.write_text(json.dumps(report, indent=2, ensure_ascii=False), encoding='utf8')
    author.say(report['status'])


if __name__ == '__main__':
    main()
