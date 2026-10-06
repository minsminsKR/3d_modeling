using UnityEditor;
using UnityEngine;

namespace HappyToy.V2.EditorTools
{
    // Generated source PNGs are preserved; Unity derives the tangent normal at
    // import from height, using the same wrapped texels as the colour surface.
    public sealed class CorridorSurfaceImport : AssetPostprocessor
    {
        public static void VerifyImports()
        {
            foreach (string name in new[] { "aged-floor-v2", "aged-floor-normal-v2", "aged-ceiling-v2", "aged-ceiling-normal-v2" })
            {
                string path = "Assets/Resources/Corridor/" + name + ".png";
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (!texture) throw new System.InvalidOperationException("Surface import missing: " + path);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                if (texture.wrapMode != TextureWrapMode.Mirror || !importer.mipmapEnabled ||
                    importer.textureType != (name.Contains("-normal-") ? TextureImporterType.NormalMap : TextureImporterType.Default))
                    throw new System.InvalidOperationException("Surface import contract mismatch: " + path);
                Debug.Log("SURFACE_IMPORT_PASS " + name + " " + texture.width + "x" + texture.height);
            }
        }

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith("Assets/Resources/Corridor/aged-") || !assetPath.EndsWith("-v2.png")) return;
            var importer = (TextureImporter)assetImporter;
            bool normal = assetPath.Contains("-normal-");
            importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.textureShape = TextureImporterShape.Texture2D;
            importer.convertToNormalmap = normal; importer.heightmapScale = .025f;
            importer.sRGBTexture = !normal;
            importer.mipmapEnabled = true; importer.isReadable = false;
            importer.wrapMode = TextureWrapMode.Mirror;
            importer.filterMode = FilterMode.Trilinear; importer.anisoLevel = 8;
            importer.maxTextureSize = 2048;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
        }
    }
}
