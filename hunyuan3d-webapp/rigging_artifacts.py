"""Inspect real GLB skinning and build a separate diagnostic joint animation."""
from pathlib import Path
import io
import json
import math
import struct

import numpy as np
from PIL import Image


def read_glb(path):
    raw = Path(path).read_bytes()
    magic, version, length = struct.unpack_from('<4sII', raw)
    if magic != b'glTF' or version != 2 or length != len(raw):
        raise ValueError('Invalid GLB container')
    doc = None
    binary = b''
    offset = 12
    while offset < len(raw):
        size, kind = struct.unpack_from('<II', raw, offset)
        chunk = raw[offset + 8:offset + 8 + size]
        if kind == 0x4E4F534A: doc = json.loads(chunk)
        if kind == 0x004E4942: binary = chunk
        offset += size + 8
    if doc is None: raise ValueError('GLB has no scene description')
    return doc, binary


def accessor(doc, binary, index):
    item = doc['accessors'][index]
    view = doc['bufferViews'][item['bufferView']]
    dtype = {5120:'i1',5121:'u1',5122:'<i2',5123:'<u2',5125:'<u4',5126:'<f4'}[item['componentType']]
    width = {'SCALAR':1,'VEC2':2,'VEC3':3,'VEC4':4,'MAT4':16}[item['type']]
    dt = np.dtype(dtype)
    values = np.ndarray((item['count'], width), dtype=dt, buffer=binary,
        offset=view.get('byteOffset',0)+item.get('byteOffset',0),
        strides=(view.get('byteStride',dt.itemsize*width),dt.itemsize))
    if item.get('normalized') and item['componentType'] != 5126:
        values = values.astype(np.float32) / np.iinfo(dt).max
    return values


def inspect_glb(path, require_skin=True):
    doc, binary = read_glb(path)
    skins = doc.get('skins',[])
    joints = {j for skin in skins for j in skin.get('joints',[])}
    vertices = faces = skinned_vertices = 0
    max_weight_error = 0.0
    for node in doc.get('nodes',[]):
        if 'mesh' not in node: continue
        for primitive in doc['meshes'][node['mesh']]['primitives']:
            attrs = primitive['attributes']
            count = doc['accessors'][attrs['POSITION']]['count']
            vertices += count
            faces += doc['accessors'][primitive['indices']]['count']//3 if 'indices' in primitive else count//3
            if 'skin' in node:
                if not {'JOINTS_0','WEIGHTS_0'} <= attrs.keys(): raise ValueError('Skin attributes missing')
                weights = accessor(doc,binary,attrs['WEIGHTS_0'])
                indices = accessor(doc,binary,attrs['JOINTS_0'])
                if not np.isfinite(weights).all() or weights.min() < 0: raise ValueError('Invalid skin weights')
                error = float(np.max(np.abs(weights.sum(axis=1)-1)))
                max_weight_error = max(error,max_weight_error)
                if error > .015: raise ValueError('Skin weights are not normalized')
                if indices.max() >= len(skins[node['skin']]['joints']): raise ValueError('Invalid joint index')
                skinned_vertices += count
    if require_skin and (len(joints)<2 or skinned_vertices==0): raise ValueError('Result has no usable skeleton and skinning')
    textures = []
    for item in doc.get('images',[]):
        if 'bufferView' not in item: continue
        view = doc['bufferViews'][item['bufferView']]
        start = view.get('byteOffset',0)
        with Image.open(io.BytesIO(binary[start:start+view['byteLength']])) as image:
            textures.append(list(image.size))
    return {'bone_count':len(joints),'skin_count':len(skins),'vertices':vertices,'faces':faces,
            'skinned_vertices':skinned_vertices,'max_weight_error':max_weight_error,
            'texture_sizes':textures,'bytes':Path(path).stat().st_size,
            'animations':[a.get('name','') for a in doc.get('animations',[])]}


def write_joint_preview(source, output):
    doc, raw = read_glb(source)
    binary = bytearray(raw)
    def add(values, kind, count):
        binary.extend(b'\0' * (-len(binary)%4))
        start = len(binary)
        packed = struct.pack('<'+'f'*len(values),*values)
        binary.extend(packed)
        views = doc.setdefault('bufferViews',[])
        views.append({'buffer':0,'byteOffset':start,'byteLength':len(packed)})
        items = doc.setdefault('accessors',[])
        item = {'bufferView':len(views)-1,'componentType':5126,'count':count,'type':kind}
        if kind=='SCALAR': item.update({'min':[min(values)],'max':[max(values)]})
        items.append(item)
        return len(items)-1
    times = [0.,.6,1.2,1.8,2.4]
    time_id = add(times,'SCALAR',len(times))
    animation = {'name':'JointCheck','samplers':[],'channels':[]}
    joints = list(dict.fromkeys(j for skin in doc.get('skins',[]) for j in skin['joints']))
    children = {child for j in joints for child in doc['nodes'][j].get('children',[]) if child in joints}
    selected = [j for j in joints if j in children][:12]
    for n, joint in enumerate(selected):
        q = doc['nodes'][joint].get('rotation',[0.,0.,0.,1.])
        rotations = []
        for angle in [0.,.10*(-1 if n%2 else 1),0.,-.10*(-1 if n%2 else 1),0.]:
            s,c = math.sin(angle/2),math.cos(angle/2)
            x,y,z,w = q
            rotations.extend([x*c+w*s,y*c+z*s,z*c-y*s,w*c-x*s])
        output_id = add(rotations,'VEC4',len(times))
        animation['samplers'].append({'input':time_id,'output':output_id,'interpolation':'LINEAR'})
        animation['channels'].append({'sampler':len(animation['samplers'])-1,'target':{'node':joint,'path':'rotation'}})
    doc['animations'] = [animation]
    doc['buffers'][0]['byteLength'] = len(binary)
    encoded = json.dumps(doc,separators=(',',':')).encode()
    encoded += b' ' * (-len(encoded)%4)
    binary.extend(b'\0' * (-len(binary)%4))
    total = 12+8+len(encoded)+8+len(binary)
    Path(output).write_bytes(struct.pack('<4sII',b'glTF',2,total)+struct.pack('<II',len(encoded),0x4E4F534A)+encoded+struct.pack('<II',len(binary),0x004E4942)+binary)
    return len(selected)
