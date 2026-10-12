using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace HappyToy.V2.Editor
{
    // Only the separately authored Resources assets are changed. No scene or
    // legacy enemy model, animation, texture or material is edited.
    public static class EnemyStudioGameImport
    {
        public const string Folder = "Assets/Resources/EnemyStudio";
        public const int MaximumPrimaryVertices = 80000;
        public const int MaximumPrimaryTriangles = 100000;
        const string BindingPrefix = "Rigged asset";
        static readonly string[] Keys = { "Uncat", "Cyclopse", "Baby" };

        // A fresh clone must build the same three reviewed runtime prefabs.
        public static void Ensure()
        {
            bool complete = true;
            foreach (string key in Keys)
            {
                string path = Folder + "/" + key + "/";
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path + key + "Runtime.prefab");
                var rig = prefab ? prefab.GetComponent<EnemyStudioRig>() : null;
                complete &= rig && rig.key == key && rig.primarySkins.Length > 0;
                foreach (string alias in key == "Baby" ? new[] { "patrol", "chase", "cry" } : new[] { "patrol", "chase" })
                    complete &= AssetDatabase.LoadAssetAtPath<AnimationClip>(path + alias + ".anim");
            }
            if (!complete) Apply();
        }

        public static void Apply()
        {
            AssetDatabase.Refresh();
            foreach (var key in Keys) ApplyKey(key);
            AssetDatabase.SaveAssets();
            Debug.Log("ENEMY_STUDIO_IMPORT_PASS all three independently authored creatures");
        }

        public static void ApplyKey(string key)
        {
            if (!EnemyStudioRig.Supported(key)) throw new ArgumentException("Unknown studio creature: " + key);
            string path = Folder + "/" + key, modelPath = path + "/" + key + ".fbx";
            var importer = AssetImporter.GetAtPath(modelPath) as ModelImporter;
            if (!importer) throw new InvalidOperationException("Export the new studio game LOD/rig first: " + modelPath);
            importer.animationType = ModelImporterAnimationType.Legacy; importer.importAnimation = true;
            importer.animationCompression = ModelImporterAnimationCompression.Off; importer.resampleCurves = true;
            importer.isReadable = true; importer.optimizeGameObjects = false; importer.preserveHierarchy = true;
            importer.importCameras = false; importer.importLights = false; importer.importBlendShapes = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.meshCompression = ModelImporterMeshCompression.Off;
            importer.importNormals = ModelImporterNormals.Import; importer.importTangents = ModelImporterTangents.CalculateMikk;
            importer.skinWeights = ModelImporterSkinWeights.Custom; importer.maxBonesPerVertex = 8; importer.minBoneWeight = .0001f;
            importer.SaveAndReimport();
            RequireTexture(path + "/albedo.png", true);
            RequireTexture(path + "/normal.png", false, true);
            RequireTexture(path + "/metallic-smoothness.png", false);
            RequireTexture(path + "/eyes-emission.png", true);
            if (!AssetDatabase.LoadAssetAtPath<TextAsset>(path + "/eyes-profile.json"))
                throw new InvalidOperationException("New creature needs its own actual eye UV anatomy: " + key);
            bool hasAO = AssetDatabase.LoadAssetAtPath<Texture2D>(path + "/ao.png");
            if (hasAO) RequireTexture(path + "/ao.png", false);
            var material = AssetDatabase.LoadAssetAtPath<Material>(path + "/Skin.mat");
            if (!material)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                if (!shader) throw new InvalidOperationException("Missing URP Lit shader");
                material = new Material(shader) { name = key + " studio skin" }; AssetDatabase.CreateAsset(material, path + "/Skin.mat");
            }
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(path + "/albedo.png"));
            material.SetColor("_BaseColor", Color.white);
            material.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(path + "/normal.png"));
            material.SetFloat("_BumpScale", 1); material.EnableKeyword("_NORMALMAP");
            material.SetTexture("_MetallicGlossMap", AssetDatabase.LoadAssetAtPath<Texture2D>(path + "/metallic-smoothness.png"));
            material.SetFloat("_Metallic", 1); material.SetFloat("_Smoothness", 1); material.SetFloat("_SmoothnessTextureChannel", 0);
            material.EnableKeyword("_METALLICSPECGLOSSMAP");
            if (hasAO) { material.SetTexture("_OcclusionMap", AssetDatabase.LoadAssetAtPath<Texture2D>(path + "/ao.png")); material.EnableKeyword("_OCCLUSIONMAP"); }
            else { material.SetTexture("_OcclusionMap", null); material.DisableKeyword("_OCCLUSIONMAP"); }
            // Keep the exact runtime eye shader combination as a build input.
            // Black colour means the unprepared skin does not emit across its body.
            material.SetTexture("_EmissionMap", AssetDatabase.LoadAssetAtPath<Texture2D>(path + "/eyes-emission.png"));
            material.SetColor("_EmissionColor", Color.black); material.EnableKeyword("_EMISSION");
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
            EditorUtility.SetDirty(material);

            var importedClips = AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<AnimationClip>()
                .Where(clip => !clip.name.StartsWith("__preview__", StringComparison.Ordinal)).ToArray();
            var aliases = key == "Baby" ? new[] { "patrol", "chase", "cry" } : new[] { "patrol", "chase" };
            var clips = aliases.Select(alias => CopyClip(path, alias, FindClip(importedClips, alias))).ToArray();
            var root = new GameObject(key);
            try
            {
                var asset = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(modelPath), root.transform);
                asset.name = BindingPrefix;
                foreach (var prior in asset.GetComponentsInChildren<Animation>(true)) UnityEngine.Object.DestroyImmediate(prior);
                foreach (var prior in asset.GetComponentsInChildren<Animator>(true)) UnityEngine.Object.DestroyImmediate(prior);
                if (asset.GetComponentsInChildren<Collider>(true).Length > 0)
                    throw new InvalidOperationException("Studio FBX must contain visual geometry only: " + key);
                var skins = asset.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                if (skins.Length == 0) throw new InvalidOperationException("Studio FBX is not a skinned creature: " + key);
                foreach (var skin in skins)
                {
                    if (!skin.sharedMesh || !skin.sharedMesh.isReadable || skin.sharedMesh.blendShapeCount != 0)
                        throw new InvalidOperationException("Studio skin must contain baked, readable game geometry: " + key);
                    skin.sharedMaterials = Enumerable.Repeat(material, skin.sharedMesh.subMeshCount).ToArray();
                    skin.updateWhenOffscreen = false; skin.quality = SkinQuality.Auto;
                    skin.shadowCastingMode = ShadowCastingMode.On; skin.receiveShadows = true;
                }
                var primary = skins.Where(skin => LodLevel(skin) == 0).ToArray();
                int vertices = primary.Sum(skin => skin.sharedMesh.vertexCount);
                int triangles = primary.Sum(skin => skin.sharedMesh.triangles.Length / 3);
                if (primary.Length == 0 || vertices > MaximumPrimaryVertices || triangles > MaximumPrimaryTriangles)
                    throw new InvalidOperationException("Export a reviewed <=80k-vertex game LOD, not the 1536 source mesh: " + key + " vertices=" + vertices);
                var nodes = asset.GetComponentsInChildren<Transform>(true);
                foreach (var name in new[] { "Hips", "Spine1", "Head", "RightArm", "RightForeArm", "RightHand", "LeftArm", "LeftForeArm", "LeftHand", "RightUpLeg", "RightLeg", "RightFoot", "LeftUpLeg", "LeftLeg", "LeftFoot" })
                    if (!nodes.Any(node => node.name == "mixamorig:" + name)) throw new InvalidOperationException("Missing new semantic pose joint " + key + ": " + name);
                var animation = root.AddComponent<Animation>(); animation.playAutomatically = false;
                animation.cullingType = AnimationCullingType.AlwaysAnimate;
                for (int index = 0; index < aliases.Length; index++)
                {
                    ValidateBindings(root.transform, clips[index]); animation.AddClip(clips[index], aliases[index]);
                    animation[aliases[index]].wrapMode = WrapMode.Loop;
                }
                animation.clip = clips[0]; clips[0].SampleAnimation(root, 0);
                var rig = root.AddComponent<EnemyStudioRig>(); rig.key = key; rig.primarySkins = primary;
                rig.surfaceSupports = primary.Select(BuildSupports).ToArray();
                // These independently fitted prefabs carry their own unit scale.
                // Runtime must not copy a retired ~100x Mixamo wrapper onto them.
                var before = EnemyStudioRig.PosedBounds(primary);
                float target = TargetHeight(key);
                if (before.size.y <= .01f) throw new InvalidOperationException("Studio creature has no posed height: " + key);
                root.transform.localScale = Vector3.one * (target / before.size.y);
                var posed = EnemyStudioRig.PosedBounds(primary);
                root.transform.position += new Vector3(-posed.center.x, -posed.min.y, -posed.center.z);
                var union = SamplePoseUnion(root, clips, rig, out float maximumSupportGap);
                foreach (var skin in skins) skin.localBounds = ToLocalBounds(union, skin.transform);
                clips[0].SampleAnimation(root, 0);
                AddLods(root, skins);
                var fitted = EnemyStudioRig.PosedBounds(primary);
                if (Mathf.Abs(fitted.size.y - target) > .02f || Mathf.Abs(fitted.min.y) > .005f)
                    throw new InvalidOperationException("Studio fitting differs from actual deformed surface: " + key + " " + fitted);
                // Validate the new atlas on the new skin now. Do not serialize the
                // runtime glow component's transient materials/anchor state.
                var eyes = MonsterRedEyes.Attach(root.transform, key);
                if (!eyes.Prepared || eyes.EyeAnchors.Count != (key == "Cyclopse" ? 1 : 2))
                    throw new InvalidOperationException("Studio real eye surface authoring is incomplete: " + key);
                UnityEngine.Object.DestroyImmediate(eyes.gameObject);
                // EditMode destruction does not guarantee runtime material rollback.
                foreach(var skin in skins) skin.sharedMaterials=Enumerable.Repeat(material,skin.sharedMesh.subMeshCount).ToArray();
                PrefabUtility.SaveAsPrefabAsset(root, path + "/" + key + "Runtime.prefab");
                Directory.CreateDirectory("Verification/enemy-studio");
                File.WriteAllText("Verification/enemy-studio/import-" + key + ".json", JsonUtility.ToJson(new ImportReport
                {
                    status = "PASS", key = key, sourceFbx = modelPath, prefab = path + "/" + key + "Runtime.prefab",
                    vertices = vertices, triangles = triangles,
                    bones = primary.SelectMany(skin => skin.bones).Distinct().Count(),
                    supportVertices = rig.surfaceSupports.Sum(surface => surface.vertexIndices.Length),
                    targetHeight = target, posedHeight = fitted.size.y, groundMin = fitted.min.y, maximumSupportGap = maximumSupportGap,
                    clips = aliases, clipSeconds = clips.Select(clip => clip.length).ToArray(),
                    clipCurves = clips.Select(clip => AnimationUtility.GetCurveBindings(clip).Length).ToArray()
                }, true));
                AssetDatabase.SaveAssets(); Debug.Log("ENEMY_STUDIO_IMPORT_PASS " + key + " vertices=" + vertices);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        static void RequireTexture(string path, bool srgb, bool normal = false)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (!importer) throw new InvalidOperationException("Missing new studio map: " + path);
            EnemyStudioTextureImport.Configure(importer,path); importer.SaveAndReimport();
            if(!AssetDatabase.LoadAssetAtPath<Texture2D>(path)) throw new InvalidOperationException("Studio map must import as Texture2D: "+path);
        }

        static AnimationClip FindClip(AnimationClip[] source, string alias)
        {
            string[] names = alias == "patrol" ? new[] { "patrol", "walk", "walkpatrol" } :
                alias == "chase" ? new[] { "chase", "run", "runinplace", "runchase" } : new[] { "cry", "crying", "idlecry" };
            var matches = source.Where(clip => names.Contains(Regex.Replace(clip.name.Split('|').Last().ToLowerInvariant(), "[^a-z0-9]", ""))).ToArray();
            if (matches.Length != 1) throw new InvalidOperationException("Studio action must unambiguously supply " + alias + ": " + string.Join(", ", source.Select(clip => clip.name)));
            if (matches[0].length < .1f) throw new InvalidOperationException("Studio action is empty: " + alias);
            return matches[0];
        }

        static AnimationClip CopyClip(string path, string alias, AnimationClip source)
        {
            string clipPath = path + "/" + alias + ".anim";
            var result = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            if (!result) { result = new AnimationClip(); AssetDatabase.CreateAsset(result, clipPath); } else result.ClearCurves();
            result.name = alias; result.legacy = true; result.frameRate = source.frameRate; result.wrapMode = WrapMode.Loop;
            foreach (var binding in AnimationUtility.GetCurveBindings(source))
            {
                var next = binding; next.path = BindingPrefix + (binding.path.Length == 0 ? "" : "/" + binding.path);
                AnimationUtility.SetEditorCurve(result, next, AnimationUtility.GetEditorCurve(source, binding));
            }
            foreach (var binding in AnimationUtility.GetObjectReferenceCurveBindings(source))
            {
                var next = binding; next.path = BindingPrefix + (binding.path.Length == 0 ? "" : "/" + binding.path);
                AnimationUtility.SetObjectReferenceCurve(result, next, AnimationUtility.GetObjectReferenceCurve(source, binding));
            }
            EditorUtility.SetDirty(result); return result;
        }

        static void ValidateBindings(Transform root, AnimationClip clip)
        {
            foreach (var binding in AnimationUtility.GetCurveBindings(clip))
                if (binding.path.Length != 0 && !root.Find(binding.path))
                    throw new InvalidOperationException("Studio action binding is missing: " + clip.name + " / " + binding.path);
        }

        static int LodLevel(SkinnedMeshRenderer skin)
        {
            var match = Regex.Match(skin.name + " " + skin.sharedMesh.name, @"(?:_|\b)LOD([0-2])(?:\b|_)", RegexOptions.IgnoreCase);
            return match.Success ? int.Parse(match.Groups[1].Value) : 0;
        }

        static void AddLods(GameObject root, SkinnedMeshRenderer[] skins)
        {
            if (!skins.Any(skin => LodLevel(skin) > 0)) return;
            var levels = skins.GroupBy(LodLevel).OrderBy(group => group.Key).ToArray();
            var lodGroup = root.AddComponent<LODGroup>();
            lodGroup.SetLODs(levels.Select(level => new LOD(level.Key == 0 ? .28f : level.Key == 1 ? .10f : .015f, level.Cast<Renderer>().ToArray())).ToArray());
            lodGroup.fadeMode = LODFadeMode.None; lodGroup.RecalculateBounds();
        }

        static float TargetHeight(string key) => MonsterPresentationScale.TargetHeight(key == "Cyclopse" ? EnemySoundKind.Cyclopse : key == "Uncat" ? EnemySoundKind.Uncat : EnemySoundKind.Baby);

        static Bounds SamplePoseUnion(GameObject root, AnimationClip[] clips, EnemyStudioRig rig, out float maximumSupportGap)
        {
            var result = new Bounds(); bool any = false; maximumSupportGap = 0;
            // Include off-beat neck extrema between footfall phases in culling/support bounds.
            foreach (var clip in clips) for (int frame = 0; frame <= 32; frame++)
            {
                clip.SampleAnimation(root, clip.length * frame / 32f);
                float actualSole = float.MaxValue, supportSole = float.MaxValue;
                foreach (var surface in rig.surfaceSupports)
                {
                    var points = EnemyStudioRig.PosedVertices(surface.skin);
                    foreach (var point in points)
                    {
                        actualSole = Mathf.Min(actualSole, point.y);
                        if (!any) { result = new Bounds(point, Vector3.zero); any = true; } else result.Encapsulate(point);
                    }
                    foreach (int vertex in surface.vertexIndices) supportSole = Mathf.Min(supportSole, points[vertex].y);
                }
                float gap = Mathf.Abs(actualSole - supportSole); maximumSupportGap = Mathf.Max(maximumSupportGap, gap);
                if (gap > .04f) throw new InvalidOperationException("Studio ground supports miss actual " + clip.name + " surface by " + gap + " m");
            }
            return result;
        }

        static Bounds ToLocalBounds(Bounds world, Transform node)
        {
            var result = new Bounds(); bool any = false;
            foreach (float x in new[] { world.min.x, world.max.x }) foreach (float y in new[] { world.min.y, world.max.y }) foreach (float z in new[] { world.min.z, world.max.z })
            {
                var point = node.InverseTransformPoint(new Vector3(x, y, z));
                if (!any) { result = new Bounds(point, Vector3.zero); any = true; } else result.Encapsulate(point);
            }
            result.Expand(.04f); return result;
        }

        static EnemyStudioRig.SurfaceSupport BuildSupports(SkinnedMeshRenderer skin)
        {
            var mesh = skin.sharedMesh; var points = mesh.vertices; var counts = mesh.GetBonesPerVertex(); var weights = mesh.GetAllBoneWeights();
            var bones = skin.bones; var groups = new Dictionary<int, List<int>>(); var contacts = new bool[points.Length];
            var cells = new Dictionary<int, int>(); var selected = new HashSet<int>();
            var bounds = mesh.bounds; int cursor = 0;
            for (int vertex = 0; vertex < points.Length; vertex++)
            {
                float strongest = -1; int dominant = -1; var limb = new float[4];
                for (int influence = 0; influence < counts[vertex]; influence++)
                {
                    var weight = weights[cursor++];
                    if (weight.boneIndex < 0 || weight.boneIndex >= bones.Length) throw new InvalidOperationException("Studio support authoring lost a bone");
                    if (weight.weight > strongest) { strongest = weight.weight; dominant = weight.boneIndex; }
                    int group = ContactGroup(bones[weight.boneIndex].name);
                    if (group >= 0) limb[group] += weight.weight;
                }
                if (dominant < 0) throw new InvalidOperationException("Studio game LOD contains an unweighted vertex");
                if (!groups.TryGetValue(dominant, out var vertices)) { vertices = new List<int>(); groups.Add(dominant, vertices); }
                vertices.Add(vertex);
                for (int group = 0; group < limb.Length; group++) if (limb[group] >= .5f)
                {
                    contacts[vertex] = true;
                    int x = Mathf.Clamp((int)(Mathf.InverseLerp(bounds.min.x, bounds.max.x, points[vertex].x) * 18), 0, 17);
                    int z = Mathf.Clamp((int)(Mathf.InverseLerp(bounds.min.z, bounds.max.z, points[vertex].z) * 18), 0, 17);
                    int cell = group * 18 * 18 + x * 18 + z;
                    if (!cells.TryGetValue(cell, out int previous) || points[vertex].y < points[previous].y) cells[cell] = vertex;
                }
            }
            if (cursor != weights.Length) throw new InvalidOperationException("Studio support authoring weight order differs");
            foreach (int vertex in cells.Values) selected.Add(vertex);
            // Extreme points in 124 directions for each dominant-bone surface
            // keep the lowest posed body, including the school fall, represented.
            // These indices are a sampling aid; diagnostics compare them to the
            // full actual mesh and must reject >4 cm ground error.
            foreach (var vertices in groups.Values) for (int x = -2; x <= 2; x++) for (int y = -2; y <= 2; y++) for (int z = -2; z <= 2; z++)
            {
                if (x == 0 && y == 0 && z == 0) continue;
                var direction = new Vector3(x, y, z); float best = float.NegativeInfinity; int winner = -1;
                foreach (int vertex in vertices)
                {
                    float distance = Vector3.Dot(points[vertex], direction);
                    if (distance > best) { best = distance; winner = vertex; }
                }
                if (winner >= 0) selected.Add(winner);
            }
            var indices = selected.OrderBy(index => index).ToArray();
            return new EnemyStudioRig.SurfaceSupport { skin = skin, vertexIndices = indices, limbContact = indices.Select(index => contacts[index]).ToArray() };
        }

        static int ContactGroup(string name)
        {
            if (name.StartsWith("mixamorig:LeftFoot", StringComparison.Ordinal) || name.StartsWith("mixamorig:LeftToe", StringComparison.Ordinal)) return 0;
            if (name.StartsWith("mixamorig:RightFoot", StringComparison.Ordinal) || name.StartsWith("mixamorig:RightToe", StringComparison.Ordinal)) return 1;
            if (name.StartsWith("mixamorig:LeftHand", StringComparison.Ordinal)) return 2;
            if (name.StartsWith("mixamorig:RightHand", StringComparison.Ordinal)) return 3;
            return -1;
        }

        [Serializable] sealed class ImportReport
        {
            public string status, key, sourceFbx, prefab;
            public int vertices, triangles, bones, supportVertices;
            public float targetHeight, posedHeight, groundMin, maximumSupportGap;
            public string[] clips; public float[] clipSeconds; public int[] clipCurves;
        }
    }
}
