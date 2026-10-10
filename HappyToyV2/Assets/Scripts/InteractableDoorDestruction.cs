using System.Collections.Generic;
using UnityEngine;

namespace HappyToy.V2
{
    public sealed partial class Interactable
    {
        // Exit seals and memory pickups are separate kinds, never breakable doors.
        // Ordinary doors may be locked by chapter authoring without preventing a
        // running wraith from physically shattering their real leaves.
        public bool DoorLocked;
        public bool DoorBroken { get; private set; }
        public int MaskImpacts { get; private set; }
        readonly Dictionary<Collider, bool> intactColliders = new Dictionary<Collider, bool>();
        readonly Dictionary<Renderer, bool> intactRenderers = new Dictionary<Renderer, bool>();

        void RememberDestructibleLeaves()
        {
            void Remember(Transform leaf)
            {
                if (!leaf) return;
                foreach (var collider in leaf.GetComponentsInChildren<Collider>(true))
                    if (!intactColliders.ContainsKey(collider)) intactColliders.Add(collider, collider.enabled);
                foreach (var renderer in leaf.GetComponentsInChildren<Renderer>(true))
                    if (!intactRenderers.ContainsKey(renderer)) intactRenderers.Add(renderer, renderer.enabled);
            }
            Remember(movingLeaf); Remember(secondaryLeaf);
        }

        public void RestoreDoorDestruction(bool broken)
        {
            if (kind != Kind.Door || !movingLeaf)
                throw new System.InvalidOperationException("Only ordinary movable doors can be shattered");
            RememberDestructibleLeaves(); DoorBroken = broken;
            foreach (var entry in intactColliders) if (entry.Key) entry.Key.enabled = !broken && entry.Value;
            foreach (var entry in intactRenderers) if (entry.Key) entry.Key.enabled = !broken && entry.Value;
            if (obstacle) obstacle.enabled = !broken && !open;
        }
        public void RestoreShatteredDoor(bool requestedOpen, Vector3 leafPosition, bool broken)
        {
            if (broken && !requestedOpen) throw new System.ArgumentException("A shattered door cannot be closed");
            RestoreDoor(requestedOpen, leafPosition); RestoreDoorDestruction(broken);
        }

        public bool BreakForMask(LanternMaskEncounter actor)
        {
            var session = GameSession.Current;
            if (!DoorOperable || !actor || !actor.CorridorRunner || !actor.isActiveAndEnabled ||
                actor.gameObject.scene != gameObject.scene || !session || !session.InputAllowed || session.Finished ||
                actor.State != LanternMaskEncounter.Phase.Wander && actor.State != LanternMaskEncounter.Phase.Investigate &&
                actor.State != LanternMaskEncounter.Phase.Chase) return false;
            var walking = actor.GetComponent<UnityEngine.AI.NavMeshAgent>();
            var delta = actor.transform.position - transform.position; delta.y = 0;
            if (!EnemyNavigation.Ready(walking) || walking.isStopped || !walking.hasPath || walking.velocity.sqrMagnitude < .25f ||
                !EnemyNavigation.SameFloor(actor.transform.position, transform.position.y) || delta.magnitude > 1.75f) return false;
            // Caller also proves that the native route crosses this opening. This
            // final physical check prevents arbitrary remote destruction APIs.
            var impactOrigin = actor.transform.position + Vector3.up;
            var impactTarget = transform.position + Vector3.up;
            if (Physics.Linecast(impactOrigin, impactTarget, out var hit, Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore) && !hit.collider.transform.IsChildOf(actor.transform) &&
                hit.collider.GetComponentInParent<Interactable>() != this) return false;
            SpawnShatteredLeaves(actor.transform.forward);
            open = true; passages.Clear(); changeFrame = Time.frameCount;
            RestoreDoorDestruction(true); MaskImpacts++; Physics.SyncTransforms();
            var voice = actor.GetComponent<CorridorMaskAudio>();
            if (voice) voice.PlayDoorSmash(transform);
            return true;
        }

        void SpawnShatteredLeaves(Vector3 direction)
        {
            RememberDestructibleLeaves();
            foreach (var entry in intactRenderers)
            {
                var renderer = entry.Key;
                if (!renderer || !entry.Value || !renderer.gameObject.activeInHierarchy) continue;
                var bounds = renderer.bounds;
                // Keep handles/trim out of the timber splinter cloud.
                if (bounds.size.y < 1 || Mathf.Max(bounds.size.x, bounds.size.z) < 1) continue;
                var shards = new GameObject("Mask impact timber splinters"); shards.layer = 2;
                shards.transform.SetParent(transform, false);
                var pieces = new Transform[7];
                for (int i = 0; i < pieces.Length; i++)
                {
                    var shard = GameObject.CreatePrimitive(PrimitiveType.Cube); shard.name = "Broken door timber"; shard.layer = 2;
                    var collider = shard.GetComponent<Collider>(); collider.enabled = false; Destroy(collider);
                    shard.transform.SetParent(shards.transform, true);
                    shard.transform.position = bounds.center + transform.right * ((i - 3) * .29f);
                    shard.transform.rotation = transform.rotation * Quaternion.Euler(0, 0, (i % 3 - 1) * 8);
                    shard.transform.localScale = new Vector3(.22f, bounds.size.y * .8f, .06f);
                    shard.GetComponent<Renderer>().sharedMaterial = renderer.sharedMaterial; pieces[i] = shard.transform;
                }
                shards.AddComponent<DoorShatterDebris>().Begin(pieces, direction);
            }
        }
    }

    // Cosmetic only: no rigid bodies, collision proxies or navigation obstacles.
    sealed class DoorShatterDebris : MonoBehaviour
    {
        Transform[] pieces; Vector3[] starts; Quaternion[] rotations; Vector3 direction; float elapsed;
        public void Begin(Transform[] parts, Vector3 forward)
        {
            pieces = parts; starts = new Vector3[parts.Length]; rotations = new Quaternion[parts.Length]; direction = forward;
            for (int i = 0; i < parts.Length; i++) { starts[i] = parts[i].position; rotations[i] = parts[i].rotation; }
        }
        void Update()
        {
            var session = GameSession.Current;
            if (!session || !session.InputAllowed) return;
            elapsed += Time.deltaTime;
            if (elapsed >= 1.4f) { Destroy(gameObject); return; }
            for (int i = 0; i < pieces.Length; i++)
            {
                float t = Mathf.Min(elapsed, .7f);
                pieces[i].position = starts[i] + direction * t * (1.8f + i * .17f) +
                    transform.right * (i - 3) * t * .35f + Vector3.up * (t * 2 - t * t * 5.5f);
                pieces[i].rotation = rotations[i] * Quaternion.Euler(t * (50 + i * 12), t * 30, t * (i - 3) * 24);
            }
        }
    }
}
