"""Read-only MCP snapshot of current static game LODs; no model save or edits.

Execute with __file__, optional MODEL_QUERY (default 'LOD') and MODEL_REPORT.
Use a disposable Blender editor with the intended authoring source already open.
"""
from pathlib import Path
import json
import bpy
import bmesh
import numpy as np
from mathutils import Vector

query=globals().get('MODEL_QUERY','LOD')
report=Path(globals().get('MODEL_REPORT',Path(__file__).resolve().parents[2]/'Builds/LocalMcp/static-blender-review.json'))
objects=[obj for obj in bpy.context.scene.objects if obj.type=='MESH' and query in obj.name]
if not objects:raise ValueError('No actual model objects match '+query)
meshes=[]
for obj in objects:
    mesh=obj.data;mesh.calc_loop_triangles()
    if not mesh.uv_layers.active:raise ValueError('Missing UV0: '+obj.name)
    uv=np.empty(len(mesh.loops)*2,dtype=np.float32);mesh.uv_layers.active.data.foreach_get('uv',uv)
    uv=uv.reshape(-1,2)
    if not np.isfinite(uv).all() or min(np.ptp(uv,axis=0))<.1:raise ValueError('Nonfinite/collapsed UV0: '+obj.name)
    topology=bmesh.new();topology.from_mesh(mesh)
    try:
        boundary=sum(edge.is_boundary for edge in topology.edges)
        nonmanifold=sum(len(edge.link_faces)>2 for edge in topology.edges)
        loose=sum(not vertex.link_faces for vertex in topology.verts)
    finally:topology.free()
    corners=[obj.matrix_world@Vector(corner) for corner in obj.bound_box]
    meshes.append(dict(name=obj.name,vertices=len(mesh.vertices),triangles=len(mesh.loop_triangles),
                       uvMin=uv.min(axis=0).tolist(),uvMax=uv.max(axis=0).tolist(),boundaryEdges=boundary,
                       complexEdges=nonmanifold,looseVertices=loose,
                       boundsMin=[min(p[i] for p in corners) for i in range(3)],boundsMax=[max(p[i] for p in corners) for i in range(3)],
                       materials=[slot.material.name if slot.material else None for slot in obj.material_slots]))
value=dict(blender=bpy.app.version_string,source=bpy.data.filepath,query=query,meshes=meshes,
           packedImages=[dict(name=image.name,size=list(image.size)) for image in bpy.data.images if image.packed_file],
           scope='Actual live mesh/UV/topology/bounds and packed-image inventory; visual appearance and native performance require separate checks.')
report.parent.mkdir(parents=True,exist_ok=True);report.write_text(json.dumps(value,indent=2)+'\n',encoding='utf-8')
print(json.dumps(value,indent=2))
