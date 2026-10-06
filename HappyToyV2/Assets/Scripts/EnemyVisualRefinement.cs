using System;
using System.Collections.Generic;
using UnityEngine;

namespace HappyToy.V2
{
    // Runtime-only version selection keeps the protected scene and original FBX assets intact.
    public static class EnemyVisualRefinement
    {
        public static bool TryApply(V1MonsterMotion motion, out Transform model, out Animation animation, out Material[] owned)
        {
            model = motion.model; animation = motion.animationPlayer; owned = Array.Empty<Material>();
            string key = Key(motion.name);
            if (key == null || !model || !animation) return false;
            var source = Resources.Load<GameObject>("EnemyRefinement/" + key + "-v2");
            if (!source) return false;
            var original = model;
            var replacement = UnityEngine.Object.Instantiate(source, original.parent, false);
            replacement.name = key + " surface refinement v2";
            replacement.transform.localPosition = original.localPosition;
            replacement.transform.localRotation = original.localRotation;
            replacement.transform.localScale = original.localScale;
            // Unity collapses Mixamo's source Armature node in three originals,
            // while the separately exported candidate retains an identity wrapper.
            // Host the unchanged source curves at their exact relative binding root;
            // keep the imported hierarchy, local transforms and bind poses intact.
            var bindingRoot = ResolveAnimationRoot(animation.transform, replacement.transform, key);
            var next = bindingRoot.GetComponent<Animation>();
            if (!next) next = bindingRoot.gameObject.AddComponent<Animation>();
            foreach (var imported in replacement.GetComponentsInChildren<Animation>(true))
            {
                if (imported == next) continue;
                imported.Stop(); imported.playAutomatically = false; imported.enabled = false;
                UnityEngine.Object.Destroy(imported);
            }
            next.Stop(); next.playAutomatically = false; next.cullingType = AnimationCullingType.AlwaysAnimate;
            var oldSkins = original.GetComponentsInChildren<SkinnedMeshRenderer>();
            var skins = replacement.GetComponentsInChildren<SkinnedMeshRenderer>();
            if (oldSkins.Length == 0 || skins.Length != oldSkins.Length)
            { UnityEngine.Object.Destroy(replacement); throw new InvalidOperationException("Refined enemy mesh count differs: " + key); }
            // Retain the real authored patrol/chase/cry clips. Every existing bone
            // binding must still point to the same relative hierarchy in the candidate.
            foreach (var skin in oldSkins)
                foreach (var bone in skin.bones)
                {
                    string path = RelativePath(animation.transform, bone);
                    var mapped = path == null ? null : path.Length == 0 ? bindingRoot : bindingRoot.Find(path);
                    if (!mapped)
                    { UnityEngine.Object.Destroy(replacement); throw new InvalidOperationException("Refined enemy lost animation bone " + key + ": " + path); }
                    // A path alone cannot prove compatible local rotation axes/units.
                    // Actual imported source/candidate data differs only by float roundoff.
                    if (Vector3.Distance(bone.localPosition, mapped.localPosition) > .00001f ||
                        Quaternion.Angle(bone.localRotation, mapped.localRotation) > .1f ||
                        Vector3.Distance(bone.localScale, mapped.localScale) > .0001f)
                    { UnityEngine.Object.Destroy(replacement); throw new InvalidOperationException("Refined enemy changed local animation basis " + key + ": " + path); }
                }
            foreach (AnimationState state in animation)
            {
                next.AddClip(state.clip, state.name);
                next[state.name].wrapMode = state.wrapMode; next[state.name].speed = state.speed;
            }
            next.clip = animation.clip;
            var patrol = animation.GetClip("patrol");
            if (patrol)
            {
                // Compare the same authored pose, including Baby's crawl height.
                // The source instance is retired below, so sampling its original
                // controller here preserves the clip asset and avoids rest-pose fit.
                patrol.SampleAnimation(animation.gameObject, 0);
                patrol.SampleAnimation(bindingRoot.gameObject, 0);
            }
            var before = BakedBounds(oldSkins); var after = BakedBounds(skins);
            if (before.size.y <= .01f || after.size.y <= .000001f)
            { UnityEngine.Object.Destroy(replacement); throw new InvalidOperationException("Refined enemy has invalid posed height: " + key); }
            replacement.transform.localScale *= before.size.y / after.size.y;
            after = BakedBounds(skins);
            // All four authored V1 agents have baseOffset=0 and roots at their
            // floor level. Subtract a future agent offset explicitly: the model's
            // patrol-zero sole belongs at the navigation feet, while height and
            // horizontal centre remain those of the same original patrol pose.
            // Copying the old renderer's offset left Uncat beyond the bounded
            // per-frame .35 m pose compensation.
            var agent = motion.GetComponent<UnityEngine.AI.NavMeshAgent>();
            float feetY = motion.transform.position.y - (agent ? agent.baseOffset : 0f);
            replacement.transform.position += new Vector3(before.center.x - after.center.x,
                feetY - after.min.y, before.center.z - after.center.z);
            var normal = Resources.Load<Texture2D>("EnemyRefinement/" + key + "-normal");
            var smoothness = Resources.Load<Texture2D>("EnemyRefinement/" + key + "-metallic-smoothness");
            if (!normal || !smoothness)
            { UnityEngine.Object.Destroy(replacement); throw new InvalidOperationException("Refined enemy lacks baked material maps: " + key); }
            var materials = new List<Material>();
            for (int index = 0; index < skins.Length; index++)
            {
                var prior = oldSkins[index].sharedMaterials; var mapped = new Material[prior.Length];
                for (int slot = 0; slot < prior.Length; slot++)
                {
                    var material = UnityEngine.Object.Instantiate(prior[slot]);
                    material.name = key + " UV baked surface v2";
                    material.SetTexture("_BumpMap", normal); material.SetFloat("_BumpScale", .75f); material.EnableKeyword("_NORMALMAP");
                    material.SetTexture("_MetallicGlossMap", smoothness); material.SetFloat("_Smoothness", 1);
                    material.SetFloat("_SmoothnessTextureChannel", 0); material.EnableKeyword("_METALLICSPECGLOSSMAP");
                    mapped[slot] = material; materials.Add(material);
                }
                skins[index].sharedMaterials = mapped; skins[index].updateWhenOffscreen = true;
            }
            original.gameObject.SetActive(false); UnityEngine.Object.Destroy(original.gameObject);
            model = replacement.transform; animation = next; owned = materials.ToArray(); return true;
        }

