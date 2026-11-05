"""Inspect the preserved A100 stone-cluster GLB with Blender 4.0.2.

This read-only geometry/PBR inventory is not a game-camera visual review.
"""
from pathlib import Path
import hashlib
import json
import math
import bpy
from mathutils import Vector

root = Path(__file__).resolve().parent
source = root / 'original.glb'
digest = hashlib.sha256(source.read_bytes()).hexdigest()
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=str(source))
meshes = [obj for obj in bpy.context.scene.objects if obj.type == 'MESH']
if not meshes:
    raise RuntimeError('Generated stone cluster contains no meshes')
items = []
for obj in meshes:
    obj.data.calc_loop_triangles()
    corners = [obj.matrix_world @ Vector(corner) for corner in obj.bound_box]
    low = [min(point[i] for point in corners) for i in range(3)]
    high = [max(point[i] for point in corners) for i in range(3)]
    if not all(math.isfinite(value) for value in low + high):
        raise RuntimeError('Non-finite source bounds')
    materials = []
    for material in obj.data.materials:
        images = []
        if material and material.use_nodes:
            images = [dict(name=node.image.name, width=node.image.size[0], height=node.image.size[1],
                           channels=node.image.channels, packed=bool(node.image.packed_file))
                      for node in material.node_tree.nodes if node.type == 'TEX_IMAGE' and node.image]
        materials.append(dict(name=material.name if material else None, images=images))
    items.append(dict(name=obj.name, vertices=len(obj.data.vertices), triangles=len(obj.data.loop_triangles),
                      uvLayers=len(obj.data.uv_layers), boundsMin=low, boundsMax=high, materials=materials))
report = dict(scope='Generated original GLB and Blender import; not in-game or performance approval',
              source=source.name, sourceSha256=digest, objects=items)
(root / 'source-measurements.json').write_text(json.dumps(report, indent=2) + '\n', encoding='utf8')
print('TEMPLE_STONE_CLUSTER_SOURCE', json.dumps(report), flush=True)
