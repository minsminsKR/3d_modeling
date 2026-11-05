"""Create a review-only pine that preserves the source trunk and roots.

Blender 4.0.2, background/CPU. Reads the preserved editable source; never
overwrites the production LODs. The result needs game-camera and FPS review.
"""
import hashlib
import json
import math
from pathlib import Path

import bmesh
import bpy
import numpy as np
from mathutils import Matrix, Vector
from mathutils.bvhtree import BVHTree

here = Path(__file__).resolve().parent
source = here / 'pine' / 'pine.blend'
out = here / 'pine-review'
out.mkdir(exist_ok=True)
editable = out / 'pine-trunk-review.blend'
export = out / 'pine-trunk-review.fbx'
sha = lambda path: hashlib.sha256(path.read_bytes()).hexdigest()
source_hash = sha(source)
bpy.ops.wm.open_mainfile(filepath=str(source))
bpy.context.preferences.filepaths.save_version = 0
original = bpy.data.objects.get('geometry_0')
baseline = bpy.data.objects.get('CE_pine_LOD0')
if not original or not baseline or not original.data.uv_layers.active:
    raise RuntimeError('Preserved pine source or production LOD is missing')
corners = [original.matrix_world @ Vector(point) for point in original.bound_box]
low = Vector(tuple(min(point[i] for point in corners) for i in range(3)))
high = Vector(tuple(max(point[i] for point in corners) for i in range(3)))
scale = 10 / (high.z - low.z)
centre = Vector(((low.x + high.x) / 2, (low.y + high.y) / 2, low.z))
normalization = Matrix.Scale(scale, 4) @ Matrix.Translation(-centre) @ original.matrix_world
collection = bpy.data.collections.new('REVIEW - trunk-preserving pine')
bpy.context.scene.collection.children.link(collection)
candidate = original.copy(); candidate.data = original.data.copy()
candidate.name = 'CE_pine_trunk_review'; collection.objects.link(candidate)
candidate.data.transform(normalization); candidate.matrix_world = Matrix.Identity(4)
mesh = bmesh.new(); mesh.from_mesh(candidate.data)
bmesh.ops.remove_doubles(mesh, verts=list(mesh.verts), dist=.00002)
bmesh.ops.recalc_face_normals(mesh, faces=list(mesh.faces))
nonmanifold = [edge for edge in mesh.edges if len(edge.link_faces) > 2]
bmesh.ops.split_edges(mesh, edges=nonmanifold)
mesh.to_mesh(candidate.data); mesh.free(); candidate.data.update()
# Keep the lower trunk completely and protect wood-colored parts of the upper
# central branches. Sample the existing preserved game atlas at each loop;
# a majority vote avoids treating a single seam pixel as a whole vertex.
atlas_path = here / 'game-export/pine/CE_pine_0-baseColor.png'
atlas_hash = sha(atlas_path)
image = bpy.data.images.load(str(atlas_path), check_existing=True)
width, height = image.size
pixels = np.empty(width * height * 4, dtype=np.float32)
image.pixels.foreach_get(pixels); pixels = pixels.reshape(height, width, 4)
loop_vertices = np.empty(len(candidate.data.loops), dtype=np.int32)
candidate.data.loops.foreach_get('vertex_index', loop_vertices)
uv = np.empty(len(candidate.data.loops) * 2, dtype=np.float32)
candidate.data.uv_layers.active.data.foreach_get('uv', uv); uv = uv.reshape(-1, 2)
x = (np.mod(uv[:, 0], 1) * width).astype(np.int32)
y = (np.mod(uv[:, 1], 1) * height).astype(np.int32)
color = pixels[y, x, :3]
wood = (color[:, 0] > color[:, 1] * 1.05) & (color[:, 0] > color[:, 2] * 1.10) & (color[:, 0] > .005)
totals = np.bincount(loop_vertices, minlength=len(candidate.data.vertices))
votes = np.bincount(loop_vertices, weights=wood, minlength=len(candidate.data.vertices))
wood_vertices = votes / np.maximum(totals, 1) > .5
protected = [v.index for v in candidate.data.vertices if v.co.z < 4.6 or
             wood_vertices[v.index] and v.co.x * v.co.x + v.co.y * v.co.y < 1.6 ** 2]
