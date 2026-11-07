using System;
using UnityEngine;

namespace HappyToy.V2
{
    // Existing fern geometry scattered outside the 3.4 m stone approach.
    // All parts are visual-only; the soil and tree trunks own their collision.
    [DisallowMultipleComponent]
    public sealed class TempleForestUndergrowth : MonoBehaviour
    {
        struct Placement
        {
            public float x, z, yaw, scale;
            public Placement(float x, float z, float yaw, float scale)
            { this.x = x; this.z = z; this.yaw = yaw; this.scale = scale; }
        }
        static readonly Placement[] Plants = {
            new Placement(-2.45f,-19.1f,  32,.65f), new Placement( 2.60f,-17.8f, 171,.70f),
            new Placement(-3.40f,-15.3f, 215,.83f), new Placement( 4.25f,-14.2f,  61,.67f),
            new Placement(-2.60f,-11.8f, 116,.72f), new Placement( 2.65f, -9.7f, 301,.75f),
            new Placement(-4.35f, -8.1f,  18,.91f), new Placement( 3.65f, -6.2f, 208,.65f),
            new Placement(-2.50f, -4.5f, 278,.78f), new Placement( 4.20f, -1.8f, 133,.88f),
            new Placement(-3.05f,  1.2f,  49,.65f), new Placement( 2.55f,  4.2f, 237,.71f),
            new Placement(-3.45f,  5.8f, 324,.80f), new Placement( 4.40f,  7.2f,  94,.70f),
            new Placement(-5.10f, 10.3f, 181,.96f), new Placement( 4.10f, 11.7f,  12,.77f),
            new Placement(-6.20f,-20.3f, 152,.94f), new Placement( 6.60f,-12.2f, 265,.87f),
            new Placement(-6.80f, -1.5f,  77,1.00f), new Placement( 7.00f,  9.6f, 192,.93f),
            // Clumps at different distances break up the bare edges without
            // occupying the central route or introducing new collision.
            new Placement(-2.85f,-15.9f,  87,1.20f), new Placement( 2.70f,-13.3f, 248,1.25f),
            new Placement(-4.75f,-12.6f, 319,1.15f), new Placement( 3.35f,-10.6f,  45,1.35f),
            new Placement(-2.65f, -8.9f, 164,1.20f), new Placement( 5.05f, -5.4f, 292,1.38f),
            new Placement(-3.15f, -2.9f, 205,1.30f), new Placement( 2.85f,  .1f,  73,1.22f),
            new Placement(-4.55f,  3.3f, 333,1.35f), new Placement( 3.15f,  6.1f, 141,1.18f),
            // Subtle foreground framing, reviewed separately from the walkable route.
            new Placement(-3.80f,  7.2f,  87, .95f), new Placement( 3.50f,  6.6f, 248, .85f)
        };
        bool prepared;
        public int PlantCount => prepared ? Plants.Length : 0;

        public void Prepare()
        {
            if (prepared) throw new InvalidOperationException("Forest undergrowth already prepared");
            var prefab = Resources.Load<GameObject>("TempleForest/fern-undergrowth-visual");
            if (!prefab || !prefab.GetComponent<LODGroup>() || prefab.GetComponentInChildren<Collider>(true) ||
                prefab.GetComponentInChildren<Rigidbody>(true))
                throw new InvalidOperationException("Visual-only undergrowth LODs are required");
            var root = new GameObject("Forest undergrowth").transform;
            root.SetParent(transform, false);
            foreach (var placement in Plants)
            {
                var plant = Instantiate(prefab, root, false);
                plant.name = "Forest undergrowth fern";
                plant.transform.localPosition = new Vector3(placement.x, -.01f, placement.z);
                plant.transform.localRotation = Quaternion.Euler(0, placement.yaw, 0);
                plant.transform.localScale = Vector3.one * placement.scale;
            }
            prepared = true;
        }
    }
}
