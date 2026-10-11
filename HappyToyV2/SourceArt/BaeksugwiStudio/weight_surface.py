"""Rebind the supplied creature using surface paths, not humanoid predictions.

Python 3.11, numpy 1.26, scipy 1.14. UV splits share weights via geometric
welding. A coarse surface graph is only used for path distances; source mesh,
UVs, normals, triangles and textures are untouched.
"""
import argparse
import json
from pathlib import Path
import numpy as np
from scipy.spatial import cKDTree
from scipy.sparse import coo_matrix, diags
from scipy.sparse.csgraph import dijkstra
from glb_io import read, accessor

def bind(source, plan, output, voxel_size=.003):
    doc, binary = read(source)
    primitive=doc['meshes'][0]['primitives'][0]
    vertices=accessor(doc,binary,primitive['attributes']['POSITION']).astype(np.float64)
    faces=accessor(doc,binary,primitive['indices']).astype(np.int64).reshape(-1,3)
    keys,first,inverse=np.unique(np.round(vertices/voxel_size).astype(np.int32),
        axis=0,return_index=True,return_inverse=True)
    points=vertices[first]
    tree=cKDTree(points)
    triangles=inverse[faces]
    edges=np.concatenate([triangles[:,[0,1]],triangles[:,[1,2]],triangles[:,[2,0]]])
    edges=np.unique(np.sort(edges,axis=1),axis=0)
    edges=edges[edges[:,0]!=edges[:,1]]
    edge_lengths=np.linalg.norm(points[edges[:,0]]-points[edges[:,1]],axis=1)
    rows=np.concatenate([edges[:,0],edges[:,1]])
    cols=np.concatenate([edges[:,1],edges[:,0]])
    graph=coo_matrix((np.tile(edge_lengths,2),(rows,cols)),shape=(len(points),len(points))).tocsr()
    bones=plan['bones']
    distances=[]
    euclidean=[]
    for index,bone in enumerate(bones):
        head=np.array(bone['head']);tail=np.array(bone['tail'])
        length=np.linalg.norm(tail-head)
        samples=head[None]+np.linspace(.12,.9,max(3,int(length/.012)))[:,None]*(tail-head)
        _,seed=tree.query(samples)
        seed=np.unique(seed)
        distance=dijkstra(graph,directed=False,indices=seed,min_only=True)
        delta=tail-head
        t=np.clip((points-head)@delta/max(delta@delta,1e-12),0,1)
        direct=np.linalg.norm(points-(head+t[:,None]*delta),axis=1)
        # Disconnected bells, skin shells and fingers still receive a sensible
        # local influence. Connected surface paths dominate wherever available.
        distance=np.where(np.isfinite(distance),distance+.6*direct,5*direct+.15)
        distances.append(distance.astype(np.float32));euclidean.append(direct.astype(np.float32))
        print('surface bone',index,bone['name'],'graph vertices',len(points),flush=True)
    distances=np.array(distances).T
    direct=np.array(euclidean).T
    # Remove accidental cross-body arm influence, particularly near the mask.
    for i,bone in enumerate(bones):
        if bone.get('limb'):
            side=np.sign(bone['head'][0])
            if abs(bone['head'][0])>.035:
                wrong=points[:,0]*side<-.025
                distances[wrong,i]+=1
    selection=np.argpartition(distances,3,axis=1)[:,:4]
    chosen=np.take_along_axis(distances,selection,axis=1)
    weights=np.power(chosen+.010,-5)
    weights/=weights.sum(1,keepdims=True)
    dense=np.zeros((len(points),len(bones)),dtype=np.float32)
    np.put_along_axis(dense,selection,weights.astype(np.float32),axis=1)
    adjacency=coo_matrix((np.ones(len(rows)),(rows,cols)),shape=graph.shape).tocsr()
    normalizer=diags(1/np.maximum(np.asarray(adjacency.sum(1)).ravel(),1))@adjacency
    for _ in range(3):dense=.65*dense+.35*(normalizer@dense)
    name_index={b['name']:i for i,b in enumerate(bones)}
    # The torso's central skin/ribbons follow its spine. The rear arms emerge
    # from this surface; proximity to their shoulder cannot make the entire
    # back follow an independently swinging limb. Smooth spatial transitions
    # keep the shoulder attachment deformable at the edges.
    strength=np.clip((.125-abs(points[:,0]))/.035,0,1)*np.clip((points[:,1]-.10)/.075,0,1)
    strength*=np.clip((.28-points[:,2])/.035,0,1)
    body_ids=[name_index[n] for n in ('BodyRoot','SpineRear','SpineBack','SpineFront','Chest')]
    torso_direct=direct[:,body_ids]
    torso=np.power(torso_direct+.012,-4)
    torso/=torso.sum(1,keepdims=True)
    dense*=1-strength[:,None]
    dense[:,body_ids]+=strength[:,None]*torso
    # The porcelain face is rigid. Soft weighting across a hand chain must
    # never drag the face or mouth into an unrelated limb.
    mask=(abs(points[:,0])<.115)&(points[:,2]>.305)&(points[:,1]>-.065)&(points[:,1]<.29)
    dense[mask]=0;dense[mask,name_index['Head']]=1
    # Give distal hands one rigid influence; blend only around their wrist.
    for limb in plan['limbs']:
        wrist=np.array(limb['wrist_position'])
        tip=np.array(limb['tip'])
        direction=tip-wrist
        if np.linalg.norm(direction)<.008:continue
        t=((points-wrist)@direction)/(direction@direction)
        radius=np.linalg.norm(points-(wrist+np.clip(t,0,1.6)[:,None]*direction),axis=1)
        area=(t>.38)&(t<1.6)&(radius<limb.get('hand_radius',.042))
        # Keep a distal region owned by this limb; avoid a nearby crossing hand.
        wi=name_index[limb['wrist']]
        closest=direct.argmin(1)
        limb_ids=[name_index[limb[k]] for k in ('shoulder','elbow','wrist')]
        area &= np.isin(closest,limb_ids)
        dense[area]=0;dense[area,wi]=1
    selected=np.argpartition(-dense,3,axis=1)[:,:4]
    values=np.take_along_axis(dense,selected,axis=1)
    values/=np.maximum(values.sum(1,keepdims=True),1e-12)
    selected=selected[inverse].astype(np.uint16)
    values=values[inverse].astype(np.float32)
    exact_mask=(abs(vertices[:,0])<.115)&(vertices[:,2]>.305)&(vertices[:,1]>-.065)&(vertices[:,1]<.29)
    selected[exact_mask]=0;selected[exact_mask,0]=name_index['Head']
    values[exact_mask]=0;values[exact_mask,0]=1
    from hair_lock import apply
    head_counts={}
    for field,filename in (('hairMask','hair-lock-mask.npy'),('decorationMask','decoration-lock-mask.npy')):
        reviewed_mask=Path(plan.get(field,Path(__file__).with_name(filename)))
        head_counts[field]=apply(selected,values,[b['name'] for b in bones],reviewed_mask)
    np.savez_compressed(output,positions=vertices.astype(np.float32),joints=selected,weights=values,
                        bone_names=np.array([b['name'] for b in bones]))
    report={'vertices':len(vertices),'faces':len(faces),'surfaceGraphVertices':len(points),
      'bones':len(bones),'maxWeightSumError':float(abs(values.sum(1)-1).max()),
      'zeroWeightVertices':int((values.sum(1)<.999).sum()),'reviewedHeadOwnership':head_counts,
      'dominantVertices':{b['name']:int(((selected[np.arange(len(selected)),values.argmax(1)])==i).sum())
                         for i,b in enumerate(bones)}}
    Path(output).with_suffix('.json').write_text(json.dumps(report,indent=2),encoding='utf8')
    print(json.dumps(report,indent=2),flush=True)

if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('source');p.add_argument('plan');p.add_argument('output')
    args=p.parse_args();bind(args.source,json.loads(Path(args.plan).read_text(encoding='utf8')),args.output)
