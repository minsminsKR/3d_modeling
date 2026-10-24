"""Repair only the generated radiator game copy while preserving open sections.

The intact dense service GLB supplies the final colour/ORM and tangent detail.
A 2.5mm voxel surface removes submillimetre reconstruction cracks before bounded
LOD collapse; no replacement cylinders, ribs or other base geometry are added.
"""
import bpy


def preprocess(game):
    bpy.context.view_layer.objects.active = game
    bpy.ops.object.select_all(action='DESELECT')
    game.select_set(True)
    before = len(game.data.polygons)
    game.data.remesh_voxel_size = .0025
    game.data.remesh_voxel_adaptivity = 0
    game.data.use_remesh_preserve_volume = True
    bpy.ops.object.voxel_remesh()
    game.data.update()
    return dict(tool='Source-derived voxel surface repair before bounded LOD collapse',
                voxelMetres=.0025, originalUnchanged=True, preserveVolume=True,
                sourceCopyFacesBefore=before, sourceCopyFacesAfter=len(game.data.polygons),
                intent='Keep cast-iron section gaps, valve, manifolds and floor feet while removing generated microcracks')
