using System.Collections.Generic;
using UnityEngine;

namespace HappyToy.V2
{
    public sealed partial class Interactable
    {
        public bool DoorLocked;
        public bool DoorBroken { get; private set; }
        public int MaskImpacts { get; private set; }
        readonly Dictionary<Collider,bool> intactColliders=new Dictionary<Collider,bool>();
        readonly Dictionary<Renderer,bool> intactRenderers=new Dictionary<Renderer,bool>();
        DoorShatterDebris debris;
        void RememberDestructibleLeaves()
        {
            void Remember(Transform leaf)
            {
                if(!leaf)return;
                foreach(var c in leaf.GetComponentsInChildren<Collider>(true))
                    if(!intactColliders.ContainsKey(c))intactColliders.Add(c,c.enabled);
                foreach(var r in leaf.GetComponentsInChildren<Renderer>(true))
                    if(!intactRenderers.ContainsKey(r))intactRenderers.Add(r,r.enabled);
            }
            Remember(movingLeaf);Remember(secondaryLeaf);
        }
        void ApplyDoorDestructionGeometry(bool broken)
        {
            if(kind!=Kind.Door || !movingLeaf)throw new System.InvalidOperationException("Only ordinary movable doors can be shattered");
            RememberDestructibleLeaves();DoorBroken=broken;
            foreach(var item in intactColliders)if(item.Key)item.Key.enabled=!broken&&item.Value;
            foreach(var item in intactRenderers)if(item.Key)item.Key.enabled=!broken&&item.Value;
            if(obstacle)obstacle.enabled=!broken&&!open;
        }
        // Checkpoint clearance probes restore geometry without allocating or
        // destroying cosmetic debris, playing sound, or changing its animation.
        internal void ProbeDoorDestruction(bool broken)=>ApplyDoorDestructionGeometry(broken);
        public void RestoreDoorDestruction(bool broken)
        {
            ApplyDoorDestructionGeometry(broken);
            if(!broken && debris){debris.gameObject.SetActive(false);Destroy(debris.gameObject);debris=null;}
            else if(broken && !debris){Physics.SyncTransforms();debris=CreateDebris();debris.Seal();}
        }
        DoorShatterDebris CreateDebris()
        {
            var panels=new List<Renderer>();
            void Panel(Transform leaf)
            {
                if(!leaf)return;
                Renderer best=null;float area=0;
                foreach(var r in leaf.GetComponentsInChildren<Renderer>(true))
                {
                    if(!intactRenderers.TryGetValue(r,out bool visible)||!visible||!r.sharedMaterial)continue;
                    var b=r.bounds;float surface=b.size.y*Mathf.Max(b.size.x,b.size.z);
                    if(b.size.y>.6f && surface>area){area=surface;best=r;}
                }
                if(best)panels.Add(best);
            }
            Panel(movingLeaf);Panel(secondaryLeaf);
            return DoorShatterDebris.Create(transform,panels,string.IsNullOrEmpty(stableId)?name:stableId);
        }
        public void RestoreShatteredDoor(bool requestedOpen,Vector3 leafPosition,bool broken)
        {
            if(broken&&!requestedOpen)throw new System.ArgumentException("A shattered door cannot be closed");
            RestoreDoor(requestedOpen,leafPosition);RestoreDoorDestruction(broken);
        }
        public void RestoreSchoolShatteredDoor(bool requestedOpen,Vector3 leafPosition,Vector3 secondaryPosition,bool broken)
        {
            if(broken&&!requestedOpen)throw new System.ArgumentException("A shattered school door cannot be closed");
            RestoreSchoolDoor(requestedOpen,leafPosition,secondaryPosition);RestoreDoorDestruction(broken);
        }
        public bool BreakForMask(LanternMaskEncounter actor)
            => BreakForMask(actor, false);
        internal bool BreakForMaskOnRoute(LanternMaskEncounter actor, Vector3 destination, UnityEngine.AI.NavMeshPath route)
        {
            // Obstacle avoidance can remove all velocity just before contact.
            // A fresh native crossing route is locomotion intent for the corridor
            // runner; all contact, cover, state and pause guards still apply.
            var agent = actor ? actor.GetComponent<UnityEngine.AI.NavMeshAgent>() : null;
            bool routeContact = actor && actor.CorridorRunner && EnemyNavigation.Ready(agent) &&
                route != null && route.status == UnityEngine.AI.NavMeshPathStatus.PathComplete &&
                StalkerDoorTraversal.RouteCrossesOpening(agent, destination, this, route);
            return BreakForMask(actor, routeContact);
        }
        bool BreakForMask(LanternMaskEncounter actor, bool routeContact)
        {
            var session=GameSession.Current;
            if(!DoorOperable || IsOpen&&AtRequestedDoorPose || !actor || !actor.isActiveAndEnabled ||
                EnemyTimeStop.Active || EnemyTrapRestraint.IsHeld(actor) || actor.gameObject.scene!=gameObject.scene ||
                !session || !session.InputAllowed || session.Finished ||
                actor.State!=LanternMaskEncounter.Phase.Wander && actor.State!=LanternMaskEncounter.Phase.Investigate &&
                actor.State!=LanternMaskEncounter.Phase.Chase)return false;
            var agent=actor.GetComponent<UnityEngine.AI.NavMeshAgent>();
            var delta=actor.transform.position-transform.position;delta.y=0;
            if(!EnemyNavigation.Ready(agent) || !routeContact && (agent.isStopped || !agent.hasPath || agent.velocity.sqrMagnitude<.25f) ||
                !EnemyNavigation.SameFloor(actor.transform.position,transform.position.y) || delta.magnitude>1.75f)return false;
            var origin=actor.transform.position+Vector3.up;var target=transform.position+Vector3.up;
            if(Physics.Linecast(origin,target,out var hit,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore) &&
                !hit.collider.transform.IsChildOf(actor.transform) && hit.collider.GetComponentInParent<Interactable>()!=this)return false;
            open=true;passages.Clear();changeFrame=Time.frameCount;
            ApplyDoorDestructionGeometry(true);Physics.SyncTransforms();
            if(debris){debris.gameObject.SetActive(false);Destroy(debris.gameObject);}
            debris=CreateDebris();debris.Launch(actor.transform.forward);
            MaskImpacts++;
            var voice=actor.GetComponent<CorridorMaskAudio>();
            if(voice)voice.PlayDoorSmash(transform);else DoorDestructionAudio.PlayAt(this);
            return true;
        }
    }
}
