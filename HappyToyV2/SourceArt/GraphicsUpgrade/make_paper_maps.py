from pathlib import Path
import hashlib
import json
import numpy as np
from PIL import Image, ImageDraw, ImageFilter

root = Path(__file__).resolve().parent
dst = root / 'staged/Assets/Resources/GraphicsPbr/paper-aged'
dst.mkdir(parents=True, exist_ok=True)
size = 2048
rng = np.random.default_rng(736129)
fibres = Image.new('L', (size, size), 128)
draw = ImageDraw.Draw(fibres)
for _ in range(68000):
    x, y = rng.integers(0, size, 2)
    angle = rng.uniform(0, np.pi*2)
    length = rng.uniform(2, 19)
    dx, dy = np.cos(angle)*length, np.sin(angle)*length
    colour = int(rng.integers(84, 165))
    # Periodic wrapped fibre strokes keep opposite texture edges continuous.
    for ox in (-size, 0, size):
        for oy in (-size, 0, size):
            if -20 < x+ox < size+20 and -20 < y+oy < size+20:
                draw.line((x+ox,y+oy,x+dx+ox,y+dy+oy), fill=colour, width=1)
height = np.asarray(fibres.filter(ImageFilter.GaussianBlur(.55)), dtype=np.float32)/255
noise = rng.normal(0,.006,(size,size)).astype(np.float32)
height = np.clip(height+noise,0,1)
soft = np.asarray(fibres.resize((64,64)).filter(ImageFilter.GaussianBlur(2)).resize((size,size),Image.Resampling.BICUBIC),dtype=np.float32)/255
tone = (height-.5)*.19+(soft-.5)*.32
rgb = np.clip(np.array([224,218,199],dtype=np.float32)[None,None,:]+tone[:,:,None]*255,0,255).astype(np.uint8)
Image.fromarray(rgb,'RGB').save(dst/'albedo.png')
gx=(np.roll(height,-1,axis=1)-np.roll(height,1,axis=1))*1.7
gy=(np.roll(height,-1,axis=0)-np.roll(height,1,axis=0))*1.7
normal=np.stack((-gx,-gy,np.ones_like(height)),axis=2)
normal/=np.linalg.norm(normal,axis=2,keepdims=True)
Image.fromarray(np.round((normal*.5+.5)*255).clip(0,255).astype(np.uint8),'RGB').save(dst/'normal.png')
Image.fromarray(np.clip(249-np.maximum(0,.5-height)*28,0,255).astype(np.uint8),'L').save(dst/'ao.png')
packed=np.zeros((size,size,4),dtype=np.uint8)
packed[:,:,3]=np.clip(12+(height-.5)*15,4,22).astype(np.uint8)
Image.fromarray(packed,'RGBA').save(dst/'metallic-smoothness.png')
manifest={'key':'paper-aged','origin':'Original deterministic torn-pulp/washi microfibre material, authored for HappyToyV2',
          'author':'HappyToyV2 development','tileMetres':[.6,.6],'pixels':[size,size],
          'method':'Periodic oriented pulp fibres produce correlated albedo/height/normal/roughness. Finite differences use wrapped edges. No source images copied; original procedural data maps, not a photograph.',
          'seed':736129,'sha256':{p.name:hashlib.sha256(p.read_bytes()).hexdigest() for p in dst.glob('*.png')}}
(root/'paper-source-manifest.json').write_text(json.dumps(manifest,indent=2),encoding='utf-8')
print(json.dumps({'authored':'paper-aged','maps':4,'pixels':size,'tileMetres':.6}))
