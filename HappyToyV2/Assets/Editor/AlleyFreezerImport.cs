using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace HappyToy.V2.Editor
{
    // Reproducible import of the reviewed source-derived FBX and base color.
    public static class AlleyFreezerImport
    {
        const string Folder = "Assets/Resources/AlleyFreezer/";
        public static void Apply()
        {
            AssetDatabase.Refresh();
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "geometry.fbx");
            var albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "albedo.png");
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (!model || !albedo || !shader) throw new InvalidOperationException("Freezer model, texture or URP shader is missing");
            var material = AssetDatabase.LoadAssetAtPath<Material>(Folder + "Freezer.mat");
            if (!material) { material = new Material(shader); AssetDatabase.CreateAsset(material, Folder + "Freezer.mat"); }
            material.shader = shader; material.SetTexture("_BaseMap", albedo); material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Smoothness", .22f); EditorUtility.SetDirty(material);
            var root = new GameObject("Old neighborhood shop freezer");
            try
            {
                var child = UnityEngine.Object.Instantiate(model, root.transform);
                var renderers = child.GetComponentsInChildren<Renderer>(true);
                if (renderers.Length == 0) throw new InvalidOperationException("Freezer has no renderer");
                foreach (var renderer in renderers) renderer.sharedMaterials = renderer.sharedMaterials.Select(_ => material).ToArray();
                var bounds = MonsterPresentationScale.Bounds(child.transform);
                if (bounds.size.y < .01f) throw new InvalidOperationException("Freezer height is invalid");
                child.transform.localScale *= .85f / bounds.size.y;
                bounds = MonsterPresentationScale.Bounds(child.transform);
                child.transform.position -= new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
                PrefabUtility.SaveAsPrefabAsset(root, Folder + "Freezer.prefab");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
            AssetDatabase.SaveAssets();
            Debug.Log("ALLEY_FREEZER_IMPORT_PASS");
        }
    }
}
