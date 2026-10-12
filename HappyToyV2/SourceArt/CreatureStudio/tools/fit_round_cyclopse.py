"""Reviewed compact standing skeleton for the user's round single-eye design."""
import json,hashlib
from pathlib import Path
import numpy as np
from glb_io import read,accessor

folder=Path(__file__).resolve().parent.parent/'Cyclopse'
source=folder/'source-original.glb';doc,b=read(source)
v=accessor(doc,b,doc['meshes'][0]['primitives'][0]['attributes']['POSITION'])
# Recorded anatomical positions in this generated canonical Y-up source.
j={'Hips':(0,-.53,-.03),'Spine':(0,-.24,0),'Spine1':(0,.04,.02),
   'Spine2':(0,.29,.02),'Neck':(0,.43,.035),'Head':(0,.54,.10)}
for side,sign in [('Left',1),('Right',-1)]:
    for suffix,p in {'Shoulder':(.32,.18,.02),'Arm':(.65,.16,.015),
        'ForeArm':(.79,-.12,.02),'Hand':(.885,-.385,.06),
        'UpLeg':(.29,-.55,-.02),'Leg':(.305,-.72,.005),
        'Foot':(.32,-.89,.05),'ToeBase':(.325,-.958,.245)}.items():
        j[side+suffix]=(p[0]*sign,p[1],p[2])
bones=[]
def add(name,parent,end):
    head=np.array(j[name]);tail=np.array(j[end]) if isinstance(end,str) else head+end
    bones.append(dict(name='mixamorig:'+name,parent='mixamorig:'+parent if parent else None,
                      head=head.tolist(),tail=tail.tolist(),semantic=name))
for name,parent,end in [('Hips',None,'Spine'),('Spine','Hips','Spine1'),('Spine1','Spine','Spine2'),
    ('Spine2','Spine1','Neck'),('Neck','Spine2','Head'),('Head','Neck',(0,.16,0))]:add(name,parent,end)
for s in ['Left','Right']:
    for suffix,parent,end in [('Shoulder','Spine2',s+'Arm'),('Arm',s+'Shoulder',s+'ForeArm'),
        ('ForeArm',s+'Arm',s+'Hand'),('Hand',s+'ForeArm',(0,-.14,.015)),
        ('UpLeg','Hips',s+'Leg'),('Leg',s+'UpLeg',s+'Foot'),
        ('Foot',s+'Leg',s+'ToeBase'),('ToeBase',s+'Foot',(0,0,.035))]:add(s+suffix,parent,end)
plan=dict(version=2,creature='Cyclopse',coordinateSystem='canonical source glTF Y-up/front +Z',
    source=source.name,sourceSHA256=hashlib.sha256(source.read_bytes()).hexdigest(),
    sourceBounds=[v.min(0).tolist(),v.max(0).tolist()],target_height_m=1.85,bones=bones,
    sourceAIUsedForSkin=False,skinMethod='reviewed compact anatomy and geodesic source-surface skin',
    game_triangle_budget=25000,requires_eye_surgery=False,
    headLock=dict(aboveY=.29,frontZ=.49,frontMinY=-.025,halfWidth=.32),
    bodyCoreVolumes=[dict(yMin=-.55,yMax=.29,halfWidth=.63,zMin=-.7,zMax=.65)],
    rigidSmallSurfaceComponentsMax=64,
    animation_profile_overrides=dict(patrol=dict(duration=1.08,pitch=.035,stride=.32,lift=.055,head_pitch=0),
        chase=dict(duration=.56,pitch=.085,stride=.54,lift=.10,head_pitch=0,bob=.012,sway=.018)),
    manualReview='Generated front/side anatomy: round rigid face, short separate upright legs, dangling arms; AI source skeleton axes rejected. Validate real posed skin and contacts.')
(folder/'body-rig-plan.json').write_text(json.dumps(plan,indent=2),encoding='utf8')