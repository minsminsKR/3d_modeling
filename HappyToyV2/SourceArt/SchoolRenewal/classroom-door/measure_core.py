"""Normal Python: measure unchanged GLB leaf planes separately from hardware.

The preserved service GLB has one identity node, standard glTF Y-up float32
position/normal accessors. Blender maps it to X,-Z,Y. Measurement changes no mesh.
"""
from pathlib import Path
import json,struct
import numpy as np

source=Path(__file__).resolve().parent
with (source/'original.glb').open('rb') as stream:
    magic,version,length=struct.unpack('<4sII',stream.read(12))
    if magic!=b'glTF' or version!=2:raise ValueError('Expected hydrated GLB2')
    chunks={}
    while head:=stream.read(8):
        size,kind=struct.unpack('<II',head);chunks[kind]=stream.read(size)
data=json.loads(chunks[0x4E4F534A]);binary=chunks[0x004E4942]
if data['nodes']!=[{'name':'geometry_0','mesh':0}]:raise ValueError('Unexpected non-identity service node')
attributes=data['meshes'][0]['primitives'][0]['attributes']
def array(key):
    accessor=data['accessors'][attributes[key]];view=data['bufferViews'][accessor['bufferView']]
    if accessor['componentType']!=5126 or accessor['type']!='VEC3':raise ValueError('Expected float32 vector')
    return np.ndarray((accessor['count'],3),dtype='<f4',buffer=binary,
                      offset=view.get('byteOffset',0)+accessor.get('byteOffset',0),strides=(view.get('byteStride',12),4))
position=array('POSITION');normal=array('NORMAL')
blender=position[:,[0,2,1]].copy();blender[:,1]*=-1
low=blender.min(axis=0);high=blender.max(axis=0)
factor=2.44/(high[2]-low[2]);centre=(low+high)*.5;centre[2]=low[2]
game=(blender-centre)*factor
planar=np.abs(normal[:,2])>.97
bins=np.arange(-.18,.182,.002)
counts,edges=np.histogram(game[planar,1],bins=bins)
centres=(edges[:-1]+edges[1:])*.5
planes=[]
for sign in (-1,1):
    valid=(centres*sign>.003)
    index=int(np.argmax(np.where(valid,counts,-1)))
    y=float(centres[index]);selection=planar&(np.abs(game[:,1]-y)<.003)
    points=game[selection]
    planes.append(dict(side=sign,dominantY=y,vertices=int(selection.sum()),
                       boundsMin=points.min(axis=0).tolist(),boundsMax=points.max(axis=0).tolist()))
envelope_min=np.min([p['boundsMin'] for p in planes],axis=0)
envelope_max=np.max([p['boundsMax'] for p in planes],axis=0)
report=dict(method='Dominant area-like vertex density of actual front/back normals; 2mm depth histogram, ±3mm selection',
            normalizedHeightMetres=2.44,wholeModelMin=game.min(axis=0).tolist(),wholeModelMax=game.max(axis=0).tolist(),
            dominantLeafPlanes=planes,leafPlaneEnvelopeMin=envelope_min.tolist(),leafPlaneEnvelopeMax=envelope_max.tolist(),
            leafPlaneWidthMetres=float(envelope_max[0]-envelope_min[0]),leafPlaneHeightMetres=float(envelope_max[2]-envelope_min[2]),
            approximateLeafThicknessMetres=float(planes[1]['dominantY']-planes[0]['dominantY']),
            note='Conservative planar-surface measurement, not a new fitted cuboid or geometry substitution; original GLB unchanged')
(source/'leaf-core-measurements.json').write_text(json.dumps(report,indent=2)+'\n',encoding='utf-8')
print('GENERATED_DOOR_LEAF_CORE_MEASUREMENTS',json.dumps(report),flush=True)
