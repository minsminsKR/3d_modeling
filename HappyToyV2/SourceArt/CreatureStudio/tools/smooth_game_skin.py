"""Regularize source-transferred Uncat body skin on the connected game surface."""
import numpy as np

def smooth(lod,source_positions):
    count=len(lod.data.vertices);groups=len(lod.vertex_groups)
    dense=np.zeros((count,groups),dtype='f4')
    for v in lod.data.vertices:
        for g in v.groups:dense[v.index,g.group]=g.weight
    original=dense.copy()
    edges=np.empty(len(lod.data.edges)*2,dtype='i4');lod.data.edges.foreach_get('vertices',edges);edges=edges.reshape(-1,2)
    rows=np.r_[edges[:,0],edges[:,1]];cols=np.r_[edges[:,1],edges[:,0]]
    degree=np.maximum(np.bincount(rows,minlength=count),1)[:,None]
    p=source_positions
    pin=(p[:,1]>.28)|((p[:,2]>.17)&(p[:,1]>.205)&(abs(p[:,0])<.16))
    # Preserve distal fingers/feet; only their attachment transitions diffuse.
    pin|=(abs(p[:,0])>.275)&(p[:,1]<-.05)
    pin|=(p[:,1]<-.44)
    for _ in range(24):
        total=np.zeros_like(dense);np.add.at(total,rows,dense[cols])
        dense=.30*dense+.70*total/degree;dense[pin]=original[pin]
    dense/=np.maximum(dense.sum(1,keepdims=True),1e-8)
    for group in lod.vertex_groups:group.remove(range(count))
    batches={}
    for index,weights in enumerate(dense):
        chosen=np.argsort(weights)[-4:];values=weights[chosen];values/=values.sum()
        bins=np.round(values*1024).astype('i4')
        for g,value in zip(chosen,bins):
            if value:batches.setdefault((int(g),int(value)),[]).append(index)
    for (g,value),ids in batches.items():lod.vertex_groups[g].add(ids,value/1024,'REPLACE')
    return dict(iterations=24,method='connected game-surface weight diffusion with preserved face/fingers/feet',pinnedVertices=int(pin.sum()))