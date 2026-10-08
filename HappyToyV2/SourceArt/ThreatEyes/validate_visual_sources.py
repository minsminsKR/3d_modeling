"""Validate editable visual sources/UV measurements; no Unity or optional previews."""
from pathlib import Path
import hashlib
import json
import re

PROJECT = Path(__file__).resolve().parents[2]


def main():
    source = PROJECT/'Assets/Scripts/MonsterRedEyes.cs'
    code = source.read_text(encoding='utf-8-sig')
    records = json.loads((Path(__file__).parent/'eye-surface-anchors.json').read_text())['assets']
    records += json.loads((Path(__file__).parent/'static-eye-surface-anchors.json').read_text())['assets']
    assert {record['key'] for record in records} == {'Cyclopse','Uncat','Hwacat_angry','Baby','LanternMask','Mannequin'}
    for record in records:
        anchors = record['eyeAnchors']
        assert len(anchors) == (4 if record['key']=='LanternMask' else 2)
        for anchor in anchors:
            assert all(0 <= value <= 1 for value in anchor['uv'])
            for value in anchor['uv']:
                assert f'{value:.9f}'.lstrip('0') in code, (record['key'], value, 'UV profile drifted from authoring measurement')
    assert not re.search(r'AddComponent\s*<\s*(Light|Collider|NavMeshObstacle)', code)
    hardware = (PROJECT/'Assets/Scripts/CorridorDoorHardware.cs').read_text(encoding='utf-8-sig')
    assert not re.search(r'AddComponent\s*<\s*(Light|Collider|NavMeshObstacle|Interactable)', hardware)
    inventory = []
    for path in sorted((PROJECT/'SourceArt/ThreatEyes').rglob('*')):
        if not path.is_file() or '__pycache__' in path.parts or path.name=='source-inventory.json': continue
        inventory.append(dict(path=path.relative_to(PROJECT).as_posix(),sha256=hashlib.sha256(path.read_bytes()).hexdigest()))
    for relative in ('Assets/Scripts/MonsterRedEyes.cs','Assets/Scripts/CorridorDoorHardware.cs',
        'Assets/Editor/ThreatEyeModelImport.cs','Assets/Tests/PlayMode/CloudRedEyeDoorVisualTests.cs'):
        path=PROJECT/relative
        assert path.is_file() and Path(str(path)+'.meta').is_file(), relative
        inventory.append(dict(path=relative,sha256=hashlib.sha256(path.read_bytes()).hexdigest()))
    result=dict(status='PASS',profiles=len(records),scope='Source/measurement validation only; Unity/native review is separate.',sources=inventory)
    (Path(__file__).parent/'source-inventory.json').write_text(json.dumps(result,indent=2),encoding='utf-8')
    print(json.dumps(dict(status=result['status'],profiles=len(records),sourceCount=len(inventory)),indent=2))


if __name__=='__main__': main()
