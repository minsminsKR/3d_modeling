import bpy,json
from pathlib import Path
p=Path(__file__).resolve().parent
bpy.ops.wm.open_mainfile(filepath=str(p/'hand-rig-prototype-v2.blend'))
h=bpy.data.objects['Blender anatomical hand candidate']; src=bpy.data.objects['hand']
print('SOURCE_MATERIALS',[(m.name if m else None) for m in src.data.materials]);print('SOURCE_UV',list(src.data.uv_layers.keys()),'TARGET_UV',list(h.data.uv_layers.keys()))
bpy.ops.object.select_all(action='DESELECT');h.select_set(True);src.select_set(True);bpy.context.view_layer.objects.active=src
bpy.ops.object.data_transfer(data_type='UV',use_create=True,loop_mapping='POLYINTERP_NEAREST',layers_select_src='ACTIVE',layers_select_dst='ACTIVE',mix_mode='REPLACE')
h.data.materials.clear()
for mat in src.data.materials:h.data.materials.append(mat)
for face in h.data.polygons: face.material_index=0
uv=h.data.uv_layers.active
us=[c.uv.x for c in uv.data];vs=[c.uv.y for c in uv.data]
report={'uvCount':len(uv.data),'uvRange':[min(us),max(us),min(vs),max(vs)],'sourceMaterialNames':[m.name if m else None for m in h.data.materials],'method':'nearest old skin surface UV transfer','visualQualityVerified':False,'knownLimitations':['UV mapping may stretch or cross seams','source texture is generated rather than a photographed scan','full item grips not validated']}
(p/'prototype-v3-report.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
bpy.ops.wm.save_as_mainfile(filepath=str(p/'hand-rig-prototype-v3.blend'))
print('REPORT',json.dumps(report))
