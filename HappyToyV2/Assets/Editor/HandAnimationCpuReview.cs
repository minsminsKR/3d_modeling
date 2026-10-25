using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace HappyToy.V2.Editor
{
    // Explicit CPU geometry export only. Never renders, enters Play Mode or opens a scene.
    public static class HandAnimationCpuReview
    {
        public static void ImportAndExport() { PlayerItemsImport.ApplyHand(); Export(); }
        const string Flag = "-v2-hand-cpu-review-output";
        static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;
        static readonly Dictionary<string, string> Hashes = new Dictionary<string, string>();
        static readonly Vector2[] CenterCorners = { new Vector2(.435f, .455f), new Vector2(.565f, .455f),
            new Vector2(.565f, .64f), new Vector2(.435f, .64f) };
        [Serializable] public sealed class Input { public string path, sha256; }
        [Serializable] public sealed class MaterialRecord
        {
            public string name, sourceName, shader, albedo, normal, metalRough;
            public Color tint;
            public Vector2 uvScale, uvOffset;
            public float metallic, smoothness, normalScale, pbrLayout, opacity, monochrome;
            public bool flame;
        }
        [Serializable] public sealed class MeshRecord
        {
            public string name, sourceAsset;
            public int vertices, triangles;
            public bool skinned, cpuBurnClipped, cpuPaperFlex;
            public Vector3 rendererLossyScale;
            public Bounds sharedMeshBounds, bakedUncompensatedBounds, bakedCompensatedBounds, rootSkinPhysicalBounds;
            public float uncompensatedBakeManualMaxError, compensatedBakeManualMaxError;
            public bool useScaleCompensation;
            public int manualSkinSamples;
        }
        [Serializable] public sealed class FrameRecord
        {
            public string name, item, obj, mtl, objSha256, state;
            public float aspect, cameraFov, burnFraction, actionProgress, actionTime;
            public int vertices, triangles, centralRectOccupiedTriangles;
            public Vector3 cameraMin, cameraMax;
            public List<MaterialRecord> materials = new List<MaterialRecord>();
            public List<MeshRecord> meshes = new List<MeshRecord>();
        }
        [Serializable] public sealed class Report
        {
            public string status, unityVersion, scope, axes;
            public string[] limitations;
            public List<Input> inputs = new List<Input>();
            public List<FrameRecord> frames = new List<FrameRecord>();
        }
        struct Vertex
        {
            public Vector3 position, normal;
            public Vector2 uv;
            public float height;
            public static Vertex Lerp(Vertex a, Vertex b, float t) => new Vertex
            { position = Vector3.LerpUnclamped(a.position, b.position, t), normal = Vector3.LerpUnclamped(a.normal, b.normal, t).normalized,
                uv = Vector2.LerpUnclamped(a.uv, b.uv, t), height = Mathf.LerpUnclamped(a.height, b.height, t) };
        }
        sealed class ObjWriter : IDisposable
        {
            readonly StreamWriter writer;
            readonly Camera camera;
            readonly FrameRecord frame;
            int count;
            bool bounded;
            Vector3 minimum, maximum;
            public ObjWriter(string path, Camera camera, FrameRecord frame)
            {
                this.camera = camera; this.frame = frame;
                writer = new StreamWriter(path, false, new UTF8Encoding(false), 1024 * 1024);
                writer.WriteLine("# Actual Unity CPU-baked hand pose; camera x=right y=up z=forward, metres");
                writer.WriteLine("mtllib " + frame.mtl);
            }
            public int Add(Vertex value, Matrix4x4 matrix, Matrix4x4 normalMatrix, Material material)
            {
                Vector3 world = matrix.MultiplyPoint3x4(value.position);
                if (material && material.HasProperty("_PaperFlex") && material.GetFloat("_PaperFlex") > 0)
                    world += (camera.transform.position - world).normalized *
                        HeldPaperEffects.BendOffset(value.height, material.GetFloat("_PaperTime")) * material.GetFloat("_PaperFlex");
                Vector3 position = camera.transform.InverseTransformPoint(world);
                Vector3 normal = normalMatrix.MultiplyVector(value.normal).normalized;
                writer.WriteLine("v " + V(position)); writer.WriteLine("vt " + F(value.uv.x) + " " + F(value.uv.y));
                writer.WriteLine("vn " + V(normal));
                if (!bounded) { minimum = maximum = position; bounded = true; }
                else { minimum = Vector3.Min(minimum, position); maximum = Vector3.Max(maximum, position); }
                return ++count;
            }
            public void Group(string value, string material)
            { writer.WriteLine("o " + value.Replace(' ', '_').Replace('/', '_')); writer.WriteLine("usemtl " + material); }
            public void Face(int a, int b, int c, Vector3 pa, Vector3 pb, Vector3 pc, bool reverse)
            {
                if (reverse) { int t = b; b = c; c = t; }
                writer.WriteLine("f " + a + "/" + a + "/" + a + " " + b + "/" + b + "/" + b + " " + c + "/" + c + "/" + c);
                frame.triangles++;
                if (CentralTriangle(pa, pb, pc, camera.fieldOfView, camera.aspect)) frame.centralRectOccupiedTriangles++;
            }
            public void Dispose()
            { frame.vertices = count; frame.cameraMin = minimum; frame.cameraMax = maximum; writer.Dispose(); }
        }

        public static void Export()
        {
            if (!Application.isBatchMode || SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null)
                throw new InvalidOperationException("CPU review requires an explicit -batchmode -nographics Editor.");
            string[] args = Environment.GetCommandLineArgs(); int at = Array.IndexOf(args, Flag);
            if (at < 0 || at + 1 >= args.Length || !Path.IsPathRooted(args[at + 1]))
                throw new InvalidOperationException("Pass " + Flag + " with an absolute fresh Builds directory.");
            string project = Directory.GetParent(Application.dataPath).FullName;
            string builds = Path.GetFullPath(Path.Combine(project, "Builds"));
            string output = Path.GetFullPath(args[at + 1]);
            if (!output.StartsWith(builds + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Review output must be a child of this project's Builds directory.");
            for (string path = output; !string.IsNullOrEmpty(path); path = Path.GetDirectoryName(path))
            {
                if (Directory.Exists(path) && (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidOperationException("Review output cannot traverse a reparse point.");
                if (path.Equals(builds, StringComparison.OrdinalIgnoreCase)) break;
            }
            if (Directory.Exists(output) && Directory.GetFileSystemEntries(output).Length != 0)
                throw new InvalidOperationException("Use a new empty review output directory; do not reuse old evidence.");
            Directory.CreateDirectory(output); Hashes.Clear();
            var report = new Report
            {
                status = "CPU_GEOMETRY_EXPORT_PASS", unityVersion = Application.unityVersion,
                scope = "Actual Unity SkinnedMeshRenderer.BakeMesh and MeshFilter geometry/material inputs from explicit CPU previews; no graphics or player execution.",
                axes = "OBJ camera-local: X right, Y up, Z forward; metres. Blender renderer maps (x,y,z) to (x,z,y) and reverses faces.",
                limitations = new[] { "Offscreen CPU reference, not a native Unity screenshot or FPS measurement.",
                    "CPU paper clips the mean burn-height plane and matches millimetre flex; irregular char/emission and Unity flame shader are not reproduced.",
                    "Blender reference lighting/PBR and monochrome approximation do not reproduce Unity's custom viewmodel shader; dynamic particle simulation is not captured.",
                    "The still preview samples authored poses, not gameplay physics, collision, action commit or item inventory state.",
                    "Central occupancy counts geometry intersecting viewport rectangle [0.435,0.455]-[0.565,0.64] without a GPU depth buffer." }
            };
            Capture(report, output, "snapper_idle", PlayerItem.Snapper);
            Capture(report, output, "trap_idle", PlayerItem.Trap);
            Capture(report, output, "incense_idle", PlayerItem.Incense);
            Capture(report, output, "talisman_idle", PlayerItem.Talisman);
            Capture(report, output, "flashlight_idle", PlayerItem.Flashlight);
            Capture(report, output, "snapper_before", PlayerItem.Snapper, -1, .28f / .85f);
            Capture(report, output, "snapper_release", PlayerItem.Snapper, -1, .32f / .85f);
            Capture(report, output, "snapper_recover", PlayerItem.Snapper, -1, .65f / .85f);
            Capture(report, output, "talisman_burn50", PlayerItem.Talisman, .5f);
            Capture(report, output, "trap_idle_4x3", PlayerItem.Trap, -1, -1, 4f / 3);
            foreach (string path in new[] { "Assets/Scripts/FirstPersonHands.cs", "Assets/Scripts/HandAnimationRig.cs",
                "Assets/Scripts/HandAnimationLibrary.cs", "Assets/Resources/PlayerHandAnimation/poses.json",
                "Assets/Resources/PlayerHandAnimation/geometry.fbx", "Assets/Resources/PlayerItems/hand.prefab",
                "Assets/Resources/PlayerItemSurface.shader", "Assets/Scripts/HeldPaperEffects.cs" })
                RecordInput(project, path, report);
            foreach (var pair in Hashes) report.inputs.Add(new Input { path = pair.Key, sha256 = pair.Value });
            File.WriteAllText(Path.Combine(output, "manifest.json"), JsonUtility.ToJson(report, true), new UTF8Encoding(false));
            Debug.Log("HAPPYTOY_HAND_CPU_EXPORT_PASS " + output);
        }

        static void Capture(Report report, string output, string name, PlayerItem item, float burn = -1, float action = -1, float aspect = 16f / 9)
        {
            var owned = new GameObject("Owned CPU hand review " + name) { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                var hands = owned.AddComponent<FirstPersonHands>();
                var camera = hands.PrepareCpuPreview(item, burn, action, aspect);
                if (camera.targetTexture || camera.enabled) throw new InvalidOperationException("CPU hand preview attempted graphics setup.");
                var frame = new FrameRecord { name = name, item = item.ToString(), obj = name + ".obj", mtl = name + ".mtl",
                    aspect = camera.aspect, cameraFov = camera.fieldOfView, burnFraction = burn, actionProgress = action,
                    actionTime = action < 0 ? -1 : HandAnimationLibrary.Load().GetClip("Use" + item).duration * action,
                    state = action < 0 ? "Authored idle" : name.EndsWith("release") ? "Exact precommit release-marker pose" : "Authored use sample" };
                string obj = Path.Combine(output, frame.obj);
                using (var writer = new ObjWriter(obj, camera, frame))
                {
                    foreach (var renderer in owned.GetComponentsInChildren<Renderer>(true))
                    {
                        if (!renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
                        Mesh mesh = null, baked = null; MeshRecord audit = null;
                        string source = string.Empty;
                        if (renderer is SkinnedMeshRenderer skin)
                        {
                            source = AssetDatabase.GetAssetPath(skin.sharedMesh);
                            baked = new Mesh { name = "Owned CPU skin bake" };
                            audit = BakeAndCheckScale(skin, baked); mesh = baked;
                        }
                        else if (renderer is MeshRenderer)
                        {
                            var filter = renderer.GetComponent<MeshFilter>(); mesh = filter ? filter.sharedMesh : null;
                            if (mesh) source = AssetDatabase.GetAssetPath(mesh);
                        }
                        try { if (mesh && mesh.vertexCount > 0) WriteMesh(writer, frame, renderer, mesh, source, camera, audit); }
                        finally { if (baked) UnityEngine.Object.DestroyImmediate(baked); }
                    }
                }
                WriteMtl(Path.Combine(output, frame.mtl), frame.materials);
                frame.objSha256 = Hash(obj); report.frames.Add(frame);
            }
            finally { UnityEngine.Object.DestroyImmediate(owned); }
        }
        static MeshRecord BakeAndCheckScale(SkinnedMeshRenderer skin, Mesh destination)
        {
            var audit = new MeshRecord { rendererLossyScale = skin.transform.lossyScale, sharedMeshBounds = skin.sharedMesh.bounds };
            skin.BakeMesh(destination, false);
            audit.bakedUncompensatedBounds = destination.bounds; Vector3[] uncompensated = destination.vertices;
            skin.BakeMesh(destination, true);
            audit.bakedCompensatedBounds = destination.bounds; Vector3[] compensated = destination.vertices;
            Vector3[] original = skin.sharedMesh.vertices;
            BoneWeight[] weights = skin.sharedMesh.boneWeights; Matrix4x4[] bind = skin.sharedMesh.bindposes;
            if (original.Length != compensated.Length || original.Length != uncompensated.Length ||
                weights.Length != original.Length || skin.bones.Length != bind.Length)
                throw new InvalidOperationException("CPU skin audit topology differs from its actual game mesh.");
            for (int i = 0; i < skin.sharedMesh.blendShapeCount; i++)
                if (Mathf.Abs(skin.GetBlendShapeWeight(i)) > .0001f)
                    throw new InvalidOperationException("Active blend shapes need a corresponding manual deformation audit.");
            var matrices = new Matrix4x4[bind.Length];
            for (int i = 0; i < bind.Length; i++)
            {
                if (!skin.bones[i]) throw new InvalidOperationException("CPU skin audit has a missing bound bone.");
                matrices[i] = skin.bones[i].localToWorldMatrix * bind[i];
            }
            audit.manualSkinSamples = Math.Min(16, original.Length);
            for (int sample = 0; sample < audit.manualSkinSamples; sample++)
            {
                int i = audit.manualSkinSamples == 1 ? 0 : sample * (original.Length - 1) / (audit.manualSkinSamples - 1);
                var weight = weights[i]; var vertex = original[i];
                Vector3 world = matrices[weight.boneIndex0].MultiplyPoint3x4(vertex) * weight.weight0;
                if (weight.weight1 > 0) world += matrices[weight.boneIndex1].MultiplyPoint3x4(vertex) * weight.weight1;
                if (weight.weight2 > 0) world += matrices[weight.boneIndex2].MultiplyPoint3x4(vertex) * weight.weight2;
                if (weight.weight3 > 0) world += matrices[weight.boneIndex3].MultiplyPoint3x4(vertex) * weight.weight3;
                audit.uncompensatedBakeManualMaxError = Mathf.Max(audit.uncompensatedBakeManualMaxError,
                    Vector3.Distance(skin.transform.TransformPoint(uncompensated[i]), world));
                audit.compensatedBakeManualMaxError = Mathf.Max(audit.compensatedBakeManualMaxError,
                    Vector3.Distance(skin.transform.TransformPoint(compensated[i]), world));
            }
            audit.useScaleCompensation = audit.compensatedBakeManualMaxError <= audit.uncompensatedBakeManualMaxError;
            float error = audit.useScaleCompensation ? audit.compensatedBakeManualMaxError : audit.uncompensatedBakeManualMaxError;
            if (error > .001f || audit.manualSkinSamples == 0)
                throw new InvalidOperationException("Neither BakeMesh scale variant matches actual weighted bind-pose skinning within 1mm: " + JsonUtility.ToJson(audit));
            if (!audit.useScaleCompensation) skin.BakeMesh(destination, false);
            Vector3[] selected = destination.vertices;
            var physical = new Bounds(skin.transform.TransformPoint(selected[0]), Vector3.zero);
            for (int i = 1; i < selected.Length; i++) physical.Encapsulate(skin.transform.TransformPoint(selected[i]));
            audit.rootSkinPhysicalBounds = physical;
            if (physical.size.magnitude > 2)
                throw new InvalidOperationException("Actual game skin has non-human physical extents: " + JsonUtility.ToJson(audit));
            return audit;
        }
        static void WriteMesh(ObjWriter writer, FrameRecord frame, Renderer renderer, Mesh mesh, string source, Camera camera, MeshRecord audit)
        {
            Vector3[] positions = mesh.vertices, normals = mesh.normals; Vector2[] uv = mesh.uv;
            Material[] materials = renderer.sharedMaterials;
            var record = audit ?? new MeshRecord();
            record.name = renderer.name; record.sourceAsset = source; record.skinned = renderer is SkinnedMeshRenderer;
            record.vertices = positions.Length;
            var matrix = renderer.localToWorldMatrix;
            var normalMatrix = Matrix4x4.Rotate(Quaternion.Inverse(camera.transform.rotation)) * matrix.inverse.transpose;
            bool reverse = matrix.determinant < 0;
            for (int sub = 0; sub < mesh.subMeshCount; sub++)
            {
                Material material = materials[Math.Min(sub, materials.Length - 1)];
                var entry = MaterialInfo(material, renderer, "mat" + frame.materials.Count.ToString("D4")); frame.materials.Add(entry);
                writer.Group(renderer.name + "_" + sub, entry.name);
                bool clipped = material && material.HasProperty("_Burning") && material.GetFloat("_Burning") > .5f;
                bool flex = material && material.HasProperty("_PaperFlex") && material.GetFloat("_PaperFlex") > 0;
                record.cpuBurnClipped |= clipped; record.cpuPaperFlex |= flex;
                Vector4 height = material && material.HasProperty("_BurnHeight") ? material.GetVector("_BurnHeight") : Vector4.zero;
                var values = new Vertex[positions.Length]; var ids = new int[positions.Length];
                for (int i = 0; i < positions.Length; i++)
                {
                    values[i] = new Vertex { position = positions[i], normal = normals.Length == positions.Length ? normals[i] : Vector3.up,
                        uv = uv.Length == positions.Length ? uv[i] : Vector2.zero,
                        height = Vector3.Dot(positions[i], new Vector3(height.x, height.y, height.z)) + height.w };
                    if (!clipped) ids[i] = writer.Add(values[i], matrix, normalMatrix, material);
                }
                int[] triangles = mesh.GetTriangles(sub);
                for (int t = 0; t < triangles.Length; t += 3)
                {
                    int a = triangles[t], b = triangles[t + 1], c = triangles[t + 2];
                    if (!clipped)
                    {
                        writer.Face(ids[a], ids[b], ids[c], CameraPoint(values[a], matrix, material, camera),
                            CameraPoint(values[b], matrix, material, camera), CameraPoint(values[c], matrix, material, camera), reverse);
                        record.triangles++; continue;
                    }
                    if (material.GetFloat("_Burn") >= .99999f) continue;
                    var polygon = Clip(values[a], values[b], values[c], 1 - material.GetFloat("_Burn"));
                    if (polygon.Count < 3) continue;
                    int first = writer.Add(polygon[0], matrix, normalMatrix, material), prior = writer.Add(polygon[1], matrix, normalMatrix, material);
                    for (int i = 2; i < polygon.Count; i++)
                    {
                        int next = writer.Add(polygon[i], matrix, normalMatrix, material);
                        writer.Face(first, prior, next, CameraPoint(polygon[0], matrix, material, camera),
                            CameraPoint(polygon[i - 1], matrix, material, camera), CameraPoint(polygon[i], matrix, material, camera), reverse);
                        prior = next; record.triangles++;
                    }
                }
            }
            frame.meshes.Add(record);
        }
        static Vector3 CameraPoint(Vertex vertex, Matrix4x4 matrix, Material material, Camera camera)
        {
            Vector3 world = matrix.MultiplyPoint3x4(vertex.position);
            if (material && material.HasProperty("_PaperFlex") && material.GetFloat("_PaperFlex") > 0)
                world += (camera.transform.position - world).normalized * HeldPaperEffects.BendOffset(vertex.height,
                    material.GetFloat("_PaperTime")) * material.GetFloat("_PaperFlex");
            return camera.transform.InverseTransformPoint(world);
        }
        static List<Vertex> Clip(Vertex a, Vertex b, Vertex c, float maximumHeight)
        {
            var result = new List<Vertex>(4); Vertex[] input = { a, b, c };
            Vertex previous = c; bool before = previous.height <= maximumHeight;
            foreach (var current in input)
            {
                bool inside = current.height <= maximumHeight;
                if (inside != before)
                    result.Add(Vertex.Lerp(previous, current, (maximumHeight - previous.height) / (current.height - previous.height)));
                if (inside) result.Add(current);
                previous = current; before = inside;
            }
            return result;
        }
        static MaterialRecord MaterialInfo(Material material, Renderer renderer, string name)
        {
            var entry = new MaterialRecord { name = name, sourceName = material ? material.name : "Missing", shader = material && material.shader ? material.shader.name : "",
                tint = Color.white, opacity = 1, smoothness = .35f, normalScale = 1, uvScale = Vector2.one };
            if (!material) return entry;
            string property = material.HasProperty("_BaseMap") ? "_BaseMap" : material.HasProperty("_MainTex") ? "_MainTex" : "_FlameTex";
            entry.albedo = TexturePath(material, property); entry.normal = TexturePath(material, "_BumpMap"); entry.metalRough = TexturePath(material, "_MetalRough");
            entry.uvScale = material.HasProperty(property) ? material.GetTextureScale(property) : Vector2.one;
            entry.uvOffset = material.HasProperty(property) ? material.GetTextureOffset(property) : Vector2.zero;
            entry.flame = material.HasProperty("_FlameTex");
            entry.tint = material.HasProperty("_BaseColor") ? material.GetColor("_BaseColor") : material.HasProperty("_FlameColor") ? material.GetColor("_FlameColor") : Color.white;
            if (material.HasProperty("_HasNormal") && material.GetFloat("_HasNormal") < .5f) entry.normal = string.Empty;
            if (material.HasProperty("_HasMetalRough") && material.GetFloat("_HasMetalRough") < .5f) entry.metalRough = string.Empty;
            if (material.HasProperty("_Smoothness")) entry.smoothness = material.GetFloat("_Smoothness");
            if (material.HasProperty("_Metallic")) entry.metallic = material.GetFloat("_Metallic");
            if (material.HasProperty("_PbrLayout")) entry.pbrLayout = material.GetFloat("_PbrLayout");
            if (material.HasProperty("_BumpScale")) entry.normalScale = material.GetFloat("_BumpScale");
            if (material.HasProperty("_Monochrome")) entry.monochrome = material.GetFloat("_Monochrome");
            if (material.HasProperty("_Opacity")) entry.opacity = material.GetFloat("_Opacity");
            var block = new MaterialPropertyBlock(); renderer.GetPropertyBlock(block);
            if (block.HasFloat("_Opacity")) entry.opacity = block.GetFloat("_Opacity");
            return entry;
        }
        static string TexturePath(Material material, string property)
        {
            if (!material.HasProperty(property) || !material.GetTexture(property)) return string.Empty;
            string asset = AssetDatabase.GetAssetPath(material.GetTexture(property));
            if (string.IsNullOrEmpty(asset)) return string.Empty;
            string project = Directory.GetParent(Application.dataPath).FullName;
            string path = Path.GetFullPath(Path.Combine(project, asset));
            if (!File.Exists(path)) return string.Empty;
            if (!Hashes.ContainsKey(path)) Hashes[path] = Hash(path);
            return path.Replace('\\', '/');
        }
        static void WriteMtl(string path, List<MaterialRecord> materials)
        {
            using (var writer = new StreamWriter(path, false, new UTF8Encoding(false)))
                foreach (var entry in materials)
                {
                    writer.WriteLine("newmtl " + entry.name); writer.WriteLine("Kd " + F(entry.tint.r) + " " + F(entry.tint.g) + " " + F(entry.tint.b));
                    writer.WriteLine("Ks 0.04 0.04 0.04"); writer.WriteLine("Ns 40"); writer.WriteLine("d " + F(entry.opacity * entry.tint.a));
                    if (!string.IsNullOrEmpty(entry.albedo)) writer.WriteLine("map_Kd " + entry.albedo);
                    if (!string.IsNullOrEmpty(entry.normal)) writer.WriteLine("map_Bump " + entry.normal);
                    writer.WriteLine();
                }
        }
        static bool CentralTriangle(Vector3 a, Vector3 b, Vector3 c, float fov, float aspect)
        {
            if (a.z <= .01f || b.z <= .01f || c.z <= .01f) return false;
            float tangent = Mathf.Tan(fov * Mathf.Deg2Rad * .5f);
            Vector2 Project(Vector3 p) => new Vector2(.5f + p.x / (p.z * tangent * aspect * 2), .5f + p.y / (p.z * tangent * 2));
            Vector2 x = Project(a), y = Project(b), z = Project(c);
            bool Inside(Vector2 p) => p.x >= .435f && p.x <= .565f && p.y >= .455f && p.y <= .64f;
            if (Inside(x) || Inside(y) || Inside(z)) return true;
            foreach (var p in CenterCorners) if (InTriangle(p, x, y, z)) return true;
            for (int i = 0; i < 4; i++)
                if (Crosses(x, y, CenterCorners[i], CenterCorners[(i + 1) % 4]) || Crosses(y, z, CenterCorners[i], CenterCorners[(i + 1) % 4]) || Crosses(z, x, CenterCorners[i], CenterCorners[(i + 1) % 4])) return true;
            return false;
        }
        static float Cross(Vector2 a, Vector2 b, Vector2 c) => (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x);
        static bool InTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            if (Mathf.Abs(Cross(a, b, c)) < .000000000001f) return false;
            float x = Cross(a, b, p), y = Cross(b, c, p), z = Cross(c, a, p);
            return (x >= 0 && y >= 0 && z >= 0) || (x <= 0 && y <= 0 && z <= 0);
        }
        static bool Crosses(Vector2 a, Vector2 b, Vector2 c, Vector2 d) => Cross(a, b, c) * Cross(a, b, d) < 0 && Cross(c, d, a) * Cross(c, d, b) < 0;
        static void RecordInput(string project, string asset, Report report)
        { string path = Path.GetFullPath(Path.Combine(project, asset)); if (File.Exists(path)) Hashes[path] = Hash(path); }
        static string Hash(string path)
        { using (var stream = File.OpenRead(path)) using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant(); }
        static string F(float value) => value.ToString("R", Invariant);
        static string V(Vector3 value) => F(value.x) + " " + F(value.y) + " " + F(value.z);
    }
}
