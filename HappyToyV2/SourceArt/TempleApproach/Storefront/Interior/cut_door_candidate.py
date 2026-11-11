"""Disposable experiment only: derive a real doorway from the saved scan.

Open the original StorefrontEditable.blend or a copy as the Blender input.
Outputs are written next to this script, never over the original source.
"""
from pathlib import Path
import bpy
import bmesh
from mathutils import Vector
from mathutils.bvhtree import BVHTree

work = Path(__file__).resolve().parent
obj = bpy.data.objects['geometry_0']
bpy.ops.object.select_all(action='DESELECT')
obj.select_set(True); bpy.context.view_layer.objects.active = obj
initial_faces = len(obj.data.polygons)
decimate = obj.modifiers.new('Experimental game face budget', 'DECIMATE')
decimate.ratio = 250000 / initial_faces
bpy.ops.object.modifier_apply(modifier=decimate.name)
print('DECIMATED', initial_faces, len(obj.data.polygons), flush=True)
assert len(obj.data.uv_layers) == 1

# Based on the orthographic front-view image and ray audit. Only the two thin
# scanned door faces at y≈-0.24 are removed; rear wall, windows, frame and
# awning are outside the cutter. Centroid boundaries leave a dense, slightly
# irregular edge; a game-ready version would need a proper door jamb.
limits = (-.275, -.028, -.35, -.12, -.01, .465)
matrix = obj.matrix_world.copy()
mesh = bmesh.new(); mesh.from_mesh(obj.data)
remove = []
for face in mesh.faces:
    p = matrix @ face.calc_center_median()
    if limits[0] <= p.x <= limits[1] and limits[2] <= p.y <= limits[3] and limits[4] <= p.z <= limits[5]:
        remove.append(face)
print('CUT_FACES', len(remove), 'BEFORE', len(mesh.faces), flush=True)
bmesh.ops.delete(mesh, geom=remove, context='FACES')
mesh.to_mesh(obj.data); mesh.free(); obj.data.update()
print('CANDIDATE', len(obj.data.vertices), len(obj.data.polygons),
      'UV', [layer.name for layer in obj.data.uv_layers], flush=True)
assert len(obj.data.uv_layers) == 1

# Probe representative door points. A remaining hit near -0.24 means that
# face selection did not fully open the doorway.
bpy.context.view_layer.update()
tree = BVHTree.FromObject(obj, bpy.context.evaluated_depsgraph_get())
inverse = matrix.inverted()
direction = (inverse.to_3x3() @ Vector((0, 1, 0))).normalized()
for x in (-.25,-.15,-.05):
    for z in (.03,.15,.30,.43):
        location, normal, index, distance = tree.ray_cast(inverse @ Vector((x,-1,z)), direction, 3)
        y = None if location is None else round((matrix @ location).y, 5)
        print('DOOR_POST_CUT', x, z, y, flush=True)
        if y is not None and y < -.12:
            raise RuntimeError('Doorway remains obstructed for ray %s %s: %s' % (x,z,y))

bpy.ops.wm.save_as_mainfile(filepath=str(work / 'Storefront-door-candidate.blend'))
