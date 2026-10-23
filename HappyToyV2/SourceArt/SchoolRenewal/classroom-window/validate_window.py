"""Validate every actual exported game FBX has eight genuinely clear panes."""
from pathlib import Path
import json
import bpy,bmesh
from mathutils import Vector
from mathutils.bvhtree import BVHTree
source=Path(__file__).resolve().parent;project=source.parents[2]
destination=project/'Assets/Resources/SchoolRenewal/classroom-window'
report=json.loads((destination/'asset.json').read_text(encoding='utf-8'))
panes=json.loads((source/'pane-cutouts.json').read_text(encoding='utf-8'))['panes']
results=[]
for index,lod in enumerate(report['lods']):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(destination/lod['file']))
    objects=[o for o in bpy.context.scene.objects if o.type=='MESH']
    bm=bmesh.new()
    for obj in objects:
        before=set(bm.verts)
        bm.from_mesh(obj.data)
        for v in bm.verts:
            if v not in before:v.co=obj.matrix_world@v.co
    tree=BVHTree.FromBMesh(bm)
    checked=0
    for pane in panes:
        for u in (.2,.5,.8):
            for v in (.2,.5,.8):
                x=pane['min'][0]*(1-u)+pane['max'][0]*u
                z=pane['min'][1]*(1-v)+pane['max'][1]*v
                hit=tree.ray_cast(Vector((x,3,z)),Vector((0,-1,0)),6)[0]
                if hit is not None:raise RuntimeError('Exported pane blocked: '+str((index,pane['name'],u,v,list(hit))))
                checked+=1
    triangles=sum(len(f.verts)-2 for f in bm.faces)
    if triangles>lod['targetTriangles']+4:raise RuntimeError('Actual FBX over game budget')
    results.append(dict(lod=index,triangles=triangles,realPaneClearanceRays=checked))
    bm.free()
(source/'validation.json').write_text(json.dumps(dict(actualExports=results),indent=2)+'\n',encoding='utf-8')
print('SCHOOL_EXPORTED_WINDOW_PANES_PASS',json.dumps(results),flush=True)
