from pathlib import Path
import json, sys
import numpy as np
from PIL import Image, ImageDraw, ImageFilter

folder = Path(sys.argv[1])
results = []
for i in range(3):
    if not (folder / f'lod{i}-uv.json').exists() or not list(folder.glob(f'*_lod{i}-coverage.png')):
        continue
    triangles = json.loads((folder / f'lod{i}-uv.json').read_text(encoding='utf-8'))
    mask = Image.new('L', (2048, 2048), 0)
    draw = ImageDraw.Draw(mask)
    for triangle in triangles:
        draw.polygon([(u * 2048, (1 - v) * 2048) for u, v in triangle], fill=255)
    # Require two whole pixels of clearance from an island boundary.
    interior = np.asarray(mask.filter(ImageFilter.MinFilter(5))) > 0
    path = next(folder.glob(f'*_lod{i}-coverage.png'))
    rgb = np.asarray(Image.open(path).convert('RGB'), dtype=np.uint8)
    values = rgb.mean(axis=2)
    total = int(interior.sum())
    missing = int((interior & (values < 128)).sum())
    results.append({'lod': i, 'interiorPixels': total, 'missedPixels': missing,
                    'missedPercent': 100.0 * missing / max(1, total),
                    'minimum': float(values[interior].min()),
                    'mean': float(values[interior].mean())})
print(json.dumps(results, indent=2))
