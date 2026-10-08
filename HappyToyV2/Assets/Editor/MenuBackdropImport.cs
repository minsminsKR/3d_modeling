using System;
using UnityEditor;
using UnityEngine;

namespace HappyToy.V2.Editor
{
    public sealed class MenuBackdropImport : AssetPostprocessor
    {
        const string Path="Assets/Resources/Menu/cyclopse-menace-v1.png";
        static void Configure(TextureImporter importer)
        {
            importer.textureType=TextureImporterType.Default;
            importer.textureShape=TextureImporterShape.Texture2D;
            importer.sRGBTexture=true;importer.mipmapEnabled=false;
            importer.maxTextureSize=2048;importer.npotScale=TextureImporterNPOTScale.None;
            importer.textureCompression=TextureImporterCompression.CompressedHQ;
            importer.compressionQuality=100;importer.wrapMode=TextureWrapMode.Clamp;
            importer.filterMode=FilterMode.Bilinear;importer.alphaSource=TextureImporterAlphaSource.None;
            importer.alphaIsTransparency=false;importer.isReadable=false;
        }
        void OnPreprocessTexture(){if(assetPath==Path)Configure((TextureImporter)assetImporter);}
        public static void Ensure()
        {
            var importer=AssetImporter.GetAtPath(Path) as TextureImporter;
            if(!importer)throw new InvalidOperationException("Missing authored Cyclopse menu wallpaper");
            Configure(importer);importer.SaveAndReimport();
            var texture=Resources.Load<Texture2D>("Menu/cyclopse-menace-v1");
            if(!texture||texture.width<1500||texture.height<800)throw new InvalidOperationException("Cyclopse menu wallpaper is not a full-resolution Texture2D Resource");
            Debug.Log("HAPPYTOY_MENU_BACKDROP_IMPORT_PASS "+texture.width+"x"+texture.height);
        }
    }
}
