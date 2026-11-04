using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace HappyToy.V2.Editor
{
    // Imports only already-finished source assets. No scene or gameplay mutation.
    public static class TemplePrologueImport
    {
        const string Source = "SourceArt/TemplePrologue/game-export/";
        const string Target = "Assets/Resources/TemplePrologue/";

        [Serializable] sealed class Report { public Surface[] materials; public Level[] lods; }
        [Serializable] sealed class Surface { public string name, baseColor, metallicSmoothness; public float metallic, roughness; }
        [Serializable] sealed class Level { public string file; }

        public static void ImportTemple() => Import("temple");
        public static void ImportPine() => Import("pine");

        public static void Ensure()
        {
            var temple = AssetDatabase.LoadAssetAtPath<GameObject>(Target + "temple-visual.prefab");
            var pine = AssetDatabase.LoadAssetAtPath<GameObject>(Target + "pine-visual.prefab");
            if (!temple || !pine) throw new InvalidOperationException("Temple prologue visual prefabs are missing");
            var group = pine.GetComponent<LODGroup>();
            var levels = group ? group.GetLODs() : null;
            if (levels == null || levels.Length != 2) throw new InvalidOperationException("The forest pine requires two authored LODs");
            foreach (var level in levels)
                if (level.renderers == null || level.renderers.Length == 0)
                    throw new InvalidOperationException("Forest pine LOD is missing renderers");
            var filter = levels[1].renderers[0] ? levels[1].renderers[0].GetComponent<MeshFilter>() : null;
            if (!filter || !filter.sharedMesh || filter.sharedMesh.subMeshCount != 1 || filter.sharedMesh.GetIndexCount(0) / 3 > 50000)
                throw new InvalidOperationException("Forest pine distant LOD is missing or exceeds its budget");
            foreach (string cue in new[] { "enemy-baby-cry", "story-bell", "ambience-corridor", "door-open" })
                if (!ExternalAudio.Shared(cue)) throw new InvalidOperationException("Missing prologue recording " + cue);
            for (int i = 0; i < 4; i++) if (!ExternalAudio.Shared("step-forest", i))
                throw new InvalidOperationException("Missing forest footstep recording " + i);
        }

        static void Import(string key)
        {
            var report = JsonUtility.FromJson<Report>(File.ReadAllText(Source + key + "/asset.json"));
            if (report == null || report.lods == null || report.lods.Length != (key == "pine" ? 2 : 1) || report.materials == null || report.materials.Length != 1)
                throw new InvalidOperationException("Unexpected finished meshes or material: " + key);
            Directory.CreateDirectory(Target + key);
            var surface = report.materials[0];
            foreach (var level in report.lods) Copy(Source + level.file, Target + level.file);
            Copy(Source + key + "/" + surface.baseColor, Target + key + "/" + surface.baseColor);
            if (!string.IsNullOrEmpty(surface.metallicSmoothness))
                Copy(Source + key + "/" + surface.metallicSmoothness, Target + key + "/" + surface.metallicSmoothness);
            AssetDatabase.Refresh();
            foreach (var level in report.lods)
            {
                var modelPath = Target + level.file;
                var importer = AssetImporter.GetAtPath(modelPath) as ModelImporter;
                if (!importer) throw new InvalidOperationException("Missing finished model " + modelPath);
                importer.importAnimation = false; importer.addCollider = false;
                importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
                importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
                importer.SaveAndReimport();
            }
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (!shader) throw new InvalidOperationException("URP Lit shader missing");
            string materialPath = Target + key + "/surface.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (!material) { material = new Material(shader); AssetDatabase.CreateAsset(material, materialPath); }
            material.SetColor("_BaseColor", Color.white);
            material.SetTexture("_BaseMap", LoadTexture(Target + key + "/" + surface.baseColor, false));
            material.SetFloat("_Metallic", surface.metallic);
            material.SetFloat("_Smoothness", 1 - surface.roughness);
            if (!string.IsNullOrEmpty(surface.metallicSmoothness))
            {
                material.SetTexture("_MetallicGlossMap", LoadTexture(Target + key + "/" + surface.metallicSmoothness, true));
                material.SetFloat("_Smoothness", 1); material.EnableKeyword("_METALLICSPECGLOSSMAP");
            }
            EditorUtility.SetDirty(material); AssetDatabase.SaveAssets();
            var root = new GameObject(key + "-visual");
            try
            {
                var levels = new LOD[report.lods.Length];
                for (int index = 0; index < report.lods.Length; index++)
                {
                    var modelPath = Target + report.lods[index].file;
                    var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
                    if (!model) throw new InvalidOperationException("Could not load " + modelPath);
                    var instance = UnityEngine.Object.Instantiate(model, root.transform, false);
                    var renderers = instance.GetComponentsInChildren<Renderer>(true);
                    if (renderers.Length == 0) throw new InvalidOperationException("Missing model renderers");
                    foreach (var renderer in renderers)
                    {
                        var slots = renderer.sharedMaterials;
                        if (slots.Length != 1) throw new InvalidOperationException("Unexpected model material slots");
                        slots[0] = material; renderer.sharedMaterials = slots;
                    }
                    levels[index] = new LOD(index == 0 ? .18f : .015f, renderers);
                }
                if (levels.Length > 1) { var group = root.AddComponent<LODGroup>(); group.SetLODs(levels); group.RecalculateBounds(); }
                PrefabUtility.SaveAsPrefabAsset(root, Target + key + "-visual.prefab");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
            AssetDatabase.SaveAssets();
            Debug.Log("TEMPLE_PROLOGUE_IMPORT_PASS " + key);
        }

        static void Copy(string source, string target) => File.Copy(source, target, true);
        static Texture2D LoadTexture(string path, bool linear)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (!importer) throw new InvalidOperationException("Missing map " + path);
            importer.sRGBTexture = !linear; importer.mipmapEnabled = true; importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
    }
}
