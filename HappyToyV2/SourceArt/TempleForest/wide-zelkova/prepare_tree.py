"""Preserve the A100 GLB and derive two editable, textured tree LODs.

Blender 4.0.2, background/CPU.  The original imported geometry and 4K maps
stay packed in the blend. The game copies use one 2K base/metallic-smoothness
atlas, retain the source branch silhouette, and introduce no new geometry.
"""
from pathlib import Path
import importlib.util
import json

import bmesh
import bpy
import numpy as np

here = Path(__file__).resolve().parent
recipe = here.parents[1] / 'CorridorEntrance' / 'finish_entrance_assets.py'
spec = importlib.util.spec_from_file_location('entrance_finish', recipe)
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)
module.SOURCE = here.parent
module.OUTPUT = here / 'game-export'
module.OUTPUT.mkdir(exist_ok=True)
module.SETTINGS['wide-zelkova'] = dict(axis=2, metres=8.0, texture=2048,
                                       budgets=(800000,))


def preserve_coincident_faces(obj):
    """Keep differing UV/normal faces distinct through Blender FBX import."""
    mesh = obj.data
    mesh.calc_loop_triangles()
    mesh.calc_normals_split()
    if not mesh.uv_layers.active or len(mesh.loop_triangles) != len(mesh.polygons):
        raise RuntimeError('Expected a triangulated mesh with a source atlas')
    positions = np.empty(len(mesh.vertices) * 3, dtype=np.float32)
    mesh.vertices.foreach_get('co', positions)
    positions = positions.reshape(-1, 3).tolist()
    faces = np.empty(len(mesh.loop_triangles) * 3, dtype=np.int32)
    mesh.loop_triangles.foreach_get('vertices', faces)
    faces = faces.reshape(-1, 3)
    loop_indices = np.empty(len(mesh.loop_triangles) * 3, dtype=np.int32)
    mesh.loop_triangles.foreach_get('loops', loop_indices)
    loop_indices = loop_indices.reshape(-1, 3)
    atlas = np.empty(len(mesh.loops) * 2, dtype=np.float32)
    mesh.uv_layers.active.data.foreach_get('uv', atlas)
    atlas = atlas.reshape(-1, 2)[loop_indices]
    normals = np.empty(len(mesh.loops) * 3, dtype=np.float32)
    mesh.loops.foreach_get('normal', normals)
    normals = normals.reshape(-1, 3)[loop_indices]

    def signature(i):
        corners = tuple((int(faces[i, corner]), *atlas[i, corner], *normals[i, corner]) for corner in range(3))
        return min(corners, corners[1:] + corners[:1], corners[2:] + corners[:2])

    seen = {}
    signatures = {}
    retained, split_faces = [], []
    identical = separated = 0
    for index, face in enumerate(faces):
        key = tuple(sorted(int(vertex) for vertex in face))
        first = seen.get(key)
        if first is None:
            seen[key] = index
            retained.append(index)
            split_faces.append(face.tolist())
            continue
        existing = signatures.setdefault(key, {signature(first)})
        current = signature(index)
        if current in existing:
            identical += 1
            continue
        existing.add(current)
        first_new = len(positions)
        positions.extend(positions[int(vertex)][:] for vertex in face)
        retained.append(index)
        split_faces.append([first_new, first_new + 1, first_new + 2])
        separated += 1
    data = bpy.data.meshes.new('Wide tree near LOD with preserved atlas seams')
    data.from_pydata(positions, [], split_faces)
    data.update()
    data.uv_layers.new(name='Original source atlas').data.foreach_set('uv', atlas[retained].reshape(-1))
    for face in data.polygons:
        face.use_smooth = True
    data.use_auto_smooth = True
    data.normals_split_custom_set(normals[retained].reshape(-1, 3).tolist())
    data.calc_normals_split()
    if any(loop.normal.length < 1e-6 for loop in data.loops):
        raise RuntimeError('Near LOD has invalid split normals')
    for material in mesh.materials:
        data.materials.append(material)
    obj.data = data
    print('WIDE_TREE_FACE_HANDOFF', json.dumps(dict(exactDuplicatesRemoved=identical,
          differentFacesSeparated=separated)), flush=True)


