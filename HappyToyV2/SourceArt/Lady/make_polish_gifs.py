"""Assemble genuine 20fps model render frames; never invent or interpolate poses."""
import argparse
import json
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont, __version__ as pillow_version


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--before',type=Path,required=True)
    parser.add_argument('--after',type=Path,required=True)
    parser.add_argument('--output',type=Path,required=True)
    args=parser.parse_args()
    before=sorted(args.before.glob('*.png'));after=sorted(args.after.glob('*.png'))
    if len(before)!=64 or len(after)!=64:raise ValueError('Require all 64 actual frames from both renders')
    size=(384,576);font=ImageFont.load_default(size=20);small=ImageFont.load_default(size=13)
    pairs=[];solo=[]
    for left,right in zip(before,after):
        a=Image.open(left).convert('RGB').resize(size,Image.Resampling.LANCZOS)
        b=Image.open(right).convert('RGB').resize(size,Image.Resampling.LANCZOS)
        pair=Image.new('RGB',(768,624),(10,12,16));pair.paste(a,(0,30));pair.paste(b,(384,30))
        draw=ImageDraw.Draw(pair);draw.text((14,5),'BEFORE',font=font,fill='white');draw.text((398,5),'AFTER',font=font,fill='white')
        draw.text((14,607),'Actual authored chase | 3.2s excerpt | 20 fps | CPU render',font=small,fill=(190,198,210))
        pairs.append(pair);solo.append(b)
    args.output.mkdir(parents=True,exist_ok=True)
    for name,frames in [('lady-motion-after.gif',solo),('lady-before-after.gif',pairs)]:
        swatches=Image.new('RGB',(frames[0].width,frames[0].height*8))
        for index,frame in enumerate(frames[::8]):swatches.paste(frame,(0,index*frame.height))
        palette=swatches.quantize(colors=192,method=Image.Quantize.MEDIANCUT)
        indexed=[frame.quantize(palette=palette,dither=Image.Dither.NONE) for frame in frames]
        indexed[0].save(args.output/name,save_all=True,append_images=indexed[1:],duration=50,loop=0,disposal=2,optimize=False)
    (args.output/'gif-review.json').write_text(json.dumps(dict(frames=64,fps=20,durationSeconds=3.2,
        sourceFrames=[13,202],fullActionSeconds=37.3,pillow=pillow_version,
        before=str(args.before),after=str(args.after),method='Same camera/lighting, actual Blender poses; no generated/interpolated motion'),indent=2))
    print('Actual Lady motion and comparison GIFs saved')


if __name__=='__main__':main()
