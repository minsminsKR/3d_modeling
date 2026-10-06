"""Verify required editable project sources are in Git's index; no Unity needed."""
from pathlib import Path
import subprocess,json,sys,os

project=Path(__file__).resolve().parents[2]
repo=project.parent
command=['git','-c','safe.directory='+repo.as_posix(),'ls-files','-z']
tracked=set(subprocess.check_output(command,cwd=repo).decode('utf-8').split('\0'))
required=[]
for folder in ('Assets','Packages','ProjectSettings','SourceArt','Tools','ThirdParty'):
    for directory,dirs,files in os.walk(project/folder):
        excluded={'__pycache__','.git','.pytest_cache'}
        if folder in ('SourceArt','Tools'):
            excluded.update(('proposed','staged','previews','work','bin','obj','.venv','venv'))
        dirs[:]=[name for name in dirs if name not in excluded]
        for filename in files:
            path=Path(directory)/filename
            if path.suffix in ('.pyc','.pyo','.blend1','.blend2') or filename=='.DS_Store': continue
            required.append(path.relative_to(repo).as_posix())
required.extend(path.relative_to(repo).as_posix() for path in project.glob('*.md'))
required.extend(('AGENTS.md','HappyToyV2/.gitignore'))
missing=sorted(set(required)-tracked)
lfsPointers=[]
for name in required:
    with (repo/name).open('rb') as stream:
        if stream.read(42).startswith(b'version https://git-lfs.github.com/spec/v1'): lfsPointers.append(name)
result=dict(status='PASS' if not missing and not lfsPointers else 'FAIL',requiredSourceCount=len(set(required)),
    missingFromGit=missing,unhydratedLfsFiles=lfsPointers,scope='Required local editable source files vs Git index; not a Unity build or clone execution test.')
print(json.dumps(result,indent=2))
sys.exit(0 if result['status']=='PASS' else 1)
