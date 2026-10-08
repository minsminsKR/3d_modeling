"""Read-only original chamber .blend/FBX/PBR authoring proof, Blender 4.0.2.

--resource-project is only a review staging dependency override. A full clone
needs no override: all image links are relative to the project's tracked Assets.
No source file is saved and no runtime/visual/navigation claim is made.
"""
from pathlib import Path
import argparse
import hashlib
import json
import math
import sys
import bpy
from mathutils import Vector

HERE = Path(__file__).resolve().parent
PROJECT = HERE.parents[1]


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--resource-project', type=Path, default=PROJECT)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args(sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else [])
    resource = args.resource_project.resolve()
    manifest = json.loads((HERE / 'model-manifest.json').read_text())
    texture_manifest = json.loads((HERE / 'texture-manifest.json').read_text())
    if len(manifest['assets']) != 4: raise RuntimeError('Expected all four editable chamber models')
    for image in texture_manifest['images']:
        if hashlib.sha256((PROJECT / image['file']).read_bytes()).hexdigest() != image['sha256']:
            raise RuntimeError('Original chalkboard PBR hash mismatch')
    records = []
    for asset in manifest['assets']:
        source, runtime = PROJECT / asset['source'], PROJECT / asset['fbx']
        if hashlib.sha256(source.read_bytes()).hexdigest() != asset['sourceSha256']: raise RuntimeError('Source hash mismatch ' + asset['key'])
        if hashlib.sha256(runtime.read_bytes()).hexdigest() != asset['fbxSha256']: raise RuntimeError('FBX hash mismatch ' + asset['key'])
        bpy.ops.wm.open_mainfile(filepath=str(source))
        if any(obj.type not in ('MESH', 'EMPTY') or obj.animation_data for obj in bpy.context.scene.objects):
            raise RuntimeError('Unexpected scene/animation data leaked into editable source')
        parts = [obj for obj in bpy.context.scene.objects if obj.type == 'MESH']
        if sorted(obj.name for obj in parts) != sorted(asset['meshParts']): raise RuntimeError('Editable named part hierarchy drift')
        triangles = 0
        for obj in parts:
            obj.data.calc_loop_triangles(); triangles += len(obj.data.loop_triangles)
            if len(obj.data.uv_layers) != 1 or not obj.data.materials: raise RuntimeError('UV/material absent ' + obj.name)
            if any(len(face.vertices) > 4 for face in obj.data.polygons): raise RuntimeError('Unsupported tangent n-gon ' + obj.name)
            if any(not math.isfinite(c) for vertex in obj.data.vertices for c in vertex.co): raise RuntimeError('Nonfinite vertex')
        if triangles != asset['triangles'] or triangles > 40000: raise RuntimeError('Measured source triangle budget mismatch')
        chalkboard_face = None
        if asset['key'] == 'lesson-blackboard':
            panel = next(obj for obj in parts if obj.name == 'Last lesson enamel front')
            backing = next(obj for obj in parts if obj.name == 'Blackboard solid rear backing')
            panel_points = [obj_point for obj_point in (panel.matrix_world @ vertex.co for vertex in panel.data.vertices)]
            backing_points = [backing.matrix_world @ vertex.co for vertex in backing.data.vertices]
            # Source-plan Z=-Blender Y; enforce solid enamel and physical separation
            # from the timber backing instead of a fragile imported single face.
            thickness = max(-p.y for p in panel_points) - min(-p.y for p in panel_points)
            panel_front = min(-p.y for p in panel_points); timber_front = min(-p.y for p in backing_points)
            if thickness < .0099 or panel_front >= timber_front - .01:
                raise RuntimeError('Enamel slab thickness/backing exposure contract failed')
            if {mat.name for mat in panel.data.materials} != {'CA_Chalkboard'}:
                raise RuntimeError('Original chalk texture is not assigned to exposed enamel')
            if not all(-.001 <= value <= 1.001 for item in panel.data.uv_layers[0].data for value in item.uv):
                raise RuntimeError('Whole-board chalk UV escaped unique 0..1 rectangle')
            chalkboard_face = dict(solidEnamelMetres=thickness, sourceFrontZ=panel_front,
                                   woodBackingFrontZ=timber_front, uniqueWholeBoardUv=True,
                                   sourcePlanXCompensatedForUnityHandedness=True)
        images = []
        for image in bpy.data.images:
            if image.source != 'FILE': continue
            if not image.filepath.startswith('//'): raise RuntimeError('Workstation absolute source image ' + image.filepath)
            final_path = Path(bpy.path.abspath(image.filepath)).resolve()
            relative = final_path.relative_to(PROJECT).as_posix()
            if not relative.startswith('Assets/Resources/'): raise RuntimeError('Source image outside tracked Assets')
            dependency = final_path if final_path.is_file() else resource / relative
            if not dependency.is_file(): raise RuntimeError('Missing source dependency ' + relative)
            images.append(relative)
        source_vertices = sum(len(obj.data.vertices) for obj in parts)
        bpy.ops.wm.read_factory_settings(use_empty=True)
        bpy.ops.import_scene.fbx(filepath=str(runtime), use_anim=False)
        imported = [obj for obj in bpy.context.scene.objects if obj.type == 'MESH']
        points = [obj.matrix_world @ vertex.co for obj in imported for vertex in obj.data.vertices]
        # Blender re-import returns physical XYZ (Z up). This independent box is
        # compared with source-plan bounds in the manifest (Y up, front -Z).
        lo = [min((p.x, p.z, -p.y)[i] for p in points) for i in range(3)]
        hi = [max((p.x, p.z, -p.y)[i] for p in points) for i in range(3)]
        expected_size = [asset['boundsMax'][i] - asset['boundsMin'][i] for i in range(3)]
        actual_size = [hi[i] - lo[i] for i in range(3)]
        if any(abs(a - b) > .00008 for a, b in zip(actual_size, expected_size)):
            raise RuntimeError('Independent FBX metric dimensions differ: ' + str((asset['key'], actual_size, expected_size)))
        imported_tris = 0
        for obj in imported:
            obj.data.calc_loop_triangles(); imported_tris += len(obj.data.loop_triangles)
            if not obj.data.uv_layers or not obj.data.materials: raise RuntimeError('Imported UV/material absent')
        if imported_tris != triangles: raise RuntimeError('FBX export lost original triangles')
        if sorted({m.name for obj in imported for m in obj.data.materials}) != asset['materialSlots']:
            raise RuntimeError('Imported named material contract drift')
        records.append(dict(key=asset['key'], editableParts=len(parts), triangles=triangles, sourceVertices=source_vertices,
                            independentFbxDimensionsMetres=actual_size, relativeImages=sorted(set(images)),
                            sourceBytes=source.stat().st_size, fbxBytes=runtime.stat().st_size,
                            **({'chalkboardFace': chalkboard_face} if chalkboard_face else {})))
    report = dict(status='PASS', blender=bpy.app.version_string, assets=records, chalkboardMaps=len(texture_manifest['images']),
                  scope='Read-only original source hashes, editable named parts, relative resource links, UVs, material contract and independent FBX metre-scale roundtrip. Does not certify Unity import, native rendering, gameplay, collisions or navigation.')
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(report, indent=2), encoding='utf-8'); print('ALTAR_CHAMBER_SOURCE_PROOF_PASS', len(records))


if __name__ == '__main__': main()
