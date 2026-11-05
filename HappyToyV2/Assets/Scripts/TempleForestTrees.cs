using System;
using UnityEngine;

namespace HappyToy.V2
{
    // Installs the reviewed forest art without changing the physical forest.
    // Adding another ancient-tree placement only changes this index list;
    // all original roots and trunk capsules remain intact.
    [DisallowMultipleComponent]
    public sealed class TempleForestTrees : MonoBehaviour
    {
        const int ForestCount = 16;
        static readonly int[] AncientIndices = { 2, 3 };
        readonly Transform[] trees = new Transform[ForestCount];
        bool prepared;
        public int TreeCount => prepared ? trees.Length : 0;
        public int AncientCount => prepared ? AncientIndices.Length : 0;
        public int PineCount => prepared ? trees.Length - AncientIndices.Length : 0;

        public bool IsAncient(Transform tree)
        {
            if (!prepared || !tree) return false;
            foreach (int index in AncientIndices) if (trees[index] == tree) return true;
            return false;
        }

        public void Prepare()
        {
            if (prepared) throw new InvalidOperationException("Forest trees already prepared");
            var ancient = Resources.Load<GameObject>("TempleForest/ancient-zelkova-visual");
            var pine = Resources.Load<GameObject>("TemplePrologue/pine-trunk-review");
            ValidatePrefab(ancient); ValidatePrefab(pine);
            for (int i = 0; i < trees.Length; i++)
            {
                var tree = transform.Find("Pine " + i);
                var group = tree ? tree.GetComponent<LODGroup>() : null;
                if (!group || !group.enabled || !tree.GetComponent<CapsuleCollider>() ||
                    tree.Find("Ancient forest tree") || tree.Find("Reviewed forest pine"))
                    throw new InvalidOperationException("Original forest tree is not ready");
                foreach (var level in group.GetLODs())
                    foreach (var renderer in level.renderers)
                        if (!renderer) throw new InvalidOperationException("Original pine LOD renderer is missing");
                trees[i] = tree;
            }
            for (int i = 0; i < trees.Length; i++)
            {
                var tree = trees[i];
                var original = tree.GetComponent<LODGroup>();
                foreach (var level in original.GetLODs())
                    foreach (var renderer in level.renderers) renderer.enabled = false;
                original.enabled = false;
                bool hero = Array.IndexOf(AncientIndices, i) >= 0;
                var replacement = Instantiate(hero ? ancient : pine, tree, false);
                replacement.name = hero ? "Ancient forest tree" : "Reviewed forest pine";
                // Keep the original capsule upright and uniformly scaled.
                // Apply the varied silhouette only to the visual prefab.
                int row = i / 2;
                float side = i % 2 == 0 ? -1 : 1;
                var desiredRotation = Quaternion.Euler((row % 3 - 1) * 2.2f,
                    (i * 83) % 360, side * (1.5f + row % 3));
                replacement.transform.localRotation = Quaternion.Inverse(tree.localRotation) * desiredRotation;
                replacement.transform.localScale = new Vector3(1 + .07f * (row % 3 - 1),
                    1, 1 + .05f * ((row + 1) % 3 - 1));
            }
            prepared = true;
        }

        static void ValidatePrefab(GameObject prefab)
        {
            if (!prefab || !prefab.GetComponent<LODGroup>() || prefab.GetComponentInChildren<Collider>(true) ||
                prefab.GetComponentInChildren<Rigidbody>(true))
                throw new InvalidOperationException("Visual-only forest tree LOD prefabs are required");
        }
    }
}
