"""Audit a newly generated AI rig before its explicit semantic mapping is reviewed."""
import argparse
import hashlib
import json
from pathlib import Path
import numpy as np
from glb_io import read, accessor, node_matrices


def inspect(path):
    document, binary = read(path)
    matrices, parents = node_matrices(document)
    report = {'input': Path(path).name, 'inputSHA256': hashlib.sha256(Path(path).read_bytes()).hexdigest(),
              'coordinateSystem': 'glTF Y-up, Blender conversion (x,-z,y)', 'meshes': [], 'skins': []}
    for node_id, node in enumerate(document.get('nodes', [])):
        if 'mesh' not in node:
            continue
        for primitive_id, primitive in enumerate(document['meshes'][node['mesh']]['primitives']):
            attrs = primitive['attributes']
            positions = accessor(document, binary, attrs['POSITION'])
            transform = matrices[node_id]
            positions = positions @ transform[:3, :3].T + transform[:3, 3]
            entry = {'node': node_id, 'name': node.get('name'), 'primitive': primitive_id,
                     'vertices': len(positions), 'bounds': [positions.min(0).tolist(), positions.max(0).tolist()],
                     'material': primitive.get('material'), 'skin': node.get('skin'),
                     'hasUV': 'TEXCOORD_0' in attrs, 'attributes': list(attrs)}
            if 'indices' in primitive:
                entry['triangles'] = len(accessor(document, binary, primitive['indices'])) // 3
            if 'JOINTS_0' in attrs:
                joints = accessor(document, binary, attrs['JOINTS_0'], normalized=False)
                weights = accessor(document, binary, attrs['WEIGHTS_0'])
                dominant = joints[np.arange(len(joints)), weights.argmax(1)]
                entry.update(maxJointIndex=int(joints.max()), maxWeightSumError=float(abs(weights.sum(1)-1).max()),
                             zeroWeightVertices=int((weights.sum(1)<.99).sum()),
                             dominantCounts=np.bincount(dominant.astype(int)).tolist())
            report['meshes'].append(entry)
    for skin_id, skin in enumerate(document.get('skins', [])):
        bind = accessor(document, binary, skin['inverseBindMatrices']).reshape(-1, 4, 4).transpose(0, 2, 1)
        report['skins'].append({'index': skin_id, 'name': skin.get('name'), 'joints': [
            {'index': index, 'node': node_id, 'name': document['nodes'][node_id].get('name'),
             'parentNode': parents.get(node_id), 'position': matrices[node_id][:3, 3].tolist(),
             'inverseBindClosureError': float(abs(matrices[node_id] @ bind[index] - np.eye(4)).max())}
            for index, node_id in enumerate(skin['joints'])]})
    report['animationNames'] = [entry.get('name') for entry in document.get('animations', [])]
    return report


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('input', type=Path)
    parser.add_argument('--output', type=Path)
    args = parser.parse_args()
    report = inspect(args.input)
    if args.output:
        args.output.write_text(json.dumps(report, indent=2, ensure_ascii=False), encoding='utf8')
    print(json.dumps(report, indent=2, ensure_ascii=False))
