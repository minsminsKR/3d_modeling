using System;
using UnityEngine;

namespace HappyToy.V2
{
    // Authored metadata belongs to the replacement prefab, never the protected scene.
    [DisallowMultipleComponent]
    public sealed class EnemyStudioRig : MonoBehaviour
    {
        [Serializable]
        public sealed class SurfaceSupport
        {
            public SkinnedMeshRenderer skin;
            public int[] vertexIndices;
            public bool[] limbContact;
        }

        public string key;
        public SurfaceSupport[] surfaceSupports = Array.Empty<SurfaceSupport>();
        public SkinnedMeshRenderer[] primarySkins = Array.Empty<SkinnedMeshRenderer>();
        public bool HasLimbContacts => key == "Uncat";
        public static bool Supported(string value) => value == "Uncat" || value == "Cyclopse" || value == "Baby";

        void Awake() => EnsureAnimationAliases();

        public Animation EnsureAnimationAliases()
        {
            if (!Supported(key)) throw new InvalidOperationException("Unknown serialized studio creature: " + key);
            var player = GetComponent<Animation>();
            if (!player) throw new InvalidOperationException("Studio prefab lacks its live Animation owner: " + key);
            var aliases = key == "Baby" ? new[] { "patrol", "chase", "cry" } : new[] { "patrol", "chase" };
            foreach (var alias in aliases)
            {
                var clip = Resources.Load<AnimationClip>("EnemyStudio/" + key + "/" + alias);
                if (!clip || !clip.legacy || clip.length < .1f)
                    throw new InvalidOperationException("Studio creature lacks its own live action: " + key + "/" + alias);
                // Animation.AddClip aliases are live state, not a guaranteed
                // serialized prefab contract. Rebuild missing aliases per instance.
                if (player.GetClip(alias) != clip) player.AddClip(clip, alias);
                player[alias].wrapMode = WrapMode.Loop;
            }
            player.clip = player.GetClip("patrol"); player.playAutomatically = false;
            player.cullingType = AnimationCullingType.AlwaysAnimate;
            return player;
        }

        // New rigs have ordinary metre units. Evaluate their actual imported skin
        // matrices instead of relying on a legacy FBX's BakeMesh scale convention.
        public static Bounds PosedBounds(SkinnedMeshRenderer[] skins)
        {
            bool any = false; var result = new Bounds();
            foreach (var skin in skins)
            {
                var points = EvaluatePose(skin, true, false, out _);
                foreach (var point in points)
                    if (!any) { result = new Bounds(point, Vector3.zero); any = true; } else result.Encapsulate(point);
            }
            if (!any) throw new InvalidOperationException("Studio creature has no posed surface");
            return result;
        }

        public static Vector3[] PosedVertices(SkinnedMeshRenderer skin) => EvaluatePose(skin, true, false, out _);

        public static void BakeLocalPose(SkinnedMeshRenderer skin, Mesh target)
        {
            var source = skin.sharedMesh;
            var points = EvaluatePose(skin, false, true, out var normals);
            target.Clear(); target.indexFormat = source.indexFormat;
            target.vertices = points; target.normals = normals; target.uv = source.uv;
            target.subMeshCount = source.subMeshCount;
            for (int index = 0; index < source.subMeshCount; index++) target.SetTriangles(source.GetTriangles(index), index, false);
            target.RecalculateBounds();
        }

        static Vector3[] EvaluatePose(SkinnedMeshRenderer skin, bool worldSpace, bool includeNormals, out Vector3[] posedNormals)
        {
            if (!skin || !skin.sharedMesh || !skin.sharedMesh.isReadable || skin.sharedMesh.blendShapeCount != 0)
                throw new InvalidOperationException("Studio posed surface must be a readable, baked skinned mesh");
            var mesh = skin.sharedMesh; var source = mesh.vertices; var bindPoses = mesh.bindposes; var bones = skin.bones;
            var sourceNormals = includeNormals ? mesh.normals : null;
            if (bones.Length == 0 || bindPoses.Length != bones.Length || includeNormals && sourceNormals.Length != source.Length)
                throw new InvalidOperationException("Studio posed skin streams differ");
            var matrices = new Matrix4x4[bones.Length];
            for (int index = 0; index < bones.Length; index++)
            {
                if (!bones[index]) throw new InvalidOperationException("Studio posed skin lost a bone");
                matrices[index] = bones[index].localToWorldMatrix * bindPoses[index];
                if (!worldSpace) matrices[index] = skin.transform.worldToLocalMatrix * matrices[index];
            }
            var counts = mesh.GetBonesPerVertex(); var weights = mesh.GetAllBoneWeights();
            if (counts.Length != source.Length) throw new InvalidOperationException("Studio posed vertex weight order differs");
            var result = new Vector3[source.Length]; posedNormals = includeNormals ? new Vector3[source.Length] : null;
            int cursor = 0;
            for (int vertex = 0; vertex < source.Length; vertex++)
            {
                if (counts[vertex] == 0) throw new InvalidOperationException("Studio posed vertex is unweighted");
                Vector3 point = Vector3.zero, normal = Vector3.zero;
                for (int influence = 0; influence < counts[vertex]; influence++)
                {
                    var weight = weights[cursor++];
                    if (weight.boneIndex < 0 || weight.boneIndex >= matrices.Length) throw new InvalidOperationException("Studio posed bone index is invalid");
                    var matrix = matrices[weight.boneIndex];
                    point += matrix.MultiplyPoint3x4(source[vertex]) * weight.weight;
                    if (includeNormals) normal += matrix.MultiplyVector(sourceNormals[vertex]) * weight.weight;
                }
                result[vertex] = point;
                if (includeNormals) posedNormals[vertex] = normal.normalized;
            }
            if (cursor != weights.Length) throw new InvalidOperationException("Studio posed weight stream is incomplete");
            return result;
        }
    }
}
