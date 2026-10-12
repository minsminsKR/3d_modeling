using System;
using UnityEditor;
using UnityEngine;
namespace HappyToy.V2.Editor
{
    public sealed class EnemyStudioTextureImport : AssetPostprocessor
    {
        internal static void Configure(TextureImporter importer,string path)
        {
            bool normal=path.EndsWith("/normal.png",StringComparison.Ordinal);
            bool emission=path.EndsWith("/eyes-emission.png",StringComparison.Ordinal);
            importer.textureType=normal?TextureImporterType.NormalMap:TextureImporterType.Default;
            importer.textureShape=TextureImporterShape.Texture2D;
            importer.sRGBTexture=path.EndsWith("albedo.png",StringComparison.Ordinal);
            importer.maxTextureSize=2048;importer.mipmapEnabled=true;
            importer.alphaSource=emission?TextureImporterAlphaSource.None:TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency=false;
            importer.textureCompression=emission?TextureImporterCompression.Uncompressed:TextureImporterCompression.CompressedHQ;
            importer.wrapMode=TextureWrapMode.Repeat;importer.filterMode=FilterMode.Trilinear;
        }
        void OnPreprocessTexture()
        {
            if(assetPath.StartsWith("Assets/Resources/EnemyStudio/",StringComparison.Ordinal)&&assetPath.EndsWith(".png",StringComparison.Ordinal))
                Configure((TextureImporter)assetImporter,assetPath);
        }
    }
}