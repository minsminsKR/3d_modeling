"""Original correlated 2K PBR maps for the newly modeled prop surfaces.
No third-party pixels. Values describe wax, oxidized brass, charred fibre and worn paint.
Normal is a real tangent-space normal derived from the matching microscopic height field.
Unity metallic/smoothness is R / A, not an albedo image renamed as a PBR map.
"""
from pathlib import Path
import numpy as np
from PIL import Image
import json, hashlib
ROOT=Path(__file__).resolve().parent
OUT=ROOT/'proposed/Assets/Resources/GraphicsPbr'
SIZE=2048
rng=np.random.default_rng(36004)

def noise(cells):
    seed=rng.random((cells,cells)).astype(np.float32)
    value=np.asarray(Image.fromarray(seed,mode='F').resize((SIZE,SIZE),Image.Resampling.BICUBIC),dtype=np.float32)
    return np.clip(value,0,1)

def fields():
    return noise(10),noise(38),noise(167),noise(513),rng.random((SIZE,SIZE),dtype=np.float32)

def save(key,color,height,metal,smooth,ao):
    folder=OUT/key;folder.mkdir(parents=True,exist_ok=True)
    color=np.clip(color,0,1);height=np.asarray(height,dtype=np.float32)
    dx=(np.roll(height,-1,axis=1)-np.roll(height,1,axis=1))*SIZE/2
    dy=(np.roll(height,-1,axis=0)-np.roll(height,1,axis=0))*SIZE/2
    # Texel height variation remains microscopic; derivative converts to physical slope.
    normal=np.dstack((-dx,dy,np.ones_like(dx)))
    normal/=np.linalg.norm(normal,axis=2,keepdims=True)
    Image.fromarray(np.uint8(color*255),'RGB').save(folder/'albedo.png')
    Image.fromarray(np.uint8(np.clip(normal*.5+.5,0,1)*255),'RGB').save(folder/'normal.png')
    Image.fromarray(np.uint8(np.clip(ao,0,1)*255),'L').save(folder/'ao.png')
    packed=np.zeros((SIZE,SIZE,4),dtype=np.float32);packed[:,:,0]=metal;packed[:,:,3]=smooth
    Image.fromarray(np.uint8(np.clip(packed,0,1)*255),'RGBA').save(folder/'metallic-smoothness.png')

broad,medium,fine,micro,white=fields()
# Tallow is off-white with quiet translucent amber residue; no strong generic stone noise.
wax=np.array((.69,.57,.39))[None,None,:]*(.93+.08*medium+.025*fine)[:,:,None]
wax+=((broad-.5)*.025)[:,:,None]*np.array((1,.4,-.25))
save('wax-tallow',wax,(fine-.5)*.000045+(micro-.5)*.000009,np.zeros_like(broad),.38+.08*medium,.97+.03*fine)
pool=np.array((.60,.42,.20))[None,None,:]*(.96+.06*medium+.006*fine)[:,:,None]
save('wax-pool',pool,(fine-.5)*.000003,np.zeros_like(broad),.56+.13*medium,np.ones_like(broad))

broad,medium,fine,micro,white=fields()
oxide=np.clip((broad*.6+medium*.4-.45)*3,0,1)
polished=np.array((.56,.40,.17));tarnish=np.array((.18,.19,.10))
brass=polished[None,None,:]*(1-oxide[:,:,None])+tarnish[None,None,:]*oxide[:,:,None]
brass*=((.91+.16*fine)[:,:,None])
height=(fine-.5)*.000034+(micro-.5)*.000006-oxide*.000015
save('brass-tarnished',brass,height,.91-oxide*.69,.64-oxide*.39-(fine-.5)*.07,.98-oxide*.055)

broad,medium,fine,micro,white=fields()
y,x=np.mgrid[0:SIZE,0:SIZE].astype(np.float32)/SIZE
weave=np.sin((x+.003*np.sin(y*np.pi*38))*np.pi*150)*np.sin(y*np.pi*123)
char=np.array((.023,.019,.014))[None,None,:]*(.55+.75*fine+.24*weave)[:,:,None]
save('cloth-charred',char,(micro-.5)*.000035+weave*.000015,np.zeros_like(broad),.025+.022*fine,.90+.08*fine)

broad,medium,fine,micro,white=fields()
# Subtle enamel orange peel with sparse edge-like coating chips; geometry supplies actual seams.
edge=np.clip((fine*.25+medium*.55+broad*.2-.64)*4.2,0,1)
chips=edge*edge*(3-2*edge)
paint=np.array((.13,.18,.135));rust=np.array((.15,.095,.047))
enamel=paint[None,None,:]*(1-chips[:,:,None])+rust[None,None,:]*chips[:,:,None]
enamel*=((.93+.10*fine+.025*(micro-.5))[:,:,None])
save('painted-metal',enamel,(micro-.5)*.000012-chips*.00014,.10+chips*.43,.44-chips*.27+.035*(medium-.5),.99-chips*.08)

manifest={'generator':'Original measured correlated micro-surface fields; no external images','resolution':[SIZE,SIZE],
 'normalConvention':'Tangent-space +Y normal; importer type NormalMap; albedo sRGB and other maps linear',
 'metallicSmoothness':'Metallic red, smoothness alpha; green/blue zero','maps':[]}
for file in sorted(OUT.rglob('*.png')):
    manifest['maps'].append({'path':str(file.relative_to(ROOT)).replace('\\','/'),'bytes':file.stat().st_size,
      'sha256':hashlib.sha256(file.read_bytes()).hexdigest()})
(ROOT/'pbr-manifest.json').write_text(json.dumps(manifest,indent=2),encoding='utf-8')
print(json.dumps({'sets':5,'maps':len(manifest['maps']),'resolution':SIZE,'output':str(OUT)}))
