using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace HappyToy.V2.Editor
{
    public static class KoreanCorridorPropsImport
    {
        const string Folder = "Assets/Resources/KoreanCorridorProps/";
        static readonly string[] Keys = { "jangseung", "onggi", "dokkaebi-tile" };
        [Serializable] sealed class Report { public float heightMetres; public Surface[] materials; }
        [Serializable] sealed class Surface { public string name, baseColor, metallicSmoothness, normal; public float roughness; }
        public static void Apply()
        {
            AssetDatabase.Refresh();
            foreach (string key in Keys) Import(key);
            AssetDatabase.SaveAssets(); Ensure();
        }
        static Texture2D Texture(string path, bool linear, bool normal = false)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (!importer) throw new InvalidOperationException("Missing generated PBR map: " + path);
            importer.sRGBTexture = !linear; importer.maxTextureSize = 2048;
            importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.mipmapEnabled = true; importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
        static void Import(string key)
        {
            string folder = Folder + key + "/";
            var report = JsonUtility.FromJson<Report>(File.ReadAllText(folder + "asset.json"));
            if (report.materials == null || report.materials.Length == 0) throw new InvalidOperationException("Generated PBR material inventory missing");
            var importer = (ModelImporter)AssetImporter.GetAtPath(folder + "geometry.fbx");
            if (!importer) throw new InvalidOperationException("Webapp game LOD missing: " + key);
            importer.importAnimation = false; importer.isReadable = true; importer.addCollider = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard; importer.SaveAndReimport();
            var surfaces = report.materials.ToDictionary(item => item.name, item =>
            {
                string path = folder + item.name + ".mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (!material) { material = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(material, path); }
                material.SetTexture("_BaseMap", Texture(folder + item.baseColor, false)); material.SetColor("_BaseColor", Color.white);
                material.SetFloat("_Smoothness", 1 - item.roughness);
                if (!string.IsNullOrEmpty(item.metallicSmoothness))
                { material.SetTexture("_MetallicGlossMap", Texture(folder + item.metallicSmoothness, true)); material.EnableKeyword("_METALLICSPECGLOSSMAP"); material.SetFloat("_Smoothness", 1); }
                if (!string.IsNullOrEmpty(item.normal))
                { material.SetTexture("_BumpMap", Texture(folder + item.normal, true, true)); material.EnableKeyword("_NORMALMAP"); }
                EditorUtility.SetDirty(material); return material;
            });
            var root = new GameObject(key);
            try
            {
                var model = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(folder + "geometry.fbx"), root.transform);
                foreach (var renderer in model.GetComponentsInChildren<Renderer>())
                    renderer.sharedMaterials = renderer.sharedMaterials.Select(item => item && surfaces.TryGetValue(item.name, out var found) ? found :
                        surfaces.Count == 1 ? surfaces.Values.Single() : throw new InvalidOperationException("Generated material slot cannot be resolved: " + key)).ToArray();
                var bounds = MonsterPresentationScale.Bounds(model.transform);
                model.transform.localScale *= report.heightMetres / bounds.size.y;
                bounds = MonsterPresentationScale.Bounds(model.transform);
                model.transform.position -= new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
                foreach (var child in root.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 8;
                PrefabUtility.SaveAsPrefabAsset(root, Folder + key + ".prefab");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
        public static void Ensure()
        {
            foreach (string key in Keys)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + key + ".prefab");
                if (!prefab || prefab.GetComponentsInChildren<MeshRenderer>().Length == 0 ||
                    prefab.GetComponentsInChildren<Renderer>().Any(item => item.sharedMaterials.Any(material => !material || !material.GetTexture("_BaseMap"))))
                    throw new InvalidOperationException("Generated Korean corridor prop is incomplete: " + key);
            }
        }
    }
}
