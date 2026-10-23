"""Record portable source hashes; no credentials or private CLI state."""
from pathlib import Path
import hashlib
import json

SOURCE=Path(__file__).resolve().parent
PROJECT=SOURCE.parents[1]

def digest(path):
    h=hashlib.sha256()
    with path.open('rb') as stream:
        for block in iter(lambda:stream.read(1024*1024),b''):h.update(block)
    return h.hexdigest()

def normalize_authored_text():
    # Preserve server artifact bytes. Canonicalize only authored text and Unity YAML.
    paths=[p for p in SOURCE.iterdir() if p.suffix in ('.py','.md','.json')]
    paths += [SOURCE/'clock/export.json',SOURCE/'clock/archive-preservation.json']
    folder=PROJECT/'Assets/Resources/SchoolProgressClock'
    paths += [p for p in folder.rglob('*') if p.suffix in ('.meta','.mat','.prefab','.shader','.json')]
    paths.append(folder.with_suffix('.meta'))
    for path in paths:
        if not path.is_file():continue
        text=path.read_text(encoding='utf-8-sig')
        text='\n'.join(line.rstrip() for line in text.splitlines())+'\n'
        path.write_text(text,encoding='utf-8',newline='\n')

def main():
    normalize_authored_text()
    required=[SOURCE/'reference.png',SOURCE/'reference-prompt.json',SOURCE/'request.json',
        SOURCE/'clock/original.glb',SOURCE/'clock/generation-record.json',
        SOURCE/'clock/clock.blend',SOURCE/'clock/export.json',
        PROJECT/'Assets/Resources/SchoolProgressClock/clock-visual.prefab',
        PROJECT/'Assets/Resources/SchoolProgressClock/clock-led.mat']
    for path in required:
        if not path.is_file():raise RuntimeError('Missing preserved clock source: '+str(path))
    generation=json.loads((SOURCE/'clock/generation-record.json').read_text(encoding='utf-8'))
    for entry in generation['artifacts']:
        name='original.glb' if entry['name']=='model.glb' else entry['name']
        path=SOURCE/'clock'/name
        if not path.is_file() or path.stat().st_size!=entry['size'] or digest(path)!=entry['sha256']:
            raise RuntimeError('Preserved server artifact changed: '+name)
    for entry in generation['inputs']:
        path=SOURCE/'clock'/entry['archive_name']
        if digest(path)!=entry['sha256']:raise RuntimeError('Preserved actual input changed')
    export=json.loads((SOURCE/'clock/export.json').read_text(encoding='utf-8'))
    if digest(SOURCE/'clock/original.glb')!=export['originalSha256']:
        raise RuntimeError('Original differs from authoring record')
    for level in export['lods']:
        path=PROJECT/'Assets/Resources/SchoolProgressClock'/level['file']
        if digest(path)!=level['sha256']:raise RuntimeError('Export changed since Blender: '+level['file'])
    paths=list(SOURCE.rglob('*'))+list((PROJECT/'Assets/Resources/SchoolProgressClock').rglob('*'))
    files=[]
    for path in sorted(set(paths)):
        if not path.is_file() or path.name=='provenance.json' or path.suffix in ('.pyc','.blend1','.blend2'):continue
        files.append(dict(path=path.relative_to(PROJECT).as_posix(),bytes=path.stat().st_size,sha256=digest(path)))
    record=dict(schema=1,request_id='791d5129-4cc1-466c-9b50-dfd870ee55bd',job_id='ed7b69e1-6a28-4779-95eb-7f902a9889d1',
        service='https://agrees-mic-icon-contribution.trycloudflare.com',blender='4.0.2',unity='6000.6.0f1',
        original_unchanged=True,game_width_metres=1.4,game_triangle_budgets=[12000,8000],game_texture_limit=1024,
        recipe='SourceArt/SchoolProgressClock/finish_clock.py',
        shared_recipe='SourceArt/CorridorEntrance/finish_entrance_assets.py',files=files)
    (SOURCE/'provenance.json').write_text(json.dumps(record,ensure_ascii=False,indent=2)+'\n',encoding='utf-8',newline='\n')
    print('SCHOOL_CLOCK_PROVENANCE_PASS',len(files))

if __name__=='__main__':main()
