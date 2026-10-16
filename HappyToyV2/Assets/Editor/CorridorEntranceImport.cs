using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace HappyToy.V2.Editor
{
    // Asset import owns PBR/prefabs only. The runtime chamber owns physical walls,
    // the blocked gate interaction and walkable space before the NavMesh bake.
    public static class CorridorEntranceImport
    {
        const string Folder = "Assets/Resources/CorridorEntrance/";
        static readonly string[] Keys = { "temple-gate", "cave-rock", "ritual-floor", "guardian-statue" };
        [Serializable] sealed class Report
        {
            public string key, normalizedAxis;
            public float widthMetres, heightMetres, depthMetres, normalizedMetres;
            public int textureMaxSize;
            public Surface[] materials;
            public Level[] lods;
        }
        [Serializable] sealed class Surface
        {
            public string name, baseColor, metallicSmoothness, normal, occlusion;
            public float roughness, metallic, normalScale, occlusionStrength;
            public float[] baseColorFactor;
        }
        [Serializable] sealed class Level { public string file; public int triangles, targetTriangles; }
        [Serializable] sealed class MeshInspection
        {
            public string name, assetPath, guid;
            public int vertices, uv0Count;
            public bool readable;
            public Vector2 uv0Min, uv0Max, uv0Span;
            public Vector2[] uv0Sample;
        }
        [Serializable] sealed class TextureInspection
        {
            public string name, assetPath, guid, format;
            public int width, height, importLimit;
            public bool sRgb;
        }
        [Serializable] sealed class MaterialInspection
        {
            public string renderer, material, materialPath, guid, shader;
            public int slot;
            public bool metallicMapKeyword, rendererPropertyBlock, slotPropertyBlock;
            public float metallic, smoothness;
            public Color baseColor;
            public string[] keywords;
            public TextureInspection baseMap, metallicGlossMap, rendererBlockBaseMap, slotBlockBaseMap;
        }
        [Serializable] sealed class Inspection
        {
            public string key, prefabPath;
            public MeshInspection[] meshes;
            public MaterialInspection[] materials;
        }

        public static void Apply()
        {
            AssetDatabase.Refresh();
            foreach (string key in Keys) Import(key);
            AssetDatabase.SaveAssets();
            Ensure();
            Debug.Log("CORRIDOR_ENTRANCE_IMPORT_PASS");
        }

        static Report Read(string key)
        {
            string path = Folder + key + "/asset.json";
            if (!File.Exists(path)) throw new InvalidOperationException("Finish the preserved entrance original first: " + key);
            var report = JsonUtility.FromJson<Report>(File.ReadAllText(path));
            if (report == null || report.key != key || report.materials == null || report.materials.Length == 0 ||
                report.lods == null || report.lods.Length != (key == "temple-gate" ? 1 : 2) ||
                report.normalizedMetres <= 0 || report.widthMetres <= 0 || report.heightMetres <= 0 || report.depthMetres <= 0 ||
                report.textureMaxSize != (key == "cave-rock" ? 2048 : 4096))
                throw new InvalidOperationException("Generated entrance inventory is incomplete: " + key);
            return report;
        }

        static Texture2D Texture(string path, int size, bool linear, bool normal = false)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (!importer) throw new InvalidOperationException("Missing original-derived entrance map: " + path);
            importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture = !linear;
            importer.maxTextureSize = size;
            importer.mipmapEnabled = true;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static Material Material(string key, Surface surface, int size)
        {
            string folder = Folder + key + "/";
            string path = folder + surface.name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!material)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                if (!shader) throw new InvalidOperationException("Pinned URP Lit shader is missing");
                material = new Material(shader) { enableInstancing = true };
                AssetDatabase.CreateAsset(material, path);
            }
            var tint = surface.baseColorFactor;
            material.SetColor("_BaseColor", tint != null && tint.Length == 4 ? new Color(tint[0], tint[1], tint[2], tint[3]) : Color.white);
            material.SetTexture("_BaseMap", Texture(folder + surface.baseColor, size, false));
            material.SetFloat("_WorkflowMode", 1);
            material.SetFloat("_Metallic", surface.metallic);
            material.SetFloat("_Smoothness", 1 - surface.roughness);
            material.SetFloat("_SmoothnessTextureChannel", 0);
            bool packed = !string.IsNullOrEmpty(surface.metallicSmoothness);
            material.SetTexture("_MetallicGlossMap", packed ? Texture(folder + surface.metallicSmoothness, size, true) : null);
            Keyword(material, "_METALLICSPECGLOSSMAP", packed);
            if (packed) material.SetFloat("_Smoothness", 1);
            bool normal = !string.IsNullOrEmpty(surface.normal);
            material.SetTexture("_BumpMap", normal ? Texture(folder + surface.normal, size, true, true) : null);
            material.SetFloat("_BumpScale", surface.normalScale);
            Keyword(material, "_NORMALMAP", normal);
            bool occlusion = !string.IsNullOrEmpty(surface.occlusion);
            material.SetTexture("_OcclusionMap", occlusion ? Texture(folder + surface.occlusion, size, true) : null);
            material.SetFloat("_OcclusionStrength", surface.occlusionStrength);
            Keyword(material, "_OCCLUSIONMAP", occlusion);
            EditorUtility.SetDirty(material);
            return material;
        }

        static void Keyword(Material material, string name, bool enabled)
        {
            if (enabled) material.EnableKeyword(name); else material.DisableKeyword(name);
        }

        static void Import(string key)
        {
            var report = Read(key);
            foreach (var level in report.lods)
            {
                var importer = AssetImporter.GetAtPath(Folder + level.file) as ModelImporter;
                if (!importer) throw new InvalidOperationException("Generated entrance LOD missing: " + level.file);
                importer.importAnimation = false;
                importer.isReadable = true;
                importer.addCollider = false;
                importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
                importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
                importer.importNormals = ModelImporterNormals.Import;
                importer.importTangents = ModelImporterTangents.CalculateMikk;
                importer.SaveAndReimport();
            }
            // Finish model imports before loading map objects into materials;
            // persist those external references before prefab serialization.
            var surfaces = report.materials.ToDictionary(surface => surface.name,
                surface => Material(key, surface, report.textureMaxSize), StringComparer.Ordinal);
            AssetDatabase.SaveAssets();
            var root = new GameObject(key);
            try
            {
                var visual = new GameObject("Source-derived entrance visual").transform;
                visual.SetParent(root.transform, false);
                var levels = new List<Renderer[]>();
                var firstBounds = new Bounds();
                foreach (var level in report.lods)
                {
                    var model = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Folder + level.file), visual);
                    foreach (var renderer in model.GetComponentsInChildren<Renderer>(true))
                    {
                        renderer.sharedMaterials = renderer.sharedMaterials.Select(source => Resolve(source, surfaces, key)).ToArray();
                        renderer.receiveShadows = true;
                    }
                    levels.Add(model.GetComponentsInChildren<Renderer>(true));
                    if (levels.Count == 1) firstBounds = GraphicsPropLibrary.LocalBounds(visual);
                }
                // Both rock exports use the same source coordinates. Normalize a
                // shared parent using LOD0, preserving positions during a switch.
                var low = firstBounds;
                float span = report.normalizedAxis == "width" ? low.size.x : low.size.y;
                if (span <= .0001f) throw new InvalidOperationException("Degenerate entrance model bounds: " + key);
                float scale = report.normalizedMetres / span;
                visual.localScale = Vector3.one * scale;
                visual.localPosition = -new Vector3(low.center.x, low.min.y, low.center.z) * scale;
                foreach (var child in root.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 8;
                if (report.lods.Length == 2)
                {
                    var group = root.AddComponent<LODGroup>();
                    group.fadeMode = LODFadeMode.None;
                    group.animateCrossFading = false;
                    group.SetLODs(new[] { new LOD(.32f, levels[0]), new LOD(.001f, levels[1]) });
                    group.RecalculateBounds();
                }
                // Resources paths omit extensions: keep the visual prefab key
                // distinct from the source model's GameObject key.
                PrefabUtility.SaveAsPrefabAsset(root, Folder + key + "-visual.prefab");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        static Material Resolve(Material source, Dictionary<string, Material> surfaces, string key)
        {
            if (source && surfaces.TryGetValue(source.name, out var found)) return found;
            if (surfaces.Count == 1) return surfaces.Values.Single();
            throw new InvalidOperationException("Unresolved generated entrance material: " + key + "/" + (source ? source.name : "null"));
        }

        // Explicit opt-in diagnostics. Reads existing imported assets only; no
        // Refresh, reimport, writes, scene instantiation or material mutation.
        public static void InspectMeshes()
        {
            foreach (string key in Keys)
            {
                string path = Folder + key + "-visual.prefab";
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (!prefab) throw new InvalidOperationException("Entrance prefab missing for inspection: " + key);
                var meshes = prefab.GetComponentsInChildren<MeshFilter>(true).Select(filter =>
                {
                    var mesh = filter.sharedMesh;
                    if (!mesh) throw new InvalidOperationException("Entrance mesh missing: " + filter.name);
                    var uv = mesh.isReadable ? mesh.uv : Array.Empty<Vector2>();
                    UvRange(uv, out var low, out var high);
                    string assetPath = AssetDatabase.GetAssetPath(mesh);
                    return new MeshInspection { name = filter.name, assetPath = assetPath,
                        guid = AssetDatabase.AssetPathToGUID(assetPath), vertices = mesh.vertexCount,
                        readable = mesh.isReadable, uv0Count = uv.Length, uv0Min = low,
                        uv0Max = high, uv0Span = high - low, uv0Sample = uv.Take(8).ToArray() };
                }).ToArray();
                var materials = new List<MaterialInspection>();
                foreach (var renderer in prefab.GetComponentsInChildren<Renderer>(true))
                {
                    var global = new MaterialPropertyBlock(); renderer.GetPropertyBlock(global);
                    var slots = renderer.sharedMaterials;
                    for (int index = 0; index < slots.Length; index++)
                    {
                        var material = slots[index];
                        if (!material) throw new InvalidOperationException("Entrance material missing: " + renderer.name);
                        var block = new MaterialPropertyBlock(); renderer.GetPropertyBlock(block, index);
                        string assetPath = AssetDatabase.GetAssetPath(material);
                        materials.Add(new MaterialInspection { renderer = renderer.name, slot = index,
                            material = material.name, materialPath = assetPath,
                            guid = AssetDatabase.AssetPathToGUID(assetPath), shader = material.shader.name,
                            keywords = material.shaderKeywords,
                            metallicMapKeyword = material.IsKeywordEnabled("_METALLICSPECGLOSSMAP"),
                            metallic = material.GetFloat("_Metallic"), smoothness = material.GetFloat("_Smoothness"),
                            baseColor = material.GetColor("_BaseColor"),
                            baseMap = Describe(material.GetTexture("_BaseMap")),
                            metallicGlossMap = Describe(material.GetTexture("_MetallicGlossMap")),
                            rendererPropertyBlock = !global.isEmpty, slotPropertyBlock = !block.isEmpty,
                            rendererBlockBaseMap = Describe(global.GetTexture("_BaseMap")),
                            slotBlockBaseMap = Describe(block.GetTexture("_BaseMap")) });
                    }
                }
                Debug.Log("CORRIDOR_ENTRANCE_MESH_INSPECTION " + JsonUtility.ToJson(new Inspection {
                    key = key, prefabPath = path, meshes = meshes, materials = materials.ToArray() }));
            }
        }

        static TextureInspection Describe(Texture texture)
        {
            if (!texture) return null;
            string path = AssetDatabase.GetAssetPath(texture);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            return new TextureInspection { name = texture.name, assetPath = path,
                guid = AssetDatabase.AssetPathToGUID(path), width = texture.width, height = texture.height,
                format = texture is Texture2D image ? image.format.ToString() : texture.GetType().Name,
                importLimit = importer ? importer.maxTextureSize : 0, sRgb = importer && importer.sRGBTexture };
        }

        static void UvRange(Vector2[] uv, out Vector2 low, out Vector2 high)
        {
            low = high = uv.Length == 0 ? Vector2.zero : uv[0];
            foreach (var point in uv) { low = Vector2.Min(low, point); high = Vector2.Max(high, point); }
        }
        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        static void EnsureUv(Mesh mesh, string key)
        {
            if (!mesh || !mesh.isReadable) throw new InvalidOperationException("Entrance mesh must be readable for UV validation: " + key);
            var uv = mesh.uv;
            UvRange(uv, out var low, out var high);
            if (uv.Length != mesh.vertexCount || uv.Length == 0 ||
                uv.Any(point => !Finite(point.x) || !Finite(point.y)) || high.x - low.x < .5f || high.y - low.y < .5f)
                throw new InvalidOperationException("Entrance UV0 is missing, nonfinite or collapsed: " + key + "/" + mesh.name);
        }

        public static void Ensure()
        {
            foreach (string key in Keys)
            {
                var report = Read(key);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + key + "-visual.prefab");
                if (!prefab || prefab.GetComponentsInChildren<Collider>(true).Length != 0 ||
                    prefab.GetComponentsInChildren<MeshRenderer>(true).Length == 0 ||
                    prefab.GetComponentsInChildren<Renderer>(true).Any(renderer => renderer.sharedMaterials.Any(material =>
                        !material || !material.GetTexture("_BaseMap"))))
                    throw new InvalidOperationException("Generated entrance prefab/PBR is incomplete: " + key);
                var bound = GraphicsPropLibrary.LocalBounds(prefab.transform);
                float span = report.normalizedAxis == "width" ? bound.size.x : bound.size.y;
                if (Mathf.Abs(span - report.normalizedMetres) > .08f || Mathf.Abs(bound.min.y) > .05f)
                    throw new InvalidOperationException("Generated entrance dimensions or ground origin changed: " + key);
                var group = prefab.GetComponent<LODGroup>();
                if (report.lods.Length == 2 && (!group || group.GetLODs().Length != 2 ||
                    group.GetLODs().Any(level => level.renderers.Length == 0)))
                    throw new InvalidOperationException("Source-derived entrance LODs are missing: " + key);
                foreach (var level in report.lods)
                    if (level.triangles <= 0 || level.triangles > level.targetTriangles + 4)
                        throw new InvalidOperationException("Generated entrance LOD budget exceeded: " + level.file);
                foreach (var filter in prefab.GetComponentsInChildren<MeshFilter>(true)) EnsureUv(filter.sharedMesh, key);
            }
        }
    }
}
