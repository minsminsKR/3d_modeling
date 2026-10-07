using UnityEngine;

namespace HappyToy.V2
{
    // Shares the candles' already-perceived danger; never samples hidden enemies itself.
    // Applies after the danger sampler and before FixtureLightLink's bounce/diffuser pass.
    [DisallowMultipleComponent, DefaultExecutionOrder(120)]
    public sealed class ThreatFixtureFlicker : MonoBehaviour
    {
        LightExplorationRun owner;
        Light source;
        Renderer[] shades;
        Color[] emissions;
        MaterialPropertyBlock block;
        float baseline, lastApplied, phase, clock, cycle;
        bool applied;
        static readonly int Emission = Shader.PropertyToID("_EmissionColor");
        public Light Source => source;
        public float Brightness { get; private set; } = 1;

        public void Configure(LightExplorationRun lighting, Light light, Renderer[] visuals, float seed)
        {
            Restore();
            owner = lighting; source = light; phase = seed;
            baseline = light ? light.intensity : 0; lastApplied = baseline;
            shades = visuals ?? new Renderer[0]; emissions = new Color[shades.Length];
            block = new MaterialPropertyBlock();
            for (int i = 0; i < shades.Length; i++)
            {
                var material = shades[i] ? shades[i].sharedMaterial : null;
                emissions[i] = material && material.HasProperty(Emission) ? material.GetColor(Emission) : Color.black;
            }
        }

        void LateUpdate()
        {
            if (!source) return;
            // A story/atmosphere owner can change the baseline. Never feed our own
            // dimmed result back into it or enable a deliberately switched-off lamp.
            if (!applied || !Mathf.Approximately(source.intensity, lastApplied)) baseline = source.intensity;
            var session = GameSession.Current;
            if (!owner || !owner.isActiveAndEnabled || !session || !session.player || session.Finished ||
                !(session.CorridorMode ? session.Corridor.Lighting == owner : session.ChapterMode && session.Chapter.Lighting == owner))
            { Restore(); return; }
            bool softened = session.Shell && session.Shell.ReducedMotion;
            // Local ceiling pools react with the player's candle warning. Distant
            // fixtures on another storey retain their own light and story state.
            bool local = Mathf.Abs(session.player.transform.position.y - (source.transform.position.y - 2.4f)) < 2 &&
                Vector3.Distance(session.player.transform.position, source.transform.position) < 20;
            float danger = local ? owner.Danger : 0;
            if (session.InputAllowed)
            {
                clock += Time.deltaTime;
                float gust = Mathf.PerlinNoise(phase + 4.1f, clock * .7f);
                cycle = Mathf.Repeat(cycle + Time.deltaTime * Mathf.Lerp(.65f, 2.1f, danger) * Mathf.Lerp(.88f, 1.12f, gust), 1);
                float pulse = Mathf.Repeat(cycle + phase * .159155f, 1);
                float dip = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.03f, .10f, pulse)) *
                    (1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.34f, .43f, pulse)));
                Brightness = softened ? 1 - .65f * danger : 1 - .96f * Mathf.Pow(danger, .35f) * dip;
                if (local && owner.Blackout) Brightness = softened ? .24f : .04f;
            }
            else if (softened) Brightness = local && owner.Blackout ? .24f : 1 - .65f * danger;
            Apply(Brightness);
        }

        void Apply(float brightness)
        {
            lastApplied = baseline * brightness; source.intensity = lastApplied; applied = true;
            for (int i = 0; i < shades.Length; i++)
                if (shades[i] && emissions[i].maxColorComponent > 0)
                {
                    shades[i].GetPropertyBlock(block);
                    block.SetColor(Emission, emissions[i] * (source.enabled ? brightness : 0));
                    shades[i].SetPropertyBlock(block);
                }
        }

        void Restore()
        {
            if (!source || !applied) return;
            if (Mathf.Approximately(source.intensity, lastApplied)) source.intensity = baseline;
            Brightness = 1; applied = false;
            for (int i = 0; shades != null && i < shades.Length; i++) if (shades[i])
            {
                shades[i].GetPropertyBlock(block); block.SetColor(Emission, emissions[i]); shades[i].SetPropertyBlock(block);
            }
        }
        void OnDisable() => Restore();
        void OnDestroy() => Restore();
    }
}
