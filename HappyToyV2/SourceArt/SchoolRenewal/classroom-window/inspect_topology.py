"""Non-exporting diagnostic for generated frame LOD boundary limitations."""
from pathlib import Path
import json,math,runpy
import bpy,bmesh
from mathutils import Matrix,Vector
source=Path(__file__).resolve().parent;project=source.parents[2]
helper=runpy.run_path(str(project/'SourceArt/CorridorEntrance/finish_entrance_assets.py'),run_name='window_topology')
bpy.ops.wm.read_factory_settings(use_empty=True);bpy.ops.import_scene.gltf(filepath=str(source/'original.glb'))
obj=next(o for o in bpy.context.scene.objects if o.type=='MESH')
obj.matrix_world=Matrix.Rotation(math.pi/2,4,'Z')@obj.matrix_world
lo,hi=helper['bounds']([obj]);obj.matrix_world=Matrix.Scale(1.6/(hi.z-lo.z),4)@Matrix.Translation(-Vector(((lo.x+hi.x)*.5,(lo.y+hi.y)*.5,lo.z)))@obj.matrix_world
bpy.ops.object.select_all(action='DESELECT');obj.select_set(True);bpy.context.view_layer.objects.active=obj;bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
result=runpy.run_path(str(source/'finish_geometry.py'))['preprocess'](obj)
try:helper['simplify'](obj,8000)
except RuntimeError as error:print(str(error),flush=True)
bm=bmesh.new();bm.from_mesh(obj.data)
boundary={e for e in bm.edges if e.is_boundary};groups=[]
while boundary:
    first=boundary.pop();stack=[first];edges=[first];verts=set(first.verts)
    while stack:
        current=stack.pop()
        for vertex in current.verts:
            for edge in vertex.link_edges:
                if edge in boundary:
                    boundary.remove(edge);stack.append(edge);edges.append(edge);verts.update(edge.verts)
    lo=[min(v.co[i] for v in verts) for i in range(3)];hi=[max(v.co[i] for v in verts) for i in range(3)]
    groups.append(dict(edges=len(edges),vertices=len(verts),min=lo,max=hi,extent=max(b-a for a,b in zip(lo,hi))))
report=dict(preprocess=result,triangles=helper['triangles'](obj),verts=len(bm.verts),faces=len(bm.faces),boundaryEdges=sum(g['edges'] for g in groups),boundaryGroups=len(groups),groups=sorted(groups,key=lambda g:g['edges'],reverse=True))
output=project/'Builds/ModelingDownloads/SchoolRenewal/classroom-window/topology.json';output.write_text(json.dumps(report,indent=2)+'\n',encoding='utf-8')
print('WINDOW_TOPOLOGY',json.dumps({k:v for k,v in report.items() if k!='groups'}),flush=True)
bm.free()
