"""Blender opt-in raw-topology collapse probe; never overwrites source/game data."""
from pathlib import Path
import argparse
import json
import math
import runpy
import sys
import bpy
import bmesh
from mathutils import Matrix, Vector
ROOT=Path(__file__).resolve().parents[2]
parser=argparse.ArgumentParser();parser.add_argument('--key',required=True);parser.add_argument('--axis',default='z');parser.add_argument('--metres',type=float,required=True);parser.add_argument('--yaw',type=float,default=0);parser.add_argument('--budget',type=int,required=True)
args=parser.parse_args(sys.argv[sys.argv.index('--')+1:]);source=ROOT/'SourceArt/SchoolRenewal'/args.key
helpers=runpy.run_path(str(ROOT/'SourceArt/CorridorEntrance/finish_entrance_assets.py'),run_name='probe_helpers')
bpy.ops.wm.read_factory_settings(use_empty=True);bpy.ops.import_scene.gltf(filepath=str(source/'original.glb'))
original=next(obj for obj in bpy.context.scene.objects if obj.type=='MESH');game=original.copy();game.data=original.data.copy();game.parent=None;bpy.context.scene.collection.objects.link(game)
game.matrix_world=Matrix.Rotation(math.radians(args.yaw),4,'Z')@original.matrix_world;low,high=helpers['bounds']([game]);axis='xyz'.index(args.axis);scale=args.metres/(high[axis]-low[axis])
game.matrix_world=Matrix.Scale(scale,4)@Matrix.Translation(-Vector(((low.x+high.x)/2,(low.y+high.y)/2,low.z)))@game.matrix_world
bpy.ops.object.select_all(action='DESELECT');game.select_set(True);bpy.context.view_layer.objects.active=game;bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
bm=bmesh.new();bm.from_mesh(game.data)
topology={'nonManifoldEdges':sum(len(edge.link_faces)>2 for edge in bm.edges),'boundaryEdges':sum(len(edge.link_faces)==1 for edge in bm.edges)};bm.free()
try:
    helpers['simplify'](game,args.budget);out={'ok':True,'triangles':helpers['triangles'](game)}
except Exception as error:out={'ok':False,'triangles':helpers['triangles'](game),'error':str(error)}
out.update({'key':args.key,'settings':vars(args),'rawTopology':topology,'weld':False,'purpose':'Compare raw dense topology to cleaned-source collapse floor without changing geometry'})
(source/'raw-collapse-probe.json').write_text(json.dumps(out,indent=2)+'\n',encoding='utf-8');print('RAW_COLLAPSE_PROBE',json.dumps(out),flush=True)
