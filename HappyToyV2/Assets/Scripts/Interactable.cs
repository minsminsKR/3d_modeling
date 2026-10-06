using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace HappyToy.V2
{
    public sealed class Interactable : MonoBehaviour
    {
        public enum Kind { Door, NameSlip, HidingPlace, Exit, Inspect, CorridorMemory, FirecrackerSupply, ChapterMemory, Decoration, FlashlightBattery, Candle }
        public Kind kind;
        public string label;
        public string stableId;
        [TextArea] public string inspectionText;
        public Transform movingLeaf;
        public Transform secondaryLeaf;
        public Vector3 openOffset = new Vector3(1.45f, 0, 0);
        public Transform inside, outside;
        public NavMeshObstacle obstacle;
        bool open;
        public bool IsOpen => open;
        public bool DoorOperable => isActiveAndEnabled && kind == Kind.Door && movingLeaf;
        public Vector3 DoorNormal => Vector3.Cross(transform.up, transform.TransformVector(openOffset)).normalized;
        public bool AtRequestedDoorPose => movingLeaf &&
            Vector3.Distance(movingLeaf.localPosition, closedPosition + (open ? openOffset : Vector3.zero)) < .001f &&
            (!secondaryLeaf || Vector3.Distance(secondaryLeaf.localPosition, secondaryClosed - (open ? openOffset : Vector3.zero)) < .001f);

        // An operable door is a route edge, not a permanent missing NavMesh edge.
        // Physical capsule sweeps in StalkerBrain guard the real leaf instead.
        // Restore authoring when the runtime owner is disabled or removed.
        bool rememberedCarving, authoredCarving;
        Transform capturedLeaf, capturedSecondary;
        Collider[] primaryColliders, secondaryColliders;
        int changeFrame = -1;
        readonly Dictionary<Behaviour, float> passages = new Dictionary<Behaviour, float>();
        readonly List<Behaviour> stalePassages = new List<Behaviour>();
        static int actorsFrame = -1;
        static NavMeshAgent[] actors;

        void RememberDoorGeometry()
        {
            if (capturedLeaf != movingLeaf)
            {
                capturedLeaf = movingLeaf;
                if (movingLeaf) { closedPosition = movingLeaf.localPosition; primaryColliders = movingLeaf.GetComponentsInChildren<Collider>(); }
            }
            if (capturedSecondary != secondaryLeaf)
            {
                capturedSecondary = secondaryLeaf;
                if (secondaryLeaf) { secondaryClosed = secondaryLeaf.localPosition; secondaryColliders = secondaryLeaf.GetComponentsInChildren<Collider>(); }
            }
            if (kind == Kind.Door && obstacle && !rememberedCarving)
            { authoredCarving = obstacle.carving; rememberedCarving = true; }
            if (kind == Kind.Door && obstacle && isActiveAndEnabled) obstacle.carving = false;
        }
        void OnEnable() { RememberDoorGeometry(); }
        void OnDisable()
        {
            passages.Clear();
            if (obstacle && rememberedCarving) obstacle.carving = authoredCarving;
        }
        public void HoldDoorPassage(Behaviour actor)
        {
            if (actor && actor.isActiveAndEnabled && DoorOperable) passages[actor] = Time.time;
        }
        public void ReleaseDoorPassage(Behaviour actor)
        { if (!ReferenceEquals(actor, null)) passages.Remove(actor); }
        public bool HasOtherDoorPassage(Behaviour actor)
        {
            stalePassages.Clear();
            foreach (var entry in passages)
                if (!entry.Key || !entry.Key.isActiveAndEnabled || Time.time - entry.Value > .5f ||
                    Vector3.Distance(entry.Key.transform.position, transform.position) > 4.5f) stalePassages.Add(entry.Key);
            foreach (var stale in stalePassages) passages.Remove(stale);
            foreach (var entry in passages) if (entry.Key != actor) return true;
            return false;
        }
        static NavMeshAgent[] DoorActors()
        {
            if (actorsFrame != Time.frameCount)
            { actorsFrame = Time.frameCount; actors = FindObjectsByType<NavMeshAgent>(FindObjectsSortMode.None); }
            return actors;
        }
        bool InThreshold(Vector3 feet, float radius, float height)
        {
            Vector3 local = transform.InverseTransformPoint(feet);
            Vector3 slide = openOffset.normalized;
            Vector3 normal = Vector3.Cross(Vector3.up, slide).normalized;
            float halfWidth = obstacle ? Vector3.Dot(obstacle.size, new Vector3(Mathf.Abs(slide.x), Mathf.Abs(slide.y), Mathf.Abs(slide.z))) * .5f : 1.3f;
            float halfDepth = obstacle ? Vector3.Dot(obstacle.size, new Vector3(Mathf.Abs(normal.x), Mathf.Abs(normal.y), Mathf.Abs(normal.z))) * .5f : .16f;
            return Mathf.Abs(Vector3.Dot(local, slide)) < halfWidth + radius + .05f &&
                Mathf.Abs(Vector3.Dot(local, normal)) < halfDepth + radius + .12f && local.y < 2.5f && local.y + height > .1f;
        }
        bool ThresholdOccupied()
        {
            var session = GameSession.Current;
            var player = session ? session.player : null;
            if (player && !player.Hidden)
            {
                var body = player.GetComponent<CharacterController>();
                if (InThreshold(player.transform.position, body ? body.radius : .35f, body ? body.height : 1.8f)) return true;
            }
            foreach (var actor in DoorActors())
                if (actor && actor.isActiveAndEnabled && actor.gameObject.scene == gameObject.scene &&
                    InThreshold(actor.transform.position, actor.radius, actor.height)) return true;
            return false;
        }
        static bool BodyInBounds(Bounds bounds, Vector3 feet, float radius, float height)
        {
            if (feet.y + height <= bounds.min.y + .02f || feet.y >= bounds.max.y - .02f) return false;
            float x = Mathf.Clamp(feet.x, bounds.min.x, bounds.max.x);
            float z = Mathf.Clamp(feet.z, bounds.min.z, bounds.max.z);
            return (feet.x - x) * (feet.x - x) + (feet.z - z) * (feet.z - z) < (radius + .025f) * (radius + .025f);
        }
        bool SweepOccupied(Collider[] colliders, Vector3 displacement)
        {
            if (colliders == null || displacement.sqrMagnitude < .00000001f) return false;
            var session = GameSession.Current;
            var player = session ? session.player : null;
            foreach (var collider in colliders)
            {
                if (!collider || !collider.enabled || collider.isTrigger) continue;
                var swept = collider.bounds;
                var destination = swept; destination.center += displacement;
                swept.Encapsulate(destination);
                if (player && !player.Hidden)
                {
                    var body = player.GetComponent<CharacterController>();
                    if (BodyInBounds(swept, player.transform.position, body ? body.radius : .35f, body ? body.height : 1.8f)) return true;
                }
                foreach (var actor in DoorActors())
                    if (actor && actor.isActiveAndEnabled && actor.gameObject.scene == gameObject.scene &&
                        BodyInBounds(swept, actor.transform.position, actor.radius, actor.height)) return true;
            }
            return false;
        }
        public bool CloseForPursuer(Behaviour actor)
        {
            var session = GameSession.Current;
            if (!DoorOperable || !open || !actor || !actor.isActiveAndEnabled || !session || !session.InputAllowed || session.Finished ||
                Vector3.Distance(actor.transform.position, transform.position) > 2.2f ||
                HasOtherDoorPassage(actor) || ThresholdOccupied()) return false;
            return SetDoorOpen(false);
        }
        public void RestoreDoor(bool requestedOpen, Vector3 leafPosition)
        {
            if (kind != Kind.Door || !movingLeaf) throw new System.InvalidOperationException("Not a movable door");
            RememberDoorGeometry(); passages.Clear(); changeFrame = -1;
            open = requestedOpen; movingLeaf.localPosition = leafPosition;
            if (obstacle) obstacle.enabled = !open;
        }
        public bool CanRestoreSchoolDoor(Vector3 leafPosition,Vector3 secondaryPosition,bool hasSecondary)
        {
            if(kind!=Kind.Door || !movingLeaf || hasSecondary!=(secondaryLeaf!=null)) return false;
            bool OnSlide(Vector3 pose,Vector3 origin,Vector3 offset)
            {
                float q=offset.sqrMagnitude>.0001f?Vector3.Dot(pose-origin,offset)/offset.sqrMagnitude:0;
                return q>=-.001f && q<=1.001f && Vector3.Distance(pose,origin+offset*Mathf.Clamp01(q))<.002f;
            }
            return OnSlide(leafPosition,closedPosition,openOffset) &&
                (!hasSecondary || OnSlide(secondaryPosition,secondaryClosed,-openOffset));
        }
        public void RestoreSchoolDoor(bool requestedOpen,Vector3 leafPosition,Vector3 secondaryPosition)
        {
            RestoreDoor(requestedOpen,leafPosition);
            if(secondaryLeaf) secondaryLeaf.localPosition=secondaryPosition;
        }
        public void ConfigureDoor(Transform leaf, NavMeshObstacle blocker, Vector3 offset)
        {
            kind = Kind.Door; movingLeaf = leaf; obstacle = blocker; openOffset = offset;
            RememberDoorGeometry();
        }
        public bool OpenForPursuer()
        {
            var session = GameSession.Current;
            if (!DoorOperable || open || !session || !session.InputAllowed || session.Finished) return false;
            RememberDoorGeometry();
            return SetDoorOpen(true);
        }
        bool SetDoorOpen(bool value)
        {
            // All actors and the player share one requested direction and cue.
            // Opposite requests in one engine frame cannot stack handle sounds.
            if (open == value || changeFrame == Time.frameCount) return false;
            open = value; changeFrame = Time.frameCount;
            if (obstacle) obstacle.enabled = !open;
            var audio = GetComponent<InteractionAudio>();
            if (!audio) audio = gameObject.AddComponent<InteractionAudio>();
            audio.PlayDoor(open);
            return true;
        }
        Vector3 closedPosition;
        Vector3 secondaryClosed;
        public string DisplayLabel
        {
            get
            {
                if(kind==Kind.Door)
                {
                    string room=name.StartsWith("CLASSROOM")?"교실":name.StartsWith("WASHROOM")?"화장실":name.StartsWith("INFIRMARY")?"보건실":"";
                    return room+" 문 "+(open?"닫기":"열기");
                }
                if(kind==Kind.FlashlightBattery)return "손전등 배터리 줍기";
                if(kind==Kind.Candle) { var candle=GetComponent<WaymarkCandle>(); return candle?candle.DisplayLabel:label; }
                if(kind==Kind.HidingPlace)return "캐비닛에 숨기";
                if(kind==Kind.Exit && GameSession.Current && GameSession.Current.CorridorMode)return "봉인된 회랑 문 확인";
                if(kind==Kind.Exit && GameSession.Current && GameSession.Current.ChapterMode)return "1층 출입문 확인";
                if(kind==Kind.Exit)return GameSession.Current&&GameSession.Current.StoryStep>=4?"출석함에 마지막 이름 돌려놓기":"현관 출석함 확인";
                return label;
            }
        }
        void Awake() { RememberDoorGeometry(); }
        void Update()
        {
            if (!DoorOperable) return;
            RememberDoorGeometry();
            var session = GameSession.Current;
            if (!session || !session.InputAllowed || session.Finished) return;
            // A body entering an already closing aperture makes the same real
            // leaf reopen. It is never shoved through the actor or switched off.
            if (!open && !AtRequestedDoorPose && ThresholdOccupied())
            { if (!SetDoorOpen(true)) return; }
            var next = Vector3.MoveTowards(movingLeaf.localPosition,
                closedPosition + (open ? openOffset : Vector3.zero), Time.deltaTime * 1.8f);
            var nextSecondary = secondaryLeaf ? Vector3.MoveTowards(secondaryLeaf.localPosition,
                secondaryClosed - (open ? openOffset : Vector3.zero), Time.deltaTime * 1.8f) : Vector3.zero;
            if (SweepOccupied(primaryColliders, movingLeaf.parent.TransformVector(next - movingLeaf.localPosition)) ||
                secondaryLeaf && SweepOccupied(secondaryColliders, secondaryLeaf.parent.TransformVector(nextSecondary - secondaryLeaf.localPosition))) return;
            bool moved = next != movingLeaf.localPosition || secondaryLeaf && nextSecondary != secondaryLeaf.localPosition;
            movingLeaf.localPosition = next;
            if (secondaryLeaf) secondaryLeaf.localPosition = nextSecondary;
            if (obstacle) obstacle.enabled = !open;
            if (moved) Physics.SyncTransforms();
        }
        public void Use(PlayerMotor player)
        {
            if (!player || !GameSession.Current || !GameSession.Current.InputAllowed) return;
            switch (kind)
            {
                case Kind.ChapterMemory:
                    if(GameSession.Current.Chapter && GameSession.Current.Chapter.Collect(stableId)) gameObject.SetActive(false);
                    break;
                case Kind.CorridorMemory:
                    if (GameSession.Current.Corridor && GameSession.Current.Corridor.Collect(stableId)) gameObject.SetActive(false);
                    break;
                case Kind.FirecrackerSupply:
                    if (player.Firecrackers && player.Firecrackers.AddSupply()) gameObject.SetActive(false);
                    else GameSession.Current.Notify("폭죽은 다섯 개까지 소지할 수 있습니다.");
                    break;
                case Kind.FlashlightBattery:
                    var battery = GetComponent<FlashlightBattery>();
                    if (battery) battery.TryCollect(player);
                    break;
                case Kind.Candle:
                    var candle = GetComponent<WaymarkCandle>();
                    if (candle) candle.TryIgnite(player);
                    break;
                case Kind.Door:
                    if (!DoorOperable) return;
                    RememberDoorGeometry();
                    if (open && ThresholdOccupied())
                    { GameSession.Current.Notify("문 사이에 누군가 있습니다. 조금 물러난 뒤 닫으세요."); return; }
                    if (SetDoorOpen(!open) && !open) GameSession.Current.NoteChapterAction(ChapterAction.DoorClosed);
                    break;
                case Kind.NameSlip:
                    if (GameSession.Current.Collect(stableId)) gameObject.SetActive(false);
                    break;
                case Kind.HidingPlace:
                    if (player.Hidden) player.LeaveHiding();
                    else if (inside && outside) player.Hide(this, inside.position, outside.position);
                    break;
                case Kind.Exit: GameSession.Current.TryEscape(); break;
                case Kind.Inspect:
                    GameSession.Current.Inspect(string.IsNullOrEmpty(stableId)?name:stableId,inspectionText);
                    break;
            }
        }
    }
}
