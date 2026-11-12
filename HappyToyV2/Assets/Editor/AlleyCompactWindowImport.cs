using System;
using System.IO;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;

namespace HappyToy.V2.Editor
{
    // Derive an emission mask from the generated house's original atlas.
    // Only two UV islands of the same physical window are included.
    public static class AlleyCompactWindowImport
    {
        const string Folder = "Assets/Resources/AlleyCompactBrickHouse/";
        const int Size = 1024;

        static bool Window(float u, float v)
        {
            bool paneA = u >= 190f / 1600 && u <= 267f / 1600 &&
                v >= 1f - 123f / 1600 && v <= 1f - 58f / 1600;
            bool paneB = u >= 587f / 1600 && u <= 667f / 1600 &&
                v >= 1f - 292f / 1600 && v <= 1f - 233f / 1600;
            return paneA || paneB;
        }

        // The curtain is a small distant detail; do not reimport other textures.
        public static void ApplySideCurtain()
        {
            AssetDatabase.Refresh();
            var importer = AssetImporter.GetAtPath(Folder + "side-window-curtain.png") as TextureImporter;
            if (!importer) throw new InvalidOperationException("Side-window curtain texture is missing");
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = true; importer.maxTextureSize = 512;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.wrapMode = TextureWrapMode.Clamp; importer.mipmapEnabled = true;
            importer.SaveAndReimport();
            Debug.Log("ALLEY_SIDE_CURTAIN_IMPORT_PASS");
        }

        public static void Apply()
        {
            AssetDatabase.Refresh();
            using (var stream = File.OpenRead(Folder + "albedo.png"))
            using (var hash = SHA256.Create())
            {
                var digest = BitConverter.ToString(hash.ComputeHash(stream)).Replace("-", "");
                if (digest != "92A027E5EE70E483081FDBF628E86E507862DB33B9B2A775DFE3E35766288AE1")
                    throw new InvalidOperationException("House atlas changed; review the window UV mask");
            }
            var importer = AssetImporter.GetAtPath(Folder + "albedo.png") as TextureImporter;
            if (!importer) throw new InvalidOperationException("Compact house atlas is missing");
            bool originalReadable = importer.isReadable;
            Texture2D mask = null;
            try
            {
                if (!originalReadable) { importer.isReadable = true; importer.SaveAndReimport(); }
                var atlas = AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "albedo.png");
                if (!atlas || atlas.width != atlas.height || atlas.width < 1024)
                    throw new InvalidOperationException("Compact house atlas has unexpected dimensions");
                mask = new Texture2D(Size, Size, TextureFormat.RGB24, false, false);
                var pixels = new Color32[Size * Size];
                for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                {
                    float u = (x + .5f) / Size, v = (y + .5f) / Size;
                    if (!Window(u, v)) continue;
                    var sample = atlas.GetPixelBilinear(u, v);
                    pixels[y * Size + x] = new Color32(
                        (byte)Mathf.RoundToInt(sample.r * 255),
                        (byte)Mathf.RoundToInt(sample.g * 255),
                        (byte)Mathf.RoundToInt(sample.b * 255), 255);
                }
                mask.SetPixels32(pixels); mask.Apply(false, false);
                File.WriteAllBytes(Folder + "window-emission.png", mask.EncodeToPNG());
            }
            finally
            {
                if (mask) UnityEngine.Object.DestroyImmediate(mask);
                if (!originalReadable)
                {
                    importer = AssetImporter.GetAtPath(Folder + "albedo.png") as TextureImporter;
                    if (!importer) throw new InvalidOperationException("Cannot restore compact atlas importer");
                    importer.isReadable = false; importer.SaveAndReimport();
                }
            }
            AssetDatabase.ImportAsset(Folder + "window-emission.png", ImportAssetOptions.ForceUpdate);
            var emission = AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "window-emission.png");
            var original = AssetDatabase.LoadAssetAtPath<Material>(Folder + "CompactBrickHousePbr.mat");
            if (!emission || !original) throw new InvalidOperationException("Window material source is missing");
            var variant = AssetDatabase.LoadAssetAtPath<Material>(Folder + "CompactBrickHouseWarm.mat");
            if (!variant)
            {
                variant = new Material(original);
                AssetDatabase.CreateAsset(variant, Folder + "CompactBrickHouseWarm.mat");
            }
            else variant.CopyPropertiesFromMaterial(original);
            variant.SetTexture("_EmissionMap", emission);
            variant.SetColor("_EmissionColor", new Color(.55f, .26f, .09f));
            variant.EnableKeyword("_EMISSION");
            variant.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
            EditorUtility.SetDirty(variant);
            AssetDatabase.SaveAssets();
            Debug.Log("ALLEY_COMPACT_WINDOW_IMPORT_PASS");
        }
    }
}
