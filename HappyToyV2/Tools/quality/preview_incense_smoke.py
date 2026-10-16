"""Pillow 10.2.0: encode the opt-in IncenseSmokePreview frames at their 1x time base."""
import argparse
import json
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('render_output', type=Path)
    args = parser.parse_args()
    root = args.render_output.resolve()
    report = json.loads((root / 'manifest.json').read_text(encoding='utf-8-sig'))
    if report['fps'] != 25 or report['playbackRate'] != 1:
        raise ValueError('Expected the recorded 25 fps / 1x preview')
    font_path = Path(__file__).resolve().parents[2] / 'Assets/Resources/Fonts/Korean.ttf'
    font = ImageFont.truetype(str(font_path), 22)
    sheet = Image.new('RGB', (1280, 392), (20, 24, 29))
    for column, (key, label) in enumerate((('before', '기존: 여러 방향 · 원형 입자'), ('after', '수정: 단일 목표 · 이어지는 연기'))):
        paths = sorted((root / 'frames' / key).glob('frame-*.png'))
        if len(paths) != report['frameCountPerClip']:
            raise ValueError('Missing preview frames: ' + key)
        frames = []
        for path in paths:
            with Image.open(path) as source:
                panel = Image.new('RGB', (960, 580), (20, 24, 29))
                panel.paste(source.convert('RGB').resize((960, 540), Image.Resampling.LANCZOS), (0, 40))
            draw = ImageDraw.Draw(panel)
            draw.text((18, 6), label, font=font, fill=(235, 236, 232))
            draw.text((861, 6), '1배속', font=font, fill=(181, 198, 187))
            frames.append(panel)
        sample = Image.new('RGB', (480, 290 * 10))
        for i in range(10):
            sample.paste(frames[round(i * (len(frames) - 1) / 9)].resize((480, 290)), (0, i * 290))
        palette = sample.quantize(colors=256, method=Image.Quantize.MEDIANCUT)
        encoded = [frame.quantize(palette=palette, dither=Image.Dither.NONE) for frame in frames]
        output = root / (key + '.gif')
        encoded[0].save(output, save_all=True, append_images=encoded[1:], duration=40, loop=0, disposal=1, optimize=False)
        with Image.open(output) as gif:
            duration = 0
            for i in range(gif.n_frames):
                gif.seek(i); duration += gif.info['duration']
            if duration != len(frames) * 40:
                raise ValueError('GIF duration differs from the sampled gameplay clock')
        ImageDraw.Draw(sheet).text((column * 640 + 14, 3), label, font=font, fill=(235, 236, 232))
        with Image.open(paths[len(paths) // 2]) as source:
            sheet.paste(source.convert('RGB').resize((640, 360), Image.Resampling.LANCZOS), (column * 640, 32))
        print(json.dumps({'gif': str(output), 'durationMs': duration, 'bytes': output.stat().st_size}, ensure_ascii=False), flush=True)
    sheet.save(root / 'comparison.png')


if __name__ == '__main__':
    main()
