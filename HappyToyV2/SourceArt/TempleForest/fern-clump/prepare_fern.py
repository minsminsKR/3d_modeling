"""Derive a review-only fern clump from the preserved generated GLB.

Blender 4.0.2, CPU/background. Leaves the original GLB unchanged and outputs
only to SourceArt. Mesh/material quality and gameplay cost need review.
"""
from pathlib import Path
import importlib.util
import bpy, bmesh

here=Path(__file__).resolve().parent
recipe=here.parents[1]/'CorridorEntrance'/'finish_entrance_assets.py'
spec=importlib.util.spec_from_file_location('entrance_finish',recipe)
module=importlib.util.module_from_spec(spec);spec.loader.exec_module(module)
module.SOURCE=here.parent;module.OUTPUT=here/'game-export';module.OUTPUT.mkdir(exist_ok=True)
module.SETTINGS['fern-clump']=dict(axis=0,metres=1.0,texture=1024,budgets=(100000,))

def simplify(obj,budget):
    bm=bmesh.new();bm.from_mesh(obj.data)
    edges=[e for e in bm.edges if len(e.link_faces)>2]
    bmesh.ops.split_edges(bm,edges=edges);bm.to_mesh(obj.data);bm.free()
    bpy.ops.object.select_all(action='DESELECT');obj.select_set(True);bpy.context.view_layer.objects.active=obj
    modifier=obj.modifiers.new('Source-derived foliage reduction','DECIMATE');modifier.ratio=.075
    bpy.ops.object.modifier_apply(modifier=modifier.name)
    modifier=obj.modifiers.new('Explicit triangles','TRIANGULATE');bpy.ops.object.modifier_apply(modifier=modifier.name)
    for face in obj.data.polygons:face.use_smooth=True
    count=module.triangles(obj)
    if count>budget:raise RuntimeError('Fern review mesh exceeds initial review budget: '+str(count))
    print('FERN_REVIEW_LOD0 triangles',count,'vertices',len(obj.data.vertices),'splitEdges',len(edges),flush=True)

module.simplify=simplify
module.prepare('fern-clump',0)
