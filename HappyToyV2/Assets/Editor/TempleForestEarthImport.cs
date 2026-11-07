using System;
using UnityEditor;
using UnityEngine;

namespace HappyToy.V2.Editor
{
    // Keep the generated albedo unchanged; Unity derives a subtle tangent
    // normal from its luminance at import, with the same wrapped coordinates.
    public sealed class TempleForestEarthImport : AssetPostprocessor
    {
        const string Prefix = "Assets/Resources/TempleForest/forest-earth/forest-earth-";
        const string CandidatePrefix = "Assets/Resources/TempleForest/forest-earth-candidate/forest-earth-candidate-";
        void OnPreprocessTexture()
        {
            bool candidate = assetPath == CandidatePrefix + "albedo.png" || assetPath == CandidatePrefix + "height.png";
            if (!candidate && assetPath != Prefix + "albedo.png" && assetPath != Prefix + "height.png") return;
            var importer = (TextureImporter)assetImporter;
            bool normal = assetPath.EndsWith("height.png", StringComparison.Ordinal);
            importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.convertToNormalmap = normal;
            importer.heightmapScale = .025f;
            importer.sRGBTexture = !normal;
            importer.mipmapEnabled = true;
            // CPU seam checks use the real imported albedo; the normal remains GPU-only.
            importer.isReadable = !normal;
            importer.wrapMode = candidate ? TextureWrapMode.Repeat : TextureWrapMode.Mirror;
            importer.filterMode = FilterMode.Trilinear;
            importer.anisoLevel = 8;
            importer.maxTextureSize = candidate ? 2048 : 1024;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
        }

        public static void VerifyCandidateImports()
        {
            foreach (string name in new[] { "albedo", "height" })
            {
                string path = CandidatePrefix + name + ".png";
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                bool normal = name == "height";
                if (!importer || !texture || texture.width != 2048 || texture.height != 2048 ||
                    importer.maxTextureSize != 2048 || importer.textureType != (normal ? TextureImporterType.NormalMap : TextureImporterType.Default) ||
                    importer.convertToNormalmap != normal || importer.wrapMode != TextureWrapMode.Repeat || !importer.mipmapEnabled)
                    throw new InvalidOperationException("Forest earth candidate texture import failed: " + path);
            }
            Debug.Log("TEMPLE_FOREST_EARTH_CANDIDATE_IMPORT_PASS");
        }

        public static void VerifyImports()
        {
            foreach (string name in new[] { "albedo", "height" })
            {
                string path = Prefix + name + ".png";
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (!importer || !AssetDatabase.LoadAssetAtPath<Texture2D>(path) ||
                    importer.textureType != (name == "height" ? TextureImporterType.NormalMap : TextureImporterType.Default) ||
                    importer.wrapMode != TextureWrapMode.Mirror || !importer.mipmapEnabled)
                    throw new InvalidOperationException("Forest earth texture import failed: " + path);
            }
            Debug.Log("TEMPLE_FOREST_EARTH_IMPORT_PASS");
        }
    }
}
