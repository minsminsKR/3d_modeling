"""Bake a generated GLB node basis into an editable canonical Y-up mesh.

Original GLB is saved separately. Embedded images/UV/indices are unchanged.
Only static, one-node/one-mesh inputs are accepted; never rewrite a skin.
"""
import argparse,json,struct,hashlib
from pathlib import Path
import numpy as np
from glb_io import read,accessor,node_matrices,DTYPES,COMPONENTS


def canonical(source,output):
    doc,binary=read(source);data=bytearray(binary)
    if doc.get('skins') or doc.get('animations'):raise ValueError('Static source required')
    matrices,_=node_matrices(doc)
    owners=[(i,n) for i,n in enumerate(doc['nodes']) if 'mesh' in n]
    if len(owners)!=1:raise ValueError('Review multi-node model explicitly')
    node_id,node=owners[0];transform=matrices[node_id]
    for primitive in doc['meshes'][node['mesh']]['primitives']:
        for name in ['POSITION','NORMAL']:
            if name not in primitive['attributes']:continue
            index=primitive['attributes'][name];item=doc['accessors'][index]
            values=accessor(doc,binary,index).astype(float)
            values=values@transform[:3,:3].T
            if name=='POSITION':values+=transform[:3,3];item['min']=values.min(0).tolist();item['max']=values.max(0).tolist()
            else:values/=np.maximum(np.linalg.norm(values,axis=1,keepdims=True),1e-12)
            view=doc['bufferViews'][item['bufferView']];offset=view.get('byteOffset',0)+item.get('byteOffset',0)
            target=np.ndarray(values.shape,dtype=DTYPES[item['componentType']],buffer=data,offset=offset,
                              strides=(view.get('byteStride',12),4));target[:]=values
    for key in ['matrix','rotation','translation','scale']:node.pop(key,None)
    body=json.dumps(doc,separators=(',',':')).encode();body+=b' '*((-len(body))%4)
    data+=b'\0'*((-len(data))%4)
    result=struct.pack('<III',0x46546c67,2,28+len(body)+len(data))+struct.pack('<II',len(body),0x4e4f534a)+body+struct.pack('<II',len(data),0x004e4942)+data
    output.write_bytes(result)
    output.with_name('canonical-source-report.json').write_text(json.dumps(dict(
        original=source.name,originalSHA256=hashlib.sha256(source.read_bytes()).hexdigest(),
        canonical=output.name,canonicalSHA256=hashlib.sha256(result).hexdigest(),
        bakedNodeMatrix=transform.tolist(),sourceImagesUVIndicesUnchanged=True),indent=2),encoding='utf8')


if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('source',type=Path);p.add_argument('output',type=Path)
    a=p.parse_args();canonical(a.source,a.output)