using System.Collections.Generic;
using UnityEngine;

namespace HappyToy.V2
{
    // Query-only capsule: catches a pea without pushing the player, baking an
    // obstacle, intercepting interaction rays, or making traps eligible.
    [DisallowMultipleComponent]
    public sealed class LadySnapperTarget : MonoBehaviour
    {
        static readonly HashSet<LadySnapperTarget> targets = new();
        CapsuleCollider capsule;
        public LadyPursuit Owner { get; private set; }
        public static void Attach(LadyPursuit owner)
        {
            var root = new GameObject("Lady snapper contact capsule");
            root.layer = 2; root.transform.SetParent(owner.transform, false);
            var target = root.AddComponent<LadySnapperTarget>(); target.Owner = owner;
            target.capsule = root.AddComponent<CapsuleCollider>();
            target.capsule.isTrigger = true;
            // Includes the projectile's 8mm radius for the centre-line query.
            target.capsule.radius = .30f + FirecrackerTrajectory.Radius;
            target.capsule.height = 2.05f + 2 * FirecrackerTrajectory.Radius;
            target.capsule.center = Vector3.up * 1.025f;
        }
        bool Ready => isActiveAndEnabled && Owner && Owner.isActiveAndEnabled && capsule && capsule.enabled;
        public static bool Overlaps(Vector3 point)
        {
            foreach (var target in targets)
                if (target.Ready && (target.capsule.ClosestPoint(point) - point).sqrMagnitude < .00000001f) return true;
            return false;
        }
        internal static bool FirstContact(Ray ray, float distance, out RaycastHit hit)
        {
            hit = default; bool found = false;
            foreach (var target in targets)
                if (target.Ready && target.capsule.Raycast(ray, out var contact, distance))
                { hit = contact; distance = contact.distance; found = true; }
            return found;
        }
        void OnEnable() { targets.Add(this); }
        void OnDisable() { targets.Remove(this); }
    }
}
