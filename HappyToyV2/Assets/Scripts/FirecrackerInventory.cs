using UnityEngine;
using UnityEngine.InputSystem;

namespace HappyToy.V2
{
    [DefaultExecutionOrder(50)]
    public sealed class FirecrackerInventory : MonoBehaviour
    {
        public int Count { get; private set; } = SnapperStockRules.ShotsPerPack * 2;
        public const int Capacity = SnapperStockRules.Capacity;
        public void SetRunStock(int count) { Count = Mathf.Clamp(count, 0, Capacity); }
        public float Cooldown => cooldown;
        public void RestoreStock(int count, float seconds)
        {
            if (player && player.HandActions) player.HandActions.Reset();
            SetRunStock(count); cooldown = seconds; throwRequested = false;
            ClearFeedback(); if (Aim) Aim.Cancel();
        }
        public bool AddSupply()
        {
            var session = GameSession.Current;
            if (!session || !session.InputAllowed || !SnapperStockRules.CanCollect(Count)) return false;
            Count += SnapperStockRules.ShotsPerPack; ShowFeedback("콩알탄 한 묶음 +6발 · 총 " + Count + "발", 1.6f); return true;
        }
        public FirecrackerProjectile LastThrown { get; private set; }
        public FirecrackerAim Aim { get; private set; }
        public string ActionFeedback { get; private set; } = string.Empty;
        public float FeedbackRemaining { get; private set; }
        public bool FeedbackVisible => FeedbackRemaining > 0 && !string.IsNullOrEmpty(ActionFeedback) &&
            GameSession.Current && GameSession.Current.InputAllowed;

        PlayerMotor player;
        float cooldown;
        bool throwRequested;

        public bool CanAim => isActiveAndEnabled && player && player.eyes && !player.Hidden && (Count > 0 || player.Items.Unlimited) && cooldown <= 0 &&
            GameSession.Current && GameSession.Current.player == player && GameSession.Current.InputAllowed && !GameSession.Current.Shell.ItemWheelOpen &&
            !player.Rituals.Burning && player.Items.Selected == PlayerItem.Snapper && !GameSession.Current.EncountersResolved;

