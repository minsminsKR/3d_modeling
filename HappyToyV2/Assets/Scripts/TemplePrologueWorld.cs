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
        public bool IsStoneApproach(Vector3 contact) => Ready && presentation && presentation.IsStoneApproach(contact);
        public event Action NextDayRequested;
        public event Action CorridorRequested;
        public string Objective
        {
            get
            {
                if (!Director) return "숲길을 따라 절로 가세요.";
                switch (Director.Phase)
                {
                    case TemplePrologueSequence.Phase.FirstApproach: return clues != null && !clues.CanPlaceIncense(Director.Phase) ? "절의 제단에 놓인 쪽지를 살펴보세요." : "제단의 향로에 향을 꽂으세요.";
                    case TemplePrologueSequence.Phase.FirstPrayer: return "잠시 기도합니다.";
                    case TemplePrologueSequence.Phase.FirstDeparture: return "오늘은 돌아가고 내일 다시 오세요.";
                    case TemplePrologueSequence.Phase.SecondApproach: return clues != null && !clues.CanPlaceIncense(Director.Phase) ? "다시 절에 가서 쪽지를 확인하세요." : "제단의 향로에 다시 향을 꽂으세요.";
                    case TemplePrologueSequence.Phase.SecondPrayer: return "기도하는 동안 향의 움직임을 살펴보세요.";
                    default: return "향 연기가 향하는 곳을 살펴보세요.";
                }
            }
        }
        GameSession session;
        TempleProloguePresentation presentation;
        TemplePrologueClueState clues;
        TempleStoryEvidence evidence;
        StoryNarration narration;
        Interactable prayerTarget, gateTarget;
        BoxCollider[] gateColliders;
        Collider noteCollider;
        TempleGateReviewTraversal walkReview;
        float hiddenGateWait, nextGateReminder;
        bool hintedGateClearance, hintedGateGlance, noticedGate;

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
            bool legacy = checkpoint != null && checkpoint.storyVersion == 0;
            clues = new TemplePrologueClueState(checkpoint != null && checkpoint.firstClueRead,
                checkpoint != null && checkpoint.secondClueRead,
                legacy ? 3 : checkpoint != null ? checkpoint.legacyClueExemptMask : 0);
            Director.SetIncensePermission(clues.CanPlaceIncense);
            narration = gameObject.AddComponent<StoryNarration>();
            narration.Prepare(() => !session || !session.InputAllowed || TransitionRequested);
            presentation = gameObject.AddComponent<TempleProloguePresentation>();
            presentation.Prepare(this, session.player.eyes);
            evidence = gameObject.AddComponent<TempleStoryEvidence>();
            evidence.Prepare();
            evidence.SetSecondDay(checkpoint != null && checkpoint.phase >= 3);
            var noteTarget = Target("이름이 비어 있는 쪽지", new Vector3(.65f, .87f, 7.46f), new Vector3(.20f, .06f, .25f),
                "쪽지 살펴보기", () => Ready && !TransitionRequested, player => ReadStoryClue());
            // A new observation must not invalidate an old saved player pose.
            // Enable the raycast target only after restore clearance succeeds.
            noteTarget.GetComponent<TemplePrologueTarget>().SetObservationOnly();
            noteCollider = noteTarget.GetComponent<Collider>();
            noteCollider.enabled = false;
            var background = gameObject.AddComponent<TempleForestBackgroundTrees>();
            background.Prepare(session.player);
            prayerTarget = Target("향로", new Vector3(0, 1.02f, 7), new Vector3(.8f, .75f, .55f), "향 꽂고 기도하기",
                () => Director.Phase == TemplePrologueSequence.Phase.FirstApproach || Director.Phase == TemplePrologueSequence.Phase.SecondApproach,
                player => {
                    if (!clues.CanPlaceIncense(Director.Phase))
                    { session.Notify("제단의 쪽지를 먼저 살펴보자."); return; }
                    if (Director.PlaceIncense()) session.Notify("향을 꽂고 잠시 기도합니다.");
                });
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
                if (checkpoint.phase == 1 || checkpoint.phase == 2 || checkpoint.phase >= 4)
                    presentation.RestoreIncense(checkpoint.incensePlaced, session.player);
            }
            gameObject.AddComponent<TempleForestPresence>().Prepare(this, session, checkpoint != null);
            // Old-save validation/relocation happens before any new trunk is
            // enabled; overlapping trunks wait until the player steps away.
            background.ActivateAtPlayer();
            noteCollider.enabled = true;
            IgnoreNotePlayerCollision();
            Ready = true;
            session.Notify(checkpoint == null ? "절의 제단에 놓인 쪽지를 먼저 살펴보세요." : "사찰 탐색을 복원했습니다. " + Objective);
            if (checkpoint == null || checkpoint.phase == 0 && !clues.CanPlaceIncense(Director.Phase))
            {
                narration.Enqueue("아이를 찾으러, 함께 숨바꼭질하던 이 절에 돌아왔다.", 5.5f);
                narration.Enqueue("얼굴은 떠오르는데 이름만 기억나지 않는다.", 5.5f);
            }
            else if (checkpoint.phase == 0 && clues.FirstClueRead ||
                     checkpoint.phase == 3 && clues.SecondClueRead)
                ShowClueNarration(checkpoint.phase >= 3);
            else if (checkpoint.phase == 3 && !clues.CanPlaceIncense(Director.Phase))
                narration.Enqueue("쪽지를 확인하고 오늘도 향을 피우자.", 5.5f);
        }
        public TemplePrologueCheckpoint Capture(float seconds)
        {
            if (!Ready || TransitionRequested) throw new InvalidOperationException("전환을 마친 뒤 저장하세요.");
            var light = session.player.FlashlightSystem;
            var result = new TemplePrologueCheckpoint {
                token = Guid.NewGuid().ToString("N"), phase = (int)Director.Phase, prayerElapsed = Director.PrayerElapsed,
                storyVersion = 2, firstClueRead = clues.FirstClueRead, secondClueRead = clues.SecondClueRead,
                legacyClueExemptMask = clues.LegacyClueExemptMask,
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
        // The same note remains readable after its first inspection. New
        // progress is accepted only in the matching day and is saved separately.
        public void ReadStoryClue()
        {
            if (!Ready || TransitionRequested || !session || !session.InputAllowed) return;
            var phase = Director.Phase;
            bool newlyRead = clues.TryRead(phase);
            ShowClueNarration((int)phase >= (int)TemplePrologueSequence.Phase.SecondApproach);
            // Rereading a clue must not erase the current phase's next lead.
            if (phase == TemplePrologueSequence.Phase.FirstDeparture)
                narration.Enqueue("이름이 떠오르지 않는다. 쪽지대로 내일 다시 오자.", 6f);
            else if (phase == TemplePrologueSequence.Phase.GateRevealed)
            {
                narration.Enqueue("향 연기가 숲길 쪽으로 흐르고 있다.", 6f);
                if (noticedGate) narration.Enqueue("저 문 안에 아이의 이름을 찾을 단서가 있을까?", 5.5f);
            }
            if (newlyRead && (phase == TemplePrologueSequence.Phase.FirstApproach ||
                phase == TemplePrologueSequence.Phase.SecondApproach))
                session.Notify("쪽지를 확인했습니다. 향로에 향을 꽂으세요.");
        }
        void ShowClueNarration(bool secondDay)
        {
            if (!secondDay)
            {
                narration.ReplaceEvidence("‘오늘과 내일, 이곳에 향을 피워 주세요.\n이름을 잊기 전에.’ 이름칸은 비어 있다.", 7f, evidence.InkTexture);
                narration.Enqueue("찾으러 온 아이의 이름이 떠오를 듯하다.", 5.5f);
            }
            else
            {
                if (clues.FirstClueRead)
                {
                    narration.ReplaceEvidence("‘오늘과 내일 이곳에 향을 피워 주세요.’\n빈 이름칸 아래에 작은 집 그림이 생겼다.", 7f, evidence.InkTexture);
                    narration.Enqueue("어제는 없던 그림이다. 누가 다녀간 걸까?", 5.5f);
                }
                else
                {
                    narration.ReplaceEvidence("‘오늘과 내일 이곳에 향을 피워 주세요.’\n빈 이름칸 아래에 작은 집 그림이 있다.", 7f, evidence.InkTexture);
                    narration.Enqueue("누가 그린 걸까?", 5.5f);
                }
            }
        }
        void PhaseChanged(TemplePrologueSequence.Phase phase)
        {
            if (phase == TemplePrologueSequence.Phase.FirstPrayer || phase == TemplePrologueSequence.Phase.SecondPrayer)
                presentation.BeginIncense(session.player);
            else if (phase == TemplePrologueSequence.Phase.FirstDeparture)
            {
                session.Notify("온 길을 따라 돌아가세요.");
                narration.Enqueue("이름이 떠오르지 않는다. 쪽지대로 내일 다시 오자.", 6f);
            }
            else if (phase == TemplePrologueSequence.Phase.GateRevealed)
            {
                session.Notify("향 연기가 숲길 쪽으로 흐르고 있다.");
                narration.Enqueue("향 연기가 숲길 쪽으로 흐르고 있다.", 6f);
            }
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
            if (Director.Phase == TemplePrologueSequence.Phase.GateRevealed && presentation.GateVisible &&
                !noticedGate && session.player.Focus == gateTarget)
            {
                noticedGate = true;
                narration.Enqueue("이런 문이 있었나…", 5.5f);
                narration.Enqueue("저 안에 아이의 이름을 찾을 단서가 있을까?", 5.5f);
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
        void IgnoreNotePlayerCollision()
        {
            if (noteCollider && session && session.player)
            {
                var controller = session.player.GetComponent<CharacterController>();
                if (controller && controller.enabled) Physics.IgnoreCollision(noteCollider, controller);
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
            evidence.SetSecondDay(true);
            narration.Replace("다시 와도 이름이 떠오르지 않는다. 쪽지를 확인해 보자.", 6f);
            session.player.RelocateWorld(Spawn, Quaternion.identity);
            IgnoreNotePlayerCollision();
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
        internal void AbortPreparation()
        {
            if (presentation) presentation.ReleaseEnvironment();
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
