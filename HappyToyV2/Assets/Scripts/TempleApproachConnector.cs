using UnityEngine;

namespace HappyToy.V2
{
    // Joins the 4.5% mountain incline to the already authored temple ground.
    // Its far end overlaps the temple ground, so teardown after departure is safe.
    internal sealed class TempleApproachConnector : MonoBehaviour
    {
        Mesh mesh;
        public static void Create(Transform mountain)
        {
            var source = mountain.Find("Walkable mountain soil");
            if (!source || !source.GetComponent<MeshRenderer>())
                throw new System.InvalidOperationException("Mountain ground material is missing");
            var root = new GameObject("Mountain to temple connector");
            root.layer = 8; root.transform.SetParent(mountain, false);
            var owner = root.AddComponent<TempleApproachConnector>();
            owner.mesh = new Mesh { name = "Walkable mountain to temple slope" };
            // Match the full 24m mountain collision width: a narrowed bridge
            // lets players step off the terrain at either side of z=448.
            owner.mesh.vertices = new[] { new Vector3(-12, 1.53f, 34), new Vector3(12, 1.53f, 34),
                new Vector3(-12, 0, 56), new Vector3(12, 0, 56) };
            owner.mesh.uv = new[] { Vector2.zero, Vector2.right, Vector2.up, Vector2.one };
            owner.mesh.triangles = new[] { 0, 2, 1, 1, 2, 3 };
            owner.mesh.RecalculateNormals(); owner.mesh.RecalculateBounds();
            root.AddComponent<MeshFilter>().sharedMesh = owner.mesh;
            root.AddComponent<MeshRenderer>().sharedMaterial = source.GetComponent<MeshRenderer>().sharedMaterial;
            root.AddComponent<MeshCollider>().sharedMesh = owner.mesh;
            // The route includes the full mountain width. Both edge guards span
            // the uphill and downhill ground without narrowing the main path or
            // the walkable side trail. The authored temple owns the far end.
            foreach (float side in new[] { -1f, 1f })
            {
                var guard = new GameObject("Mountain lateral safety boundary");
                guard.layer = 8; guard.transform.SetParent(root.transform, false);
                guard.transform.localPosition = new Vector3(side * 12f, 3f, 28f);
                guard.AddComponent<BoxCollider>().size = new Vector3(.3f, 8f, 56f);
            }
            ConnectorBankDressing.Create(root.transform, mountain);
            ConnectorStoneDressing.Create(root.transform);
        }
        void OnDestroy() { if (mesh) Destroy(mesh); }
    }
}
