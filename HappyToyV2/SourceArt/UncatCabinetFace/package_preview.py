"""Package the opt-in actual-runtime Uncat cabinet face asset review."""
from pathlib import Path
import argparse,json
from PIL import Image,ImageChops,ImageStat
p=argparse.ArgumentParser(description=__doc__);p.add_argument('--project',type=Path,default=Path(__file__).resolve().parents[2]);a=p.parse_args()
folder=a.project/'Builds/UncatCabinetFace/review'
paths=sorted(folder.glob('frame-*.png'));assert len(paths)==60
frames=[Image.open(p).convert('RGB') for p in paths]
atlas=Image.new('RGB',(768*3,768))
for i,f in enumerate((frames[0],frames[18],frames[33])):atlas.paste(f,(i*768,0))
palette=atlas.quantize(colors=256,method=Image.Quantize.MEDIANCUT)
converted=[f.quantize(palette=palette,dither=Image.Dither.NONE) for f in frames]
converted[0].save(folder/'uncat-cabinet-aligned.gif',save_all=True,append_images=converted[1:],duration=67,loop=0,disposal=2,optimize=False)
comparison=Image.new('RGB',(1536,768))
comparison.paste(Image.open(folder/'chase.png'),(0,0));comparison.paste(Image.open(folder/'gape.png'),(768,0));comparison.save(folder/'chase-to-cabinet.png')
difference=sum(ImageStat.Stat(ImageChops.difference(frames[8],frames[33])).mean)
assert difference>3
report=dict(status='PASS',frames=60,seconds=4.02,meanChannelChange=difference,scope='Actual runtime lit asset review only; no player launch')
(folder/'packaging.json').write_text(json.dumps(report,indent=2),encoding='utf-8');print(json.dumps(report,indent=2))
