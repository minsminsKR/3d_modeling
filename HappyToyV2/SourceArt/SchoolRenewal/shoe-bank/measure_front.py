"""Blender 4.0.2: measure actual generated open-cubby facing without editing."""
from pathlib import Path
import json
import bpy
import bmesh
from mathutils import Vector
from mathutils.bvhtree import BVHTree

source = Path(__file__).resolve().parent
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=str(source/'original.glb'))
obj = next(o for o in bpy.context.scene.objects if o.type=='MESH')
bm=bmesh.new();bm.from_mesh(obj.data);bm.transform(obj.matrix_world)
tree=BVHTree.FromBMesh(bm)
points=[]
for x in (-.37,-.12,.12,.37):
    for z in (-.245,-.08,.08,.245):
        a=tree.ray_cast(Vector((x,1,z)),Vector((0,-1,0)),2)[0]
        b=tree.ray_cast(Vector((x,-1,z)),Vector((0,1,0)),2)[0]
        points.append(dict(x=x,z=z,fromPositiveY=list(a) if a else None,fromNegativeY=list(b) if b else None))
positions=[p[k][1] for p in points for k in ('fromPositiveY','fromNegativeY') if p[k]]
mean=sum(positions)/len(positions)
report=dict(cubbyCentreRays=points,meanBackPanelY=mean,openFront='+Y' if mean<0 else '-Y',
            rotationZDegrees=0 if mean<0 else 180)
(source/'front-measurements.json').write_text(json.dumps(report,indent=2)+'\n',encoding='utf-8')
print('SHOE_OPEN_FRONT_MEASUREMENTS',json.dumps(report),flush=True)
bm.free()
