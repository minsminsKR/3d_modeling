"""Create the continuous game UV atlas for BVH source-surface PBR transfer.

Blender 4.0.2; originals, source UVs and 8K images remain unchanged.
"""
import bpy


def unwrap(lod):
    bpy.ops.object.select_all(action='DESELECT');lod.select_set(True)
    bpy.context.view_layer.objects.active=lod
    bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.uv.smart_project(angle_limit=1.151917,island_margin=.003,correct_aspect=True,scale_to_bounds=True)
    bpy.ops.object.mode_set(mode='OBJECT')
    lod['studio_continuous_atlas']=True

