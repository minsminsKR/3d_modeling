"""Audit recorded body bindings against the actual new source and UV seams.

This checks source-order correspondence, normalized influences and coincident
surface continuity. It does not certify visible poses or animation contacts.
"""
import argparse
import hashlib
import json
from pathlib import Path

import numpy as np

from glb_io import accessor, read


def audit(folder):
    folder = Path(folder)
    plan = json.loads((folder / 'body-rig-plan.json').read_text(encoding='utf8'))
    source = folder / plan['source']
    document, binary = read(source)
    primitive = document['meshes'][0]['primitives'][0]
    positions = accessor(document, binary, primitive['attributes']['POSITION'])
    with np.load(folder / 'body-surface-weights.npz', allow_pickle=False) as stream:
        recorded = stream['positions']
        joints, weights, names = stream['joints'], stream['weights'], stream['bone_names']
    expected_names = [bone['name'] for bone in plan['bones']]
    correspondence = recorded.shape == positions.shape and np.array_equal(recorded, positions)
    source_hash = hashlib.sha256(source.read_bytes()).hexdigest()
    palette = names.tolist() == expected_names
    valid = bool(np.isfinite(weights).all() and (weights >= 0).all()
                 and (joints < len(names)).all())
    weight_error = float(np.max(np.abs(weights.sum(axis=1) - 1)))
    # UVs/normals may split the source vertex palette. Coincident points must
    # retain the same influences or animation can tear along those atlas seams.
    dense = np.zeros((len(positions), len(names)), dtype=np.float32)
    np.add.at(dense, (np.arange(len(positions))[:, None], joints), weights)
    _, first, inverse = np.unique(np.round(positions * 1e6).astype(np.int32),
                                  axis=0, return_index=True, return_inverse=True)
    seam_error = float(np.max(np.abs(dense - dense[first[inverse]])))
    active = {name: int((dense[:, index] > .5).sum()) for index, name in enumerate(names)}
    hands = all(active.get('mixamorig:' + side + 'Hand', 0) > 100 for side in ('Left', 'Right'))
    passed = (correspondence and source_hash == plan['sourceSHA256'] and palette
              and valid and weight_error < 1e-5 and seam_error < 1e-5 and hands)
    return dict(key=plan['creature'], status='PASS' if passed else 'FAIL',
                sourceSHA256=source_hash, vertices=len(positions), bones=len(names),
                sourceOrderMatches=bool(correspondence), paletteMatches=palette,
                finiteNonnegativeInfluences=valid, maximumWeightSumError=weight_error,
                coincidentSeamVertices=len(positions)-len(first),
                maximumCoincidentWeightDifference=seam_error,
                realPalmOwnership=hands, dominantVertices=active)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('studio', type=Path)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    results = [audit(args.studio / key) for key in ('Uncat', 'Cyclopse', 'Baby')]
    report = dict(status='PASS' if all(r['status'] == 'PASS' for r in results) else 'FAIL',
                  scope='Original source-order binding, palette and coincident UV-seam continuity; final deformed game mesh requires separate pose/render validation.',
                  results=results)
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(report, indent=2), encoding='utf8')
    print(json.dumps(report, indent=2))
    raise SystemExit(0 if report['status'] == 'PASS' else 1)
