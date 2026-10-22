"""Record the preserved webapp inputs and actual source/Unity derivation hashes."""
from pathlib import Path
import hashlib
import json

SOURCE = Path(__file__).resolve().parent
PROJECT = SOURCE.parents[1]


def fingerprint(path):
    with path.open('rb') as stream:
        digest = hashlib.file_digest(stream, 'sha256').hexdigest()
    return dict(path=path.relative_to(PROJECT).as_posix(), bytes=path.stat().st_size,
                sha256=digest)


if __name__ == '__main__':
    job = json.loads((SOURCE / 'retractable-gate/job.json').read_text(encoding='utf-8'))
    report = json.loads((SOURCE / 'retractable-gate/export.json').read_text(encoding='utf-8'))
    inputs = [SOURCE / name for name in (
        'user-gate-reference.png', 'reference.png', 'reference-prompt.json',
        'retractable-gate/processed.png', 'retractable-gate/job.json',
        'retractable-gate/original.glb', 'retractable-gate/retractable-gate.blend',
        'retractable-gate/export.json', 'finish_gate.py', 'record_provenance.py',
        'README.md', 'TRELLIS.2-LICENSE.txt')]
    inputs.append(PROJECT / 'SourceArt/CorridorEntrance/finish_entrance_assets.py')
    outputs = sorted(path for path in (PROJECT / 'Assets/Resources/SchoolRetractableGate').rglob('*')
                     if path.is_file() and path.suffix != '.meta')
    data = dict(schema=1, date='2026-10-22',
        purpose='One folded school entry scissor gate, with one padlock stored on the free end post',
        generation=dict(site='https://agrees-mic-icon-contribution.trycloudflare.com/',
            jobId=job['id'], saved=job['saved'], options=job['options'],
            input='reference.png', original='retractable-gate/original.glb'),
        derivation=dict(tool='Blender 4.0.2', buildHash='9be62e85b727',
            recipe='finish_gate.py', sharedRecipe='../CorridorEntrance/finish_entrance_assets.py',
            description='Original geometry and 8K PBR packed unchanged in hidden collection; '
                'copied game meshes normalized, welded, degenerate faces repaired and reduced; '
                '2K game PBR exported from real base color and metallic/roughness, no invented normal/AO',
            originalTriangles=report['originalTriangles'],
            lodTriangles=[level['triangles'] for level in report['lods']],
            unityOrientation=dict(front='-Z', freeEndPost='-X', placementYawDegrees=-90),
            normalizedHeightMetres=2.55, textureMaxSize=report['textureMaxSize']),
        sourceFiles=[fingerprint(path) for path in inputs],
        unityFiles=[fingerprint(path) for path in outputs],
        dependencies='Pinned Unity/URP and Blender installation are documented in README.md; '
            'no server job, Downloads directory, build cache or other checkout is required',
        rights='User supplied photo is a visual reference; generated reference and model are '
            'project work. TRELLIS.2 upstream software notice is retained separately.')
    (SOURCE / 'provenance.json').write_text(json.dumps(data, ensure_ascii=False, indent=2) + '\n',
                                         encoding='utf-8', newline='\n')
    print('SCHOOL_GATE_PROVENANCE_RECORDED sources=' + str(len(inputs)) + ' unityFiles=' + str(len(outputs)))
