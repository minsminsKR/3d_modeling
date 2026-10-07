using UnityEngine;

namespace HappyToy.V2
{
    public sealed class FirecrackerProjectile : MonoBehaviour
    {
        public bool Exploded { get; private set; }
        public int Attracted { get; private set; }
        public int PopsPlayed { get; private set; }
        public bool InitialOverlap { get; private set; }
        public bool HasFirstContact { get; private set; }
        public Vector3 FirstContactPosition { get; private set; }
        public Vector3 FirstContactPoint { get; private set; }
        public Vector3 FirstContactNormal { get; private set; }
        public float FirstContactTime { get; private set; }
        Vector3 velocity;
        float life, pulse;
        bool grounded;
        double movementRemainder;
        int movementSteps;
        const int MaximumMovementStepsPerFrame = 32;
        Light glow;
        AudioSource source;
        AudioClip crack;
        AudioClip[] crackTakes;
        Material material;
        GameObject body;

        public void Launch(Vector3 initialVelocity)
        {
            velocity = initialVelocity;
            movementRemainder = 0;
            movementSteps = 0;
            HasFirstContact = false;
            FirstContactPosition = FirstContactPoint = FirstContactNormal = Vector3.zero;
            FirstContactTime = 0;
            InitialOverlap = FirecrackerTrajectory.IsInitialOverlap(transform.position);
            // Inventory rejects an invalid origin before consuming an item. Keep this
            // final safety for direct/controlled launches too, without a fake normal.
            grounded = InitialOverlap;
        }
        void Awake()
        {
            body = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            body.name = "Red paper firecracker";
            body.transform.SetParent(transform, false);
            body.transform.localScale = new Vector3(.08f, .1f, .08f);
            var bodyCollider = body.GetComponent<Collider>();
            bodyCollider.enabled = false;
            Destroy(bodyCollider);
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.SetColor("_BaseColor", new Color(.85f, .07f, .025f));
            body.GetComponent<Renderer>().sharedMaterial = material;
            glow = gameObject.AddComponent<Light>();
            glow.color = new Color(1, .36f, .05f); glow.range = 3.2f; glow.intensity = .7f;
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false; source.spatialBlend = 1;
            source.minDistance = 2; source.maxDistance = 28; source.volume = .35f;
            source.dopplerLevel = 0; source.ignoreListenerPause = false; source.ignoreListenerVolume = false;
            crackTakes = new AudioClip[ExternalAudio.VariantCount("firecracker")];
            for (int i = 0; i < crackTakes.Length; i++) crackTakes[i] = ExternalAudio.Required("firecracker", i);
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
                movementRemainder += Time.deltaTime;
                Vector3 position = transform.position;
                int stepsThisFrame = 0;
                // Carry any remainder rather than changing the step with frame rate.
                // The cap bounds work after a stall and never changes engine timing.
                while (movementRemainder >= FirecrackerTrajectory.StepSeconds &&
                    stepsThisFrame++ < MaximumMovementStepsPerFrame && !grounded)
                {
                    bool contact = FirecrackerTrajectory.Step(ref position, ref velocity,
                        out var hit, out float fraction);
                    if (contact)
                    {
                        if (!HasFirstContact)
                        {
                            HasFirstContact = true;
                            FirstContactPosition = position;
                            FirstContactPoint = hit.point;
                            FirstContactNormal = hit.normal;
                            FirstContactTime = (movementSteps + fraction) * FirecrackerTrajectory.StepSeconds;
                        }
                        grounded = hit.normal.y > .6f;
                    }
                    movementRemainder -= FirecrackerTrajectory.StepSeconds;
                    movementSteps++;
                }
                transform.position = position;
                if (!Exploded && !reducedMotion) body.transform.Rotate(420 * Time.deltaTime, 0, 0);
            }
            if (!Exploded)
            {
                if (life < FirecrackerTrajectory.FuseSeconds) return;
                Exploded = true;
                body.SetActive(false);
                pulse = 0;
            }
            if (life >= FirecrackerTrajectory.BurnSeconds) { Destroy(gameObject); return; }
            pulse -= Time.deltaTime;
            if (!reducedMotion) glow.intensity = Mathf.MoveTowards(glow.intensity, 0, Time.deltaTime * 18);
            if (pulse > 0) return;
            pulse = .5f;
            glow.intensity = reducedMotion ? .45f : 4;
            PlayPop(session);
            foreach (var brain in FindObjectsByType<StalkerBrain>(FindObjectsSortMode.None))
                if (brain.HearNoise(transform.position, FirecrackerTrajectory.BurnSeconds - life)) Attracted++;
            foreach (var mask in FindObjectsByType<LanternMaskEncounter>(FindObjectsSortMode.None))
                if (mask.HearNoise(transform.position, FirecrackerTrajectory.BurnSeconds - life)) Attracted++;
        }
        void PlayPop(GameSession session)
        {
            // The first explosion uses this same path: one pop, not a second overlapping one-shot.
            crack = crackTakes[PopsPlayed % crackTakes.Length];
            source.PlayOneShot(crack);
            PopsPlayed++;
            if ((PopsPlayed - 1) % 4 == 0 && session.player && session.player.eyes &&
                Vector3.Distance(session.player.eyes.transform.position, transform.position) <= 18)
                session.Shell.ShowCaption("[폭죽 터지는 소리]", 1.2f);
        }
        void OnDisable() { if (source) source.Stop(); if (glow) glow.intensity = 0; }
        void OnDestroy()
        {
            if (material) Destroy(material);
            if (crackTakes != null) foreach (var take in crackTakes) if (take) Destroy(take);
        }
    }
}
