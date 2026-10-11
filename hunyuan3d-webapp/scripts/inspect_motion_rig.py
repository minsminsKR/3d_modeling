import json
import sys
from pathlib import Path
import numpy as np
sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from rigging_artifacts import read_glb, accessor
path = Path(sys.argv[1])
doc, binary = read_glb(path)
print(json.dumps({'nodes':doc['nodes'],'skins':doc.get('skins'),'scene':doc.get('scenes')}, indent=2))
for node in doc['nodes']:
    if 'skin' not in node: continue
    skin = doc['skins'][node['skin']]
    weights_sum = np.zeros(len(skin['joints']))
    centers = np.zeros((len(weights_sum),3))
    minimum = np.full(3, np.inf)
    maximum = -minimum
    for prim in doc['meshes'][node['mesh']]['primitives']:
        a = prim['attributes']
        p = accessor(doc,binary,a['POSITION'])
        w = accessor(doc,binary,a['WEIGHTS_0'])
        j = accessor(doc,binary,a['JOINTS_0'])
        minimum = np.minimum(minimum,p.min(axis=0))
        maximum = np.maximum(maximum,p.max(axis=0))
        for n in range(len(weights_sum)):
            per_vertex = np.where(j==n,w,0).sum(axis=1)
            weights_sum[n] += per_vertex.sum()
            centers[n] += (p*per_vertex[:,None]).sum(axis=0)
    print(json.dumps({'bounds':[minimum.tolist(),maximum.tolist()], 'influence':[
        {'node':joint,'weight':round(float(weights_sum[n]),2),'centroid':(centers[n]/max(weights_sum[n],1)).tolist()}
        for n,joint in enumerate(skin['joints'])]},indent=2))
