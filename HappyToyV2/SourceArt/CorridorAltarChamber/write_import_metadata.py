"""Create missing deterministic Unity .meta files for the new chamber assets.

Only this art's named files/folders are covered; existing metadata is preserved.
The tracked postprocessor supplies full import defaults in the Unity Editor.
Run after author_models.py when intentionally adding a new absent art resource.
"""
from pathlib import Path
import hashlib

PROJECT = Path(__file__).resolve().parents[2]
NAMESPACE = 'HappyToyV2-original-distant-altar-chamber-20261008:'


def write(path, importer, body='', folder=False):
    destination = path.with_name(path.name + '.meta')
    if destination.exists(): return
    guid = hashlib.sha256((NAMESPACE + path.relative_to(PROJECT).as_posix()).encode()).hexdigest()[:32]
    text = 'fileFormatVersion: 2\nguid: ' + guid + '\n'
    if folder: text += 'folderAsset: yes\n'
    text += importer + ':\n' + body + '  userData: \n  assetBundleName: \n  assetBundleVariant: \n'
    destination.write_text(text, encoding='utf-8')


if __name__ == '__main__':
    for name in ('Assets/Resources/CorridorAltarChamber', 'Assets/Resources/CorridorAltarChamber/Chalkboard'):
        write(PROJECT / name, 'DefaultImporter', '  externalObjects: {}\n', folder=True)
    for name in ('Assets/Scripts/CorridorAltarChamber.cs', 'Assets/Editor/CorridorAltarChamberImport.cs'):
        write(PROJECT / name, 'MonoImporter', '  externalObjects: {}\n  serializedVersion: 2\n  defaultReferences: []\n  executionOrder: 0\n  icon: {instanceID: 0}\n')
    for file in sorted((PROJECT / 'Assets/Resources/CorridorAltarChamber').glob('*.fbx')):
        write(file, 'ModelImporter', '''  serializedVersion: 24600
  internalIDToNameTable: []
  externalObjects: {}
  materials:
    materialImportMode: 1
    materialName: 0
    materialSearch: 1
    materialLocation: 1
  animations:
    importConstraints: 0
    isReadable: 1
  meshes:
    globalScale: 1
    meshCompression: 0
    addColliders: 0
    importCameras: 0
    importLights: 0
    useFileUnits: 1
    bakeAxisConversion: 0
    preserveHierarchy: 1
    useFileScale: 1
  tangentSpace:
    normalSmoothAngle: 60
    normalImportMode: 0
    tangentImportMode: 3
    normalCalculationMode: 4
  importAnimation: 0
  animationType: 2
  avatarSetup: 0
''')
    for file in sorted((PROJECT / 'Assets/Resources/CorridorAltarChamber/Chalkboard').glob('*.png')):
        normal, srgb = int(file.name == 'normal.png'), int(file.name == 'albedo.png')
        write(file, 'TextureImporter', '''  internalIDToNameTable: []
  externalObjects: {}
  serializedVersion: 13
  mipmaps:
    mipMapMode: 0
    enableMipMap: 1
    sRGBTexture: %d
    linearTexture: %d
  bumpmap:
    convertToNormalMap: 0
    flipGreenChannel: 0
  isReadable: 0
  streamingMipmaps: 0
  textureFormat: 1
  maxTextureSize: 2048
  textureSettings:
    serializedVersion: 2
    filterMode: 2
    aniso: 8
    wrapU: 1
    wrapV: 1
    wrapW: 1
  nPOTScale: 0
  compressionQuality: 100
  textureType: %d
  textureShape: 1
  alphaIsTransparency: 0
  platformSettings: []
''' % (srgb, 1 - srgb, normal))
    print('CHAMBER_MISSING_METADATA_CREATED')
