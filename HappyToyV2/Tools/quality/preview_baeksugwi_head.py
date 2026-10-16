"""Pillow 10.2.0: assemble silent native head captures without changing sources."""
import argparse,json
from pathlib import Path
from PIL import Image,ImageDraw

def frame_intervals(records):
    clock='wallSeconds' if all('wallSeconds' in frame for frame in records) else 'time'
    intervals=[max(20,round((b[clock]-a[clock])*1000)) for a,b in zip(records,records[1:])]
    # Older manifests retain their original animation-clock preview behavior.
    return [min(1000,value) for value in intervals] if clock=='time' else intervals

def main():
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('native_output',type=Path);args=parser.parse_args()
    report=json.loads((args.native_output/'baeksugwi-head.json').read_text(encoding='utf-8-sig'))
    if report['status']!='PASS':raise ValueError('Native proof did not pass')
    sheet=Image.new('RGB',(1280,2*204),(12,12,14))
    for row,stage in enumerate(('title','chase')):
        records=[frame for frame in report['frames'] if frame['stage']==stage];frames=[]
        for record in records:
            with Image.open(args.native_output/record['image']) as source:
                panel=Image.new('RGB',(640,384),(12,12,14));panel.paste(source.resize((640,360),Image.Resampling.LANCZOS),(0,24))
            ImageDraw.Draw(panel).text((10,6),f"Baeksugwi / {stage} / head only",fill=(240,240,240));frames.append(panel)
        intervals=frame_intervals(records)
        intervals.append(round(sum(intervals)/len(intervals)))
        frames[0].save(args.native_output/f'{stage}-head.gif',save_all=True,append_images=frames[1:],duration=intervals,loop=0)
        extremes=sorted(range(len(records)),key=lambda index:records[index].get('rollDegrees',0))
        indices=(extremes[0],len(records)//3,extremes[-1],2*len(records)//3)
        for column,index in enumerate(indices):sheet.paste(frames[index].resize((320,192),Image.Resampling.LANCZOS),(column*320,row*204))
    sheet.save(args.native_output/'head-contact-sheet.png')

if __name__=='__main__':main()
