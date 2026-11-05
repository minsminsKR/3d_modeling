using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace HappyToy.V2
{
    // Sparse visual-only undergrowth; no gameplay or physics ownership.
    [DisallowMultipleComponent]
    public sealed class TempleForestFerns : MonoBehaviour
    {
        bool prepared;

        public void Prepare()
        {
            if (prepared) throw new InvalidOperationException("Forest ferns already prepared");
            var prefab = Resources.Load<GameObject>("TempleForest/fern-clump-visual");
            if (!prefab || !prefab.GetComponent<LODGroup>() || prefab.GetComponentInChildren<Collider>(true) ||
                prefab.GetComponentInChildren<Rigidbody>(true))
                throw new InvalidOperationException("Visual-only fern with LOD is required");
            prepared = true;
            var root = new GameObject("Forest fern accents").transform;
            root.SetParent(transform, false);
            // Two pairs with asymmetric rotation/size; all remain well outside the 3.4 m path.
            var positions = new[] {
                new Vector3(-3.65f, -.01f, -16.5f), new Vector3(3.9f, -.01f, -12.3f),
                new Vector3(-3.9f, -.01f, -4.7f), new Vector3(3.65f, -.01f, 2.7f)
            };
            var rotations = new[] { 27f, 154f, 231f, 316f };
            var scales = new[] { .70f, .58f, .76f, .63f };
            for (int i = 0; i < positions.Length; i++)
            {
                var plant = Instantiate(prefab, root, false);
                plant.name = "Forest fern " + i;
                plant.transform.localPosition = positions[i];
                plant.transform.localRotation = Quaternion.Euler(0, rotations[i], 0);
                plant.transform.localScale = Vector3.one * scales[i];
                foreach (var renderer in plant.GetComponentsInChildren<Renderer>(true))
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
            }
        }
    }
}
