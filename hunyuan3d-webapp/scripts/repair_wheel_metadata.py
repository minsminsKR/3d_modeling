# Official bpy/ninja artifacts have malformed embedded WHEEL headers.
# Correct only the metadata to match the actual published wheel filenames.
from pathlib import Path
import importlib.metadata as m
import shutil
p=Path(__file__).resolve().parents[1]
for name,version,tags in [('bpy','4.0.0',['cp310-cp310-manylinux_2_28_x86_64']),('ninja','1.11.1.1',['py3-none-manylinux2014_x86_64','py3-none-manylinux_2_17_x86_64'])]:
    d=m.distribution(name)
    if d.version!=version: continue
    f=Path(d._path)/'WHEEL'
    backup=p/'backups/wheel-metadata'/name/'WHEEL'
    backup.parent.mkdir(parents=True,exist_ok=True)
    if not backup.exists(): shutil.copy2(f,backup)
    lines=[x for x in f.read_text().splitlines() if x.strip() and not x.startswith('Tag:')]
    f.write_text('\n'.join(lines+['Tag: '+t for t in tags])+'\n')
    print('Repaired WHEEL header:',name)
