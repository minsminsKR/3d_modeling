"""Reproducible school decay finish over the project's preserved CC0 scans.

The photographs remain the source of surface grain and normal detail. This tool
adds correlated, authored moisture, mold, paint edges and water roughness; it
does not bake lighting into albedo or invent a replacement 3D model.
Run from any directory with Python 3.11, NumPy 1.26.4 and Pillow 10.4.0.
"""
from pathlib import Path
import hashlib
import json
import uuid

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

HERE = Path(__file__).resolve().parent
PROJECT = HERE.parents[1]
SCANS = PROJECT / 'Assets/Resources/GraphicsPbr'
OUT = PROJECT / 'Assets/Resources/SchoolHorror'
SIZE = 2048
SEED = 2026102119
rng = np.random.default_rng(SEED)
inputs = {}
outputs = {}


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def load(key, channel):
    path = SCANS / key / (channel + '.png')
    inputs[path.relative_to(PROJECT).as_posix()] = sha(path)
    return np.asarray(Image.open(path).convert('RGB' if channel != 'metallic-smoothness' else 'RGBA'), np.float32) / 255


def noise(cells, size=SIZE, shape=None):
    # A wrapped bicubic field has no discontinuity at a repeating texture seam.
    rows, cols = shape or (cells, cells)
    source = rng.random((rows, cols), dtype=np.float32)
    tiled = np.tile(source, (3, 3))
    up = Image.fromarray(tiled, 'F').resize((size * 3, size * 3), Image.Resampling.BICUBIC)
    return np.clip(np.asarray(up, np.float32)[size:size * 2, size:size * 2], 0, 1)


def smooth(a, low, high):
    t = np.clip((a - low) / (high - low), 0, 1)
    return t * t * (3 - 2 * t)


def cracks(count, width=2):
    canvas = Image.new('L', (SIZE, SIZE))
    draw = ImageDraw.Draw(canvas)
    for _ in range(count):
        x, y = rng.uniform(0, SIZE, 2)
        theta = rng.uniform(0, np.pi * 2)
        points = [(x, y)]
        for step in range(int(rng.integers(9, 32))):
            theta += rng.normal(0, .32)
            x += np.cos(theta) * rng.uniform(7, 32)
            y += np.sin(theta) * rng.uniform(7, 32)
            points.append((x, y))
        draw.line(points, fill=int(rng.integers(90, 220)), width=width)
    return np.asarray(canvas.filter(ImageFilter.GaussianBlur(.45)), np.float32) / 255


def normal_from_height(height, scan_normal, strength=1):
    dx = (np.roll(height, -1, axis=1) - np.roll(height, 1, axis=1)) * SIZE / 2
    dy = (np.roll(height, -1, axis=0) - np.roll(height, 1, axis=0)) * SIZE / 2
    n = scan_normal * 2 - 1
    n[:, :, 0] -= dx * strength
    n[:, :, 1] += dy * strength
    n[:, :, 2] = np.maximum(n[:, :, 2], .25)
    n /= np.maximum(np.linalg.norm(n, axis=2, keepdims=True), .00001)
    return n * .5 + .5


def guid(name):
    return uuid.uuid5(uuid.NAMESPACE_URL, 'happytoy-v2/school-horror/' + name).hex


def meta(path, normal=False, linear=False):
    template = SCANS / 'plaster-damp' / ('normal.png.meta' if normal else 'albedo.png.meta')
    text = template.read_text(encoding='utf-8-sig')
    import re
    text = re.sub(r'guid: [a-f0-9]{32}', 'guid: ' + guid(path.relative_to(OUT).as_posix()), text, count=1)
    if linear:
        text = text.replace('sRGBTexture: 1', 'sRGBTexture: 0')
    text = text.replace('aniso: 16', 'aniso: 8')
    path.with_name(path.name + '.meta').write_text(text, encoding='utf-8', newline='\n')


def save_image(path, data, normal=False, linear=False):
    path.parent.mkdir(parents=True, exist_ok=True)
    Image.fromarray(np.uint8(np.clip(data, 0, 1) * 255 + .5)).save(path, compress_level=7)
    meta(path, normal=normal, linear=linear)
    outputs[path.relative_to(PROJECT).as_posix()] = {'sha256': sha(path), 'bytes': path.stat().st_size}


