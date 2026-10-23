"""Validate closed leaf, recessed pane and protruding generated handle in FBX."""
from pathlib import Path
import json
import bpy,bmesh
from mathutils import Vector
from mathutils.bvhtree import BVHTree
source=Path(__file__).resolve().parent;project=source.parents[2]
destination=project/'Assets/Resources/SchoolRenewal/classroom-door'
report=json.loads((destination/'asset.json').read_text(encoding='utf-8'));results=[]
for index,lod in enumerate(report['lods']):
    bpy.ops.wm.read_factory_settings(use_empty=True);bpy.ops.import_scene.fbx(filepath=str(destination/lod['file']))
    obj=next(o for o in bpy.context.scene.objects if o.type=='MESH')
    bm=bmesh.new();bm.from_mesh(obj.data);bm.transform(obj.matrix_world);tree=BVHTree.FromBMesh(bm)
    def hit(x,z):return tree.ray_cast(Vector((x,.5,z)),Vector((0,-1,0)),1)[0]
    panels=[]
    for z in (.4,1.2,2.25):
        point=hit(0,z)
        if point is None:raise RuntimeError('Actual closed leaf missing '+str((index,z)))
        panels.append(point.y)
    glass=hit(-.25,1.75)
    if glass is None or min(panels)-glass.y<.012:raise RuntimeError('Actual recessed wired-glass pane flattened '+str(index))
    handles=[]
    for u in range(11):
        for v in range(11):
            point=hit(-.47+u*.025,1.0+v*.025)
            if point is not None:handles.append(point.y)
    if not handles or max(handles)<max(panels)+.06:raise RuntimeError('Actual generated handle lost protruding shape '+str(index))
    triangles=sum(len(f.verts)-2 for f in bm.faces)
    if triangles>lod['targetTriangles']+4:raise RuntimeError('Actual door FBX exceeds target budget')
    results.append(dict(lod=index,triangles=triangles,closedPanelFrontY=panels,
                        wiredPaneY=glass.y,recessMetres=min(panels)-glass.y,handleFrontY=max(handles),
                        handleProtrusionMetres=max(handles)-max(panels)))
    bm.free()
(source/'validation.json').write_text(json.dumps(dict(actualExports=results),indent=2)+'\n',encoding='utf-8')
print('SCHOOL_EXPORTED_DOOR_LEAF_PANE_HANDLE_PASS',json.dumps(results),flush=True)
