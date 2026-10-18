"""Game-only continuous surface, reviewed source skin and real PBR reprojection."""
import json
import sys
from pathlib import Path
import bpy
import bmesh
import numpy as np
from mathutils import Vector
from mathutils.kdtree import KDTree
from mathutils.bvhtree import BVHTree

HERE=Path(__file__).resolve().parent
PROJECT=HERE.parents[1]
sys.path.insert(0,str(PROJECT/'SourceArt/CreatureStudio/tools'))
import author_creature as shared
import bake_game_atlas
import bake_surface_atlas as atlas


def build(high,rig):
    rig.data.pose_position='REST'
    obj=high.copy();obj.data=high.data.copy();bpy.context.collection.objects.link(obj)
    obj.name='Lady_LOD0';obj.data.name='Lady continuous game surface'
    obj.hide_set(False);obj.hide_render=False
    for mod in list(obj.modifiers):obj.modifiers.remove(mod)
    for group in list(obj.vertex_groups):obj.vertex_groups.remove(group)
    bpy.ops.object.select_all(action='DESELECT');obj.select_set(True);bpy.context.view_layer.objects.active=obj
    p=atlas.array(high.data.vertices,'co',3)
    span=float(np.ptp(p,axis=0).max())
    obj.data.remesh_voxel_size=span/800;obj.data.remesh_voxel_adaptivity=0
    bpy.ops.object.voxel_remesh()
    for layer in list(obj.data.uv_layers):obj.data.uv_layers.remove(layer)
    obj.data.calc_loop_triangles();count=len(obj.data.loop_triangles)
    print('[Lady surface] outer volume triangles='+str(count),flush=True)
    modifier=obj.modifiers.new('Game continuous outer surface only','DECIMATE')
    modifier.ratio=min(1,80000/count);modifier.use_collapse_triangulate=True
    bpy.ops.object.modifier_apply(modifier=modifier.name)
    shared.clean_game_topology(obj)
    for poly in obj.data.polygons:poly.use_smooth=True
    modifier=obj.modifiers.new('Lady reviewed source skeleton','ARMATURE');modifier.object=rig
    modifier.use_deform_preserve_volume=False
    from refine_game import finish
    finish(high,obj,rig)
    return obj
