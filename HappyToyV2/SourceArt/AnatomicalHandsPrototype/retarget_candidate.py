"""Transfer measured candidate-grip offsets to sampled pose and clip endpoints.
This maintains the original gameplay release times; it does not certify visual quality.
"""
import json
from pathlib import Path
from snapper_palm_action import author as author_snapper_palm
root=Path(__file__).resolve().parent
base=json.loads((root/'original-poses.json').read_text(encoding='utf8'))
cal=json.loads((root/'candidate-rest.json').read_text(encoding='utf8'))
deltas=json.loads((root/'grip-deltas.json').read_text(encoding='utf8'))
limits={b['name']:b for b in base['bones']}
for b in base['bones']:b.update(cal[b['name']])
def adjust(bones,change,weight):
 for b in bones:
  if b['name'] not in change:continue
  for k in ('curl','spread','twist'):
   b[k]=round(max(limits[b['name']][k+'Min'],min(limits[b['name']][k+'Max'],b[k]+change[b['name']][k]*weight)),5)
for pose in base['poses']:
 if pose['name'] in deltas:adjust(pose['bones'],deltas[pose['name']],1.)
for clip in base['clips']:
 if clip['item'] not in deltas:continue
 for frame in clip['frames']:
  t=frame['time'];duration=clip['duration']
  if clip['name'].startswith('Equip'):weight=t/duration
  elif clip['name'].startswith('Unequip'):weight=1-t/duration
  # These items remain held through their use marker. Returning to the old
  # bind's uncorrected grip at that instant made the hand sink back into them.
  elif clip['item'] in ('Flashlight','Incense','Talisman'):weight=1.
  else:
   release=clip['releaseTime'];weight=(1-t/release) if t<=release else (t-release)/(duration-release)
  adjust(frame['bones'],deltas[clip['item']],max(0.,min(1.,weight)))
author_snapper_palm(base)
(root/'candidate-retargeted-poses.json').write_text(json.dumps(base,indent=2)+'\n',encoding='utf8')