        void Awake()
        {
            player = GetComponent<PlayerMotor>();
            Aim = GetComponent<FirecrackerAim>();
            if (!Aim) Aim = gameObject.AddComponent<FirecrackerAim>();
        }
        void Update()
        {
            throwRequested = false;
            var session = GameSession.Current;
            if (!session || session.Finished) { ClearFeedback(); return; }
            // Menus freeze the cooldown and feedback, and Q there has no effect.
            if (!session.InputAllowed || session.Shell.GameplayEntryFrame == Time.frameCount) return;
            cooldown = Mathf.Max(0, cooldown - Time.deltaTime);
            FeedbackRemaining = Mathf.Max(0, FeedbackRemaining - Time.deltaTime);
            if (FeedbackRemaining <= 0) ActionFeedback = string.Empty;
            throwRequested = !session.Shell.ItemWheelOpen && session.Shell.ItemWheelChangedFrame != Time.frameCount && !player.Rituals.Burning && PlayerControls.Throw;
        }
        // The same rendered-frame Q press uses the final player/camera pose,
        // after PlayerFeedback's stance/bob, just like the held preview.
        void LateUpdate()
        {
            if (!throwRequested) return;
            throwRequested = false;
            if (player && player.HandActions) player.HandActions.Request(PlayerItem.Snapper);
        }
        // A preflight never spends stock, starts cooldown, selects a slot or
        // creates a projectile. The hand timeline may safely call it twice.
        public bool CanThrow(out string reason)
        {
            reason = string.Empty;
            var session = GameSession.Current;
            if (!session || !session.InputAllowed || !player || !player.eyes || session.Shell.ItemWheelOpen ||
                session.Shell.ItemWheelChangedFrame == Time.frameCount || player.Rituals.Burning) return false;
            if (player.Hidden) reason = "숨어 있는 동안에는 콩알탄을 던질 수 없습니다.";
            else if (session.EncountersResolved) reason = "이름이 복원되어 콩알탄이 필요하지 않습니다.";
            else if (Count <= 0 && !player.Items.Unlimited) reason = "콩알탄을 모두 사용했습니다.";
            else if (cooldown > 0) reason = "다음 콩알탄을 준비 중입니다. 잠시 기다리세요.";
            return reason.Length == 0;
        }
        public bool CanThrowFromPose(Vector3 point, Vector3 forward, out string reason)
        {
            if (!CanThrow(out reason)) return false;
            if (!CorridorCheckpoint.Vector(point) || !CorridorCheckpoint.Vector(forward) || forward.sqrMagnitude < .0001f)
            { reason = "콩알탄을 놓을 손 위치가 올바르지 않습니다."; return false; }
            var eye = player.eyes.transform.position;
            var path = point - eye;
            // The isolated hand camera must not let a hand beyond a thin wall
            // create a projectile on its far side. Validate the full short path,
            // including a containing origin, as well as the final release sphere.
            if (path.magnitude > 1.1f || FirecrackerTrajectory.IsInitialOverlap(eye) ||
                FirecrackerTrajectory.IsInitialOverlap(point) ||
                path.sqrMagnitude > .000001f && Physics.SphereCast(eye, FirecrackerTrajectory.Radius,
                    path.normalized, out _, path.magnitude, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            { reason = "던질 공간이 없습니다. 벽에서 조금 물러나세요."; return false; }
            return true;
        }
        public bool TryThrow()
        {
            if (!CanThrow(out var reason)) return Reject(reason);
            var eye = player.eyes.transform;
            FirecrackerTrajectory.GetLaunch(eye.position, eye.forward, out var point, out var velocity);
            if (FirecrackerTrajectory.IsInitialOverlap(point)) return Deny("던질 공간이 없습니다. 벽에서 조금 물러나세요.");
            return CommitThrow(point, velocity, true);
        }
        // The coordinator calls this once at its authored release event. The
        // existing direct API above stays immediate for old interactions/audits.
        public bool TryThrowFromPose(Vector3 point, Vector3 forward, bool playHandAction = false)
        {
            if (!CanThrowFromPose(point, forward, out var reason)) return Reject(reason);
            return CommitThrow(point, forward.normalized * 14 + Vector3.up * 3.5f, playHandAction);
        }
        bool CommitThrow(Vector3 point, Vector3 velocity, bool playHandAction)
        {
            var session = GameSession.Current;
            var root = new GameObject("Thrown paper snapper");
            root.transform.position = point;
            LastThrown = root.AddComponent<FirecrackerProjectile>();
            LastThrown.Launch(velocity);
            if (!player.Items.Unlimited) Count--;
            player.Items.Select(PlayerItem.Snapper);
            if (playHandAction) player.Hands?.PlayAction(PlayerItem.Snapper);
            session.NoteChapterAction(ChapterAction.FirecrackerThrown);
            cooldown = .5f;
            if (Aim) Aim.Cancel();
            ShowFeedback(player.Items.Unlimited ? "콩알탄 사용 · 무한 ∞" : "콩알탄 사용 · 남은 " + Count + "발", 1.6f);
            return true;
        }
        bool Reject(string reason) => string.IsNullOrEmpty(reason) ? false : Deny(reason);
        bool Deny(string text) { ShowFeedback(text, 1.8f); return false; }
        void ShowFeedback(string text, float seconds)
        {
            // Item feedback must not replace the recovered record or journal notice.
            ActionFeedback = text;
            FeedbackRemaining = seconds;
        }
        void ClearFeedback() { ActionFeedback = string.Empty; FeedbackRemaining = 0; }
        void OnDisable() { throwRequested = false; ClearFeedback(); if (Aim) Aim.Cancel(); }
    }
}
