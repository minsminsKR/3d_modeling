"""Measure connected source-derived FBX components for bounded LOD cleanup."""
from pathlib import Path
import argparse
import json
import sys
import bpy
import bmesh

ROOT=Path(__file__).resolve().parents[2]
parser=argparse.ArgumentParser();parser.add_argument('key');args=parser.parse_args(sys.argv[sys.argv.index('--')+1:])
path=ROOT/'Assets/Resources/SchoolRenewal'/args.key/(args.key+'-lod0.fbx')
bpy.ops.wm.read_factory_settings(use_empty=True);bpy.ops.import_scene.fbx(filepath=str(path))
obj=next(obj for obj in bpy.context.scene.objects if obj.type=='MESH')
bm=bmesh.new();bm.from_mesh(obj.data);bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=.00002);bm.verts.index_update()
face_keys={};duplicates=0
for face in bm.faces:
    key=tuple(sorted(v.index for v in face.verts))
    if key in face_keys:duplicates+=1
    face_keys[key]=True
topology={'nonManifoldEdges':sum(len(e.link_faces)>2 for e in bm.edges),
          'boundaryEdges':sum(len(e.link_faces)==1 for e in bm.edges),'duplicateFaces':duplicates}
pending=set(bm.verts);parts=[]
while pending:
    first=pending.pop();stack=[first];verts=[first];faces=set()
    while stack:
        v=stack.pop();faces.update(v.link_faces)
        for edge in v.link_edges:
            other=edge.other_vert(v)
            if other in pending:pending.remove(other);stack.append(other);verts.append(other)
    low=[min(v.co[a] for v in verts) for a in range(3)];high=[max(v.co[a] for v in verts) for a in range(3)]
    size=[b-a for a,b in zip(low,high)]
    parts.append({'vertices':len(verts),'triangles':sum(len(f.verts)-2 for f in faces),'size':size,'maxExtent':max(size)})
parts.sort(key=lambda part:part['triangles'],reverse=True)
report={'key':args.key,'components':len(parts),'topology':topology,'totalTriangles':sum(p['triangles'] for p in parts),
        'microComponents':{str(mm):{'count':sum(p['maxExtent']<mm/1000 for p in parts),'triangles':sum(p['triangles'] for p in parts if p['maxExtent']<mm/1000)} for mm in (1,2,5,10,20)},
        'largest':parts[:20]}
(ROOT/'SourceArt/SchoolRenewal'/args.key/'component-inspection.json').write_text(json.dumps(report,indent=2)+'\n',encoding='utf-8');bm.free();print('COMPONENT_REPORT',json.dumps(report),flush=True)
