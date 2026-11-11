using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace HappyToy.V2
{
    // Sparse visual-only transition landmarks using existing forest assets.
    // The walkable floor, altar and interactive objects remain owned by the
    // prologue; this component creates no collision or gameplay state.
    [DisallowMultipleComponent]
    public sealed class TempleApproachPolish : MonoBehaviour
    {
        bool prepared;
        public void Prepare()
        {
            if (prepared) throw new InvalidOperationException("Temple approach already prepared");
            var stones = Resources.Load<GameObject>("TempleForest/temple-stone-cluster-visual");
            var fern = Resources.Load<GameObject>("TempleForest/fern-clump-visual");
            if (!stones || !fern || !stones.GetComponent<LODGroup>() || !fern.GetComponent<LODGroup>())
                throw new InvalidOperationException("Temple approach requires the existing LOD forest assets");
            if (stones.GetComponentInChildren<Collider>(true) || fern.GetComponentInChildren<Collider>(true) ||
                stones.GetComponentInChildren<Rigidbody>(true) || fern.GetComponentInChildren<Rigidbody>(true))
                throw new InvalidOperationException("Temple approach dressing must be visual only");
            prepared = true;
            var root = new GameObject("Mountain approach to temple visual dressing").transform;
            root.SetParent(transform, false);
            for (int row = 0; row < 4; row++)
            {
                float z = -21 + row * 4.2f;
                foreach (float side in new[] { -1f, 1f })
                {
                    Add(stones, root, "Approach side stone", new Vector3(side * (3.8f + row % 2 * .35f), 0, z),
                        .36f + row * .035f, side * 41 + row * 73);
                    Add(fern, root, "Approach side fern", new Vector3(side * (4.7f + row % 2 * .35f), 0, z + 1.1f),
                        .42f + row * .025f, side * 63 + row * 53);
                }
            }
        }
        static void Add(GameObject source, Transform parent, string name, Vector3 at, float height, float yaw)
        {
            var holder = new GameObject(name).transform;
            holder.gameObject.layer = 8; holder.SetParent(parent, false);
            Instantiate(source, holder, false);
            foreach (var node in holder.GetComponentsInChildren<Transform>(true)) node.gameObject.layer = 8;
            CorridorEntranceModels.GroundAtHeight(holder, at, height, Quaternion.Euler(0, yaw, 0));
            foreach (var renderer in holder.GetComponentsInChildren<Renderer>(true))
                renderer.shadowCastingMode = ShadowCastingMode.Off;
        }
    }
}
