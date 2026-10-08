using System;
using UnityEditor;
using UnityEngine;

namespace HappyToy.V2.Editor
{
    // Full-atlas white-intensity masks are data, never colour/albedo replacements.
    public sealed class ThreatEyeMaskImport : AssetPostprocessor
    {
        static bool Mask(string path)=>path.StartsWith("Assets/Resources/ThreatEyes/",StringComparison.Ordinal)&&path.EndsWith("-emission.png",StringComparison.Ordinal);
        static void Set(TextureImporter importer)
        {
            importer.textureType=TextureImporterType.Default;importer.sRGBTexture=false;
            importer.textureShape=TextureImporterShape.Texture2D;
            importer.mipmapEnabled=true;importer.maxTextureSize=2048;
            importer.textureCompression=TextureImporterCompression.Uncompressed;
            importer.wrapMode=TextureWrapMode.Repeat;importer.filterMode=FilterMode.Trilinear;importer.anisoLevel=4;
            importer.alphaSource=TextureImporterAlphaSource.None;importer.alphaIsTransparency=false;importer.isReadable=false;
        }
        void OnPreprocessTexture(){if(Mask(assetPath))Set((TextureImporter)assetImporter);}
        public static void Ensure()
        {
            foreach(var guid in AssetDatabase.FindAssets("t:Texture2D",new[]{"Assets/Resources/ThreatEyes"}))
            {
                var path=AssetDatabase.GUIDToAssetPath(guid);if(!Mask(path))continue;
                var importer=(TextureImporter)AssetImporter.GetAtPath(path);Set(importer);importer.SaveAndReimport();
            }
            foreach(string key in new[]{"Cyclopse","Uncat","Hwacat_angry","Baby","LanternMask","Mannequin"})
            {
                var profile=Resources.Load<TextAsset>("ThreatEyes/"+key+"-profile");
                var mask=Resources.Load<Texture2D>("ThreatEyes/"+key+"-emission");
                if(!profile||!mask)throw new InvalidOperationException("Unusable original-eye Resource: "+key+" profile="+(bool)profile+" mask="+(bool)mask);
            }
            Debug.Log("HAPPYTOY_EYE_RESOURCES_PASS 6 anatomy profiles and surface emission atlases");
        }
    }
}
