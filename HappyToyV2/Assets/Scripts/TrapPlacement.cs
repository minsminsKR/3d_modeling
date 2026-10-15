using UnityEngine;
using UnityEngine.AI;

namespace HappyToy.V2
{
    // A real, supported, clear footprint; invalid placement never consumes stock.
    public static class TrapPlacement
    {
        public const float Radius = .285f;
        static readonly RaycastHit[] floorHits = new RaycastHit[64];
        static readonly Collider[] overlaps = new Collider[64];
        public static bool Ground(Vector3 desired, out Vector3 point, bool ignoreActors = false)
        {
            point = default;
            if (!CorridorCheckpoint.Vector(desired) || !NavMesh.SamplePosition(desired,out var nav,.25f,NavMesh.AllAreas) ||
                Mathf.Abs(nav.position.y-desired.y) > .25f || Vector2.Distance(new Vector2(nav.position.x,nav.position.z),new Vector2(desired.x,desired.z)) > .20f ||
                !NavMesh.FindClosestEdge(nav.position,out var edge,NavMesh.AllAreas) || edge.distance < Radius) return false;
            int hitCount = Physics.RaycastNonAlloc(nav.position+Vector3.up*.45f,Vector3.down,floorHits,.8f,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore);
            bool found = false; RaycastHit hit = default; float nearest = float.MaxValue;
            if(hitCount==floorHits.Length)return false;
            for(int i=0;i<hitCount;i++)
            {
                var candidate = floorHits[i];
                if(ignoreActors && Transient(candidate.collider))continue;
                if(candidate.distance<nearest){hit=candidate;nearest=candidate.distance;found=true;}
            }
            if(!found || hit.normal.y<.97f || Mathf.Abs(hit.point.y-nav.position.y)>.075f)return false;
            int count = Physics.OverlapBoxNonAlloc(hit.point+Vector3.up*.095f,new Vector3(Radius,.065f,Radius),overlaps,
                Quaternion.identity,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore);
            if (count == overlaps.Length) return false;
            for (int i=0;i<count;i++)
            {
                if (ignoreActors && Transient(overlaps[i])) continue;
                if (overlaps[i].bounds.max.y > hit.point.y+.035f) return false;
            }
            point = hit.point; return true;
        }
        static bool Transient(Collider collider)
        {
            if(collider.GetComponentInParent<PlayerMotor>() || collider.GetComponentInParent<NavMeshAgent>())return true;
            var item=collider.GetComponentInParent<Interactable>();
            return item && PickupOutline.IsPickup(item.kind);
        }
        public static bool InFront(PlayerMotor player, out Vector3 point, out Quaternion facing)
        {
            point = default; facing = Quaternion.identity;
            if (!player || !player.eyes) return false;
            var direction = Vector3.ProjectOnPlane(player.eyes.transform.forward,Vector3.up);
            if (direction.sqrMagnitude < .01f) direction = player.transform.forward;
            direction.Normalize(); facing = Quaternion.LookRotation(direction);
            return Ground(player.transform.position+direction*.95f,out point);
        }
    }
}
