using UnityEngine;

namespace HappyToy.V2
{
    // Player input requests a use. Inventories retain authoritative stock,
    // collision and effect logic, invoked once at the authored contact marker.
    [DefaultExecutionOrder(700), DisallowMultipleComponent]
    public sealed class PlayerHandActions : MonoBehaviour
    {
        readonly HandActionTimeline timeline = new HandActionTimeline();
        PlayerMotor player;
        Transform world;
        Vector3 previousPosition;
        MonsterTrapInventory.PlacementPose placement;
        bool active, waitingForEquip, commitPending, finishAfterCommit;
        float marker;
        public bool Busy => active;
        public PlayerItem PendingItem { get; private set; }
        public bool CommitSucceeded { get; private set; }
        public int CommittedCount { get; private set; }
        public Vector3 LastCommitPosition { get; private set; }
        public Vector3 LastVisualReleasePosition { get; private set; }
        public float AnimationTime => commitPending ? marker : timeline.Elapsed;
        public bool Playing => active && !waitingForEquip;
        public bool Released => Playing && timeline.CommitDelivered && !commitPending && CommitSucceeded;
        public bool CommitPending => commitPending;
        public MonsterTrapInventory.PlacementPose Placement => placement;
        void Awake() { player = GetComponent<PlayerMotor>(); previousPosition = transform.position; }
        public bool Request(PlayerItem item)
        {
            var session = GameSession.Current;
            if ((int)item < 0 || (int)item >= PlayerItemLoadout.SlotCount || active || !isActiveAndEnabled ||
                !player || player.Hidden || player.Paused || !session || session.Shell.ItemWheelOpen ||
                player.Rituals.Burning && item != PlayerItem.Flashlight) return false;
            string reason = string.Empty;
            bool allowed;
            switch (item)
            {
                case PlayerItem.Snapper: allowed = player.Firecrackers.CanThrow(out reason); break;
                case PlayerItem.Trap:
                    allowed = player.Traps.TryPreparePlacement(out placement, out reason) && player.Traps.CanPlaceAt(placement, out reason); break;
                case PlayerItem.Incense: allowed = player.Rituals.CanUseIncense; break;
                case PlayerItem.Talisman: allowed = player.Rituals.CanUseTalisman; break;
                default: allowed = player.FlashlightSystem.CanToggle; break;
            }
            if (!allowed)
            { if (!string.IsNullOrEmpty(reason)) session.Notify(reason); return false; }
            timeline.Reset();
            PendingItem = item; CommitSucceeded = false; commitPending = finishAfterCommit = false;
            if (item != PlayerItem.Flashlight) player.Items.Select(item);
            active = waitingForEquip = true; world = player.Rituals.WorldRoot;
            previousPosition = transform.position;
            return true;
        }
        void Update()
        {
            if (!active) return;
            var session = GameSession.Current;
            bool changedWorld = !player || world != player.Rituals.WorldRoot ||
                (transform.position - previousPosition).sqrMagnitude > 4;
            previousPosition = transform.position;
            if (!session || session.Finished || changedWorld || player.Hidden ||
                !timeline.CommitDelivered && PendingItem != PlayerItem.Flashlight && player.Items.Selected != PendingItem)
            { Reset(); return; }
            if (player.Paused || session.Shell.ItemWheelOpen || Time.deltaTime <= 0) return;
            if (waitingForEquip)
            {
                if (!player.Hands || !player.Hands.ReadyForAction(PendingItem)) return;
                var clip = HandAnimationLibrary.Load().GetClip("Use" + PendingItem);
                var defaults = HandActionTiming.For(Kind(PendingItem));
                if (!timeline.TryBegin(Kind(PendingItem), new HandActionTiming(clip.duration, defaults.PrepareEnds, clip.releaseTime)))
                { Reset(); return; }
                marker = clip.releaseTime; waitingForEquip = false;
                return;
            }
            var step = timeline.Advance(Time.deltaTime);
            if (step.Commit) commitPending = true;
            if (step.Completed) finishAfterCommit = true;
        }
        // Hands (650) evaluates the exact marker pose first. The camera renders
        // after this callback, so the held object and world effect change together.
        void LateUpdate()
        {
            if (!active || !player || player.Paused || !GameSession.Current || GameSession.Current.Shell.ItemWheelOpen) return;
            if (commitPending)
            {
                bool result = false;
                switch (PendingItem)
                {
                    case PlayerItem.Snapper:
                        if (player.Hands.TryGetSnapperLaunch(out var point, out var direction))
                        { LastCommitPosition = point; result = player.Firecrackers.TryThrowFromPose(point, direction); }
                        break;
                    case PlayerItem.Trap:
                        LastCommitPosition = placement.position;
                        if (player.Hands.TryGetHeldRootWorld(PlayerItem.Trap, out var visualPoint))
                        {
                            LastVisualReleasePosition = visualPoint;
                            if (Vector3.Distance(visualPoint, placement.position + Vector3.up * .006f) < .025f)
                                result = player.Traps.TryPlaceAt(placement);
                        }
                        break;
                    case PlayerItem.Incense: result = player.Rituals.UseIncense(false); break;
                    case PlayerItem.Talisman: result = player.Rituals.UseTalisman(false); break;
                    case PlayerItem.Flashlight: result = player.FlashlightSystem.Toggle(false); break;
                }
                CommitSucceeded = result; if (result) CommittedCount++;
                commitPending = false;
                player.Hands.RefreshAfterCommit();
            }
            if (finishAfterCommit) { active = waitingForEquip = finishAfterCommit = false; }
        }
        public void Reset()
        { active = waitingForEquip = commitPending = finishAfterCommit = false; timeline.Reset(); }
        void OnDisable() { Reset(); }
        public static HandActionKind Kind(PlayerItem item)
        {
            switch (item)
            {
                case PlayerItem.Snapper: return HandActionKind.Snapper;
                case PlayerItem.Trap: return HandActionKind.Trap;
                case PlayerItem.Incense: return HandActionKind.Incense;
                case PlayerItem.Talisman: return HandActionKind.Talisman;
                default: return HandActionKind.Flashlight;
            }
        }
    }
}
