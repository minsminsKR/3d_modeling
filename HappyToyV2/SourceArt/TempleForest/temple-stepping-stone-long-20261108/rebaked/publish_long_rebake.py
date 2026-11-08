from pathlib import Path
import json, sys, hashlib, shutil, subprocess
from PIL import Image

raw=Path(sys.argv[1]).resolve(); root=Path(sys.argv[2]).resolve(); key=root.name; dest=root/'rebaked'
if dest.exists(): raise RuntimeError('rebaked directory already exists; preserve existing assets')
def sha(p):
 h=hashlib.sha256()
 with p.open('rb') as f:
  for block in iter(lambda:f.read(1048576),b''): h.update(block)
 return h.hexdigest()
trial=json.loads((raw/'rebake-report.json').read_text(encoding='utf-8'))
assert key==trial['source'] and sha(root/'original.glb')==trial['originalSha256']
assert sha(root/(key+'.blend'))==trial['sourceBlendSha256']
validator=Path(__file__).with_name('validate_long_coverage.py')
coverage=json.loads(subprocess.check_output([sys.executable,str(validator),str(raw)],text=True))
fbxproof=json.loads((raw/'fbx-reimport-proof.json').read_text(encoding='utf-8'))
assert len(coverage)==len(fbxproof)==len(trial['lods'])==3 and all(x['missedPixels']==0 for x in coverage)
report={'key':key,'originalSha256':trial['originalSha256'],'editableBlend':trial['blend']['file'],'editableSha256':trial['blend']['sha256'],'sourceBlendSha256':trial['sourceBlendSha256'],'quality':'Blender verified; Unity gameplay verification pending','bake':trial['bake'],'packing':'metallic in R, smoothness in A, G/B zero','coverageMethod':'white emission source to target; UV island raster eroded two pixels; threshold 128/255','lods':[]}
for i,entry in enumerate(trial['lods']):
 assert i==entry['lod']==fbxproof[i]['lod']==coverage[i]['lod'] and entry['triangles']==fbxproof[i]['triangles']
 record={'lod':i,'triangles':entry['triangles'],'fbx':entry['fbx'],'textures':{},'coverage':coverage[i],'fbxReimport':fbxproof[i]}
 assert sha(raw/entry['fbx']['file'])==entry['fbx']['sha256']
 for channel in ('baseColor','metallicSmoothness'):
  info=entry['textures'][channel].copy(); path=raw/info['file']; assert sha(path)==info['sha256']
  with Image.open(path) as img: assert img.size==(2048,2048); info.update(width=img.width,height=img.height)
  record['textures'][channel]=info
 report['lods'].append(record)
assert sha(raw/trial['blend']['file'])==trial['blend']['sha256']
dest.mkdir()
for entry in report['lods']:
 for f in [entry['fbx'],*entry['textures'].values()]: shutil.copy2(raw/f['file'],dest/f['file'])
shutil.copy2(raw/report['editableBlend'],dest/report['editableBlend'])
for name in ('rebake_stone_trial.py','validate_long_coverage.py','verify_long_rebaked_fbx.py','preview_long_rebaked.py','publish_long_rebake.py'):
 shutil.copy2(Path(__file__).with_name(name),dest/name)
(dest/'rebake-report.json').write_text(json.dumps(report,indent=2)+'\n',encoding='utf-8')
print('REBaked source assets copied:',dest)
