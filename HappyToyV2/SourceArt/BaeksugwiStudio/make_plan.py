"""Convert reviewed asymmetric limb fits into an editable creature rig plan."""
import json
from pathlib import Path

HERE=Path(__file__).resolve().parent
def main():
    fitted=json.loads((HERE/'fitted-limbs.json').read_text(encoding='utf8'))
    body=[
      ('BodyRoot',None,[0,.23,-.08],[0,.30,-.16]),
      ('SpineRear','BodyRoot',[0,.30,-.16],[0,.26,-.29]),
      ('SpineBack','SpineRear',[0,.26,-.29],[0,.16,-.40]),
      ('Tail','SpineBack',[0,.16,-.40],[0,.15,-.47]),
      ('SpineFront','BodyRoot',[0,.27,.08],[0,.17,.26]),
      ('Chest','SpineFront',[0,.17,.26],[0,.11,.33]),
      ('Neck','Chest',[0,.11,.33],[0,.08,.40]),
      ('Head','Neck',[0,.08,.40],[0,-.04,.40]),
      ('MaskTip','Head',[0,-.04,.40],[0,-.08,.41])]
    bones=[dict(name=n,parent=p,head=h,tail=t) for n,p,h,t in body]
    limbs=[]
    for i,l in enumerate(fitted['limbs']):
        name=l['name'];s,e,w=[l['joints'][k]['position'] for k in ('Shoulder','Elbow','Wrist')]
        tip=l['tip'];side='L' if s[0]<0 else 'R'
        z=s[2];parent='Chest' if z>.14 else 'SpineFront' if z>.0 else 'BodyRoot' if z>-.13 else 'SpineRear' if z>-.27 else 'SpineBack'
        names=[name+'_'+k for k in ('Shoulder','Elbow','Wrist')]
        hand_tail=[w[k]+(tip[k]-w[k]) for k in range(3)]
        if sum((hand_tail[k]-w[k])**2 for k in range(3))<.0001:hand_tail=[w[0],w[1]-.03,w[2]+.01]
        for n,p,h,t in zip(names,[parent,names[0],names[1]],[s,e,w],[e,w,hand_tail]):
            bones.append(dict(name=n,parent=p,head=h,tail=t,limb=name))
        limb=dict(name=name,shoulder=names[0],elbow=names[1],wrist=names[2],
                  wrist_position=w,tip=tip,part=l['part'],index=(i//2)+1,side=side,
                  contact=(i<12),phase=(i//2*.137+(0 if side=='L' else .5))%1,
                  hand_radius=.045 if i<2 else .032)
        # Major arms take long strokes. Small appendages recoil independently.
        limb.update(stride=.12 if i<4 else .075 if i>=10 else .085,lift=.047 if i<12 else .035)
        if i>=12:limb.update(stride=.028,lift=.032,phase=(.11+i*.173)%1)
        limbs.append(limb)
    plan=dict(coordinateSystem='glTF Y-up / face +Z; Blender (x,-z,y)',bones=bones,limbs=limbs,
              floor=-.20,body_height_offset=-.075,cycle_seconds=.72,
              source='fitted-limbs.json; reviewed source mesh, no humanoid retargeting')
    (HERE/'skeleton-plan.json').write_text(json.dumps(plan,indent=2),encoding='utf8')
    print(len(bones),'bones',len(limbs),'limbs')

if __name__=='__main__':main()
