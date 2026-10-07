using UnityEngine;

namespace HappyToy.V2
{
    // A route marker, not a sanctuary: enemy sight, hearing and navigation are unchanged.
    [DisallowMultipleComponent]
    public sealed class WaymarkCandle : MonoBehaviour
    {
        Transform flame;
        Light localLight;
        AudioSource ignition;
        AudioClip clip;
        Renderer[] flameRenderers;
        MaterialPropertyBlock flameProperties;
        float[] flameEmission, flameOpacity;
        static readonly int EmissionId = Shader.PropertyToID("_Emission"), OpacityId = Shader.PropertyToID("_Opacity");
        Vector3 flameScale, flamePosition;
        Quaternion flameRotation;
        float phase, flutter, turbulenceClock, lightRange;
        LightExplorationRun dangerOwner;
        public float Danger { get; private set; }
        public bool IgnitionBlocked { get; private set; }
        public bool Lit { get; private set; }
        public bool HasBeenLit { get; private set; }
        public int Ignitions { get; private set; }
        public Light LocalLight => localLight;
        public Transform Flame => flame;
        public AudioSource IgnitionSource => ignition;
        public string DisplayLabel => IgnitionBlocked ? (HasBeenLit ? "기척에 잠시 꺼진 촛불" : "위험 · 촛불을 켤 수 없습니다") :
            Lit ? (Danger > 0 ? "떨리는 촛불 · 근처에 적이 있습니다" : "켜진 촛불 · 지나온 길") : "촛불 켜기";

        public void BindDanger(LightExplorationRun owner) => dangerOwner = owner;
        public void ApplyDanger(float danger, bool blackout)
        {
            Danger = Mathf.Clamp01(danger); IgnitionBlocked = blackout;
            bool visible = HasBeenLit && !blackout;
            if (Lit == visible) return;
            if (!visible && ignition) ignition.Stop();
            Apply(visible);
        }

