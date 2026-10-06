"""Read-only fresh Windows artifact/input checks; does not execute the player."""
from pathlib import Path
import argparse,datetime,hashlib,json,sys

def sha(path):
    digest=hashlib.sha256()
    with path.open('rb') as stream:
        for block in iter(lambda:stream.read(1024*1024),b''):digest.update(block)
    return digest.hexdigest()

def verify(project,build):
    project=project.resolve();build=build.resolve()
    manifest=json.loads((build/'quality-build.json').read_text(encoding='utf-8-sig'))
    errors=[]
    def check(condition,message):
        if not condition:errors.append(message)
    check(manifest.get('status')=='PASS','Build manifest did not report PASS')
    check(manifest.get('unityVersion')=='6000.6.0f1','Build Unity version differs from pinned editor')
    check(manifest.get('scene')=='Assets/Annex/SchoolAnnex.unity','Build scene differs from preserved authored school')
    expected={entry.get('path',''):entry.get('sha256','') for entry in manifest.get('inputs',[])}
    check(len(expected)==len(manifest.get('inputs',[])) and bool(expected),'Duplicate/empty build-input manifest')
    actual={path.relative_to(project).as_posix():path for folder in ('Assets','Packages','ProjectSettings') for path in (project/folder).rglob('*') if path.is_file()}
    check(actual.keys()==expected.keys(),'Build-input inventory changed: added '+str(sorted(actual.keys()-expected.keys()))+'; removed '+str(sorted(expected.keys()-actual.keys())))
    for name,digest in expected.items():
        path=actual.get(name)
        if path:check(sha(path)==digest,'Current source changed since build: '+name)
    scene='Assets/Annex/SchoolAnnex.unity'
    check(expected.get(scene)=='0f2d25f211c76c7aa5299702ad15cf52d042f5a316ef32f5c4f43d69994aaede','Preserved scene fingerprint differs')
    anchors=sorted(name for name in expected if name.startswith('Assets/Resources/FeedbackShaderVariants/') and name.endswith('.mat'))
    check(len(anchors)==4 and all(name+'.meta' in expected for name in anchors),'Four serialized shader anchors/metadata omitted from build inputs')
    artifacts=[]
    for name in ('HappyToyV2.exe','UnityPlayer.dll','HappyToyV2_Data/globalgamemanagers','Audio-Credits.txt'):
        path=build/name
        check(path.is_file() and path.stat().st_size>0,'Native artifact missing/empty: '+name)
        if path.is_file():artifacts.append({'path':name,'bytes':path.stat().st_size,'sha256':sha(path)})
    credits=build/'Audio-Credits.txt';source_credits=project/'ThirdParty/Audio/Audio-Credits.txt'
    check(credits.is_file() and sha(credits)==sha(source_credits),'Copied audio credits differ from the current source')
    return {'status':'BUILD_INPUTS_PASS' if not errors else 'FAIL',
            'scope':'Fresh native artifact existence/checksums, full current Assets/Packages/ProjectSettings inventory/hashes and copied credit equality; does not certify actual native shader appearance, route completion, DSP/device audio or performance.',
            'readUtc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'project':str(project),'build':str(build),'errors':errors,
            'manifestCreatedUtc':manifest.get('createdUtc'),'unity':manifest.get('unityVersion'),'inputCount':len(expected),'shaderAnchors':anchors,'artifacts':artifacts}

if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--project',required=True,type=Path);parser.add_argument('--build',required=True,type=Path);parser.add_argument('--output',required=True,type=Path)
    args=parser.parse_args()
    try:result=verify(args.project,args.build)
    except Exception as error:result={'status':'FAIL','scope':'Artifact/parser failure; no build-input pass claimed','errors':[str(error)]}
    args.output.write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
    print(json.dumps(result,ensure_ascii=False,indent=2));sys.exit(0 if result['status']=='BUILD_INPUTS_PASS' else 1)
