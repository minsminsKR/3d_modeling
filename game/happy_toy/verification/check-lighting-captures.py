from PIL import Image,ImageStat
from pathlib import Path
p=Path(__file__).resolve().parent/'lighting-play-qa';v={}
for n in ['off-far','on-far','on-near','off-near']:
 v[n]=sum(ImageStat.Stat(Image.open(p/('final-'+n+'.png')).convert('RGB').crop((240,110,1040,610))).mean)/3
print(v)
assert v['on-far']>v['off-far']*2
assert v['off-near']<=v['off-far']+1
assert v['on-near']<=v['on-far']+1
print('Rendered screenshot brightness checks passed')
