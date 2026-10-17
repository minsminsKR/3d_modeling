using System;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;

namespace HappyToy.V2
{
    // Seats world ritual pickups on real support. Inventory and saved identities
    // remain with RitualWorld; hand-held item transforms have a separate owner.
    public static class RitualPickupPlacement
    {
        const float SurfaceGap = .002f;

        static Vector3 Support(Vector3 desired)
        {
            foreach (var hit in Physics.RaycastAll(desired + Vector3.up * .35f, Vector3.down, 4,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore).OrderBy(hit => hit.distance))
            {
                if (hit.normal.y < .95f || hit.collider.GetComponentInParent<PlayerMotor>() ||
                    hit.collider.GetComponentInParent<NavMeshAgent>()) continue;
                var item = hit.collider.GetComponentInParent<Interactable>();
                if (item && PickupOutline.IsPickup(item.kind)) continue;
                return hit.point;
            }
            throw new InvalidOperationException("Ritual pickup has no supporting surface at " + desired);
        }

        public static void Place(Transform root, Transform visual, BoxCollider target, Vector3 desired)
        {
            Seat(root,visual,target,Support(desired));
        }
        public static void PlaceOnSupport(Transform root,Transform visual,BoxCollider target,Vector3 desired,Collider support)
        {
            // A closed drawer's tabletop is above its contents. Query the real
            // tray directly instead of accidentally seating the item on the desk.
            if(!support||!support.Raycast(new Ray(desired+Vector3.up*.10f,Vector3.down),out var hit,.3f)||hit.normal.y<.95f)
                throw new InvalidOperationException("Ritual pickup has no drawer support at "+desired);
            Seat(root,visual,target,hit.point);
        }
        static void Seat(Transform root,Transform visual,BoxCollider target,Vector3 support)
        {
            root.position = support;
            // Both source models are upright, centred meshes. Lay them flat,
            // then align their measured geometry instead of assuming a pivot.
            visual.localRotation = Quaternion.Euler(90, 15, 0);
            var bounds = GraphicsPropLibrary.LocalBounds(root);
            visual.localPosition += new Vector3(-bounds.center.x, SurfaceGap - bounds.min.y, -bounds.center.z);
            bounds = GraphicsPropLibrary.LocalBounds(root);
            target.size = Vector3.Max(bounds.size, new Vector3(.16f, .05f, .12f));
            // A thin paper's usable focus target also sits above the surface.
            target.center = new Vector3(bounds.center.x, bounds.min.y + target.size.y * .5f, bounds.center.z);
        }
    }
}
