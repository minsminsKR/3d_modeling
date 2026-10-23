"""Opt-in Blender topology probe: independent far collapse from dense source."""
from pathlib import Path
import json
import math
import runpy
import bpy
import bmesh
from mathutils import Matrix, Vector
ROOT=Path(__file__).resolve().parents[2]
helpers=runpy.run_path(str(ROOT/'SourceArt/CorridorEntrance/finish_entrance_assets.py'),run_name='probe_helpers')
source=ROOT/'SourceArt/SchoolRenewal/pipe-manifold'
bpy.ops.wm.read_factory_settings(use_empty=True);bpy.ops.import_scene.gltf(filepath=str(source/'original.glb'))
original=next(obj for obj in bpy.context.scene.objects if obj.type=='MESH')
game=original.copy();game.data=original.data.copy();game.parent=None;bpy.context.scene.collection.objects.link(game)
game.matrix_world=Matrix.Rotation(math.pi/2,4,'Z')@original.matrix_world
low,high=helpers['bounds']([game]);scale=2/(high.x-low.x)
game.matrix_world=Matrix.Scale(scale,4)@Matrix.Translation(-Vector(((low.x+high.x)/2,(low.y+high.y)/2,low.z)))@game.matrix_world
bpy.ops.object.select_all(action='DESELECT');game.select_set(True);bpy.context.view_layer.objects.active=game
bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
bm=bmesh.new();bm.from_mesh(game.data)
bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=.00002)
bmesh.ops.dissolve_degenerate(bm,edges=list(bm.edges),dist=.000001)
bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(game.data);bm.free();game.data.update()
try:
    helpers['simplify'](game,2000);out={'ok':True,'triangles':helpers['triangles'](game)}
except Exception as error:
    out={'ok':False,'triangles':helpers['triangles'](game),'error':str(error)}
(source/'independent-far-probe.json').write_text(json.dumps(out,indent=2)+'\n',encoding='utf-8')
print('INDEPENDENT_FAR_PROBE',json.dumps(out),flush=True)
