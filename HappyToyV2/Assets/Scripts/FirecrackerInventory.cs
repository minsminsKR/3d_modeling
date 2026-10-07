using UnityEngine;
using UnityEngine.InputSystem;

namespace HappyToy.V2
{
    [DefaultExecutionOrder(50)]
    public sealed class FirecrackerInventory : MonoBehaviour
    {
        public int Count { get; private set; } = 2;
        public const int Capacity = 5;
        public void SetRunStock(int count) { Count = Mathf.Clamp(count, 0, Capacity); }
        public float Cooldown => cooldown;
        public void RestoreStock(int count, float seconds)
        {
            SetRunStock(count); cooldown = seconds; throwRequested = false;
            ClearFeedback(); if (Aim) Aim.Cancel();
        }
        public bool AddSupply()
        {
            var session = GameSession.Current;
            if (!session || !session.InputAllowed || Count >= Capacity) return false;
            Count++; ShowFeedback("폭죽 +1 · " + Count + "/" + Capacity, 1.6f); return true;
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

        public bool CanAim => isActiveAndEnabled && player && player.eyes && !player.Hidden && Count > 0 && cooldown <= 0 &&
            GameSession.Current && GameSession.Current.player == player && GameSession.Current.InputAllowed && !GameSession.Current.EncountersResolved;

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
            throwRequested = PlayerControls.Throw;
        }
        // The same rendered-frame Q press uses the final player/camera pose,
        // after PlayerFeedback's stance/bob, just like the held preview.
        void LateUpdate() { if (throwRequested) { throwRequested = false; TryThrow(); } }
        public bool TryThrow()
        {
            var session = GameSession.Current;
            if (!session || !session.InputAllowed || !player || !player.eyes) return false;
            if (player.Hidden) return Deny("숨어 있는 동안에는 폭죽을 던질 수 없습니다.");
            if (session.EncountersResolved) return Deny("이름이 복원되어 폭죽이 필요하지 않습니다.");
            if (Count <= 0) return Deny("폭죽을 모두 사용했습니다.");
            if (cooldown > 0) return Deny("다음 폭죽을 준비 중입니다. 잠시 기다리세요.");
            var eye = player.eyes.transform;
            FirecrackerTrajectory.GetLaunch(eye.position, eye.forward, out var point, out var velocity);
            if (FirecrackerTrajectory.IsInitialOverlap(point)) return Deny("던질 공간이 없습니다. 벽에서 조금 물러나세요.");
            var root = new GameObject("Thrown firecracker");
            root.transform.position = point;
            LastThrown = root.AddComponent<FirecrackerProjectile>();
            LastThrown.Launch(velocity);
            Count--;
            session.NoteChapterAction(ChapterAction.FirecrackerThrown);
            cooldown = .5f;
            if (Aim) Aim.Cancel();
            ShowFeedback("폭죽 사용 · 남은 " + Count + "개", 1.6f);
            return true;
        }
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
