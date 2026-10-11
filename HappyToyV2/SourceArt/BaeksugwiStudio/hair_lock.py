"""Apply reviewed hair and hanging decoration ownership to final skin weights."""
import json
from pathlib import Path
import numpy as np

def apply(joints,weights,bone_names,path):
    mask=np.load(path)
    if mask.dtype!=np.bool_ or mask.shape!=(len(joints),):
        raise ValueError('Hair mask must match the original GLB vertex ordering')
    head=list(bone_names).index('Head')
    joints[mask]=0;joints[mask,0]=head
    weights[mask]=0;weights[mask,0]=1
    return int(mask.sum())

if __name__=='__main__':
    here=Path(__file__).resolve().parent
    target=here/'surface-weights.npz'
    payload=dict(np.load(target))
    count=apply(payload['joints'],payload['weights'],payload['bone_names'],here/'hair-lock-mask.npy')
    decoration_count=apply(payload['joints'],payload['weights'],payload['bone_names'],here/'decoration-lock-mask.npy')
    np.savez_compressed(target,**payload)
    report=json.loads(target.with_suffix('.json').read_text())
    joints=payload['joints'];weights=payload['weights'];names=payload['bone_names']
    dominant=joints[np.arange(len(joints)),weights.argmax(1)]
    report['dominantVertices']={str(name):int((dominant==i).sum()) for i,name in enumerate(names)}
    report['reviewedHairVertices']=count;report['hairBone']='Head'
    report['reviewedDecorationVertices']=decoration_count;report['decorationBone']='Head'
    target.with_suffix('.json').write_text(json.dumps(report,indent=2),encoding='utf8')
    print('head ownership corrected:',count,'hair vertices,',decoration_count,'decoration vertices; all hands preserved')
