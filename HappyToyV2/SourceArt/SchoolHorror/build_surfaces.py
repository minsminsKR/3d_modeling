"""Offline school finish from newly acquired photographed CC0 PBR surfaces.

Acquire original source bytes once with acquire_photographed_sources.py. This
tool reads the tracked originals offline. Photographs supply actual flaking,
organic black staining, chips, worn grout, normal detail and roughness. It does
not bake lighting into albedo or invent a replacement 3D model.
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
ACQUIRED = HERE / 'Acquired'
OUT = PROJECT / 'Assets/Resources/SchoolHorror'
SIZE = 2048
SEED = 2026102119
rng = np.random.default_rng(SEED)
inputs = {}
outputs = {}
scan_cache = {}


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def load_scan(slug):
    if slug in scan_cache:
        return scan_cache[slug]
    manifest = json.loads((ACQUIRED / 'acquisition-manifest.json').read_text(encoding='utf-8-sig'))
    record = next(asset for asset in manifest['assets'] if asset['slug'] == slug)
    result = {}
    for channel, entry in record['sources'].items():
        path = PROJECT / entry['path']
        if not path.is_file() or sha(path) != entry['sha256']:
            raise ValueError('Missing or changed photographed source: ' + str(path))
        inputs[path.relative_to(PROJECT).as_posix()] = entry['sha256']
        image = Image.open(path)
        if channel == 'Displacement':
            values = np.asarray(image, np.float32)
            result[channel] = values / (65535 if values.max() > 255 else 255)
        else:
            result[channel] = np.asarray(image.convert('RGB'), np.float32) / 255
    scan_cache[slug] = result
    return result


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


def plaster(name, slug, lower=False, ceiling=False, rotation=0):
    scan = load_scan(slug)
    mold_scan = load_scan('worn_mossy_plasterwall')
    base = scan['Diffuse'].copy()
    luminance = base[:, :, 0] * .2126 + base[:, :, 1] * .7152 + base[:, :, 2] * .0722
    normals = scan['nor_gl'].copy()
    packed = scan['arm'].copy()
    mold_rgb = np.roll(mold_scan['Diffuse'], (363 if lower else 0, 641 if ceiling else 0), axis=(0, 1))
    mold_luma = mold_rgb[:, :, 0] * .2126 + mold_rgb[:, :, 1] * .7152 + mold_rgb[:, :, 2] * .0722
    # This mask comes from real photographed organic staining, not Gaussian
    # noise spots. Its channels remain correlated with the acquired scan.
    organic = 1 - smooth(mold_luma, .18, .64)
    if lower:
        coating = smooth(base[:, :, 0] - base[:, :, 2], .012, .075)
        pale_substrate = luminance[:, :, None] * np.array((1.07, 1.03, .94))
        paint = luminance[:, :, None] * np.array((.64, .75, .66))
        base = pale_substrate * (1 - coating[:, :, None]) + paint * coating[:, :, None]
        amount = organic * .80
    else:
        # Retain photographed warm exposed plaster and its irregular peel edge,
        # removing the original outdoor paint's strong salmon/brown cast.
        base = luminance[:, :, None] * np.array((1.045, 1.015, .945))
        amount = organic * (.66 if ceiling else .68)
    stain = np.array((.025, .030, .022), np.float32)
    base = base * (1 - amount[:, :, None]) + stain[None, None, :] * amount[:, :, None]
    # Preserve the source normals. Add only a restrained share of the actual
    # organic wall's high-frequency relief, weighted by that photographed stain.
    original_n = normals * 2 - 1
    organic_n = mold_scan['nor_gl'] * 2 - 1
    original_n[:, :, :2] += organic_n[:, :, :2] * (organic * .22)[:, :, None]
    original_n /= np.maximum(np.linalg.norm(original_n, axis=2, keepdims=True), .00001)
    normals = original_n * .5 + .5
    ao = packed[:, :, 0] * (1 - organic * .13)
    gloss = np.clip(1 - packed[:, :, 1], .055, .28) * (1 - organic * .35)
    if rotation:
        base, ao, gloss = (np.rot90(field, rotation).copy() for field in (base, ao, gloss))
        normals = np.rot90(normals, rotation).copy()
        for _ in range(rotation):
            old_x = normals[:, :, 0].copy()
            # np.rot90 is CCW in image coordinates (rows point down). For an
            # OpenGL +Y normal this maps tangent (X,Y) to (-Y,X).
            normals[:, :, 0] = 1 - normals[:, :, 1]
            normals[:, :, 1] = old_x
    save_set(name, base, normals, ao, gloss)


def floor():
    scan = load_scan('dirty_tiles')
    raw = scan['Diffuse']
    lum = raw[:, :, 0] * .2126 + raw[:, :, 1] * .7152 + raw[:, :, 2] * .0722
    # This is a different photographed single-colour quarry tile, not a graded
    # checkerboard. Keep its actual grout, chips and dirty micro-detail intact.
    charcoal = np.clip(lum * .84 + .055, .055, .33)
    base = charcoal[:, :, None] * np.array((1.035, 1.0, .945))
    packed = scan['arm']
    smoothness = np.clip(.17 + (1 - packed[:, :, 1]) * .24, .16, .38)
    save_set('floor', base, scan['nor_gl'], packed[:, :, 0], smoothness)


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
    photo = load_scan('worn_mossy_plasterwall')['Diffuse'].mean(axis=2)
    photo = np.asarray(Image.fromarray(photo, 'F').resize((n, n), Image.Resampling.LANCZOS), np.float32)
    growth = 1 - smooth(photo, .20, .62)
    y = np.arange(n, dtype=np.float32)[:, None] / n
    # MetreBox UV places the pane's top/bottom at repeat phase .5. Organic
    # residue is localized there and narrow runoff trails leave clear areas.
    edge = np.exp(-((y - .5) / .085) ** 2)
    drip = np.exp(-((y - .5) / .20) ** 2) * smooth(vertical, .74, .94)
    colour[:, :, 3] += edge * growth * .075 + drip * .04
    colour[:, :, :3] *= (1 - edge * growth * .25)[:, :, None]
    save_image(OUT / 'glass/albedo.png', colour)
    height = streaks * .000009 + (fine - .5) * .0000015 + droplets * .000004 + edge * growth * .000005 + drip * .000004
    dx = (np.roll(height, -1, axis=1) - np.roll(height, 1, axis=1)) * n / 2
    dy = (np.roll(height, -1, axis=0) - np.roll(height, 1, axis=0)) * n / 2
    normal = np.dstack((-dx, dy, np.ones_like(dx)))
    normal /= np.linalg.norm(normal, axis=2, keepdims=True)
    save_image(OUT / 'glass/normal.png', normal * .5 + .5, normal=True, linear=True)
    macro = np.dstack((broad, noise(29, size=n), streaks, noise(55, size=n)))
    save_image(OUT / 'moisture-macro.png', macro, linear=True)


def puddle_field():
    # This independent seed keeps every photographed/glass output byte-identical
    # when the water-placement field changes. No glass runoff channel is reused.
    n = 1024
    water_rng = np.random.default_rng(SEED + 449)

    def isotropic(cells):
        source = water_rng.random((cells, cells), dtype=np.float32)
        tiled = np.tile(source, (3, 3))
        up = Image.fromarray(tiled, 'F').resize((n * 3, n * 3), Image.Resampling.BICUBIC)
        return np.clip(np.asarray(up, np.float32)[n:n * 2, n:n * 2], 0, 1)

    # One repeat spans 24 physical metres: R has broad 2--3.4m connected
    # features, while G offers only gentle ~1m edge variation in the shader.
    broad = isotropic(7) * .72 + isotropic(12) * .28
    detail = isotropic(24)
    height = isotropic(63) * .72 + isotropic(151) * .28
    dx = (np.roll(height, -1, axis=1) - np.roll(height, 1, axis=1)) * .5
    dy = (np.roll(height, -1, axis=0) - np.roll(height, 1, axis=0)) * .5
    slope_rms = np.sqrt(np.mean(dx * dx + dy * dy))
    slopes = np.dstack((-dx, dy)) / max(float(slope_rms) * 3, .00001)
    # BA stores normalized tangent-water XY, not additional pool coverage.
    # Shader scale .02 makes RMS slope .00165, preserving the reflected tube.
    slopes = np.clip(slopes, -1, 1) * .35
    data = np.dstack((broad, detail, slopes * .5 + .5))
    save_image(OUT / 'puddle-field.png', data, linear=True)


def folder_metadata():
    for directory in sorted([OUT] + [p for p in OUT.rglob('*') if p.is_dir()]):
        path = directory.with_name(directory.name + '.meta')
        if path.exists():
            continue
        name = directory.relative_to(OUT).as_posix() if directory != OUT else 'root'
        path.write_text('fileFormatVersion: 2\nguid: ' + guid(name + '/') + '\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n', encoding='utf-8', newline='\n')


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    plaster('wall', 'rough_plaster_broken')
    plaster('wall-alternate', 'peeling_painted_wall', rotation=2)
    plaster('lower-wall', 'peeling_painted_wall', lower=True)
    plaster('ceiling', 'peeling_painted_wall', ceiling=True, rotation=1)
    plaster('ceiling-alternate', 'rough_plaster_broken', ceiling=True, rotation=3)
    floor()
    glass_and_macro()
    puddle_field()
    folder_metadata()
    source_manifest = json.loads((ACQUIRED / 'acquisition-manifest.json').read_text(encoding='utf-8-sig'))
    credits = source_manifest['assets']
    manifest = {'finishRevision': 5, 'seed': SEED, 'generator': 'SourceArt/SchoolHorror/build_surfaces.py', 'generatorSha256': sha(Path(__file__)),
                'python': '3.11', 'numpy': '1.26.4', 'pillow': '10.4.0', 'resolution': [SIZE, SIZE],
                'normalConvention': 'Tangent +Y/OpenGL; Unity NormalMap importer. Image CCW90 rotates tangent XY to (-Y,X), matching the rotated photographed ceiling relief.',
                'packedChannels': 'Metallic red (dielectric zero), smoothness alpha, G/B zero; AO separate linear grayscale.',
                'physicalRepeatMetres': {'wall': 1.5, 'wall-alternate': 1.8, 'lower-wall': 1.8, 'ceiling': 1.8, 'ceiling-alternate': 1.5, 'floor': 2.26, 'puddle-field': 24},
                'puddleFieldChannels': 'Linear RGBA1024; R isotropic broad 2--3.4m water-placement field; G isotropic ~1m gentle breakup; BA normalized shallow water-normal XY encoded around 0.5 (decoded max +/-0.35, RMS0.0825 before shader scale0.02). Separate deterministic seed +449, seamless24m world repeat. No glass drip channels.',
                'waterPlacement': 'Dry acquired quarry-tile map is unchanged. A separate isotropic puddle-field supplies broad water placement and shallow water normals; shader controls world-space coverage and the actual planar reflection comes from SchoolPlanarReflection. Neither lighting nor reflection is painted into albedo.',
                'sourceScans': credits, 'inputs': inputs, 'outputs': outputs,
                'acquisitionManifestSha256': sha(ACQUIRED / 'acquisition-manifest.json'),
                'limits': 'New photographed CC0 plaster/organic-stain/paint/quarry-tile originals are preserved. Ceiling finish adapts photographed peeling wall paint to authored damaged ceiling geometry; it is not a capture of the target school. No painted light/reflection in albedo.'}
    (HERE / 'surface-manifest.json').write_text(json.dumps(manifest, indent=2, ensure_ascii=False) + '\n', encoding='utf-8', newline='\n')
    print(json.dumps({'status': 'SCHOOL_SURFACES_GENERATED', 'outputs': len(outputs), 'sourceInputs': len(inputs)}))


if __name__ == '__main__':
    main()
