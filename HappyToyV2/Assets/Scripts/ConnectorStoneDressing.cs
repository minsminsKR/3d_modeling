using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace HappyToy.V2
{
    // Sparse visual stepping stones bridge the existing authored mountain and
    // temple paths. The connector itself remains the sole owner of floor physics.
    internal sealed class ConnectorStoneDressing : MonoBehaviour
    {
        const int Rows = 17;
        const float StartZ = 34f, EndZ = 56f, StartY = 1.53f;

        public static void Create(Transform connector)
        {
            if (!connector) throw new ArgumentNullException(nameof(connector));
            var root = new GameObject("Mountain connector stepping stones");
            root.transform.SetParent(connector, false);
            var dressing = root.AddComponent<ConnectorStoneDressing>();
            try { dressing.Build(); }
            catch { Destroy(root); throw; }
        }

        void Build()
        {
            var longStone = Resources.Load<GameObject>("TempleForest/temple-stepping-stone-long-20261108/visual");
            var shortStone = Resources.Load<GameObject>("TempleForest/temple-stepping-stone-reference-20261107/visual");
            if (!longStone || !shortStone || !longStone.GetComponent<LODGroup>() || !shortStone.GetComponent<LODGroup>())
                throw new InvalidOperationException("Reviewed connector stepping stones and their LODs are required");
            foreach (var source in new[] { longStone, shortStone })
                if (source.GetComponentInChildren<Collider>(true) || source.GetComponentInChildren<Rigidbody>(true))
                    throw new InvalidOperationException("Connector stepping stones must be visual only");

            for (int row = 0; row < Rows; row++)
            {
                float centre = Mathf.Lerp(StartZ + .55f, EndZ - .7f, row / (float)(Rows - 1));
                for (int side = -1; side <= 1; side += 2)
                {
                    float z = centre + side * .11f + .09f * Mathf.Sin(row * 1.63f + side);
                    float x = side * (.40f + .07f * Mathf.Sin(row * 2.17f));
                    float y = Mathf.Lerp(StartY, 0, Mathf.InverseLerp(StartZ, EndZ, z));
                    var holder = CorridorEntranceModels.Group(transform, "Connector stepping stone", Vector3.zero);
                    Instantiate((row + side) % 3 == 0 ? shortStone : longStone, holder, false);
                    CorridorEntranceModels.FitBox(holder, new Vector3(x, y + .028f, z),
                        new Vector3(.63f + .05f * Mathf.Sin(row), .055f, .83f + .07f * Mathf.Cos(row * 1.3f)),
                        Quaternion.Euler(0, side * 12 + row % 3 * 7, 0));
                    foreach (var node in holder.GetComponentsInChildren<Transform>(true)) node.gameObject.layer = 8;
                    foreach (var renderer in holder.GetComponentsInChildren<Renderer>(true))
                        renderer.shadowCastingMode = ShadowCastingMode.Off;
                }
            }
        }
    }
}
