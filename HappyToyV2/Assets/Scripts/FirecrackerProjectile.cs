using UnityEngine;

namespace HappyToy.V2
{
    public sealed class FirecrackerProjectile : MonoBehaviour
    {
        public bool Exploded { get; private set; }
        public int Attracted { get; private set; }
        public int PopsPlayed { get; private set; }
        Vector3 velocity;
        float life, pulse;
        bool grounded;
        Light glow;
        AudioSource source;
        AudioClip crack;
        Material material;
        GameObject body;

        public void Launch(Vector3 initialVelocity) { velocity = initialVelocity; }
        void Awake()
        {
            body = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            body.name = "Red paper firecracker";
            body.transform.SetParent(transform, false);
            body.transform.localScale = new Vector3(.08f, .1f, .08f);
            Destroy(body.GetComponent<Collider>());
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.SetColor("_BaseColor", new Color(.85f, .07f, .025f));
            body.GetComponent<Renderer>().sharedMaterial = material;
            glow = gameObject.AddComponent<Light>();
            glow.color = new Color(1, .36f, .05f); glow.range = 3.2f; glow.intensity = .7f;
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false; source.spatialBlend = 1;
            source.minDistance = 2; source.maxDistance = 28; source.volume = .35f;
            source.dopplerLevel = 0; source.ignoreListenerPause = false; source.ignoreListenerVolume = false;
            const int rate = 24000;
            var samples = new float[rate / 5];
            var random = new System.Random(381);
            for (int i = 0; i < samples.Length; i++)
            {
                float t = i / (float)rate;
                samples[i] = (float)(random.NextDouble() * 2 - 1) * Mathf.Exp(-t * 40);
            }
            crack = AudioClip.Create("Firecracker pop", samples.Length, 1, rate, false);
            crack.SetData(samples, 0);
        }
        void Update()
        {
            var session = GameSession.Current;
            if (!session || session.Finished || session.StoryStep >= 4) { Destroy(gameObject); return; }
            bool reducedMotion = session.Shell && session.Shell.ReducedMotion;
            // Apply comfort changes even if Settings has paused gameplay mid-flash.
            if (reducedMotion) glow.intensity = Exploded ? .45f : .7f;
            if (!session.InputAllowed) return;
            life += Time.deltaTime;
            if (!grounded)
            {
                velocity += Physics.gravity * Time.deltaTime;
                var step = velocity * Time.deltaTime;
                if (step.sqrMagnitude > 0 && Physics.SphereCast(transform.position, .08f, step.normalized,
                    out var hit, step.magnitude, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                {
                    transform.position += step.normalized * Mathf.Max(0, hit.distance - .005f);
                    velocity = Vector3.ProjectOnPlane(velocity, hit.normal) * .45f;
                    grounded = hit.normal.y > .6f;
                }
                else transform.position += step;
                if (!Exploded && !reducedMotion) body.transform.Rotate(420 * Time.deltaTime, 0, 0);
            }
            if (!Exploded)
            {
                if (life < 1.2f) return;
                Exploded = true;
                body.SetActive(false);
                pulse = 0;
            }
            if (life >= 11.2f) { Destroy(gameObject); return; }
            pulse -= Time.deltaTime;
            if (!reducedMotion) glow.intensity = Mathf.MoveTowards(glow.intensity, 0, Time.deltaTime * 18);
            if (pulse > 0) return;
            pulse = .5f;
            glow.intensity = reducedMotion ? .45f : 4;
            PlayPop(session);
            foreach (var brain in FindObjectsByType<StalkerBrain>(FindObjectsSortMode.None))
                if (brain.HearNoise(transform.position, 11.2f - life)) Attracted++;
            foreach (var mask in FindObjectsByType<LanternMaskEncounter>(FindObjectsSortMode.None))
                if (mask.HearNoise(transform.position, 11.2f - life)) Attracted++;
        }
        void PlayPop(GameSession session)
        {
            // The first explosion uses this same path: one pop, not a second overlapping one-shot.
            source.PlayOneShot(crack);
            PopsPlayed++;
            if ((PopsPlayed - 1) % 4 == 0 && session.player && session.player.eyes &&
                Vector3.Distance(session.player.eyes.transform.position, transform.position) <= 18)
                session.Shell.ShowCaption("[폭죽 터지는 소리]", 1.2f);
        }
        void OnDisable() { if (source) source.Stop(); if (glow) glow.intensity = 0; }
        void OnDestroy() { if (material) Destroy(material); if (crack) Destroy(crack); }
    }
}
