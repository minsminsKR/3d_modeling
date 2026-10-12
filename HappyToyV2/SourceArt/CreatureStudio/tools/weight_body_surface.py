"""Rebind a reviewed NEW 22-bone source through its real surface connectivity."""
import argparse
import json
from pathlib import Path
import numpy as np
from scipy.spatial import cKDTree
from scipy.sparse import coo_matrix,diags
from scipy.sparse.csgraph import dijkstra
from scipy.sparse.csgraph import connected_components
from glb_io import read,accessor


def bind(source,plan,output,voxel=.003):
    document,binary=read(source);primitive=document['meshes'][0]['primitives'][0]
    vertices=accessor(document,binary,primitive['attributes']['POSITION']).astype(float)
    faces=accessor(document,binary,primitive['indices'],normalized=False).reshape(-1,3)
    _,first,inverse=np.unique(np.round(vertices/voxel).astype(np.int32),axis=0,return_index=True,return_inverse=True)
    points=vertices[first];tree=cKDTree(points);triangles=inverse[faces]
    edges=np.unique(np.sort(np.concatenate([triangles[:,[0,1]],triangles[:,[1,2]],triangles[:,[2,0]]]),axis=1),axis=0)
    edges=edges[edges[:,0]!=edges[:,1]];length=np.linalg.norm(points[edges[:,0]]-points[edges[:,1]],axis=1)
    rows=np.r_[edges[:,0],edges[:,1]];cols=np.r_[edges[:,1],edges[:,0]]
    graph=coo_matrix((np.r_[length,length],(rows,cols)),shape=(len(points),len(points))).tocsr()
    distances=[];directs=[];names=[bone['name']for bone in plan['bones']]
    for index,bone in enumerate(plan['bones']):
        head,tail=np.array(bone['head']),np.array(bone['tail']);delta=tail-head
        samples=head+np.linspace(.12,.92,max(4,int(np.linalg.norm(delta)/.009)))[:,None]*delta
        _,seeds=tree.query(samples);distance=dijkstra(graph,directed=False,indices=np.unique(seeds),min_only=True)
        t=np.clip((points-head)@delta/max(delta@delta,1e-12),0,1);direct=np.linalg.norm(points-(head+t[:,None]*delta),axis=1)
        distance=np.where(np.isfinite(distance),distance+.75*direct,5*direct+.15)
        if bone['semantic'].startswith(('Left','Right')):
            side=1 if bone['semantic'].startswith('Left')else-1
            distance[points[:,0]*side<-.015]+=1
        distances.append(distance);directs.append(direct)
        print('surface',index,bone['name'],len(points),flush=True)
    distance=np.array(distances).T;direct=np.array(directs).T
    selected=np.argpartition(distance,3,axis=1)[:,:4];picked=np.take_along_axis(distance,selected,axis=1)
    weights=(picked+.008)**-5;weights/=weights.sum(1,keepdims=True)
    dense=np.zeros((len(points),len(names)),dtype=np.float32);np.put_along_axis(dense,selected,weights.astype('f4'),axis=1)
    adjacency=coo_matrix((np.ones(len(rows)),(rows,cols)),shape=graph.shape).tocsr()
    normalize=diags(1/np.maximum(np.asarray(adjacency.sum(1)).ravel(),1))@adjacency
    for _ in range(2):dense=.75*dense+.25*(normalize@dense)
    name_index={name:i for i,name in enumerate(names)};lock=plan['headLock']
    # A broad furry torso is close to lateral arm capsules. Surface-distance
    # seeds can otherwise let the arm own disconnected back fur. Explicit
    # reviewed body volumes use smooth anatomical spine-height weights, which
    # are consistent across the whole cross section and its small fur islands.
    body_mask=np.zeros(len(points),dtype=bool)
    for roi in plan.get('bodyCoreVolumes',[]):
        selected=(points[:,1]>=roi['yMin'])&(points[:,1]<=roi['yMax'])&(abs(points[:,0])<roi['halfWidth'])
        selected&=(points[:,2]>=roi.get('zMin',-10))&(points[:,2]<=roi.get('zMax',10))
        body_mask|=selected
    if body_mask.any():
        body_names=['mixamorig:Hips','mixamorig:Spine','mixamorig:Spine1','mixamorig:Spine2']
        ys=np.array([next(b['head'][1] for b in plan['bones'] if b['name']==name)for name in body_names])
        lower=np.clip(np.searchsorted(ys,points[body_mask,1],side='right')-1,0,len(ys)-2)
        factor=np.clip((points[body_mask,1]-ys[lower])/(ys[lower+1]-ys[lower]),0,1)
        dense[body_mask]=0
        rows_body=np.flatnonzero(body_mask)
        dense[rows_body,[name_index[body_names[i]]for i in lower]]=1-factor
        dense[rows_body,[name_index[body_names[i+1]]for i in lower]]=factor
    head_mask=(points[:,1]>lock['aboveY'])|((points[:,2]>lock['frontZ'])&(points[:,1]>lock['frontMinY'])&(abs(points[:,0])<lock['halfWidth']))
    if plan.get('headBlendWidth',0):
        width=float(plan['headBlendWidth'])
        def smooth(value):
            value=np.clip(value,0,1);return value*value*(3-2*value)
        upper=smooth((points[:,1]-lock['aboveY']+width)/(2*width))
        front=smooth((points[:,2]-lock['frontZ'])/width)*smooth((points[:,1]-lock['frontMinY'])/width)
        front*=smooth((lock['halfWidth']-abs(points[:,0]))/width)
        influence=np.maximum(upper,front)
        dense*=1-influence[:,None];dense[:,name_index['mixamorig:Head']]+=influence
        head_mask=influence>.999
    else:
        dense[head_mask]=0;dense[head_mask,name_index['mixamorig:Head']]=1
    for side in ('Left','Right'):
        hand=next(b for b in plan['bones']if b['semantic']==side+'Hand');head,tail=np.array(hand['head']),np.array(hand['tail'])
        direction=tail-head;side_sign=1 if side=='Left'else-1
        # Keep the real entire palm/fingers rigid; only wrist transition blends.
        region=(points[:,0]*side_sign>abs(head[0])-.024)&(points[:,1]<head[1]+.008)
        region&=(np.linalg.norm(points-head,axis=1)<.17)&(~head_mask)
        dense[region]=0;dense[region,name_index[hand['name']]]=1
    # Detached little fur leaves need one consistent local ownership mixture,
    # rather than a different shoulder/torso decision at every sharp corner.
    component_count,components=connected_components(graph,directed=False)
    counts=np.bincount(components,minlength=component_count)
    small_labels=np.flatnonzero((counts>1)&(counts<=plan.get('rigidSmallSurfaceComponentsMax',0)))
    for label in small_labels:
        ids=np.flatnonzero(components==label)
        mixture=dense[ids].mean(0)
        if head_mask[ids].any():
            mixture[:]=0;mixture[name_index['mixamorig:Head']]=1
        elif body_mask[ids].any():
            mixture=dense[ids[body_mask[ids]]].mean(0)
        dense[ids]=mixture
    selected=np.argpartition(-dense,3,axis=1)[:,:4];weights=np.take_along_axis(dense,selected,axis=1)
    weights/=weights.sum(1,keepdims=True);selected=selected[inverse].astype('u2');weights=weights[inverse].astype('f4')
    np.savez_compressed(output,positions=vertices.astype('f4'),joints=selected,weights=weights,bone_names=np.array(names))
    dominant=selected[np.arange(len(selected)),weights.argmax(1)]
    report={'source':Path(source).name,'vertices':len(vertices),'bones':len(names),'surfaceGraphVertices':len(points),
            'sourceAIWeightsReused':False,'maxWeightSumError':float(abs(weights.sum(1)-1).max()),
            'zeroWeightVertices':int((weights.sum(1)<.99).sum()),
            'explicitBodyCoreGraphVertices':int(body_mask.sum()),'rigidHeadGraphVertices':int(head_mask.sum()),
            'consistentSmallSurfaceComponents':int(len(small_labels)),
            'dominantVertices':{name:int((dominant==index).sum())for index,name in enumerate(names)}}
    Path(output).with_suffix('.json').write_text(json.dumps(report,indent=2),encoding='utf8');print(json.dumps(report),flush=True)


if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('source',type=Path);parser.add_argument('plan',type=Path);parser.add_argument('output',type=Path);args=parser.parse_args()
    bind(args.source,json.loads(args.plan.read_text()),args.output)
