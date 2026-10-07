using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace HappyToy.V2
{
    /// <summary>Original recognition interference and continuous pressure from witnessed pursuit.</summary>
    [DisallowMultipleComponent]
    public sealed class DetectionFeedback : MonoBehaviour
    {
        const float Duration = .8f, Cooldown = 4f;
        readonly List<Transform> witnessed = new List<Transform>(8);
        PlayerMotor player;
        AudioSource source;
        AudioClip sting;
        Texture2D peripheral, nativeGrain;
        GameObject postObject;
        Volume post;
        VolumeProfile profile;
        LensDistortion lens;
        ChromaticAberration chromatic;
        FilmGrain filmGrain;
        float remaining, cooldown, chase, pulseClock;
        public int CuesPlayed { get; private set; }
        public float Remaining => remaining;
        public bool Active => remaining > 0 && CurrentSession && CurrentSession.InputAllowed && !player.Hidden;
        public bool Softened => CurrentSession && CurrentSession.Shell && CurrentSession.Shell.ReducedMotion;
        public float Strength => Active ? Mathf.Clamp01((Duration - remaining) / .045f) * Mathf.Clamp01(remaining / .35f) : 0;
        // Grain is rendered at native pixel density by URP, never stretched over
        // the HUD from a low-resolution animated static texture.
        public Texture2D GrainTexture => null;
        public Texture2D NativeNoiseTexture => nativeGrain;
        public Texture2D PeripheralTexture => peripheral;
        public float ChaseStrength => CurrentSession ? chase : 0;
        public float PeripheralStrength
        {
            get
            {
                if (!CurrentSession || !CurrentSession.InputAllowed) return 0;
                float heartbeat = Softened ? 0 : PerceivedTension.HeartbeatEnvelope(pulseClock);
                float sustained = chase * (.34f + heartbeat * .07f);
                return Mathf.Clamp01(Mathf.Max(Strength * .88f, sustained)) * (Softened ? .4f : 1);
            }
        }
        public float DistortionStrength => lens != null && post && post.weight > 0 ? Mathf.Abs(lens.intensity.value) : 0;
        public float ChromaticStrength => chromatic != null && post && post.weight > 0 ? chromatic.intensity.value : 0;
        public float NoiseStrength => filmGrain != null && post && post.weight > 0 && filmGrain.active ? filmGrain.intensity.value : 0;
        public float WitnessedPressure => CurrentSession && CurrentSession.InputAllowed && !player.Hidden ? Mathf.Max(Strength, chase) : 0;
        GameSession CurrentSession
        {
            get
            {
                var session = GameSession.Current;
                return isActiveAndEnabled && player && session && session.player == player &&
                    session.gameObject.scene == gameObject.scene && !session.Finished && session.StoryStep < 4 ? session : null;
            }
        }

        void Awake()
        {
            player = GetComponent<PlayerMotor>();
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false; source.spatialBlend = 0; source.volume = .64f; source.priority = 20;
            source.ignoreListenerPause = false; source.ignoreListenerVolume = false; source.dopplerLevel = 0;
            sting = MakeRecognition(); peripheral = MakePeripheralVeil(); nativeGrain = MakeNativeGrain();
            // Only perception parameters override the independently owned lighting.
            postObject = new GameObject("Witnessed threat lens scope");
            postObject.transform.SetParent(transform, false);
            profile = ScriptableObject.CreateInstance<VolumeProfile>(); profile.name = "Original witnessed threat lens";
            lens = profile.Add<LensDistortion>(); lens.intensity.Override(0);
            lens.xMultiplier.Override(.55f); lens.yMultiplier.Override(.65f); lens.scale.Override(1);
            chromatic = profile.Add<ChromaticAberration>(); chromatic.intensity.Override(0);
            filmGrain = profile.Add<FilmGrain>(); filmGrain.type.Override(FilmGrainLookup.Custom);
            filmGrain.texture.Override(nativeGrain);
            filmGrain.intensity.Override(0); filmGrain.response.Override(.58f);
            post = postObject.AddComponent<Volume>(); post.isGlobal = true; post.priority = 90; post.weight = 0; post.sharedProfile = profile;
        }

        public static void Signal(GameSession session, Transform observer)
        {
            if (!session || session != GameSession.Current || !session.InputAllowed || session.StoryStep >= 4 ||
                !session.player || !observer || observer.gameObject.scene != session.gameObject.scene) return;
            var player = session.player;
            if (player.Hidden || !EnemyNavigation.SameFloor(player.transform.position, observer.position.y) ||
                Vector3.Distance(player.transform.position, observer.position) > 22 || !ActualRecognitionSight(player, observer)) return;
            var feedback = player.GetComponent<DetectionFeedback>();
            if (!feedback) feedback = player.gameObject.AddComponent<DetectionFeedback>();
            if (!feedback.isActiveAndEnabled) return;
            // Register all genuine sightings, including simultaneous coalesced sounds.
            // Unknown enemies never drive the ongoing pursuit effect.
            if (!feedback.witnessed.Contains(observer)) feedback.witnessed.Add(observer);
            feedback.Play(observer);
        }

        static bool ActualRecognitionSight(PlayerMotor player, Transform observer)
        {
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

        void Play(Transform observer)
        {
            if (cooldown > 0 || !isActiveAndEnabled) return;
            remaining = Duration; cooldown = Cooldown; CuesPlayed++; pulseClock = 0;
            var session = CurrentSession;
            if (session) PerceivedTension.ReportRecognition(session);
            // Direction is sampled at the real sighting. A moving stereo alarm must
            // never track an actor through a wall; physical feet remain the locator.
            Vector3 direction = (observer.position - transform.position).normalized;
            source.panStereo = Softened ? 0 : Mathf.Clamp(Vector3.Dot(transform.right, direction) * .34f, -.34f, .34f);
            source.volume = Softened ? .32f : .64f;
            source.PlayOneShot(sting);
        }

        bool ActivePursuitIdentity(Transform observer)
        {
            if (!observer || !observer.gameObject.activeInHierarchy || !CurrentSession ||
                observer.gameObject.scene != gameObject.scene) return false;
            var stalker = observer.GetComponent<StalkerBrain>();
            if (stalker) return stalker.isActiveAndEnabled && (stalker.state == StalkerBrain.State.Chase || stalker.AttackActive);
            var lantern = observer.GetComponent<LanternMaskEncounter>();
            if (lantern) return lantern.isActiveAndEnabled && (lantern.State == LanternMaskEncounter.Phase.Chase || lantern.AttackActive);
            var mannequin = observer.GetComponent<WeepingAngelEncounter>();
            return mannequin && mannequin.isActiveAndEnabled && !mannequin.Resolved && mannequin.Released &&
                (mannequin.Moving || mannequin.AttackActive);
        }

        bool Pursuing(Transform observer)
        {
            if (!EnemyNavigation.SameFloor(player.transform.position, observer.position.y)) return false;
            var stalker = observer.GetComponent<StalkerBrain>();
            if (stalker) return !player.Hidden || stalker.SawHiding && !player.HidingProtected;
            if (observer.GetComponent<LanternMaskEncounter>()) return !player.Hidden;
            var mannequin = observer.GetComponent<WeepingAngelEncounter>();
            return mannequin && !player.Hidden && player.flashlight && player.flashlight.enabled && !mannequin.Observed;
        }

        void Update()
        {
            var session = CurrentSession;
            if (!session) { Clear(); return; }
            if (source) source.volume = Softened ? .32f : .64f;
            if (!session.InputAllowed) return;
            float dt = Time.deltaTime;
            remaining = Mathf.Max(0, remaining - dt); cooldown = Mathf.Max(0, cooldown - dt);
            bool pursuing = false;
            for (int i = witnessed.Count - 1; i >= 0; i--)
            {
                // Keep the identity for this one continuous, genuinely earned Chase
                // through stairs/protected hiding. These states suppress intensity,
                // but the AI may not emit a second recognition on returning.
                // Search, patrol, disabled actors and dead scenes end its lifetime.
                if (!ActivePursuitIdentity(witnessed[i])) { witnessed.RemoveAt(i); continue; }
                pursuing |= Pursuing(witnessed[i]);
            }
            chase = Mathf.MoveTowards(chase, pursuing ? 1 : 0, dt * (pursuing ? 2.6f : 1.15f));
            float stress = Mathf.Max(chase, Strength);
            pulseClock += dt * Mathf.Lerp(.9f, 1.2f, stress);
        }

        void LateUpdate()
        {
            if (!post) return;
            var session = CurrentSession;
            bool motion = session && session.InputAllowed && !Softened && !player.Hidden;
            post.weight = motion ? 1 : 0;
            if (!motion) { lens.intensity.value = chromatic.intensity.value = filmGrain.intensity.value = 0; return; }
            float breathing = .5f + .5f * Mathf.Sin(pulseClock * 2.4f);
            // Discovery attacks rapidly, then resolves into a finer, irregular
            // pursuit texture. Original fine grain is tiled in screen pixels and
            // luminance aware, preserving the path, faces and HUD readability.
            float interference = .5f + .5f * Mathf.Sin(pulseClock * 7.1f + .6f * Mathf.Sin(pulseClock * 2.7f));
            filmGrain.intensity.value = Mathf.Max(Strength * .76f, chase * (.35f + .055f * interference));
            // A smooth contraction and breathing veil support the grain without
            // strobes, camera rotation, blur or additional FOV movement.
            lens.intensity.value = -Strength * .085f - chase * (.022f + .007f * breathing);
            chromatic.intensity.value = Strength * .11f + chase * .025f;
        }

        void Clear()
        {
            remaining = cooldown = chase = pulseClock = 0; witnessed.Clear();
            if (source) source.Stop();
            if (filmGrain) filmGrain.intensity.value = 0;
            if (post) post.weight = 0;
        }

        static Texture2D MakeNativeGrain()
        {
            const int size = 512;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false, true) {
                name = "Original native-pixel witnessed interference", filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Repeat
            };
            var random = new System.Random(61781);
            var field = new float[size * size];
            for (int i = 0; i < field.Length; i++) field[i] = (float)random.NextDouble() * 2 - 1;
            var pixels = new Color32[field.Length];
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                int left = (x + size - 1) % size, right = (x + 1) % size, above = (y + size - 1) % size;
                // Correlation stays within one native pixel. No upscaled blocks,
                // checkerboard pattern or hard horizontal tearing. The stronger
                // original alpha contrast makes interference legible in shadows,
                // where the stock photographic grain is almost imperceptible.
                float grain = .58f * field[y * size + x] + .14f * field[y * size + left] +
                    .14f * field[above * size + x] + .07f * field[y * size + right] + .07f * field[above * size + left];
                byte alpha = (byte)Mathf.RoundToInt((.5f + .46f * grain) * 255);
                // URP ApplyGrain samples alpha and treats .5 as neutral. RGB
                // remains neutral; this texture cannot tint the scene or the HUD.
                pixels[y * size + x] = new Color32(128, 128, 128, alpha);
            }
            texture.SetPixels32(pixels); texture.Apply(false, true); return texture;
        }

        static Texture2D MakePeripheralVeil()
        {
            const int width = 1024, height = 576;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false) {
                name = "Original smooth peripheral threat veil", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp
            };
            var pixels = new Color32[width * height];
            for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
            {
                float u = (x + .5f) / width * 2 - 1, v = (y + .5f) / height * 2 - 1;
                float angle = Mathf.Atan2(v, u);
                float radius = Mathf.Sqrt(u * u * .87f + v * v);
                // Broad fixed contours close peripheral vision with a clear centre.
                radius += .028f * Mathf.Sin(angle * 3 + .7f) + .018f * Mathf.Sin(angle * 5 - .4f);
                float edge = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.36f, 1.2f, radius));
                float warm = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.6f, .95f, radius));
                pixels[y * width + x] = new Color32((byte)Mathf.Lerp(4, 30, warm), 2, 5, (byte)(edge * 238));
            }
            texture.SetPixels32(pixels); texture.Apply(false, true); return texture;
        }

        static AudioClip MakeRecognition()
        {
            const int rate = 48000; const float seconds = 1.65f;
            var samples = new float[Mathf.RoundToInt(rate * seconds)];
            var random = new System.Random(9341);
            float fast = 0, slow = 0;
            float highCut = 1 - Mathf.Exp(-2 * Mathf.PI * 2700 / rate);
            float lowCut = 1 - Mathf.Exp(-2 * Mathf.PI * 190 / rate);
            for (int i = 0; i < samples.Length; i++)
            {
                float t = i / (float)rate;
                float white = (float)random.NextDouble() * 2 - 1;
                fast += (white - fast) * highCut; slow += (fast - slow) * lowCut;
                float attack = Mathf.Clamp01(t / .008f), fade = Mathf.Clamp01((seconds - t) / .16f);
                float body = .43f * Mathf.Exp(-6 * t) * Mathf.Sin(2 * Mathf.PI * (64 * t - 9 * t * t));
                float resonance = .12f * Mathf.Exp(-3.7f * t) * Mathf.Sin(2 * Mathf.PI * 311 * t) +
                    .09f * Mathf.Exp(-4.2f * t) * Mathf.Sin(2 * Mathf.PI * 437 * t);
                float scrapeEnvelope = Mathf.Clamp01(t / .035f) * Mathf.Exp(-3.5f * t);
                float scrape = scrapeEnvelope * (.1f * Mathf.Sin(2 * Mathf.PI * (1490 * t - 320 * t * t)) +
                    .07f * Mathf.Sin(2 * Mathf.PI * (1513 * t - 331 * t * t)));
                float tail = t > .17f ? .065f * Mathf.Exp(-3 * (t - .17f)) *
                    Mathf.Sin(2 * Mathf.PI * 229 * (t - .17f)) * Mathf.Clamp01((t - .17f) / .04f) : 0;
                // Original band-limited interference tears into the metallic
                // impact and fades before it masks the enemy's next contact.
                float interference = (fast - slow) * .30f * Mathf.Exp(-4.8f * t) * Mathf.Clamp01(t / .015f);
                float value = (body + resonance + scrape + tail + interference) * attack * fade;
                samples[i] = .58f * (float)System.Math.Tanh(value / .58f);
            }
            var clip = AudioClip.Create("Original recognition iron and interference", samples.Length, 1, rate, false);
            clip.SetData(samples, 0); return clip;
        }

        void OnDisable() { Clear(); }
        void OnDestroy()
        {
            Clear();
            if (source) Destroy(source);
            if (sting) Destroy(sting);
            if (peripheral) Destroy(peripheral);
            if (nativeGrain) Destroy(nativeGrain);
            if (postObject) { postObject.SetActive(false); Destroy(postObject); }
            if (profile)
            {
                foreach (var effect in profile.components) if (effect) Destroy(effect);
                Destroy(profile);
            }
        }
    }
}
