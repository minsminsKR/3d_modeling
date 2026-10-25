using System;
using System.Collections.Generic;
using UnityEngine;

namespace HappyToy.V2
{
    // Applies sampled authored channels to the actual imported bind rotations.
    // Blender rest quaternions are provenance, never mistaken for FBX bind axes.
    public sealed class HandAnimationRig
    {
        readonly HandAnimationLibrary library;
        readonly Transform[] bones;
        readonly Quaternion[] rest, saved;
        readonly Vector3[] authored, current, savedAuthored, savedCurrent;
        readonly Dictionary<string, Transform> markers = new Dictionary<string, Transform>();
        readonly Dictionary<string, int[]> digitBones = new Dictionary<string, int[]>();
        readonly Quaternion bindPalmInRoot;
        bool applied;
        public Transform Root { get; }
        public HandAnimationRig(Transform root, HandAnimationLibrary source)
        {
            Root = root; library = source;
            foreach (var node in root.GetComponentsInChildren<Transform>(true)) markers[node.name] = node;
            bones = new Transform[source.Bones.Length]; rest = new Quaternion[bones.Length]; saved = new Quaternion[bones.Length];
            authored = new Vector3[bones.Length]; current = new Vector3[bones.Length];
            savedAuthored = new Vector3[bones.Length]; savedCurrent = new Vector3[bones.Length];
            for (int i = 0; i < bones.Length; i++)
            {
                if (!markers.TryGetValue(source.Bones[i].name, out bones[i]))
                    throw new InvalidOperationException("Hand rig has no authored bone: " + source.Bones[i].name);
                rest[i] = bones[i].localRotation;
            }
            foreach (string digit in new[] { "thumb", "index", "middle", "ring", "little" })
            {
                var chain = new int[3];
                for (int joint = 1; joint <= 3; joint++) chain[joint - 1] = Index(digit + "_" + joint);
                digitBones.Add(digit, chain);
            }
            bindPalmInRoot = Quaternion.Inverse(root.rotation) * markers["palm"].rotation;
        }
        public void Apply(HandAnimationLibrary.Pose pose, HandAnimationLibrary.Clip clip, float time)
        {
            HandAnimationLibrary.Frame a = null, b = null; float blend = 0;
            if (clip != null) clip.Sample(time, out a, out b, out blend);
            for (int i = 0; i < bones.Length; i++)
            {
                var start = clip == null ? pose.bones[i] : a.bones[i];
                var end = clip == null ? start : b.bones[i];
                var limit = library.Bones[i];
                float curl = Mathf.Clamp(Mathf.LerpUnclamped(start.curl, end.curl, blend), limit.curlMin, limit.curlMax);
                float spread = Mathf.Clamp(Mathf.LerpUnclamped(start.spread, end.spread, blend), limit.spreadMin, limit.spreadMax);
                float twist = Mathf.Clamp(Mathf.LerpUnclamped(start.twist, end.twist, blend), limit.twistMin, limit.twistMax);
                authored[i] = current[i] = new Vector3(curl, spread, twist);
                ApplyBone(i);
            }
            applied = true;
        }
        public Vector3 Marker(string name)
        { if (!markers.TryGetValue(name, out var value)) throw new InvalidOperationException("Missing hand contact: " + name); return value.position; }
        public Vector3 Tip(string digit) => Marker(digit + "_tip");
        public Vector3 Palm => Marker("palm_contact");
        public Quaternion PalmRotationDelta => markers["palm"].rotation * Quaternion.Inverse(Root.rotation * bindPalmInRoot);
        public Vector3 Pinch => (Tip("thumb") + Tip("index")) * .5f;
        public void SavePose()
        {
            for (int i = 0; i < bones.Length; i++)
            { saved[i] = bones[i].localRotation; savedAuthored[i] = authored[i]; savedCurrent[i] = current[i]; }
        }
        public void RestorePose()
        {
            for (int i = 0; i < bones.Length; i++)
            { bones[i].localRotation = saved[i]; authored[i] = savedAuthored[i]; current[i] = savedCurrent[i]; }
        }
        void ApplyBone(int index)
        {
            Vector3 value = current[index];
            bones[index].localRotation = rest[index] * Quaternion.AngleAxis(value.y, Vector3.forward) *
                Quaternion.AngleAxis(value.z, Vector3.up) * Quaternion.AngleAxis(value.x, Vector3.right);
        }
        // A small contact finish on an authored grip, never a replacement pose.
        // Numerical hinge CCD uses the actual transforms, including a mirrored
        // left-hand hierarchy, instead of guessing reflection-sensitive axes.
        public float CorrectTip(string digit, Vector3 target, float maxDegrees = 6)
        {
            if (!markers.TryGetValue(digit + "_tip", out var tip))
                throw new InvalidOperationException("Missing hand fingertip: " + digit);
            if (!applied || !Finite(target) || float.IsNaN(maxDegrees) || float.IsInfinity(maxDegrees))
                return Vector3.Distance(tip.position, target);
            maxDegrees = Mathf.Clamp(maxDegrees, 0, 6);
            if (maxDegrees <= 0 || !digitBones.TryGetValue(digit, out var chain)) return Vector3.Distance(tip.position, target);
            for (int pass = 0; pass < 3; pass++)
            {
                float step = maxDegrees * (2f / 3) / (1 << pass);
                for (int joint = 3; joint >= 1; joint--)
                {
                    int index = chain[joint - 1];
                    if (index < 0) continue;
                    AdjustHinge(index, false, target, tip, step, maxDegrees);
                    if (digit == "thumb" && joint == 1) AdjustHinge(index, true, target, tip, step, maxDegrees);
                }
                if ((tip.position - target).sqrMagnitude < .00000025f) break;
            }
            return Vector3.Distance(tip.position, target);
        }
        void AdjustHinge(int index, bool spread, Vector3 target, Transform tip, float step, float maximum)
        {
            var limit = library.Bones[index];
            float origin = spread ? current[index].y : current[index].x;
            float baseline = spread ? authored[index].y : authored[index].x;
            float minimum = Mathf.Max(spread ? limit.spreadMin : limit.curlMin, baseline - maximum);
            float max = Mathf.Min(spread ? limit.spreadMax : limit.curlMax, baseline + maximum);
            float best = origin, cost = (tip.position - target).sqrMagnitude;
            for (int sign = -1; sign <= 1; sign += 2)
            {
                float candidate = Mathf.Clamp(origin + sign * step, minimum, max);
                var value = current[index];
                if (spread) value.y = candidate; else value.x = candidate;
                current[index] = value; ApplyBone(index);
                float error = (tip.position - target).sqrMagnitude;
                if (error < cost) { cost = error; best = candidate; }
            }
            var selected = current[index];
            if (spread) selected.y = best; else selected.x = best;
            current[index] = selected; ApplyBone(index);
        }
        int Index(string name)
        { for (int i = 0; i < library.Bones.Length; i++) if (library.Bones[i].name == name) return i; return -1; }
        public float CorrectionDegrees(string digit)
        {
            float largest = 0;
            if (!digitBones.TryGetValue(digit, out var chain)) return 0;
            for (int joint = 0; joint < chain.Length; joint++)
            {
                int i = chain[joint];
                if (i >= 0)
                {
                    var delta = current[i] - authored[i];
                    largest = Mathf.Max(largest, Mathf.Max(Mathf.Abs(delta.x), Mathf.Max(Mathf.Abs(delta.y), Mathf.Abs(delta.z))));
                }
            }
            return largest;
        }
        static bool Finite(Vector3 value) => !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
            !float.IsNaN(value.y) && !float.IsInfinity(value.y) && !float.IsNaN(value.z) && !float.IsInfinity(value.z);
#if UNITY_EDITOR
        public void PreviewCurl(float degrees)
        {
            for (int i = 0; i < bones.Length; i++)
                if (library.Bones[i].name.Contains("_"))
                {
                    authored[i] = current[i] = new Vector3(Mathf.Clamp(degrees,
                        library.Bones[i].curlMin, library.Bones[i].curlMax), 0, 0);
                    ApplyBone(i);
                }
        }
#endif
    }
}
