using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace HappyToy.V2.Editor
{
    // Explicit batch-only inspection of the actual weighted hand surface. This
    // tool does not modify a prefab, a scene, gameplay state, or authoring data.
    public static class HandGripSurfaceAudit
    {
        [Serializable] sealed class Result
        {
            public string scope, item;
            public float radius, minimum, maximum, maxPenetrationMetres, sumSquaredPenetration;
            public int sampledVertices, shaftVertices, penetratingVertices, deepPenetratingVertices;
        }
        [Serializable] sealed class Optimized
        {
            public string scope;
            public Result before, after;
            public HandAnimationLibrary.Channel[] bones;
        }
        static T Field<T>(object instance, string name) => (T)instance.GetType()
            .GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(instance);

        static Result Measure(FirstPersonHands hands)
        {
            hands.PrepareCpuPreview(PlayerItem.Flashlight);
            var hand = Field<Transform>(hands, "left");
            var torch = Field<Transform>(hands, "torch");
            var contact = Field<HandCylinderContact>(hands, "torchContact");
            var skin = hand.GetComponentInChildren<SkinnedMeshRenderer>(true);
            if (!skin || !skin.sharedMesh) throw new InvalidOperationException("Missing actual left-hand skin.");
            var mesh = skin.sharedMesh;
            var source = mesh.vertices;
            var weights = mesh.boneWeights;
            var bind = mesh.bindposes;
            var bones = skin.bones;
            if (source.Length != weights.Length || bind.Length != bones.Length)
                throw new InvalidOperationException("Hand skin topology is inconsistent.");
            var matrices = new Matrix4x4[bind.Length];
            for (int i = 0; i < bind.Length; i++) matrices[i] = torch.worldToLocalMatrix * bones[i].localToWorldMatrix * bind[i];
            var result = new Result { item = "Flashlight", radius = contact.Radius, minimum = contact.Minimum,
                maximum = contact.Maximum, scope = "Actual weighted skin vertices against a measured cylindrical barrel approximation; not triangle intersection or full prop surface certification." };
            for (int i = 0; i < source.Length; i++)
            {
                var bw = weights[i]; var point = matrices[bw.boneIndex0].MultiplyPoint3x4(source[i]) * bw.weight0;
                if (bw.weight1 > 0) point += matrices[bw.boneIndex1].MultiplyPoint3x4(source[i]) * bw.weight1;
                if (bw.weight2 > 0) point += matrices[bw.boneIndex2].MultiplyPoint3x4(source[i]) * bw.weight2;
                if (bw.weight3 > 0) point += matrices[bw.boneIndex3].MultiplyPoint3x4(source[i]) * bw.weight3;
                result.sampledVertices++;
                if (point.y < contact.Minimum || point.y > contact.Maximum) continue;
                result.shaftVertices++;
                float radial = new Vector2(point.x - contact.Centre.x, point.z - contact.Centre.z).magnitude;
                float penetration = Mathf.Max(0, contact.Radius - radial);
                if (penetration <= .001f) continue;
                result.penetratingVertices++;
                if (penetration > .003f) result.deepPenetratingVertices++;
                result.maxPenetrationMetres = Mathf.Max(result.maxPenetrationMetres, penetration);
                result.sumSquaredPenetration += penetration * penetration;
            }
            return result;
        }

        // Offline bounded coordinate search. Only writes a candidate report;
        // applying and reviewing the pose is an explicit separate step.
        public static void Optimize()
        {
            if (!Application.isBatchMode || SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null)
                throw new InvalidOperationException("Hand optimization requires batch mode and -nographics.");
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "-v2-hand-surface-audit-output");
            if (index < 0 || index + 1 >= args.Length) throw new InvalidOperationException("Missing output path.");
            string builds = Path.GetFullPath(Path.Combine(Application.dataPath, "../Builds"));
            string output = Path.GetFullPath(args[index + 1]);
            if (!output.StartsWith(builds + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) || File.Exists(output))
                throw new InvalidOperationException("Output must be a fresh file under Builds.");
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            var owner = new GameObject("Opt-in hand surface optimizer") { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                var hands = owner.AddComponent<FirstPersonHands>();
                hands.PrepareCpuPreview(PlayerItem.Flashlight);
                var library = HandAnimationLibrary.Load(); var pose = library.GetPose("Flashlight");
                var initial = new float[pose.bones.Length];
                for (int i = 0; i < initial.Length; i++) initial[i] = pose.bones[i].curl;
                var before = Measure(hands);
                float Score()
                {
                    var measured = Measure(hands);
                    var rig = Field<HandAnimationRig>(hands, "leftRig");
                    var torch = Field<Transform>(hands, "torch");
                    var contact = Field<HandCylinderContact>(hands, "torchContact");
                    float cost = measured.sumSquaredPenetration;
                    foreach (string digit in new[] { "thumb", "index", "middle", "ring", "little" })
                    {
                        Vector3 point = torch.InverseTransformPoint(rig.Tip(digit));
                        float radial = new Vector2(point.x - contact.Centre.x, point.z - contact.Centre.z).magnitude;
                        float miss = Mathf.Max(0, Mathf.Abs(radial - contact.Radius) - .005f);
                        cost += miss * miss * 10;
                    }
                    for (int i = 0; i < initial.Length; i++) cost += Mathf.Pow(pose.bones[i].curl - initial[i], 2) * .00000002f;
                    return cost;
                }
                float best = Score();
                foreach (float step in new[] { 12f, 6f, 3f, 1.5f })
                    for (int pass = 0; pass < 2; pass++)
                        for (int i = 0; i < pose.bones.Length; i++)
                        {
                            var channel = pose.bones[i];
                            if (!channel.name.Contains("_")) continue;
                            var limit = library.Bones[i]; float original = channel.curl, chosen = original;
                            for (int sign = -1; sign <= 1; sign += 2)
                            {
                                channel.curl = Mathf.Clamp(original + sign * step,
                                    Mathf.Max(limit.curlMin, initial[i] - 30), Mathf.Min(limit.curlMax, initial[i] + 30));
                                float candidate = Score();
                                if (candidate < best) { best = candidate; chosen = channel.curl; }
                            }
                            channel.curl = chosen;
                        }
                var report = new Optimized { before = before, after = Measure(hands), bones = pose.bones,
                    scope = "Offline candidate optimized for a measured cylindrical shaft and constrained fingertip markers; GPU visual and full triangle-contact validation remain required." };
                File.WriteAllText(output, JsonUtility.ToJson(report, true));
                Debug.Log("HAPPYTOY_HAND_SURFACE_OPTIMIZED " + JsonUtility.ToJson(report));
            }
            finally { UnityEngine.Object.DestroyImmediate(owner); }
        }

        public static void Export()
        {
            if (!Application.isBatchMode || SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null)
                throw new InvalidOperationException("Hand surface audit requires batch mode and -nographics.");
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "-v2-hand-surface-audit-output");
            if (index < 0 || index + 1 >= args.Length) throw new InvalidOperationException("Missing audit output path.");
            string builds = Path.GetFullPath(Path.Combine(Application.dataPath, "../Builds"));
            string output = Path.GetFullPath(args[index + 1]);
            if (!output.StartsWith(builds + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Hand audit output must be inside Builds.");
            if (File.Exists(output)) throw new InvalidOperationException("Hand audit must use a fresh output file.");
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            var owner = new GameObject("Opt-in hand surface audit") { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                var hands = owner.AddComponent<FirstPersonHands>();
                hands.PrepareCpuPreview(PlayerItem.Flashlight);
                var result = Measure(hands);
                File.WriteAllText(output, JsonUtility.ToJson(result, true));
                Debug.Log("HAPPYTOY_HAND_SURFACE_AUDIT " + JsonUtility.ToJson(result));
            }
            finally { UnityEngine.Object.DestroyImmediate(owner); }
        }
    }
}
