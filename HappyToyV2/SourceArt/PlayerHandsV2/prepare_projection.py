"""Prepare photographic projection borders; Python 3.11/Pillow/NumPy/SciPy.
Keeps the generated reference photographs unchanged. This deterministic padding
prevents grey background bleeding onto the UV seams of the finished 3D hand.
"""
from pathlib import Path
from PIL import Image,ImageFilter
import numpy as np
from scipy.ndimage import distance_transform_edt,binary_erosion
ROOT=Path(__file__).resolve().parent
(ROOT/'Derived').mkdir(exist_ok=True)
for name in ('neutral-hand','dorsal-hand'):
    pixels=np.array(Image.open(ROOT/'References'/(name+'.png')).convert('RGB'))
    p=pixels.astype(np.float32)
    # Discard antialiased background/skin edge pixels as well as the backdrop.
    # Otherwise their pale grey casts become long striped borders on side UVs.
    mask=(p[:,:,0]>p[:,:,1]*1.15)&(p[:,:,0]>p[:,:,2]*1.25)
    mask=binary_erosion(mask,iterations=6)
    indices=distance_transform_edt(~mask,return_distances=False,return_indices=True)
    nearest=pixels[indices[0],indices[1]]
    soft=np.asarray(Image.fromarray(nearest).filter(ImageFilter.GaussianBlur(12)))
    weight=np.clip(distance_transform_edt(mask)/6,0,1)[:,:,None]
    padded=np.uint8(pixels*weight+soft*(1-weight))
    Image.fromarray(padded).save(ROOT/'Derived'/(name+'-projection.png'))
print('HAND_PROJECTION_INPUTS_PASS')
