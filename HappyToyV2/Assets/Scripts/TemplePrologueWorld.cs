using System;
using UnityEngine;

namespace HappyToy.V2
{
    // The physical prologue and its player-facing interactions. Presentation,
    // transition masking and corridor creation have separate owners.
    [DisallowMultipleComponent]
    public sealed class TemplePrologueWorld : MonoBehaviour
    {
        public static readonly Vector3 Origin = new Vector3(500, 0, 500);
        public Vector3 Spawn => Origin + new Vector3(0, .04f, -20);
        public Vector3 IncensePoint => Origin + new Vector3(0, 1.08f, 7);
        public Vector3 GatePoint => Origin + new Vector3(0, 0, -8);
        public TemplePrologueDirector Director { get; private set; }
        public bool Ready { get; private set; }
        public bool TransitionRequested { get; private set; }
        public bool IncensePlacementPending => Ready && presentation && presentation.IncensePending;
        public event Action NextDayRequested;
        public event Action CorridorRequested;
        public string Objective
        {
            get
            {
                if (!Director) return "숲길을 따라 절로 가세요.";
                switch (Director.Phase)
                {
                    case TemplePrologueSequence.Phase.FirstApproach: return "숲길을 따라 절로 가서 향을 꽂으세요.";
                    case TemplePrologueSequence.Phase.FirstPrayer: return "잠시 기도합니다.";
                    case TemplePrologueSequence.Phase.FirstDeparture: return "오늘은 돌아가고 내일 다시 오세요.";
                    case TemplePrologueSequence.Phase.SecondApproach: return "다시 절에 와서 향을 꽂으세요.";
                    case TemplePrologueSequence.Phase.SecondPrayer: return "기도하는 동안 향의 움직임을 살펴보세요.";
                    default: return "향 연기가 향하는 곳을 살펴보세요.";
                }
            }
        }
        GameSession session;
        TempleProloguePresentation presentation;
        Interactable prayerTarget, gateTarget;
        BoxCollider[] gateColliders;
        TempleGateReviewTraversal walkReview;
        float hiddenGateWait, nextGateReminder;
        bool hintedGateClearance, hintedGateGlance;

