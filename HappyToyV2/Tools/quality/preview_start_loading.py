"""Pillow 10.2.0: assemble actual silent loading captures and drawer evidence."""
import argparse
import json
from pathlib import Path
from PIL import Image, ImageDraw


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('native_output', type=Path)
    output = parser.parse_args().native_output
    report = json.loads((output / 'start-loading.json').read_text(encoding='utf-8-sig'))
    if report['status'] != 'PASS':
        raise ValueError('Actual player verification did not pass')
    records = [frame for frame in report['frames'] if not frame['image'].startswith('drawer-')]
    frames = []
    for record in records:
        with Image.open(output / record['image']) as original:
            frames.append(original.convert('RGB').resize((640, 360), Image.Resampling.LANCZOS))
    # A shared palette avoids palette flicker and keeps the optional GIF compact.
    montage = Image.new('RGB', (640, 360 * 4))
    for row, index in enumerate((0, len(frames)//3, 2*len(frames)//3, len(frames)-1)):
        montage.paste(frames[index], (0, row*360))
    palette = montage.quantize(colors=128, dither=Image.Dither.NONE)
    compact = [frame.quantize(palette=palette, dither=Image.Dither.NONE) for frame in frames]
    duration = [max(30, min(1500, round((b['seconds']-a['seconds'])*1000))) for a, b in zip(records, records[1:])]
    duration.append(1200)
    compact[0].save(output / 'start-loading.gif', save_all=True, append_images=compact[1:], duration=duration, loop=0, optimize=True)
    moving = min((i for i, frame in enumerate(records) if frame['page']=='Title' and frame['menu']<.01 and frame['black']<.1),
                 key=lambda i: -records[i]['approach'])
    black = max(range(len(records)), key=lambda i: records[i]['black'])
    sheet = Image.new('RGB', (1280, 744), (8, 8, 10))
    for index, (frame, label) in enumerate(zip((0, moving, black, len(frames)-1), ('Menu', 'Approach while loading', 'Black transition', 'Corridor revealed'))):
        x, y = (index % 2)*640, (index // 2)*372
        sheet.paste(frames[frame], (x, y+12))
        ImageDraw.Draw(sheet).text((x+8, y), label, fill=(235, 235, 235))
    sheet.save(output / 'start-contact-sheet.png')
    drawer = Image.new('RGB', (1280, 372), (8, 8, 10))
    for column, name in enumerate(('drawer-before.png', 'drawer-open.png')):
        with Image.open(output/name) as source:
            drawer.paste(source.resize((640, 360), Image.Resampling.LANCZOS), (column*640, 12))
        ImageDraw.Draw(drawer).text((column*640+8, 0), 'Touching distance / '+('closed' if column==0 else 'open'), fill=(235, 235, 235))
    drawer.save(output / 'drawer-contact-sheet.png')


if __name__ == '__main__':
    main()
