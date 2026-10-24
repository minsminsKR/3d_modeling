"""Subtle tangent-space skin relief derived from the preserved webapp albedo.
Python 3.11, Pillow 10.2.0, NumPy: run after finish_player_items.py --only=hand.
This is photographic texture finishing, not a replacement/generated base mesh.
"""
from pathlib import Path
import numpy as np
from PIL import Image, ImageFilter

root=Path(__file__).resolve().parents[2]/'Assets/Resources/PlayerItems/hand'
source=Image.open(root/'albedo.png').convert('RGB')
gray=source.convert('L')
height=np.asarray(gray,dtype=np.float32)/255-np.asarray(gray.filter(ImageFilter.GaussianBlur(5)),dtype=np.float32)/255
dy,dx=np.gradient(height)
normal=np.stack((-dx*1.4,dy*1.4,np.ones_like(dx)),axis=-1)
normal/=np.linalg.norm(normal,axis=-1,keepdims=True)
Image.fromarray(np.uint8(np.clip(normal*.5+.5,0,1)*255)).save(root/'skin-normal.png')
Image.new('RGB',(1,1),(255,255,255)).save(root/'nail-white.png')
print('Preserved albedo-derived skin micro relief:',root/'skin-normal.png')
