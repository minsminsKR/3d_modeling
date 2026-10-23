"""Remove only inspected opaque pane surfaces from the generated frame copy.

finish_asset.py runs preprocess after normalization, before simplification and
all UV/colour/ORM/normal bakes. No vertices of the preserved original are edited.
The existing school glass is inserted behind these true geometric openings.
"""
from pathlib import Path
import json
import bpy
import bmesh
from mathutils import Vector
from mathutils.bvhtree import BVHTree

SOURCE = Path(__file__).resolve().parent


def preprocess(game):
    path = SOURCE / 'pane-cutouts.json'
    if not path.is_file():
        raise RuntimeError('Actual generated pane boundaries have not been inspected')
    config = json.loads(path.read_text(encoding='utf-8'))
    panes = config['panes']
    if not panes:
        raise RuntimeError('No inspected glass opening regions')
    source_faces_before = len(game.data.polygons)
    bpy.ops.object.select_all(action='DESELECT')
    game.select_set(True)
    bpy.context.view_layer.objects.active = game
    game.data.remesh_voxel_size = config.get('voxelMetres', .004)
    game.data.remesh_voxel_adaptivity = 0
    game.data.use_remesh_preserve_volume = True
    bpy.ops.object.voxel_remesh()
    game.data.update()
    bm = bmesh.new()
    bm.from_mesh(game.data)
    initial = len(bm.faces)
    # Exact boundary cuts retain narrow rails even where a source triangle
    # crosses a pane edge. UV custom data is interpolated by Blender.
    cuts = set()
    for pane in panes:
        a, b = pane['min'], pane['max']
        if not (a[0] < b[0] and a[1] < b[1]):
            raise ValueError('Pane rectangle is inverted')
        cuts.update(((0, a[0]), (0, b[0]), (2, a[1]), (2, b[1])))
    for axis, coordinate in sorted(cuts):
        normal = Vector((1, 0, 0) if axis == 0 else (0, 0, 1))
        point = normal * coordinate
        bmesh.ops.bisect_plane(bm, geom=list(bm.verts) + list(bm.edges) + list(bm.faces),
                              dist=.000001, plane_co=point, plane_no=normal,
                              clear_inner=False, clear_outer=False)
    remove = []
    for face in bm.faces:
        centre = face.calc_center_median()
        if any(p['min'][0] + .000001 < centre.x < p['max'][0] - .000001
               and p['min'][1] + .000001 < centre.z < p['max'][1] - .000001 for p in panes):
            remove.append(face)
    if not remove or len(remove) > len(bm.faces) * .95:
        bm.free()
        raise RuntimeError('Pane segmentation removed zero or almost all generated surfaces')
    removed = len(remove)
    bmesh.ops.delete(bm, geom=remove, context='FACES')
    isolated = [v for v in bm.verts if not v.link_faces]
    if isolated:
        bmesh.ops.delete(bm, geom=isolated, context='VERTS')
    # Sub-millimetre reconstruction cracks create thousands of boundary vertices
    # in this generated frame. Weld only the normalized game copy before its
    # low-poly collapse; the untouched high source restores the fine detail.
    weld_distance = config.get('derivedWeldMetres', .0008)
    vertices_before_weld = len(bm.verts)
    bmesh.ops.remove_doubles(bm, verts=list(bm.verts), dist=weld_distance)
    bmesh.ops.dissolve_degenerate(bm, edges=list(bm.edges), dist=.00001)
    # The generated frosted panes contain tiny detached triangles. After the
    # real pane separation, discard only sub-22mm disconnected remnants; these
    # otherwise impose an unnecessary open-boundary floor on both game LODs.
    for vertex in bm.verts:
        vertex.tag = False
    for face in bm.faces:
        face.tag = False
    tiny = []
    tiny_count = 0
    threshold = config.get('tinyDisconnectedMaxExtent', .022)
    thin_ratio = config.get('thinDetachedAreaRatio', .004)
    for vertex in list(bm.verts):
        if vertex.tag:
            continue
        stack = [vertex]
        vertex.tag = True
        component = []
        area = 0.0
        lo = vertex.co.copy()
        hi = vertex.co.copy()
        while stack:
            current = stack.pop()
            component.append(current)
            for face in current.link_faces:
                if not face.tag:
                    face.tag = True
                    area += face.calc_area()
            for axis in range(3):
                lo[axis] = min(lo[axis], current.co[axis])
                hi[axis] = max(hi[axis], current.co[axis])
            for edge in current.link_edges:
                other = edge.other_vert(current)
                if not other.tag:
                    other.tag = True
                    stack.append(other)
        extent = max(hi - lo)
        if extent < threshold or (extent < 1.2 and area < extent * extent * thin_ratio):
            tiny.extend(component)
            tiny_count += 1
    if tiny:
        bmesh.ops.delete(bm, geom=tiny, context='VERTS')
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    tree = BVHTree.FromBMesh(bm)
    for pane in panes:
        x = (pane['min'][0] + pane['max'][0]) * .5
        z = (pane['min'][1] + pane['max'][1]) * .5
        hit = tree.ray_cast(Vector((x, 3, z)), Vector((0, -1, 0)), 6)
        if hit[0] is not None:
            bm.free()
            raise RuntimeError('Derived pane still blocks the classroom view: ' + pane.get('name', '?'))
    bm.to_mesh(game.data)
    bm.free()
    game.data.update()
    return dict(tool='Generated frame pane separation before LOD/bake',
                source='pane-cutouts.json', coordinateSystem='Normalized Blender X/Z metres; through all Y',
                paneCount=len(panes), panes=panes, sourceCopyFacesBefore=initial,
                unchangedDenseSourceFaces=source_faces_before, voxelMetres=config.get('voxelMetres', .004),
                removedFaces=removed, sourceCopyFacesAfter=len(game.data.polygons),
                tinyDisconnectedRemnantsRemoved=tiny_count, tinyDisconnectedMaxExtent=threshold,
                thinDetachedAreaRatio=thin_ratio,
                derivedWeldMetres=weld_distance, verticesMerged=vertices_before_weld-len(game.data.vertices),
                verification='All pane centre rays pass through the actual derived frame mesh',
                glass='Existing school transparent glass is assembled behind the generated frame')
