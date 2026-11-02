"""Create a source-derived reduced seated mesh for visual review in Builds.

Uses the existing seated probe and keeps its AI rig untouched. This is not a
production export: inspect both surface and head deformation before promotion.
"""
import argparse
from collections import defaultdict
from pathlib import Path
import runpy
import sys
import bpy
from mathutils import Matrix,Vector

HERE=Path(__file__).resolve().parent
PROJECT=HERE.parents[1]
parser=argparse.ArgumentParser();parser.add_argument('--job',required=True);parser.add_argument('--budget',type=int,default=40000)
parser.add_argument('--repair',action='store_true',help='derive a closed surface and rebake original PBR for visual review')
parser.add_argument('--repair-legs',action='store_true',help='correct source-derived knee and leg deformation before rebaking')
parser.add_argument('--hips',help='two source upper leg bones')
parser.add_argument('--knees',help='two source lower leg bones')
parser.add_argument('--hip-angle',type=float,default=85)
parser.add_argument('--lower-angle',type=float,help='lower-leg correction angle; defaults to the hip angle')
args=parser.parse_args(sys.argv[sys.argv.index('--')+1:])
if not 1000<=args.budget<=50000:raise ValueError('Invalid review triangle budget')
rigs=[o for o in bpy.context.scene.objects if o.type=='ARMATURE']
meshes=[o for o in bpy.context.scene.objects if o.type=='MESH' and o.vertex_groups and o.material_slots]
if len(rigs)!=1 or len(meshes)!=1:raise RuntimeError('Expected exactly one imported AI rig and skin')
source,mesh=rigs[0],meshes[0]
head=source.pose.bones.get('bone_5');hips=source.pose.bones.get('bone_0')
if not head or not hips:raise RuntimeError('Reviewed head and hips must exist')
bpy.context.view_layer.update()
deps=bpy.context.evaluated_depsgraph_get();evaluated=mesh.evaluated_get(deps)
data=bpy.data.meshes.new_from_object(evaluated,preserve_all_data_layers=True,depsgraph=deps)
game=bpy.data.objects.new('Student seated game review',data);bpy.context.scene.collection.objects.link(game)
game.matrix_world=mesh.matrix_world.copy()
for group in mesh.vertex_groups:game.vertex_groups.new(name=group.name)
if args.repair_legs:
    if not args.hips or not args.knees:raise ValueError('Leg repair requires source hips and knees')
    repair=runpy.run_path(str(HERE/'repair_seated_legs.py'),run_name='student_leg_repair')['repair']
    lower_angle=args.hip_angle if args.lower_angle is None else args.lower_angle
    print('SOURCE_LEG_REPAIR',args.job,repair(source,mesh,game,args.hips.split(','),args.knees.split(','),args.hip_angle,lower_angle),flush=True)
out=PROJECT/'Builds'/'SchoolStudyStudentsReview'/args.job/'export';out.mkdir(parents=True,exist_ok=True)
high=None
if args.repair:
    high=game.copy();high.data=game.data.copy();high.name='Posed high source for PBR transfer';bpy.context.scene.collection.objects.link(high)
    bpy.ops.object.select_all(action='DESELECT');game.select_set(True);bpy.context.view_layer.objects.active=game
    game.data.remesh_voxel_size=.003;game.data.remesh_voxel_adaptivity=0
    bpy.ops.object.voxel_remesh()
data.calc_loop_triangles();original=len(data.loop_triangles)
game.data.calc_loop_triangles();original=len(game.data.loop_triangles)
dec=game.modifiers.new('Review collapse preserving UV and source skin','DECIMATE');dec.ratio=min(1,args.budget/max(1,original))
bpy.ops.object.select_all(action='DESELECT');game.select_set(True);bpy.context.view_layer.objects.active=game
bpy.ops.object.modifier_apply(modifier=dec.name);game.data.calc_loop_triangles()
if len(game.data.loop_triangles)>args.budget*1.05:raise RuntimeError('Review geometry exceeds triangle budget')
head_point=source.matrix_world @ head.head;hips_point=source.matrix_world @ hips.head
drop=hips_point.z-.48
game.location.z-=drop;bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
head_point.z-=drop
if high:
    high.location.z-=drop
    bpy.context.view_layer.update()
    bake=runpy.run_path(str(PROJECT/'SourceArt/SchoolLoop/rebake_music_lod.py'),run_name='student_pbr_transfer')['bake']
    save_pixels=runpy.run_path(str(PROJECT/'SourceArt/CorridorEntrance/finish_entrance_assets.py'),run_name='student_image_transfer')['save_pixels']
    bake(game,high,Matrix.Identity(4),out,'student-review',2048,save_pixels)
    tools=runpy.run_path(str(PROJECT/'SourceArt/SchoolRenewal/finish_asset.py'),run_name='student_normal_transfer')
    tools['normal_atlas'](game,high,Matrix.Identity(4),out,'student-review',2048)
head_group=game.vertex_groups.get('bone_5');head_index=head_group.index if head_group else -1
buckets=defaultdict(list)
for vertex in game.data.vertices:
    original_weight=max((g.weight for g in vertex.groups if g.group==head_index),default=0)
    geometry=max(0,min(1,(vertex.co.z-(head_point.z-.13))/.10))
    weight=max(original_weight,geometry)
    buckets[round(weight*255)].append(vertex.index)
game.vertex_groups.clear();root_group=game.vertex_groups.new(name='Root');head_group=game.vertex_groups.new(name='HeadPivot')
for amount,indices in buckets.items():
    if amount:head_group.add(indices,amount/255,'REPLACE')
    if amount<255:root_group.add(indices,1-amount/255,'REPLACE')
armature=bpy.data.armatures.new('Seated student head rig');newrig=bpy.data.objects.new('Seated student head rig',armature)
bpy.context.scene.collection.objects.link(newrig);bpy.ops.object.select_all(action='DESELECT');newrig.select_set(True);bpy.context.view_layer.objects.active=newrig
bpy.ops.object.mode_set(mode='EDIT');root=armature.edit_bones.new('Root');root.head=(0,0,0);root.tail=(0,0,.3)
newhead=armature.edit_bones.new('HeadPivot');newhead.head=head_point;newhead.tail=head_point+Vector((0,0,.15));newhead.parent=root
bpy.ops.object.mode_set(mode='OBJECT');game.parent=newrig
modifier=game.modifiers.new('Head rotation rig','ARMATURE');modifier.object=newrig
source.hide_render=True;source.hide_set(True);mesh.hide_render=True;mesh.hide_set(True)
if high:high.hide_render=True;high.hide_set(True)
bpy.ops.object.select_all(action='DESELECT');newrig.select_set(True);game.select_set(True);bpy.context.view_layer.objects.active=newrig
bpy.ops.export_scene.fbx(filepath=str(out/'student-review.fbx'),use_selection=True,object_types={'ARMATURE','MESH'},
    add_leaf_bones=False,bake_anim=False,axis_forward='-Z',axis_up='Y',path_mode='COPY',embed_textures=True)
bpy.ops.file.pack_all()
bpy.ops.wm.save_as_mainfile(filepath=str(out/'student-review.blend'),compress=True)
print('SEATED_REVIEW_EXPORTED',args.job,'triangles',len(game.data.loop_triangles),'head',tuple(head_point),'drop',drop,flush=True)