def save_set(name, albedo, normal, ao, smoothness, metal=0):
    root = OUT / name
    save_image(root / 'albedo.png', albedo)
    save_image(root / 'normal.png', normal, normal=True, linear=True)
    save_image(root / 'ao.png', ao, linear=True)
    packed = np.zeros((SIZE, SIZE, 4), np.float32)
    packed[:, :, 0] = metal
    packed[:, :, 3] = smoothness
    save_image(root / 'metallic-smoothness.png', packed, linear=True)


def plaster(name, lower=False, ceiling=False):
    base = load('plaster-damp', 'albedo').copy()
    scan_normal = load('plaster-damp', 'normal').copy()
    scan_ao = load('plaster-damp', 'ao').mean(axis=2)
    if ceiling:
        base = np.rot90(base).copy()
        scan_normal = np.rot90(scan_normal).copy()
        # Reorient the tangent normal with the rotated source photograph.
        old_x = scan_normal[:, :, 0].copy()
        scan_normal[:, :, 0] = scan_normal[:, :, 1]
        scan_normal[:, :, 1] = 1 - old_x
        scan_ao = np.rot90(scan_ao).copy()
    broad, medium, fine, micro = noise(5), noise(23), noise(115), noise(490)
    streak = noise(1, shape=(5, 95))
    mold = smooth(broad * .42 + medium * .34 + fine * .24, .55, .75)
    damp = smooth(broad * .67 + streak * .33, .32, .77)
    crack = cracks(22 if ceiling else 12)
    luminance = base.mean(axis=2)
    exposed = 1 - smooth(luminance, .30, .52)
    # Concentrate growth in the photograph's exposed substrate and actual paint
    # edges. Dense high-contrast procedural clouds read as repeating camouflage.
    mold *= .17 + exposed * .83
    if lower:
        # The scan's detached paint exposes the same ochre plaster below a
        # subdued old school green coating. Grain and actual cracks remain.
        enamel = np.array((.31, .36, .30), np.float32)
        coating = smooth(luminance, .29, .49) * .64
        base = base * (1 - coating[:, :, None]) + enamel[None, None, :] * coating[:, :, None]
    elif ceiling:
        grey = base.mean(axis=2)
        base = np.dstack((grey * .94, grey * .96, grey * .91))
    mold_colour = np.array((.035, .040, .028), np.float32)
    amount = mold * (.36 if ceiling else .31 if lower else .25)
    base = base * (1 - amount[:, :, None]) + mold_colour[None, None, :] * amount[:, :, None]
    base *= (1 - damp * .16 - crack * .63)[:, :, None]
    base += ((fine - .5) * .018)[:, :, None]
    height = -mold * .00036 - crack * .0010 + (micro - .5) * .000025
    normal = normal_from_height(height, scan_normal, .7)
    ao = scan_ao * (1 - exposed * .13 - crack * .30 - mold * .12)
    gloss = .10 + damp * .17 + exposed * .035 - mold * .07
    save_set(name, base, normal, ao, gloss)


def floor():
    base = load('ceramic-tile', 'albedo').copy()
    scan_normal = load('ceramic-tile', 'normal')
    scan_ao = load('ceramic-tile', 'ao').mean(axis=2)
    # Preserve the scan's scratches/veins while bringing contrasting marble
    # squares into the neutral grey-beige range of the reference school tile.
    lum = base.mean(axis=2)
    neutral = np.zeros_like(lum)
    for row in range(4):
        for col in range(4):
            part = lum[row * 512:(row + 1) * 512, col * 512:(col + 1) * 512]
            neutral[row * 512:(row + 1) * 512, col * 512:(col + 1) * 512] = np.clip((part - part.mean()) * .65 + rng.uniform(.28, .40), .12, .57)
    base = np.dstack((neutral * 1.06, neutral * 1.025, neutral * .94))
    broad, medium, fine = noise(5), noise(29), noise(187)
    dirt = smooth(broad * .6 + medium * .4, .31, .76)
    crack = cracks(18, width=2)
    # Real grout and chips remain in the source normal/AO; damage correlates
    # roughness and albedo, while the pooled water flattens the final normal.
    base *= (1 - dirt * .25 - crack * .64)[:, :, None]
    height = -crack * .0012 + (fine - .5) * .000015
    normal = normal_from_height(height, scan_normal, .55)
    # The tile texture carries only dry ceramic roughness. Actual water is a
    # continuous metre-space leak field in the shader, never a 1.28m repeat.
    smoothness = .20 + fine * .10 - crack * .09
    ao = scan_ao * (1 - crack * .35 - dirt * .08)
    save_set('floor', base, normal, ao, smoothness)