        public void Configure(Transform flameVisual, Light light, float seed)
        {
            if (flame) throw new System.InvalidOperationException("Candle already configured");
            flame = flameVisual; localLight = light; lightRange = light.range;
            flameScale = flame.localScale; flamePosition = flame.localPosition; flameRotation = flame.localRotation; phase = seed;
            flameRenderers = flame.GetComponentsInChildren<Renderer>(true);
            flameProperties = new MaterialPropertyBlock();
            flameEmission = new float[flameRenderers.Length]; flameOpacity = new float[flameRenderers.Length];
            for (int i = 0; i < flameRenderers.Length; i++)
            {
                var material = flameRenderers[i].sharedMaterial;
                flameEmission[i] = material && material.HasProperty(EmissionId) ? material.GetFloat(EmissionId) : 0;
                flameOpacity[i] = material && material.HasProperty(OpacityId) ? material.GetFloat(OpacityId) : 0;
            }
            ignition = gameObject.AddComponent<AudioSource>(); ignition.playOnAwake = false;
            ignition.spatialBlend = 1; ignition.volume = .3f; ignition.dopplerLevel = 0;
            ignition.minDistance = 1; ignition.maxDistance = 7; ignition.rolloffMode = AudioRolloffMode.Linear;
            ignition.ignoreListenerPause = false; ignition.ignoreListenerVolume = false;
            // Original short match strike/fwoosh, generated here. No external asset license dependency.
            const int rate = 24000; var samples = new float[rate / 3];
            var noise = new System.Random(7133); float filtered = 0;
            for (int i = 0; i < samples.Length; i++)
            {
                float t = i / (float)rate;
                filtered = Mathf.Lerp(filtered, (float)noise.NextDouble() * 2 - 1, .38f);
                float attack = Mathf.Clamp01(t / .012f), tail = Mathf.Clamp01((1f / 3 - t) / .07f);
                float scrape = filtered * Mathf.Exp(-12 * t) * .20f;
                float catchFlame = Mathf.Sin(t * Mathf.PI * 230) * Mathf.Exp(-15 * t) * .045f;
                samples[i] = (scrape + catchFlame) * attack * tail;
            }
            clip = AudioClip.Create("Original candle match strike", samples.Length, 1, rate, false);
            clip.SetData(samples, 0); ignition.clip = clip; Apply(false);
        }
        public bool TryIgnite(PlayerMotor player)
        {
            if (HasBeenLit || !isActiveAndEnabled || !player || player.Hidden || !GameSession.Current ||
                !GameSession.Current.InputAllowed || !flame || !localLight) return false;
            if (dangerOwner) dangerOwner.RefreshDanger();
            if (IgnitionBlocked) return false;
            HasBeenLit = true; Apply(true); Ignitions++;
            ignition.Play();
            GameSession.Current.Shell.ShowCaption("[치익 · 촛불 점화]", 1.2f);
            GameSession.Current.Notify("촛불을 켰습니다. 적이 가까워질수록 불꽃이 심하게 꺼질 듯 떨리고, 바로 곁에 오면 잠시 꺼졌다가 거리가 벌어지면 다시 켜집니다.");
            return true;
        }
        // Restore the player's durable ignition, not the transient danger blackout.
        // Automatic recovery is silent and never counts as another manual ignition.
        public void Restore(bool lit)
        {
            if (ignition) ignition.Stop();
            HasBeenLit = lit; Apply(lit && !IgnitionBlocked);
        }
        void Apply(bool lit)
        {
            Lit = lit;
            if (flame)
            {
                flame.localScale = flameScale; flame.localPosition = flamePosition; flame.localRotation = flameRotation;
                flame.gameObject.SetActive(lit);
            }
            if (localLight) { localLight.enabled = lit; localLight.intensity = .68f; localLight.range = lightRange; }
            ApplyFlameRadiance(1);
        }
        void Update()
        {
            if (!Lit || !GameSession.Current || !GameSession.Current.InputAllowed) return;
            bool softened = GameSession.Current.Shell && GameSession.Current.Shell.ReducedMotion;
            // Hold the dying flame long enough to read it at walking speed, then let
            // it catch strongly. A fast sine wave averaged into an ordinary small
            // light in the real corridor, especially beside the player's flashlight.
            // Integrated clocks retain the phase across approach and pause.
            turbulenceClock += Time.deltaTime;
            float gust = Mathf.PerlinNoise(phase * .21f + 17.6f, turbulenceClock * .73f);
            float frequency = Mathf.Lerp(.62f, 2.05f, Danger) * Mathf.Lerp(.86f, 1.14f, gust);
            flutter = Mathf.Repeat(flutter + Time.deltaTime * frequency, 1);
            float cycle = Mathf.Repeat(flutter + phase * .159155f, 1);
            // At close range this plateau lasts about 0.12 seconds rather than a
            // single dark frame; the following flare lights a larger patch of wall.
            float choke = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.025f, .10f, cycle)) *
                (1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.35f, .42f, cycle)));
            float catchFlame = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.35f, .45f, cycle)) *
                (1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.45f, .66f, cycle)));
            float depth = .998f * Mathf.Pow(Danger, .35f);
            float surge = catchFlame * 3 * Mathf.Pow(Danger, 1.15f);
            float wave = softened ? 0 : Mathf.Sin(turbulenceClock * 8.2f + phase) * (.025f + .13f * Danger);
            // Comfort mode keeps the same proximity warning as steady dimming.
            float brightness = softened ? 1 - .78f * Danger :
                (1 - depth * choke) * (1 - .12f * Danger) + surge;
            localLight.intensity = Mathf.Clamp((.68f + wave) * brightness, .001f, 3);
            localLight.range = Mathf.Min(Mathf.Max(lightRange, 5), lightRange *
                (softened ? Mathf.Lerp(1, .65f, Danger) :
                    Mathf.Lerp(.22f, 1, Mathf.Clamp01(brightness)) + surge * .30f));
            float flameBody = Mathf.Clamp01(brightness);
            flame.localScale = Vector3.Scale(flameScale,
                new Vector3((1 - wave) * Mathf.Lerp(.04f, 1, flameBody) + (softened ? 0 : surge * .27f),
                    (1 + wave) * Mathf.Lerp(.025f, 1, flameBody) + (softened ? 0 : surge * .40f),
                    Mathf.Lerp(.35f, 1, flameBody)));
            flame.localPosition = flamePosition + new Vector3(wave * (.019f + .06f * Danger), 0, wave * .018f);
            flame.localRotation = flameRotation * Quaternion.Euler(wave * Danger * 55, 0, wave * (12 + Danger * 150));
            ApplyFlameRadiance(brightness);
        }
        void ApplyFlameRadiance(float brightness)
        {
            if (flameRenderers == null) return;
            // The photographic flame material is shared. Property blocks make each
            // flame actually fade with its light without modifying other candles.
            for (int i = 0; i < flameRenderers.Length; i++)
            {
                var renderer = flameRenderers[i]; if (!renderer) continue;
                renderer.GetPropertyBlock(flameProperties);
                if (flameEmission[i] > 0) flameProperties.SetFloat(EmissionId, flameEmission[i] * Mathf.Max(0, brightness));
                if (flameOpacity[i] > 0) flameProperties.SetFloat(OpacityId, flameOpacity[i] * Mathf.Clamp01(brightness));
                renderer.SetPropertyBlock(flameProperties);
            }
        }
        void OnDisable() { if (ignition) ignition.Stop(); }
        void OnDestroy() { if (clip) Destroy(clip); }
    }
}
