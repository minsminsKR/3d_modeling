using UnityEditor;

namespace HappyToy.V2.EditorTools
{
    public sealed class CorridorAltarImport : AssetPostprocessor
    {
        void OnPreprocessModel()
        {
            if(assetPath!="Assets/Resources/Corridor/seal-altar-v2.fbx") return;
            var importer=(ModelImporter)assetImporter;
            importer.importNormals=ModelImporterNormals.Import;
            importer.importTangents=ModelImporterTangents.CalculateMikk;
            importer.importAnimation=false;importer.addCollider=false;
            importer.globalScale=1;importer.useFileScale=true;
            importer.materialImportMode=ModelImporterMaterialImportMode.ImportStandard;
        }
    }
}
