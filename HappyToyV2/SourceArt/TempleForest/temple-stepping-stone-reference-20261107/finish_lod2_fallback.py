"""Preserve the faulty LOD2 candidate and prepare a safe LOD1-based LOD2.

Blender 4.0.2: blender -b -t 2 --factory-startup --python-exit-code 1
--python <this file>.  Writes only rebaked-safe; the original and the previous
rebake remain untouched. The safe fallback retains LOD1's 1,999 triangles.
"""
from pathlib import Path
import copy, hashlib, json, shutil
import bpy

HERE=Path(__file__).resolve().parent
KEY=HERE.name
SOURCE=(HERE/'rebaked-rejected-lod2') if (HERE/'rebaked-rejected-lod2').is_dir() else (HERE/'rebaked')
OUT=HERE/'rebaked-safe'
if bpy.app.version[:3] != (4,0,2) or OUT.exists(): raise RuntimeError('Wrong Blender version or output exists')
def sha(path):
 h=hashlib.sha256()
 with path.open('rb') as f:
  for chunk in iter(lambda:f.read(1024*1024),b''):h.update(chunk)
 return h.hexdigest()
report=json.loads((SOURCE/'rebake-report.json').read_text(encoding='utf-8'))
if report['lods'][2].get('reusesLod1GeometryAndMaps'):
 raise RuntimeError('Source must be the preserved pre-fallback candidate')
original_sha=sha(HERE/'original.glb')
if original_sha != report['originalSha256']:raise RuntimeError('Original hash mismatch')
for item in report['lods'][:2]:
 for descriptor in [item['fbx'],*item['textures'].values()]:
  if sha(SOURCE/descriptor['file'])!=descriptor['sha256']:raise RuntimeError('LOD input hash mismatch')
if sha(SOURCE/report['editableBlend'])!=report['editableSha256']:raise RuntimeError('Editable source hash mismatch')
OUT.mkdir()
for item in report['lods'][:2]:
 for descriptor in [item['fbx'],*item['textures'].values()]:shutil.copy2(SOURCE/descriptor['file'],OUT/descriptor['file'])
item=copy.deepcopy(report['lods'][1]);item['lod']=2
for descriptor in [item['fbx'],*item['textures'].values()]:
 old=descriptor['file']; new=old.replace('-lod1.', '-lod2.').replace('_lod1-', '_lod2-')
 if new==old:raise RuntimeError('Unexpected LOD1 filename '+old)
 shutil.copy2(SOURCE/old,OUT/new);descriptor['file']=new
 if sha(OUT/new)!=descriptor['sha256']:raise RuntimeError('LOD2 copy changed bytes')
item['reusesLod1GeometryAndMaps']=True
item['fallbackReason']='The original 599-triangle LOD2 exposes its underside at a visible folded edge; exact LOD1 reuse avoids this defect.'
report['lods'][2]=item
bpy.ops.wm.open_mainfile(filepath=str(SOURCE/report['editableBlend']),load_ui=False)
lod1=bpy.data.objects['ROUND_REBAKED_LOD1'];lod2=bpy.data.objects['ROUND_REBAKED_LOD2']
lod2.data=lod1.data.copy();lod2.matrix_world=lod1.matrix_world.copy()
text=bpy.data.texts.new('SOURCE - finish_lod2_fallback.py');text.write(Path(__file__).read_text(encoding='utf-8'))
bpy.ops.file.pack_all();blend=OUT/report['editableBlend'];bpy.ops.wm.save_as_mainfile(filepath=str(blend),compress=True)
report['editableSha256']=sha(blend)
report['fallbackRecipe']=Path(__file__).name
report['notes']='LOD2 intentionally reuses LOD1 geometry and maps byte for byte to avoid a visible folded-edge defect. Unity may use lower texture import resolution at distance; verify LOD transitions and scene performance.'
report['hashes']={p.name:sha(p) for p in OUT.iterdir() if p.is_file()}
if sha(HERE/'original.glb')!=original_sha:raise RuntimeError('Original changed')
(OUT/'rebake-report.json').write_text(json.dumps(report,indent=2)+'\n',encoding='utf-8')
print('LOD2_FALLBACK_COMPLETE',json.dumps(item),flush=True)
