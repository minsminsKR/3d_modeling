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
        AudioClip click, cabinetOpen, cabinetClose, discovery, breath;
        AudioClip[] woodSteps, stoneSteps, waterSteps;
        readonly List<AudioClip> ownedClips = new List<AudioClip>();
        Vector3 previousPosition, cameraHome, cameraPreviousPosition, stancePosition;
        float stride;
        readonly LocomotionCameraMotion cameraMotion = new LocomotionCameraMotion();
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
            cameraPreviousPosition = transform.position; stancePosition = cameraHome;
            steps = Source("Player footsteps", .36f, 120);
            foley = Source("Player interaction foley", .38f, 70);
            breathing = Source("Player breathing", 0, 160);
            woodSteps = Variants("step-wood");
            stoneSteps = Variants("step-stone");
            waterSteps = Variants("step-wet");
            click = Recorded("flashlight");
            cabinetOpen = Own(ExternalAudio.Required("cabinet-open"));
            cabinetClose = Own(ExternalAudio.Required("cabinet-close"));
            discovery = Recorded("discovery");
            breath = Own(ExternalAudio.Required("player-breath"));
            breathing.clip = breath; breathing.loop = true; breathing.Play();
            initialized = true;
        }
        AudioClip Own(AudioClip clip)
        {
            if (clip) ownedClips.Add(clip);
            return clip;
        }
        AudioClip Recorded(string cue)
        { return Own(ExternalAudio.Required(cue)); }
        AudioClip[] Variants(string cue)
        {
            var clips = new AudioClip[Mathf.Max(1, ExternalAudio.VariantCount(cue))];
            for (int i = 0; i < clips.Length; i++)
            {
                var recorded = Own(ExternalAudio.Required(cue, i));
                clips[i] = recorded;
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
            var displacement = transform.position - cameraPreviousPosition;
            cameraPreviousPosition = transform.position;
            if (session.ChapterMode && session.Chapter.FirstAppearances && session.Chapter.FirstAppearances.CameraOwned)
            {
                cameraMotion.Reset(); return;
            }
            // Explicit pause freezes the player's actual view, including an in-flight stride.
            if (!session.InputAllowed) return;
            var stanceHome = player.Hidden ? player.HiddenCameraLocalPosition : cameraHome - Vector3.up * player.CameraHeightOffset;
            bool motion = !player.Hidden && !session.Shell.ReducedMotion && player.isActiveAndEnabled;
            if (motion)
            {
                cameraMotion.Step(displacement, player.ActualSpeed, player.Grounded, player.Running,
                    player.Crouching, Time.deltaTime);
                stancePosition = Vector3.Lerp(stancePosition, stanceHome, 1 - Mathf.Exp(-14 * Time.deltaTime));
            }
            else { cameraMotion.Reset(); stancePosition = stanceHome; }
            player.eyes.transform.localPosition = stancePosition + cameraMotion.PositionOffset;
            // Rebuild from the mouse look every frame. An additive quaternion is never accumulated.
            // Disabled motors remain available to scripted camera/audit owners.
            if (player.isActiveAndEnabled)
                player.eyes.transform.localRotation = player.LookRotation * Quaternion.Euler(cameraMotion.RotationOffset);
            if (player.Hidden && player.flashlight)
            {
                player.flashlight.transform.SetPositionAndRotation(player.eyes.transform.position, player.eyes.transform.rotation);
            }
            float targetFov = session.Shell.FieldOfView + cameraMotion.FovOffset;
            player.eyes.fieldOfView = Mathf.Lerp(player.eyes.fieldOfView, targetFov, 1 - Mathf.Exp(-5 * Time.unscaledDeltaTime));
        }

        void OnDisable()
        {
            cameraMotion.Reset();
            var session = GameSession.Current;
            bool cameraOwned = session && session.ChapterMode && session.Chapter.FirstAppearances && session.Chapter.FirstAppearances.CameraOwned;
            if (initialized && player && player.eyes && !cameraOwned)
            {
                player.eyes.transform.localPosition = player.Hidden ? player.HiddenCameraLocalPosition : cameraHome - Vector3.up * player.CameraHeightOffset;
                if (player.isActiveAndEnabled) player.eyes.transform.localRotation = player.LookRotation;
            }
            if (steps) steps.Stop(); if (foley) foley.Stop(); if (breathing) breathing.Stop();
        }
        void OnEnable()
        {
            cameraPreviousPosition = transform.position;
            if (initialized && breathing) breathing.Play();
        }
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
