"""Extract the original metallic/roughness image from a self-contained GLB.

Usage: python extract_gltf_metallic_roughness.py model.glb output.png
Preserves the original encoded bytes; no color or channel conversion.
"""
from pathlib import Path
import hashlib
import json
import struct
import sys

if len(sys.argv) != 3:
    raise SystemExit(__doc__)
source, target = map(Path, sys.argv[1:])
with source.open('rb') as stream:
    magic, version, length = struct.unpack('<4sII', stream.read(12))
    if magic != b'glTF' or version != 2:
        raise ValueError('Expected glTF 2 GLB')
    json_size, chunk_type = struct.unpack('<I4s', stream.read(8))
    if chunk_type != b'JSON': raise ValueError('Missing JSON chunk')
    document = json.loads(stream.read(json_size))
    binary_size, chunk_type = struct.unpack('<I4s', stream.read(8))
    if chunk_type != b'BIN\x00': raise ValueError('Missing binary chunk')
    binary_start = stream.tell()
    materials = document.get('materials', [])
    if len(materials) != 1: raise ValueError('Expected exactly one material')
    pbr = materials[0]['pbrMetallicRoughness']
    texture = document['textures'][pbr['metallicRoughnessTexture']['index']]
    image = document['images'][texture['source']]
    if image.get('mimeType') != 'image/png': raise ValueError('Expected an embedded PNG')
    view = document['bufferViews'][image['bufferView']]
    if view.get('buffer', 0) != 0: raise ValueError('Expected the GLB binary buffer')
    offset = view.get('byteOffset', 0)
    size = view['byteLength']
    if offset < 0 or offset + size > binary_size: raise ValueError('Image exceeds binary chunk')
    stream.seek(binary_start + offset)
    data = stream.read(size)
    if len(data) != size or not data.startswith(b'\x89PNG\r\n\x1a\n'):
        raise ValueError('Incomplete embedded PNG')
target.parent.mkdir(parents=True, exist_ok=True)
target.write_bytes(data)
print('EXTRACTED_GLTF_METALLIC_ROUGHNESS', len(data), hashlib.sha256(data).hexdigest())
