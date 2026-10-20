"""Restore hash-pinned public Freesound previews used/compared by chase authoring."""
from pathlib import Path
import hashlib
import json
import urllib.request

root=Path(__file__).resolve().parents[2]/'ThirdParty/Audio/BeastFootfalls'
for entry in json.loads((root/'sources.json').read_text(encoding='utf-8')).values():
    path=root/'sources'/entry['file']
    if path.is_file() and hashlib.sha256(path.read_bytes()).hexdigest()==entry['source_file_sha256']:
        continue
    data=urllib.request.urlopen(entry['download'],timeout=60).read()
    if hashlib.sha256(data).hexdigest()!=entry['source_file_sha256']:
        raise ValueError('Download fingerprint changed: '+entry['page'])
    path.parent.mkdir(parents=True,exist_ok=True);path.write_bytes(data)
