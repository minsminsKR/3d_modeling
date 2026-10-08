using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace HappyToy.V2
{
    /// <summary>Depth-tested red eye cores on the authored animated head. No Light is created.</summary>
    [DisallowMultipleComponent]
    public sealed class MonsterRedEyes : MonoBehaviour
    {
        struct Profile
        {
            public Vector2 left, right;
            public Profile(float lx, float ly, float rx, float ry)
            { left = new Vector2(lx, ly); right = new Vector2(rx, ry); }
        }
        struct Surface { public Vector3 point, normal; public float distance; }
        readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();
        readonly GraphicsSurfaceLibrary.Pool surfaces = new GraphicsSurfaceLibrary.Pool();
        readonly List<Transform> anchors = new List<Transform>();
        readonly List<MeshRenderer> cores = new List<MeshRenderer>();
        MaterialPropertyBlock block;
        void Awake() => block = new MaterialPropertyBlock();
        static readonly int Emission = Shader.PropertyToID("_EmissionColor");
        static readonly Color CoreEmission = new Color(18f, .075f, .025f);
        static readonly Vector2 MaskLeftUpper = new Vector2(.268022388f, .958278418f);
        static readonly Vector2 MaskRightUpper = new Vector2(.720218182f, .527960896f);
        float phase;
        public bool Prepared { get; private set; }
        public Transform Head { get; private set; }
        public IReadOnlyList<Transform> EyeAnchors => anchors;
        public IReadOnlyList<MeshRenderer> Cores => cores;
        public string ProfileKey { get; private set; }

        public static MonsterRedEyes Attach(Transform model, string key)
        {
            if (!model) throw new ArgumentNullException(nameof(model));
            var prior = model.GetComponentInChildren<MonsterRedEyes>(true);
            if (prior) return prior;
            var profile = For(key);
            Transform head = null;
            foreach (var node in model.GetComponentsInChildren<Transform>(true))
                if (node.name == "mixamorig:Head") { head = node; break; }
            if (!head) throw new InvalidOperationException("Animated head is required for red eyes: " + key);
            var skins = model.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            if (skins.Length == 0) throw new InvalidOperationException("Skinned enemy face is absent: " + key);
            var left = new Surface { distance = float.PositiveInfinity };
            var right = left;
            var scratch = new Mesh();
            var bounds = new Bounds(); bool any = false;
            try
            {
                foreach (var skin in skins)
                {
                    // The imported FBX is intentionally not CPU-readable. BakeMesh is
                    // a readable posed snapshot and retains its authored UVs/topology.
                    skin.BakeMesh(scratch, true);
                    var vertices = scratch.vertices; var uv = scratch.uv; var normals = scratch.normals;
                    var triangles = scratch.triangles;
                    foreach (var vertex in vertices)
                    {
                        var point = skin.transform.TransformPoint(vertex);
                        if (!any) { bounds = new Bounds(point, Vector3.zero); any = true; }
                        else bounds.Encapsulate(point);
                    }
                    if (uv.Length != vertices.Length || normals.Length != vertices.Length)
                        throw new InvalidOperationException("Posed enemy has no complete surface UVs/normals: " + key);
                    FindSurface(skin.transform, vertices, uv, normals, triangles, profile.left, head.position, ref left);
                    FindSurface(skin.transform, vertices, uv, normals, triangles, profile.right, head.position, ref right);
                    scratch.Clear();
                }
            }
            finally { GraphicsSurfaceLibrary.DestroyOwned(scratch); }
            if (!any || float.IsInfinity(left.distance) || float.IsInfinity(right.distance))
                throw new InvalidOperationException("Authored eye UV anchor is missing on the posed enemy: " + key);
            return Create(head, key, left, right, bounds);
        }

        public static MonsterRedEyes AttachStatic(Transform faceRoot, string key)
        {
            if (!faceRoot) throw new ArgumentNullException(nameof(faceRoot));
            var prior = faceRoot.GetComponentInChildren<MonsterRedEyes>(true);
            if (prior) return prior;
            var profile = For(key);
            var left = new Surface { distance = float.PositiveInfinity }; var right = left;
            var leftUpper = left; var rightUpper = left;
            var bounds = new Bounds(); bool any = false;
            foreach (var filter in faceRoot.GetComponentsInChildren<MeshFilter>(true))
            {
                var renderer = filter.GetComponent<MeshRenderer>();
                if (!renderer || !renderer.enabled || !filter.sharedMesh) continue;
                var mesh = filter.sharedMesh;
                if (!mesh.isReadable) throw new InvalidOperationException("Static red-eye face needs its dedicated readable import: " + key);
                var vertices = mesh.vertices; var uv = mesh.uv; var normals = mesh.normals; var triangles = mesh.triangles;
                foreach (var vertex in vertices)
                {
                    var point = filter.transform.TransformPoint(vertex);
                    if (!any) { bounds = new Bounds(point, Vector3.zero); any = true; } else bounds.Encapsulate(point);
                }
                if (uv.Length != vertices.Length || normals.Length != vertices.Length)
                    throw new InvalidOperationException("Static face has no complete surface UVs/normals: " + key);
                FindSurface(filter.transform, vertices, uv, normals, triangles, profile.left, faceRoot.position, ref left);
                FindSurface(filter.transform, vertices, uv, normals, triangles, profile.right, faceRoot.position, ref right);
                if (key == "LanternMask")
                {
                    FindSurface(filter.transform, vertices, uv, normals, triangles, MaskLeftUpper, faceRoot.position, ref leftUpper);
                    FindSurface(filter.transform, vertices, uv, normals, triangles, MaskRightUpper, faceRoot.position, ref rightUpper);
                }
            }
            if (!any || float.IsInfinity(left.distance) || float.IsInfinity(right.distance))
                throw new InvalidOperationException("Authored static eye UV anchor is missing: " + key);
            if (key == "LanternMask")
            {
                if (float.IsInfinity(leftUpper.distance) || float.IsInfinity(rightUpper.distance))
                    throw new InvalidOperationException("Mask aperture boundary anchor is missing");
                var horizontal = right.point - left.point;
                var vertical = (leftUpper.point + rightUpper.point - left.point - right.point) * .5f;
                var forward = Vector3.Cross(horizontal, vertical).normalized;
                if (Vector3.Dot(forward, left.normal + right.normal) < 0) forward = -forward;
                left = Aperture(left, leftUpper); right = Aperture(right, rightUpper);
                left.normal = forward; right.normal = forward;
            }
            return Create(faceRoot, key, left, right, bounds);
        }
        static Surface Aperture(Surface lower, Surface upper) => new Surface
        { point = (lower.point + upper.point) * .5f, normal = (lower.normal + upper.normal).normalized, distance = lower.distance };
        static MonsterRedEyes Create(Transform head, string key, Surface left, Surface right, Bounds bounds)
        {
            float separation = Vector3.Distance(left.point, right.point);
            if (separation <= bounds.size.y * .015f || separation >= bounds.size.y * (key == "LanternMask" ? .85f : .35f))
                throw new InvalidOperationException("Red eye pair does not fit the authored head: " + key);
            var root = new GameObject("Animated paired red eye sockets");
            root.transform.SetParent(head, false);
            var art = root.AddComponent<MonsterRedEyes>();
            art.Head = head; art.ProfileKey = key;
            art.phase = key == "Baby" ? 2.1f : key == "Uncat" ? 1.3f : key == "Cyclopse" ? .4f : 3.6f;
            try
            {
                float radius = Mathf.Clamp(separation * .115f, bounds.size.y * .007f,
                    bounds.size.y * (key == "LanternMask" ? .065f : .013f));
                var coreMaterial = art.CoreMaterial();
                var socketMaterial = art.surfaces.Get("cloth-charred", new Color(.18f, .055f, .035f));
                var coreMesh = Dome(radius, radius * .38f, false, "Red eye convex core");
                var socketMesh = Dome(radius * 1.48f, radius * .10f, true, "Charred recessed eye rim");
                art.owned.Add(coreMesh); art.owned.Add(socketMesh);
                art.Eye(head, left, "Left red eye", radius, coreMesh, coreMaterial, socketMesh, socketMaterial);
                art.Eye(head, right, "Right red eye", radius, coreMesh, coreMaterial, socketMesh, socketMaterial);
                art.Prepared = true;
                return art;
            }
            catch { GraphicsSurfaceLibrary.DestroyOwned(root); throw; }
        }

        static Profile For(string key)
        {
            // UVs measured by ray hits on the editable posed source faces; see
            // SourceArt/ThreatEyes/eye-surface-anchors.json and inspect_head_sources.py.
            switch (key)
            {
                case "Cyclopse": return new Profile(.336293548f, .214996651f, .330183864f, .142739877f);
                case "Uncat": return new Profile(.884333134f, .905642629f, .956248999f, .120433450f);
                case "Hwacat_angry": return new Profile(.865963340f, .367665559f, .068320289f, .948560596f);
                case "Baby": return new Profile(.582066715f, .621558905f, .575446725f, .698374629f);
                case "LanternMask": return new Profile(.798695505f, .970696390f, .978926063f, .136576608f);
                case "Mannequin": return new Profile(.606296301f, .841941833f, .600609303f, .897853851f);
                default: throw new ArgumentException("Unsupported red eye anatomy: " + key);
            }
        }
        static void FindSurface(Transform meshTransform, Vector3[] vertices, Vector2[] uv, Vector3[] normals,
            int[] triangles, Vector2 at, Vector3 head, ref Surface result)
        {
            var normalMatrix = meshTransform.worldToLocalMatrix.transpose;
            for (int index = 0; index < triangles.Length; index += 3)
            {
                int a = triangles[index], b = triangles[index + 1], c = triangles[index + 2];
                Vector2 ab = uv[b] - uv[a], ac = uv[c] - uv[a], ap = at - uv[a];
                float determinant = ab.x * ac.y - ac.x * ab.y;
                if (Mathf.Abs(determinant) < .0000000001f) continue;
                float v = (ap.x * ac.y - ac.x * ap.y) / determinant;
                float w = (ab.x * ap.y - ap.x * ab.y) / determinant;
                float u = 1 - v - w;
                if (u < -.00001f || v < -.00001f || w < -.00001f) continue;
                var point = meshTransform.TransformPoint(vertices[a] * u + vertices[b] * v + vertices[c] * w);
                float distance = (point - head).sqrMagnitude;
                if (distance >= result.distance) continue;
                var normal = normalMatrix.MultiplyVector(normals[a] * u + normals[b] * v + normals[c] * w).normalized;
                if (normal.sqrMagnitude < .5f) continue;
                result = new Surface { point = point, normal = normal, distance = distance };
            }
        }
        Material CoreMaterial()
        {
            // Reuse the imported URP Lit emission variant retained in release
            // builds. Keep its keyword set; constant maps remove paper appearance.
            var template = Resources.Load<Material>("GraphicsPbr/paper-aged/material-emissive");
            if (!template) throw new InvalidOperationException("Retained red-eye emission template is absent");
            var material = new Material(template) { name = "Depth-tested blood-red eye emission", enableInstancing = true };
            material.SetTexture("_BaseMap", Texture2D.whiteTexture);
            // A hot inner pupil fades into deep red at the curved outer iris.
            // A constant white emission map made the dome read as a flat button.
            // This small original radial texture is generated from editable code.
            const int side=64;
            var heat=new Texture2D(side,side,TextureFormat.RGB24,false,true)
                {name="Original blood-red iris heat",wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear};
            var pixels=new Color[side*side];
            for(int y=0;y<side;y++)for(int x=0;x<side;x++)
            {
                var p=new Vector2((x+.5f)/side*2-1,(y+.5f)/side*2-1);
                float inner=Mathf.Clamp01(1-p.magnitude);
                float rays=.96f+.04f*Mathf.Sin(Mathf.Atan2(p.y,p.x)*19+inner*13);
                float red=.004f+.996f*Mathf.Pow(inner,2.3f)*rays;
                float hot=Mathf.Pow(inner,7);
                pixels[y*side+x]=new Color(red,hot,hot*.62f);
            }
            heat.SetPixels(pixels);heat.Apply(false,true);owned.Add(heat);
            material.SetTexture("_EmissionMap", heat);
            material.SetTexture("_OcclusionMap", Texture2D.whiteTexture);
            material.SetTexture("_MetallicGlossMap", Texture2D.blackTexture);
            material.SetTextureScale("_BaseMap", Vector2.one); material.SetTextureOffset("_BaseMap", Vector2.zero);
            material.SetColor("_BaseColor", new Color(.20f, .003f, .002f));
            material.SetColor("_EmissionColor", CoreEmission);
            material.SetFloat("_BumpScale", 0); material.SetFloat("_Metallic", 0);
            material.SetFloat("_Smoothness", .38f); material.SetFloat("_OcclusionStrength", 0);
            owned.Add(material); return material;
        }
        void Eye(Transform head, Surface surface, string name, float radius, Mesh mesh, Material material,
            Mesh socketMesh, Material socketMaterial)
        {
            var anchor = new GameObject(name).transform; anchor.SetParent(transform, false);
            anchor.position = surface.point + surface.normal * radius * .08f;
            var up = Vector3.ProjectOnPlane(head.up, surface.normal);
            if (up.sqrMagnitude < .001f) up = Vector3.ProjectOnPlane(head.right, surface.normal);
            anchor.rotation = Quaternion.LookRotation(surface.normal, up.normalized);
            // Preserve the measured world size through FBX bone/unit scales. All
            // later model enlargement and animation transforms are inherited.
            anchor.localScale = Vector3.one;
            var scale = anchor.lossyScale;
            anchor.localScale = new Vector3(1 / Mathf.Abs(scale.x), 1 / Mathf.Abs(scale.y), 1 / Mathf.Abs(scale.z));
            anchors.Add(anchor);
            Render(anchor, socketMesh, socketMaterial, "Charred eye socket", false);
            cores.Add(Render(anchor, mesh, material, "Visible red eye core", true));
        }
        static MeshRenderer Render(Transform parent, Mesh mesh, Material material, string name, bool core)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false); go.layer = parent.gameObject.layer;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = !core;
            renderer.lightProbeUsage = core ? LightProbeUsage.Off : LightProbeUsage.BlendProbes;
            renderer.reflectionProbeUsage = core ? ReflectionProbeUsage.Off : ReflectionProbeUsage.BlendProbes;
            return renderer;
        }
        static Mesh Dome(float radius, float depth, bool annulus, string name)
        {
            const int sides = 32, rings = 5;
            var vertices = new List<Vector3>(); var uv = new List<Vector2>(); var triangles = new List<int>();
            for (int ring = 0; ring <= rings; ring++)
            {
                float t = ring / (float)rings;
                float r = annulus ? Mathf.Lerp(radius * .66f, radius, t) : radius * t;
                float z = annulus ? depth * Mathf.Sin(t * Mathf.PI) : depth * Mathf.Sqrt(Mathf.Max(0, 1 - t * t)) + depth * .32f;
                for (int side = 0; side <= sides; side++)
                {
                    float angle = side * Mathf.PI * 2 / sides;
                    vertices.Add(new Vector3(Mathf.Cos(angle) * r, Mathf.Sin(angle) * r, z));
                    uv.Add(new Vector2(.5f + Mathf.Cos(angle) * r / radius * .5f, .5f + Mathf.Sin(angle) * r / radius * .5f));
                }
            }
            for (int ring = 0; ring < rings; ring++) for (int side = 0; side < sides; side++)
            {
                int a = ring * (sides + 1) + side, b = a + 1, c = a + sides + 1, d = c + 1;
                // Front-only positive-Z winding, opaque depth writes and ordinary
                // backface culling stop the glow being visible through the skull.
                if (ring > 0 || annulus) { triangles.Add(a); triangles.Add(c); triangles.Add(b); }
                triangles.Add(b); triangles.Add(c); triangles.Add(d);
            }
            var mesh = new Mesh { name = name };
            mesh.SetVertices(vertices); mesh.SetUVs(0, uv); mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals(); mesh.RecalculateTangents(); mesh.RecalculateBounds(); return mesh;
        }
        void LateUpdate()
        {
            if (!Prepared) return;
            float breath = .96f + .04f * Mathf.Sin(Time.time * 2.15f + phase);
            block.SetColor(Emission, CoreEmission * breath);
            foreach (var core in cores) if (core) core.SetPropertyBlock(block);
        }
        void OnDestroy()
        {
            foreach (var resource in owned) GraphicsSurfaceLibrary.DestroyOwned(resource);
            surfaces.Dispose();
        }
    }
}
