using System;
using UnityEditor;
using UnityEngine;

namespace HappyToy.V2.Editor
{
    public static class AlleyStorefrontSignImport
    {
        public static void Apply()
        {
            const string path = "Assets/Resources/AlleyStorefrontInterior/woam-front-sign.png";
            AssetDatabase.Refresh();
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (!importer) throw new InvalidOperationException("Shop sign texture missing");
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = true; importer.maxTextureSize = 1024;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.wrapMode = TextureWrapMode.Clamp; importer.mipmapEnabled = true;
            importer.SaveAndReimport();
            Debug.Log("ALLEY_FRONT_SIGN_IMPORT_PASS");
        }
    }
}
