"""Refresh the two ritual additions' local handoff records after export/import.

python HappyToyV2/SourceArt/CorridorEntrance/record_ritual_assets.py
Requires the preserved actual webapp job JSON, original GLB, inputs, packed
Blender source and imported Unity resources. Does not generate or replace art.
"""
from pathlib import Path
import hashlib
import json
import struct

SOURCE = Path(__file__).resolve().parent
PROJECT = SOURCE.parents[1]


def record(path):
    raw = path.read_bytes()
    result = dict(path=path.relative_to(PROJECT).as_posix(), bytes=len(raw),
                  sha256=hashlib.sha256(raw).hexdigest())
    if path.suffix == '.meta':
        for line in raw.decode('utf-8-sig').splitlines():
            if line.startswith('guid: '):
                result['guid'] = line[6:]
    return result


def capture(key):
    folder = SOURCE / key
    job = json.loads((folder / 'job.json').read_text(encoding='utf-8-sig'))
    exported = json.loads((folder / 'export.json').read_text(encoding='utf-8-sig'))
    original = folder / 'original.glb'
    with original.open('rb') as stream:
        magic, version, size = struct.unpack('<4sII', stream.read(12))
    if magic != b'glTF' or version != 2 or size != original.stat().st_size:
        raise ValueError('Hydrated original GLB required: ' + key)
    if record(original)['sha256'] != exported['originalSha256']:
        raise ValueError('Export does not derive from the preserved original: ' + key)
    if job['status'] != 'completed' or not job['saved']:
        raise ValueError('Actual completed and saved webapp job required')
    output = PROJECT / 'Assets/Resources/CorridorEntrance'
    resources = sorted(path for path in (output / key).glob('*') if path.is_file())
    resources += sorted(output.glob(key + '-lod*.fbx*'))
    resources += sorted(output.glob(key + '-visual.prefab*'))
    resources += [output / (key + '.meta')]
    prefab = output / (key + '-visual.prefab')
    if not prefab.is_file() or len(exported['lods']) != 2:
        raise ValueError('Both real LODs and imported visual prefab required')
    required = [prefab, Path(str(prefab) + '.meta'), output / (key + '.meta'),
                output / key / 'asset.json', output / key / 'asset.json.meta']
    for lod in exported['lods']:
        mesh = output / lod['file']
        required += [mesh, Path(str(mesh) + '.meta')]
        if record(mesh)['sha256'] != lod['sha256']:
            raise ValueError('Exported LOD hash mismatch: ' + key)
    for material in exported['materials']:
        path = output / key / (material['name'] + '.mat')
        required += [path, Path(str(path) + '.meta')]
        for field in ('baseColor', 'metallicSmoothness', 'normal', 'occlusion', 'emission'):
            if field in material:
                path = output / key / material[field]
                required += [path, Path(str(path) + '.meta')]
    for path in required:
        if not path.is_file() or path.stat().st_size == 0:
            raise ValueError('Required local resource or metadata absent: ' + str(path))
    if (output / key / 'asset.json').read_bytes() != (folder / 'export.json').read_bytes():
        raise ValueError('Imported source report differs from the authoring export: ' + key)
    editable = record(folder / (key + '.blend'))
    previous = {}
    provenance = folder / 'provenance.json'
    if provenance.is_file():
        candidate = json.loads(provenance.read_text(encoding='utf-8-sig')).get('finishing', {})
        if candidate.get('editable', {}).get('sha256') == editable['sha256']:
            previous = candidate
    current_script = record(SOURCE / 'finish_entrance_assets.py')
    current_report = record(folder / 'export.json')
    value = dict(
        asset=key,
        reference=record(folder / 'reference.png'),
        referenceGeneration=record(folder / 'reference-generation.json'),
        processedInput=record(folder / 'processed.png'),
        generator=dict(application='hunyuan3d-webapp',
                       site='https://agrees-mic-icon-contribution.trycloudflare.com/',
                       jobId=job['id'], batchId=job.get('batch_id'),
                       options=job['options'], elapsedSeconds=job['elapsed_seconds'],
                       record=record(folder / 'job.json'), savedOnServer=job['saved'],
                       note='Server paths and links are historical evidence only; all required inputs and outputs are local.'),
        original=record(original),
        finishing=dict(tool='Blender 4.0.2',
                       script=previous.get('script', current_script),
                       command='blender --background --factory-startup --python-exit-code 1 --python '
                               'HappyToyV2/SourceArt/CorridorEntrance/finish_entrance_assets.py -- --only ' + key,
                       editable=editable,
                       exportReport=previous.get('exportReport', current_report),
                       currentReproductionScript=current_script,
                       currentReproductionExportReport=current_report,
                       historicalExportNote='The script/exportReport records retain the raw hashes for this preserved editable export. Current reproduction records reflect LF-normalized local text; binary originals and editable artifacts are unchanged.',
                       embeddedSourceText='SOURCE - finish_entrance_assets.py',
                       settings=exported['finishing'],
                       originalTriangles=exported['originalTriangles'], lods=exported['lods']),
        unityImport=dict(editor='6000.6.0f1',
                         method='HappyToy.V2.Editor.CorridorEntranceImport.ImportBuddhistLantern' if key=='buddhist-lantern' else 'HappyToy.V2.Editor.CorridorEntranceImport.Apply',
                         script=record(PROJECT / 'Assets/Editor/CorridorEntranceImport.cs'),
                         scriptMeta=record(PROJECT / 'Assets/Editor/CorridorEntranceImport.cs.meta'),
                         runtimePresentation=record(PROJECT / 'Assets/Scripts/CorridorEntrancePresentation.cs'),
                         runtimeCollaborators=[record(PROJECT / 'Assets/Scripts' / (name + '.cs')) for name in (
                             'CorridorEntranceModels', 'CorridorEntranceCavern', 'CorridorEntranceRitualDressing',
                             'CorridorEntranceSealInk', 'CorridorEntranceVapour')],
                         resourcesKey='CorridorEntrance/' + key + '-visual',
                         outputs=[record(path) for path in resources]),
        licenses=[record(SOURCE / name) for name in ('LICENSE.md', 'TRELLIS.2-LICENSE.txt')],
        verificationScope='Source handoff hashes only; actual camera review, focused integration checks and native performance are separate evidence.')
    if key=='buddhist-lantern':
        value['finishing']['supportingRecipes']=[record(SOURCE/'rebake_lantern_atlas.py')]
        value['finishing']['embeddedSupportingSourceText']='SOURCE - rebake_lantern_atlas.py'
        value['unityImport']['sharedVisualLoader']=[record(PROJECT/'Assets/Scripts'/name) for name in (
            'BuddhistLanternVisual.cs','BuddhistLanternVisual.cs.meta')]
        value['unityImport']['corridorPresentation']=record(PROJECT/'Assets/Scripts/HauntedCorridorPresentation.cs')
    provenance.write_text(json.dumps(value, indent=2, ensure_ascii=False) + '\n', encoding='utf-8', newline='\n')
    print('RITUAL_ENTRANCE_HANDOFF ' + key + ' ' + str(len(resources)) + ' resource files')