def simplify(obj, budget):
    """Preserve roots and bark-coloured branches while reducing foliage."""
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    nonmanifold = [edge for edge in bm.edges if len(edge.link_faces) > 2]
    bmesh.ops.split_edges(bm, edges=nonmanifold)
    bm.to_mesh(obj.data)
    bm.free()

    shader = next(node for node in obj.data.materials[0].node_tree.nodes if node.type == 'BSDF_PRINCIPLED')
    image = module.image_for(shader.inputs['Base Color'])[0]
    width, height = image.size
    pixels = np.empty(width * height * 4, dtype=np.float32)
    image.pixels.foreach_get(pixels)
    pixels = pixels.reshape(height, width, 4)
    loops = np.empty(len(obj.data.loops), dtype=np.int32)
    obj.data.loops.foreach_get('vertex_index', loops)
    uv = np.empty(len(obj.data.loops) * 2, dtype=np.float32)
    obj.data.uv_layers.active.data.foreach_get('uv', uv)
    uv = uv.reshape(-1, 2)
    x = (np.mod(uv[:, 0], 1) * width).astype(np.int32)
    y = (np.mod(uv[:, 1], 1) * height).astype(np.int32)
    color = pixels[y, x, :3]
    bark = ((color[:, 0] > color[:, 1] * 1.05) &
            (color[:, 0] > color[:, 2] * 1.10) & (color[:, 0] > .005))
    totals = np.bincount(loops, minlength=len(obj.data.vertices))
    votes = np.bincount(loops, weights=bark, minlength=len(obj.data.vertices))
    wood = votes / np.maximum(totals, 1) > .5
    # Root flare and the main interior branching structure stay dense. Outer
    # wood can simplify with the foliage so the asset fits the game budget.
    protected = [v.index for v in obj.data.vertices if v.co.z < 2.2 or
                 wood[v.index] and v.co.x * v.co.x + v.co.y * v.co.y < 3.8 ** 2]
    group = obj.vertex_groups.get('Preserve roots and branching bark')
    if not group:
        group = obj.vertex_groups.new(name='Preserve roots and branching bark')
    if protected:
        group.add(protected, 1, 'REPLACE')

    bpy.ops.object.select_all(action='DESELECT')
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    modifier = obj.modifiers.new('Foliage reduction preserving wood', 'DECIMATE')
    modifier.ratio = .45
    modifier.vertex_group = group.name
    modifier.vertex_group_factor = 10
    modifier.invert_vertex_group = True
    bpy.ops.object.modifier_apply(modifier=modifier.name)
    modifier = obj.modifiers.new('Explicit triangles', 'TRIANGULATE')
    bpy.ops.object.modifier_apply(modifier=modifier.name)
    for face in obj.data.polygons:
        face.use_smooth = True
    preserve_coincident_faces(obj)
    count = module.triangles(obj)
    if count > budget:
        raise RuntimeError('Tree LOD exceeds its triangle budget: ' + str(count))
    print('WIDE_TREE_LOD', json.dumps(dict(name=obj.name, triangles=count,
          vertices=len(obj.data.vertices), splitNonmanifoldEdges=len(nonmanifold),
          protectedVertices=len(protected), limit=budget)), flush=True)


module.simplify = simplify
module.prepare('wide-zelkova', 0)
text = bpy.data.texts.get('SOURCE - prepare_tree.py') or bpy.data.texts.new('SOURCE - prepare_tree.py')
text.clear()
text.write(Path(__file__).read_text(encoding='utf-8'))
bpy.ops.wm.save_as_mainfile(filepath=str(here / 'wide-zelkova.blend'), compress=True)
