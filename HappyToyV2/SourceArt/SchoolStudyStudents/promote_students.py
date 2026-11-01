"""Copy already reviewed Blender outputs into the Unity resource boundary."""
import hashlib
import json
from pathlib import Path
import shutil

HERE=Path(__file__).resolve().parent
PROJECT=HERE.parents[1]
JOBS=('10c8d883-8918-44d3-bcd5-7cf85bffff26',
      'b1f35610-5dd0-4d0d-ac15-e9c109bba82f',
      '0ac03514-069f-4afc-a8cc-90d4d86cff8f')
FILES={'student.fbx':'student.fbx','student-review-baseColor.png':'student-baseColor.png',
       'student-review-metallicSmoothness.png':'student-metallicSmoothness.png','student-review-normal.png':'student-normal.png'}
def sha(p):
    h=hashlib.sha256()
    with p.open('rb') as f:
        for block in iter(lambda:f.read(1024*1024),b''):h.update(block)
    return h.hexdigest()
for i,job in enumerate(JOBS,1):
    label=f'student{i:02d}';source=PROJECT/'Builds'/'SchoolStudyStudentsReview'/job/'export'
    editable=HERE/'finished'/(label+'.blend')
    if not editable.is_file():raise FileNotFoundError(editable)
    target=PROJECT/'Assets'/'Resources'/'SchoolStudyStudents'/label;target.mkdir(parents=True,exist_ok=True)
    for old,new in FILES.items():
        if not (source/old).is_file():raise FileNotFoundError(source/old)
        shutil.copyfile(source/old,target/new)
    manifest={'job':job,'editable_sha256':sha(editable),'assets':{v:sha(target/v) for v in FILES.values()}}
    (target/'source-manifest.json').write_text(json.dumps(manifest,indent=2)+'\n',encoding='utf-8')
    print(label,'prepared',flush=True)
