using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;
using static HappyToy.V2.CloudTests.RuntimeAccess;
using Object = UnityEngine.Object;

namespace HappyToy.V2.CloudTests
{
    // Test-only art candidate: the source ribbons simulate the same world
    // trajectories but are not rendered. Small softly tapered camera-plane
    // pieces visualize their paths without a long ribbon folding onto itself.
    // This is not a production performance measurement.
    public sealed class TempleAltarVaporCandidateProbe : MonoBehaviour
    {
        const int Strands = 3, Puffs = 14, VerticesPerPuff = 6, IndicesPerPuff = 12;
        const float Lifetime = 3.6f;
        readonly Action<Vector3, Vector3, float, float, float>[] advance = new Action<Vector3, Vector3, float, float, float>[Strands];
        readonly Component[] trails = new Component[Strands];
        readonly Vector3[][] samples = new Vector3[Strands][];
        readonly float[][] ages = new float[Strands][];
        readonly Vector3[] vertices = new Vector3[Strands * Puffs * VerticesPerPuff];
        readonly Vector2[] uv = new Vector2[Strands * Puffs * VerticesPerPuff];
        readonly Color[] colors = new Color[Strands * Puffs * VerticesPerPuff];
        readonly int[] indices = new int[Strands * Puffs * IndicesPerPuff];
        static readonly float[] Phase = { .12f, 2.25f, -2.10f };
        static readonly Vector3[] Offsets = { Vector3.zero, new Vector3(-.10f, -.01f, .035f), new Vector3(.11f, .015f, -.035f) };
        Component world, session, shell;
        Camera eye;
        Mesh renderMesh;
        Material renderMaterial;
        float clock;
        bool prepared;
        public float SimulationTime => clock;
        public MeshRenderer VisibleRenderer { get; private set; }

        public void Prepare(Camera camera, Component prologue, Component game)
        {
            if (prepared || !camera || !prologue || !game) throw new ArgumentException("A fresh prologue, session and eye camera are required");
            prepared = true; eye = camera; world = prologue; session = game; shell = Get<Component>(game, "Shell");
            var trailType = RequireType("IncenseSmokeTrail");
            var advanceMethod = trailType.GetMethod("Advance", BindingFlags.Public | BindingFlags.Instance);
            var positionsField = trailType.GetField("positions", BindingFlags.NonPublic | BindingFlags.Instance);
            var agesField = trailType.GetField("ages", BindingFlags.NonPublic | BindingFlags.Instance);
            if (advanceMethod == null || positionsField == null || agesField == null)
                throw new MissingMemberException("The existing incense trajectory contract changed");
            for (int i = 0; i < Strands; i++)
            {
                var node = new GameObject("Candidate simulated wisp " + i);
                node.layer = 8; node.transform.SetParent(transform, false);
                var trail = node.AddComponent(trailType);
                Call(trail, "Prepare", eye);
                var sourceRenderer = node.GetComponent<MeshRenderer>();
                sourceRenderer.forceRenderingOff = true;
                trails[i] = trail;
                samples[i] = (Vector3[])positionsField.GetValue(trail);
                ages[i] = (float[])agesField.GetValue(trail);
                advance[i] = (Action<Vector3, Vector3, float, float, float>)Delegate.CreateDelegate(
                    typeof(Action<Vector3, Vector3, float, float, float>), trail, advanceMethod);
            }
            var shader = Resources.Load<Shader>("IncenseSmoke");
            if (!shader) throw new InvalidOperationException("Existing incense shader is required");
            renderMesh = new Mesh { name = "Test-only soft altar vapor segments" };
            renderMesh.MarkDynamic();
            renderMaterial = new Material(shader) { name = "Test-only altar vapor material" };
            var surface = new GameObject("Candidate soft altar vapor");
            surface.layer = 8; surface.transform.SetParent(transform, false);
            surface.AddComponent<MeshFilter>().sharedMesh = renderMesh;
            VisibleRenderer = surface.AddComponent<MeshRenderer>();
            VisibleRenderer.sharedMaterial = renderMaterial;
            VisibleRenderer.shadowCastingMode = ShadowCastingMode.Off;
            VisibleRenderer.receiveShadows = false;
            VisibleRenderer.lightProbeUsage = LightProbeUsage.Off;
            VisibleRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            for (int p = 0; p < Strands * Puffs; p++)
            {
                int v = p * VerticesPerPuff, t = p * IndicesPerPuff;
                indices[t] = v; indices[t + 1] = v + 2; indices[t + 2] = v + 1;
                indices[t + 3] = v + 1; indices[t + 4] = v + 2; indices[t + 5] = v + 3;
                indices[t + 6] = v + 2; indices[t + 7] = v + 4; indices[t + 8] = v + 3;
                indices[t + 9] = v + 3; indices[t + 10] = v + 4; indices[t + 11] = v + 5;
            }
            renderMesh.vertices = vertices; renderMesh.uv = uv; renderMesh.colors = colors;
            renderMesh.triangles = indices;
        }

