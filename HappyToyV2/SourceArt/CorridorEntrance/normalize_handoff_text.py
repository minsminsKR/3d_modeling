"""Normalize this revision's exact source-handoff text inputs to portable LF.

python HappyToyV2/SourceArt/CorridorEntrance/normalize_handoff_text.py
After Unity has exited, add --include-unity for the six known Unity text inputs.
Binary models, editable blends, images, GUIDs and historical hashes are untouched.
Run record_ritual_assets.py afterwards to refresh current reproduction/import records.
"""
from pathlib import Path
import argparse
import json

HERE = Path(__file__).resolve().parent
PROJECT = HERE.parents[1]
UNITY_INPUTS = (
    'Assets/Editor/CorridorEntranceImport.cs.meta',
    'Assets/Scripts/CorridorEntrancePresentation.cs',
    'Assets/Resources/CorridorEntrance/temple-gate/asset.json',
    'Assets/Resources/CorridorEntrance/cave-rock/asset.json',
    'Assets/Resources/CorridorEntrance/ritual-floor/asset.json',
    'Assets/Resources/CorridorEntrance/guardian-statue/asset.json',
)


def normalize(include_unity=False):
    paths = set(HERE.glob('*.py')) | set(HERE.rglob('*.json'))
    paths.update(HERE / name for name in ('LICENSE.md', 'TRELLIS.2-LICENSE.txt'))
    lod = PROJECT / 'SourceArt/BaeksugwiStudio'
    paths.add(lod / 'derive_game_lod.py')
    paths.update(lod.glob('game-lod-*.json'))
    paths.update(PROJECT / 'SourceArt/PlayerItems' / name
                 for name in ('finish_player_items.py', 'finish_skin_detail.py'))
    if include_unity:
        paths.update(PROJECT / path for path in UNITY_INPUTS)
    changed = []
    for path in sorted(paths):
        raw = path.read_bytes()
        # Validate text before touching a file. Newline conversion is the only edit.
        raw.decode('utf-8-sig')
        normalized = raw.replace(b'\r\n', b'\n')
        if raw != normalized:
            path.write_bytes(normalized)
            changed.append(path.relative_to(PROJECT).as_posix())
    print(json.dumps({'normalized': changed, 'unityIncluded': include_unity}, indent=2))
    return changed


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--include-unity', action='store_true',
                        help='Normalize the six known Unity text inputs; use only after Unity has exited')
    normalize(parser.parse_args().include_unity)
