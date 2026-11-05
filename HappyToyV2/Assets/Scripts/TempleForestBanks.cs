using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace HappyToy.V2
{
    // Irregular, low relief outside the existing walkable approach. These are
    // instances of authored 3D rocks; the existing forest floor owns physics.
    [DisallowMultipleComponent]
    public sealed class TempleForestBanks : MonoBehaviour
    {
        readonly struct Placement
        {
            public readonly float x, z, width, height, depth, yaw;
            public Placement(float x, float z, float width, float height, float depth, float yaw)
            { this.x = x; this.z = z; this.width = width; this.height = height; this.depth = depth; this.yaw = yaw; }
        }
        static readonly Placement[] Banks = {
            new Placement(-5.4f, -18.0f, 2.4f, .34f, 3.0f,  25),
            new Placement( 6.8f, -16.5f, 2.8f, .48f, 2.4f, -28),
            new Placement(-7.4f, -10.6f, 3.3f, .52f, 2.1f,  18),
            new Placement( 5.6f,  -7.8f, 2.0f, .32f, 3.5f,  68),
            new Placement(-6.5f,  -1.2f, 2.6f, .40f, 2.4f,  34),
            new Placement( 8.5f,   2.4f, 3.1f, .55f, 2.8f,  10),
            new Placement(-8.2f,   8.6f, 2.9f, .48f, 2.8f,  49),
            new Placement( 6.2f,  12.4f, 2.8f, .35f, 3.0f,  70)
        };
        static readonly Placement[] Stones = {
            new Placement(-3.2f,-17.5f,.62f,.15f,.48f,  23),
            new Placement(-4.0f,-14.2f,.44f,.12f,.58f, 121),
            new Placement( 4.1f,-12.3f,.55f,.14f,.40f, 227),
            new Placement(-5.0f, -9.0f,.71f,.17f,.51f,  73),
            new Placement( 3.4f, -6.6f,.48f,.11f,.64f, 174),
            new Placement( 5.2f, -4.0f,.67f,.15f,.45f, 312),
            new Placement(-4.1f, -2.0f,.56f,.13f,.56f,  46),
            new Placement( 3.8f,   .8f,.64f,.16f,.47f, 208),
            new Placement(-3.4f,  3.7f,.50f,.12f,.60f, 148),
            new Placement( 5.0f,  6.2f,.68f,.17f,.50f, 291),
            new Placement(-5.2f,  9.6f,.53f,.12f,.48f,  83),
            new Placement( 4.3f, 12.1f,.58f,.14f,.61f, 256)
        };
        Transform root;

        public void Prepare()
        {
            if (root) throw new InvalidOperationException("Forest banks already prepared");
            var ground = transform.Find("Forest ground");
            var path = transform.Find("Stone approach");
            var stones = Resources.Load<GameObject>("SchoolRenewal/loose-stones/loose-stones-visual");
            var rock = Resources.Load<GameObject>("CorridorEntrance/cave-rock-visual");
            if (!ground || !path || !stones || !rock || !stones.GetComponent<LODGroup>() || !rock.GetComponent<LODGroup>() ||
                stones.GetComponentInChildren<Collider>(true) || rock.GetComponentInChildren<Collider>(true) ||
                stones.GetComponentInChildren<Rigidbody>(true) || rock.GetComponentInChildren<Rigidbody>(true))
                throw new InvalidOperationException("Existing visual-only forest rock LODs are required");
            float soilTop = ground.localPosition.y + ground.localScale.y * .5f;
            root = CorridorEntranceModels.Group(transform, "Natural forest banks", Vector3.zero);
            for (int i = 0; i < Banks.Length; i++)
            {
                var holder = CorridorEntranceModels.Group(root, "Mossy low bank " + i, Vector3.zero);
                Instantiate(rock, holder, false);
                Place(holder, Banks[i], soilTop);
            }
            for (int i = 0; i < Stones.Length; i++)
            {
                var holder = CorridorEntranceModels.Group(root, "Scattered weathered stones " + i, Vector3.zero);
                Instantiate(stones, holder, false);
                Place(holder, Stones[i], soilTop);
            }
        }

        static void Place(Transform holder, Placement placement, float soilTop)
        {
            CorridorEntranceModels.FitBox(holder,
                new Vector3(placement.x, soilTop + placement.height * .40f, placement.z),
                new Vector3(placement.width, placement.height, placement.depth), Quaternion.Euler(0, placement.yaw, 0));
            var lod = holder.GetComponentInChildren<LODGroup>(true);
            if (!lod || lod.lodCount != 2) throw new InvalidOperationException("Forest bank needs two authored LODs");
            // These low visual accents do not warrant detailed geometry or
            // dynamic shadows; retain one authored distance mesh per accent.
            lod.ForceLOD(1);
            foreach (var renderer in holder.GetComponentsInChildren<Renderer>(true))
            {
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = true;
            }
        }

        void OnDestroy()
        {
            if (root) { if (Application.isPlaying) Destroy(root.gameObject); else DestroyImmediate(root.gameObject); }
        }
    }
}
