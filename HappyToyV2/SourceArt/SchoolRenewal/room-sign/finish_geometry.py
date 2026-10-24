"""Finish the generated sign while retaining the untouched designated original.

The service painted meaningless dark marks on the reverse enamel face. The
derived transfer copy reuses the same model's clean front enamel UVs at matching
X/Z coordinates. Geometry, source images and the original GLB stay unchanged;
only the derived game colour/ORM bake uses this corrected paint source.
"""
from array import array
import hashlib

import bpy
from mathutils import Vector
from mathutils.bvhtree import BVHTree
from mathutils.geometry import barycentric_transform


def _uv_hash(mesh):
    coordinates = array('f', [0.0]) * (len(mesh.uv_layers.active.data) * 2)
    mesh.uv_layers.active.data.foreach_get('uv', coordinates)
    return hashlib.sha256(coordinates.tobytes()).hexdigest()


def prepare_transfer_source(original):
    """Return a hidden original-geometry copy with repaired reverse enamel UVs.

    All selection/ray coordinates are in the unchanged Blender source space:
    X along the projecting sign, +Y clean front, Z up. Barycentric transfer uses
    the exact generated front triangles and their actual original UV seams.
    """
    if not original.data.uv_layers.active:
        raise RuntimeError('Generated sign has no original paint UVs')
    mesh = original.data
    mesh.calc_loop_triangles()
    matrix = original.matrix_world.copy()
    normal_matrix = matrix.to_3x3().inverted().transposed()
    vertices = [matrix @ vertex.co for vertex in mesh.vertices]
    normals = [(normal_matrix @ face.normal).normalized() for face in mesh.polygons]
    centres = [matrix @ face.center for face in mesh.polygons]

    def board(face_index):
        centre = centres[face_index]
        return centre.x < .315 and abs(centre.z) < .18

    fronts = [triangle for triangle in mesh.loop_triangles
              if normals[triangle.polygon_index].y > .25 and board(triangle.polygon_index)]
    reverse = [face for face in mesh.polygons if normals[face.index].y < -.25 and board(face.index)]
    if len(fronts) < 100 or len(reverse) < 100:
        raise RuntimeError('Could not identify both actual broad generated enamel faces')
    tree = BVHTree.FromPolygons(vertices, [tuple(triangle.vertices) for triangle in fronts], all_triangles=True)
    front_y = max(point.y for point in vertices) + .10
    back_y = min(point.y for point in vertices) - .10
    ray_distance = front_y - back_y
    original_uv = mesh.uv_layers.active.data
    original_uv_hash = _uv_hash(mesh)
    original_coordinates = array('f', [0.0]) * (len(original_uv) * 2)
    original_uv.foreach_get('uv', original_coordinates)
    original_layer_name = mesh.uv_layers.active.name
    derived = original.copy()
    derived.data = mesh.copy()
    derived.name = 'Derived paint transfer source — reverse uses actual blank front enamel'
    bpy.context.scene.collection.objects.link(derived)
    derived.hide_render = True
    derived.hide_set(True)
    # Allocate independent UV custom data explicitly. A copied Blender mesh may
    # initially share UV custom-data storage until it is made writable.
    for layer in list(derived.data.uv_layers):
        derived.data.uv_layers.remove(layer)
    derived_layer = derived.data.uv_layers.new(name=original_layer_name, do_init=False)
    derived_layer.active_render = True
    derived.data.uv_layers.active = derived_layer
    derived_uv = derived_layer.data
    derived_uv.foreach_set('uv', original_coordinates)
    if _uv_hash(mesh) != original_uv_hash:
        raise RuntimeError('Allocating independent derived enamel UVs changed original custom data')
    transfers = {}
    nearest_count = 0
    for face in reverse:
        for loop_index in face.loop_indices:
            vertex_index = mesh.loops[loop_index].vertex_index
            if vertex_index not in transfers:
                point = vertices[vertex_index]
                hit, _, triangle_index, _ = tree.ray_cast(Vector((point.x, front_y, point.z)),
                                                         Vector((0, -1, 0)), ray_distance)
                if hit is None:
                    # Opposite generated outlines differ slightly. Only a close
                    # actual front-surface hit may repair those edge vertices.
                    hit, _, triangle_index, distance = tree.find_nearest(point, .10)
                    if hit is None or distance > .10:
                        raise RuntimeError('Reverse enamel vertex has no corresponding generated front surface')
                    nearest_count += 1
                triangle = fronts[triangle_index]
                a, b, c = (vertices[index] for index in triangle.vertices)
                uv_a, uv_b, uv_c = (Vector((*original_uv[index].uv, 0)) for index in triangle.loops)
                value = barycentric_transform(hit, a, b, c, uv_a, uv_b, uv_c)
                transfers[vertex_index] = (value.x, value.y)
            derived_uv[loop_index].uv = transfers[vertex_index]
    derived.data.update()
    if _uv_hash(mesh) != original_uv_hash:
        coordinates = array('f', [0.0]) * len(original_coordinates)
        mesh.uv_layers.active.data.foreach_get('uv', coordinates)
        differences = [(index, float(a), float(b)) for index, (a, b) in enumerate(zip(original_coordinates, coordinates)) if a != b]
        raise RuntimeError('Reverse enamel repair modified the preserved original UVs: '+str(dict(
            count=len(differences), first=differences[:3], meshDistinct=mesh!=derived.data)))

    # Portable measurements let Unity place ink on the real panel faces rather
    # than on the deeper mounting bracket's overall bounding envelope.
    all_tree = BVHTree.FromPolygons(vertices, [tuple(triangle.vertices) for triangle in mesh.loop_triangles], all_triangles=True)
    measurements = []
    for x in (-.36, -.18, 0, .18):
        for z in (-.10, 0, .10):
            front = all_tree.ray_cast(Vector((x, front_y, z)), Vector((0, -1, 0)), ray_distance)[0]
            back = all_tree.ray_cast(Vector((x, back_y, z)), Vector((0, 1, 0)), ray_distance)[0]
            if front is None or back is None:
                raise RuntimeError('Generated room sign lost a broad enamel surface measurement')
            measurements.append(dict(x=x, z=z, frontY=front.y, reverseY=back.y))
    report = dict(
        tool='Source-derived barycentric UV repair of reverse enamel only',
        originalUnchanged=True,
        sourceImagesUnchanged=True,
        sourceUvSha256=original_uv_hash,
        derivedUvSha256=_uv_hash(derived.data),
        coordinates='Unchanged Blender source space: width X, clean front +Y, up Z',
        selection=dict(reverseNormalYLessThan=-.25, boardCentreXLessThan=.315, boardCentreAbsZLessThan=.18),
        sourceMount='Actual original mounting plate is at +X; the final rigid 180-degree Z turn puts it at canonical -X',
        originalGeometryVertices=len(mesh.vertices),
        originalGeometryTriangles=len(mesh.loop_triangles),
        actualCleanFrontTriangles=len(fronts),
        repairedReversePolygons=len(reverse),
        repairedReverseLoops=sum(len(face.loop_indices) for face in reverse),
        transferredReverseVertices=len(transfers),
        nearestEdgeFallbackVertices=nearest_count,
        maximumEdgeFallbackSourceUnits=.10,
        sourceBounds=dict(min=[min(point[axis] for point in vertices) for axis in range(3)],
                          max=[max(point[axis] for point in vertices) for axis in range(3)]),
        broadFaceRays=measurements,
        normalBake='Unchanged original geometry remains the high-to-low tangent normal source',
        limitation='Native Korean ink is applied separately after the game model is imported')
    return derived, report


