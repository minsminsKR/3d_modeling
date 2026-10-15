using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;

namespace HappyToy.V2.Editor
{
    public static class CyclopseExpressionImport
    {
        const string Folder = "Assets/Resources/CyclopseExpression";
        public static void Ensure()
        {
            var data = AssetDatabase.LoadAssetAtPath<CyclopseExpressionData>(Folder + "/Expression.asset");
            using (var sha = SHA256.Create())
            {
                string meshHash = BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes("Assets/Resources/EnemyStudio/Cyclopse/Cyclopse.fbx"))).Replace("-", "").ToLowerInvariant();
                if (!data || !data.chaseFace || !data.shader || data.sourceMeshHash != meshHash ||
                    !AssetDatabase.LoadAssetAtPath<Material>(Folder + "/ExpressionMaterial.mat") ||
                    !data.cabinetFace || data.cabinetFace.sourceHash != CyclopseCabinetFaceImport.InputHash()) Apply();
            }
        }
        public static void Apply()
        {
            Directory.CreateDirectory(Folder);
            string texturePath = Folder + "/chase-face.png";
            string sourceTexture = "SourceArt/CyclopseExpression/chase-face-generated-original.png";
            // Unity can memory-map an already imported PNG on Windows. Avoid
            // reopening identical bytes for writing on every reproducible import.
            if (!File.Exists(texturePath) || !File.ReadAllBytes(texturePath).SequenceEqual(File.ReadAllBytes(sourceTexture)))
            {
                AssetDatabase.ReleaseCachedFileHandles();
                File.Copy(sourceTexture, texturePath, true);
            }
            AssetDatabase.Refresh();
            var importer = (TextureImporter)AssetImporter.GetAtPath(texturePath);
            importer.textureType = TextureImporterType.Default; importer.sRGBTexture = true;
            importer.npotScale = TextureImporterNPOTScale.None; importer.maxTextureSize = 2048;
            importer.textureCompression = TextureImporterCompression.Uncompressed; importer.mipmapEnabled = true;
            importer.wrapMode = TextureWrapMode.Clamp; importer.SaveAndReimport();
            var root = UnityEngine.Object.Instantiate(Resources.Load<GameObject>("EnemyStudio/Cyclopse/CyclopseRuntime"));
            try
            {
                Resources.Load<AnimationClip>("EnemyStudio/Cyclopse/patrol").SampleAnimation(root, 0);
                var skin = root.GetComponentsInChildren<SkinnedMeshRenderer>().Single();
                var posed = EnemyStudioRig.PosedVertices(skin); var mesh = skin.sharedMesh;
                var profile = JsonUtility.FromJson<CyclopseExpressionPreview.Profile>(File.ReadAllText("SourceArt/CyclopseExpression/grin-profile.json"));
                var data = AssetDatabase.LoadAssetAtPath<CyclopseExpressionData>(Folder + "/Expression.asset");
                if (!data) { data = ScriptableObject.CreateInstance<CyclopseExpressionData>(); AssetDatabase.CreateAsset(data, Folder + "/Expression.asset"); }
                data.bindDeltas = new Vector3[posed.Length]; data.faceUV = new Vector2[posed.Length]; data.faceMask = new Color[posed.Length];
                var bones = skin.bones; var bind = mesh.bindposes;
                var matrices = bones.Select((bone, i) => bone.localToWorldMatrix * bind[i]).ToArray();
                var counts = mesh.GetBonesPerVertex(); var weights = mesh.GetAllBoneWeights(); int cursor = 0;
                var eyeUv = new Vector2(.078195f, .330003f); var uv = mesh.uv;
                int eyeIndex = Enumerable.Range(0, uv.Length).OrderBy(i => (uv[i] - eyeUv).sqrMagnitude).First();
                var centre = posed[eyeIndex] + new Vector3(0, -.09f, -.04f);
                for (int i = 0; i < posed.Length; i++)
                {
                    Matrix4x4 matrix = Matrix4x4.zero;
                    for (int j = 0; j < counts[i]; j++)
                    {
                        var influence = weights[cursor++]; var source = matrices[influence.boneIndex];
                        for (int element = 0; element < 16; element++) matrix[element] += source[element] * influence.weight;
                    }
                    var grin = CyclopseExpressionPreview.Sculpt(posed[i], profile, 1);
                    data.bindDeltas[i] = matrix.inverse.MultiplyVector(grin - posed[i]);
                    // The approved orthographic review looks from +Z; screen right is -X.
                    data.faceUV[i] = new Vector2(.5f - (grin.x - centre.x) / .86f, .5f + (grin.y - centre.y) / .86f);
                    float radius = new Vector2(posed[i].x / .49f, (posed[i].y - 1.65f) / .47f).magnitude;
                    float mask = (1 - Mathf.SmoothStep(0, 1, (radius - .72f) / .28f)) * Mathf.SmoothStep(0, 1, (posed[i].z - .40f) / .18f);
                    data.faceMask[i] = new Color(mask, mask, mask, 1);
                }
                data.cabinetFace = CyclopseCabinetFaceImport.Bake(skin, posed, profile);
                data.chaseFace = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
                data.shader = AssetDatabase.LoadAssetAtPath<Shader>(Folder + "/ChaseFace.shader");
                if (!data.shader) throw new InvalidOperationException("Chase face lighting shader missing");
                var template = AssetDatabase.LoadAssetAtPath<Material>(Folder + "/ExpressionMaterial.mat");
                if (!template) { template = new Material(data.shader); AssetDatabase.CreateAsset(template, Folder + "/ExpressionMaterial.mat"); }
                template.CopyPropertiesFromMaterial(skin.sharedMaterial); template.shader = data.shader;
                template.EnableKeyword("_EMISSION"); template.SetTexture("_ChaseFace", data.chaseFace);
                EditorUtility.SetDirty(template);
                using (var sha = SHA256.Create()) data.sourceMeshHash = BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes("Assets/Resources/EnemyStudio/Cyclopse/Cyclopse.fbx"))).Replace("-", "").ToLowerInvariant();
                EditorUtility.SetDirty(data); AssetDatabase.SaveAssets();
                Debug.Log("CYCLOPSE_CHASE_EXPRESSION_IMPORT_PASS vertices=" + posed.Length);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
    }
}
