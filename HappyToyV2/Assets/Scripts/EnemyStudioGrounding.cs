using System;
using UnityEngine;

namespace HappyToy.V2
{
    // Exact skin deformation of authored support vertices. Bone world matrices
    // apply every ancestor scale once, without baking an entire large LOD per frame.
    internal sealed class EnemyStudioGrounding
    {
        sealed class Surface
        {
            public Transform[] bones;
            public Matrix4x4[] bindPoses, matrices;
            public Vector3[] points;
            public int[] offsets, counts, influences;
            public float[] weights;
            public bool[] contacts;
        }
        readonly Surface[] surfaces;
        public bool HasLimbContacts { get; }
        public int SampleCount { get; }

        public EnemyStudioGrounding(EnemyStudioRig rig)
        {
            if (!rig || !EnemyStudioRig.Supported(rig.key) || rig.surfaceSupports == null || rig.surfaceSupports.Length == 0)
                throw new InvalidOperationException("Studio creature is missing authored ground supports");
            surfaces = new Surface[rig.surfaceSupports.Length];
            int sampleCount = 0, contactCount = 0;
            for (int index = 0; index < surfaces.Length; index++)
            {
                var authored = rig.surfaceSupports[index];
                var skin = authored.skin;
                var mesh = skin ? skin.sharedMesh : null;
                if (!mesh || !mesh.isReadable || authored.vertexIndices == null || authored.vertexIndices.Length == 0 ||
                    authored.limbContact == null || authored.limbContact.Length != authored.vertexIndices.Length || mesh.blendShapeCount != 0)
                    throw new InvalidOperationException("Invalid studio support stream: " + rig.key);
                var bones = skin.bones; var bindPoses = mesh.bindposes;
                if (bones.Length == 0 || bones.Length != bindPoses.Length)
                    throw new InvalidOperationException("Studio support bind pose count differs: " + rig.key);
                foreach (var bone in bones) if (!bone) throw new InvalidOperationException("Missing studio support bone: " + rig.key);
                var vertices = mesh.vertices;
                // Unity owns these borrowed streams. Do not Dispose Allocator.None views.
                var counts = mesh.GetBonesPerVertex(); var allWeights = mesh.GetAllBoneWeights();
                if (counts.Length != vertices.Length || allWeights.Length == 0)
                    throw new InvalidOperationException("Studio support weights are absent: " + rig.key);
                var offsets = new int[counts.Length]; int cursor = 0;
                for (int vertex = 0; vertex < offsets.Length; vertex++) { offsets[vertex] = cursor; cursor += counts[vertex]; }
                if (cursor != allWeights.Length) throw new InvalidOperationException("Studio support weight order differs: " + rig.key);
                int influenceCount = 0;
                foreach (int vertex in authored.vertexIndices)
                {
                    if (vertex < 0 || vertex >= vertices.Length || counts[vertex] == 0)
                        throw new InvalidOperationException("Studio support vertex is invalid: " + rig.key);
                    influenceCount += counts[vertex];
                }
                var surface = new Surface
                {
                    bones = bones, bindPoses = bindPoses, matrices = new Matrix4x4[bones.Length],
                    points = new Vector3[authored.vertexIndices.Length], offsets = new int[authored.vertexIndices.Length],
                    counts = new int[authored.vertexIndices.Length], contacts = authored.limbContact,
                    influences = new int[influenceCount], weights = new float[influenceCount]
                };
                int write = 0;
                for (int point = 0; point < authored.vertexIndices.Length; point++)
                {
                    int vertex = authored.vertexIndices[point], sourceOffset = offsets[vertex];
                    surface.points[point] = vertices[vertex]; surface.offsets[point] = write; surface.counts[point] = counts[vertex];
                    float total = 0;
                    for (int influence = 0; influence < counts[vertex]; influence++)
                    {
                        var weight = allWeights[sourceOffset + influence];
                        if (weight.boneIndex < 0 || weight.boneIndex >= bones.Length || !StealthRules.Finite(weight.weight) || weight.weight < 0)
                            throw new InvalidOperationException("Studio support influence is invalid: " + rig.key);
                        surface.influences[write] = weight.boneIndex; surface.weights[write++] = weight.weight; total += weight.weight;
                    }
                    if (Mathf.Abs(total - 1) > .001f) throw new InvalidOperationException("Studio support weights are not normalized: " + rig.key);
                    if (surface.contacts[point]) contactCount++;
                }
                surfaces[index] = surface; sampleCount += surface.points.Length;
            }
            SampleCount = sampleCount;
            HasLimbContacts = rig.HasLimbContacts && contactCount > 0;
            if (rig.HasLimbContacts && !HasLimbContacts) throw new InvalidOperationException("Studio Uncat lacks hand/foot support surfaces");
        }

        public void Sample(out float sole, out float limbSole)
        {
            sole = limbSole = float.MaxValue;
            foreach (var surface in surfaces)
            {
                for (int bone = 0; bone < surface.bones.Length; bone++)
                    surface.matrices[bone] = surface.bones[bone].localToWorldMatrix * surface.bindPoses[bone];
                for (int point = 0; point < surface.points.Length; point++)
                {
                    Vector3 world = Vector3.zero;
                    int start = surface.offsets[point], end = start + surface.counts[point];
                    for (int influence = start; influence < end; influence++)
                        world += surface.matrices[surface.influences[influence]].MultiplyPoint3x4(surface.points[point]) * surface.weights[influence];
                    sole = Mathf.Min(sole, world.y);
                    if (HasLimbContacts && surface.contacts[point]) limbSole = Mathf.Min(limbSole, world.y);
                }
            }
        }
    }
}
