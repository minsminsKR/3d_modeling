using System.Collections.Generic;
using UnityEngine;

namespace HappyToy.V2
{
    /// <summary>Local, distance-driven foley and restrained first-person motion. No scene rebuild needed.</summary>
    [DisallowMultipleComponent]
    public sealed class PlayerFeedback : MonoBehaviour
    {
        PlayerMotor player;
        AudioSource steps, foley, breathing;
        AudioClip dryStep, wetStep, click, cabinetOpen, cabinetClose, discovery, breath;
        AudioClip[] woodSteps, stoneSteps, waterSteps;
        readonly List<AudioClip> ownedClips = new List<AudioClip>();
        Vector3 previousPosition, cameraHome;
        float stride, phase;
        int stepIndex;
        bool initialized;
        public int FootstepsPlayed { get; private set; }
        public string LastFootstepSurface { get; private set; } = "";
        public string LastFootstepColliderName { get; private set; } = "";
        public AudioClip LastFootstepClip { get; private set; }
        public AudioClip LastInteractionClip { get; private set; }
        public int InteractionCuesPlayed { get; private set; }
        public AudioSource FootstepSource => steps;
        public AudioSource InteractionSource => foley;
        public event System.Action<Vector3, bool, float> Footstep;

        void Start()
        {
            player = GetComponent<PlayerMotor>();
            if (!player || !player.eyes) { enabled = false; return; }
            if (!GetComponent<DetectionFeedback>()) gameObject.AddComponent<DetectionFeedback>();
            if (!GetComponent<PerceivedTension>()) gameObject.AddComponent<PerceivedTension>();
            previousPosition = transform.position;
            cameraHome = player.eyes.transform.localPosition;
            steps = Source("Player footsteps", .36f, 120);
            foley = Source("Player interaction foley", .38f, 70);
            breathing = Source("Player breathing", 0, 160);
            dryStep = Own(MakeTransient("Soft sole on dusty floor", .19f, 0));
            wetStep = Own(MakeTransient("Soft sole in shallow water", .27f, 1));
            woodSteps = Variants("step-wood", dryStep);
            stoneSteps = Variants("step-stone", dryStep);
            waterSteps = Variants("step-wet", wetStep);
            click = RecordedOrFallback("flashlight", "Flashlight switch", .065f, 2);
            cabinetOpen = Own(ExternalAudio.Owned("cabinet-open"));
            cabinetClose = Own(ExternalAudio.Owned("cabinet-close"));
            if (!cabinetOpen) cabinetOpen = Own(MakeCabinetFallback(true));
            if (!cabinetClose) cabinetClose = Own(MakeCabinetFallback(false));
            discovery = RecordedOrFallback("discovery", "Recovered paper and soft bell", .48f, 4);
            breath = Own(MakeBreath());
            breathing.clip = breath; breathing.loop = true; breathing.Play();
            initialized = true;
        }
        AudioClip Own(AudioClip clip)
        {
            if (clip) ownedClips.Add(clip);
            return clip;
        }
        AudioClip RecordedOrFallback(string cue, string label, float seconds, int kind)
        {
            var clip = ExternalAudio.Owned(cue);
            return Own(clip ? clip : MakeTransient(label, seconds, kind));
        }
        AudioClip[] Variants(string cue, AudioClip fallback)
        {
            var clips = new AudioClip[Mathf.Max(1, ExternalAudio.VariantCount(cue))];
            for (int i = 0; i < clips.Length; i++)
            {
                var recorded = Own(ExternalAudio.Owned(cue, i));
                clips[i] = recorded ? recorded : fallback;
            }
            return clips;
        }
        AudioSource Source(string label, float volume, int priority)
        {
            var emitter = new GameObject(label);
            emitter.transform.SetParent(transform, false);
            var source = emitter.AddComponent<AudioSource>();
            source.playOnAwake = false; source.spatialBlend = 0;
            source.volume = volume; source.priority = priority; source.dopplerLevel = 0;
            source.ignoreListenerPause = false;
            source.ignoreListenerVolume = false;
            return source;
        }
        public void PlayFlashlight(bool on)
        {
            if (!initialized || !isActiveAndEnabled) return;
            LastInteractionClip = click; InteractionCuesPlayed++;
            foley.pitch = on ? 1 : .96f; foley.PlayOneShot(click, .75f);
        }
        public void PlayHide(bool entering)
        {
            previousPosition = transform.position; stride = 0;
            if (!initialized || !isActiveAndEnabled) return;
            LastInteractionClip = entering ? cabinetClose : cabinetOpen; InteractionCuesPlayed++;
            // The successful hiding transition seats the door; leaving opens it.
            // One short metal latch clack replaces any preceding interaction. No cloth,
            // extra impact, paper or pitch-transformed copy is layered on it.
            foley.Stop(); foley.pitch = 1; foley.clip = LastInteractionClip; foley.Play();
            Caption(entering ? "[철컥 · 문 잠금쇠 닫힘]" : "[철컥 · 문 잠금쇠 열림]", .8f);
        }
        public void PlayDiscovery()
        {
            if (!initialized || !isActiveAndEnabled) return;
            LastInteractionClip = discovery; InteractionCuesPlayed++;
            foley.pitch = 1; foley.PlayOneShot(discovery, .6f);
        }
        void Caption(string text, float duration)
        {
            if (GameSession.Current && GameSession.Current.Shell)
                GameSession.Current.Shell.ShowCaption(text, duration);
        }
        void Update()
        {
            if (!initialized) return;
            var displacement = transform.position - previousPosition;
            previousPosition = transform.position; displacement.y = 0;
            var session = GameSession.Current;
            if (!session || !session.InputAllowed)
            {
                stride = 0; return;
            }
            // Recovery continues in a cabinet; hiding stops footsteps, not the breathing mix.
            float effort = Mathf.InverseLerp(.6f, .06f, player.Stamina);
            breathing.volume = Mathf.MoveTowards(breathing.volume, effort * .22f, Time.deltaTime * .18f);
            if (player.Hidden) { stride = 0; return; }
            // Teleports/hiding/audit setup must never create a burst of footsteps.
            if (displacement.magnitude > .6f) { stride = 0; return; }
            if (player.Grounded && player.ActualSpeed > .12f)
            {
                stride += displacement.magnitude;
                float interval = player.Crouching ? .85f : player.Running ? 1.6f : 1.15f;
                if (stride >= interval)
                {
                    stride %= interval;
                    int strideIndex = stepIndex++;
                    var contact = transform.position + transform.right * (strideIndex % 2 == 0 ? .11f : -.11f);
                    LastFootstepSurface = ContactSurface(contact, out bool wet, out string floorName);
                    LastFootstepColliderName = floorName;
                    var bank = wet ? waterSteps : LastFootstepSurface == "stone" ? stoneSteps : woodSteps;
                    // Several recorded contacts and a tiny shoe variation avoid a repeating single-sample rhythm.
                    LastFootstepClip = bank[strideIndex % bank.Length];
                    steps.pitch = 1 + ((strideIndex % 7) - 3) * .008f;
                    float strength = player.Crouching ? .24f : player.Running ? 1 : .55f;
                    steps.PlayOneShot(LastFootstepClip, strength * .95f);
                    FootstepsPlayed++;
                    player.ReportFootstep(contact, wet);
                    Footstep?.Invoke(contact, wet, strength);
                }
            }
            else stride = Mathf.MoveTowards(stride, 0, Time.deltaTime * 2);
        }
        static string ContactSurface(Vector3 contact, out bool wet, out string floorName)
        {
            // The visible water sheet need not have collision; its authored bounds are authoritative.
            wet = WaterSurfaceFeedback.IsSubmerged(contact);
            floorName = "";
            string surface = "";
            if (Physics.Raycast(contact + Vector3.up * .15f, Vector3.down, out var floor,
                .6f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                floorName = floor.collider.name;
                surface = floorName.ToLowerInvariant();
                // Damp stair paint is not standing water and must not increase the AI noise radius.
                wet |= surface.Contains("water") || surface.Contains("wet");
                var renderer = floor.collider.GetComponent<Renderer>();
                if (!renderer) renderer = floor.collider.GetComponentInParent<Renderer>();
                if (renderer && renderer.sharedMaterial) surface += " " + renderer.sharedMaterial.name.ToLowerInvariant();
            }
            if (wet) return "wet";
            // The school's washroom is grouted ceramic; the corridor/classrooms retain parquet.
            return surface.Contains("washroom") || surface.Contains("tile") || surface.Contains("stone") ||
                surface.Contains("ceramic") || surface.Contains("concrete") || surface.Contains("grout") ? "stone" : "wood";
        }
        void LateUpdate()
        {
            if (!initialized || !player.eyes) return;
            var session = GameSession.Current;
            if (!session || !session.Shell) return;
            if (session.ChapterMode && session.Chapter.FirstAppearances && session.Chapter.FirstAppearances.CameraOwned) return;
            bool motion = session.InputAllowed && !player.Hidden && !session.Shell.ReducedMotion;
            float speed = motion && player.Grounded ? Mathf.Clamp(player.ActualSpeed, 0, player.runSpeed) : 0;
            phase += speed * Time.deltaTime * 4.5f;
            // Position only: mouse rotation and scripted camera direction remain authoritative.
            var bob = speed > .12f ? new Vector3(Mathf.Sin(phase * .5f) * .009f,
                Mathf.Abs(Mathf.Sin(phase)) * .015f, 0) : Vector3.zero;
            var stanceHome = player.Hidden ? player.HiddenCameraLocalPosition : cameraHome - Vector3.up * player.CameraHeightOffset;
            player.eyes.transform.localPosition = player.Hidden || session.Shell.ReducedMotion ? stanceHome : Vector3.Lerp(player.eyes.transform.localPosition,
                stanceHome + bob, 1 - Mathf.Exp(-14 * Time.deltaTime));
            if (player.Hidden && player.flashlight)
            {
                player.flashlight.transform.SetPositionAndRotation(player.eyes.transform.position, player.eyes.transform.rotation);
            }
            float targetFov = session.Shell.FieldOfView + (motion && player.Running && speed > .5f ? 2.5f : 0);
            player.eyes.fieldOfView = Mathf.Lerp(player.eyes.fieldOfView, targetFov, 1 - Mathf.Exp(-5 * Time.unscaledDeltaTime));
        }
        static AudioClip MakeTransient(string name, float seconds, int kind)
        {
            const int rate = 24000;
            var data = new float[Mathf.CeilToInt(seconds * rate)];
            var random = new System.Random(9127 + kind);
            float filtered = 0;
            for (int i = 0; i < data.Length; i++)
            {
                float t = i / (float)rate;
                float n = (float)random.NextDouble() * 2 - 1;
                filtered = Mathf.Lerp(filtered, n, kind == 2 ? .8f : .18f);
                float attack = Mathf.Clamp01(t / .004f);
                float tail = Mathf.Clamp01((seconds - t) / .015f);
                float value;
                if (kind == 0) value = .35f * Mathf.Exp(-30 * t) * Mathf.Sin(2 * Mathf.PI * 95 * t) + filtered * .18f * Mathf.Exp(-15 * t);
                else if (kind == 1) value = filtered * .32f * Mathf.Exp(-11 * t) + .08f * Mathf.Sin(2 * Mathf.PI * (330 * t - 250 * t * t)) * Mathf.Exp(-25 * t);
                else if (kind == 2) value = filtered * .35f * Mathf.Exp(-65 * t);
                else if (kind == 3) value = filtered * .26f * Mathf.Sin(Mathf.PI * t / seconds);
                else value = filtered * .12f * Mathf.Exp(-12 * t) + .1f * Mathf.Sin(2 * Mathf.PI * 660 * t) * Mathf.Exp(-9 * t);
                data[i] = Mathf.Clamp(value * attack * tail, -.65f, .65f);
            }
            var clip = AudioClip.Create(name, data.Length, 1, rate, false); clip.SetData(data, 0); return clip;
        }
        static AudioClip MakeCabinetFallback(bool opening)
        {
            const int rate = 24000;
            float seconds = opening ? .24f : .28f;
            var data = new float[Mathf.CeilToInt(rate * seconds)];
            var random = new System.Random(opening ? 7821 : 7822);
            for (int index = 0; index < data.Length; index++)
            {
                float t = index / (float)rate;
                float value = 0;
                foreach(float start in new[]{0f, opening ? .055f : .072f})
                {
                    float u=t-start;if(u<0) continue;
                    value+=Mathf.Clamp01(u/.0007f)*(.34f*Mathf.Exp(-65*u)*Mathf.Sin(2*Mathf.PI*210*u)+
                        .22f*Mathf.Exp(-90*u)*Mathf.Sin(2*Mathf.PI*1170*u)+
                        .28f*Mathf.Exp(-210*u)*((float)random.NextDouble()*2-1));
                }
                data[index] = value*Mathf.Clamp01((seconds-t)/.015f);
            }
            var clip = AudioClip.Create(opening ? "Cabinet metal latch release fallback" : "Cabinet metal latch seat fallback", data.Length, 1, rate, false);
            clip.SetData(data, 0); return clip;
        }
        static AudioClip MakeBreath()
        {
            const int rate = 24000;
            var data = new float[rate * 3]; var random = new System.Random(4921); float filtered = 0;
            for (int i = 0; i < data.Length; i++)
            {
                float t = i / (float)rate;
                filtered = Mathf.Lerp(filtered, (float)random.NextDouble() * 2 - 1, .045f);
                float envelope = Mathf.Pow(Mathf.Max(0, Mathf.Sin(t * Mathf.PI * 2 / 3)), 1.4f);
                data[i] = filtered * envelope * .5f;
            }
            var clip = AudioClip.Create("Quiet exertion breath", data.Length, 1, rate, false); clip.SetData(data, 0); return clip;
        }
        void OnDisable()
        {
            if (initialized && player && player.eyes) player.eyes.transform.localPosition = cameraHome;
            if (steps) steps.Stop(); if (foley) foley.Stop(); if (breathing) breathing.Stop();
        }
        void OnEnable() { if (initialized && breathing) breathing.Play(); }
        void OnDestroy()
        {
            foreach (var clip in ownedClips) if (clip) Destroy(clip);
            ownedClips.Clear();
            // Removing this component alone must release its emitters, as scene retry does.
            if (steps) Destroy(steps.gameObject);
            if (foley) Destroy(foley.gameObject);
            if (breathing) Destroy(breathing.gameObject);
        }
    }
}