        void Update()
        {
            if (!prepared || !world || !session || !eye || !Get<bool>(world, "Ready") ||
                Get<bool>(world, "TransitionRequested") || !Get<bool>(session, "InputAllowed") ||
                (shell && Get<bool>(shell, "ReducedMotion"))) return;
            Vector3 source = transform.TransformPoint(new Vector3(0, 1.07f, 7.2f));
            if ((eye.transform.position - source).sqrMagnitude > 400) return;
            float delta = Time.deltaTime;
            if (delta <= 0) return;
            clock += delta;
            for (int i = 0; i < Strands; i++)
            {
                float direction = Phase[i] + .85f * Mathf.Sin(clock * .58f + i * 1.17f) +
                    .27f * Mathf.Sin(clock * 1.21f + i * 2.08f);
                Vector3 heading = transform.TransformDirection(new Vector3(Mathf.Sin(direction), 0, Mathf.Cos(direction)));
                advance[i](source + transform.TransformVector(Offsets[i]), heading, delta, 1, .45f + .08f * i);
            }
            UpdateSoftSegments();
        }

        void UpdateSoftSegments()
        {
            for (int strand = 0; strand < Strands; strand++)
            {
                int count = Get<int>(trails[strand], "SampleCount");
                for (int p = 0; p < Puffs; p++)
                {
                    int first = (strand * Puffs + p) * VerticesPerPuff;
                    if (count < 3)
                    {
                        for (int j = 0; j < VerticesPerPuff; j++) colors[first + j] = Color.clear;
                        continue;
                    }
                    float station = (p + .5f) / Puffs * (count - 1);
                    int index = Mathf.Min(count - 2, Mathf.FloorToInt(station));
                    float fraction = station - index;
                    Vector3 center = Vector3.Lerp(samples[strand][index], samples[strand][index + 1], fraction);
                    float age = Mathf.Lerp(ages[strand][index], ages[strand][index + 1], fraction);
                    Vector3 tangent = samples[strand][Mathf.Min(count - 1, index + 1)] - samples[strand][Mathf.Max(0, index - 1)];
                    Vector3 view = (eye.transform.position - center).normalized;
                    Vector3 along = tangent - view * Vector3.Dot(tangent, view);
                    if (along.sqrMagnitude < .000001f) along = eye.transform.up;
                    along.Normalize();
                    Vector3 across = Vector3.Cross(view, along).normalized;
                    float spacing = tangent.magnitude * (count - 1) / Puffs * .45f;
                    float halfLength = Mathf.Clamp(spacing, .035f, .16f);
                    float halfWidth = .035f + .07f * Mathf.SmoothStep(0, 1, age / Lifetime);
                    float opacity = .13f * Mathf.SmoothStep(0, 1, age / .12f) *
                        (1 - Mathf.SmoothStep(0, 1, (age - 2.4f) / 1.2f));
                    Vector3 left = across * halfWidth, longAxis = along * halfLength;
                    for (int j = 0; j < VerticesPerPuff; j++)
                    {
                        int row = j / 2;
                        Vector3 position = center + longAxis * (row - 1) + left * (j % 2 == 0 ? -1 : 1);
                        vertices[first + j] = transform.InverseTransformPoint(position);
                        uv[first + j] = new Vector2(j % 2, age / Lifetime + (j / 2 - 1) * .035f);
                        colors[first + j] = new Color(.32f, .055f, .045f, j == 2 || j == 3 ? opacity : 0);
                    }
                }
            }
            renderMesh.vertices = vertices; renderMesh.uv = uv; renderMesh.colors = colors;
            renderMesh.RecalculateBounds();
            renderMaterial.SetFloat("_FlowTime", clock);
        }

        void OnDestroy()
        {
            if (renderMesh) { if (Application.isPlaying) Object.Destroy(renderMesh); else Object.DestroyImmediate(renderMesh); }
            if (renderMaterial) { if (Application.isPlaying) Object.Destroy(renderMaterial); else Object.DestroyImmediate(renderMaterial); }
        }
    }
}
