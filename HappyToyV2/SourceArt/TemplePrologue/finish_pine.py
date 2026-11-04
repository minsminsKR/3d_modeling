"""Finish the preserved pine in Blender 4.0.2 background, without GPU rendering.

Splitting edges with more than two faces permits Blender's collapse operation
on this generated topology; loop UVs remain attached to their faces. This is
an intermediate 160k budget, not evidence of visual quality or final LODs.
"""
from pathlib import Path
import importlib.util
import bmesh
import bpy

here = Path(__file__).resolve().parent
recipe = here.parent / 'CorridorEntrance' / 'finish_entrance_assets.py'
spec = importlib.util.spec_from_file_location('entrance_finish', recipe)
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)
module.SOURCE = here
module.OUTPUT = here / 'game-export'
module.OUTPUT.mkdir(exist_ok=True)
module.SETTINGS['pine'] = dict(axis=2, metres=10.0, texture=2048, budgets=(160000,))

def simplify(obj, budget):
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    edges = [edge for edge in bm.edges if len(edge.link_faces) > 2]
    bmesh.ops.split_edges(bm, edges=edges)
    bm.to_mesh(obj.data)
    bm.free()
    bpy.ops.object.select_all(action='DESELECT')
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    # More aggressive than the nominal ratio because open generated leaves
    # have a significant number of boundary edges that cannot collapse.
    modifier = obj.modifiers.new('Pine split-edge reduction', 'DECIMATE')
    modifier.ratio = .075
    bpy.ops.object.modifier_apply(modifier=modifier.name)
    modifier = obj.modifiers.new('Explicit triangles', 'TRIANGULATE')
    bpy.ops.object.modifier_apply(modifier=modifier.name)
    for face in obj.data.polygons:
        face.use_smooth = True
    count = module.triangles(obj)
    if count > budget:
        raise RuntimeError('Pine exceeds budget: ' + str(count))
    print('PINE_SPLIT_EDGES', len(edges), 'TRIANGLES', count, flush=True)

module.simplify = simplify
module.prepare('pine', 0)
