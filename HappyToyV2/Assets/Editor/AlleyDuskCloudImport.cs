using System;
using UnityEditor;
using UnityEngine;

namespace HappyToy.V2.Editor
{
    public static class AlleyDuskCloudImport
    {
        // Explicitly target this one asset; do not globally reimport textures.
        public static void Apply()
        {
            const string path = "Assets/Resources/AlleyDusk/clouds.png";
            var texture = AssetImporter.GetAtPath(path) as TextureImporter;
            if (!texture) throw new InvalidOperationException("Dusk cloud texture is missing");
            texture.textureType = TextureImporterType.Default;
            texture.sRGBTexture = true;
            texture.npotScale = TextureImporterNPOTScale.None;
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.mipmapEnabled = true;
            texture.maxTextureSize = 2048;
            texture.SaveAndReimport();
        }
    }
}
