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
            var stones = Resources.Load<GameObject>("TempleForest/temple-stepping-stone-reference-20261107/visual");
            var fern = Resources.Load<GameObject>("TempleForest/fern-clump-visual");
            var groundcover = Resources.Load<GameObject>("TempleForest/fern-undergrowth-visual");
            var lantern = Resources.Load<GameObject>("TempleForest/stone-lantern-visual");
            if (!stones || !fern || !groundcover || !lantern || !stones.GetComponent<LODGroup>() || !fern.GetComponent<LODGroup>())
                throw new InvalidOperationException("Temple approach requires the existing LOD forest assets");
            foreach (var prefab in new[] { stones, fern, groundcover, lantern })
                if (prefab.GetComponentInChildren<Collider>(true) || prefab.GetComponentInChildren<Rigidbody>(true))
                    throw new InvalidOperationException("Temple approach dressing must be visual only");
            prepared = true;
            var root = new GameObject("Mountain approach to temple visual dressing").transform;
            root.SetParent(transform, false);
            for (int row = 0; row < 6; row++)
            {
                float z = -21 + row * 3.5f;
                foreach (float side in new[] { -1f, 1f })
                {
                    Add(stones, root, "Approach side stone", new Vector3(side * (3.8f + row % 2 * .35f), -.01f, z),
                        .10f + row * .005f, side * 41 + row * 73);
                    Add(fern, root, "Approach side fern", new Vector3(side * (4.7f + row % 2 * .35f), 0, z + 1.1f),
                        .42f + row * .025f, side * 63 + row * 53);
                    Add(groundcover, root, "Approach forest groundcover", new Vector3(side * (5.1f + row % 3 * .3f), -.01f, z - .65f),
                        .32f + row * .015f, side * 31 + row * 47);
                }
            }
            foreach (float side in new[] { -1f, 1f })
            {
                float z = side < 0 ? -18.2f : -14.8f;
                var at = new Vector3(side * 3.65f, 0, z);
                Add(lantern, root, "Approach weathered stone lantern", at, 1.12f, side * 18);
                var lightObject = new GameObject("Approach lantern glow"); lightObject.transform.SetParent(root, false);
                lightObject.transform.localPosition = at + Vector3.up * .9f;
                var light = lightObject.AddComponent<Light>(); light.type = LightType.Point;
                light.color = new Color(1, .58f, .3f); light.range = 4; light.intensity = 1.0f; light.shadows = LightShadows.None;
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
