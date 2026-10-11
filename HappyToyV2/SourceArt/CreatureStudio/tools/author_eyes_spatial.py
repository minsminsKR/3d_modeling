"""Map reviewed original front-face eye regions to actual new source UV triangles.

The reviewer records pixel landmarks in preview_geometry.py's orthographic face
view. We select frontmost anatomical triangles, preserve atlas seams, and exclude
the reviewed pupil. Nothing fills a polygon across disconnected UV islands.
"""
import argparse
import hashlib
import json
from pathlib import Path

import numpy as np
from scipy.sparse import coo_matrix
from scipy.sparse.csgraph import connected_components
from scipy.spatial import ConvexHull
from scipy.ndimage import maximum_filter

from glb_io import accessor, read


def author(folder):
    folder = Path(folder)
    plan = json.loads((folder / 'eyes-source-plan.json').read_text(encoding='utf8'))
    source = folder / plan.get('source', 'source-original.glb')
    document, binary = read(source)
    primitive = document['meshes'][0]['primitives'][0]
    positions = accessor(document, binary, primitive['attributes']['POSITION'])
    uv = accessor(document, binary, primitive['attributes']['TEXCOORD_0'])
    # glTF V is top-origin. Blender's importer flips it to bottom-origin.
    uv[:, 1] = 1 - uv[:, 1]
    triangles = accessor(document, binary, primitive['indices']).ravel().reshape(-1, 3)
    low, high = positions.min(0), positions.max(0)
    height = float(high[1] - low[1])
    center = (low + high) / 2
    scale = height * .45
    view = plan.get('face_preview_size', 1200)
    target_y = float(center[1] + height * .30)
    pixels = np.column_stack(((positions[:, 0] - center[0]) / scale * view + view / 2,
                             (target_y - positions[:, 1]) / scale * view + view / 2))
    centroids = positions[triangles].mean(1)
    pixel_centroids = pixels[triangles].mean(1)
    normals = np.cross(positions[triangles[:, 1]] - positions[triangles[:, 0]],
                       positions[triangles[:, 2]] - positions[triangles[:, 0]])
    normals /= np.maximum(np.linalg.norm(normals, axis=1, keepdims=True), 1e-12)
    # Front silhouette visibility uses actual source points; eye sockets on the
    # back of the head cannot pass simply because they share projected X/Y.
    bins = np.clip(np.floor(pixels).astype(int), 0, view - 1)
    front = np.full(view * view, -np.inf)
    np.maximum.at(front, bins[:, 1] * view + bins[:, 0], positions[:, 2])
    # Vertex-only bins are sparse: a back-of-head vertex can otherwise occupy a
    # pixel where the much nearer front triangle has no exact vertex. A small
    # depth dilation covers actual high-resolution front triangle interiors.
    front = maximum_filter(front.reshape(view, view), size=plan.get('frontDepthDilationPixels', 9)).ravel()
    out = {'version': 2, 'key': plan['key'], 'sourceGLBSHA256': hashlib.sha256(source.read_bytes()).hexdigest(),
           'source': source.name, 'selection': 'reviewed orthographic anatomical landmarks; real source triangle UVs', 'eyes': []}
    for eye in plan['eyes']:
        landmark = eye['centerPixels']
        radii = np.asarray(eye['radiiPixels'], dtype=float)
        delta = (pixel_centroids - landmark) / radii
        ellipse = np.einsum('ij,ij->i', delta, delta)
        eligible = (ellipse <= 1) & (normals[:, 2] > .03)
        eligible &= centroids[:, 2] >= eye.get('sourceZMin', -10)
        vertex_delta = (pixels[triangles] - landmark) / radii
        eligible &= np.max(np.sum(vertex_delta * vertex_delta, axis=2), axis=1) < 1.35
        # Reject a buried/back surface while allowing thin eye-orbit relief.
        candidates = np.flatnonzero(eligible)
        ids = triangles[candidates]
        references = front[bins[ids, 1] * view + bins[ids, 0]]
        visible = np.mean(positions[ids, 2] >= references - height * .004, axis=1) >= 2 / 3
        candidates = candidates[visible]
        pupil = float(eye.get('pupilRadiusFraction', 0))
        selected = candidates[ellipse[candidates] > pupil * pupil]
        black = candidates[ellipse[candidates] <= pupil * pupil]
        if len(selected) < 4:
            raise ValueError('Reviewed visible eye has too few real source triangles: ' + str(landmark))
        selected_vertices = np.unique(triangles[selected])
        local = {int(vertex): index for index, vertex in enumerate(selected_vertices)}
        edges = np.concatenate((triangles[selected][:, :2], triangles[selected][:, 1:], triangles[selected][:, ::2]))
        graph = coo_matrix((np.ones(len(edges)), ([local[int(v)] for v in edges[:, 0]],
                                                [local[int(v)] for v in edges[:, 1]])),
                           shape=(len(local), len(local))).tocsr()
        _, component = connected_components(graph, directed=False)
        labels, counts = np.unique(component, return_counts=True)
        biggest = selected_vertices[component == labels[counts.argmax()]]
        points = uv[biggest]
        hull = points[ConvexHull(points).vertices]
        # Profile boundary describes one real contiguous dominant UV island.
        # Emission itself uses every real selected triangle independently.
        boundary = [{'x': float(x), 'y': float(y)} for x, y in hull]
        mean = points.mean(0)
        center3d = positions[selected_vertices].mean(0)
        out['eyes'].append({'uv': {'x': float(mean[0]), 'y': float(mean[1])},
                           'materialSlot': 0, 'sourceMesh': plan['key'] + '_LOD0',
                           'uvBoundarySamples': boundary,
                           'rasterTriangles': [[{'x': float(u), 'y': float(v)} for u, v in uv[triangle]] for triangle in triangles[selected]],
                           'blackTriangles': [[{'x': float(u), 'y': float(v)} for u, v in uv[triangle]] for triangle in triangles[black]],
                           'sourceCenterGLTF': center3d.tolist(),
                           'sourceRadius': float(np.max(np.linalg.norm(positions[selected_vertices] - center3d, axis=1))),
                           'sourceTriangleCount': int(len(selected)), 'sourceUVIslands': int(len(labels)),
                           'sourceTriangleIndices': selected.tolist(),
                           'reviewedPixelLandmark': landmark, 'reviewedRadiiPixels': radii.tolist(),
                           'pupilExcluded': pupil > 0})
    (folder / 'eyes-authoring.json').write_text(json.dumps(out, indent=2), encoding='utf8')
    return {'key': out['key'], 'eyes': [{'triangles': eye['sourceTriangleCount'],
                                        'uvIslands': eye['sourceUVIslands'],
                                        'center': eye['sourceCenterGLTF']} for eye in out['eyes']]}


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('folder', type=Path)
    print(json.dumps(author(parser.parse_args().folder), indent=2))
