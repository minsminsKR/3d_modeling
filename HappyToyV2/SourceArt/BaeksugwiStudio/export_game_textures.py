"""Convert glTF metallic(B)/roughness(G) to URP metallic(R)/smoothness(A)."""
from pathlib import Path
import io
from PIL import Image,ImageOps
from glb_io import read
HERE=Path(__file__).resolve().parent
out=HERE.parents[1]/'Assets/Resources/Baeksugwi'
out.mkdir(parents=True,exist_ok=True)
doc,binary=read(HERE/'Baeksugwi_Run.glb')
def texture(index):
    image=doc['images'][doc['textures'][index]['source']];view=doc['bufferViews'][image['bufferView']]
    im=Image.open(io.BytesIO(binary[view.get('byteOffset',0):view.get('byteOffset',0)+view['byteLength']])).convert('RGB')
    im.thumbnail((2048,2048),Image.Resampling.LANCZOS)
    return im
pbr=doc['materials'][0]['pbrMetallicRoughness']
texture(pbr['baseColorTexture']['index']).save(out/'albedo.png')
packed=texture(pbr['metallicRoughnessTexture']['index']);r,g,b=packed.split()
zero=Image.new('L',packed.size,0)
Image.merge('RGBA',(b,zero,zero,ImageOps.invert(g))).save(out/'metallic-smoothness.png')
print('UNITY_PBR_MAPS_EXPORTED',flush=True)
