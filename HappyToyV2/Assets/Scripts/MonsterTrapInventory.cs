using UnityEngine;

namespace HappyToy.V2
{
    [DefaultExecutionOrder(50)]
    public sealed class MonsterTrapInventory : MonoBehaviour
    {
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
        public void Restore(int count, float cooldown) { Count = Mathf.Clamp(count,0,Capacity); Cooldown = cooldown; ClearFeedback(); }
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
            if (!session.Shell.ItemWheelOpen && session.Shell.ItemWheelChangedFrame != Time.frameCount && !player.Rituals.Burning && PlayerControls.PlaceTrap) TryPlace();
        }
        public bool TryPlace()
        {
            var session = GameSession.Current;
            if (!session || !session.InputAllowed || !player || session.Shell.ItemWheelOpen ||
                session.Shell.ItemWheelChangedFrame == Time.frameCount || player.Rituals.Burning) return false;
            if (!WorldRoot) WorldRoot = session.transform;
            if (player.Hidden) return Deny("숨어 있는 동안에는 덫을 설치할 수 없습니다.");
            if (session.EncountersResolved) return Deny("이름이 복원되어 덫이 필요하지 않습니다.");
            if (Count <= 0 && !player.Items.Unlimited) return Deny("가지고 있는 덫이 없습니다.");
            if (Cooldown > 0) return Deny("다음 덫을 준비 중입니다.");
            if (!TrapPlacement.InFront(player,out var point,out var facing)) return Deny("평평하고 비어 있는 바닥을 향해 덫을 설치하세요.");
            foreach (var trap in WorldRoot.GetComponentsInChildren<PlacedMonsterTrap>())
                if (!trap.Spent && Vector3.Distance(trap.transform.position,point) < .7f) return Deny("다른 덫에서 조금 떨어져 설치하세요.");
            LastPlaced = PlacedMonsterTrap.Create(WorldRoot,point,facing,true);
            player.Items.Select(PlayerItem.Trap);
            player.Hands?.PlayAction(PlayerItem.Trap);
            if (!player.Items.Unlimited) Count--;
            Cooldown = .5f; Notice(player.Items.Unlimited ? "덫 설치 · 무한 ∞ · 몬스터를 5초간 붙잡습니다." : "덫 설치 · 남은 "+Count+"개 · 몬스터를 5초간 붙잡습니다."); return true;
        }
        bool Deny(string text) { Notice(text); return false; }
        void Notice(string text) { Feedback = text; FeedbackRemaining = 1.8f; }
        void ClearFeedback() { Feedback = ""; FeedbackRemaining = 0; }
        void OnDisable() { ClearFeedback(); }
    }
}
