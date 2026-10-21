"""Record hydrated source handoff after Blender finishing and final Unity import.

python HappyToyV2/SourceArt/SchoolLoop/record_music_sources.py
Does not regenerate art or certify runtime/native performance.
"""
from pathlib import Path
import hashlib
import json
import struct

SOURCE=Path(__file__).resolve().parent
PROJECT=SOURCE.parents[1]
def normalize_import_text():
    # Unity may emit CRLF on Windows. These exact new resources have eol=lf
    # in .gitattributes, so hash the bytes another checkout will actually have.
    # Run only after the importing/testing Editor has exited.
    folder=PROJECT/'Assets/Resources/SchoolLoop'
    paths=[p for p in folder.rglob('*') if p.is_file() and p.suffix in ('.meta','.mat','.prefab','.json')]
    paths += [folder.with_suffix('.meta'),PROJECT/'Assets/Editor/SchoolLoopAssetsImport.cs.meta']
    for path in paths:
        raw=path.read_bytes()
        stable=raw.replace(b'\r\n',b'\n')
        if raw!=stable:path.write_bytes(stable)

def record(path):
    raw=path.read_bytes()
    value=dict(path=path.relative_to(PROJECT).as_posix(),bytes=len(raw),sha256=hashlib.sha256(raw).hexdigest())
    if path.suffix=='.meta':
        for line in raw.decode('utf-8-sig').splitlines():
            if line.startswith('guid: '):value['guid']=line[6:]
    return value

def capture(key):
    source=SOURCE/key;job=json.loads((source/'job.json').read_text(encoding='utf-8-sig'))
    exported=json.loads((source/'export.json').read_text(encoding='utf-8-sig'))
    if job['status']!='completed' or not job['saved']:raise ValueError('Actual saved completed job required')
    original=source/'original.glb'
    with original.open('rb') as stream:magic,version,size=struct.unpack('<4sII',stream.read(12))
    if magic!=b'glTF' or version!=2 or size!=original.stat().st_size or record(original)['sha256']!=exported['originalSha256']:
        raise ValueError('Hydrated unchanged original required')
    folder=PROJECT/'Assets/Resources/SchoolLoop'
    outputs=sorted(path for path in (folder/key).iterdir() if path.is_file())
    outputs+=sorted(folder.glob(key+'-lod*.fbx*'))+sorted(folder.glob(key+'-visual.prefab*'))+[folder/(key+'.meta')]
    for level in exported['lods']:
        if record(folder/level['file'])['sha256']!=level['sha256']:raise ValueError('LOD does not match the source export')
    if (folder/key/'asset.json').read_bytes()!=(source/'export.json').read_bytes():raise ValueError('Game/source report mismatch')
    value=dict(asset=key,reference=record(source/'reference.png'),referenceGeneration=record(source/'reference-generation.json'),
        processedInput=record(source/'processed.png'),job=record(source/'job.json'),
        generation=dict(application='designated hunyuan3d-webapp',site='https://agrees-mic-icon-contribution.trycloudflare.com/',
            jobId=job['id'],batchId=job['batch_id'],options=job['options'],elapsedSeconds=job['elapsed_seconds'],saved=True),
        original=record(original),editable=record(source/(key+'.blend')),exportReport=record(source/'export.json'),
        finishing=dict(tool='Blender 4.0.2',recipes=[record(SOURCE/'finish_music_assets.py'),record(SOURCE/'rebake_music_lod.py'),
            record(PROJECT/'SourceArt/CorridorEntrance/finish_entrance_assets.py')],
            command='blender -b --factory-startup --python-exit-code 1 --python HappyToyV2/SourceArt/SchoolLoop/finish_music_assets.py -- --only '+key,
            embeddedRecipes=['SOURCE - finish_music_assets.py','SOURCE - finish_entrance_assets.py','SOURCE - rebake_music_lod.py'],
            settings=exported['finishing'],originalTriangles=exported['originalTriangles'],lods=exported['lods']),
        unityImport=dict(editor='6000.6.0f1',method='HappyToy.V2.Editor.SchoolLoopAssetsImport.Apply',
            importer=record(PROJECT/'Assets/Editor/SchoolLoopAssetsImport.cs'),importerMeta=record(PROJECT/'Assets/Editor/SchoolLoopAssetsImport.cs.meta'),
            resourceKey='SchoolLoop/'+key+'-visual',outputs=[record(path) for path in outputs]),
        license=record(SOURCE/'LICENSE.md'),scope='Exact local source handoff. Render review, loop physics, frame timing and native build verification are separate evidence.')
    (source/'provenance.json').write_text(json.dumps(value,indent=2,ensure_ascii=False)+'\n',encoding='utf-8',newline='\n')
    print('SCHOOL_LOOP_HANDOFF '+key+' '+str(len(outputs))+' resource files')

if __name__=='__main__':
    normalize_import_text()
    for key in ('grand-piano','music-stand'):capture(key)
    reference=SOURCE/'References/school-loop-target-20261021.png'
    (SOURCE/'References/provenance.json').write_text(json.dumps(dict(role='User-supplied school plan and visual target',
        originalFilename='어두운 학교 복도 평면도와 안내 패널.png',image=record(reference)),indent=2,ensure_ascii=False)+'\n',encoding='utf-8',newline='\n')
