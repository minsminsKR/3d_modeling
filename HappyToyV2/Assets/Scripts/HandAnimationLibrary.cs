using System;
using System.Collections.Generic;
using UnityEngine;

namespace HappyToy.V2
{
    // Blender Actions are sampled once at authoring time. Runtime only blends
    // their bounded channels; it does not invent a new grasp every frame.
    public sealed class HandAnimationLibrary
    {
        [Serializable] public sealed class Bone
        {
            public string name, parent;
            public float curlMin, curlMax, spreadMin, spreadMax, twistMin, twistMax;
        }
        [Serializable] public sealed class Channel
        { public string name; public float curl, spread, twist; }
        [Serializable] public sealed class Pose
        {
            public string name, item, hand;
            public Channel[] bones;
            public Vector3 handleOffset;
            public float handleRadius;
        }
        [Serializable] public sealed class Frame
        { public float time; public Vector3 position, rotationEuler; public Channel[] bones; }
        [Serializable] public sealed class Clip
        {
            public string name, item, hand;
            public float duration, releaseTime;
            public Frame[] frames;
            public void Sample(float time, out Frame before, out Frame after, out float blend)
            {
                time = Mathf.Clamp(time, 0, duration);
                int low = 0, high = frames.Length - 1;
                while (high - low > 1)
                { int middle = (low + high) / 2; if (frames[middle].time <= time) low = middle; else high = middle; }
                before = frames[low]; after = frames[high];
                blend = Mathf.InverseLerp(before.time, after.time, time);
            }
            public Vector3 Position(float time)
            { Sample(time, out var a, out var b, out var t); return Vector3.LerpUnclamped(a.position, b.position, t); }
            public Quaternion Rotation(float time)
            { Sample(time, out var a, out var b, out var t); return Quaternion.SlerpUnclamped(Quaternion.Euler(a.rotationEuler), Quaternion.Euler(b.rotationEuler), t); }
        }
        [Serializable] sealed class Document
        { public int version; public string sourceSha256, sourceBlendSha256; public Bone[] bones; public Pose[] poses; public Clip[] clips; }
        readonly Dictionary<string, Pose> poses = new Dictionary<string, Pose>();
        readonly Dictionary<string, Clip> clips = new Dictionary<string, Clip>();
        public Bone[] Bones { get; }
        static HandAnimationLibrary cached;
        public static HandAnimationLibrary Load()
        {
            if (cached != null) return cached;
            var source = Resources.Load<TextAsset>("PlayerHandAnimation/poses");
            if (!source) throw new InvalidOperationException("Missing authored hand Actions and grip data");
            return cached = new HandAnimationLibrary(source.text);
        }
        public HandAnimationLibrary(string json)
        {
            var data = JsonUtility.FromJson<Document>(json);
            if (data == null || data.version != 1 || data.bones == null || data.bones.Length < 18 ||
                string.IsNullOrEmpty(data.sourceBlendSha256)) throw new InvalidOperationException("Invalid hand animation source");
            Bones = data.bones;
            var names = new HashSet<string>();
            foreach (var bone in Bones)
                if (string.IsNullOrEmpty(bone.name) || !names.Add(bone.name) ||
                    !Range(bone.curlMin, bone.curlMax) || !Range(bone.spreadMin, bone.spreadMax) || !Range(bone.twistMin, bone.twistMax))
                    throw new InvalidOperationException("Invalid hand bone limits");
            foreach (var pose in data.poses)
            { Validate(pose.bones); poses.Add(pose.name, pose); }
            foreach (var clip in data.clips)
            {
                if (!Finite(clip.duration) || clip.duration <= 0 || clip.frames == null || clip.frames.Length < 2)
                    throw new InvalidOperationException("Invalid hand clip: " + clip.name);
                float prior = -1;
                foreach (var frame in clip.frames)
                {
                    if (!Finite(frame.time) || frame.time <= prior || frame.time < 0 || frame.time > clip.duration + .00001f ||
                        !Vector(frame.position) || !Vector(frame.rotationEuler)) throw new InvalidOperationException("Invalid authored hand frame");
                    Validate(frame.bones); prior = frame.time;
                }
                if (clip.frames[0].time != 0 || Mathf.Abs(prior - clip.duration) > .00001f)
                    throw new InvalidOperationException("Incomplete hand clip: " + clip.name);
                clips.Add(clip.name, clip);
            }
            foreach (PlayerItem item in Enum.GetValues(typeof(PlayerItem)))
            {
                GetPose(item.ToString());
                var use = GetClip("Use" + item);
                if (use.releaseTime <= 0 || use.releaseTime >= use.duration) throw new InvalidOperationException("Missing release marker: " + item);
                GetClip("Equip" + item); GetClip("Unequip" + item);
            }
            GetPose("Open"); GetPose("Relaxed");
        }
        void Validate(Channel[] values)
        {
            if (values == null || values.Length != Bones.Length) throw new InvalidOperationException("Hand channel count differs from its rig");
            for (int i = 0; i < values.Length; i++)
                if (values[i].name != Bones[i].name || !Finite(values[i].curl) || !Finite(values[i].spread) || !Finite(values[i].twist))
                    throw new InvalidOperationException("Invalid or reordered authored hand channel");
        }
        public Pose GetPose(string name) => poses.TryGetValue(name, out var value) ? value : throw new InvalidOperationException("Missing hand pose: " + name);
        public Clip GetClip(string name) => clips.TryGetValue(name, out var value) ? value : throw new InvalidOperationException("Missing hand clip: " + name);
        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        static bool Range(float a, float b) => Finite(a) && Finite(b) && a <= b && a >= -180 && b <= 180;
        static bool Vector(Vector3 value) => Finite(value.x) && Finite(value.y) && Finite(value.z);
    }
}
