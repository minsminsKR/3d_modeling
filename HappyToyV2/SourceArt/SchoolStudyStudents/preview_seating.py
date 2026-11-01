"""Inspect a source rig in a preliminary seated pose, without changing originals.

Requires Blender 4.0.2; writes a review blend under Builds only. This is an
authoring probe, not an approved Unity model or a replacement for visual QA.
"""
import argparse
import math
from pathlib import Path
import runpy
import sys

import bpy
from mathutils import Matrix

HERE=Path(__file__).resolve().parent
PROJECT=HERE.parents[1]
parser=argparse.ArgumentParser()
parser.add_argument('--job',required=True)
parser.add_argument('--yaw',type=float,default=0)
parser.add_argument('--hips',required=True,help='comma-separated upper leg bone names')
parser.add_argument('--knees',required=True,help='comma-separated lower leg bone names')
parser.add_argument('--hands',help='two hand bones and their forearms as hand,forearm,hand,forearm')
parser.add_argument('--corrected',action='store_true')
parser.add_argument('--bend',type=float,default=85)
args=parser.parse_args(sys.argv[sys.argv.index('--')+1:])
source=HERE/('rigged-corrected' if args.corrected else 'rigged')/args.job/'rigged.glb'
output=PROJECT/'Builds'/'SchoolStudyStudentsReview'/args.job/'seated-probe.blend'
if not source.is_file():raise FileNotFoundError(source)
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=str(source))
rigs=[o for o in bpy.context.scene.objects if o.type=='ARMATURE']
meshes=[o for o in bpy.context.scene.objects if o.type=='MESH' and o.vertex_groups and o.material_slots]
if len(rigs)!=1 or len(meshes)!=1:raise RuntimeError('Unexpected imported rig or skin')
rig,mesh=rigs[0],meshes[0]
finisher=runpy.run_path(str(PROJECT/'SourceArt'/'SchoolWalker'/'finish_character.py'),run_name='student_normalization')
finisher['normalize'](rig,mesh,args.yaw)
if args.hands:
    lh,lf,rh,rf=args.hands.split(',')
    finisher['repair_relaxed_hands'](mesh,rig,{'LeftHand':lh,'LeftForeArm':lf,'RightHand':rh,'RightForeArm':rf},
        {'radiusMetres':.105,'wristBlendStartMetres':-.025,'wristBlendEndMetres':.020})
for modifier in mesh.modifiers:
    if modifier.type=='ARMATURE':modifier.object=rig;modifier.show_viewport=True;modifier.show_render=True
rig.data.pose_position='POSE'

def rotate_world(name,degrees):
    bone=rig.pose.bones.get(name)
    if bone is None:raise RuntimeError('Missing reviewed joint '+name)
    point=bone.head.copy()
    bone.matrix=Matrix.Translation(point) @ Matrix.Rotation(math.radians(degrees),4,'X') @ Matrix.Translation(-point) @ bone.matrix
    bpy.context.view_layer.update()

for bone in args.hips.split(','):rotate_world(bone,-args.bend)
for bone in args.knees.split(','):rotate_world(bone,args.bend)
output.parent.mkdir(parents=True,exist_ok=True)
bpy.ops.wm.save_as_mainfile(filepath=str(output))
print('SEATED_PROBE_SAVED',output,flush=True)
