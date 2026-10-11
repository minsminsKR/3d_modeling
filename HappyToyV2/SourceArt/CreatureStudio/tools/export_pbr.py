"""Extract reviewed generated PBR images without changing the editable 8K source."""
import argparse
import io
import json
from pathlib import Path
from PIL import Image, ImageOps
from glb_io import read


def export(source, destination, size=2048, material_index=0):
    document, binary = read(source)
    destination = Path(destination)
    destination.mkdir(parents=True, exist_ok=True)

    def texture(index):
        item = document['images'][document['textures'][index]['source']]
        if 'bufferView' not in item:
            raise ValueError('Only embedded GLB texture images are accepted')
        view = document['bufferViews'][item['bufferView']]
        image = Image.open(io.BytesIO(binary[view.get('byteOffset', 0):view.get('byteOffset', 0)+view['byteLength']]))
        image.thumbnail((size, size), Image.Resampling.LANCZOS)
        return image

    material = document['materials'][material_index]
    pbr = material['pbrMetallicRoughness']
    if 'baseColorTexture' not in pbr:
        raise ValueError('New generated mesh has no base-color image; do not substitute old creature textures')
    albedo = texture(pbr['baseColorTexture']['index']).convert('RGBA')
    albedo.save(destination/'albedo.png')
    if 'metallicRoughnessTexture' in pbr:
        packed = texture(pbr['metallicRoughnessTexture']['index']).convert('RGB')
        _, roughness, metallic = packed.split()
    else:
        roughness = Image.new('L', albedo.size, round(255*pbr.get('roughnessFactor', 1)))
        metallic = Image.new('L', albedo.size, round(255*pbr.get('metallicFactor', 1)))
    zero = Image.new('L', metallic.size, 0)
    Image.merge('RGBA', (metallic, zero, zero, ImageOps.invert(roughness))).save(destination/'metallic-smoothness.png')
    result = {'albedo': list(albedo.size), 'metallicSmoothness': list(metallic.size),
              'normalSource': 'high-to-low-geometry-bake-required', 'sourceMaterial': material_index}
    if 'normalTexture' in material:
        texture(material['normalTexture']['index']).save(destination/'source-normal.png')
        result['normalSource'] = 'source tangent normal available; final LOD should still bake the changed tangent basis'
    if 'occlusionTexture' in material:
        texture(material['occlusionTexture']['index']).convert('RGB').split()[0].save(destination/'ao.png')
    return result


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('source', type=Path)
    parser.add_argument('destination', type=Path)
    parser.add_argument('--size', type=int, choices=[2048, 4096], default=2048)
    parser.add_argument('--material', type=int, default=0)
    args = parser.parse_args()
    print(json.dumps(export(args.source, args.destination, args.size, args.material), indent=2))
