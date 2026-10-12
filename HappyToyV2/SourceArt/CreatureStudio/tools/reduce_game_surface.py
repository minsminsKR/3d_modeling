"""Weld source UV splits and simplify continuous game geometry only.

Python 3.11, numpy 1.26.4, fast-simplification 0.1.12. Original generated
GLB/PBR stays untouched. Blender reprojects its atlas and reviewed skin.
"""
import argparse
import hashlib
import json
from pathlib import Path
import numpy as np
import fast_simplification
from glb_io import read, accessor


def reduce(source, destination, triangles):
    doc, binary = read(source)
    if len(doc['meshes']) != 1 or len(doc['meshes'][0]['primitives']) != 1:
        raise ValueError('Review multi-material source explicitly')
    primitive = doc['meshes'][0]['primitives'][0]
    points = accessor(doc, binary, primitive['attributes']['POSITION']).astype(np.float64)
    faces = accessor(doc, binary, primitive['indices']).reshape(-1, 3)
    _, first, inverse = np.unique(np.round(points * 1e5).astype(np.int64), axis=0,
                                  return_index=True, return_inverse=True)
    welded = points[first]
    faces = inverse[faces]
    valid = (faces[:,0] != faces[:,1]) & (faces[:,1] != faces[:,2]) & (faces[:,2] != faces[:,0])
    faces = faces[valid]
    area = np.linalg.norm(np.cross(welded[faces[:,1]]-welded[faces[:,0]],
                                  welded[faces[:,2]]-welded[faces[:,0]]),axis=1)
    faces = faces[area > 1e-14]
    _, unique = np.unique(np.sort(faces, axis=1), axis=0, return_index=True)
    faces = faces[np.sort(unique)]
    print(f'Simplifying {len(welded)} welded vertices / {len(faces)} triangles', flush=True)
    result, reduced = fast_simplification.simplify(welded, faces, target_count=triangles, agg=10)
    result = np.asarray(result); reduced = np.asarray(reduced, dtype=np.int32)
    valid=(reduced[:,0]!=reduced[:,1])&(reduced[:,1]!=reduced[:,2])&(reduced[:,2]!=reduced[:,0])
    reduced=reduced[valid]
    _,unique=np.unique(np.sort(reduced,axis=1),axis=0,return_index=True)
    reduced=reduced[np.sort(unique)]
    print(f'Reduction yielded {len(result)} vertices / {len(reduced)} triangles',flush=True)
    if len(reduced) > triangles * 1.05 or len(reduced) == 0:
        raise ValueError('Reduction missed the requested budget')
    destination.parent.mkdir(parents=True, exist_ok=True)
    np.savez_compressed(destination, positions=result, triangles=reduced)
    report = dict(source=source.name, sourceSHA256=hashlib.sha256(source.read_bytes()).hexdigest(),
                  tool='fast-simplification 0.1.12', coordinateSystem='source glTF Y-up',
                  sourceBoundsGLTF=[points.min(0).tolist(),points.max(0).tolist()],
                  originalVertices=len(points), weldedVertices=len(welded),
                  gameVertices=len(result), gameTriangles=len(reduced), originalUnchanged=True,
                  requiredBlenderStep='reproject original source atlas and reviewed surface skin')
    destination.with_suffix('.json').write_text(json.dumps(report, indent=2), encoding='utf8')
    print(json.dumps(report), flush=True)


if __name__ == '__main__':
    parser=argparse.ArgumentParser(); parser.add_argument('source',type=Path)
    parser.add_argument('destination',type=Path);parser.add_argument('--triangles',type=int,default=25000)
    args=parser.parse_args();reduce(args.source,args.destination,args.triangles)