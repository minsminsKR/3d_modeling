using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace HappyToy.V2
{
    /// <summary>Owned, camera-local paper smoke/ash; gameplay burn rules stay elsewhere.</summary>
    public sealed class HeldPaperEffects : IDisposable
    {
        const int Capacity = 40;
        const float SmokeInterval = 1f / 13, AshInterval = 1f / 6;
        struct Particle
        {
            public Vector3 position, velocity;
            public float age, lifetime, size, spin;
            public bool ash;
        }
        readonly Particle[] particles = new Particle[Capacity];
        readonly Vector3[] vertices = new Vector3[Capacity * 4];
        readonly Vector2[] uvs = new Vector2[Capacity * 4], kinds = new Vector2[Capacity * 4];
        readonly Color[] colours = new Color[Capacity * 4];
        readonly int[] indices = new int[Capacity * 6];
        readonly Transform paper;
        readonly Bounds localBounds;
        readonly GameObject root;
        readonly Mesh mesh;
        readonly Material material;
        float smokeClock, ashClock;
        int count, sequence;
        bool disposed, wasBurning;
        Vector3 previousSource;
        public int ActiveCount => count;
        public float SimulationTime { get; private set; }

        public HeldPaperEffects(Transform paper, Bounds localBounds, int viewLayer)
        {
            if (!paper) throw new ArgumentNullException(nameof(paper));
            this.paper = paper; this.localBounds = localBounds;
            var shader = Resources.Load<Shader>("HeldPaperParticles");
            if (!shader) throw new InvalidOperationException("Missing held paper particle shader");
            root = new GameObject("Held paper smoke and ash") { layer = viewLayer };
            var camera = paper.GetComponentInParent<Camera>();
            root.transform.SetParent(camera ? camera.transform : paper.parent, false);
            mesh = new Mesh { name = "Held paper pooled particles" };
            mesh.MarkDynamic(); root.AddComponent<MeshFilter>().sharedMesh = mesh;
            material = new Material(shader) { name = "Owned held paper smoke and ash" };
            var renderer = root.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off; renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            for (int i = 0; i < Capacity; i++)
            {
                int v = i * 4, t = i * 6;
                uvs[v] = Vector2.zero; uvs[v + 1] = Vector2.right;
                uvs[v + 2] = Vector2.up; uvs[v + 3] = Vector2.one;
                indices[t] = v; indices[t + 1] = v + 2; indices[t + 2] = v + 1;
                indices[t + 3] = v + 1; indices[t + 4] = v + 2; indices[t + 5] = v + 3;
            }
        }

        public void Advance(float age, float delta, float fraction, Camera camera, bool burning)
        {
            if (disposed || !paper || !camera || !Finite(delta) || delta <= 0) return;
            // A pathological suspended frame only has old, expired particles.
            float dt = Mathf.Min(delta, 3);
            SimulationTime += dt;
            if (root.transform.parent != camera.transform)
            {
                // Only expected before the first sample. Keep already emitted
                // positions if a caller replaces its isolated hand camera.
                var oldFrame = root.transform.localToWorldMatrix;
                root.transform.SetParent(camera.transform, false);
                for (int i = 0; i < count; i++)
                {
                    particles[i].position = root.transform.InverseTransformPoint(oldFrame.MultiplyPoint3x4(particles[i].position));
                    particles[i].velocity = root.transform.InverseTransformVector(oldFrame.MultiplyVector(particles[i].velocity));
                }
            }
            for (int i = count - 1; i >= 0; i--)
            {
                var particle = particles[i]; Move(ref particle, dt);
                if (particle.age >= particle.lifetime) particles[i] = particles[--count];
                else particles[i] = particle;
            }
            age = Finite(age) ? age : SimulationTime;
            fraction = Finite(fraction) ? Mathf.Clamp01(fraction) : 1;
            Vector3 localSource = new Vector3(localBounds.center.x,
                Mathf.Lerp(localBounds.max.y, localBounds.min.y, fraction), localBounds.center.z);
            Vector3 source = paper.TransformPoint(localSource);
            source += (camera.transform.position - source).normalized * BendOffset(1 - fraction, age);
            source = root.transform.InverseTransformPoint(source);
            bool emitting = burning && fraction < .99999f;
            if (emitting)
            {
                if (!wasBurning) previousSource = source;
                Emit(ref smokeClock, SmokeInterval, false, dt, source);
                Emit(ref ashClock, AshInterval, true, dt, source);
            }
            else smokeClock = ashClock = 0;
            previousSource = source; wasBurning = emitting;
            Rebuild(camera);
        }

        void Emit(ref float clock, float interval, bool ash, float dt, Vector3 source)
        {
            float before = clock;
            int births = Mathf.FloorToInt((before + dt) / interval);
            clock = (before + dt) - births * interval;
            for (int i = 0; i < births; i++)
            {
                float offset = (i + 1) * interval - before;
                float alreadyAged = Mathf.Max(0, dt - offset);
                float lifetime = ash ? 1.05f : 1.6f;
                if (alreadyAged >= lifetime) continue;
                float u = Unit(sequence * 17 + (ash ? 73 : 7));
                float v = Unit(sequence * 31 + 41);
                float w = Unit(sequence * 47 + 19); sequence++;
                Vector3 across = root.transform.InverseTransformVector(paper.right).normalized;
                var particle = new Particle
                {
                    position = Vector3.Lerp(previousSource, source, Mathf.Clamp01(offset / dt)) +
                        across * ((u - .5f) * localBounds.size.x * .9f),
                    velocity = ash ? new Vector3((v - .5f) * .024f, -.025f - w * .025f, .002f) :
                        new Vector3((v - .5f) * .016f, .038f + w * .017f, .006f),
                    age = 0, lifetime = lifetime, size = ash ? .0012f + v * .001f : .007f + v * .004f,
                    spin = w * Mathf.PI * 2, ash = ash
                };
                Move(ref particle, alreadyAged);
                if (count < Capacity) particles[count++] = particle;
                else particles[(sequence - 1) % Capacity] = particle;
            }
        }

        static void Move(ref Particle particle, float dt)
        {
            Vector3 acceleration = particle.ash ? new Vector3(0, -.055f, 0) : Vector3.zero;
            particle.position += particle.velocity * dt + acceleration * (.5f * dt * dt);
            particle.velocity += acceleration * dt; particle.age += dt;
        }
        void Rebuild(Camera camera)
        {
            if (count == 0) { mesh.Clear(); return; }
            Vector3 right = root.transform.InverseTransformVector(camera.transform.right).normalized;
            Vector3 up = root.transform.InverseTransformVector(camera.transform.up).normalized;
            for (int i = 0; i < count; i++)
            {
                var p = particles[i]; float life = Mathf.Clamp01(p.age / p.lifetime);
                float size = p.size * (p.ash ? 1 : 1 + life * 2.2f);
                float rotation = p.spin + p.age * (p.ash ? 2.2f : .18f);
                Vector3 across = (right * Mathf.Cos(rotation) + up * Mathf.Sin(rotation)) * size;
                Vector3 vertical = (-right * Mathf.Sin(rotation) + up * Mathf.Cos(rotation)) * size * (p.ash ? .55f : 1.45f);
                float fade = Mathf.SmoothStep(0, 1, p.age / .08f) * (1 - Mathf.SmoothStep(.5f, 1, life));
                var colour = p.ash ? new Color(.17f, .135f, .1f, fade * .56f) : new Color(.47f, .46f, .43f, fade * .16f);
                int v = i * 4;
                vertices[v] = p.position - across - vertical; vertices[v + 1] = p.position + across - vertical;
                vertices[v + 2] = p.position - across + vertical; vertices[v + 3] = p.position + across + vertical;
                for (int corner = 0; corner < 4; corner++)
                { colours[v + corner] = colour; kinds[v + corner] = new Vector2(p.ash ? 1 : 0, p.age + p.spin); }
            }
            mesh.Clear(); mesh.SetVertices(vertices, 0, count * 4); mesh.SetUVs(0, uvs, 0, count * 4);
            mesh.SetUVs(1, kinds, 0, count * 4); mesh.SetColors(colours, 0, count * 4);
            mesh.SetTriangles(indices, 0, count * 6, 0, true);
        }

        // Matches PlayerItemSurface's world-metre displacement exactly at a given height.
        public static float BendOffset(float normalisedHeight, float age)
        {
            if (!Finite(normalisedHeight) || !Finite(age)) return 0;
            float height = Math.Max(0, Math.Min(1, normalisedHeight)), free = 1 - height;
            return (float)((Math.Sin(age * 2.1 + height * 4.7) * .0025 +
                Math.Sin(age * 1.13 + height * 2.3) * .0008) * free * free);
        }
        static float Unit(int value) => Mathf.Repeat(Mathf.Sin(value * 12.9898f + 78.233f) * 43758.5453f, 1);
        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        public void Dispose()
        {
            if (disposed) return;
            disposed = true; count = 0;
            Discard(root); Discard(mesh); Discard(material);
        }
        static void Discard(UnityEngine.Object item)
        {
            if (!item) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(item); else UnityEngine.Object.DestroyImmediate(item);
        }
    }
}
