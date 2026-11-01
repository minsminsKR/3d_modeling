"""Extract the active edited rig and its source from a verified CLI archive."""
import hashlib
import json
from pathlib import Path
import zipfile

HERE=Path(__file__).resolve().parent
JOB='10c8d883-8918-44d3-bcd5-7cf85bffff26'
archive=HERE.parents[1]/'Builds'/'ModelingCache'/'SchoolStudyStudents'/'corrected-archive'/(JOB+'.zip')
receipt=json.loads((archive.parent/(JOB+'.download.json')).read_text(encoding='utf-8'))
def digest(path):
    h=hashlib.sha256()
    with path.open('rb') as f:
        for chunk in iter(lambda:f.read(1024*1024),b''):h.update(chunk)
    return h.hexdigest()
if digest(archive)!=receipt['archive_sha256']:raise ValueError('CLI receipt hash mismatch')
output=HERE/'rigged-corrected'/JOB;output.mkdir(parents=True,exist_ok=True)
with zipfile.ZipFile(archive) as z:
    record=json.loads(z.read('generation-record.json'))
    run=record['rigging']['run_id'];prefix='outputs/rigging/'+run+'/'
    names=('rigged.glb','analysis.glb','edited-input.glb','editor-request.json',
           'editor-state.json','motion-actions-v2.json','preparation.json',
           'rigging-request.json','rigging-result.json')
    for name in names:(output/name).write_bytes(z.read(prefix+name))
    (output/'generation-record.json').write_text(json.dumps(record,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
manifest={'job':JOB,'run':run,'archive_sha256':receipt['archive_sha256'],
          'files':{p.name:digest(p) for p in output.iterdir() if p.is_file() and p.name!='manifest.json'}}
(output/'manifest.json').write_text(json.dumps(manifest,indent=2)+'\n',encoding='utf-8')
print('COLLECTED_CORRECTED_RIG',run)
