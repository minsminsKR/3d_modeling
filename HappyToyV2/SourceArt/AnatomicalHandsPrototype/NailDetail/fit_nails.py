"""Fit the CC0 nail source to the prototype rig's measured neutral landmarks.

Run with the project's pinned authoring Python plus scipy==1.11.4. The output
is an intermediate in a caller-specified work directory, never a runtime asset.
"""
import argparse
import json
from pathlib import Path

import numpy as np
from scipy.interpolate import RBFInterpolator

HERE = Path(__file__).resolve().parent
SOURCE = HERE / 'Source'
LANDMARKS = {
    'thumb': [(.038, -.004, .065), (.061, -.012, .09), (.079, -.020, .112), (.088, -.022, .136)],
    'index': [(.033, -.002, .125), (.039, -.006, .157), (.046, -.010, .188), (.052, -.014, .218)],
    'middle': [(.012, .001, .132), (.014, -.003, .166), (.016, -.007, .198), (.018, -.011, .228)],
    'ring': [(-.012, .001, .13), (-.012, -.003, .160), (-.012, -.007, .192), (-.012, -.011, .220)],
    'little': [(-.035, -.002, .115), (-.041, -.006, .140), (-.047, -.012, .166), (-.050, -.018, .188)],
}


def mesh(path):
    vertices, uv, faces, groups = [], [], [], {}
    group = ''
    for line in path.read_text(encoding='utf8').splitlines():
        if line.startswith('v '):
            vertices.append(list(map(float, line.split()[1:4])))
        elif line.startswith('vt '):
            uv.append(list(map(float, line.split()[1:3])))
        elif line.startswith('g '):
            group = line.split()[1]
            groups[group] = set()
        elif line.startswith('f '):
            parts = [part.split('/') for part in line.split()[1:]]
            if group.startswith('joint-l-'):
                groups[group].update(int(part[0]) - 1 for part in parts)
            if all(len(part) > 1 and part[1] for part in parts):
                faces.append([(int(part[0]) - 1, int(part[1]) - 1) for part in parts])
    return np.asarray(vertices), uv, faces, groups


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--output-dir', required=True, type=Path)
    args = parser.parse_args()
    args.output_dir.mkdir(parents=True, exist_ok=True)
    base, _, _, groups = mesh(SOURCE / 'makehuman-base.obj')
    source, target = [], []
    for digit_number, points in enumerate(LANDMARKS.values(), 1):
        for joint_number, point in enumerate(points, 1):
            group = groups[f'joint-l-finger-{digit_number}-{joint_number}']
            source.append(base[list(group)].mean(axis=0))
            target.append(point)
    for name, point in [('joint-l-hand', (0, 0, .035)), ('joint-l-elbow', (0, 0, -.26))]:
        source.append(base[list(groups[name])].mean(axis=0))
        target.append(point)
    source, target = np.asarray(source), np.asarray(target)
    _, _, axes = np.linalg.svd(source[:20] - source[:20].mean(axis=0), full_matrices=False)
    normal = axes[-1]
    if normal[1] < 0:
        normal = -normal
    controls = np.concatenate((source, source[:20] + normal * .08, source[:20] - normal * .08))
    values = np.concatenate((target, target[:20] + [0, .008, 0], target[:20] - [0, .008, 0]))
    fit = RBFInterpolator(controls, values, kernel='thin_plate_spline', smoothing=.0002)
    nails, uv, faces, _ = mesh(SOURCE / 'mind_nails_01_short.obj')
    faces = [face for face in faces if all(nails[index, 0] > 0 for index, _ in face)]
    ids = sorted({index for face in faces for index, _ in face})
    remap = {index: i for i, index in enumerate(ids)}
    result = {'verts': fit(nails[ids]).tolist(),
              'faces': [[remap[index] for index, _ in face] for face in faces],
              'uv': [[uv[index] for _, index in face] for face in faces]}
    (args.output_dir / 'fitted-nails.json').write_text(json.dumps(result), encoding='utf8')
    print(f'Fitted {len(ids)} vertices in {len(faces)} faces')


if __name__ == '__main__':
    main()
