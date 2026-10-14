using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace HappyToy.V2.Editor
{
    // Explicit art tool: renders the existing patrol-zero mesh for aligned texture editing.
    public static class CreatureFaceAuthoring
    {
        [Serializable] public sealed class Profile
        {
            public string key;
            public Vector3 centre, forward;
            public Vector2 radius;
            public float size, frontStart, frontEnd;
            public Vector3[] eyes;
        }
        [Serializable] sealed class Eye { public Vector2 uv; }
        [Serializable] sealed class Eyes { public Eye[] eyes; }
        public static void RenderTargets()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            foreach (string key in new[] { "Uncat", "Baby" }) Render(key, false);
            Debug.Log("CREATURE_FACE_TARGETS_PASS");
        }
        public static void RenderApplied()
        {
            CreatureFaceImport.Ensure();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            foreach (string key in new[] { "Uncat", "Baby" }) Render(key, true);
            Debug.Log("CREATURE_FACE_APPLIED_PREVIEW_PASS");
        }
        static void Render(string key, bool applied)
        {
            string folder = "SourceArt/ChaseFaces/" + key; Directory.CreateDirectory(folder);
            var root = UnityEngine.Object.Instantiate(Resources.Load<GameObject>($"EnemyStudio/{key}/{key}Runtime"));
            Resources.Load<AnimationClip>($"EnemyStudio/{key}/patrol").SampleAnimation(root, 0);
            var skin = root.GetComponentsInChildren<SkinnedMeshRenderer>().Single();
            var posed = EnemyStudioRig.PosedVertices(skin); var uv = skin.sharedMesh.uv;
            var eyes = JsonUtility.FromJson<Eyes>(File.ReadAllText($"Assets/Resources/EnemyStudio/{key}/eyes-profile.json"));
            var anchors = eyes.eyes.Select(e => posed[Enumerable.Range(0, uv.Length).OrderBy(i => (uv[i] - e.uv).sqrMagnitude).First()]).ToArray();
            var mid = (anchors[0] + anchors[1]) * .5f;
            float span = Vector3.Distance(anchors[0], anchors[1]);
            var forward = Vector3.Cross(anchors[1] - anchors[0], Vector3.up).normalized;
            if (forward.z < 0) forward = -forward;
            var profile = new Profile { key = key, eyes = anchors, centre = mid - Vector3.up * span * .3f, forward = forward,
                size = span * 3.8f, radius = new Vector2(span * 1.35f, span * 1.48f),
                frontStart = -span * .8f, frontEnd = -span * .35f };
            // Uncat's long muzzle and original tooth surfaces extend far behind
            // its eyelids. Include that mouth depth while excluding the rear head.
            if (key == "Uncat") { profile.frontStart = -span * 1.7f; profile.frontEnd = -span * .95f; profile.radius.y = span * 1.8f; }
            if (!applied) File.WriteAllText(folder + "/projection.json", JsonUtility.ToJson(profile, true));
            var mesh = new Mesh { indexFormat = skin.sharedMesh.indexFormat, vertices = posed, uv = uv, subMeshCount = skin.sharedMesh.subMeshCount };
            for (int i = 0; i < mesh.subMeshCount; i++) mesh.SetTriangles(skin.sharedMesh.GetTriangles(i), i);
            mesh.RecalculateBounds(); mesh.RecalculateNormals();
            var display = new GameObject(key + " texture edit target"); display.AddComponent<MeshFilter>().sharedMesh = mesh;
            var expression = applied ? Resources.Load<CyclopseExpressionData>($"ChaseFaces/{key}/Expression") : null;
            GameObject lining = null;
            if (expression && expression.mouthLiner)
            {
                var head = root.GetComponentsInChildren<Transform>().Single(t => t.name == expression.mouthLinerBone);
                lining = new GameObject("Authored mouth interior preview"); lining.transform.SetPositionAndRotation(head.TransformPoint(expression.mouthLinerOffset), head.rotation); lining.transform.localScale = head.lossyScale;
                lining.AddComponent<MeshFilter>().sharedMesh = expression.mouthLiner;
                var material = new Material(Shader.Find("Universal Render Pipeline/Unlit")); material.SetColor("_BaseColor", new Color(.012f, .003f, .002f));
                lining.AddComponent<MeshRenderer>().sharedMaterial = material;
            }
            if (expression) { mesh.uv2 = expression.faceUV; mesh.colors = expression.faceMask; }
            var materials = skin.sharedMaterials.Select(m => {
                var copy = new Material(Shader.Find(applied ? "Hidden/HappyToy/CyclopseExpressionPreview" : "Universal Render Pipeline/Unlit"));
                copy.SetTexture("_BaseMap", m.GetTexture("_BaseMap"));
                if (expression) { copy.SetTexture("_FaceMap", expression.chaseFace); copy.SetFloat("_ExpressionWeight", 1); } return copy;
            }).ToArray();
            display.AddComponent<MeshRenderer>().sharedMaterials = materials; root.SetActive(false);
            var camera = new GameObject("Face texture projection").AddComponent<Camera>(); camera.enabled = false;
            camera.orthographic = true; camera.orthographicSize = profile.size * .5f; camera.aspect = 1;
            camera.nearClipPlane = .01f; camera.farClipPlane = 10; camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.025f, .03f, .04f); camera.transform.position = profile.centre + forward * 3;
            camera.transform.LookAt(profile.centre); camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
            var target = new RenderTexture(1024, 1024, 24); target.Create();
            RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
            var previous = RenderTexture.active; RenderTexture.active = target;
            var image = new Texture2D(1024, 1024, TextureFormat.RGB24, false); image.ReadPixels(new Rect(0, 0, 1024, 1024), 0, 0); image.Apply();
            string destination = applied ? "Builds/ChaseFace-ArtReview/" + key : folder;
            Directory.CreateDirectory(destination);
            File.WriteAllBytes(destination + (applied ? "/applied.png" : "/generation-edit-target.png"), image.EncodeToPNG()); RenderTexture.active = previous;
            Debug.Log(key + " FACE_PROJECTION " + JsonUtility.ToJson(profile));
            target.Release(); UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(image);
            foreach (var m in materials) UnityEngine.Object.DestroyImmediate(m);
            if (lining) { UnityEngine.Object.DestroyImmediate(lining.GetComponent<MeshRenderer>().sharedMaterial); UnityEngine.Object.DestroyImmediate(lining); }
            UnityEngine.Object.DestroyImmediate(camera.gameObject); UnityEngine.Object.DestroyImmediate(display);
            UnityEngine.Object.DestroyImmediate(mesh); UnityEngine.Object.DestroyImmediate(root);
        }
    }
}
