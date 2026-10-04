using UnityEngine;
using UnityEngine.InputSystem;

namespace HappyToy.V2
{
    public sealed class FirecrackerInventory : MonoBehaviour
    {
        public int Count { get; private set; } = 2;
        public FirecrackerProjectile LastThrown { get; private set; }
        public string ActionFeedback { get; private set; } = string.Empty;
        public float FeedbackRemaining { get; private set; }
        public bool FeedbackVisible => FeedbackRemaining > 0 && !string.IsNullOrEmpty(ActionFeedback) &&
            GameSession.Current && GameSession.Current.InputAllowed;

        PlayerMotor player;
        float cooldown;

        void Awake() { player = GetComponent<PlayerMotor>(); }
        void Update()
        {
            var session = GameSession.Current;
            if (!session || session.Finished) { ClearFeedback(); return; }
            // Menus freeze the cooldown and feedback, and Q there has no effect.
            if (!session.InputAllowed) return;
            cooldown = Mathf.Max(0, cooldown - Time.deltaTime);
            FeedbackRemaining = Mathf.Max(0, FeedbackRemaining - Time.deltaTime);
            if (FeedbackRemaining <= 0) ActionFeedback = string.Empty;
            if (Keyboard.current != null && Keyboard.current.qKey.wasPressedThisFrame) TryThrow();
        }
        public bool TryThrow()
        {
            var session = GameSession.Current;
            if (!session || !session.InputAllowed || !player || !player.eyes) return false;
            if (player.Hidden) return Deny("숨어 있는 동안에는 폭죽을 던질 수 없습니다.");
            if (session.StoryStep >= 4) return Deny("이름이 복원되어 폭죽이 필요하지 않습니다.");
            if (Count <= 0) return Deny("폭죽을 모두 사용했습니다.");
            if (cooldown > 0) return Deny("다음 폭죽을 준비 중입니다. 잠시 기다리세요.");
            var eye = player.eyes.transform;
            Vector3 point = eye.position;
            if (!Physics.SphereCast(point, .08f, eye.forward, out var hit, .35f,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) point += eye.forward * .3f;
            var root = new GameObject("Thrown firecracker");
            root.transform.position = point;
            LastThrown = root.AddComponent<FirecrackerProjectile>();
            LastThrown.Launch(eye.forward * 14 + Vector3.up * 3.5f);
            Count--;
            cooldown = .5f;
            ShowFeedback("폭죽을 던졌습니다 · 남은 " + Count + "개", 2.5f);
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
        void OnDisable() { ClearFeedback(); }
    }
}
