using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace HappyToy.V2.Editor
{
    public static class AlleyBeastImport
    {
        const string Folder = "Assets/Resources/AlleyBeast/";
        public static void InspectAxes()
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(Folder + "geometry.fbx");
            bool readable = importer.isReadable;
            try
            {
                importer.isReadable = true; importer.SaveAndReimport();
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "geometry.fbx");
                var obj = UnityEngine.Object.Instantiate(model);
                try
                {
                    foreach (var filter in obj.GetComponentsInChildren<MeshFilter>(true))
                    {
                        var vertices = filter.sharedMesh.vertices.Select(v => filter.transform.TransformPoint(v)).ToArray();
                        for (int axis = 0; axis < 3; axis++)
                        {
                            float min = vertices.Min(v => v[axis]), max = vertices.Max(v => v[axis]);
                            int low = vertices.Count(v => v[axis] < min + (max - min) * .15f);
                            int high = vertices.Count(v => v[axis] > max - (max - min) * .15f);
                            Debug.Log($"DOG_AXIS {axis} min={min} max={max} low={low} high={high}");
                        }
                        Debug.Log("DOG_AXIS_MATRIX " + filter.transform.localToWorldMatrix);
                    }
                }
                finally { UnityEngine.Object.DestroyImmediate(obj); }
            }
            finally { importer.isReadable = readable; importer.SaveAndReimport(); }
        }
        public static void Apply()
        {
            AssetDatabase.Refresh();
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "geometry.fbx");
            var albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "albedo.png");
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (!model || !albedo || !shader) throw new InvalidOperationException("Alley dog model, texture or URP shader missing");
            var material = AssetDatabase.LoadAssetAtPath<Material>(Folder + "Dog.mat");
            if (!material) { material = new Material(shader); AssetDatabase.CreateAsset(material, Folder + "Dog.mat"); }
            material.shader = shader; material.SetTexture("_BaseMap", albedo); material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Smoothness", .22f); EditorUtility.SetDirty(material);
            var root = new GameObject("Alley dog");
            try
            {
                var child = UnityEngine.Object.Instantiate(model, root.transform);
                // Unity's FBX root already carries the source Z-up to Unity
                // Y-up conversion. Compose yaw with it; replacing that rotation
                // discards the conversion and puts the dog on its side.
                child.transform.localRotation = Quaternion.Euler(0, -90, 0) * child.transform.localRotation;
                var renderers = child.GetComponentsInChildren<Renderer>(true);
                if (renderers.Length == 0) throw new InvalidOperationException("Dog has no renderers");
                foreach (var renderer in renderers) renderer.sharedMaterials = renderer.sharedMaterials.Select(_ => material).ToArray();
                var bounds = MonsterPresentationScale.Bounds(child.transform);
                if (bounds.size.y < .01f) throw new InvalidOperationException("Dog height invalid");
                child.transform.localScale *= .90f / bounds.size.y;
                bounds = MonsterPresentationScale.Bounds(child.transform);
                child.transform.position -= new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
                PrefabUtility.SaveAsPrefabAsset(root, Folder + "Beast.prefab");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
            AssetDatabase.SaveAssets();
            Debug.Log("ALLEY_BEAST_IMPORT_PASS");
        }
    }
}