def preprocess(game):
    """Repair only the derived game surface at 2mm before LOD simplification."""
    bpy.ops.object.select_all(action='DESELECT')
    game.select_set(True)
    bpy.context.view_layer.objects.active = game
    before = len(game.data.polygons)
    game.data.remesh_voxel_size = .002
    game.data.remesh_voxel_adaptivity = 0
    game.data.use_remesh_preserve_volume = True
    bpy.ops.object.voxel_remesh()
    game.data.update()
    bpy.context.view_layer.update()
    points = [Vector(point) for point in game.bound_box]
    size = [max(point[axis] for point in points) - min(point[axis] for point in points) for axis in range(3)]
    if not (.60 < size[0] < .72 and .03 < size[1] < .10 and .25 < size[2] < .40):
        raise RuntimeError('Source-derived sign surface repair changed the broad panel/bracket silhouette')
    # The dependency graph can still contain the pre-transform/pre-remesh copy
    # in background Blender. Probe the actual newly written mesh coordinates.
    tree = BVHTree.FromPolygons([vertex.co.copy() for vertex in game.data.vertices],
                                [tuple(face.vertices) for face in game.data.polygons])
    probes = []
    # The normalized generated source is floor-centred, not centre-pivoted.
    for x in (-.10, .10, .23):
        z = size[2] * .5
        front = tree.ray_cast(Vector((x, .20, z)), Vector((0, -1, 0)), .40)[0]
        back = tree.ray_cast(Vector((x, -.20, z)), Vector((0, 1, 0)), .40)[0]
        if front is None or back is None or front.y - back.y <= 0:
            raise RuntimeError('Source-derived room sign face probes failed: '+str(dict(
                dimensions=size, x=x, height=z, front=list(front) if front else None,
                reverse=list(back) if back else None)))
        probes.append(dict(x=x, height=z, frontY=front.y, reverseY=back.y, thicknessMetres=front.y-back.y))
    return dict(tool='Source-derived 2mm sign surface repair before LOD/PBR bake',
                originalUnchanged=True, voxelMetres=.002, preserveVolume=True,
                sourceCopyFacesBefore=before, sourceCopyFacesAfter=len(game.data.polygons),
                dimensionsMetres=size, physicalEnamelFaceRays=probes,
                geometry='Generated enclosure, frame and wall bracket retained; no substitute plaque')
