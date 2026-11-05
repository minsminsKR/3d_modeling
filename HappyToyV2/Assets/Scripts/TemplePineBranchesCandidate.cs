using System;
using System.Collections.Generic;
using UnityEngine;

namespace HappyToy.V2
{
    // Opt-in comparisons restore the earlier pine on either the tree framed
    // by 01b-pine-close.png or all fourteen pines. Original parents, capsules,
    // transforms and distant art are retained.
    [DisallowMultipleComponent]
    public sealed class TemplePineBranchesCandidate : MonoBehaviour
    {
        readonly List<GameObject> originals = new List<GameObject>();
        readonly List<GameObject> replacements = new List<GameObject>();
        bool prepared;
        public int Count => replacements.Count;
        public void Prepare() => PrepareInternal(false);
        public void PrepareAll() => PrepareInternal(true);

        void PrepareInternal(bool all)
        {
            if (prepared || originals.Count != 0 || replacements.Count != 0)
                throw new InvalidOperationException("Pine branch comparison already prepared");
            var prefab = Resources.Load<GameObject>("TemplePrologue/pine-trunk-review");
            if (!prefab || !prefab.GetComponent<LODGroup>() ||
                prefab.GetComponentInChildren<Collider>(true) || prefab.GetComponentInChildren<Rigidbody>(true))
                throw new InvalidOperationException("The original pine and visual-only review prefab are required");
            var group = prefab.GetComponent<LODGroup>();
            if (group.lodCount != 2) throw new InvalidOperationException("Two pine review LODs are required");
            var targets = new List<Transform>();
            for (int i = 0; i < 16; i++)
            {
                if ((!all && i != 1) || i == 2 || i == 3) continue;
                var tree = transform.Find("Pine " + i);
                var old = tree ? tree.Find("Reviewed forest pine") : null;
                if (!tree || !tree.GetComponent<CapsuleCollider>() || !old || !old.gameObject.activeSelf || !old.GetComponent<LODGroup>())
                    throw new InvalidOperationException("An original pine or trunk capsule is missing");
                targets.Add(old);
            }
            if (targets.Count != (all ? 14 : 1)) throw new InvalidOperationException("Unexpected pine review count");
            try
            {
                foreach (var old in targets)
                {
                    var replacement = Instantiate(prefab, old.parent, false);
                    replacements.Add(replacement);
                    replacement.name = "Candidate original forest pine";
                    replacement.transform.localPosition = old.localPosition;
                    replacement.transform.localRotation = old.localRotation;
                    replacement.transform.localScale = old.localScale;
                    originals.Add(old.gameObject); old.gameObject.SetActive(false);
                }
                prepared = true;
            }
            catch
            {
                Restore(); throw;
            }
        }

        void Restore()
        {
            foreach (var original in originals) if (original) original.SetActive(true);
            foreach (var replacement in replacements)
                if (replacement) { if (Application.isPlaying) Destroy(replacement); else DestroyImmediate(replacement); }
            originals.Clear(); replacements.Clear();
        }
        void OnDestroy() => Restore();
    }
}
