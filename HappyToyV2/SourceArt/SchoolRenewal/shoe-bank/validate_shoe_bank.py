"""Validate actual exported shoe-cubby LOD openings, back and board depth."""
from pathlib import Path
import json
import bpy,bmesh
from mathutils import Vector
from mathutils.bvhtree import BVHTree
source=Path(__file__).resolve().parent;project=source.parents[2]
destination=project/'Assets/Resources/SchoolRenewal/shoe-bank'
report=json.loads((destination/'asset.json').read_text(encoding='utf-8'))
results=[]
for index,lod in enumerate(report['lods']):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(destination/lod['file']))
    obj=next(o for o in bpy.context.scene.objects if o.type=='MESH')
    bm=bmesh.new();bm.from_mesh(obj.data);bm.transform(obj.matrix_world)
    tree=BVHTree.FromBMesh(bm);openings=[]
    for column,x in enumerate((-.575,-.187,.187,.575)):
        for row,z in enumerate((.145,.40,.65,.91)):
            origin=Vector((x,.7,z))
            if tree.ray_cast(origin,Vector((0,-1,0)),.7)[0] is not None:
                raise RuntimeError('Actual exported cubby is blocked '+str((index,column,row)))
            back=tree.ray_cast(origin,Vector((0,-1,0)),1.1)[0]
            if back is None or not -.25<back.y<-.01:
                raise RuntimeError('Actual exported cubby lost back '+str((index,column,row)))
            openings.append(dict(column=column,row=row,x=x,height=z,backY=back.y))
    origin=Vector((.187,0,1.2));crossings=[]
    for _ in range(60):
        hit=tree.ray_cast(origin,Vector((0,0,-1)),2)[0]
        if hit is None:break
        crossings.append(float(hit.z));origin=hit-Vector((0,0,.0008))
    groups=[]
    for z in crossings:
        if groups and abs(groups[-1][-1]-z)<.004:groups[-1].append(z)
        else:groups.append([z])
    surfaces=[sum(group)/len(group) for group in groups]
    if len(surfaces)!=10:raise RuntimeError('Actual cubby export lost one of five horizontal boards '+str((index,surfaces)))
    thickness=[surfaces[i]-surfaces[i+1] for i in range(0,10,2)]
    if min(thickness)<.015:raise RuntimeError('Actual cubby board below15mm '+str((index,thickness)))
    results.append(dict(lod=index,triangles=sum(len(f.verts)-2 for f in bm.faces),openings=openings,
                        midDepthBoardSurfaceHeights=surfaces,boardThicknessMetres=thickness))
    bm.free()
(source/'validation.json').write_text(json.dumps(dict(actualExports=results),indent=2)+'\n',encoding='utf-8')
print('SCHOOL_EXPORTED_SHOE_BANK_OPENINGS_AND_BOARDS_PASS',json.dumps(results),flush=True)
