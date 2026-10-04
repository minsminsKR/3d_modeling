using UnityEngine;

namespace HappyToy.V2
{
    /// <summary>A brief, original static sting from a real recognition event, never a proximity radar.</summary>
    [DisallowMultipleComponent]
    public sealed class DetectionFeedback : MonoBehaviour
    {
        const float Duration = .8f, Cooldown = 4f;
        AudioSource source;
        AudioClip sting;
        Texture2D[] grain;
        float remaining, cooldown;
        public int CuesPlayed { get; private set; }
        public float Remaining => remaining;
        public bool Active => remaining > 0 && GameSession.Current && GameSession.Current.InputAllowed;
        public bool Softened => GameSession.Current && GameSession.Current.Shell && GameSession.Current.Shell.ReducedMotion;
        public float Strength => Active ? Mathf.Clamp01((Duration - remaining) / .045f) * Mathf.Clamp01(remaining / .35f) : 0;
        public Texture2D GrainTexture => Active && !Softened ? grain[Mathf.Min(grain.Length - 1, (int)((Duration - remaining) * 10))] : null;

        void Awake()
        {
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false; source.spatialBlend = 0; source.volume = .52f; source.priority = 20;
            source.ignoreListenerPause = false; source.ignoreListenerVolume = false; source.dopplerLevel = 0;
            const int rate = 24000;
            var samples = new float[(int)(rate * .58f)]; var random = new System.Random(2713); float low = 0;
            for (int i = 0; i < samples.Length; i++)
            {
                float t = i / (float)rate, noise = (float)(random.NextDouble() * 2 - 1);
                low = Mathf.Lerp(low, noise, .22f);
                float envelope = Mathf.Clamp01(t / .009f) * Mathf.Pow(1 - t / .58f, 1.7f);
                // Soft-limited banded crackle with a short low alarm body; no borrowed audio.
                samples[i] = Mathf.Clamp((low * 1.3f + (noise - low) * .16f + Mathf.Sin(2 * Mathf.PI * 73 * t) * .18f) * envelope, -.58f, .58f);
            }
            sting = AudioClip.Create("Recognition static sting", samples.Length, 1, rate, false); sting.SetData(samples, 0);
            grain = new Texture2D[8];
            for (int frame = 0; frame < grain.Length; frame++)
            {
                var texture = new Texture2D(192, 108, TextureFormat.RGBA32, false) { name = "Recognition grain " + frame, filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
                var pixels = new Color32[192 * 108];
                for (int y = 0; y < 108; y++) for (int x = 0; x < 192; x++)
                {
                    byte value = (byte)random.Next(45, 191);
                    // Low-opacity grain and thin tracking tears, never a full-screen white flash.
                    byte alpha = (byte)(y == 18 + frame * 7 || y == 83 - frame * 4 ? 85 : random.Next(14, 47));
                    pixels[y * 192 + x] = new Color32(value, value, value, alpha);
                }
                texture.SetPixels32(pixels); texture.Apply(false, true); grain[frame] = texture;
            }
        }
        public static void Signal(GameSession session, Transform observer)
        {
            if (!session || !session.InputAllowed || session.StoryStep >= 4 || !session.player || !observer) return;
            var player = session.player;
            if (player.Hidden || !EnemyNavigation.SameFloor(player.transform.position, observer.position.y) ||
                Vector3.Distance(player.transform.position, observer.position) > 22 || !ActualRecognitionSight(player, observer)) return;
            var feedback = player.GetComponent<DetectionFeedback>();
            if (!feedback) feedback = player.gameObject.AddComponent<DetectionFeedback>();
            feedback.Play();
        }
        static bool ActualRecognitionSight(PlayerMotor player, Transform observer)
        {
            // Reuse each actor's real recognition samples. A second low ray would
            // incorrectly suppress a genuine eye-level sighting over a desk/piano.
            var stalker = observer.GetComponent<StalkerBrain>();
            if (stalker && stalker.isActiveAndEnabled) return stalker.CanSeePlayer();
            var lantern = observer.GetComponent<LanternMaskEncounter>();
            if (lantern && lantern.isActiveAndEnabled) return lantern.CanSeePlayer();
            var mannequin = observer.GetComponent<WeepingAngelEncounter>();
            return mannequin && mannequin.isActiveAndEnabled && mannequin.Released && !mannequin.Observed &&
                player.eyes && player.flashlight && player.flashlight.enabled &&
                Vector3.Distance(player.transform.position, observer.position) < 1.05f &&
                EnemyNavigation.ClearSight(player.eyes.transform.position, observer.position + Vector3.up * 1.2f);
        }
        void Play()
        {
            if (cooldown > 0 || !isActiveAndEnabled) return;
            remaining = Duration; cooldown = Cooldown; CuesPlayed++;
            // Keep tension on the same accepted, four-second-coalesced recognition
            // event as the actual visual/sting, rather than suppressed Signal calls.
            var session = GameSession.Current;
            if (session && session.player && session.player.gameObject == gameObject)
                PerceivedTension.ReportRecognition(session);
            source.volume = Softened ? .29f : .52f;
            source.PlayOneShot(sting);
        }
        void Update()
        {
            var session = GameSession.Current;
            if (source) source.volume = Softened ? .29f : .52f;
            if (!session || session.Finished || session.StoryStep >= 4) { remaining = 0; if (source) source.Stop(); return; }
            if (!session.InputAllowed) return;
            remaining = Mathf.Max(0, remaining - Time.deltaTime);
            cooldown = Mathf.Max(0, cooldown - Time.deltaTime);
        }
        void OnDisable() { remaining = cooldown = 0; if (source) source.Stop(); }
        void OnDestroy()
        {
            if (sting) Destroy(sting);
            if (grain != null) foreach (var texture in grain) if (texture) Destroy(texture);
        }
    }
}
