"""Source-derived 3mm surface retopology for the closed generated wood cupboard.

Only the normalized game copy is changed. The generated dense original retains
its exact geometry and PBR, which supply all game colour/ORM/tangent detail.
"""
import bpy
from mathutils import Vector
from mathutils.bvhtree import BVHTree


def preprocess(game):
    bpy.ops.object.select_all(action='DESELECT')
    game.select_set(True)
    bpy.context.view_layer.objects.active = game
    before = len(game.data.polygons)
    game.data.remesh_voxel_size = .003
    game.data.remesh_voxel_adaptivity = 0
    game.data.use_remesh_preserve_volume = True
    bpy.ops.object.voxel_remesh()
    game.data.update()
    tree = BVHTree.FromObject(game, bpy.context.evaluated_depsgraph_get())
    front = tree.ray_cast(Vector((0, .9, .42)), Vector((0, -1, 0)), 1.8)
    if front[0] is None:
        raise RuntimeError('Source-derived low cabinet lost the real closed door surfaces')
    bounds = [Vector(point) for point in game.bound_box]
    size = [max(point[a] for point in bounds) - min(point[a] for point in bounds) for a in range(3)]
    if not (.8 < size[0] < 1.4 and .3 < size[1] < .65 and .82 < size[2] < .88):
        raise RuntimeError('Source-derived low cabinet changed the inspected silhouette')
    return dict(tool='Source-derived 3mm voxel surface retopology before LOD collapse',
                voxelMetres=.003, originalUnchanged=True, preserveVolume=True,
                sourceCopyFacesBefore=before, sourceCopyFacesAfter=len(game.data.polygons),
                dimensionsMetres=size, realFrontRayHit=list(front[0]),
                note='Closed twin door cupboard silhouette retained from generated source; '
                     'untouched high model supplies all colour/ORM/normal detail')
