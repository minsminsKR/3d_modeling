"""Independently inspect rebaked FBX topology, UVs, transforms and surface distances.

Run with Blender 4.0.2 background CPU: blender -b -t 2 --factory-startup
--python-exit-code 1 --python <this script>. Does not modify authoring files.
"""
from pathlib import Path
import json
import os
import bpy
import numpy as np
from mathutils import Matrix, Vector

HERE = Path(__file__).resolve().parent
KEY = HERE.name
OUT = HERE / os.environ.get('STONE_REBAKE_DIRECTORY', 'rebaked')
REPORT = OUT / 'rebake-report.json'
PROOF = OUT / 'independent-verification.json'
if bpy.app.version[:3] != (4,0,2) or PROOF.exists():
    raise RuntimeError('Wrong Blender version or output exists')
# Reuse the first verifier's geometry-only helpers without running its main body.
helpers = (HERE / 'verify_stepping_stone_candidate.py').read_text(encoding='utf-8')
exec(compile(helpers[:helpers.index("if bpy.app.version[:3] != (4, 0, 2):")], str(HERE / 'verify_stepping_stone_candidate.py'), 'exec'))
OUT = HERE / os.environ.get('STONE_REBAKE_DIRECTORY', 'rebaked')
REPORT = OUT / 'rebake-report.json'
PROOF = OUT / 'independent-verification.json'
report = json.loads(REPORT.read_text(encoding='utf-8'))
blend = OUT / report['editableBlend']
if sha(HERE / 'original.glb') != report['originalSha256'] or sha(blend) != report['editableSha256']:
    raise RuntimeError('Preserved original or editable hash mismatch')
bpy.ops.wm.open_mainfile(filepath=str(blend), load_ui=False)
source = next(o for o in bpy.data.collections['Complete unchanged webapp original'].objects if o.type == 'MESH')
corners = [source.matrix_world @ Vector(c) for c in source.bound_box]
lo = Vector(tuple(min(p[i] for p in corners) for i in range(3)))
hi = Vector(tuple(max(p[i] for p in corners) for i in range(3)))
center = Vector(((lo.x+hi.x)*.5,(lo.y+hi.y)*.5,lo.z))
transform = Matrix.Scale(1/(hi.x-lo.x),4) @ Matrix.Translation(-center) @ source.matrix_world
sv, si = mesh_arrays(source, transform)
samples, _ = sample_surface(sv, si, 2048, 873156)
source_tree = tree_for(source, transform)
result = dict(originalSha256=report['originalSha256'], editableSha256=report['editableSha256'], lods=[])
for entry in report['lods']:
    i = entry['lod']
    obj = bpy.data.objects['ROUND_REBAKED_LOD' + str(i)]
    vertices, triangles = mesh_arrays(obj, obj.matrix_world)
    if len(triangles) != entry['triangles']: raise RuntimeError('LOD triangle mismatch')
    points, degenerate = sample_surface(vertices, triangles, 2048, 873157+i)
    row = dict(lod=i, triangles=len(triangles), uv=uv_stats(obj), normals=normal_stats(obj),
               sourceToLod=nearest_stats(samples,tree_for(obj,obj.matrix_world)),
               lodToSource=nearest_stats(points,source_tree), degenerateTriangles=degenerate)
    fbx = OUT / entry['fbx']['file']
    if sha(fbx) != entry['fbx']['sha256']: raise RuntimeError('FBX hash mismatch')
    before = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=str(fbx), use_anim=False)
    imported = [o for o in bpy.data.objects if o not in before and o.type=='MESH']
    if len(imported) != 1: raise RuntimeError('Expected one FBX mesh')
    iv, it = mesh_arrays(imported[0], imported[0].matrix_world)
    delta = max(float(np.max(np.abs(iv.min(axis=0)-vertices.min(axis=0)))),float(np.max(np.abs(iv.max(axis=0)-vertices.max(axis=0)))))
    row['fbxReimport'] = dict(triangles=len(it),boundsDifferenceMetres=delta,uv=uv_stats(imported[0]),normals=normal_stats(imported[0]))
    if len(it) != len(triangles) or delta > .002: raise RuntimeError('FBX round trip changed geometry')
    for o in set(bpy.data.objects)-before: bpy.data.objects.remove(o,do_unlink=True)
    for tex in entry['textures'].values():
        path = OUT / tex['file']
        if sha(path) != tex['sha256']: raise RuntimeError('Texture hash mismatch')
        image = bpy.data.images.load(str(path),check_existing=False)
        if list(image.size) != [tex['width'],tex['height']]: raise RuntimeError('Texture dimensions mismatch')
        bpy.data.images.remove(image)
    result['lods'].append(row)
PROOF.write_text(json.dumps(result,indent=2)+'\n',encoding='utf-8')
print('INDEPENDENT_VERIFICATION', json.dumps(result), flush=True)
