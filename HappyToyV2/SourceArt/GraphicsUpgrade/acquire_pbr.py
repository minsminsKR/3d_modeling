from pathlib import Path
import urllib.request
import hashlib
import json
import concurrent.futures
import time
from PIL import Image
import numpy as np

ROOT = Path(__file__).resolve().parent
DEST = ROOT / 'staged/Assets/Resources/GraphicsPbr'
SETS = {'wood-floor': 'wood_floor_worn', 'wood-aged': 'wood_table_worn',
        'plaster-damp': 'worn_plaster_wall', 'concrete-rough': 'concrete_floor_02',
        'ceramic-tile': 'floor_tiles_06', 'metal-rust': 'rusty_metal_02'}
UA = 'HappyToyV2/3.0'

def fetch(url):
    for attempt in range(3):
        try:
            with urllib.request.urlopen(urllib.request.Request(url, headers={'User-Agent': UA}), timeout=60) as r:
                return r.read()
        except Exception:
            if attempt == 2:
                raise
            time.sleep(1 + attempt)

def metadata(slug, kind):
    path = ROOT / f'{slug}-{kind}.json'
    if path.exists():
        return json.loads(path.read_text(encoding='utf-8-sig'))
    value = json.loads(fetch(f'https://api.polyhaven.com/{kind}/{slug}'))
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2), encoding='utf-8')
    return value

def acquire(key, slug):
    info = metadata(slug, 'info')
    files = metadata(slug, 'files')
    local = ROOT / 'source' / slug
    local.mkdir(parents=True, exist_ok=True)
    dst = DEST / key
    dst.mkdir(parents=True, exist_ok=True)
    sources = {}
    for map_name in ('Diffuse', 'nor_gl', 'arm'):
        variants = files[map_name]['2k']
        fmt = 'png' if 'png' in variants else 'jpg'
        record = variants[fmt]
        source = local / (map_name + '.' + fmt)
        if not source.exists():
            source.write_bytes(fetch(record['url']))
        data = source.read_bytes()
        if len(data) != record['size'] or hashlib.md5(data).hexdigest() != record['md5']:
            raise RuntimeError(f'Official size/MD5 mismatch {slug}/{map_name}')
        sources[map_name] = {'url': record['url'], 'bytes': len(data), 'md5': record['md5'],
                             'sha256': hashlib.sha256(data).hexdigest()}
    diffuse = Image.open(local / ('Diffuse.png' if (local/'Diffuse.png').exists() else 'Diffuse.jpg')).convert('RGB')
    normal = Image.open(local / ('nor_gl.png' if (local/'nor_gl.png').exists() else 'nor_gl.jpg')).convert('RGB')
    arm = Image.open(local / ('arm.png' if (local/'arm.png').exists() else 'arm.jpg')).convert('RGB')
    if diffuse.size != normal.size or diffuse.size != arm.size:
        raise RuntimeError(f'Map sizes differ {slug}')
    diffuse.save(dst / 'albedo.png')
    normal.save(dst / 'normal.png')
    channels = np.asarray(arm)
    Image.fromarray(channels[:, :, 0], 'L').save(dst / 'ao.png')
    packed = np.zeros((arm.height, arm.width, 4), np.uint8)
    packed[:, :, 0] = channels[:, :, 2]
    packed[:, :, 3] = 255 - channels[:, :, 1]
    Image.fromarray(packed, 'RGBA').save(dst / 'metallic-smoothness.png')
    dims = info.get('dimensions')
    span = [float(v)/1000 for v in dims[:2]] if dims else [2, 2]
    result = {'key': key, 'slug': slug, 'sourcePage': f'https://polyhaven.com/a/{slug}',
              'license': 'CC0-1.0', 'licensePage': 'https://polyhaven.com/license',
              'authors': info.get('authors'), 'tileMetres': span, 'pixels': list(diffuse.size),
              'sources': sources,
              'derivation': 'Official 2k diffuse/OpenGL tangent normal converted to RGB8 PNG. glTF ARM red->AO; blue->URP metallic red; 255-green roughness->URP smoothness alpha. Packed green/blue zero.',
              'outputs': {p.name: {'bytes': p.stat().st_size, 'sha256': hashlib.sha256(p.read_bytes()).hexdigest()} for p in dst.glob('*.png')}}
    print(json.dumps({'downloaded': key, 'slug': slug, 'pixels': result['pixels'], 'tileMetres': span}), flush=True)
    return result

def main():
    DEST.mkdir(parents=True, exist_ok=True)
    with concurrent.futures.ThreadPoolExecutor(max_workers=3) as pool:
        results = list(pool.map(lambda kv: acquire(*kv), SETS.items()))
    manifest = {'assetLicense': 'CC0-1.0', 'apiCredit': 'Powered by Poly Haven (https://polyhaven.com)',
                'maps': results, 'scope': 'Authentic downloaded source PBR maps and correlated Unity channel conversion; actual game render verification remains separate.'}
    (ROOT / 'pbr-source-manifest.json').write_text(json.dumps(manifest, ensure_ascii=False, indent=2), encoding='utf-8')
    print('ALL_PBR_MAPS_CHECKSUM_VERIFIED', flush=True)

if __name__ == '__main__':
    main()
