using UnityEngine;
using UnityEngine.Rendering;

namespace HappyToy.V2
{
    // A single advected ribbon, rather than overlapping circular particle cards.
    // World-space history lets smoke drift after the hand has moved away.
    public sealed class IncenseSmokeTrail : MonoBehaviour
    {
        const int Capacity = 128, Sections = 160;
        const float Interval = 1f / 32, Lifetime = 3.6f;
        readonly Vector3[] positions = new Vector3[Capacity];
        readonly Vector3[] velocities = new Vector3[Capacity];
        readonly float[] ages = new float[Capacity];
        readonly Vector3[] vertices = new Vector3[(Sections + 1) * 2];
        readonly Vector2[] uv = new Vector2[(Sections + 1) * 2];
        readonly Color[] colors = new Color[(Sections + 1) * 2];
        readonly int[] indices = new int[Sections * 6];
        Camera eye;
        Mesh mesh;
        Material material;
        Vector3 previousSource, flow;
        float emission;
        int count;
        public int SampleCount => count;
        public float SimulationTime { get; private set; }

        public void Prepare(Camera camera)
        {
            eye = camera;
            mesh = new Mesh { name = "Continuous incense plume" };
            mesh.MarkDynamic();
            gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            material = new Material(Resources.Load<Shader>("IncenseSmoke"));
            var renderer = gameObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            for (int i = 0; i < Sections; i++)
            {
                int vertex = i * 2, index = i * 6;
                indices[index] = vertex; indices[index + 1] = vertex + 2; indices[index + 2] = vertex + 1;
                indices[index + 3] = vertex + 1; indices[index + 4] = vertex + 2; indices[index + 5] = vertex + 3;
            }
        }
        public void Advance(Vector3 source, Vector3 direction, float delta, float remaining)
        {
            if (!mesh || !eye || delta <= 0) return;
            direction.y = 0;
            if (direction.sqrMagnitude < .001f) direction = Vector3.forward;
            direction.Normalize();
            if (count == 0 || (source - previousSource).sqrMagnitude > 4)
            {
                count = 1; emission = 0; ages[0] = 0;
                positions[0] = previousSource = source; flow = direction;
            }
            int steps = Mathf.Max(1, Mathf.CeilToInt(delta / (1f / 60)));
            float step = delta / steps;
            for (int tick = 0; tick < steps; tick++)
            {
                SimulationTime += step;
                flow = Vector3.Slerp(flow, direction, 1 - Mathf.Exp(-step * 2.8f)).normalized;
                Vector3 side = Vector3.Cross(Vector3.up, flow);
                for (int i = 1; i < count; i++)
                {
                    ages[i] += step;
                    float birth = SimulationTime - ages[i];
                    float curl = Mathf.Sin(birth * 1.9f + ages[i] * 1.7f) * .027f +
                        Mathf.Sin(ages[i] * 5.1f - birth * 1.1f) * .012f;
                    Vector3 desired = flow * (.17f + .61f * Mathf.SmoothStep(0, 1, ages[i] / .5f)) +
                        Vector3.up * (.19f - .05f * ages[i] / Lifetime) + side * curl;
                    velocities[i] = Vector3.Lerp(velocities[i], desired, 1 - Mathf.Exp(-step * 2.8f));
                    positions[i] += velocities[i] * step;
                }
                while (count > 1 && ages[count - 1] >= Lifetime) count--;
                var emitter = Vector3.Lerp(previousSource, source, (tick + 1f) / steps);
                emission += step;
                while (emission >= Interval)
                {
                    emission -= Interval;
                    count = Mathf.Min(Capacity, count + 1);
                    for (int i = count - 1; i > 1; i--)
                    { positions[i] = positions[i - 1]; velocities[i] = velocities[i - 1]; ages[i] = ages[i - 1]; }
                    positions[1] = emitter; velocities[1] = flow * .12f + Vector3.up * .18f; ages[1] = 0;
                }
                positions[0] = emitter;
            }
            previousSource = source;
            BuildRibbon(Mathf.Clamp01(remaining));
            material.SetFloat("_FlowTime", SimulationTime);
        }
        Vector3 Curve(float station)
        {
            int i = Mathf.Clamp(Mathf.FloorToInt(station), 0, count - 2);
            float t = Mathf.Clamp01(station - i);
            Vector3 a = positions[Mathf.Max(0, i - 1)], b = positions[i],
                c = positions[i + 1], d = positions[Mathf.Min(count - 1, i + 2)];
            // Differences avoid cancellation far from the world's origin.
            a -= b; c -= b; d -= b;
            return b + .5f * ((c - a) * t + (2 * a + 4 * c - d) * t * t +
                (-a - 3 * c + d) * t * t * t);
        }
        void BuildRibbon(float fade)
        {
            if (count < 2) { mesh.Clear(); return; }
            Vector3 previousAcross = eye.transform.right;
            for (int i = 0; i <= Sections; i++)
            {
                float station = i / (float)Sections * (count - 1);
                int node = Mathf.Min(count - 2, Mathf.FloorToInt(station));
                float age = Mathf.Lerp(ages[node], ages[node + 1], station - node);
                Vector3 center = Curve(station);
                Vector3 tangent = (Curve(Mathf.Min(count - 1, station + .12f)) - Curve(Mathf.Max(0, station - .12f))).normalized;
                Vector3 across = Vector3.Cross(tangent, eye.transform.forward);
                if (across.sqrMagnitude < .00000001f) across = eye.transform.right;
                across.Normalize();
                if (Vector3.Dot(across, previousAcross) < 0) across = -across;
                previousAcross = across;
                // Looking along a translucent strip must not stack its narrow
                // segments into an opaque fan at the far end of the plume.
                float alignment = Vector3.Cross(tangent, (eye.transform.position - center).normalized).magnitude;
                float viewFade = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.025f, .25f, alignment));
                float fraction = age / Lifetime;
                float width = .006f + .025f * Mathf.SmoothStep(0, 1, age / .8f) + .028f * fraction * fraction;
                float opacity = Mathf.SmoothStep(0, 1, age / .08f) *
                    (1 - Mathf.SmoothStep(0, 1, (age - 2.4f) / 1.2f)) * .21f * fade * viewFade;
                int vertex = i * 2;
                vertices[vertex] = transform.InverseTransformPoint(center - across * width);
                vertices[vertex + 1] = transform.InverseTransformPoint(center + across * width);
                uv[vertex] = new Vector2(0, fraction); uv[vertex + 1] = new Vector2(1, fraction);
                colors[vertex] = colors[vertex + 1] = new Color(.61f, .65f, .63f, opacity);
            }
            mesh.vertices = vertices; mesh.uv = uv; mesh.colors = colors; mesh.triangles = indices;
            mesh.RecalculateBounds();
        }
        void OnDestroy()
        {
            if (mesh) { if (Application.isPlaying) Destroy(mesh); else DestroyImmediate(mesh); }
            if (material) { if (Application.isPlaying) Destroy(material); else DestroyImmediate(material); }
        }
    }
}
