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
        void OnPreprocessTexture()
        {
            if (assetPath != Prefix + "albedo.png" && assetPath != Prefix + "height.png") return;
            var importer = (TextureImporter)assetImporter;
            bool normal = assetPath.EndsWith("height.png", StringComparison.Ordinal);
            importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.convertToNormalmap = normal;
            importer.heightmapScale = .025f;
            importer.sRGBTexture = !normal;
            importer.mipmapEnabled = true;
            // CPU seam checks use the real imported albedo; the normal remains GPU-only.
            importer.isReadable = !normal;
            importer.wrapMode = TextureWrapMode.Mirror;
            importer.filterMode = FilterMode.Trilinear;
            importer.anisoLevel = 8;
            importer.maxTextureSize = 1024;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
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
