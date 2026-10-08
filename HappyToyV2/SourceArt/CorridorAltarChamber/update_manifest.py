"""Record the chamber's required source/art inventory without reading caches."""
from pathlib import Path
import hashlib
import json

HERE = Path(__file__).resolve().parent
PROJECT = HERE.parents[1]


if __name__ == '__main__':
    models = json.loads((HERE / 'model-manifest.json').read_text())
    textures = json.loads((HERE / 'texture-manifest.json').read_text())
    files = [PROJECT / 'Assets/Scripts/CorridorAltarChamber.cs', PROJECT / 'Assets/Editor/CorridorAltarChamberImport.cs']
    files += sorted((PROJECT / 'Assets/Resources/CorridorAltarChamber').rglob('*.fbx'))
    files += sorted((PROJECT / 'Assets/Resources/CorridorAltarChamber').rglob('*.png'))
    files += sorted(HERE.glob('*.py')) + sorted(HERE.glob('*.md')) + sorted((HERE / 'Models').glob('*.blend'))
    result = dict(
        title='Distant classroom five-memory offering chamber',
        authored=True, thirdPartyMeshesAdded=False, downloadedImagesAdded=False,
        authoringTools=dict(blender='4.0.2', python='3.11.7', Pillow='10.2.0', numpy='1.26.4'),
        contract=dict(roomMetres=[12, 3.4, 10], entranceLocal=[0, 0, -5], focalLocal=[0, 1.05, 2.8],
                      approachLocal=[0, .03, 1.1], offeringTransformLocal=[0, .9, 2.8],
                      offeringColliderCenterLocal=[0, .555, 2.8], offeringColliderMetres=[2.22, 1.11, 1.14],
                      clearCentreHalfWidthMetres=1.6, stableId='corridor-offering', interaction='Kind.Exit',
                      mainFloorShellOwnedBy='CorridorRun', navigation='Final caller PhysicsColliders bake after Prepare',
                      materials='Original classroom board plus reused attributed physical Resources; all props own their runtime materials'),
        newModels=[dict(key=asset['key'], editableSource=asset['source'], resource=asset['fbx'], triangles=asset['triangles'])
                   for asset in models['assets']],
        originalChalkboardTextures=textures['images'],
        reusedTrackedDependencies=[
            'Assets/Art/Finished/classroom-desk.fbx', 'SourceArt/classroom-desk.blend',
            'Assets/Art/Finished/classroom-chair.fbx', 'SourceArt/classroom-chair.blend',
            'Tools/build_classroom_furniture.py', 'Assets/Annex/SchoolAnnex.unity',
            'Assets/Resources/CorridorFurnishings/archive-shelf.fbx',
            'Assets/Resources/CorridorFurnishings/writing-set.fbx',
            'SourceArt/CorridorFurnishings/Models/archive-shelf.blend',
            'SourceArt/CorridorFurnishings/Models/writing-set.blend',
            'SourceArt/CorridorFurnishings/author_corridor_furnishings.py',
            'Assets/Resources/GraphicsUpgrade/Props/paper-lantern.fbx',
            'SourceArt/GraphicsUpgrade/Props/paper-lantern.blend',
            'SourceArt/GraphicsUpgrade/author_props_v3.py',
            'Assets/Resources/GraphicsPbr/', 'ThirdParty/Graphics/',
            'SourceArt/GraphicsUpgrade/pbr-source-manifest.json',
            'Assets/Resources/Fonts/Korean.ttf', 'Assets/Resources/Fonts/OFL.txt', 'Assets/Resources/Fonts/SOURCE.txt',
            'Assets/Resources/WorldSignText.shader', 'Assets/Scripts/GraphicsSurfaceLibrary.cs',
            'Assets/Scripts/CorridorFurnitureLibrary.cs', 'Assets/Scripts/GraphicsPropLibrary.cs', 'Assets/Scripts/AnnexSignFont.cs'],
        scope='Required editable/runtime source inventory and art contract; external source/FBX proof and native import/render/navigation acceptance are separate.',
        files=[])
    for path in sorted(set(files)):
        result['files'].append(dict(file=path.relative_to(PROJECT).as_posix(), bytes=path.stat().st_size,
                                    sha256=hashlib.sha256(path.read_bytes()).hexdigest()))
    (HERE / 'asset-manifest.json').write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding='utf-8')
    print('ALTAR_REQUIRED_SOURCE_ART_INVENTORY_READY', len(result['files']))
