"""Extract game-sized PBR maps; generated original 8K images stay in the GLB."""
import hashlib
import json
import sys
from pathlib import Path

HERE=Path(__file__).resolve().parent
PROJECT=HERE.parents[1]
sys.path.insert(0,str(PROJECT/'SourceArt/CreatureStudio/tools'))

def export(destination,report_path):
    from PIL import Image,ImageOps
    packed=Image.open(destination/'game-orm.png').convert('RGB')
    _,roughness,metallic=packed.split();zero=Image.new('L',packed.size,0)
    temporary=destination/'.metallic-smoothness.exporting.png'
    Image.merge('RGBA',(metallic,zero,zero,ImageOps.invert(roughness))).save(temporary)
    temporary.replace(destination/'metallic-smoothness.png')
    result={'method':'pack the actual BVH-reprojected game ORM into Unity metallic/smoothness','atlas':list(packed.size)}
    result['files']={p.name:hashlib.sha256(p.read_bytes()).hexdigest() for p in destination.glob('*.png')}
    report_path.write_text(json.dumps(result,indent=2)+'\n',encoding='utf-8')
    print(json.dumps(result,indent=2))


if __name__=='__main__':
    export(PROJECT/'Assets/Resources/Lady',HERE/'pbr-export-report.json')
    far=PROJECT/'Assets/Resources/Lady/Far'
    if far.is_dir():export(far,HERE/'far-pbr-export-report.json')
