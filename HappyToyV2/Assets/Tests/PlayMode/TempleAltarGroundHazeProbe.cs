using System;
using UnityEngine;
using UnityEngine.Rendering;
using static HappyToy.V2.CloudTests.RuntimeAccess;

namespace HappyToy.V2.CloudTests
{
    // Diagnostic-only additive low veil; production incense remains untouched.
    public sealed class TempleAltarGroundHazeProbe : MonoBehaviour
    {
        const int Trails = 3, Puffs = 12, V = 6, I = 12;
        readonly Vector3[] vertices = new Vector3[Trails * Puffs * V];
        readonly Vector2[] uv = new Vector2[Trails * Puffs * V];
        readonly Color[] colors = new Color[Trails * Puffs * V];
        readonly int[] indices = new int[Trails * Puffs * I];
        Camera eye;
        Component world, session, shell;
        Mesh mesh;
        Material material;
        bool variant2;
        MeshRenderer surface;
        public float SimulationTime { get; private set; }
        public MeshRenderer Surface => surface;

        public void Prepare(Camera view, Component owner, Component game, Component ui, bool secondVariant = false)
        {
            if (!view || !owner || !game || !ui || mesh) throw new ArgumentException("Fresh world, session and camera required");
            var shader = Resources.Load<Shader>("IncenseSmoke");
            if (!shader) throw new InvalidOperationException("IncenseSmoke shader is required");
            eye = view; world = owner; session = game; shell = ui; variant2 = secondVariant;
            mesh = new Mesh { name = "Diagnostic altar ground haze" }; mesh.MarkDynamic();
            material = new Material(shader) { name = "Diagnostic altar ground haze" };
            gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            surface = gameObject.AddComponent<MeshRenderer>(); surface.sharedMaterial = material;
            surface.shadowCastingMode = ShadowCastingMode.Off; surface.receiveShadows = false;
            surface.lightProbeUsage = LightProbeUsage.Off; surface.reflectionProbeUsage = ReflectionProbeUsage.Off;
            for (int p = 0; p < Trails * Puffs; p++)
            {
                int v = p * V, t = p * I;
                indices[t] = v; indices[t + 1] = v + 2; indices[t + 2] = v + 1;
                indices[t + 3] = v + 1; indices[t + 4] = v + 2; indices[t + 5] = v + 3;
                indices[t + 6] = v + 2; indices[t + 7] = v + 4; indices[t + 8] = v + 3;
                indices[t + 9] = v + 3; indices[t + 10] = v + 4; indices[t + 11] = v + 5;
            }
            mesh.vertices = vertices; mesh.uv = uv; mesh.colors = colors; mesh.triangles = indices;
            Rebuild();
        }

        void Update()
        {
            if (!mesh || !eye || !world || !session || !shell || !Get<bool>(world, "Ready") || Get<bool>(world, "TransitionRequested")) return;
            bool near = (eye.transform.position - transform.TransformPoint(new Vector3(0, .25f, 7.2f))).sqrMagnitude <= 400f;
            surface.enabled = near;
            if (!near) return;
            if (Get<bool>(session, "InputAllowed") && !Get<bool>(shell, "ReducedMotion"))
                SimulationTime += Mathf.Max(0, Time.deltaTime);
            Rebuild();
        }

        Vector3 Center(int trail, float t)
        {
            float phase = trail * 2.16f + SimulationTime * .13f;
            float a = phase + t * 2.35f;
            float x = ((variant2 ? 1.50f : 1.05f) + .18f * Mathf.Sin(t * 6 + phase)) * Mathf.Cos(a);
            float z = (variant2 ? 6.9f : 7.2f) + ((variant2 ? .95f : .60f) + .10f * Mathf.Cos(t * 5 - phase)) * Mathf.Sin(a);
            float y = (variant2 ? .18f : .12f) + .17f * Mathf.Sin(Mathf.PI * t) + .045f * Mathf.Sin(phase * 2 + t * 7);
            return transform.TransformPoint(new Vector3(x, Mathf.Max(.06f, y), z));
        }

        void Rebuild()
        {
            Vector3 viewUp = eye.transform.up, viewPosition = eye.transform.position;
            var inverse = transform.worldToLocalMatrix;
            for (int s = 0; s < Trails; s++)
            for (int p = 0; p < Puffs; p++)
            {
                float t = (p + .5f) / Puffs;
                Vector3 center = Center(s, t);
                Vector3 tangent = Center(s, Mathf.Min(1, t + .03f)) - Center(s, Mathf.Max(0, t - .03f));
                Vector3 toward = (viewPosition - center).normalized;
                Vector3 along = tangent - toward * Vector3.Dot(tangent, toward);
                if (along.sqrMagnitude < .000001f) along = viewUp;
                along.Normalize();
                Vector3 across = Vector3.Cross(toward, along).normalized;
                Vector3 axis = along * .17f, lateral = across * ((variant2 ? .16f : .11f) + .07f * Mathf.Sin(Mathf.PI * t));
                float opacity = (variant2 ? .20f : .075f) * Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0, .18f, t)) * (1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.72f, 1, t)));
                int first = (s * Puffs + p) * V;
                for (int j = 0; j < V; j++)
                {
                    int row = j / 2;
                    vertices[first + j] = inverse.MultiplyPoint3x4(center + axis * (row - 1) + lateral * (j % 2 == 0 ? -1 : 1));
                    uv[first + j] = new Vector2(j % 2, t + (row - 1) * .04f);
                    colors[first + j] = new Color(variant2 ? .31f : .19f, variant2 ? .045f : .025f, variant2 ? .035f : .023f, row == 1 ? opacity : 0);
                }
            }
            mesh.vertices = vertices; mesh.uv = uv; mesh.colors = colors; mesh.RecalculateBounds();
            material.SetFloat("_FlowTime", SimulationTime);
        }

        void OnDestroy()
        {
            if (mesh) Destroy(mesh);
            if (material) Destroy(material);
        }
    }
}


