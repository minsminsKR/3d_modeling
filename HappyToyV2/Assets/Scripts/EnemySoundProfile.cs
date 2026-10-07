using UnityEngine;

namespace HappyToy.V2
{
    public enum EnemySoundKind { Generic, Cyclopse, Hwacat, Uncat, Baby, Lantern, Wraith }

    // Recorded shoe contacts for walking pursuers; floating/attack signatures
    // remain separate. Sound identity never changes the AI.
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
        public static float Pitch(EnemySoundKind kind, int contacts)
        {
            float basePitch = kind == EnemySoundKind.Cyclopse ? .96f : kind == EnemySoundKind.Hwacat ? 1.03f :
                kind == EnemySoundKind.Baby ? 1.10f : 1;
            return basePitch * (contacts % 2 == 0 ? .985f : 1.015f);
        }
        public static AudioClip Create(EnemySoundKind kind, bool attack) => Create(kind, attack, 0);
        public static AudioClip Create(EnemySoundKind kind, bool attack, int variant)
        {
            if (kind == EnemySoundKind.Generic)
                return ExternalAudio.Required(attack ? "frame-impact" : "step-wood", variant);
            return ExternalAudio.Required("enemy-" + kind.ToString().ToLowerInvariant() +
                (attack ? "-attack" : "-movement"), variant);
        }
    }
}