def refresh_existing_import(key):
    """Refresh shared importer outputs without claiming the base was re-exported."""
    path = SOURCE / key / 'provenance.json'
    data = json.loads(path.read_text(encoding='utf-8-sig'))
    imported = data['unityImport']
    imported['script'] = record(PROJECT / 'Assets/Editor/CorridorEntranceImport.cs')
    imported['scriptMeta'] = record(PROJECT / 'Assets/Editor/CorridorEntranceImport.cs.meta')
    imported['outputs'] = [record(PROJECT / entry['path']) for entry in imported['outputs']]
    data['exportedResources'] = [record(PROJECT / entry['path']) for entry in data['exportedResources']]
    imported['runtimePresentation'] = record(PROJECT / 'Assets/Scripts/CorridorEntrancePresentation.cs')
    imported['runtimeCollaborators'] = [record(PROJECT / 'Assets/Scripts' / (name + '.cs')) for name in (
        'CorridorEntranceModels', 'CorridorEntranceCavern',
        'CorridorEntranceRitualDressing', 'CorridorEntranceSealInk', 'CorridorEntranceVapour')]
    data['finishing']['currentReproductionExportReport'] = record(SOURCE / key / 'export.json')
    data['finishing']['currentReproductionScript'] = record(SOURCE / 'finish_entrance_assets.py')
    data['finishing']['historicalExportNote'] = (
        'Original geometry, PBR and packed Blender are unchanged. The original '
        'script/exportReport records retain the raw hashes for that historical export; '
        'currentReproductionExportReport has the same report data with LF-normalized text. '
        'currentReproductionScript adds the two ritual assets and bounded budget-rounding correction. '
        'exportedResources records the current local Unity resource bytes, including the LF-normalized '
        'asset.json copy of that report. Only current Unity import metadata/prefab serialization '
        'and shared presentation references are refreshed; no original geometry or PBR was regenerated.')
    data['licenses'] = [record(SOURCE / name) for name in ('LICENSE.md', 'TRELLIS.2-LICENSE.txt')]
    path.write_text(json.dumps(data, indent=2, ensure_ascii=False) + '\n', encoding='utf-8', newline='\n')
    print('EXISTING_ENTRANCE_IMPORT_HANDOFF ' + key)


if __name__ == '__main__':
    import argparse
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--only',choices=('ritual-floor','guardian-statue','buddhist-lantern'))
    args=parser.parse_args()
    if args.only:
        capture(args.only)
        raise SystemExit(0)
    for requested in ('ritual-floor', 'guardian-statue', 'buddhist-lantern'):
        capture(requested)
    for requested in ('temple-gate', 'cave-rock'):
        refresh_existing_import(requested)
