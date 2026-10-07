"""Read-only Blender 4.0.2 authoring/relative-texture handoff validation.

blender --background --python SourceArt/CorridorFurnishings/validate_furnishing_sources.py
Optional report: -- --output <absolute path outside source>
"""
import argparse
import hashlib
import json
import sys
from pathlib import Path

import bpy
from mathutils import Vector

HERE = Path(__file__).resolve().parent
PROJECT = HERE.parents[1]


def check():
    manifest = json.loads((HERE / 'model-manifest.json').read_text())
    records = []
    if len(manifest['assets']) != 5:
        raise RuntimeError('All five editable/runtime resources must be present')
    for asset in manifest['assets']:
        source = PROJECT / asset['source']
        fbx = PROJECT / asset['fbx']
        for path, expected in ((source, asset['sourceSha256']), (fbx, asset['fbxSha256'])):
            if hashlib.sha256(path.read_bytes()).hexdigest() != expected:
                raise RuntimeError('Authoring manifest digest mismatch: ' + str(path))
        bpy.ops.wm.open_mainfile(filepath=str(source))
        objects = list(bpy.context.scene.objects)
        if any(obj.type not in {'MESH', 'EMPTY'} for obj in objects):
            raise RuntimeError('QA light/camera or other nonvisual data leaked into source')
        meshes = [obj for obj in objects if obj.type == 'MESH']
        if sorted(o.name for o in meshes) != sorted(asset['meshParts']):
            raise RuntimeError('Authoring part hierarchy mismatch: ' + asset['key'])
        if any(o.animation_data for o in objects):
            raise RuntimeError('Unexpected source animation')
        tris = 0
        for obj in meshes:
            obj.data.calc_loop_triangles()
            tris += len(obj.data.loop_triangles)
            if len(obj.data.uv_layers) != 1 or not obj.data.materials:
                raise RuntimeError('Missing authored UV/material data: ' + obj.name)
            if any(len(poly.vertices) > 4 for poly in obj.data.polygons):
                raise RuntimeError('Unsupported FBX tangent n-gon: ' + obj.name)
        if not 800 <= tris <= 40000 or tris != asset['triangles']:
            raise RuntimeError('Measured triangle contract mismatch: ' + asset['key'])
        image_paths = []
        for image in bpy.data.images:
            if image.source != 'FILE':
                continue
            if not image.filepath.startswith('//'):
                raise RuntimeError('Workstation-absolute source image path: ' + image.filepath)
            resolved = Path(bpy.path.abspath(image.filepath)).resolve()
            relative = resolved.relative_to(PROJECT.resolve()).as_posix()
            if not resolved.is_file() or not relative.startswith('Assets/Resources/GraphicsPbr/'):
                raise RuntimeError('Missing or untracked-location source image: ' + relative)
            image_paths.append(relative)
        if not image_paths:
            raise RuntimeError('Expected physically mapped source images absent')
        if asset['key'] == 'writing-desk':
            drawers = [o for o in meshes if o.name == 'Drawer']
            if len(drawers) != 1 or not drawers[0].parent or drawers[0].parent.type != 'EMPTY':
                raise RuntimeError('Drawer must remain an independent root-child mesh')
            pivot = drawers[0].location
            expected = asset['drawer']['pivot']
            actual = (pivot.x, pivot.z, -pivot.y)
            if any(abs(a - b) > .000001 for a, b in zip(actual, expected)):
                raise RuntimeError('Drawer pivot drift')
        if asset['key'] == 'firecracker-pack':
            # Use actual Blender world vertices, not the declared bounding size.
            height = max((obj.matrix_world @ vertex.co).z for obj in meshes for vertex in obj.data.vertices)
            desk = next(a for a in manifest['assets'] if a['key'] == 'writing-desk')
            mount = desk['drawer']['pickupMount'][1]
            ceiling = desk['drawer']['closedCavityCeiling']
            if height > .112 or mount + height >= ceiling - .004:
                raise RuntimeError('Payload penetrates the real closed drawer under-top bead')
        shelf_clearance = None
        if asset['key'] == 'archive-shelf':
            battery = next(a for a in manifest['assets'] if a['key'] == 'battery-pack')
            anchor = asset['pickupMounts']['lowerLeft']
            # Actual preserved FBX handedness: asymmetric source-plan X maps to
            # runtime owner -X, while the floor height/depth contract is retained.
            payload_min = [anchor[0] - battery['boundsMax'][0],
                           anchor[1] + battery['boundsMin'][1], anchor[2] + battery['boundsMin'][2]]
            payload_max = [anchor[0] - battery['boundsMin'][0],
                           anchor[1] + battery['boundsMax'][1], anchor[2] + battery['boundsMax'][2]]
            overlaps = 0
            for obj in meshes:
                for face in obj.data.polygons:
                    slot = obj.data.materials[face.material_index].name
                    if slot not in {'CF_ledger_blue', 'CF_ledger_red', 'GU_washi', 'CF_celadon', 'GU_charred_wick'}:
                        continue
                    points = [obj.matrix_world @ obj.data.vertices[index].co for index in face.vertices]
                    runtime = [(-p.x, p.z, -p.y) for p in points]
                    lo = [min(p[i] for p in runtime) for i in range(3)]
                    hi = [max(p[i] for p in runtime) for i in range(3)]
                    if all(lo[i] < payload_max[i] and hi[i] > payload_min[i] for i in range(3)):
                        overlaps += 1
            if overlaps:
                raise RuntimeError('Shelf battery overlaps modeled books/scrolls/vessel: %d face boxes' % overlaps)
            if payload_min[0] <= -.62 or payload_max[0] >= .62 or payload_min[2] <= -.175 or payload_max[2] >= .15:
                raise RuntimeError('Shelf battery overhangs the open bearing board')
            shelf_clearance = dict(runtimeAnchor=anchor, payloadMin=payload_min, payloadMax=payload_max,
                                   storedObjectFaceOverlaps=overlaps, boardSurfaceY=.50)
        records.append(dict(key=asset['key'], triangles=tris, parts=len(meshes),
                            relativeSourceImages=len(image_paths), sourceBytes=source.stat().st_size,
                            runtimeBytes=fbx.stat().st_size,
                            **({'shelfPickupClearance': shelf_clearance} if shelf_clearance else {})))
    return dict(status='PASS', blender=bpy.app.version_string, assets=records,
                scope='Read-only editable source, relative mapped texture, hierarchy and FBX digest checks; no Unity gameplay or visual certification')


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--output', type=Path)
    args = parser.parse_args(sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else [])
    try:
        report = check()
    except Exception as error:
        report = dict(status='FAIL', error=str(error))
    value = json.dumps(report, indent=2)
    print('CORRIDOR_SOURCE_VALIDATION ' + value, flush=True)
    if args.output:
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_text(value + '\n', encoding='utf-8')
    if report['status'] != 'PASS':
        raise RuntimeError(report['error'])


if __name__ == '__main__':
    main()
