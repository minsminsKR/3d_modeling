using UnityEngine;
namespace HappyToy.V2
{
    // Existing component/API is retained for scene GUIDs and old interactions.
    // No fuse: first solid contact triggers one recorded three-tick burst.
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
        float life, afterImpact;
        double movementRemainder;
        int movementSteps;
        const int MaximumMovementStepsPerFrame = 32;
        Transform body;
        AudioSource source;
        AudioClip crack;
        public void Launch(Vector3 initialVelocity)
        {
            velocity = initialVelocity;
            InitialOverlap = FirecrackerTrajectory.IsInitialOverlap(transform.position);
            if (InitialOverlap) Destroy(gameObject); // Never fabricate a contact or sound.
        }
        void Awake()
        {
            gameObject.layer = 2; // Visual/effect cannot block another throw or a pickup.
            body = SnapperVisuals.AttachPellet(transform);
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false; source.spatialBlend = 1;
            source.minDistance = 2; source.maxDistance = 28; source.volume = .55f;
            source.rolloffMode = AudioRolloffMode.Logarithmic;
            source.dopplerLevel = 0; source.ignoreListenerPause = false; source.ignoreListenerVolume = false;
            crack = ExternalAudio.Required("firecracker", Random.Range(0, ExternalAudio.VariantCount("firecracker")));
        }
        void Update()
        {
            var session = GameSession.Current;
            if (!session || session.Finished || session.StoryStep >= 4) { Destroy(gameObject); return; }
            source.ignoreListenerPause = LadyFinaleAudio.Active && session.InputAllowed;
            if (!session.InputAllowed) return;
            if (Exploded)
            {
                afterImpact += Time.deltaTime;
                if (afterImpact >= Mathf.Max(FirecrackerTrajectory.BurnSeconds, crack.length + .05f)) Destroy(gameObject);
                return;
            }
            life += Time.deltaTime;
            // An out-of-level throw expires silently; a fuse must not pop in the air.
            if (life > 5) { Destroy(gameObject); return; }
            movementRemainder += Time.deltaTime;
            Vector3 position = transform.position;
            int steps = 0;
            while (movementRemainder >= FirecrackerTrajectory.StepSeconds && steps++ < MaximumMovementStepsPerFrame)
            {
                bool contact = FirecrackerTrajectory.Step(ref position, ref velocity, out var hit, out float fraction);
                movementRemainder -= FirecrackerTrajectory.StepSeconds;
                if (contact)
                {
                    HasFirstContact = true; FirstContactPosition = position;
                    FirstContactPoint = hit.point; FirstContactNormal = hit.normal;
                    FirstContactTime = (movementSteps + fraction) * FirecrackerTrajectory.StepSeconds;
                    transform.position = position;
                    Pop(session, hit.point, hit.normal, hit.collider); return;
                }
                movementSteps++;
            }
            transform.position = position;
            if (!session.Shell.ReducedMotion) body.Rotate(360 * Time.deltaTime, 140 * Time.deltaTime, 0);
        }
        void Pop(GameSession session, Vector3 point, Vector3 normal, Collider hit)
        {
            if (Exploded) return;
            Exploded = true; body.gameObject.SetActive(false);
            source.PlayOneShot(crack); PopsPlayed = 1;
            SnapperImpactEffect.Create(transform, point, normal);
            // A one-shot observation owns a six-second inspection AFTER arrival.
            // Hearing/occlusion, chase priority and deaf actors remain AI-owned.
            foreach (var brain in FindObjectsByType<StalkerBrain>(FindObjectsSortMode.None))
                if (brain.HearSnapper(point)) Attracted++;
            foreach (var mask in FindObjectsByType<LanternMaskEncounter>(FindObjectsSortMode.None))
                if (mask.HearNoise(point, SnapperStockRules.InspectionSeconds)) Attracted++;
            foreach (var lady in FindObjectsByType<LadyPursuit>(FindObjectsSortMode.None))
                lady.ReactToSnapper(point, hit);
            if (session.player && session.player.eyes && Vector3.Distance(session.player.eyes.transform.position, point) <= 18)
                session.Shell.ShowCaption("[콩알탄이 티티틱, 빠르게 세 번 터지는 소리]", 1.2f);
        }
        void OnDisable() { if (source) source.Stop(); }
        void OnDestroy() { if (crack) Destroy(crack); }
    }
}
