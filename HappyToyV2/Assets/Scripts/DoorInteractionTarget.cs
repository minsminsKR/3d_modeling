using UnityEngine;
using Unity.AI.Navigation;

namespace HappyToy.V2
{
    // An opened sliding leaf must remain operable from its original doorway.
    // This query has no physical collision and never contributes to navigation.
    [DisallowMultipleComponent]
    public sealed class DoorInteractionTarget : MonoBehaviour
    {
        Interactable door;
        static readonly RaycastHit[] hits = new RaycastHit[32];
        public static void Ensure(Interactable owner)
        {
            if (!owner.DoorOperable) return;
            var existing = owner.transform.Find("Doorway interaction query");
            var root = existing ? existing.gameObject : new GameObject("Doorway interaction query");
            root.transform.SetParent(owner.transform, false); root.layer = 2;
            var target = root.GetComponent<DoorInteractionTarget>();
            if (!target) target = root.AddComponent<DoorInteractionTarget>();
            target.door = owner;
            var box = root.GetComponent<BoxCollider>(); if (!box) box = root.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.center = owner.obstacle ? owner.obstacle.center : Vector3.up * 1.18f;
            box.size = owner.obstacle ? owner.obstacle.size : new Vector3(2.6f, 2.36f, .32f);
            var modifier = root.GetComponent<NavMeshModifier>(); if (!modifier) modifier = root.AddComponent<NavMeshModifier>();
            modifier.ignoreFromBuild = true;
        }
        public static Interactable Find(Vector3 origin, Vector3 direction, float reach, float opaqueDistance)
        {
            int count = Physics.RaycastNonAlloc(origin, direction, hits, reach, 1 << 2, QueryTriggerInteraction.Collide);
            if (count == hits.Length) return null;
            Interactable result = null; float nearest = Mathf.Min(reach, opaqueDistance + .01f);
            for (int i = 0; i < count; i++)
            {
                var target = hits[i].collider.GetComponent<DoorInteractionTarget>();
                if (!target || !target.door || !target.door.DoorOperable || hits[i].distance > nearest) continue;
                nearest = hits[i].distance; result = target.door;
            }
            return result;
        }
    }
}
