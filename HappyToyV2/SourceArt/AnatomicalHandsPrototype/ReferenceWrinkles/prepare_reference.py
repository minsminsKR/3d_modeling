"""Extract a local, neutral-gray wrinkle-detail map from the user's photo."""
import argparse
from pathlib import Path

import numpy as np
from PIL import Image, ImageFilter

HERE = Path(__file__).resolve().parent
parser = argparse.ArgumentParser()
parser.add_argument('--work-dir', type=Path, required=True)
args = parser.parse_args()
args.work_dir.mkdir(parents=True, exist_ok=True)
photo = Image.open(HERE / 'user-hand-dorsal-20261110.jpg').convert('L')
if photo.size != (960, 1280):
    raise ValueError('Reference image size does not match recorded landmarks')
low = photo.filter(ImageFilter.GaussianBlur(12))
detail = np.clip((np.asarray(photo, dtype=np.float32) - np.asarray(low, dtype=np.float32))
                 * 2.0 + 127.5, 0, 255).astype('uint8')
Image.fromarray(detail, 'L').convert('RGB').save(args.work_dir / 'reference-detail.png')
