"""Build matching poster textures from the two generated portrait originals.

Run from any directory with Python 3 and Pillow installed. Korean.ttf is the
project's existing font. These are source outputs, not yet connected to Unity.
"""
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

HERE = Path(__file__).resolve().parent
FONT = HERE.parents[1] / "Assets" / "Resources" / "Fonts" / "Korean.ttf"


def make(source, destination):
    canvas = Image.new("RGB", (1024, 1536), (224, 218, 196))
    draw = ImageDraw.Draw(canvas)
    draw.rectangle((27, 27, 996, 1508), outline=(93, 70, 41), width=9)
    draw.rectangle((42, 42, 981, 1493), outline=(142, 117, 72), width=3)
    title = ImageFont.truetype(str(FONT), 100)
    sub = ImageFont.truetype(str(FONT), 52)
    small = ImageFont.truetype(str(FONT), 40)
    def centred(y, text, font, color):
        draw.text((512, y), text, font=font, fill=color, anchor="ma")
    centred(77, "축  금상 수상", title, (107, 37, 29))
    centred(233, "교내 학업 경진대회", sub, (55, 49, 39))
    portrait = Image.open(source).convert("RGB")
    portrait.thumbnail((660, 885), Image.Resampling.LANCZOS)
    canvas.paste(portrait, ((1024-portrait.width)//2, 360))
    centred(1290, "정독반  김도윤", sub, (55, 49, 39))
    centred(1380, "수상을 진심으로 축하합니다", small, (83, 66, 45))
    canvas.save(destination, optimize=True)


if __name__ == "__main__":
    make(HERE / "normal.png", HERE / "poster-normal.png")
    make(HERE / "anomaly.png", HERE / "poster-anomaly.png")
    make(HERE / "anomaly-ghost.png", HERE / "poster-anomaly-ghost.png")
    make(HERE / "anomaly-specter.png", HERE / "poster-anomaly-specter.png")
