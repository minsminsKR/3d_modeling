using System;
using UnityEngine;

namespace HappyToy.V2
{
    public static class EnemyStudioVisual
    {
        public static bool TryApply(V1MonsterMotion motion, string key, out Transform model, out Animation animation)
        {
            model = motion.model; animation = motion.animationPlayer;
            if (!EnemyStudioRig.Supported(key)) return false;
            var prefab = Resources.Load<GameObject>("EnemyStudio/" + key + "/" + key);
            // The handoff may contain an exported FBX before its reviewed prefab exists.
            // Resources.Load also finds that raw FBX; only mount a completed studio rig.
            if (!prefab || !prefab.GetComponent<EnemyStudioRig>()) return false;
            if (!model || !animation) throw new InvalidOperationException("Studio enemy lacks its navigation visual mount: " + key);
            var original = model;
            var replacement = UnityEngine.Object.Instantiate(prefab, original.parent, false);
            replacement.name = key + " studio creature";
            try
            {
                var rig = replacement.GetComponent<EnemyStudioRig>();
                var next = replacement.GetComponent<Animation>();
                // An inactive corridor clone may reach this before the nested
                // prefab's Awake. Ensure aliases explicitly before validation.
                if (rig) next = rig.EnsureAnimationAliases();
                if (!rig || rig.key != key || !next || !next.GetClip("patrol") || !next.GetClip("chase") ||
                    key == "Baby" && !next.GetClip("cry"))
                    throw new InvalidOperationException("Studio creature/clip contract is incomplete: " + key);
                if (replacement.GetComponentsInChildren<Collider>(true).Length > 0)
                    throw new InvalidOperationException("Studio visual must not alter navigation collision: " + key);
                next.Stop(); next.playAutomatically = false; next.cullingType = AnimationCullingType.AlwaysAnimate;
                foreach (AnimationState state in next) state.wrapMode = WrapMode.Loop;
                next.GetClip("patrol").SampleAnimation(replacement, 0);
                // Keep the new prefab's own unit scale, bind poses and local axes.
                // The retired FBX's transforms/weights/UVs do not apply to this mesh.
                var bounds = EnemyStudioRig.PosedBounds(rig.primarySkins);
                var agent = motion.GetComponent<UnityEngine.AI.NavMeshAgent>();
                float feet = motion.transform.position.y - (agent ? agent.baseOffset : 0);
                replacement.transform.position += new Vector3(motion.transform.position.x - bounds.center.x,
                    feet - bounds.min.y, motion.transform.position.z - bounds.center.z);
                MonsterRedEyes.Attach(replacement.transform, key);
                original.gameObject.SetActive(false); UnityEngine.Object.Destroy(original.gameObject);
                model = replacement.transform; animation = next; return true;
            }
            catch { replacement.SetActive(false); UnityEngine.Object.Destroy(replacement); throw; }
        }
    }
}
