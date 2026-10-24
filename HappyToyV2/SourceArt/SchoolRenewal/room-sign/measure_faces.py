"""Read-only Blender ray measurements of the designated generated sign."""
from pathlib import Path
import json
import bpy
from mathutils import Vector
from mathutils.bvhtree import BVHTree

source = Path(__file__).resolve().parent
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=str(source / 'original.glb'))
objects = [obj for obj in bpy.context.scene.objects if obj.type == 'MESH']
if len(objects) != 1:
    raise RuntimeError('Expected one original generated primitive')
original = objects[0]
vertices = [original.matrix_world @ vertex.co for vertex in original.data.vertices]
tree = BVHTree.FromPolygons(vertices, [tuple(face.vertices) for face in original.data.polygons])
measurements = []
for x in (-.45, -.36, -.27, -.18, 0, .18, .27, .36, .45):
    for z in (-.10, 0, .10):
        front = tree.ray_cast(Vector((x, .20, z)), Vector((0, -1, 0)), .40)[0]
        reverse = tree.ray_cast(Vector((x, -.20, z)), Vector((0, 1, 0)), .40)[0]
        measurements.append(dict(x=x, z=z, frontY=front.y if front else None,
                                 reverseY=reverse.y if reverse else None))
report = dict(coordinates='Unmodified Blender world coordinates after glTF import',
              source='original.glb', sourceUnchanged=True,
              boundsMin=[min(vertex[axis] for vertex in vertices) for axis in range(3)],
              boundsMax=[max(vertex[axis] for vertex in vertices) for axis in range(3)],
              broadFaceRays=measurements)
(source / 'source-face-measurements.json').write_text(json.dumps(report, indent=2)+'\n', encoding='utf-8')
print('ROOM_SIGN_SOURCE_FACE_MEASUREMENTS '+json.dumps(report), flush=True)
