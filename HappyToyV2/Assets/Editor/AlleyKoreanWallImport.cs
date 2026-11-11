using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace HappyToy.V2.Editor
{
    // Reproducible import of the reviewed source-derived FBX and base color.
    public static class AlleyKoreanWallImport
    {
        const string Folder = "Assets/Resources/AlleyKoreanWall/";
        public static void Apply()
        {
            AssetDatabase.Refresh();
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "geometry.fbx");
            var albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "albedo.png");
            var packedPath = Folder + "metallic-smoothness.png";
            var packedImporter = AssetImporter.GetAtPath(packedPath) as TextureImporter;
            if (!packedImporter) throw new InvalidOperationException("Missing material map: " + packedPath);
            packedImporter.sRGBTexture = false;
            packedImporter.alphaSource = TextureImporterAlphaSource.FromInput;
            packedImporter.SaveAndReimport();
            var packed = AssetDatabase.LoadAssetAtPath<Texture2D>(packedPath);
            if (!packed) throw new InvalidOperationException("Material map import failed: " + packedPath);
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (!model || !albedo || !shader) throw new InvalidOperationException("KoreanWall model, texture or URP shader is missing");
            var material = AssetDatabase.LoadAssetAtPath<Material>(Folder + "KoreanWall.mat");
            if (!material) { material = new Material(shader); AssetDatabase.CreateAsset(material, Folder + "KoreanWall.mat"); }
            material.shader = shader; material.SetTexture("_BaseMap", albedo); material.SetColor("_BaseColor", Color.white);
            material.SetTexture("_MetallicGlossMap", packed); material.SetFloat("_Metallic", 1f);
            material.SetFloat("_Smoothness", 1f); material.EnableKeyword("_METALLICSPECGLOSSMAP"); EditorUtility.SetDirty(material);
            var root = new GameObject("Old Korean wall");
            try
            {
                var child = UnityEngine.Object.Instantiate(model, root.transform);
                var renderers = child.GetComponentsInChildren<Renderer>(true);
                if (renderers.Length == 0) throw new InvalidOperationException("KoreanWall has no renderer");
                foreach (var renderer in renderers) renderer.sharedMaterials = renderer.sharedMaterials.Select(_ => material).ToArray();
                var bounds = MonsterPresentationScale.Bounds(child.transform);
                if (bounds.size.y < .01f) throw new InvalidOperationException("KoreanWall height is invalid");
                child.transform.localScale *= 4.0f / bounds.size.y;
                bounds = MonsterPresentationScale.Bounds(child.transform);
                child.transform.position -= new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
                PrefabUtility.SaveAsPrefabAsset(root, Folder + "KoreanWall.prefab");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
            AssetDatabase.SaveAssets();
            Debug.Log("ALLEY_KOREAN_WALL_IMPORT_PASS");
        }
    }
}
