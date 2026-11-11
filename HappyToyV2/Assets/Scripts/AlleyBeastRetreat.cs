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
        readonly Vector3 destination;
        float elapsed;

        public AlleyBeastRetreat(Transform root, Animation animation, Vector3 destination)
        {
            this.root = root;
            this.animation = animation;
            this.destination = destination;
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
            Vector3 displacement = destination - origin; displacement.y = 0;
            var faceForward = Quaternion.Euler(0, 180, 0) * facingPlayer;
            var faceEscape = Quaternion.FromToRotation(Vector3.forward, displacement.normalized) * faceForward;
            root.rotation = Quaternion.Slerp(facingPlayer, faceEscape, turn);
            float distance = Mathf.Min(displacement.magnitude, 1.2f * Mathf.Max(0, elapsed - .3f));
            float clipTime = elapsed % animation.clip.length;
            animation.clip.SampleAnimation(animation.gameObject, clipTime);
            float phase = clipTime / animation.clip.length * (GroundOffsets.Length - 1);
            int index = Mathf.Min(GroundOffsets.Length - 2, Mathf.FloorToInt(phase));
            float offset = Mathf.Lerp(GroundOffsets[index], GroundOffsets[index + 1], phase - index);
            // This is a whole-mesh floor approximation, not individual paw IK.
            root.position = origin + (destination - origin).normalized * distance - Vector3.up * offset;
            return distance >= Vector3.Distance(origin, destination);
        }
    }
}