        public void Prepare(GameSession owner, TemplePrologueCheckpoint checkpoint = null)
        {
            if (Ready || session) throw new InvalidOperationException("Prologue already prepared");
            if (!owner || !owner.player || !owner.player.eyes) throw new ArgumentException("Player camera is required");
            session = owner;
            transform.position = Origin;
            Director = gameObject.AddComponent<TemplePrologueDirector>();
            if (checkpoint != null) checkpoint.Validate();
            Director.Prepare(() => !session || !session.InputAllowed || TransitionRequested || presentation && presentation.IncensePending,
                checkpoint == null ? 0 : checkpoint.phase, checkpoint == null ? 0 : checkpoint.prayerElapsed);
            presentation = gameObject.AddComponent<TempleProloguePresentation>();
            presentation.Prepare(this, session.player.eyes);
            prayerTarget = Target("향로", new Vector3(0, 1.02f, 7), new Vector3(.8f, .75f, .55f), "향 꽂고 기도하기",
                () => Director.Phase == TemplePrologueSequence.Phase.FirstApproach || Director.Phase == TemplePrologueSequence.Phase.SecondApproach,
                player => { if (Director.PlaceIncense()) session.Notify("향을 꽂고 잠시 기도합니다."); });
            gateTarget = Target("나타난 대문", new Vector3(0, 2, -7.13f), new Vector3(4.7f, 4, .35f), "문 열고 들어가기",
                () => presentation.GateVisible && Director.Phase == TemplePrologueSequence.Phase.GateRevealed &&
                    (!walkReview || !walkReview.Started),
                player => UseGate());
            foreach (float side in new[] { -1f, 1f })
            {
                var post = new GameObject("대문 기둥 충돌"); post.layer = 8;
                post.transform.SetParent(gateTarget.transform, false); post.transform.localPosition = new Vector3(side * 3.0f, 0, 0);
                post.AddComponent<BoxCollider>().size = new Vector3(1.3f, 4, .55f);
            }
            gateColliders = gateTarget.GetComponentsInChildren<BoxCollider>(true);
            gateTarget.gameObject.SetActive(false);
            Director.PhaseChanged += PhaseChanged;
            if (checkpoint == null) session.player.RelocateWorld(Spawn, Quaternion.identity);
            else
            {
                if (checkpoint.phase >= 3) presentation.BeginSecondDay();
                if (checkpoint.phase == 1 || checkpoint.phase == 2 || checkpoint.phase >= 4)
                    presentation.RestoreIncense(checkpoint.incensePlaced, session.player);
                if (checkpoint.gateVisible) { presentation.RevealGate(); gateTarget.gameObject.SetActive(true); }
                if (checkpoint.version == 3)
                {
                    SetupGateWalkReview();
                    walkReview.Restore(checkpoint.reviewGateStarted, checkpoint.reviewGateProgress, checkpoint.reviewPreviousPosition);
                    if (checkpoint.reviewGateStarted) presentation.RestoreGateSound(checkpoint.reviewGateProgress);
                }
                Physics.SyncTransforms();
                if (!session.player.CanRestoreTempleProgress(checkpoint.player)) throw new InvalidOperationException("Saved position is obstructed");
                session.player.RestoreTempleProgress(checkpoint.player);
                session.player.FlashlightSystem.Restore(checkpoint.flashlightCharge, checkpoint.flashlightPacks, checkpoint.flashlightDepletions);
            }
            Ready = true;
            session.Notify(checkpoint == null ? "숲길을 따라 절에 가서 향을 꽂으세요." : "사찰 탐색을 복원했습니다.");
        }
        public TemplePrologueCheckpoint Capture(float seconds)
        {
            if (!Ready || TransitionRequested) throw new InvalidOperationException("전환을 마친 뒤 저장하세요.");
            var light = session.player.FlashlightSystem;
            var result = new TemplePrologueCheckpoint {
                token = Guid.NewGuid().ToString("N"), phase = (int)Director.Phase, prayerElapsed = Director.PrayerElapsed,
                gateVisible = presentation.GateVisible, incensePlaced = presentation.IncensePlaced,
                version = walkReview ? 3 : 2, reviewGateWalk = walkReview,
                reviewGateStarted = walkReview && walkReview.Started,
                reviewGateProgress = walkReview ? walkReview.Progress : 0,
                reviewPreviousPosition = walkReview ? walkReview.PreviousPosition : Vector3.zero,
                seconds = seconds, player = session.player.CaptureProgress(),
                flashlightCharge = light.Charge, flashlightPacks = light.PacksCollected, flashlightDepletions = light.Depletions
            };
            result.Validate(); return result;
        }
        Interactable Target(string name, Vector3 position, Vector3 size, string label, Func<bool> available, Action<PlayerMotor> action)
        {
            var go = new GameObject(name); go.layer = 8; go.transform.SetParent(transform, false); go.transform.localPosition = position;
            go.AddComponent<BoxCollider>().size = size;
            var item = go.AddComponent<Interactable>(); item.kind = Interactable.Kind.TemplePrologue; item.label = label;
            go.AddComponent<TemplePrologueTarget>().Prepare(available, action);
            return item;
        }
        void PhaseChanged(TemplePrologueSequence.Phase phase)
        {
            if (phase == TemplePrologueSequence.Phase.FirstPrayer || phase == TemplePrologueSequence.Phase.SecondPrayer)
                presentation.BeginIncense(session.player);
            else if (phase == TemplePrologueSequence.Phase.FirstDeparture)
                session.Notify("오늘은 이만 돌아가야겠다. 내일 다시 와야지.");
            else if (phase == TemplePrologueSequence.Phase.GateRevealed)
                session.Notify("향 연기가 뒤쪽으로 흐르고 있다.");
        }
        void UseGate()
        {
            if (walkReview)
            {
                if (!walkReview.Started) { presentation.PlayGateSound(false); walkReview.Begin(); }
                return;
            }
            if (Director.EnterGate())
            { presentation.PlayGateSound(); TransitionRequested = true; CorridorRequested?.Invoke(); }
        }
        void CompleteGateWalk()
        {
            if (!Director.EnterGate()) throw new InvalidOperationException("Gate phase was lost during traversal review");
            TransitionRequested = true; CorridorRequested?.Invoke();
        }
        void Update()
        {
            if (!Ready || !session || !session.InputAllowed || TransitionRequested) return;
            if (Director.Phase == TemplePrologueSequence.Phase.FirstDeparture && session.player.transform.position.z <= Spawn.z + 1.5f)
            {
                if (Director.LeaveFirstDay()) { TransitionRequested = true; NextDayRequested?.Invoke(); }
                return;
            }
            if (Director.Phase == TemplePrologueSequence.Phase.GateRevealed && !presentation.GateVisible)
            {
                var towardGate = (GatePoint - session.player.eyes.transform.position).normalized;
                bool lookingAway = Vector3.Dot(session.player.eyes.transform.forward, towardGate) < .1f;
                bool clear = GateClearOfPlayer();
                if (lookingAway && clear)
                {
                    presentation.RevealGate(); gateTarget.gameObject.SetActive(true);
                    if (hintedGateClearance || hintedGateGlance) session.Notify("낯선 문이 나타났다.");
                }
                else
                {
                    // Keep the unseen reveal, but do not leave a player who
                    // arrived early or is watching the empty spot guessing.
                    hiddenGateWait += Time.deltaTime;
                    if (hiddenGateWait >= 8 && !clear && (!hintedGateClearance || hiddenGateWait >= nextGateReminder))
                    {
                        hintedGateClearance = true; nextGateReminder = hiddenGateWait + 20;
                        session.Notify("한 걸음 물러서 주변을 살펴보자.");
                    }
                    else if (hiddenGateWait >= 8 && clear && !lookingAway && (!hintedGateGlance || hiddenGateWait >= nextGateReminder))
                    {
                        hintedGateGlance = true; nextGateReminder = hiddenGateWait + 20;
                        session.Notify("잠시 시선을 돌려 주변을 둘러보자.");
                    }
                }
            }
        }
        bool GateClearOfPlayer()
        {
            // The gate is initially inactive, so Collider.bounds is empty.
            // Test its authored boxes in local space before activating them;
            // include the controller's skin to avoid spawning against its edge.
            var controller = session.player.GetComponent<CharacterController>();
            if (!controller) return false;
            Vector3 playerScale = controller.transform.lossyScale;
            float radius = (controller.radius + controller.skinWidth) *
                Mathf.Max(Mathf.Abs(playerScale.x), Mathf.Abs(playerScale.z));
            Vector3 center = controller.transform.TransformPoint(controller.center);
            foreach (var box in gateColliders)
            {
                Vector3 local = box.transform.InverseTransformPoint(center) - box.center;
                Vector3 scale = box.transform.lossyScale;
                float xMargin = radius / Mathf.Max(.0001f, Mathf.Abs(scale.x));
                float zMargin = radius / Mathf.Max(.0001f, Mathf.Abs(scale.z));
                if (Mathf.Abs(local.x) <= box.size.x * .5f + xMargin &&
                    Mathf.Abs(local.z) <= box.size.z * .5f + zMargin) return false;
            }
            return true;
        }
        // Called only under the fully opaque transition veil.
        public void BeginSecondDay()
        {
            if (!TransitionRequested || Director.Phase != TemplePrologueSequence.Phase.SecondApproach)
                throw new InvalidOperationException("First day has not finished");
            presentation.BeginSecondDay();
            session.player.RelocateWorld(Spawn, Quaternion.identity);
            TransitionRequested = false;
            session.Notify("다음 날 — 다시 절로 향합니다.");
        }
        public void Retire()
        {
            Ready = false;
            if (session && session.player && session.player.Items) session.player.Items.Select(PlayerItem.Snapper);
            if (presentation) presentation.ReleaseEnvironment();
            gameObject.SetActive(false);
        }
        public TempleGateReviewMotion InstallGateReview()
        {
            if (!Ready || TransitionRequested || !presentation) throw new InvalidOperationException("Temple is not ready for gate review");
            return presentation.InstallGateReview(gateTarget.GetComponent<BoxCollider>());
        }
        public TempleGateReviewMotion InstallGateWalkReview()
        {
            if (!Ready || TransitionRequested || !presentation) throw new InvalidOperationException("Temple is not ready for gate review");
            return SetupGateWalkReview();
        }
        TempleGateReviewMotion SetupGateWalkReview()
        {
            var motion = presentation.InstallGateReview(gateTarget.GetComponent<BoxCollider>());
            walkReview = gameObject.AddComponent<TempleGateReviewTraversal>();
            walkReview.Prepare(this, session, motion, CompleteGateWalk);
            return motion;
        }
        void OnDestroy()
        {
            if (Director) Director.PhaseChanged -= PhaseChanged;
        }
    }
}
