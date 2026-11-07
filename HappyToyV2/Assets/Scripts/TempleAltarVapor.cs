using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace HappyToy.V2
{
    // Visual-only prologue altar vapor. Three short advected paths are drawn
    // as softly tapered camera-facing segments in one mesh, avoiding the
    // axial ribbon fan while keeping normal incense completely independent.
    [DisallowMultipleComponent]
    public sealed class TempleAltarVapor : MonoBehaviour
    {
        const int StrandCount = 3, Capacity = 128, Puffs = 14, VerticesPerPuff = 6, IndicesPerPuff = 12;
        const float Interval = 1f / 32, Lifetime = 3.6f;
        static readonly float[] Phase = { .12f, 2.25f, -2.10f };
        static readonly Vector3[] Offsets = { Vector3.zero, new Vector3(-.10f, -.01f, .035f), new Vector3(.11f, .015f, -.035f) };
        sealed class Strand
        {
            public readonly Vector3[] positions = new Vector3[Capacity];
            public readonly Vector3[] velocities = new Vector3[Capacity];
            public readonly float[] ages = new float[Capacity];
            public Vector3 flow;
            public float emission;
            public int count;
        }
        readonly Strand[] strands = { new Strand(), new Strand(), new Strand() };
        readonly Vector3[] vertices = new Vector3[StrandCount * Puffs * VerticesPerPuff];
        readonly Vector2[] uv = new Vector2[StrandCount * Puffs * VerticesPerPuff];
        readonly Color[] colors = new Color[StrandCount * Puffs * VerticesPerPuff];
        readonly int[] indices = new int[StrandCount * Puffs * IndicesPerPuff];
        Camera eye;
        TemplePrologueWorld world;
        Mesh mesh;
        Material material;
        MeshRenderer surface;
        bool prepared;
        public float SimulationTime { get; private set; }
        public int SampleCount => Mathf.Min(strands[0].count, Mathf.Min(strands[1].count, strands[2].count));

        public void Prepare(Camera camera, TemplePrologueWorld owner)
        {
            if (prepared || !camera || !owner) throw new ArgumentException("A fresh prologue and camera are required");
            var shader = Resources.Load<Shader>("IncenseSmoke");
            if (!shader) throw new InvalidOperationException("Existing incense shader is required");
            prepared = true; eye = camera; world = owner;
            mesh = new Mesh { name = "Owned altar spectral vapor" }; mesh.MarkDynamic();
            material = new Material(shader) { name = "Owned altar spectral vapor" };
            gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            surface = gameObject.AddComponent<MeshRenderer>();
            surface.sharedMaterial = material; surface.shadowCastingMode = ShadowCastingMode.Off;
            surface.receiveShadows = false; surface.lightProbeUsage = LightProbeUsage.Off;
            surface.reflectionProbeUsage = ReflectionProbeUsage.Off;
            for (int p = 0; p < StrandCount * Puffs; p++)
            {
                int v = p * VerticesPerPuff, t = p * IndicesPerPuff;
                indices[t] = v; indices[t + 1] = v + 2; indices[t + 2] = v + 1;
                indices[t + 3] = v + 1; indices[t + 4] = v + 2; indices[t + 5] = v + 3;
                indices[t + 6] = v + 2; indices[t + 7] = v + 4; indices[t + 8] = v + 3;
                indices[t + 9] = v + 3; indices[t + 10] = v + 4; indices[t + 11] = v + 5;
            }
            mesh.vertices = vertices; mesh.uv = uv; mesh.colors = colors; mesh.triangles = indices;
        }

        void Update()
        {
            var session = GameSession.Current;
            if (!prepared || !world || !world.Ready || world.TransitionRequested || !session || !eye) return;
            Vector3 source = transform.TransformPoint(new Vector3(0, 1.07f, 7.2f));
            // Keep distance culling current even while simulation is held by
            // pause or reduced motion; entering range restores the same mesh.
            bool inRange = (eye.transform.position - source).sqrMagnitude <= 400f;
            if (surface.enabled != inRange) surface.enabled = inRange;
            if (!inRange || !session.InputAllowed || (session.Shell && session.Shell.ReducedMotion)) return;
            float delta = Time.deltaTime;
            if (delta <= 0) return;
            float endTime = SimulationTime + delta;
            int steps = Mathf.Max(1, Mathf.CeilToInt(delta / (1f / 60)));
            float step = delta / steps;
            // The relaxation depends only on this substep, not on a sample.
            float relaxation = 1 - Mathf.Exp(-step * 2.8f);
            for (int tick = 0; tick < steps; tick++)
            {
                SimulationTime += step;
                for (int i = 0; i < StrandCount; i++)
                {
                    // Match the reviewed candidate: direction is selected at
                    // the end of this frame and each trail eases toward it.
                    float direction = Phase[i] + .85f * Mathf.Sin(endTime * .58f + i * 1.17f) +
                        .27f * Mathf.Sin(endTime * 1.21f + i * 2.08f);
                    Vector3 heading = transform.TransformDirection(new Vector3(Mathf.Sin(direction), 0, Mathf.Cos(direction)));
                    Advance(strands[i], source + transform.TransformVector(Offsets[i]), heading, step, .45f + .08f * i, relaxation);
                }
            }
            UpdateSegments();
        }

        void Advance(Strand strand, Vector3 source, Vector3 heading, float step, float horizontalFlow, float relaxation)
        {
            heading.y = 0;
            if (heading.sqrMagnitude < .001f) heading = Vector3.forward;
            heading.Normalize();
            if (strand.count == 0) { strand.count = 1; strand.emission = 0; strand.ages[0] = 0; strand.positions[0] = source; strand.flow = heading; }
            strand.flow = Vector3.Slerp(strand.flow, heading, relaxation).normalized;
            Vector3 side = Vector3.Cross(Vector3.up, strand.flow);
            for (int i = 1; i < strand.count; i++)
            {
                strand.ages[i] += step;
                float birth = SimulationTime - strand.ages[i];
                float curl = Mathf.Sin(birth * 1.9f + strand.ages[i] * 1.7f) * .027f +
                    Mathf.Sin(strand.ages[i] * 5.1f - birth * 1.1f) * .012f;
                Vector3 desired = strand.flow * (horizontalFlow * (.17f + .61f * Mathf.SmoothStep(0, 1, strand.ages[i] / .5f))) +
                    Vector3.up * (.19f - .05f * strand.ages[i] / Lifetime) + side * curl;
                strand.velocities[i] = Vector3.Lerp(strand.velocities[i], desired, relaxation);
                strand.positions[i] += strand.velocities[i] * step;
            }
            while (strand.count > 1 && strand.ages[strand.count - 1] >= Lifetime) strand.count--;
            strand.emission += step;
            while (strand.emission >= Interval)
            {
                strand.emission -= Interval;
                strand.count = Mathf.Min(Capacity, strand.count + 1);
                for (int i = strand.count - 1; i > 1; i--)
                { strand.positions[i] = strand.positions[i - 1]; strand.velocities[i] = strand.velocities[i - 1]; strand.ages[i] = strand.ages[i - 1]; }
                strand.positions[1] = source; strand.velocities[1] = strand.flow * (.12f * horizontalFlow) + Vector3.up * .18f; strand.ages[1] = 0;
            }
            strand.positions[0] = source;
        }

        void UpdateSegments()
        {
            // One camera and transform snapshot for the complete mesh avoids
            // repeated engine lookups for every short segment and vertex.
            Transform viewTransform = eye.transform;
            Vector3 eyePosition = viewTransform.position, eyeUp = viewTransform.up;
            Matrix4x4 worldToLocal = transform.worldToLocalMatrix;
            for (int strand = 0; strand < StrandCount; strand++)
            {
                var path = strands[strand]; int count = path.count;
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
                    Vector3 center = Vector3.Lerp(path.positions[index], path.positions[index + 1], fraction);
                    float age = Mathf.Lerp(path.ages[index], path.ages[index + 1], fraction);
                    Vector3 tangent = path.positions[Mathf.Min(count - 1, index + 1)] - path.positions[Mathf.Max(0, index - 1)];
                    Vector3 view = (eyePosition - center).normalized;
                    Vector3 along = tangent - view * Vector3.Dot(tangent, view);
                    if (along.sqrMagnitude < .000001f) along = eyeUp;
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
                        vertices[first + j] = worldToLocal.MultiplyPoint3x4(position);
                        uv[first + j] = new Vector2(j % 2, age / Lifetime + (row - 1) * .035f);
                        colors[first + j] = new Color(.32f, .055f, .045f, j == 2 || j == 3 ? opacity : 0);
                    }
                }
            }
            mesh.vertices = vertices; mesh.uv = uv; mesh.colors = colors; mesh.RecalculateBounds();
            material.SetFloat("_FlowTime", SimulationTime);
        }

        void OnDestroy()
        {
            if (mesh) { if (Application.isPlaying) Destroy(mesh); else DestroyImmediate(mesh); }
            if (material) { if (Application.isPlaying) Destroy(material); else DestroyImmediate(material); }
        }
    }
}
