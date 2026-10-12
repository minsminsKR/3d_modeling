using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace HappyToy.V2
{
    [DisallowMultipleComponent]
    public sealed class CorridorDrawer : MonoBehaviour
    {
        const float OpenTravel = .30f;
        public string StableId { get; private set; }
        public Transform MovingDrawer => transform;
        public Interactable ContainedPickup { get; private set; }
        public float Travel { get; private set; }
        public bool IsOpen { get; private set; }
        public bool AtRequestedPose => Mathf.Abs(Travel - (IsOpen ? OpenTravel : 0)) < .0001f;
        public bool ExposesPickup => IsOpen && Travel >= OpenTravel - .012f;
        public string DisplayLabel => "책상 서랍 " + (IsOpen ? "닫기" : "열기");
        Vector3 closedPosition, parentSlide;
        Collider[] trayColliders, pickupColliders;
        int changedFrame = -1;
        CharacterController contactPlayer;
        readonly Dictionary<Collider,bool> previousPlayerPairs=new Dictionary<Collider,bool>();

        public void Configure(string id, Transform furniture, Interactable pickup)
        {
            if (string.IsNullOrEmpty(id) || !furniture || !transform.parent)
                throw new ArgumentException("Invalid corridor drawer");
            StableId = id; ContainedPickup = pickup; closedPosition = transform.localPosition;
            // A Blender/FBX hierarchy may contain unit and axis conversion. Slide
            // in the identity wrapper's physical metres, keeping those transforms.
            parentSlide = transform.parent.InverseTransformVector(furniture.TransformVector(Vector3.back * OpenTravel));
            trayColliders = GetComponentsInChildren<Collider>(true);
            pickupColliders = pickup ? pickup.GetComponentsInChildren<Collider>(true) : Array.Empty<Collider>();
            RefreshPickup();
        }
        public bool CanRestore(float distance) => CorridorCheckpoint.Number(distance, 0, OpenTravel);
        public void Restore(bool open, float distance)
        {
            if (!CanRestore(distance)) throw new ArgumentException("Invalid drawer travel");
            IsOpen = open; Travel = distance; changedFrame = -1; ApplyPose(); RefreshPickup();
        }
        void ApplyPose() { transform.localPosition = closedPosition + parentSlide * (Travel / OpenTravel); }
        void RefreshPickup()
        {
            if (pickupColliders == null) return;
            // Keep activeSelf as the finite availability flag for old checkpoints.
            // Closed contents cannot be focused through a seam or collected by API.
            foreach (var collider in pickupColliders) if (collider) collider.enabled = ExposesPickup;
            // Contents become physical at full extension. Include their pair as
            // well so an exposed pack cannot shove the user standing at the tray.
            RefreshPlayerContacts();
        }
        public void Use(PlayerMotor player)
        {
            var session = GameSession.Current;
            if (!player || !session || !session.InputAllowed || session.Finished || player.Hidden || changedFrame == Time.frameCount) return;
            float destination = IsOpen ? 0 : OpenTravel;
            if (SweepOccupied(destination - Travel))
            { session.Notify("서랍 앞이 막혀 있습니다. 조금 물러난 뒤 여닫으세요."); return; }
            IsOpen = !IsOpen; changedFrame = Time.frameCount;
            RefreshPlayerContacts();
            RefreshPickup();
            var audio = GetComponent<InteractionAudio>();
            if (!audio) audio = gameObject.AddComponent<InteractionAudio>();
            audio.PlayDoor(IsOpen);
            if (session.Shell.ReducedMotion) { Travel = destination; ApplyPose(); RefreshPickup(); Physics.SyncTransforms(); }
        }
        static bool BodyIntersects(Bounds bounds, Vector3 feet, float radius, float height)
        {
            if (feet.y + height <= bounds.min.y + .02f || feet.y >= bounds.max.y - .02f) return false;
            float x = Mathf.Clamp(feet.x, bounds.min.x, bounds.max.x), z = Mathf.Clamp(feet.z, bounds.min.z, bounds.max.z);
            return (feet.x-x)*(feet.x-x)+(feet.z-z)*(feet.z-z) < (radius+.025f)*(radius+.025f);
        }
        bool SweepOccupied(float metres)
        {
            if (trayColliders == null || Mathf.Abs(metres) < .00001f) return false;
            Vector3 shift = transform.parent.TransformVector(parentSlide * (metres / OpenTravel));
            var actors = FindObjectsByType<NavMeshAgent>(FindObjectsSortMode.None);
            foreach (var collider in trayColliders)
            {
                if (!collider || !collider.enabled || collider.isTrigger || collider.GetComponentInParent<Interactable>() == ContainedPickup) continue;
                var swept = collider.bounds; var end = swept; end.center += shift; swept.Encapsulate(end);
                // A usable tray must open while its user stands at the handle.
                // Only that controller/tray pair is managed below; desk physics,
                // other actors and loose payloads keep their existing collision.
                foreach (var actor in actors)
                    if (actor && actor.isActiveAndEnabled && actor.gameObject.scene == gameObject.scene &&
                        BodyIntersects(swept, actor.transform.position, actor.radius, actor.height)) return true;
                // The payload moves with its tray. Unrelated physical pickups may
                // not be closed through, including a thrown firecracker nearby.
                foreach (var other in Physics.OverlapBox(swept.center, swept.extents, Quaternion.identity,
                    (1 << 8) | (1 << 9), QueryTriggerInteraction.Ignore))
                {
                    if (other.transform.IsChildOf(transform) || other.transform.IsChildOf(GetComponentInParent<CorridorFurniturePlacement>().transform)) continue;
                    var item = other.GetComponentInParent<Interactable>();
                    if (item && (item.kind == Interactable.Kind.FirecrackerSupply || item.kind == Interactable.Kind.FlashlightBattery) ||
                        other.GetComponentInParent<FirecrackerProjectile>()) return true;
                }
            }
            return false;
        }
        void Update()
        {
            RefreshPlayerContacts();
            var session = GameSession.Current;
            if (!session || !session.InputAllowed || session.Finished || AtRequestedPose) return;
            float target = IsOpen ? OpenTravel : 0;
            float next = session.Shell.ReducedMotion ? target : Mathf.MoveTowards(Travel, target, Time.deltaTime * .72f);
            if (SweepOccupied(next - Travel)) return;
            Travel = next; ApplyPose(); RefreshPickup(); Physics.SyncTransforms();
        }
        void RefreshPlayerContacts()
        {
            var session=GameSession.Current;var player=session?session.player:null;
            var body=player?player.GetComponent<CharacterController>():null;
            if(body!=contactPlayer){RestorePlayerContacts();contactPlayer=body;}
            if(!body || trayColliders==null)return;
            foreach(var collider in trayColliders)
            {
                if(!collider || collider.isTrigger)continue;
                if(!collider.enabled || !collider.gameObject.activeInHierarchy){previousPlayerPairs.Remove(collider);continue;}
                bool ignore=player && !player.Hidden && (!AtRequestedPose || BodyIntersects(collider.bounds,player.transform.position,body.radius,body.height));
                if(ignore && !previousPlayerPairs.ContainsKey(collider))
                {previousPlayerPairs.Add(collider,Physics.GetIgnoreCollision(collider,body));Physics.IgnoreCollision(collider,body,true);}
                else if(ignore && !Physics.GetIgnoreCollision(collider,body))Physics.IgnoreCollision(collider,body,true);
                else if(!ignore && previousPlayerPairs.TryGetValue(collider,out bool prior))
                {Physics.IgnoreCollision(collider,body,prior);previousPlayerPairs.Remove(collider);}
            }
        }
        void RestorePlayerContacts()
        {
            if(contactPlayer)foreach(var pair in previousPlayerPairs)if(pair.Key)Physics.IgnoreCollision(pair.Key,contactPlayer,pair.Value);
            previousPlayerPairs.Clear();contactPlayer=null;
        }
        void OnDisable()=>RestorePlayerContacts();
    }
}
