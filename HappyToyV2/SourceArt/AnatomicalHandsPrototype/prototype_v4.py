import bpy,json
from pathlib import Path
p=Path(__file__).resolve().parent
bpy.ops.wm.open_mainfile(filepath=str(p/'hand-rig-prototype-v3.blend'))
h=bpy.data.objects['Blender anatomical hand candidate'];bpy.ops.object.select_all(action='DESELECT');h.select_set(True);bpy.context.view_layer.objects.active=h
# Preserve the transferred UV for comparison; create a nonoverlapping draft layout.
h.data.uv_layers.active.name='TransferredOldUV'
h.data.uv_layers.new(name='NewSkinUV');h.data.uv_layers.active=h.data.uv_layers['NewSkinUV']
bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT');bpy.ops.uv.smart_project(island_margin=.008);bpy.ops.object.mode_set(mode='OBJECT')
uv=h.data.uv_layers.active.data
areas=[]
for f in h.data.polygons:
 c=[uv[i].uv for i in f.loop_indices];areas.append(abs(sum(c[i].x*c[(i+1)%len(c)].y-c[(i+1)%len(c)].x*c[i].y for i in range(len(c))))/2)
report={'faceCount':len(areas),'zeroUvFaces':sum(x<1e-10 for x in areas),'uvLayout':'NewSkinUV','status':'candidate UV layout only: new skin texture not baked; no visual approval'}
(p/'prototype-v4-report.json').write_text(json.dumps(report,indent=2),encoding='utf8')
bpy.ops.wm.save_as_mainfile(filepath=str(p/'hand-rig-prototype-v4.blend'))
print('REPORT',json.dumps(report))
