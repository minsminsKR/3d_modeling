"""Source-derived architectural retopology and square mating boundaries.

Called by per-asset finish_geometry.py after source normalization. Original GLB
is untouched; coordinates stay registered for high-to-low PBR/normal baking.
"""
import bpy
import bmesh

def bounds(mesh):
    return tuple(min(v.co[a] for v in mesh.vertices) for a in range(3)), tuple(max(v.co[a] for v in mesh.vertices) for a in range(3))

def cut(mesh, axis, value, keep_positive):
    bm = bmesh.new()
    bm.from_mesh(mesh)
    point = [0., 0., 0.]
    normal = [0., 0., 0.]
    point[axis] = value
    normal[axis] = 1
    result = bmesh.ops.bisect_plane(bm, geom=list(bm.verts)+list(bm.edges)+list(bm.faces),
        plane_co=point, plane_no=normal, clear_inner=keep_positive, clear_outer=not keep_positive, dist=.00001)
    boundary = [item for item in result['geom_cut'] if isinstance(item, bmesh.types.BMEdge) and item.is_boundary]
    if boundary:
        bmesh.ops.holes_fill(bm, edges=boundary, sides=0)
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    bm.to_mesh(mesh)
    bm.free()
    mesh.update()

def preprocess(near, kind):
    low, high = bounds(near.data)
    before = len(near.data.polygons)
    # Voxel retopology removes the generated microtriangle tears before collapse.
    # Relief finer than this is transferred from the unchanged source into the
    # coherent tangent normal atlas, rather than duplicated at every floor cell.
    near.data.remesh_voxel_size = .005 if kind != 'wall-panel' else .008
    near.data.remesh_voxel_adaptivity = 0
    bpy.context.view_layer.objects.active = near
    near.select_set(True)
    bpy.ops.object.voxel_remesh()
    # Remove only the outer rounding. Adjacent modules meet on straight planes.
    if kind == 'wall-panel':
        cut(near.data, 0, low[0]+.012, True)
        cut(near.data, 0, high[0]-.012, False)
        cut(near.data, 2, low[2]+.006, True)
        cut(near.data, 2, high[2]-.006, False)
    else:
        for axis in (0, 1):
            cut(near.data, axis, low[axis]+.012, True)
            cut(near.data, axis, high[axis]-.012, False)
        if kind == 'ceiling-panel':
            # The generated source is a 1.2m four-panel assembly. Retain one
            # genuine quadrant for the existing 60cm missing-panel grid.
            cut(near.data, 0, (low[0]+high[0])*.5, True)
            cut(near.data, 1, (low[1]+high[1])*.5, True)
    after_low, after_high = bounds(near.data)
    return dict(method='Source-derived voxel retopology and planar mating cuts',
        voxelMetres=float(near.data.remesh_voxel_size), originalRegisteredCoordinates=True,
        inputPolygons=before, derivedPolygons=len(near.data.polygons), boundsMin=after_low, boundsMax=after_high,
        quadrant=(kind == 'ceiling-panel'), unchangedSource='original.glb')
