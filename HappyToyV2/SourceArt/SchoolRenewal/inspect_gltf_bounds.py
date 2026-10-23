"""Read generated GLB scene/accessor bounds without loading dense Blender data.

Standard glTF Y-up -> Blender Z-up basis is applied to the same preserved source.
This is an inexpensive preflight; the real Blender export still measures its mesh.
"""
from pathlib import Path
import argparse
import itertools
import json
import struct
import numpy as np

SOURCE=Path(__file__).resolve().parent

def inspect(key):
    path=SOURCE/key/'original.glb'
    with path.open('rb') as stream:
        magic,version,total=struct.unpack('<4sII',stream.read(12))
        if magic!=b'glTF' or version!=2:raise ValueError('Expected hydrated glTF2 GLB')
        size,kind=struct.unpack('<II',stream.read(8))
        if kind!=0x4E4F534A:raise ValueError('Expected GLB JSON chunk')
        gltf=json.loads(stream.read(size))
    basis=np.array([[1,0,0,0],[0,0,-1,0],[0,1,0,0],[0,0,0,1]],dtype=float)
    points=[];primitives=[]
    def matrix(node):
        if 'matrix' in node:return np.array(node['matrix'],dtype=float).reshape((4,4),order='F')
        x,y,z,w=node.get('rotation',[0,0,0,1])
        r=np.array([[1-2*(y*y+z*z),2*(x*y-z*w),2*(x*z+y*w),0],
                    [2*(x*y+z*w),1-2*(x*x+z*z),2*(y*z-x*w),0],
                    [2*(x*z-y*w),2*(y*z+x*w),1-2*(x*x+y*y),0],[0,0,0,1]],dtype=float)
        r[:3,:3]=r[:3,:3]@np.diag(node.get('scale',[1,1,1]));r[:3,3]=node.get('translation',[0,0,0])
        return r
    def visit(index,parent,ancestors):
        if index in ancestors:raise ValueError('Cyclic GLB node graph')
        node=gltf['nodes'][index];transform=parent@matrix(node)
        if 'mesh' in node:
            for primitive in gltf['meshes'][node['mesh']]['primitives']:
                position=gltf['accessors'][primitive['attributes']['POSITION']]
                if 'min' not in position or 'max' not in position:raise ValueError('Source accessor lacks bounds')
                for corner in itertools.product((0,1),repeat=3):
                    value=np.array([(position['max'] if corner[a] else position['min'])[a] for a in range(3)]+[1])
                    points.append((basis@transform@value)[:3])
                indices=gltf['accessors'][primitive['indices']]['count'] if 'indices' in primitive else position['count']
                primitives.append({'vertices':position['count'],'triangles':indices//3,'mode':primitive.get('mode',4)})
        for child in node.get('children',[]):visit(child,transform,ancestors|{index})
    scene=gltf['scenes'][gltf.get('scene',0)]
    for node in scene['nodes']:visit(node,np.eye(4),set())
    values=np.array(points);low=values.min(axis=0);high=values.max(axis=0)
    report={'key':key,'source':'original.glb','method':'GLB accessor scene bounds with standard glTF-to-Blender basis',
            'blenderBoundsMin':low.tolist(),'blenderBoundsMax':high.tolist(),'blenderSize':(high-low).tolist(),
            'primitives':primitives,'scope':'Metadata-only preflight; final Blender export records actual mesh bounds'}
    (SOURCE/key/'gltf-measurements.json').write_text(json.dumps(report,indent=2)+'\n',encoding='utf-8')
    print('GLTF_BOUNDS_PASS',json.dumps(report))

if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('key');inspect(parser.parse_args().key)