group = candidate.vertex_groups.new(name='Preserve trunk and central wooden branches')
group.add(protected, 1, 'REPLACE')
bpy.ops.object.select_all(action='DESELECT'); candidate.select_set(True)
bpy.context.view_layer.objects.active = candidate
modifier = candidate.modifiers.new('Reduce canopy, preserve trunk', 'DECIMATE')
modifier.ratio = .075; modifier.vertex_group = group.name
modifier.vertex_group_factor = 10; modifier.invert_vertex_group = True
bpy.ops.object.modifier_apply(modifier=modifier.name)
for face in candidate.data.polygons: face.use_smooth = True
candidate.data.calc_loop_triangles()
if not candidate.data.uv_layers.active or len(candidate.data.loop_triangles) > 200000:
    raise RuntimeError('Review pine lost its atlas or exceeded 200k triangles')

def surface_tree(obj):
    obj.data.calc_loop_triangles()
    vertices = [obj.matrix_world @ v.co for v in obj.data.vertices]
    triangles = [tuple(t.vertices) for t in obj.data.loop_triangles]
    return BVHTree.FromPolygons(vertices, triangles, all_triangles=True)

old_tree, new_tree = surface_tree(baseline), surface_tree(candidate)
samples = [normalization @ original.data.vertices[index].co
           for index in range(0, len(original.data.vertices), 97)
           if (normalization @ original.data.vertices[index].co).z < 4.6]
if len(samples) < 50: raise RuntimeError('Too few trunk comparison samples')
def distances(tree):
    values = sorted(tree.find_nearest(point)[3] for point in samples)
    if not all(math.isfinite(value) for value in values): raise RuntimeError('Invalid surface distance')
    return dict(p95=values[math.ceil((len(values)-1)*.95)], maximum=values[-1])
comparison = dict(samples=len(samples), baseline=distances(old_tree), candidate=distances(new_tree))
if comparison['candidate']['p95'] >= comparison['baseline']['p95']:
    raise RuntimeError('Candidate does not improve sampled trunk surface fidelity')

for obj in bpy.data.objects:
    if obj.type == 'MESH': obj.hide_render = obj != candidate
candidate.hide_set(False); collection.hide_viewport = False; collection.hide_render = False
text = bpy.data.texts.get('SOURCE - prepare_pine_trunk_review.py') or bpy.data.texts.new('SOURCE - prepare_pine_trunk_review.py')
text.clear(); text.write(Path(__file__).read_text(encoding='utf-8'))
bpy.ops.wm.save_as_mainfile(filepath=str(editable), compress=True)
bpy.ops.export_scene.fbx(filepath=str(export), use_selection=True, object_types={'MESH'},
    axis_forward='-Z', axis_up='Y', bake_anim=False, add_leaf_bones=False,
    path_mode='STRIP', use_mesh_modifiers=True)
if sha(source) != source_hash: raise RuntimeError('Original editable pine changed')
report = dict(scope='Review-only candidate; game camera, performance and device audio remain unverified',
    source='pine/pine.blend', sourceSha256=source_hash, editable=editable.name,
    editableSha256=sha(editable), export=export.name, exportSha256=sha(export),
    vertices=len(candidate.data.vertices), triangles=len(candidate.data.loop_triangles),
    weldDistance=.00002, nonmanifoldEdgesSplit=len(nonmanifold), protectedBelowMetres=4.6,
    woodAtlas='game-export/pine/CE_pine_0-baseColor.png', woodAtlasSha256=atlas_hash,
    woodCoreRadiusMetres=1.6, protectedVertices=len(protected),
    decimateRatio=.075, invertedVertexGroup=True, vertexGroupStrength=10,
    trunkSurfaceDistanceMetres=comparison)
(out / 'report.json').write_text(json.dumps(report, indent=2) + '\n', encoding='utf-8')
print('PINE_TRUNK_REVIEW_PASS', json.dumps(report), flush=True)
