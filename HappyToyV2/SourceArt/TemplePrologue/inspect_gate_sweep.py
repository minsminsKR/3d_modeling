"""Inspect source-mesh/frame intersections during the optional gate opening.

Run Blender in background on a private desktop. This CPU-only diagnostic
does not change the blend or validate Unity's runtime appearance or collision.
"""
import argparse
import hashlib
import json
import math
from pathlib import Path
import sys

import bpy
from mathutils import Vector
from mathutils.bvhtree import BVHTree

parser = argparse.ArgumentParser()
parser.add_argument('--output', required=True, help='New JSON file for the observations')
args = parser.parse_args(sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else [])
out = Path(args.output).resolve()
if out.exists(): raise RuntimeError('Choose a new report path')
root = Path(__file__).resolve().parent / 'gate-opening'
source = root / 'gate-opening.blend'
report = json.loads((root / 'geometry-report.json').read_text(encoding='utf-8'))
digest = hashlib.sha256(source.read_bytes()).hexdigest()
if digest != report['editableSha256']: raise RuntimeError('Editable gate hash differs from report')
bpy.ops.wm.open_mainfile(filepath=str(source))
bpy.data.collections['PROTOTYPE - thin leaf alternative'].hide_viewport = False
scene = bpy.context.scene

def make_tree(name):
    obj = bpy.data.objects[name]
    mesh = obj.data
    mesh.calc_loop_triangles()
    vertices = [obj.matrix_world @ vertex.co for vertex in mesh.vertices]
    triangles = [tuple(tri.vertices) for tri in mesh.loop_triangles]
    return obj, vertices, triangles, BVHTree.FromPolygons(vertices, triangles, all_triangles=True)

poses = []
for frame in (1, 10, 19, 28, 37, 46, 55, 64, 73):
    scene.frame_set(frame)
    bpy.context.view_layer.update()
    _, _, _, frame_tree = make_tree('Gate frame')
    leaves = []
    for name in ('Left thin leaf', 'Right thin leaf'):
        leaf, vertices, triangles, leaf_tree = make_tree(name)
        overlap = frame_tree.overlap(leaf_tree)
        centres = [sum((vertices[i] for i in triangles[part]), Vector()) / 3 for _, part in overlap]
        hinge = leaf.matrix_world.translation
        # Ignore the shared horizontal cuts near the sill and lintel. Triangle
        # centres are only a rough locator, not penetration depth or clearance.
        middle = [p for p in centres if report['bottom']+.1 < p.z < report['top']-.1]
        radii = [math.hypot(p.x-hinge.x, p.y-hinge.y) for p in middle]
        leaves.append(dict(name=name, rotationDegrees=math.degrees(leaf.rotation_euler.z),
            intersectingTrianglePairs=len(overlap), middlePairs=len(middle),
            maxMiddleTriangleCentreRadius=max(radii, default=0),
            middlePairsBeyondHalfSourceUnit=sum(r > .5 for r in radii)))
    poses.append(dict(frame=frame, leaves=leaves))
if abs(poses[-1]['leaves'][0]['rotationDegrees']-80) > .01 or abs(poses[-1]['leaves'][1]['rotationDegrees']+80) > .01:
    raise RuntimeError('Hidden collection did not evaluate the opening animation')
if hashlib.sha256(source.read_bytes()).hexdigest() != digest: raise RuntimeError('Source changed during inspection')
out.parent.mkdir(parents=True, exist_ok=True)
with out.open('x', encoding='utf-8', newline='\n') as stream:
    json.dump(dict(scope='Blender triangle intersections, not visual approval or penetration depth',
        sourceSha256=digest, blender=bpy.app.version_string, poses=poses), stream, indent=2)
    stream.write('\n')
print('GATE_SWEEP_INSPECTION_SAVED', out, flush=True)
