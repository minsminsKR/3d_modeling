"""Pillow 10.2.0: assemble 1x hand-layer Editor renders into silent GIFs.

Render PNG sequences first with PlayerHandsPreview.RenderActions. This script
only crops unused transparent space, adds a neutral background and labels, and
encodes the supplied frames at their recorded frame rate; it invents no poses.
"""
import argparse
import json
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont


CLIPS = (
    ("snapper", "01-snapper.gif", "콩알탄 투척"),
    ("trap", "02-trap.gif", "덫 설치 손동작"),
    ("incense", "03-incense.gif", "향 들기 · 사용"),
    ("flashlight", "04-flashlight.gif", "손전등을 들고 이동"),
    ("talisman", "05-talisman.gif", "부적 사용 · 10초 연소"),
)


def panel(path, title, font, small):
    with Image.open(path) as source:
        source = source.convert("RGBA")
        if source.size != (1280, 720):
            raise ValueError(f"Unexpected source frame size: {path}: {source.size}")
        background = Image.new("RGBA", source.size, (20, 24, 29, 255))
        background.alpha_composite(source)
        view = background.crop((0, 220, 1280, 720)).resize(
            (960, 375), Image.Resampling.LANCZOS
        ).convert("RGB")
    result = Image.new("RGB", (960, 459), (20, 24, 29))
    result.paste(view, (0, 56))
    draw = ImageDraw.Draw(result)
    draw.text((22, 12), title, font=font, fill=(235, 236, 232))
    draw.text((864, 17), "1배속", font=small, fill=(181, 198, 187))
    draw.line((20, 54, 940, 54), fill=(48, 55, 61))
    draw.text((22, 437), "Unity 손 레이어 미리보기 · 실제 동작 코드", font=small, fill=(146, 157, 166))
    return result


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("render_root", type=Path)
    parser.add_argument("--font", type=Path, default=Path(__file__).resolve().parents[2] / "Assets/Resources/Fonts/Korean.ttf")
    args = parser.parse_args()
    root = args.render_root.resolve()
    manifest = json.loads((root / "manifest.json").read_text(encoding="utf-8-sig"))
    fps = int(manifest["fps"])
    if fps != 25:
        raise ValueError("This review expects the recorded 25 fps / 40 ms time base")
    font = ImageFont.truetype(str(args.font), 25)
    small = ImageFont.truetype(str(args.font), 15)
    records = []
    sheet = Image.new("RGB", (960, 5 * 153), (20, 24, 29))
    for row, (key, filename, title) in enumerate(CLIPS):
        paths = sorted((root / "frames" / key).glob("frame-*.png"))
        if not paths:
            raise ValueError(f"Missing render sequence: {key}")
        frames = [panel(path, title, font, small) for path in paths]
        # One palette per clip avoids temporal colour flicker and preserves the
        # monochrome hands alongside the orange flame and yellow burning paper.
        sample_ids = sorted({round(i * (len(frames) - 1) / 11) for i in range(12)})
        samples = Image.new("RGB", (320, 153 * len(sample_ids)))
        for index, frame_id in enumerate(sample_ids):
            samples.paste(frames[frame_id].resize((320, 153), Image.Resampling.LANCZOS), (0, index * 153))
        palette = samples.quantize(colors=256, method=Image.Quantize.MEDIANCUT)
        encoded = [frame.quantize(palette=palette, dither=Image.Dither.NONE) for frame in frames]
        output = root / filename
        encoded[0].save(output, save_all=True, append_images=encoded[1:], duration=40,
                        loop=0, disposal=1, optimize=False)
        with Image.open(output) as gif:
            duration = 0
            for index in range(gif.n_frames):
                gif.seek(index)
                duration += gif.info["duration"]
            if duration != len(paths) * 40:
                raise ValueError(f"GIF playback time differs from runtime sampling: {key}")
            encoded_frames = gif.n_frames
        records.append({"clip": key, "gif": filename, "renderedFrames": len(paths),
                        "encodedFrames": encoded_frames, "durationMs": duration,
                        "speed": 1, "bytes": output.stat().st_size})
        sample_frames = ([8, 22, 32] if key in {"snapper", "trap", "incense"}
                         else [round((len(frames) - 1) * fraction) for fraction in (0.1, 0.5, 0.85)])
        for column, frame_id in enumerate(sample_frames):
            frame = frames[frame_id]
            sheet.paste(frame.resize((320, 153), Image.Resampling.LANCZOS), (column * 320, row * 153))
        print(json.dumps(records[-1], ensure_ascii=False), flush=True)
    sheet.save(root / "contact-sheet.png")
    (root / "gif-manifest.json").write_text(json.dumps({
        "scope": "Editor hand-layer preview sampled through the current game pose code; not native gameplay footage",
        "sourceManifest": "manifest.json", "fps": fps, "crop": [0, 220, 1280, 720],
        "clips": records,
    }, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


if __name__ == "__main__":
    main()
