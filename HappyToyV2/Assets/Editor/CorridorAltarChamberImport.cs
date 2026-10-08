using UnityEditor;

namespace HappyToy.V2.EditorTools
{
    // Tracked import rules keep native batchable models in physical metres. The
    // original scene and reused authored furniture imports are never changed.
    public sealed class CorridorAltarChamberImport : AssetPostprocessor
    {
        void OnPreprocessModel()
        {
            if (!assetPath.StartsWith("Assets/Resources/CorridorAltarChamber/", System.StringComparison.Ordinal)) return;
            var importer = (ModelImporter)assetImporter;
            importer.importNormals = ModelImporterNormals.Import;
            importer.importTangents = ModelImporterTangents.CalculateMikk;
            importer.isReadable = true; importer.importAnimation = false; importer.addCollider = false;
            importer.importCameras = false; importer.importLights = false; importer.globalScale = 1;
            importer.useFileScale = true; importer.meshCompression = ModelImporterMeshCompression.Off;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
        }
        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith("Assets/Resources/CorridorAltarChamber/Chalkboard/", System.StringComparison.Ordinal)) return;
            var importer = (TextureImporter)assetImporter;
            bool normal = assetPath.EndsWith("/normal.png", System.StringComparison.Ordinal);
            importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture = assetPath.EndsWith("/albedo.png", System.StringComparison.Ordinal);
            importer.mipmapEnabled = true; importer.maxTextureSize = 2048; importer.anisoLevel = 8;
            importer.wrapMode = UnityEngine.TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
        }
    }
}
