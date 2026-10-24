"""Derive the game normal from the retained A100 pore/crease colour detail.
Python 3.11, NumPy 1.26.4, Pillow 10.2.0. Original generated PBR stays unchanged.
This micro normal supplements the actual shaped skin, rather than replacing it.
"""
from pathlib import Path
from PIL import Image, ImageFilter
import numpy as np
import json,hashlib
ROOT=Path(__file__).resolve().parents[2]/'Assets/Resources/PlayerItems/hand'
image=Image.open(ROOT/'albedo.png').convert('RGB')
image.thumbnail((4096,4096),Image.Resampling.LANCZOS)
gray=image.convert('L')
height=np.asarray(gray,dtype=np.float32)/255-np.asarray(gray.filter(ImageFilter.GaussianBlur(3)),dtype=np.float32)/255
dy,dx=np.gradient(height)
normal=np.stack((-dx*2.0,dy*2.0,np.ones_like(dx)),axis=-1)
normal/=np.linalg.norm(normal,axis=-1,keepdims=True)
def save(image,name):
    destination=ROOT/name;temporary=ROOT/(destination.stem+'-export.png')
    image.save(temporary)
    with Image.open(temporary) as check:check.verify()
    temporary.replace(destination)
save(Image.fromarray(np.uint8(np.clip(normal*.5+.5,0,1)*255)),'skin-normal.png')
rough=np.clip(.58+np.abs(height)*.8,.54,.72)
packed=np.stack((np.ones_like(rough),rough,np.zeros_like(rough)),axis=-1)
save(Image.fromarray(np.uint8(packed*255)),'metalrough.png')
report=json.loads((ROOT/'asset.json').read_text(encoding='utf-8'))
for name,path in [('normal',ROOT/'skin-normal.png'),('metallicRoughness',ROOT/'metalrough.png')]:
    report['outputSha256'][name]=hashlib.sha256(path.read_bytes()).hexdigest()
(ROOT/'asset.json').write_text(json.dumps(report,indent=2)+'\n',encoding='utf-8')
(Path(__file__).resolve().parent/'finish-report.json').write_text(json.dumps(report,indent=2)+'\n',encoding='utf-8')
print('HAND_NORMAL_PASS',image.size)
