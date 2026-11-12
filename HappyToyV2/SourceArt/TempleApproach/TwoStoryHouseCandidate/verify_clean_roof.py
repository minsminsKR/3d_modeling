"""Check the local roof cleanup against the retained prepared house.

Run with Blender 4.2.23: blender -b --threads 2 --python-exit-code 1
--python verify_clean_roof.py. These are geometric sample checks; game-camera
visual review remains separate.
"""
from pathlib import Path
import bpy
from mathutils import Vector
from mathutils.bvhtree import BVHTree

root = Path(__file__).resolve().parent
points = {
    'mass-centre': (-.195, .245), 'mass-edge': (-.30, .16),
    'tank': (-.07, -.08), 'shed': (.19, .16), 'roof-clear': (-.1, .04),
    'fragments': (-.12, -.19), 'floating-fragment': (-.139247, -.277936),
}
def sample(path):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(path)); bpy.context.view_layer.update()
    objects = [obj for obj in bpy.context.scene.objects if obj.type == 'MESH']
    if len(objects) != 1: raise RuntimeError('Expected one house mesh')
    obj = objects[0]; mesh = obj.data
    tree = BVHTree.FromPolygons([obj.matrix_world @ v.co for v in mesh.vertices],
        [list(face.vertices) for face in mesh.polygons], all_triangles=True)
    result = {}
    for label, (x, y) in points.items():
        heights = []
        for start in (1.3, .754):
            hit, _, _, _ = tree.ray_cast(Vector((x, y, start)), Vector((0, 0, -1)), 2)
            heights.append(None if hit is None else hit.z)
        result[label] = heights
    return result
original = sample(root / 'TwoStoryHouse.fbx')
clean = sample(root / 'TwoStoryHouseClean.fbx')
for key in ('mass-centre', 'mass-edge'):
    if original[key][0] < .8 or not .749 < clean[key][0] < .753:
        raise RuntimeError('Rooftop mass not removed at ' + key)
    if abs(original[key][1] - clean[key][1]) > .00001:
        raise RuntimeError('Roof floor changed at ' + key)
if original['fragments'][0] < .9 or not .749 < clean['fragments'][0] < .753:
    raise RuntimeError('Rooftop fragment not removed')
if abs(original['fragments'][1] - clean['fragments'][1]) > .00001:
    raise RuntimeError('Roof floor changed under fragment')
if original['floating-fragment'][0] is None or original['floating-fragment'][0] < .9:
    raise RuntimeError('Original floating fragment was not found at the reviewed game-camera location')
if clean['floating-fragment'][0] is not None and clean['floating-fragment'][0] > .82:
    raise RuntimeError('Floating fragment remains above the roof')
for key in ('tank', 'shed', 'roof-clear'):
    if abs(original[key][0] - clean[key][0]) > .00001:
        raise RuntimeError('Roof feature changed at ' + key)
print('CLEAN_ROOF_GEOMETRIC_PROBES_PASS', original, clean)
