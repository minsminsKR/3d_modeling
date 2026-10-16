using System;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace HappyToy.V2
{
    // Shared derived game geometry. Applying this profile changes only a live
    // encounter clone; the original prefab, title, rig and clips stay intact.
    public sealed class BaeksugwiGameLod : ScriptableObject
    {
        [Serializable]
        public sealed class Level
        {
            public Mesh mesh;
            public string[] boneNames;
            public Bounds localBounds;
            public float screenHeight;
        }
        public Level[] levels;
        public string sourceHash;
        public int LevelCount => levels?.Length ?? 0;

        public void Apply(Transform model)
        {
            if (!model) throw new ArgumentNullException(nameof(model));
            if (model.GetComponent<LODGroup>()) return;
            var original = model.GetComponentsInChildren<SkinnedMeshRenderer>(true).Single();
            var originalBones = original.bones;
            var originalBind = original.sharedMesh.bindposes;
            var names = originalBones.Select(bone => bone.name).ToArray();
            Validate();
            var lods = new LOD[levels.Length];
            for (int index = 0; index < levels.Length; index++)
            {
                var level = levels[index];
                var mapped = new Transform[level.boneNames.Length];
                var bind = level.mesh.bindposes;
                for (int bone = 0; bone < mapped.Length; bone++)
                {
                    int source = Array.IndexOf(names, level.boneNames[bone]);
                    if (source < 0 || !SameBindPose(bind[bone], originalBind[source]))
                        throw new InvalidOperationException("Derived Baeksugwi bone binding differs: " + level.boneNames[bone]);
                    mapped[bone] = originalBones[source];
                }
                var skin = original;
                if (index > 0)
                {
                    var child = new GameObject("Game-only Baeksugwi LOD" + index);
                    child.layer = original.gameObject.layer;
                    child.transform.SetParent(original.transform, false);
                    skin = child.AddComponent<SkinnedMeshRenderer>();
                    skin.sharedMaterials = original.sharedMaterials;
                    skin.rootBone = original.rootBone;
                    skin.quality = original.quality;
                    skin.shadowCastingMode = original.shadowCastingMode;
                    skin.receiveShadows = original.receiveShadows;
                    skin.lightProbeUsage = original.lightProbeUsage;
                    skin.reflectionProbeUsage = original.reflectionProbeUsage;
                    skin.motionVectorGenerationMode = original.motionVectorGenerationMode;
                    skin.skinnedMotionVectors = original.skinnedMotionVectors;
                    skin.allowOcclusionWhenDynamic = original.allowOcclusionWhenDynamic;
                }
                skin.sharedMesh = level.mesh;
                skin.bones = mapped;
                skin.localBounds = level.localBounds;
                skin.updateWhenOffscreen = false;
                lods[index] = new LOD(level.screenHeight, new Renderer[] { skin });
            }
            var group = model.gameObject.AddComponent<LODGroup>();
            group.fadeMode = LODFadeMode.None;
            group.SetLODs(lods);
            group.RecalculateBounds();
        }

        public void Validate()
        {
            if (levels == null || levels.Length != 1)
                throw new InvalidOperationException("One reviewed Baeksugwi game LOD is required");
            for (int i = 0; i < levels.Length; i++)
            {
                var level = levels[i];
                if (!level.mesh || level.boneNames == null || level.boneNames.Length != level.mesh.bindposes.Length ||
                    level.boneNames.Length == 0 || Mathf.Min(level.localBounds.size.x, level.localBounds.size.y, level.localBounds.size.z) <= 0 ||
                    level.mesh.triangles.Length / 3 > 400000)
                    throw new InvalidOperationException("Invalid derived Baeksugwi LOD" + i);
            }
        }

        public static bool SameBindPose(Matrix4x4 a, Matrix4x4 b)
        {
            for (int element = 0; element < 16; element++)
                if (Mathf.Abs(a[element] - b[element]) > .0001f) return false;
            return true;
        }
    }
}
