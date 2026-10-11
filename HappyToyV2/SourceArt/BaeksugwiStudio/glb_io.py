"""Small lossless GLB reader for the supplied TRELLIS mesh (Python 3.11)."""
import json
import struct
from pathlib import Path
import numpy as np

DT = {5120:'i1',5121:'u1',5122:'<i2',5123:'<u2',5125:'<u4',5126:'<f4'}
NC = {'SCALAR':1,'VEC2':2,'VEC3':3,'VEC4':4,'MAT4':16}

def read(path):
    data = Path(path).read_bytes()
    magic, version, size = struct.unpack_from('<III', data)
    if magic != 0x46546C67 or version != 2 or size != len(data):
        raise ValueError('Invalid GLB header')
    offset = 12
    document = binary = None
    while offset < len(data):
        length, kind = struct.unpack_from('<II',data,offset)
        offset += 8
        chunk = data[offset:offset+length]
        offset += length
        if kind == 0x4E4F534A: document=json.loads(chunk)
        elif kind == 0x004E4942: binary=chunk
    return document,binary

def accessor(document,binary,index):
    a=document['accessors'][index]
    if 'sparse' in a: raise ValueError('Sparse accessor unsupported')
    view=document['bufferViews'][a['bufferView']]
    dtype=np.dtype(DT[a['componentType']]);width=NC[a['type']]
    offset=view.get('byteOffset',0)+a.get('byteOffset',0)
    value=np.ndarray((a['count'],width),dtype=dtype,buffer=binary,offset=offset,
                     strides=(view.get('byteStride',dtype.itemsize*width),dtype.itemsize)).copy()
    if a.get('normalized',False) and dtype.kind in 'iu':
        value=value.astype(np.float32)/np.iinfo(dtype).max
        if dtype.kind=='i': value=np.maximum(value,-1)
    return value

def write(document,binary,path):
    document['buffers']=[{'byteLength':len(binary)}]
    text=json.dumps(document,separators=(',',':'),ensure_ascii=True).encode()
    text+=b' '*((-len(text))%4)
    binary+=b'\0'*((-len(binary))%4)
    size=12+8+len(text)+8+len(binary)
    Path(path).write_bytes(struct.pack('<III',0x46546C67,2,size)
       +struct.pack('<II',len(text),0x4E4F534A)+text
       +struct.pack('<II',len(binary),0x004E4942)+binary)
