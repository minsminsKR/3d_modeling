"""Blender 4.0.2 read-only report of the retained generated debris surface."""
from pathlib import Path
import bpy,bmesh,json
from mathutils import Vector
base=Path(__file__).resolve().parent
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=str(base/'door-debris-original.glb'))
report=[]
for obj in [o for o in bpy.context.scene.objects if o.type=='MESH']:
    points=[obj.matrix_world@Vector(c) for c in obj.bound_box]
    bm=bmesh.new();bm.from_mesh(obj.data)
    bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=.00001)
    visited=set();components=[]
    for vertex in bm.verts:
        if vertex in visited:continue
        queue=[vertex];visited.add(vertex);group=[]
        while queue:
            v=queue.pop();group.append(v)
            for edge in v.link_edges:
                other=edge.other_vert(v)
                if other not in visited:visited.add(other);queue.append(other)
        if len(group)>20:
            low=[min(v.co[i] for v in group) for i in range(3)];high=[max(v.co[i] for v in group) for i in range(3)]
            components.append(dict(vertices=len(group),low=low,high=high))
    report.append(dict(name=obj.name,faces=len(obj.data.polygons),vertices=len(obj.data.vertices),
        low=[min(p[i] for p in points) for i in range(3)],high=[max(p[i] for p in points) for i in range(3)],
        components=sorted(components,key=lambda c:-c['vertices'])[:35],materials=[m.name for m in obj.data.materials]))
    bm.free()
result=dict(meshes=report,images=[dict(name=i.name,size=list(i.size),colorspace=i.colorspace_settings.name) for i in bpy.data.images])
(base/'source-inspection.json').write_text(json.dumps(result,indent=2)+'\n',encoding='utf-8')
print(json.dumps(result,indent=2),flush=True)
