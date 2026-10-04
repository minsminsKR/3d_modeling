using UnityEngine;

namespace HappyToy.V2
{
    public enum EnemySoundKind { Generic, Cyclopse, Hwacat, Uncat, Baby, Lantern, Wraith }

    // Original deterministic textures. Identity changes timbre/cadence only, never AI.
    public static class EnemySoundProfile
    {
        public const int SampleRate = 24000;
        public const float MovementSeconds = .34f, AttackSeconds = .46f;
        public static EnemySoundKind Identify(Transform actor)
        {
            string label = actor.name.ToLowerInvariant();
            if (label.Contains("cyclopse")) return EnemySoundKind.Cyclopse;
            if (label.Contains("hwacat")) return EnemySoundKind.Hwacat;
            if (label.Contains("uncat")) return EnemySoundKind.Uncat;
            if (label.Contains("baby")) return EnemySoundKind.Baby;
            return EnemySoundKind.Generic;
        }
        public static float Stride(EnemySoundKind kind, int contacts)
        {
            switch (kind)
            {
                case EnemySoundKind.Cyclopse: return .95f;
                case EnemySoundKind.Hwacat: return .68f;
                case EnemySoundKind.Uncat: return contacts % 2 == 0 ? .68f : 1.02f;
                case EnemySoundKind.Baby: return .78f;
                case EnemySoundKind.Lantern: return 1.1f;
                case EnemySoundKind.Wraith: return .82f;
                default: return .85f;
            }
        }
        public static float Pitch(EnemySoundKind kind, int contacts) => contacts % 2 == 0 ? .97f : 1.025f;
        static float Contact(float t, float frequency, float decay, float noise, float grain)
        {
            if (t < 0) return 0;
            float envelope = Mathf.Clamp01(t / .004f) * Mathf.Exp(-decay * t);
            return envelope * (Mathf.Sin(2 * Mathf.PI * frequency * t) + noise * grain);
        }
        public static AudioClip Create(EnemySoundKind kind, bool attack)
        {
            float seconds = attack ? AttackSeconds : MovementSeconds;
            var samples = new float[Mathf.RoundToInt(seconds * SampleRate)];
            var random = new System.Random(114 + (int)kind * 977 + (attack ? 3701 : 0));
            float smooth = 0;
            for (int i = 0; i < samples.Length; i++)
            {
                float t = i / (float)SampleRate;
                float grain = (float)(random.NextDouble() * 2 - 1);
                smooth = Mathf.Lerp(smooth, grain, .18f);
                float value;
                switch (kind)
                {
                    case EnemySoundKind.Cyclopse:
                        value = .33f * Contact(t, 61, 18, .18f, smooth) + .085f * Contact(t - .075f, 106, 28, .65f, grain); break;
                    case EnemySoundKind.Hwacat:
                        value = .17f * Contact(t, 310, 48, .75f, grain) + .14f * Contact(t - .048f, 440, 55, .8f, grain) +
                            .11f * Contact(t - .095f, 570, 60, .7f, grain); break;
                    case EnemySoundKind.Uncat:
                        value = .25f * Contact(t, 127, 29, .18f, smooth) + .11f * Contact(t - .09f, 184, 40, .5f, smooth); break;
                    case EnemySoundKind.Baby:
                        value = .23f * Contact(t, 158, 33, .7f, smooth) + .20f * Contact(t - .125f, 113, 35, .65f, smooth); break;
                    case EnemySoundKind.Lantern:
                        value = .12f * Contact(t, 740, 15, .08f, grain) + .075f * Contact(t - .035f, 1187, 22, .13f, grain); break;
                    case EnemySoundKind.Wraith:
                        value = .20f * Contact(t, 83, 20, .5f, smooth) +
                            .18f * smooth * Mathf.Sin(Mathf.PI * t / seconds) * Mathf.Exp(-5 * t); break;
                    default: value = .28f * Contact(t, 85, 25, .35f, grain); break;
                }
                if (attack)
                {
                    // Separate breath/tension-to-impact shape, not a repitched walking clip.
                    float tension = Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / .16f));
                    float strike = t - .10f, frequency = 92 + (int)kind * 29;
                    value = .15f * smooth * tension + .28f * Contact(strike, frequency, 13, .5f, smooth) +
                        .075f * Contact(strike, frequency * 2.37f, 17, .2f, grain);
                }
                float edge = Mathf.Clamp01(t / .004f) * Mathf.Clamp01((seconds - t) / .025f);
                samples[i] = Mathf.Clamp(value * edge, -.44f, .44f);
            }
            var clip = AudioClip.Create(kind + (attack ? " attack anticipation" : " movement contact"), samples.Length, 1, SampleRate, false);
            clip.SetData(samples, 0); return clip;
        }
    }
}
