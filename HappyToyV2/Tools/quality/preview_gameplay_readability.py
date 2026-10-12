"""Review-only GIFs/contact sheet from GameplayReadabilityProbe native captures.

Requires Pillow 10.2.0. Does not edit game assets or models.
python HappyToyV2/Tools/quality/preview_gameplay_readability.py <native-output>
"""
import argparse
from pathlib import Path
from PIL import Image, ImageDraw


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("native_output", type=Path)
    args = parser.parse_args()
    sheet = Image.new("RGB", (1280, 4 * 204), (12, 12, 14))
    for row, (key, action) in enumerate((
        ("Cyclopse", "patrol"), ("Cyclopse", "chase"),
        ("Uncat", "patrol"), ("Uncat", "chase"),
    )):
        frames = []
        for index in range(10):
            with Image.open(args.native_output / f"{key}-{action}-{index:02}.png") as source:
                panel = Image.new("RGB", (640, 384), (12, 12, 14))
                panel.paste(source.resize((640, 360), Image.Resampling.LANCZOS), (0, 24))
            ImageDraw.Draw(panel).text((10, 6), f"{key} / {action} / frame {index + 1:02}/10", fill=(240, 240, 240))
            frames.append(panel)
        frames[0].save(args.native_output / f"{key}-{action}.gif", save_all=True,
                       append_images=frames[1:], duration=80, loop=0)
        for column, index in enumerate((0, 3, 6, 9)):
            sheet.paste(frames[index].resize((320, 192), Image.Resampling.LANCZOS), (column * 320, row * 204))
    sheet.save(args.native_output / "gait-contact-sheet.png")


if __name__ == "__main__":
    main()
