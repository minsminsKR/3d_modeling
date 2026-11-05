"""Inspect the preserved generated fern clump with Blender 4.0.2, CPU/background.

Writes only source-measurements.json beside this script; does not alter GLB.
"""
from pathlib import Path
import hashlib, json, math
import bpy
from mathutils import Vector

root = Path(__file__).resolve().parent
source = root / 'original.glb'
digest = hashlib.sha256(source.read_bytes()).hexdigest()
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=str(source))
meshes = [o for o in bpy.context.scene.objects if o.type == 'MESH']
if not meshes: raise RuntimeError('Fern GLB has no mesh')
items = []
for obj in meshes:
    obj.data.calc_loop_triangles()
    points = [obj.matrix_world @ Vector(c) for c in obj.bound_box]
    lo = [min(v[i] for v in points) for i in range(3)]
    hi = [max(v[i] for v in points) for i in range(3)]
    if not all(math.isfinite(v) for v in lo + hi): raise RuntimeError('Invalid model bounds')
    materials = []
    for material in obj.data.materials:
        images = []
        if material and material.use_nodes:
            images = [dict(name=n.image.name, width=n.image.size[0], height=n.image.size[1],
                           channels=n.image.channels, packed=bool(n.image.packed_file))
                      for n in material.node_tree.nodes if n.type == 'TEX_IMAGE' and n.image]
        materials.append(dict(name=material.name if material else None, images=images))
    items.append(dict(name=obj.name, vertices=len(obj.data.vertices), triangles=len(obj.data.loop_triangles),
                      uvLayers=len(obj.data.uv_layers), boundsMin=lo, boundsMax=hi, materials=materials))
report = dict(scope='Generated original GLB, Blender import; not in-game appearance or performance',
              source=source.name, sourceSha256=digest, objects=items)
(root/'source-measurements.json').write_text(json.dumps(report, indent=2)+'\n', encoding='utf-8')
print('FERN_CLUMP_SOURCE', json.dumps(report), flush=True)
