using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace HappyToy.V2
{
    // Sparse 3D outer forest, using the reviewed pine's shared mesh/material
    // and both LODs. The original route and its existing trees remain unchanged.
    [DisallowMultipleComponent]
    public sealed class TempleForestBackgroundTrees : MonoBehaviour
    {
        struct Placement
        {
            public Vector3 position;
            public float yaw, lean, scale;
            public Placement(float x, float z, float yaw, float lean, float scale)
            { position = new Vector3(x, -.15f, z); this.yaw = yaw; this.lean = lean; this.scale = scale; }
        }
        static readonly Placement[] Placements = {
            new Placement(-12.8f, -7.5f, 41, 2.4f, .84f),
            new Placement(14.4f, -5.5f, 137, -1.5f, .91f),
            new Placement(-14.8f, 3.0f, 216, 1.2f, .78f),
            new Placement(13.0f, 8.2f, 299, -2.1f, .88f),
            new Placement(-10.8f, 20.8f, 91, 2.0f, .93f),
            new Placement(16.4f, 20.7f, 247, -1.1f, .81f)
        };
        readonly List<CapsuleCollider> pending = new List<CapsuleCollider>();
        CharacterController player;
        bool prepared, activated;
        public int TreeCount { get; private set; }
        public int PendingColliders => pending.Count;

        // Build and validate before moving the player; every new collider stays
        // disabled until the old checkpoint position has been restored.
        public void Prepare(PlayerMotor actor)
        {
            if (prepared || !actor) throw new InvalidOperationException("A fresh forest and player are required");
            player = actor.GetComponent<CharacterController>();
            if (!player) throw new InvalidOperationException("The player character controller is required");
            var prefab = Resources.Load<GameObject>("TemplePrologue/pine-trunk-review");
            if (!prefab || !prefab.GetComponent<LODGroup>() || prefab.GetComponentInChildren<Collider>(true) ||
                prefab.GetComponentInChildren<Rigidbody>(true))
                throw new InvalidOperationException("The visual-only reviewed pine LODs are required");
            foreach (var placement in Placements)
            {
                var tree = Instantiate(prefab, transform, false);
                tree.name = "Outer forest tree " + TreeCount;
                tree.transform.localPosition = placement.position;
                tree.transform.localRotation = Quaternion.Euler(placement.lean, placement.yaw, -placement.lean * .5f);
                tree.transform.localScale = new Vector3(placement.scale * .93f, placement.scale, placement.scale * 1.06f);
                var group = tree.GetComponent<LODGroup>();
                var levels = group.GetLODs();
                if (levels.Length != 2 || levels[0].renderers.Length == 0 || levels[1].renderers.Length == 0 ||
                    levels[1].screenRelativeTransitionHeight >= .46f)
                    throw new InvalidOperationException("Two valid pine LOD levels are required");
                // A high-detail tree is reserved for close inspection. The
                // path normally sees the 40k-triangle LOD instead.
                levels[0].screenRelativeTransitionHeight = .46f; group.SetLODs(levels);
                foreach (var level in levels)
                    foreach (var renderer in level.renderers)
                    {
                        if (!renderer) throw new InvalidOperationException("Missing background pine renderer");
                        renderer.shadowCastingMode = ShadowCastingMode.Off;
                    }
                var trunk = tree.AddComponent<CapsuleCollider>();
                trunk.center = new Vector3(0, 2, 0); trunk.height = 4; trunk.radius = .35f;
                trunk.enabled = false;
                pending.Add(trunk);
                TreeCount++;
            }
            prepared = true;
        }

        public void ActivateAtPlayer()
        {
            if (!prepared || activated || !player) throw new InvalidOperationException("Fresh background trees are required");
            activated = true;
            EnableClearTrunks();
        }

        bool ClearOfPlayer(CapsuleCollider trunk)
        {
            if (!trunk || !player) return false;
            Vector3 trunkScale = trunk.transform.lossyScale, playerScale = player.transform.lossyScale;
            float trunkRadius = trunk.radius * Mathf.Max(Mathf.Abs(trunkScale.x), Mathf.Abs(trunkScale.z));
            float playerRadius = (player.radius + player.skinWidth) * Mathf.Max(Mathf.Abs(playerScale.x), Mathf.Abs(playerScale.z));
            // The modest tilted trunk's centreline can protrude horizontally.
            Vector3 up = trunk.transform.up;
            float tiltExtent = Mathf.Sqrt(up.x * up.x + up.z * up.z) *
                Mathf.Max(0, trunk.height * Mathf.Abs(trunkScale.y) * .5f - trunkRadius);
            Vector3 delta = trunk.transform.TransformPoint(trunk.center) - player.transform.TransformPoint(player.center);
            float safe = trunkRadius + playerRadius + tiltExtent + .06f;
            return delta.x * delta.x + delta.z * delta.z > safe * safe;
        }

        void Update()
        {
            if (!activated || !player) return;
            EnableClearTrunks();
        }

        void EnableClearTrunks()
        {
            for (int i = pending.Count - 1; i >= 0; i--)
                if (!pending[i]) pending.RemoveAt(i);
                else if (ClearOfPlayer(pending[i])) { pending[i].enabled = true; pending.RemoveAt(i); }
        }
    }
}
