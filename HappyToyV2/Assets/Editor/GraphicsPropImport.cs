using UnityEditor;

namespace HappyToy.V2.EditorTools
{
    public sealed class GraphicsPropImport : AssetPostprocessor
    {
        void OnPreprocessModel()
        {
            if (!assetPath.StartsWith("Assets/Resources/GraphicsUpgrade/Props/",System.StringComparison.Ordinal)) return;
            var importer=(ModelImporter)assetImporter;
            importer.importNormals=ModelImporterNormals.Import;
            importer.importTangents=ModelImporterTangents.CalculateMikk;
            importer.importAnimation=false; importer.addCollider=false;
            importer.globalScale=1; importer.useFileScale=true;
            importer.importCameras=false; importer.importLights=false;
            importer.meshCompression=ModelImporterMeshCompression.Off;
            importer.materialImportMode=ModelImporterMaterialImportMode.ImportStandard;
        }
    }
}
