using UnityEngine;

namespace HappyToy.V2
{
    /// <summary>Local, distance-driven foley and restrained first-person motion. No scene rebuild needed.</summary>
    [DisallowMultipleComponent]
    public sealed class PlayerFeedback : MonoBehaviour
    {
        PlayerMotor player;
        AudioSource steps, foley, breathing;
        AudioClip dryStep, wetStep, click, rustle, discovery, breath;
        Vector3 previousPosition, cameraHome;
        float stride, phase;
        int stepIndex;
        bool initialized;
        public int FootstepsPlayed { get; private set; }
        public event System.Action<Vector3, bool, float> Footstep;

        void Start()
        {
            player = GetComponent<PlayerMotor>();
            if (!player || !player.eyes) { enabled = false; return; }
            previousPosition = transform.position;
            cameraHome = player.eyes.transform.localPosition;
            steps = Source("Player footsteps", .36f, 120);
            foley = Source("Player interaction foley", .38f, 70);
            breathing = Source("Player breathing", 0, 160);
            dryStep = MakeTransient("Soft sole on dusty floor", .19f, 0);
            wetStep = MakeTransient("Soft sole in shallow water", .27f, 1);
            click = MakeTransient("Flashlight switch", .065f, 2);
            rustle = MakeTransient("Cabinet clothing rustle", .28f, 3);
            discovery = MakeTransient("Recovered paper and soft bell", .48f, 4);
            breath = MakeBreath();
            breathing.clip = breath; breathing.loop = true; breathing.Play();
            initialized = true;
        }
        AudioSource Source(string label, float volume, int priority)
        {
            var emitter = new GameObject(label);
            emitter.transform.SetParent(transform, false);
            var source = emitter.AddComponent<AudioSource>();
            source.playOnAwake = false; source.spatialBlend = 0;
            source.volume = volume; source.priority = priority; source.dopplerLevel = 0;
            source.ignoreListenerPause = false;
            return source;
        }
        public void PlayFlashlight(bool on)
        {
            if (!initialized) return;
            foley.pitch = on ? 1 : .86f; foley.PlayOneShot(click, .75f);
            Caption(on ? "[찰칵 · 손전등 켜짐]" : "[찰칵 · 손전등 꺼짐]", 1.2f);
        }
        public void PlayHide(bool entering)
        {
            previousPosition = transform.position; stride = 0;
            if (!initialized) return;
            foley.pitch = entering ? .85f : 1; foley.PlayOneShot(rustle, .65f);
            Caption(entering ? "[옷자락 스침 · 캐비닛 안]" : "[옷자락 스침 · 캐비닛 밖]", 1.8f);
        }
        public void PlayDiscovery()
        {
            if (!initialized) return;
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
                float interval = player.Running ? 1.6f : 1.15f;
                if (stride >= interval)
                {
                    stride %= interval;
                    int strideIndex = stepIndex++;
                    var contact = transform.position + transform.right * (strideIndex % 2 == 0 ? .11f : -.11f);
                    bool wet = false;
                    if (Physics.Raycast(contact + Vector3.up * .15f, Vector3.down, out var floor,
                        .6f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                    {
                        string surface = floor.collider.name.ToLowerInvariant();
                        wet = surface.Contains("water") || surface.Contains("wet") || WaterSurfaceFeedback.IsSubmerged(contact);
                    }
                    steps.pitch = 1 + ((strideIndex % 5) - 2) * .025f;
                    steps.PlayOneShot(wet ? wetStep : dryStep, player.Running ? .95f : .58f);
                    FootstepsPlayed++;
                    Footstep?.Invoke(contact, wet, player.Running ? 1 : .55f);
                }
            }
            else stride = Mathf.MoveTowards(stride, 0, Time.deltaTime * 2);
        }
        void LateUpdate()
        {
            if (!initialized || !player.eyes) return;
            var session = GameSession.Current;
            if (!session || !session.Shell) return;
            bool motion = session.InputAllowed && !player.Hidden && !session.Shell.ReducedMotion;
            float speed = motion && player.Grounded ? Mathf.Clamp(player.ActualSpeed, 0, player.runSpeed) : 0;
            phase += speed * Time.deltaTime * 4.5f;
            // Position only: mouse rotation and scripted camera direction remain authoritative.
            var bob = speed > .12f ? new Vector3(Mathf.Sin(phase * .5f) * .009f,
                Mathf.Abs(Mathf.Sin(phase)) * .015f, 0) : Vector3.zero;
            player.eyes.transform.localPosition = Vector3.Lerp(player.eyes.transform.localPosition,
                cameraHome + bob, 1 - Mathf.Exp(-14 * Time.unscaledDeltaTime));
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
            foreach (var clip in new[] { dryStep, wetStep, click, rustle, discovery, breath }) if (clip) Destroy(clip);
        }
    }
}
