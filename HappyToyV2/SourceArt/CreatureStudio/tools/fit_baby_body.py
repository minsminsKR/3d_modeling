"""Fit Baby limb centers from actual generated neutral-pose cross sections."""
import json,hashlib
from pathlib import Path
import numpy as np
from glb_io import read,accessor
folder=Path(__file__).resolve().parent.parent/'Baby';source=folder/'source-original.glb'
d,b=read(source);v=accessor(d,b,d['meshes'][0]['primitives'][0]['attributes']['POSITION'])
_,first=np.unique(np.round(v*1e5).astype('i4'),axis=0,return_index=True);points=v[first];fitting=[]
def station(name,side,y,xrange,zrange=(-.16,.16)):
    mask=(abs(points[:,1]-y)<.01)&(points[:,0]*side>xrange[0])&(points[:,0]*side<xrange[1])
    mask&=(points[:,2]>zrange[0])&(points[:,2]<zrange[1])
    if mask.sum()<16:raise ValueError('No actual source station '+name)
    value=points[mask].mean(0);fitting.append(dict(joint=name,sourcePosition=value.tolist(),sourceVertices=int(mask.sum()),yStation=y,xRange=xrange))
    return value
j={n:np.array(p) for n,p in {'Hips':(0,-.14,0),'Spine':(0,-.065,0),'Spine1':(0,-.005,0),
    'Spine2':(0,.07,0),'Neck':(0,.145,0),'Head':(-.012,.185,.012)}.items()}
for s,side in [('Left',1),('Right',-1)]:
    j[s+'Shoulder']=np.array([side*.085,.12,0])
    j[s+'Arm']=station(s+'Arm',side,.105,(.155,.245))
    j[s+'ForeArm']=station(s+'ForeArm',side,-.015,(.19,.295))
    j[s+'Hand']=station(s+'Hand',side,-.105,(.225,.325))
    j[s+'UpLeg']=np.array([side*.10,-.18,.005])
    j[s+'Leg']=station(s+'Leg',side,-.295,(.055,.155))
    j[s+'Foot']=station(s+'Foot',side,-.43,(.055,.18))
    toes=(points[:,0]*side>.045)&(points[:,0]*side<.2)&(points[:,1]<-.47)&(points[:,2]>.035)
    j[s+'ToeBase']=points[toes].mean(0)
bones=[]
def add(name,parent,end):
    head=j[name];tail=j[end] if isinstance(end,str) else head+end
    bones.append(dict(name='mixamorig:'+name,parent='mixamorig:'+parent if parent else None,head=head.tolist(),tail=tail.tolist(),semantic=name))
for n,p,e in [('Hips',None,'Spine'),('Spine','Hips','Spine1'),('Spine1','Spine','Spine2'),('Spine2','Spine1','Neck'),('Neck','Spine2','Head'),('Head','Neck',(0,.13,0))]:add(n,p,e)
for s in ['Left','Right']:
    for suffix,p,e in [('Shoulder','Spine2',s+'Arm'),('Arm',s+'Shoulder',s+'ForeArm'),('ForeArm',s+'Arm',s+'Hand'),
        ('Hand',s+'ForeArm',(0,-.08,.012)),('UpLeg','Hips',s+'Leg'),('Leg',s+'UpLeg',s+'Foot'),('Foot',s+'Leg',s+'ToeBase'),('ToeBase',s+'Foot',(0,0,.03))]:add(s+suffix,p,e)
plan=dict(version=2,creature='Baby',coordinateSystem='generated source glTF Y-up/front +Z',source=source.name,
    sourceSHA256=hashlib.sha256(source.read_bytes()).hexdigest(),sourceBounds=[v.min(0).tolist(),v.max(0).tolist()],target_height_m=1.48,
    bones=bones,fittedStations=fitting,sourceAIUsedForSkin=False,skinMethod='actual neutral anatomy fitted geodesic source surface skin',
    headLock=dict(aboveY=.125,frontZ=.08,frontMinY=.08,halfWidth=.14),
    bodyCoreVolumes=[dict(yMin=-.17,yMax=.12,halfWidth=.13,zMin=-.2,zMax=.17)],rigidSmallSurfaceComponentsMax=64,
    game_triangle_budget=30000,animation_profile_overrides={},manualReview='Actual source front/side and limb stations; AI shoulders/wrists were inside torso/forearms. Refit both limbs before crawling skin audit.')
(folder/'body-rig-plan.json').write_text(json.dumps(plan,indent=2),encoding='utf8');print(json.dumps(fitting),flush=True)