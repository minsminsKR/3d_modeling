"""Repair fragmented generated sheet-metal topology only on the game copy.

3mm source-derived voxel retopology keeps the closed cabinet silhouette, legs,
handles and door recesses. Dense original geometry supplies all PBR/normal detail.
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
    hit = tree.ray_cast(Vector((0, .8, .055)), Vector((0, -1, 0)), 1.6)
    if hit[0] is not None:
        raise RuntimeError('Generated cabinet retopology filled the space below its feet')
    bounds = [Vector(point) for point in game.bound_box]
    dimensions = [max(point[a] for point in bounds) - min(point[a] for point in bounds) for a in range(3)]
    if not (.65 < dimensions[0] < .80 and .35 < dimensions[1] < .50 and 1.75 < dimensions[2] < 1.85):
        raise RuntimeError('Generated cabinet retopology changed the inspected silhouette')
    return dict(tool='Source-derived 3mm voxel surface retopology before low-LOD collapse',
                originalUnchanged=True, voxelMetres=.003, preserveVolume=True,
                sourceCopyFacesBefore=before, sourceCopyFacesAfter=len(game.data.polygons),
                dimensionsMetres=dimensions, undersideCentreRay='Clear at55mm above original feet',
                note='Closed cabinet remains generated shape; original dense vents/door recesses '
                     'supply high-to-low tangent normal and original colour/ORM transfer')
