using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;

namespace HappyToy.V2.Editor
{
    public static class BaeksugwiGameLodImport
    {
        const string Source = "SourceArt/BaeksugwiStudio/";
        const string Model = "Assets/Resources/Baeksugwi/GameLodModel.fbx";
        const string Profile = "Assets/Resources/Baeksugwi/GameLod.asset";
        [Serializable] sealed class SourceReport
        { public string status, sourceSHA256, recipeSHA256, blendSHA256, fbxSHA256; }
        static void ValidateSources()
        {
            string report = Source + "game-lod-report.json";
            if (!File.Exists(report)) throw new InvalidOperationException("Generate the reviewed derived Baeksugwi LOD source first");
            var data = JsonUtility.FromJson<SourceReport>(File.ReadAllText(report));
            if (data.status != "PASS" || data.sourceSHA256 != Hash(Source + "Baeksugwi_Run.blend") ||
                data.recipeSHA256 != Hash(Source + "game-lod-recipe.json") ||
                data.blendSHA256 != Hash(Source + "Baeksugwi_GameLOD.blend") || data.fbxSHA256 != Hash(Source + "Baeksugwi_GameLOD.fbx"))
                throw new InvalidOperationException("Derived Baeksugwi LOD source/report hashes are stale");
        }
        public static void Ensure()
        {
            ValidateSources();
            var profile = AssetDatabase.LoadAssetAtPath<BaeksugwiGameLod>(Profile);
            if (!profile || profile.sourceHash != Hash(Source + "Baeksugwi_GameLOD.fbx")) Apply();
            else profile.Validate();
        }
        static string Hash(string path)
        {
            using (var algorithm = SHA256.Create())
            using (var stream = File.OpenRead(path))
                return BitConverter.ToString(algorithm.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }
        public static void Apply()
        {
            ValidateSources();
            string file = Source + "Baeksugwi_GameLOD.fbx";
            if (!File.Exists(file) || !File.Exists(Source + "Baeksugwi_GameLOD.blend") || !File.Exists(Source + "game-lod-report.json"))
                throw new InvalidOperationException("Generate the reviewed derived Baeksugwi LOD source first");
            if (!File.Exists(Model) || Hash(Model) != Hash(file)) File.Copy(file, Model, true);
            AssetDatabase.ImportAsset(Model, ImportAssetOptions.ForceSynchronousImport);
            var importer = (ModelImporter)AssetImporter.GetAtPath(Model);
            importer.animationType = ModelImporterAnimationType.Legacy;
            importer.importAnimation = false;
            importer.importCameras = importer.importLights = importer.importBlendShapes = false;
            importer.isReadable = true;
            importer.optimizeGameObjects = false;
            importer.preserveHierarchy = true;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.meshCompression = ModelImporterMeshCompression.Off;
            importer.importNormals = ModelImporterNormals.Import;
            importer.importTangents = ModelImporterTangents.CalculateMikk;
            importer.skinWeights = ModelImporterSkinWeights.Custom;
            importer.maxBonesPerVertex = 4;
            importer.minBoneWeight = .0001f;
            importer.SaveAndReimport();
            var derived = AssetDatabase.LoadAssetAtPath<GameObject>(Model).GetComponentsInChildren<SkinnedMeshRenderer>(true);
            var original = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Baeksugwi/Baeksugwi.prefab")
                .GetComponentInChildren<SkinnedMeshRenderer>(true);
            var originalNames = original.bones.Select(bone => bone.name).ToArray();
            var originalBind = original.sharedMesh.bindposes;
            var profile = AssetDatabase.LoadAssetAtPath<BaeksugwiGameLod>(Profile);
            if (!profile) { profile = ScriptableObject.CreateInstance<BaeksugwiGameLod>(); AssetDatabase.CreateAsset(profile, Profile); }
            profile.levels = new BaeksugwiGameLod.Level[1];
            for (int index = 0; index < profile.levels.Length; index++)
            {
                var skin = derived.Single(candidate => candidate.name.EndsWith("LOD" + index, StringComparison.Ordinal));
                var names = skin.bones.Select(bone => bone.name).ToArray();
                var bind = skin.sharedMesh.bindposes;
                for (int bone = 0; bone < names.Length; bone++)
                {
                    int source = Array.IndexOf(originalNames, names[bone]);
                    if (source < 0 || !BaeksugwiGameLod.SameBindPose(bind[bone], originalBind[source]))
                        throw new InvalidOperationException("Derived FBX rest binding does not match the original rig: " + names[bone]);
                }
                profile.levels[index] = new BaeksugwiGameLod.Level { mesh = skin.sharedMesh, boneNames = names,
                    screenHeight = .008f, localBounds = new Bounds(Vector3.zero, Vector3.one) };
            }
            profile.sourceHash = Hash(file);
            var clone = UnityEngine.Object.Instantiate(original.transform.root.gameObject);
            try
            {
                profile.Apply(clone.transform);
                var skins = clone.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                var run = Resources.Load<AnimationClip>("Baeksugwi/Run_InPlace");
                var head = Resources.Load<AnimationClip>("Baeksugwi/Head_Writhe");
                var vertices = skins.Select(skin => skin.sharedMesh.vertices).ToArray();
                var weights = skins.Select(skin => skin.sharedMesh.boneWeights).ToArray();
                var binds = skins.Select(skin => skin.sharedMesh.bindposes).ToArray();
                var envelopes = new Bounds[skins.Length];var started = new bool[skins.Length];
                for (int gait = 0; gait < 4; gait++) for (int pose = 0; pose < 8; pose++)
                {
                    run.SampleAnimation(clone, run.length * gait / 4);
                    head.SampleAnimation(clone, head.length * pose / 8);
                    for (int skinIndex = 0; skinIndex < skins.Length; skinIndex++)
                    {
                        var skin = skins[skinIndex];
                        var matrices = skin.bones.Select((bone, index) =>
                            skin.transform.worldToLocalMatrix * bone.localToWorldMatrix * binds[skinIndex][index]).ToArray();
                        for (int index = 0; index < vertices[skinIndex].Length; index++)
                        {
                            var vertex = vertices[skinIndex][index];var weight = weights[skinIndex][index];
                            var value = matrices[weight.boneIndex0].MultiplyPoint3x4(vertex) * weight.weight0;
                            if (weight.weight1 > 0) value += matrices[weight.boneIndex1].MultiplyPoint3x4(vertex) * weight.weight1;
                            if (weight.weight2 > 0) value += matrices[weight.boneIndex2].MultiplyPoint3x4(vertex) * weight.weight2;
                            if (weight.weight3 > 0) value += matrices[weight.boneIndex3].MultiplyPoint3x4(vertex) * weight.weight3;
                            if (!started[skinIndex]) { envelopes[skinIndex] = new Bounds(value, Vector3.zero); started[skinIndex] = true; }
                            else envelopes[skinIndex].Encapsulate(value);
                        }
                    }
                }
                // Conservative local skin bounds cover the derived game mesh and
                // the original run/head extrema, including off-beat positions.
                var originalEnvelope = Resources.Load<BaeksugwiHallwayPresentation>("Baeksugwi/HallwayPresentation").sourceEnvelope;
                for (int index = 0; index < skins.Length; index++)
                {
                    foreach (int x in new[] { -1, 1 }) foreach (int y in new[] { -1, 1 }) foreach (int z in new[] { -1, 1 })
                        envelopes[index].Encapsulate(skins[index].transform.InverseTransformPoint(originalEnvelope.center +
                            Vector3.Scale(originalEnvelope.extents, new Vector3(x, y, z))));
                    envelopes[index].Expand(.06f);
                    profile.levels[index].localBounds = envelopes[index];
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(clone); }
            profile.Validate();
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            Debug.Log("BAEKSUGWI_GAME_LOD_IMPORT_PASS original prefab/title/rig/clips preserved; triangles=" +
                string.Join(",", profile.levels.Select(level => level.mesh.triangles.Length / 3)));
        }
    }
}
