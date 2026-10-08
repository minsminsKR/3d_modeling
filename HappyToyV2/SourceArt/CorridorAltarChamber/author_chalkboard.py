"""Original abandoned classroom chalkboard surface; Python 3.11.7 / Pillow 10.2 / NumPy 1.26.4.

No downloaded pixels. Chalk dust, overlapping eraser strokes, small scratches,
water runs and rubbed-out lesson lettering share correlated physical fields.
The bundled OFL Korean font supplies glyph outlines, then individual deposits
are roughened. Outputs always follow this script's repository-relative location.
--resource-project is an optional read-only source override for review staging.
"""
from pathlib import Path
import argparse
import hashlib
import json
import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont

HERE = Path(__file__).resolve().parent
PROJECT = HERE.parents[1]
WIDTH, HEIGHT = 2048, 1024


def resized_noise(rng, x, y):
    source = rng.random((y, x)).astype(np.float32)
    return np.clip(np.asarray(Image.fromarray(source, mode='F').resize(
        (WIDTH, HEIGHT), Image.Resampling.BICUBIC)), 0, 1)


def build(resource_project):
    out = PROJECT / 'Assets/Resources/CorridorAltarChamber/Chalkboard'
    out.mkdir(parents=True, exist_ok=True)
    rng = np.random.default_rng(810085)
    broad, fine, micro = (resized_noise(rng, *size) for size in ((16, 9), (155, 89), (530, 267)))
    yy, xx = np.mgrid[:HEIGHT, :WIDTH].astype(np.float32)
    x, y = xx / WIDTH, yy / HEIGHT
    # Old green classroom enamel is still visible beneath its erased last lesson.
    colour = np.array((.053, .083, .068))[None, None, :] * (
        .81 + .29 * broad + .035 * fine)[:, :, None]
    wiped = np.zeros((HEIGHT, WIDTH), np.float32)
    for i in range(34):
        cx, cy = rng.uniform(.07, .95), rng.uniform(.08, .90)
        rx, ry = rng.uniform(.10, .34), rng.uniform(.021, .047)
        stripe = np.exp(-((x - cx) / rx) ** 4 - ((y - cy - .004 * np.sin(x * 37 + i)) / ry) ** 2)
        wiped += stripe * rng.uniform(.01, .038)
    wiped = np.clip(wiped, 0, .10)
    colour += wiped[:, :, None] * np.array((.57, .59, .47))
    # Vertical damp marks have soft shoulders and a darker physical grain.
    wet = np.zeros_like(wiped)
    for _ in range(14):
        cx, top = rng.uniform(.03, .98), rng.uniform(0, .44)
        width, length = rng.uniform(.0012, .007), rng.uniform(.11, .61)
        wet += np.exp(-((x - cx) / width) ** 2) * np.clip((top + length - y) / length, 0, 1) * (y >= top)
    wet = np.clip(wet, 0, 1)
    colour *= (1 - .31 * wet)[:, :, None]

    font_path = resource_project / 'Assets/Resources/Fonts/Korean.ttf'
    if not font_path.is_file():
        raise FileNotFoundError('Bundled OFL Korean font required: Assets/Resources/Fonts/Korean.ttf')
    mask = Image.new('L', (WIDTH, HEIGHT))
    draw = ImageDraw.Draw(mask)
    # School-specific remnants: attendance, five erased entries, then a crooked
    # instruction. These are original words, not a facsimile of a real document.
    lesson = [('오늘의 출석', (116, 83), 95, 224),
              ('돌아오지 않은 아이들', (124, 253), 60, 187),
              ('1.  이름을 기억한다', (150, 395), 50, 174),
              ('2.  자리를 비워 둔다', (151, 488), 50, 132),
              ('3.  다섯을 돌려놓는다', (148, 581), 50, 202),
              ('결석  Ⅰ  Ⅱ  Ⅲ  Ⅳ  Ⅴ', (144, 822), 50, 155),
              ('아직 끝나지 않았다', (1126, 260), 57, 123)]
    for content, at, size, strength in lesson:
        font = ImageFont.truetype(str(font_path), size)
        draw.text(at, content, font=font, fill=strength, stroke_width=0)
    # The numerals and lesson rules are chipped deposits rather than uniform text.
    draw.line((120, 208, 892, 220), fill=113, width=4)
    draw.line((126, 222, 920, 233), fill=49, width=2)
    for i in range(5):
        cy = 395 + i * 73
        draw.line((1200, cy, 1854 - (i % 2) * 71, cy + 4), fill=66, width=2)
        for stroke in range(3):
            draw.line((1228 + stroke * 161, cy - 37, 1271 + stroke * 161, cy - 26), fill=30, width=3)
        draw.line((1211, cy - 12, 1801, cy - 21), fill=32, width=12)
    deposit = np.asarray(mask, dtype=np.float32) / 255
    chalk_grain = np.clip(.16 + fine * .87 + micro * .33, 0, 1)
    sparse = rng.random((HEIGHT, WIDTH)) > .115
    deposit *= chalk_grain * sparse
    # Deposited off-white chalk has a much higher physical albedo than the old
    # green enamel. Broken deposits/erasure masks remain; the whole board is not
    # brightened and no emissive letters substitute for classroom material.
    chalk_colour = np.array((.81, .84, .73))
    colour = colour * (1 - deposit[:, :, None]) + chalk_colour * deposit[:, :, None]
    scratched = Image.new('L', (WIDTH, HEIGHT)); scratches = ImageDraw.Draw(scratched)
    for _ in range(139):
        px, py = int(rng.integers(20, WIDTH - 20)), int(rng.integers(20, HEIGHT - 20))
        length = int(rng.integers(6, 160))
        scratches.line((px, py, px + length, py + int(rng.integers(-2, 3))), fill=int(rng.integers(10, 75)), width=1)
    scratch = np.asarray(scratched, dtype=np.float32) / 255
    colour += scratch[:, :, None] * np.array((.21, .22, .15))
    # Correlated board height in metres and physically separate smoothness data.
    height = (fine - .5) * .000045 + (micro - .5) * .000009 - scratch * .00009 + deposit * .000026
    dx = (np.roll(height, -1, 1) - np.roll(height, 1, 1)) * WIDTH / (2 * 4.62)
    dy = (np.roll(height, -1, 0) - np.roll(height, 1, 0)) * HEIGHT / (2 * 1.48)
    normal = np.dstack((-dx, dy, np.ones_like(dx)))
    normal /= np.linalg.norm(normal, axis=2, keepdims=True)
    Image.fromarray(np.uint8(np.clip(colour, 0, 1) * 255), 'RGB').save(out / 'albedo.png')
    Image.fromarray(np.uint8(np.clip(normal * .5 + .5, 0, 1) * 255), 'RGB').save(out / 'normal.png')
    Image.fromarray(np.uint8(np.clip(.99 - scratch * .04, 0, 1) * 255), 'L').save(out / 'ao.png')
    packed = np.zeros((HEIGHT, WIDTH, 4), np.uint8)
    packed[:, :, 3] = np.uint8(np.clip(.28 + .035 * broad + wet * .09 - deposit * .23 - scratch * .08, 0, 1) * 255)
    Image.fromarray(packed, 'RGBA').save(out / 'metallic-smoothness.png')
    manifest = dict(authoring='Original procedural classroom enamel and eraser/chalk deposits; no downloaded pixels',
                    fontSource='Assets/Resources/Fonts/Korean.ttf', fontLicense='Assets/Resources/Fonts/OFL.txt',
                    dimensionsMetres=[4.62, 1.48], resolution=[WIDTH, HEIGHT],
                    metallicSmoothness='R=dielectric metallic 0, A=correlated smoothness; normal is OpenGL +Y tangent space',
                    seed=810085, chalkPigmentSrgb=[.81, .84, .73], images=[])
    for path in sorted(out.glob('*.png')):
        manifest['images'].append(dict(file=path.relative_to(PROJECT).as_posix(), bytes=path.stat().st_size,
                                       sha256=hashlib.sha256(path.read_bytes()).hexdigest()))
    (HERE / 'texture-manifest.json').write_text(json.dumps(manifest, ensure_ascii=False, indent=2), encoding='utf-8')
    print('ORIGINAL_CHALKBOARD_READY', len(manifest['images']))


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--resource-project', type=Path, default=PROJECT)
    build(parser.parse_args().resource_project.resolve())
