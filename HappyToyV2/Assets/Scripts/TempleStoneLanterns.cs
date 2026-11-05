using System;
using UnityEngine;

namespace HappyToy.V2
{
    // Two source-derived Korean stone lanterns at the temple shoulders. Their
    // existing generated meshes/material are shared; the path and physics are
    // still owned by the original prologue.
    [DisallowMultipleComponent]
    public sealed class TempleStoneLanterns : MonoBehaviour
    {
        Transform root;
        public void Prepare()
        {
            if (root) throw new InvalidOperationException("Stone lanterns already prepared");
            var prefab = Resources.Load<GameObject>("TempleForest/stone-lantern-visual");
            if (!prefab || !prefab.GetComponent<LODGroup>() || prefab.GetComponentInChildren<Collider>(true) ||
                prefab.GetComponentInChildren<Rigidbody>(true))
                throw new InvalidOperationException("Visual-only stone lantern with LODs is required");
            root = CorridorEntranceModels.Group(transform, "Temple weathered stone lanterns", Vector3.zero);
            var positions = new[] { new Vector3(-4.30f, 0, 10.1f), new Vector3(4.55f, 0, 11.15f) };
            var heights = new[] { 1.38f, 1.25f };
            var angles = new[] { 12f, -19f };
            for (int i = 0; i < positions.Length; i++)
            {
                var holder = CorridorEntranceModels.Group(root, "Aged Korean stone lantern", Vector3.zero);
                Instantiate(prefab, holder, false);
                CorridorEntranceModels.GroundAtHeight(holder, positions[i], heights[i], Quaternion.Euler(0, angles[i], 0));
                foreach (var child in holder.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 8;
            }
        }
        void OnDestroy() { if (root) Destroy(root.gameObject); }
    }
}
