using System;
using UnityEngine;

namespace HappyToy.V2
{
    // Editable additive gait over the creature's own imported clips and weights.
    // Restore before sampling, so changing state/pause never accumulates rotation.
    internal sealed class EnemyLocomotionPose
    {
        readonly Transform[] joints = new Transform[8];
        readonly Quaternion[] before = new Quaternion[8];
        readonly bool quadruped;
        bool applied;
        float phase, chaseBlend;
        public float SwingDegrees { get; private set; }
        public bool Available { get; }
        public EnemyLocomotionPose(Transform model, EnemySoundKind kind)
        {
            quadruped = kind == EnemySoundKind.Uncat;
            if (!quadruped && kind != EnemySoundKind.Cyclopse) return;
            var bones = model.GetComponentsInChildren<Transform>(true);
            string[] names = { "LeftArm", "RightArm", "LeftUpLeg", "RightUpLeg",
                "LeftForeArm", "RightForeArm", "LeftLeg", "RightLeg" };
            Available = true;
            for (int i = 0; i < names.Length; i++)
            {
                string name = names[i];
                joints[i] = Array.Find(bones, bone => bone.name == name || bone.name.EndsWith(":" + name, StringComparison.Ordinal));
                if (!joints[i]) Available = false;
            }
        }
        public void Reset()
        {
            if (!applied) return;
            for (int i = 0; i < joints.Length; i++) if (joints[i]) joints[i].localRotation = before[i];
            applied = false;
        }
        public void Apply(Transform actor, float speed, bool chasing, float seconds, bool blocked)
        {
            SwingDegrees = 0;
            if (!Available || blocked || speed < .06f) return;
            chaseBlend = Mathf.MoveTowards(chaseBlend, chasing ? 1 : 0, seconds * 5);
            phase = Mathf.Repeat(phase + seconds * speed * (quadruped ? 4.8f : 4.2f), Mathf.PI * 2);
            float wave = Mathf.Sin(phase), opposite = -wave;
            float amplitude = Mathf.Lerp(quadruped ? 24 : 28, quadruped ? 58 : 72, chaseBlend);
            float stride = Mathf.Lerp(18, quadruped ? 48 : 42, chaseBlend);
            SwingDegrees = amplitude;
            for (int i = 0; i < joints.Length; i++) before[i] = joints[i].localRotation;
            applied = true;
            void Pitch(int index, float angle) => joints[index].rotation = Quaternion.AngleAxis(angle, actor.right) * joints[index].rotation;
            // Diagonal pairs for Uncat's four-legged gait; opposite arm/leg pairs for Cyclopse.
            Pitch(0, wave * amplitude); Pitch(1, opposite * amplitude);
            Pitch(2, opposite * stride); Pitch(3, wave * stride);
            float bend = Mathf.Lerp(12, 32, chaseBlend);
            Pitch(4, Mathf.Max(0, -wave) * bend); Pitch(5, Mathf.Max(0, wave) * bend);
            Pitch(6, Mathf.Max(0, wave) * bend); Pitch(7, Mathf.Max(0, -wave) * bend);
        }
    }
}
