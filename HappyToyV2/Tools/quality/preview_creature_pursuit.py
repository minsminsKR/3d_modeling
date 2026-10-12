"""Create review-only GIFs/contact sheet from silent native pursuit captures.

Requires Pillow 10.2.0. No model/image source is modified. Run from repo root:
python HappyToyV2/Tools/quality/preview_creature_pursuit.py <native-output>
"""
import argparse
from pathlib import Path
from PIL import Image, ImageDraw


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('native_output',type=Path)
    args=parser.parse_args()
    sheet=Image.new('RGB',(1280,3*204),(12,12,14))
    for row,(key,duration) in enumerate((('Uncat',1.116667),('Cyclopse',1.116667),('Baby',1.183333))):
        frames=[]
        for index in range(16):
            with Image.open(args.native_output/f'{key}-chase-{index:02}.png') as source:
                panel=Image.new('RGB',(640,384),(12,12,14))
                panel.paste(source.resize((640,360),Image.Resampling.LANCZOS),(0,24))
            ImageDraw.Draw(panel).text((10,6),f'{key} / pursuit / frame {index+1:02}/16',fill=(240,240,240))
            frames.append(panel)
        frames[0].save(args.native_output/f'{key}-pursuit.gif',save_all=True,append_images=frames[1:],duration=round(duration*1000/16),loop=0)
        for column,index in enumerate((0,3,7,11)):
            sheet.paste(frames[index].resize((320,192),Image.Resampling.LANCZOS),(column*320,row*204))
    sheet.save(args.native_output/'pursuit-contact-sheet.png')

if __name__=='__main__':main()
