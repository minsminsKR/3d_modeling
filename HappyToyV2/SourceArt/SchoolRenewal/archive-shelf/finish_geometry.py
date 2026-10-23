"""Retopologize only the generated shelf copy; preserve its three real openings.

The service original remains untouched. No primitive shelf is substituted.
3mm voxel surface reconstruction fixes fragmented self-intersections before the
original dense geometry transfers colour/ORM and tangent normals to game LODs.
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
    openings = []
    for name, z in (('lower compartment', .24), ('middle compartment', .64), ('upper compartment', 1.04)):
        origin = Vector((0, .65, z))
        hit = tree.ray_cast(origin, Vector((0, -1, 0)), .65)
        if hit[0] is not None:
            raise RuntimeError('Source-derived voxel shelf filled the real opening: ' + name)
        # The intact source back remains behind the open compartment.
        back = tree.ray_cast(origin, Vector((0, -1, 0)), 1.2)
        if back[0] is None or not -.32 < back[0].y < -.12:
            raise RuntimeError('Source-derived shelf lost its real back panel: ' + name)
        openings.append(dict(name=name, centreHeightMetres=z, clearToDepthY=0, backY=back[0].y))
    # At mid-depth the top, two dividers and bottom remain actual thick boards.
    intersections = []
    origin = Vector((0, 0, 1.5))
    for _ in range(40):
        hit = tree.ray_cast(origin, Vector((0, 0, -1)), 2)
        if hit[0] is None:
            break
        intersections.append(float(hit[0].z))
        origin = hit[0] - Vector((0, 0, .001))
    print('SHELF_VOXEL_BOARD_CROSS_SECTION', intersections, flush=True)
    if len(intersections) < 8:
        raise RuntimeError('Source-derived shelf lost a structural board cross section')
    # The generated boards contain hollow internal shells. Measure their outer
    # visible board envelope rather than mistaking a shell wall for the board.
    groups = []
    for z in intersections:
        if not groups or groups[-1][-1] - z > .08:
            groups.append([z])
        else:
            groups[-1].append(z)
    if len(groups) != 4:
        raise RuntimeError('Shelf must retain four separate structural board envelopes')
    boards = []
    for group in groups:
        thickness = group[0] - group[-1]
        if thickness < .015:
            raise RuntimeError('Source-derived shelf board became thinner than 15mm: ' + str(thickness))
        boards.append(dict(top=group[0], bottom=group[-1], outerThicknessMetres=thickness,
                           surfaceIntersections=group))
    return dict(tool='Source-derived 3mm voxel surface retopology before low-LOD collapse',
                originalUnchanged=True, voxelMetres=.003, preserveVolume=True,
                sourceCopyFacesBefore=before, sourceCopyFacesAfter=len(game.data.polygons),
                openingValidation=openings, midDepthBoardCrossSections=boards,
                note='Retains generated width/height/depth, real three compartments and back; '
                     'original normalized high geometry remains the source of all game PBR/normal bakes')