def glass_and_macro():
    n = 1024
    broad = noise(7, size=n)
    fine = noise(150, size=n)
    vertical = noise(1, size=n, shape=(34, 183))
    streaks = smooth(vertical * .72 + broad * .28, .35, .71)
    droplets_image = Image.new('L', (n, n))
    droplets_draw = ImageDraw.Draw(droplets_image)
    for _ in range(460):
        x, y = rng.integers(0, n, 2)
        width, height = rng.integers(1, 3), rng.integers(2, 6)
        droplets_draw.ellipse((int(x), int(y), int(x + width), int(y + height)), fill=int(rng.integers(70, 180)))
    droplets = np.asarray(droplets_image.filter(ImageFilter.GaussianBlur(.5)), np.float32) / 255
    colour = np.zeros((n, n, 4), np.float32)
    colour[:, :, :3] = (.22 + streaks * .055 + fine * .018)[:, :, None] * np.array((.91, .98, 1.0))
    colour[:, :, 3] = .012 + streaks * .048 + smooth(broad, .57, .78) * .028 + droplets * .025
    save_image(OUT / 'glass/albedo.png', colour)
    height = streaks * .000009 + (fine - .5) * .0000015 + droplets * .000004
    dx = (np.roll(height, -1, axis=1) - np.roll(height, 1, axis=1)) * n / 2
    dy = (np.roll(height, -1, axis=0) - np.roll(height, 1, axis=0)) * n / 2
    normal = np.dstack((-dx, dy, np.ones_like(dx)))
    normal /= np.linalg.norm(normal, axis=2, keepdims=True)
    save_image(OUT / 'glass/normal.png', normal * .5 + .5, normal=True, linear=True)
    macro = np.dstack((broad, noise(29, size=n), streaks, noise(55, size=n)))
    save_image(OUT / 'moisture-macro.png', macro, linear=True)


def folder_metadata():
    for directory in sorted([OUT] + [p for p in OUT.rglob('*') if p.is_dir()]):
        path = directory.with_name(directory.name + '.meta')
        if path.exists():
            continue
        name = directory.relative_to(OUT).as_posix() if directory != OUT else 'root'
        path.write_text('fileFormatVersion: 2\nguid: ' + guid(name + '/') + '\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n', encoding='utf-8', newline='\n')


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    plaster('wall')
    plaster('lower-wall', lower=True)
    plaster('ceiling', ceiling=True)
    floor()
    glass_and_macro()
    folder_metadata()
    source_manifest = json.loads((PROJECT / 'ThirdParty/Graphics/polyhaven-pbr-manifest.json').read_text(encoding='utf-8-sig'))
    credits = [record for record in source_manifest['maps'] if record['key'] in ('plaster-damp', 'ceramic-tile')]
    manifest = {'finishRevision': 3, 'seed': SEED, 'generator': 'SourceArt/SchoolHorror/build_surfaces.py', 'generatorSha256': sha(Path(__file__)),
                'python': '3.11', 'numpy': '1.26.4', 'pillow': '10.4.0', 'resolution': [SIZE, SIZE],
                'normalConvention': 'Tangent +Y/OpenGL; Unity NormalMap importer. Rotated ceiling normals reoriented with scan.',
                'packedChannels': 'Metallic red (dielectric zero), smoothness alpha, G/B zero; AO separate linear grayscale.',
                'physicalRepeatMetres': {'wall': 1.8, 'lower-wall': 1.8, 'ceiling': 2.4, 'floor': 1.28},
                'waterPlacement': 'Continuous world-space runoff from the z10.8 roof leak is centred at school local (-.35,9.8), radii(1.2,3.7); second pool (.55,24.3), radii(1.3,2.4). A narrow irregular route film has roughly .50-.59 smoothness, pools reach .94, wall edges stay dry. Source tile texture is dry; world wetness controls darkening and roughness.',
                'sourceScans': credits, 'inputs': inputs, 'outputs': outputs,
                'limits': 'Photographed scan detail plus reproducible authored moisture/mold/crack finish. No captured target-school scan, displacement geometry, pre-baked GI or painted reflections.'}
    (HERE / 'surface-manifest.json').write_text(json.dumps(manifest, indent=2, ensure_ascii=False) + '\n', encoding='utf-8', newline='\n')
    print(json.dumps({'status': 'SCHOOL_SURFACES_GENERATED', 'outputs': len(outputs), 'sourceInputs': len(inputs)}))


if __name__ == '__main__':
    main()
