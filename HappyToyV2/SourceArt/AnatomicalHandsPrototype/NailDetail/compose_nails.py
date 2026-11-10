"""Combine the baked CC0 nail color with the existing hand atlas."""
import argparse
from pathlib import Path
from PIL import Image, ImageFilter

HERE = Path(__file__).resolve().parent
parser = argparse.ArgumentParser()
parser.add_argument('--work-dir', required=True, type=Path)
parser.add_argument('--output', required=True, type=Path)
args = parser.parse_args()
mask = Image.open(args.work_dir / 'bake-mask.png').convert('L')
color = Image.open(args.work_dir / 'bake-color.png').convert('RGB')
base = Image.open(HERE.parent / 'draft-skin-2048-v7.png').convert('RGB')
if mask.size != base.size or color.size != base.size or mask.getbbox() is None:
    raise ValueError('Nail bake is missing or has the wrong dimensions')
Image.composite(color, base, mask.filter(ImageFilter.GaussianBlur(1.1))).save(args.output)
