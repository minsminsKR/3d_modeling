"""Small read-only GLB inspector; Python 3.11 + numpy 1.26.

Only embedded GLB buffers are accepted. No network fetch or external files.
"""
import json
import struct
from pathlib import Path
import numpy as np

DTYPES = {5120: 'i1', 5121: 'u1', 5122: '<i2', 5123: '<u2', 5125: '<u4', 5126: '<f4'}
COMPONENTS = {'SCALAR': 1, 'VEC2': 2, 'VEC3': 3, 'VEC4': 4, 'MAT2': 4, 'MAT3': 9, 'MAT4': 16}


def read(path):
    raw = Path(path).read_bytes()
    magic, version, length = struct.unpack_from('<III', raw)
    if magic != 0x46546C67 or version != 2 or length != len(raw):
        raise ValueError('Expected a complete GLB 2.0 file')
    cursor, document, binary = 12, None, None
    while cursor < length:
        size, kind = struct.unpack_from('<II', raw, cursor)
        cursor += 8
        chunk = raw[cursor:cursor + size]
        if kind == 0x4E4F534A:
            document = json.loads(chunk)
        elif kind == 0x004E4942:
            binary = chunk
        cursor += size
    if document is None or binary is None:
        raise ValueError('GLB JSON/BIN chunk is missing')
    if any(buffer.get('uri') for buffer in document.get('buffers', [])):
        raise ValueError('External buffers are not accepted')
    return document, binary


def accessor(document, binary, index, normalized=True):
    item = document['accessors'][index]
    if 'sparse' in item:
        raise ValueError('Sparse accessors require an explicit conversion before authoring')
    view = document['bufferViews'][item['bufferView']]
    dtype = np.dtype(DTYPES[item['componentType']])
    width = COMPONENTS[item['type']]
    offset = view.get('byteOffset', 0) + item.get('byteOffset', 0)
    values = np.ndarray((item['count'], width), dtype=dtype, buffer=binary,
                        offset=offset, strides=(view.get('byteStride', dtype.itemsize * width), dtype.itemsize)).copy()
    if normalized and item.get('normalized'):
        limits = np.iinfo(dtype)
        values = values.astype(np.float32) / limits.max
        if limits.min < 0:
            values = np.maximum(values, -1)
    return values


def node_matrices(document):
    nodes = document.get('nodes', [])
    parents = {child: index for index, node in enumerate(nodes) for child in node.get('children', [])}
    result = {}

    def visit(index):
        if index in result:
            return result[index]
        node = nodes[index]
        if 'matrix' in node:
            matrix = np.asarray(node['matrix'], dtype=float).reshape(4, 4).T
        else:
            x, y, z, w = node.get('rotation', [0, 0, 0, 1])
            rotation = np.array([[1-2*(y*y+z*z), 2*(x*y-z*w), 2*(x*z+y*w)],
                                 [2*(x*y+z*w), 1-2*(x*x+z*z), 2*(y*z-x*w)],
                                 [2*(x*z-y*w), 2*(y*z+x*w), 1-2*(x*x+y*y)]])
            matrix = np.eye(4)
            matrix[:3, :3] = rotation @ np.diag(node.get('scale', [1, 1, 1]))
            matrix[:3, 3] = node.get('translation', [0, 0, 0])
        result[index] = visit(parents[index]) @ matrix if index in parents else matrix
        return result[index]

    for index in range(len(nodes)):
        visit(index)
    return result, parents
