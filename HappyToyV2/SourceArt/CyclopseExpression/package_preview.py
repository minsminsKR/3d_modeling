"""Package the actual Unity-rendered proposal frames into a looping GIF (Pillow 10.4.0)."""
from pathlib import Path
import argparse
import json
from PIL import Image, ImageChops, ImageStat


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('frames', type=Path)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    paths = sorted(args.frames.glob('frame-*.png'))
    if len(paths) != 90:
        raise ValueError(f'Expected 90 freshly rendered frames, got {len(paths)}')
    frames = [Image.open(path).convert('RGB') for path in paths]
    assert all(image.size == (768, 768) for image in frames)
    difference = sum(ImageStat.Stat(ImageChops.difference(frames[0], frames[45])).mean)
    assert difference > 2, 'Proposal is visually unchanged'
    # A shared palette prevents colour shimmer; uniform lighting keeps the morph readable.
    atlas = Image.new('RGB', (768 * 3, 768))
    for i, frame in enumerate((frames[0], frames[27], frames[45])):
        atlas.paste(frame, (768 * i, 0))
    palette = atlas.quantize(colors=256, method=Image.Quantize.MEDIANCUT)
    converted = [frame.quantize(palette=palette, dither=Image.Dither.NONE) for frame in frames]
    durations = [60 if i % 9 < 5 else 50 for i in range(len(frames))]
    args.output.parent.mkdir(parents=True, exist_ok=True)
    converted[0].save(args.output, save_all=True, append_images=converted[1:],
                      duration=durations, loop=0, optimize=False, disposal=2)
    contact = Image.new('RGB', (1536, 768))
    contact.paste(frames[0], (0, 0)); contact.paste(frames[45], (768, 0))
    contact.save(args.output.with_suffix('.comparison.png'))
    decoded = Image.open(args.output)
    assert decoded.is_animated and decoded.n_frames >= 40
    report = dict(status='PASS', sourceFrameCount=90, encodedFrameCount=decoded.n_frames,
                  seconds=sum(durations)/1000, size=decoded.size,
                  neutralToGrinMeanChannelDifference=difference,
                  scope='Facial expression art proposal rendered on a copy of CyclopseRuntime; gameplay integration pending user review.')
    args.output.with_suffix('.json').write_text(json.dumps(report, indent=2), encoding='utf-8')
    print(json.dumps(report, indent=2))


if __name__ == '__main__':
    main()
