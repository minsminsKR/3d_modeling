"""Convert glTF metallic/roughness into Unity URP metallic/smoothness.

Usage: python convert_gltf_material.py input.png output.png
The glTF green channel is roughness and blue is metallic; Unity uses red for
metallic and alpha for smoothness. Green and blue output channels remain zero.
"""
from pathlib import Path
import sys
import numpy as np
from PIL import Image

if len(sys.argv) != 3:
    raise SystemExit(__doc__)
source, destination = map(Path, sys.argv[1:])
image = np.asarray(Image.open(source).convert('RGB'))
output = np.zeros((*image.shape[:2], 4), dtype=np.uint8)
output[:, :, 0] = image[:, :, 2]
output[:, :, 3] = 255 - image[:, :, 1]
destination.parent.mkdir(parents=True, exist_ok=True)
Image.fromarray(output, 'RGBA').save(destination)
print('Wrote URP metallic/smoothness:', destination)
