"""Blender 4.0.2 non-mutating measurements of the generated decay originals."""
from pathlib import Path
import argparse
import json
import sys
import bpy
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[2]
parser = argparse.ArgumentParser()
parser.add_argument('key')
args = parser.parse_args(sys.argv[sys.argv.index('--') + 1:])
source = ROOT / 'SourceArt/SchoolRenewal' / args.key
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=str(source / 'original.glb'))
objects = [obj for obj in bpy.context.scene.objects if obj.type == 'MESH']
points = [obj.matrix_world @ Vector(corner) for obj in objects for corner in obj.bound_box]
low = [min(p[axis] for p in points) for axis in range(3)]
high = [max(p[axis] for p in points) for axis in range(3)]
report = {'key': args.key, 'blenderAxes': 'X right, Y depth, Z up', 'boundsMin': low,
          'boundsMax': high, 'size': [b-a for a,b in zip(low,high)],
          'meshes': [{'name': obj.name, 'vertices': len(obj.data.vertices),
                      'triangles': sum(len(p.vertices)-2 for p in obj.data.polygons),
                      'materials': [mat.name for mat in obj.data.materials]} for obj in objects],
          'images': [{'name': image.name, 'size': list(image.size)} for image in bpy.data.images]}
(source / 'original-measurements.json').write_text(json.dumps(report, indent=2)+'\n', encoding='utf-8')
print('DECAY_SOURCE_MEASUREMENTS', json.dumps(report), flush=True)