        public static Transform ResolveAnimationRoot(Transform sourceAnimationRoot, Transform candidateModel, string key)
        {
            if (!sourceAnimationRoot || !candidateModel) throw new ArgumentException("Missing enemy binding root");
            if (sourceAnimationRoot.Find("mixamorig:Hips"))
            {
                if (candidateModel.Find("mixamorig:Hips")) return candidateModel;
                var wrapper = candidateModel.Find("Armature");
                if (!wrapper || !wrapper.Find("mixamorig:Hips") ||
                    wrapper.localPosition.sqrMagnitude > .0000000001f ||
                    Quaternion.Angle(wrapper.localRotation, Quaternion.identity) > .001f ||
                    Vector3.Distance(wrapper.localScale, Vector3.one) > .00001f)
                    throw new InvalidOperationException("Refined enemy has nonidentity binding wrapper: " + key);
                return wrapper;
            }
            if (sourceAnimationRoot.Find("Armature/mixamorig:Hips") && candidateModel.Find("Armature/mixamorig:Hips"))
                return candidateModel;
            throw new InvalidOperationException("Refined enemy has incompatible animation root: " + key);
        }

        static string Key(string name)
        {
            foreach (string key in new[] { "Cyclopse", "Uncat", "Hwacat_angry", "Baby" })
                if (name.IndexOf(key, StringComparison.OrdinalIgnoreCase) >= 0) return key;
            return null;
        }
        static string RelativePath(Transform root, Transform node)
        {
            if (!node) return null;
            string path = "";
            while (node != root)
            {
                if (!node.parent) return null;
                path = path.Length == 0 ? node.name : node.name + "/" + path; node = node.parent;
            }
            return path;
        }
        static Bounds BakedBounds(SkinnedMeshRenderer[] skins)
        {
            var mesh = new Mesh(); var vertices = new List<Vector3>(); var bounds = new Bounds(); bool any = false;
            try
            {
                foreach (var skin in skins)
                {
                    // Actual GPU silhouettes on all four scaled source rigs match
                    // useScale=true; false followed by TransformPoint is ~91x oversized.
                    skin.BakeMesh(mesh, true); mesh.GetVertices(vertices);
                    foreach (var point in vertices)
                    {
                        var world = skin.transform.TransformPoint(point);
                        if (!any) { bounds = new Bounds(world, Vector3.zero); any = true; } else bounds.Encapsulate(world);
                    }
                }
                return bounds;
            }
            finally { UnityEngine.Object.Destroy(mesh); }
        }
    }
}
