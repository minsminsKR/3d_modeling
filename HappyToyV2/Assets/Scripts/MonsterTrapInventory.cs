using UnityEngine;

namespace HappyToy.V2
{
    [DefaultExecutionOrder(50)]
    public sealed class MonsterTrapInventory : MonoBehaviour
    {
        // A fixed supported destination prepared before the hands begin lowering.
        // It is deliberately not checkpointed until the placement is committed.
        public struct PlacementPose
        {
            public Vector3 position;
            public Quaternion rotation;
        }
        public const int Capacity = 3;
        public int Count { get; private set; } = 1;
        public float Cooldown { get; private set; }
        public string Feedback { get; private set; } = "";
        public float FeedbackRemaining { get; private set; }
        public bool FeedbackVisible => FeedbackRemaining > 0 && GameSession.Current && GameSession.Current.InputAllowed;
        public PlacedMonsterTrap LastPlaced { get; private set; }
        public Transform WorldRoot { get; private set; }
        PlayerMotor player;
        void Awake() { player = GetComponent<PlayerMotor>(); }
        public void ConfigureWorld(Transform root) { WorldRoot = root; Count = 1; Cooldown = 0; ClearFeedback(); }
        public void Restore(int count, float cooldown)
        { if (player && player.HandActions) player.HandActions.Reset(); Count = Mathf.Clamp(count,0,Capacity); Cooldown = cooldown; ClearFeedback(); }
        public bool AddSupply()
        {
            var session = GameSession.Current;
            if (!session || !session.InputAllowed) return false;
            if (Count >= Capacity) return Deny("덫을 더 들 수 없습니다. 최대 3개까지 휴대합니다.");
            Count++; Notice("덫 +1 · 남은 덫 "+Count+"개"); return true;
        }
        void Update()
        {
            var session = GameSession.Current;
            if (!session || session.Finished) { ClearFeedback(); return; }
            if (!session.InputAllowed || session.Shell.GameplayEntryFrame == Time.frameCount) return;
            Cooldown = Mathf.Max(0,Cooldown-Time.deltaTime); FeedbackRemaining = Mathf.Max(0,FeedbackRemaining-Time.deltaTime);
            if (!session.Shell.ItemWheelOpen && session.Shell.ItemWheelChangedFrame != Time.frameCount && !player.Rituals.Burning && PlayerControls.PlaceTrap)
                player.HandActions.Request(PlayerItem.Trap);
        }
        bool CanPlace(out string reason)
        {
            reason = string.Empty;
            var session = GameSession.Current;
            if (!session || !session.InputAllowed || !player || session.Shell.ItemWheelOpen ||
                session.Shell.ItemWheelChangedFrame == Time.frameCount || player.Rituals.Burning) return false;
            if (player.Hidden) reason = "숨어 있는 동안에는 덫을 설치할 수 없습니다.";
            else if (session.EncountersResolved) reason = "이름이 복원되어 덫이 필요하지 않습니다.";
            else if (Count <= 0 && !player.Items.Unlimited) reason = "가지고 있는 덫이 없습니다.";
            else if (Cooldown > 0) reason = "다음 덫을 준비 중입니다.";
            return reason.Length == 0;
        }
        bool FootprintFree(Vector3 point, out string reason)
        {
            reason = string.Empty;
            var root = WorldRoot ? WorldRoot : GameSession.Current.transform;
            foreach (var trap in root.GetComponentsInChildren<PlacedMonsterTrap>())
                if (!trap.Spent && Vector3.Distance(trap.transform.position, point) < .7f)
                { reason = "다른 덫에서 조금 떨어져 설치하세요."; return false; }
            return true;
        }
        public bool TryPreparePlacement(out PlacementPose pose, out string reason)
        {
            pose = default;
            if (!CanPlace(out reason)) return false;
            if (!TrapPlacement.InFront(player, out var point, out var facing))
            { reason = "평평하고 비어 있는 바닥을 향해 덫을 설치하세요."; return false; }
            if (!FootprintFree(point, out reason)) return false;
            pose = new PlacementPose { position = point, rotation = facing };
            return true;
        }
        public bool CanPlaceAt(PlacementPose pose, out string reason)
        {
            if (!CanPlace(out reason)) return false;
            var offset = pose.position - player.transform.position;
            if (!CorridorCheckpoint.Vector(pose.position) || !ChapterCheckpoint.Rotation(pose.rotation) ||
                Vector3.Dot(pose.rotation * Vector3.up, Vector3.up) < .995f ||
                Vector2.Distance(new Vector2(pose.position.x, pose.position.z),
                    new Vector2(player.transform.position.x, player.transform.position.z)) > 1.45f ||
                Mathf.Abs(offset.y) > .25f || !TrapPlacement.Ground(pose.position, out var point) ||
                Vector3.Distance(point, pose.position) > .04f)
            { reason = "덫을 내려놓을 자리가 더 이상 안전하지 않습니다."; return false; }
            var from = player.transform.position + Vector3.up * 1.05f;
            var reach = pose.position + Vector3.up * .16f - from;
            if (Physics.SphereCast(from, .08f, reach.normalized, out _, reach.magnitude,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            { reason = "덫을 내려놓는 길이 막혀 있습니다."; return false; }
            return FootprintFree(pose.position, out reason);
        }
        public bool TryPlace()
        {
            if (!TryPreparePlacement(out var pose, out var reason)) return Reject(reason);
            return CommitPlacement(pose, true);
        }
        public bool TryPlaceAt(PlacementPose pose, bool playHandAction = false)
        {
            if (!CanPlaceAt(pose, out var reason)) return Reject(reason);
            return CommitPlacement(pose, playHandAction);
        }
        bool CommitPlacement(PlacementPose pose, bool playHandAction)
        {
            var session = GameSession.Current;
            if (!WorldRoot) WorldRoot = session.transform;
            LastPlaced = PlacedMonsterTrap.Create(WorldRoot, pose.position, pose.rotation, true);
            player.Items.Select(PlayerItem.Trap);
            if (playHandAction) player.Hands?.PlayAction(PlayerItem.Trap);
            if (!player.Items.Unlimited) Count--;
            Cooldown = .5f; Notice(player.Items.Unlimited ? "덫 설치 · 무한 ∞ · 몬스터를 5초간 붙잡습니다." : "덫 설치 · 남은 "+Count+"개 · 몬스터를 5초간 붙잡습니다."); return true;
        }
        bool Reject(string reason) => string.IsNullOrEmpty(reason) ? false : Deny(reason);
        bool Deny(string text) { Notice(text); return false; }
        void Notice(string text) { Feedback = text; FeedbackRemaining = 1.8f; }
        void ClearFeedback() { Feedback = ""; FeedbackRemaining = 0; }
        void OnDisable() { ClearFeedback(); }
    }
}
