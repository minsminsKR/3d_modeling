"""Extract game-sized PBR maps; generated original 8K images stay in the GLB."""
import hashlib
import json
import sys
from pathlib import Path

HERE=Path(__file__).resolve().parent
PROJECT=HERE.parents[1]
sys.path.insert(0,str(PROJECT/'SourceArt/CreatureStudio/tools'))

if __name__=='__main__':
    # Import by explicit file to avoid this entry point's identical module name.
    destination=PROJECT/'Assets/Resources/Lady'
    from PIL import Image,ImageOps
    packed=Image.open(destination/'game-orm.png').convert('RGB')
    _,roughness,metallic=packed.split();zero=Image.new('L',packed.size,0)
    Image.merge('RGBA',(metallic,zero,zero,ImageOps.invert(roughness))).save(destination/'metallic-smoothness.png')
    result={'method':'pack the actual BVH-reprojected game ORM into Unity metallic/smoothness','atlas':[2048,2048]}
    result['files']={p.name:hashlib.sha256(p.read_bytes()).hexdigest() for p in destination.glob('*.png')}
    (HERE/'pbr-export-report.json').write_text(json.dumps(result,indent=2)+'\n',encoding='utf-8')
    print(json.dumps(result,indent=2))
