using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;

namespace HappyToy.V2.Editor
{
    public static class CreatureFaceImport
    {
        public static void Ensure()
        {
            foreach (string key in new[] { "Uncat", "Baby" })
            {
                string folder = "Assets/Resources/ChaseFaces/" + key;
                var data = AssetDatabase.LoadAssetAtPath<CyclopseExpressionData>(folder + "/Expression.asset");
                if (!data || !data.chaseFace || !data.shader || data.sourceMeshHash != InputHash(key) ||
                    !AssetDatabase.LoadAssetAtPath<Material>(folder + "/ExpressionMaterial.mat")) Import(key);
            }
        }
        public static void Apply() { foreach (string key in new[] { "Uncat", "Baby" }) Import(key); }
        static string InputHash(string key)
        {
            // Include the recipe as well as model, pose, projection and generated original.
            var paths = new[] { $"Assets/Resources/EnemyStudio/{key}/{key}.fbx", $"Assets/Resources/EnemyStudio/{key}/patrol.anim",
                $"SourceArt/ChaseFaces/{key}/projection.json", $"SourceArt/ChaseFaces/{key}/chase-face-generated-original.png", "Assets/Editor/CreatureFaceImport.cs" };
            using (var sha = SHA256.Create())
                return string.Join(":", paths.Select(p => BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(p))).Replace("-", "").ToLowerInvariant()));
        }
        static void Import(string key)
        {
            string folder = "Assets/Resources/ChaseFaces/" + key; Directory.CreateDirectory(folder);
            string source = $"SourceArt/ChaseFaces/{key}/chase-face-generated-original.png", texture = folder + "/chase-face.png";
            if (!File.Exists(texture) || !File.ReadAllBytes(texture).SequenceEqual(File.ReadAllBytes(source)))
            { AssetDatabase.ReleaseCachedFileHandles(); File.Copy(source, texture, true); }
            AssetDatabase.Refresh();
            var importer = (TextureImporter)AssetImporter.GetAtPath(texture);
            importer.textureType = TextureImporterType.Default; importer.sRGBTexture = true;
            importer.npotScale = TextureImporterNPOTScale.None; importer.maxTextureSize = 2048;
            importer.textureCompression = TextureImporterCompression.Uncompressed; importer.mipmapEnabled = true;
            importer.wrapMode = TextureWrapMode.Clamp; importer.SaveAndReimport();
            var root = UnityEngine.Object.Instantiate(Resources.Load<GameObject>($"EnemyStudio/{key}/{key}Runtime"));
            try
            {
                Resources.Load<AnimationClip>($"EnemyStudio/{key}/patrol").SampleAnimation(root, 0);
                var skin = EnemyStudioGameImport.PrimarySkin(root);
                var posed = EnemyStudioRig.PosedVertices(skin);
                var profile = JsonUtility.FromJson<CreatureFaceAuthoring.Profile>(File.ReadAllText($"SourceArt/ChaseFaces/{key}/projection.json"));
                var data = AssetDatabase.LoadAssetAtPath<CyclopseExpressionData>(folder + "/Expression.asset");
                if (!data) { data = ScriptableObject.CreateInstance<CyclopseExpressionData>(); AssetDatabase.CreateAsset(data, folder + "/Expression.asset"); }
                // The generated expression is painted on the existing shape. Preserve the
                // authored jaw, tongue, eye geometry, normals, skeleton and support samples.
                data.bindDeltas = new Vector3[posed.Length]; data.faceUV = new Vector2[posed.Length]; data.faceMask = new Color[posed.Length];
                var right = Vector3.Cross(Vector3.up, profile.forward).normalized;
                for (int i = 0; i < posed.Length; i++)
                {
                    var delta = posed[i] - profile.centre;
                    float x = Vector3.Dot(delta, right), z = Vector3.Dot(delta, profile.forward);
                    data.faceUV[i] = new Vector2(.5f - x / profile.size, .5f + delta.y / profile.size);
                    float radius = new Vector2(x / profile.radius.x, delta.y / profile.radius.y).magnitude;
                    float mask = (1 - Mathf.SmoothStep(0, 1, (radius - .74f) / .26f)) *
                        Mathf.SmoothStep(0, 1, (z - profile.frontStart) / (profile.frontEnd - profile.frontStart));
                    data.faceMask[i] = new Color(mask, mask, mask, 1);
                }
                data.chaseFace = AssetDatabase.LoadAssetAtPath<Texture2D>(texture);
                data.shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Resources/CyclopseExpression/ChaseFace.shader");
                if (!data.shader) throw new InvalidOperationException("Shared lit chase-face shader missing");
                var template = AssetDatabase.LoadAssetAtPath<Material>(folder + "/ExpressionMaterial.mat");
                if (!template) { template = new Material(data.shader); AssetDatabase.CreateAsset(template, folder + "/ExpressionMaterial.mat"); }
                template.CopyPropertiesFromMaterial(skin.sharedMaterial); template.shader = data.shader;
                template.EnableKeyword("_EMISSION"); template.SetTexture("_ChaseFace", data.chaseFace); EditorUtility.SetDirty(template);
                data.sourceMeshHash = InputHash(key); EditorUtility.SetDirty(data); AssetDatabase.SaveAssets();
                if (key == "Uncat") BakeMouthLiner(root, profile, data, folder);
                Debug.Log("CREATURE_CHASE_FACE_IMPORT_PASS " + key + " vertices=" + posed.Length);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
        static void BakeMouthLiner(GameObject root, CreatureFaceAuthoring.Profile profile, CyclopseExpressionData data, string folder)
        {
            // Cap the existing open mouth behind its original teeth/tongue. Without
            // this small interior the old model exposes the torso through its mouth.
            var head = root.GetComponentsInChildren<Transform>().Single(t => t.name == "mixamorig:Head");
            var right = Vector3.Cross(Vector3.up, profile.forward).normalized;
            var centre = profile.centre - Vector3.up * .18f - profile.forward * .12f;
            var offset = head.InverseTransformPoint(centre);
            const int sides = 48;
            var vertices = new Vector3[sides + 1]; var triangles = new int[sides * 3];
            vertices[0] = Vector3.zero;
            for (int i = 0; i < sides; i++)
            {
                float angle = i * Mathf.PI * 2 / sides;
                vertices[i + 1] = head.InverseTransformPoint(centre + right * (.17f * Mathf.Cos(angle)) + Vector3.up * (.13f * Mathf.Sin(angle))) - offset;
                triangles[i * 3] = 0; triangles[i * 3 + 1] = i + 1; triangles[i * 3 + 2] = (i + 1) % sides + 1;
            }
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(folder + "/MouthInterior.asset");
            if (!mesh) { mesh = new Mesh(); AssetDatabase.CreateAsset(mesh, folder + "/MouthInterior.asset"); }
            mesh.Clear(); mesh.name = "Uncat chase mouth interior"; mesh.vertices = vertices; mesh.triangles = triangles; mesh.RecalculateNormals(); mesh.RecalculateBounds();
            var material = AssetDatabase.LoadAssetAtPath<Material>(folder + "/MouthInterior.mat");
            if (!material) { material = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(material, folder + "/MouthInterior.mat"); }
            material.SetColor("_BaseColor", new Color(.012f, .003f, .002f)); material.SetFloat("_Smoothness", .05f);
            data.mouthLiner = mesh; data.mouthLinerMaterial = material; data.mouthLinerBone = head.name;
            data.mouthLinerOffset = offset;
            EditorUtility.SetDirty(mesh); EditorUtility.SetDirty(material); EditorUtility.SetDirty(data); AssetDatabase.SaveAssets();
        }
    }
}
