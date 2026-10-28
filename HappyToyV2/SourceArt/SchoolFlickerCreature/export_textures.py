"""Extract the actual GLB's PBR images for Unity; pillow and numpy required."""
from pathlib import Path
from PIL import Image
import io, json, struct
import numpy as np
HERE=Path(__file__).resolve().parent
source=HERE/'Generated/rigged.glb'
out=HERE.parents[1]/'Assets/Resources/SchoolFlickerCreature'
out.mkdir(parents=True, exist_ok=True)
with source.open('rb') as f:
    magic,version,total=struct.unpack('<4sII',f.read(12))
    length,kind=struct.unpack('<I4s',f.read(8)); data=json.loads(f.read(length))
    binsize,bintype=struct.unpack('<I4s',f.read(8)); binstart=f.tell()
    images=[]
    for entry in data['images']:
        view=data['bufferViews'][entry['bufferView']]; f.seek(binstart+view.get('byteOffset',0)); images.append(Image.open(io.BytesIO(f.read(view['byteLength']))).convert('RGBA'))
    textures=data['textures']; mat=data['materials'][0]['pbrMetallicRoughness']
    base=images[textures[mat['baseColorTexture']['index']]['source']]
    orm=images[textures[mat['metallicRoughnessTexture']['index']]['source']]
    base.resize((4096,4096),Image.Resampling.LANCZOS).save(out/'creature-base.png')
    pixels=np.asarray(orm.resize((4096,4096),Image.Resampling.LANCZOS))
    output=np.empty_like(pixels); output[:,:,0]=pixels[:,:,2]; output[:,:,1]=pixels[:,:,2]; output[:,:,2]=pixels[:,:,2]; output[:,:,3]=255-pixels[:,:,1]
    Image.fromarray(output,'RGBA').save(out/'creature-metallic-smoothness.png')
print('CREATURE_TEXTURES_PASS')
