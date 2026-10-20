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
    // Opt-in art proposal: renders a posed copy, never saves a scene or changes game assets.
    public static class CyclopseExpressionPreview
    {
        [Serializable] public sealed class Profile
        {
            public Vector3 mouth, eye;
            public float mouthWidth = .13f, mouthHeight = .065f, faceDepth = .14f;
            public float smileWidth = .065f, smileLift = .065f, jawDrop = .035f;
            public float eyeWidth = .08f, eyeHeight = .07f, eyePinch = .022f;
            public int frames = 90, size = 768;
        }
        [Serializable] sealed class Inspection
        {
            public Bounds bounds;
            public Vector3 eye, head;
            public string scope = "Actual CyclopseRuntime patrol-zero surface; original UV/albedo blends to newly generated face texture through a second projected UV set. Unlit art proposal, not integrated into gameplay.";
        }
        public static void Render()
        {
            string output = "Builds/CyclopseExpression-Preview";
            var args = Environment.GetCommandLineArgs(); int flag = Array.IndexOf(args, "-expression-output");
            if (flag >= 0 && flag + 1 < args.Length) output = args[flag + 1];
            Directory.CreateDirectory(output);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var source = UnityEngine.Object.Instantiate(Resources.Load<GameObject>("EnemyStudio/Cyclopse/CyclopseRuntime"));
            Resources.Load<AnimationClip>("EnemyStudio/Cyclopse/patrol").SampleAnimation(source, 0);
            foreach (var behaviour in source.GetComponentsInChildren<Behaviour>()) behaviour.enabled = false;
            var skin = EnemyStudioGameImport.PrimarySkin(source);
            var posed = EnemyStudioRig.PosedVertices(skin);
            var mesh = new Mesh { indexFormat = skin.sharedMesh.indexFormat, name = "Cyclopse expression proposal posed surface" };
            mesh.vertices = posed; mesh.uv = skin.sharedMesh.uv;
            mesh.triangles = skin.sharedMesh.triangles; mesh.RecalculateBounds();
            // Dense facial samples keep the existing low-poly lips from turning into
            // straight wedges when the nonlinear grin stretches them. Unposed positions
            // and UVs are linear edge interpolation; the source mesh stays untouched.
            SubdivideFace(mesh); SubdivideFace(mesh); posed = mesh.vertices;
            mesh.RecalculateNormals();
            var uv = mesh.uv; var eyeUv = new Vector2(.078195f, .330003f);
            int eyeIndex = Enumerable.Range(0, uv.Length).OrderBy(i => (uv[i] - eyeUv).sqrMagnitude).First();
            var eye = posed[eyeIndex];
            var head = source.GetComponentsInChildren<Transform>().Single(t => t.name == "mixamorig:Head");
            File.WriteAllText(Path.Combine(output, "inspection.json"), JsonUtility.ToJson(new Inspection { bounds = mesh.bounds, eye = eye, head = head.position }, true));
            // Retain an OBJ inspection copy for precise, reproducible landmark authoring.
            using (var file = new StreamWriter(Path.Combine(output, "posed-surface.obj")))
            {
                foreach (var v in posed) file.WriteLine(FormattableString.Invariant($"v {v.x:R} {v.y:R} {v.z:R}"));
                var triangles = mesh.triangles;
                for (int i = 0; i < triangles.Length; i += 3) file.WriteLine($"f {triangles[i] + 1} {triangles[i+1] + 1} {triangles[i+2] + 1}");
            }
            var display = new GameObject("Proposed Cyclopse face");
            display.AddComponent<MeshFilter>().sharedMesh = mesh;
            const string facePath = "SourceArt/CyclopseExpression/chase-face-generated-original.png";
            Texture2D generatedFace = null;
            if (File.Exists(facePath))
            {
                generatedFace = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!generatedFace.LoadImage(File.ReadAllBytes(facePath))) throw new InvalidDataException("Generated face PNG could not be decoded");
                generatedFace.wrapMode = TextureWrapMode.Clamp;
            }
            var previewMaterials = skin.sharedMaterials.Select(m =>
            {
                var shader = Shader.Find(generatedFace ? "Hidden/HappyToy/CyclopseExpressionPreview" : "Universal Render Pipeline/Unlit");
                if (!shader || !shader.isSupported) throw new InvalidOperationException("Expression review shader unavailable");
                var material = new Material(shader);
                material.SetTexture("_BaseMap", m.GetTexture("_BaseMap"));
                if (generatedFace) material.SetTexture("_FaceMap", generatedFace); return material;
            }).ToArray();
            display.AddComponent<MeshRenderer>().sharedMaterials = previewMaterials;
            source.SetActive(false);
            RenderSettings.fog = false;
            var camera = new GameObject("Face proposal camera").AddComponent<Camera>();
            camera.enabled = false; camera.orthographic = true; camera.orthographicSize = .43f;
            camera.nearClipPlane = .01f; camera.farClipPlane = 10; camera.aspect = 1;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.025f, .03f, .04f);
            var centre = new Vector3(eye.x, eye.y - .09f, eye.z - .04f);
            float forward = Mathf.Sign(eye.z - mesh.bounds.center.z);
            camera.transform.position = centre + new Vector3(0, 0, forward * 2);
            camera.transform.LookAt(centre);
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
            string profilePath = "SourceArt/CyclopseExpression/grin-profile.json";
            var profile = File.Exists(profilePath) ? JsonUtility.FromJson<Profile>(File.ReadAllText(profilePath)) : null;
            if (profile != null && generatedFace)
            {
                // The generated reference depicts the FULL grin. Fix its projection UVs
                // on that 3D endpoint; they then follow each lip/eyelid during the morph.
                mesh.uv2 = posed.Select(v => (Vector2)camera.WorldToViewportPoint(Sculpt(v, profile, 1))).ToArray();
                mesh.colors = posed.Select(v =>
                {
                    float radius = new Vector2(v.x / .49f, (v.y - 1.65f) / .47f).magnitude;
                    float mask = (1 - Mathf.SmoothStep(0, 1, (radius - .72f) / .28f)) * Mathf.SmoothStep(0, 1, (v.z - .40f) / .18f);
                    return new Color(mask, mask, mask, 1);
                }).ToArray();
            }
            int size = profile == null ? 768 : profile.size;
            var target = new RenderTexture(size, size, 24); target.Create();
            var image = new Texture2D(size, size, TextureFormat.RGB24, false);
            try
            {
                int count = profile == null ? 1 : profile.frames;
                for (int frame = 0; frame < count; frame++)
                {
                    float seconds = frame / 18f;
                    float weight = seconds < .6f ? 0 : seconds < 2 ? Mathf.SmoothStep(0, 1, (seconds - .6f) / 1.4f) :
                        seconds < 3.7f ? 1 : 1 - Mathf.SmoothStep(0, 1, (seconds - 3.7f) / 1.1f);
                    if (profile != null)
                    {
                        mesh.vertices = posed.Select(v => Sculpt(v, profile, weight)).ToArray();
                        mesh.RecalculateBounds();
                        if (generatedFace) foreach (var material in previewMaterials) material.SetFloat("_ExpressionWeight", weight);
                    }
                    RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
                    var previous = RenderTexture.active; RenderTexture.active = target;
                    image.ReadPixels(new Rect(0, 0, size, size), 0, 0); image.Apply(); RenderTexture.active = previous;
                    File.WriteAllBytes(Path.Combine(output, $"frame-{frame:000}.png"), image.EncodeToPNG());
                }
            }
            finally
            {
                target.Release(); UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(image);
                UnityEngine.Object.DestroyImmediate(mesh);
                foreach (var material in previewMaterials) UnityEngine.Object.DestroyImmediate(material);
                if (generatedFace) UnityEngine.Object.DestroyImmediate(generatedFace);
            }
            Debug.Log("CYCLOPSE_EXPRESSION_PREVIEW_READY " + output);
        }
        static float Falloff(float x) => Mathf.Pow(Mathf.Clamp01(1 - x * x), 2);
        static void SubdivideFace(Mesh mesh)
        {
            var vertices = mesh.vertices.ToList(); var uv = mesh.uv.ToList();
            var triangles = mesh.triangles; var next = new System.Collections.Generic.List<int>();
            var edges = new System.Collections.Generic.Dictionary<ulong, int>();
            int Middle(int a, int b)
            {
                ulong key = ((ulong)(uint)Math.Min(a, b) << 32) | (uint)Math.Max(a, b);
                if (edges.TryGetValue(key, out int prior)) return prior;
                int index = vertices.Count; vertices.Add((vertices[a] + vertices[b]) * .5f);
                uv.Add((uv[a] + uv[b]) * .5f); edges[key] = index; return index;
            }
            bool Face(Vector3 p) => Mathf.Abs(p.x) < .5f && p.y > 1.2f && p.y < 2.03f && p.z > .43f;
            for (int i = 0; i < triangles.Length; i += 3)
            {
                int a = triangles[i], b = triangles[i + 1], c = triangles[i + 2];
                if (!Face(vertices[a]) && !Face(vertices[b]) && !Face(vertices[c])) { next.AddRange(new[] { a, b, c }); continue; }
                int ab = Middle(a, b), bc = Middle(b, c), ca = Middle(c, a);
                next.AddRange(new[] { a, ab, ca, ab, b, bc, ca, bc, c, ab, bc, ca });
            }
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(vertices); mesh.SetUVs(0, uv); mesh.SetTriangles(next, 0); mesh.RecalculateBounds();
        }
        public static Vector3 Sculpt(Vector3 vertex, Profile p, float weight)
        {
            var d = vertex - p.mouth;
            float front = Falloff(d.z / p.faceDepth);
            float mouth = Falloff(d.x / p.mouthWidth) * Falloff(d.y / p.mouthHeight) * front;
            float corner = Mathf.Clamp01(Mathf.Abs(d.x) / (p.mouthWidth * .65f));
            float side = d.x >= 0 ? 1.18f : .82f;
            var delta = new Vector3(Mathf.Sign(d.x) * p.smileWidth * corner * mouth,
                p.smileLift * corner * side * mouth - p.jawDrop * mouth * Mathf.Clamp01(-d.y / .025f),
                .015f * mouth * corner);
            var e = vertex - p.eye;
            float eyelid = Falloff(e.x / p.eyeWidth) * Falloff(e.y / p.eyeHeight) * Falloff(e.z / p.faceDepth);
            delta.y -= p.eyePinch * eyelid * Mathf.Clamp01(e.y / .025f);
            delta.y += p.eyePinch * .6f * eyelid * Mathf.Clamp01(-e.y / .025f);
            float delayed = Mathf.Lerp(weight * weight, weight, Mathf.SmoothStep(0, 1, (d.x + .08f) / .16f));
            return vertex + delta * delayed;
        }
    }
}
