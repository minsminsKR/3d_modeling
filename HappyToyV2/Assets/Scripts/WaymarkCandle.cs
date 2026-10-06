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
        Vector3 flameScale, flamePosition;
        Quaternion flameRotation;
        float phase, flutter;
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
            flame = flameVisual; localLight = light; flameScale = flame.localScale; flamePosition = flame.localPosition; flameRotation = flame.localRotation; phase = seed;
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
            GameSession.Current.Notify("촛불을 켰습니다. 적이 가까우면 떨리고, 아주 가까워지거나 들키면 잠시 꺼졌다가 위험이 사라지면 다시 켜집니다.");
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
            if (localLight) { localLight.enabled = lit; localLight.intensity = .68f; }
        }
        void Update()
        {
            if (!Lit || !GameSession.Current || !GameSession.Current.InputAllowed) return;
            bool softened = GameSession.Current.Shell && GameSession.Current.Shell.ReducedMotion;
            // Both frequency and depth grow continuously with danger. Comfort mode retains
            // the warning as steady dimming rather than flashing or bending the flame.
            float frequency = Mathf.Lerp(6.2f, 22f, Danger);
            // Integrate phase: changing distance cannot turn a long playtime into a rapid flash.
            flutter = Mathf.Repeat(flutter + Time.deltaTime * frequency, Mathf.PI * 2);
            float wave = softened ? 0 : Mathf.Sin(flutter + phase) * .05f +
                Mathf.Sin(Time.time * 2.7f + phase) * .035f;
            float pulse = softened ? 0 : Mathf.Pow(.5f + .5f * Mathf.Sin(flutter + phase), 2);
            float brightness = softened ? 1 - .75f * Danger :
                1 - Danger * (.18f + .78f * pulse);
            localLight.intensity = Mathf.Max(.01f, (.68f + wave) * brightness);
            flame.localScale = Vector3.Scale(flameScale,
                new Vector3((1 - wave) * Mathf.Lerp(1, .65f, 1 - brightness),
                    (1 + wave) * Mathf.Lerp(.35f, 1, brightness), 1 - wave));
            flame.localPosition = flamePosition + new Vector3(wave * .008f, 0, wave * .003f);
            flame.localRotation = flameRotation * Quaternion.Euler(0, 0, wave * (12 + Danger * 70));
        }
        void OnDisable() { if (ignition) ignition.Stop(); }
        void OnDestroy() { if (clip) Destroy(clip); }
    }
}
