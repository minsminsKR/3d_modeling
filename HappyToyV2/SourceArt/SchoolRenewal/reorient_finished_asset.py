"""Rotate finished source-derived LODs without rebaking invariant tangent maps.

Used only after orthogonal inspection establishes the generated object's facing.
The unchanged generated original collection stays at its original transform.
"""
from pathlib import Path
import argparse
import json
import math
import runpy
import sys
import bpy
from mathutils import Matrix

SOURCE = Path(__file__).resolve().parent
PROJECT = SOURCE.parents[1]


def reorient(key, degrees):
    source = SOURCE / key
    destination = PROJECT / 'Assets/Resources/SchoolRenewal' / key
    report = json.loads((source / 'export.json').read_text(encoding='utf-8'))
    helpers = runpy.run_path(str(PROJECT / 'SourceArt/CorridorEntrance/finish_entrance_assets.py'),
                             run_name='school_renewal_orientation_helpers')
    if helpers['sha'](source / 'original.glb') != report['originalSha256']:
        raise RuntimeError('Unchanged source hash did not match the original bake')
    bpy.ops.wm.open_mainfile(filepath=str(source / (key + '.blend')))
    rotation = Matrix.Rotation(math.radians(degrees), 4, 'Z')
    for index, level in enumerate(report['lods']):
        game = bpy.data.objects['SR_' + key + '_LOD' + str(index)]
        game.hide_set(False)
        game.hide_render = False
        bpy.ops.object.select_all(action='DESELECT')
        game.select_set(True)
        bpy.context.view_layer.objects.active = game
        game.matrix_world = rotation @ game.matrix_world
        bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
        path = destination / level['file']
        bpy.ops.export_scene.fbx(filepath=str(path), use_selection=True, object_types={'MESH'},
                                 axis_forward='-Z', axis_up='Y', bake_anim=False,
                                 add_leaf_bones=False, path_mode='RELATIVE', use_mesh_modifiers=True)
        low, high = helpers['bounds']([game])
        level.update(boundsMin=list(low), boundsMax=list(high), sha256=helpers['sha'](path))
        if index == 0:
            report.update(widthMetres=high.x-low.x, heightMetres=high.z-low.z,
                          depthMetres=high.y-low.y)
        else:
            game.hide_set(True)
            game.hide_render = True
    report.setdefault('postFinishing', []).append(dict(tool='reorient_finished_asset.py',
        rotationZDegrees=degrees, reason='Orthogonal actual mesh inspection established the open front',
        maps='Rigid orientation preserves UV/tangent basis and the baked colour/ORM/normal detail'))
    report['front'] = 'Confirmed open/front side is Blender +Y / Unity -Z after standard FBX conversion'
    for path in (source / 'export.json', destination / 'asset.json'):
        path.write_text(json.dumps(report, indent=2) + '\n', encoding='utf-8')
    bpy.data.texts.new('SOURCE - reorient_finished_asset.py').write(Path(__file__).read_text(encoding='utf-8'))
    bpy.data.texts.new('SOURCE - final-export.json').write(json.dumps(report, indent=2))
    bpy.ops.file.pack_all()
    bpy.context.preferences.filepaths.save_version = 0
    bpy.ops.wm.save_as_mainfile(filepath=str(source / (key + '.blend')), compress=True)
    print('SCHOOL_RENEWAL_REORIENTATION_PASS', key, degrees)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--key', required=True)
    parser.add_argument('--rotation-z', type=float, required=True)
    args = parser.parse_args(sys.argv[sys.argv.index('--') + 1:])
    reorient(args.key, args.rotation_z)
