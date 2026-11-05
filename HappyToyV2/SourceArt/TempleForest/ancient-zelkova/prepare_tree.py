"""Create a review-only, editable tree and LOD0 from the generated GLB.

Blender 4.0.2, CPU/background. Keeps the original GLB and an editable shape
key for the trunk height adjustment. Outputs remain in SourceArt for review;
this script does not create Unity assets or approve the model for gameplay.
"""
from pathlib import Path
import importlib.util
import json
import bpy
import bmesh
import numpy as np

here = Path(__file__).resolve().parent
recipe = here.parents[1] / 'CorridorEntrance' / 'finish_entrance_assets.py'
spec = importlib.util.spec_from_file_location('entrance_finish', recipe)
module = importlib.util.module_from_spec(spec); spec.loader.exec_module(module)
module.SOURCE = here.parent
module.OUTPUT = here / 'game-export'
module.OUTPUT.mkdir(exist_ok=True)
# The post-normalization shape key lengthens only above the root region. A
# 7.6m basis becomes 9.91m, remaining editable and reversible in the blend.
module.SETTINGS['ancient-zelkova'] = dict(axis=2, metres=7.6, texture=2048, budgets=(600000,))

def simplify(obj, budget):
    # The original object is preserved by the shared finishing recipe.
    # This separate, full-detail copy retains both original proportions and
    # a reversible shape key. Only the downstream game copy is reduced.
    review = obj.copy(); review.data = obj.data.copy()
    review.name = 'Zelkova proportion review (full detail)'
    bpy.context.scene.collection.objects.link(review)
    review.shape_key_add(name='Basis')
    tall = review.shape_key_add(name='Raised canopy')
    for vertex in tall.data:
        vertex.co.z += .35 * max(0, vertex.co.z - 1.0)
    tall.value = 1
    review.hide_render = True; review.hide_set(True)
    for vertex in obj.data.vertices:
        vertex.co.z += .35 * max(0, vertex.co.z - 1.0)
    obj.data.update()

    bm = bmesh.new(); bm.from_mesh(obj.data)
    edges = [edge for edge in bm.edges if len(edge.link_faces) > 2]
    bmesh.ops.split_edges(bm, edges=edges); bm.to_mesh(obj.data); bm.free()
    # Preserve the lower trunk and roots; central bark-coloured branches get
    # additional weighting based on the generated material, not hand-built shapes.
    shader = next(n for n in obj.data.materials[0].node_tree.nodes if n.type == 'BSDF_PRINCIPLED')
    image = module.image_for(shader.inputs['Base Color'])[0]
    width, height = image.size
    pixels = np.empty(width*height*4, dtype=np.float32)
    image.pixels.foreach_get(pixels); pixels = pixels.reshape(height, width, 4)
    loops = np.empty(len(obj.data.loops), dtype=np.int32)
    obj.data.loops.foreach_get('vertex_index', loops)
    uv = np.empty(len(obj.data.loops)*2, dtype=np.float32)
    obj.data.uv_layers.active.data.foreach_get('uv', uv); uv = uv.reshape(-1,2)
    x = (np.mod(uv[:,0],1)*width).astype(np.int32)
    y = (np.mod(uv[:,1],1)*height).astype(np.int32)
    color = pixels[y,x,:3]
    bark = (color[:,0] > color[:,1]*1.05) & (color[:,0] > color[:,2]*1.10) & (color[:,0] > .005)
    totals = np.bincount(loops, minlength=len(obj.data.vertices))
    votes = np.bincount(loops, weights=bark, minlength=len(obj.data.vertices))
    wood = votes / np.maximum(totals,1) > .5
    protected = [v.index for v in obj.data.vertices if v.co.z < 3.0 or
                 wood[v.index] and v.co.x*v.co.x + v.co.y*v.co.y < 1.7**2]
    group = obj.vertex_groups.new(name='Preserve roots and central wood')
    group.add(protected, 1, 'REPLACE')
    bpy.ops.object.select_all(action='DESELECT'); obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    modifier = obj.modifiers.new('Source-derived canopy reduction', 'DECIMATE')
    modifier.ratio = .50; modifier.vertex_group = group.name
    modifier.vertex_group_factor = 10; modifier.invert_vertex_group = True
    bpy.ops.object.modifier_apply(modifier=modifier.name)
    modifier = obj.modifiers.new('Explicit triangles', 'TRIANGULATE')
    bpy.ops.object.modifier_apply(modifier=modifier.name)
    for face in obj.data.polygons: face.use_smooth = True
    count = module.triangles(obj)
    if count > budget: raise RuntimeError('Ancient tree candidate exceeds budget: '+str(count))
    print('ANCIENT_TREE_LOD0',json.dumps(dict(triangles=count, vertices=len(obj.data.vertices),
          splitNonmanifoldEdges=len(edges), protectedVertices=len(protected),
          verticalAdjustment='z += .35 * max(0,z-1m)', ratio=.50)), flush=True)

module.simplify = simplify
module.prepare('ancient-zelkova', 0)
