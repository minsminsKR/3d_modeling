using UnityEngine;

namespace HappyToy.V2
{
    // The authored morph clip has no locomotion root motion. Movement and the
    // sampled mesh share one simulation clock, including while the game is paused.
    internal sealed class AlleyBeastRetreat
    {
        static readonly float[] GroundOffsets = { 0, .0319f, .0605f, .0701f, .0560f, .0434f,
            .0568f, .0696f, .0759f, .0751f, .0157f, 0 };
        readonly Transform root;
        readonly Animation animation;
        readonly Vector3 origin;
        readonly Quaternion facingPlayer;
        float elapsed;

        public AlleyBeastRetreat(Transform root, Animation animation)
        {
            this.root = root;
            this.animation = animation;
            origin = root.position;
            // The authoring mesh points along +Z; begin facing the approaching player.
            root.rotation *= Quaternion.Euler(0, 180, 0);
            facingPlayer = root.rotation;
            animation.Stop();
            animation.playAutomatically = false;
            animation.clip.SampleAnimation(animation.gameObject, 0);
        }

        public bool Advance(float deltaTime)
        {
            elapsed += Mathf.Max(0, deltaTime);
            float turn = Mathf.SmoothStep(0, 1, Mathf.Clamp01(elapsed / .3f));
            root.rotation = Quaternion.Slerp(facingPlayer, Quaternion.Euler(0, 180, 0) * facingPlayer, turn);
            float distance = Mathf.Min(4f, 1.2f * Mathf.Max(0, elapsed - .3f));
            float clipTime = elapsed % animation.clip.length;
            animation.clip.SampleAnimation(animation.gameObject, clipTime);
            float phase = clipTime / animation.clip.length * (GroundOffsets.Length - 1);
            int index = Mathf.Min(GroundOffsets.Length - 2, Mathf.FloorToInt(phase));
            float offset = Mathf.Lerp(GroundOffsets[index], GroundOffsets[index + 1], phase - index);
            // This is a whole-mesh floor approximation, not individual paw IK.
            root.position = origin + Vector3.forward * distance - Vector3.up * offset;
            return distance >= 4f;
        }
    }
}
