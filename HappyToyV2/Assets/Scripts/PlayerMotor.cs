using UnityEngine;
using UnityEngine.InputSystem;

namespace HappyToy.V2
{
    [RequireComponent(typeof(CharacterController))]
    public sealed partial class PlayerMotor : MonoBehaviour
    {
        public Camera eyes;
        public Light flashlight;
        public float walkSpeed = 2.6f, runSpeed = 4.4f, sensitivity = .09f;
        public float Stamina { get; private set; } = 1;
        public bool Hidden { get; private set; }
        public int HidingEntryId => hidingDecision.EntryId;
        public int HidingRolls => hidingDecision.Rolls;
        public CabinetHidingOutcome HidingOutcome => hidingDecision.Outcome;
        public bool HidingProtected => Hidden && (HidingOutcome == CabinetHidingOutcome.Quiet ||
            HidingOutcome == CabinetHidingOutcome.Survived);
        // Test/audit injection is per player and nonserialized. Normal play uses Unity's RNG.
        [System.NonSerialized] public System.Func<float> HidingRandomSample;
        readonly CabinetHidingRules.Decision hidingDecision = new CabinetHidingRules.Decision();
        // Authored upper ventilation slit. The capsule/stance stays unchanged;
        // while hidden only the eye occupies this outward-facing peek position.
        public Vector3 HiddenCameraLocalPosition => hiddenPeek ? transform.InverseTransformPoint(hiddenPeek.EyePosition) : new Vector3(.272f, 1.645f, 0);
        // A short remembered cue from an actual attack at this cabinet door.
        // This is not a query of unseen enemy awareness or general hiding safety.
        public bool HidingThreatCueActive => Hidden && hidingPlace && warnedHidingPlace == hidingPlace && Time.time < hidingThreatUntil;
        public float HidingThreatCueRemaining => HidingThreatCueActive ? Mathf.Max(0, hidingThreatUntil - Time.time) : 0;
        public bool Running { get; private set; }
        public bool Crouching { get; private set; }
        public bool StandingBlocked { get; private set; }
        public float CameraHeightOffset => Crouching ? standingHeight - crouchedHeight : 0;
        public Quaternion LookRotation => Quaternion.Euler(pitch, 0, 0);
        public float SightTargetHeight => Crouching ? crouchedHeight * .63f : standingHeight * .63f;
        public float FootstepNoiseRadius { get; private set; }
        public float FootstepNoiseRemaining { get; private set; }
        public event System.Action<Vector3, float> FootstepNoiseEmitted;
        public bool SprintExhausted { get; private set; }
        public bool Paused => !GameSession.Current || !GameSession.Current.InputAllowed ||
            GameSession.Current.ChapterMode && GameSession.Current.Chapter.FirstAppearances &&
            GameSession.Current.Chapter.FirstAppearances.CameraOwned;
        public Interactable Focus { get; private set; }
        public int MovementUpdates { get; private set; }
        public CollisionFlags LastCollision { get; private set; }
        public FirecrackerInventory Firecrackers { get; private set; }
        public PlayerFeedback Feedback { get; private set; }
        public float SlowRemaining { get; private set; }
        public float MovementMultiplier => SlowRemaining > 0 ? .5f : 1;
        public float ActualSpeed { get; private set; }
        public bool Grounded => controller && controller.enabled && controller.isGrounded;
        public void ApplyCurse(float duration) { SlowRemaining = Mathf.Max(SlowRemaining, duration); }
        CharacterController controller;
        Interactable hidingPlace, warnedHidingPlace;
        float hidingThreatUntil;
        Vector3 hideExit, moveDirection;
        bool sprintRequested;
        float pitch, fallSpeed, standingHeight, crouchedHeight;
        Vector3 standingCenter;
        readonly Collider[] stanceOverlaps = new Collider[32];
        CabinetPeekWindow hiddenPeek;
        float hiddenFacing, normalNearClip;
        Vector3 normalFlashlightPosition;
        Quaternion normalFlashlightRotation;

        void Awake()
        {
            controller = GetComponent<CharacterController>();
            standingHeight = controller.height;
            standingCenter = controller.center;
            crouchedHeight = Mathf.Min(standingHeight, Mathf.Max(controller.radius * 2 + .1f, standingHeight * .63f));
            gameObject.layer = 2;
            Firecrackers = GetComponent<FirecrackerInventory>();
            if (!Firecrackers) Firecrackers = gameObject.AddComponent<FirecrackerInventory>();
            Feedback = GetComponent<PlayerFeedback>();
            if (!Feedback) Feedback = gameObject.AddComponent<PlayerFeedback>();
            Lock(GameSession.Current && GameSession.Current.InputAllowed);
        }
        void Lock(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }
        void OnDisable() { moveDirection = Vector3.zero; sprintRequested = Running = false; ActualSpeed = 0; Lock(false); }
        void Update()
        {
            var session = GameSession.Current;
            if (Paused || !eyes || session.Shell.GameplayEntryFrame == Time.frameCount)
            {
                sprintRequested = Running = false; moveDirection = Vector3.zero; ActualSpeed = 0; Focus = null; return;
            }
            SlowRemaining = session.EncountersResolved ? 0 : Mathf.Max(0, SlowRemaining - Time.deltaTime);
            FootstepNoiseRemaining = Mathf.Max(0, FootstepNoiseRemaining - Time.deltaTime);
            if (FootstepNoiseRemaining <= 0) FootstepNoiseRadius = 0;
            if (PlayerControls.Crouch)
                TrySetCrouching(!Crouching);
            var delta = PlayerControls.Look(sensitivity);
            if (Hidden && hiddenPeek)
            {
                float yaw = Mathf.Clamp(Mathf.DeltaAngle(hiddenFacing, transform.eulerAngles.y) + delta.x, -55, 55);
                transform.rotation = Quaternion.Euler(0, hiddenFacing + yaw, 0);
            }
            else transform.Rotate(0, delta.x, 0);
            pitch = Mathf.Clamp(pitch - delta.y, Hidden && hiddenPeek ? -25 : -78, Hidden && hiddenPeek ? 25 : 78);
            eyes.transform.localRotation = Quaternion.Euler(pitch, 0, 0);
            if (PlayerControls.Flashlight && flashlight)
            {
                FlashlightSystem.Toggle();
            }
            var move = PlayerControls.Movement;
            move = Vector2.ClampMagnitude(move, 1);
            bool sprintHeld = PlayerControls.Sprint;
            // Recover, then release Shift: no low-stamina sprint pulsing.
            if (SprintExhausted && Stamina >= .25f && !sprintHeld) SprintExhausted = false;
            if (Stamina <= .05f) SprintExhausted = true;
            // Input requests speed; only the subsequent physical move proves exertion.
            // Keeping these separate also lets held input escape a wall immediately.
            sprintRequested = !Hidden && !Crouching && sprintHeld && move.sqrMagnitude > .01f && !SprintExhausted;
            moveDirection = Hidden ? Vector3.zero : transform.right * move.x + transform.forward * move.y;
            Focus = FindFocus();
            if (PlayerControls.Interact && Focus) Focus.Use(this);
        }
        /// <summary>Toggle stance without moving the feet or standing through a ceiling.</summary>
        public bool TrySetCrouching(bool crouched)
        {
            if (Paused || Hidden || !controller || !controller.enabled) return false;
            if (crouched == Crouching) return true;
            if (!crouched && CapsuleBlocked(transform.position, standingHeight, standingCenter))
            {
                StandingBlocked = true;
                GameSession.Current.Notify("머리 위가 막혀 있습니다. 낮은 자세로 이동한 뒤 다시 일어나세요.");
                return false;
            }
            Crouching = crouched; StandingBlocked = false; Running = false;
            controller.height = crouched ? crouchedHeight : standingHeight;
            controller.center = standingCenter - Vector3.up * (standingHeight - controller.height) * .5f;
            // Do not retain a sprint velocity for the fixed step immediately after crouching.
            moveDirection = Vector3.zero; sprintRequested = false;
            return true;
        }
        bool CapsuleBlocked(Vector3 feet, float height, Vector3 center)
        {
            // Inset the query slightly so existing floor/doorway contact is not an obstruction.
            float radius = Mathf.Max(.01f, controller.radius - .02f);
            Vector3 worldCenter = feet + transform.TransformVector(center);
            Vector3 up = transform.up;
            float half = Mathf.Max(0, height * .5f - controller.radius);
            int count = Physics.OverlapCapsuleNonAlloc(worldCenter - up * half, worldCenter + up * half,
                radius, stanceOverlaps, ~0, QueryTriggerInteraction.Ignore);
            // A full buffer cannot prove that all remaining colliders belong to the player.
            if (count >= stanceOverlaps.Length) return true;
            for (int i = 0; i < count; i++)
                if (stanceOverlaps[i] && !stanceOverlaps[i].transform.IsChildOf(transform)) return true;
            return false;
        }
        /// <summary>Called only when a real distance-driven foot contact plays its sound.</summary>
        public void ReportFootstep(Vector3 point, bool wet)
        {
            if (Paused || Hidden || !Grounded || ActualSpeed <= .12f) return;
            FootstepNoiseRadius = StealthRules.FootstepRadius(Crouching, Running, wet);
            FootstepNoiseRemaining = .8f;
            FootstepNoiseEmitted?.Invoke(point, FootstepNoiseRadius);
        }
        Interactable FindFocus()
        {
            if (Hidden) return hidingPlace;
            var origin = eyes.transform.position;
            var direction = eyes.transform.forward;
            // The exact ray always wins; opaque geometry cannot be bypassed by aim assistance.
            if (Physics.Raycast(origin, direction, out var direct, 2.2f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                var item=direct.collider.GetComponentInParent<Interactable>();
                return item&&item.InteractionAvailable&&item.kind!=Interactable.Kind.Decoration?item:null;
            }
            // Tiny distant notes should not demand pixel-perfect aim. A wall still blocks this cast.
            if (!Physics.SphereCast(origin, .065f, direction, out var assist, 2.135f,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) return null;
            var candidate = assist.collider.GetComponentInParent<Interactable>();
            if (!candidate || !candidate.InteractionAvailable || candidate.kind==Interactable.Kind.Decoration) return null;
            var toPoint = assist.point - origin;
            if (toPoint.sqrMagnitude > 2.2f * 2.2f) return null;
            if (Physics.Raycast(origin, toPoint.normalized, out var blocker, toPoint.magnitude + .015f,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) &&
                blocker.collider.GetComponentInParent<Interactable>() == candidate) return candidate;
            return null;
        }
        void FixedUpdate()
        {
            if (Paused) { sprintRequested = Running = false; moveDirection = Vector3.zero; ActualSpeed = 0; return; }
            if (Hidden || !controller.enabled)
            {
                Running = false; ActualSpeed = 0;
                Stamina = Mathf.Clamp01(Stamina + .12f * Time.fixedDeltaTime);
                return;
            }
            // Re-evaluate exhaustion for every physics step, including catch-up steps
            // with no intervening Update. Never retain an exhausted sprint velocity.
            bool sprintStep = sprintRequested && !SprintExhausted && !Crouching;
            float speed = Crouching ? walkSpeed * .55f : sprintStep ? runSpeed : walkSpeed;
            fallSpeed = controller.isGrounded ? -2 : Mathf.Max(fallSpeed - 20 * Time.fixedDeltaTime, -35);
            var previous = transform.position;
            LastCollision = controller.Move((moveDirection * speed * MovementMultiplier + Vector3.up * fallSpeed) * Time.fixedDeltaTime);
            var displacement = transform.position - previous; displacement.y = 0;
            ActualSpeed = displacement.magnitude / Time.fixedDeltaTime;
            // Match the foot-contact threshold. Wall slides still cost normal effort;
            // only a physically blocked/resting player recovers. Stairs need no new
            // grounded gate, and curse-slowed sprinting still spends stamina.
            Running = sprintStep && ActualSpeed > .12f;
            Stamina = Mathf.Clamp01(Stamina + Time.fixedDeltaTime * (Running ? -.18f : .12f));
            if (Stamina <= .05f) SprintExhausted = true;
            MovementUpdates++;
        }
        public void ReportHidingDoorAttack(float duration)
        {
            if (Paused || !Hidden || !hidingPlace || !StealthRules.Finite(duration) || duration <= 0) return;
            warnedHidingPlace = hidingPlace;
            hidingThreatUntil = Mathf.Max(hidingThreatUntil, Time.time + Mathf.Min(duration, 2));
        }
        public void Hide(Interactable place, Vector3 inside, Vector3 exit)
        {
            if (Hidden || !place || Paused) return;
            // Decide once on successful entry, before any observer loses the real sightline.
            // The player owns the draw: adding enemies or running more frames cannot multiply risk.
            var stalkers = FindObjectsByType<StalkerBrain>(FindObjectsSortMode.None);
            bool pursued = IsPursuedForHiding(stalkers);
            hidingDecision.Resolve(hidingDecision.EntryId + 1, pursued, HidingRandomSample ?? (() => Random.value));
            foreach (var stalker in stalkers) stalker.ObserveHiding(exit);
            if (warnedHidingPlace != place) { warnedHidingPlace = null; hidingThreatUntil = 0; }
            hidingPlace = place; hideExit = exit; Hidden = true; Running = false;
            FootstepNoiseRemaining = FootstepNoiseRadius = 0;
            moveDirection = Vector3.zero; sprintRequested = false; ActualSpeed = 0;
            hiddenPeek = place.GetComponent<CabinetPeekWindow>();
            controller.enabled = false; transform.position = inside;
            var outward = exit - inside; outward.y = 0;
            if (hiddenPeek) outward = hiddenPeek.Outward;
            if (outward.sqrMagnitude > .001f) transform.rotation = Quaternion.LookRotation(outward);
            hiddenFacing = transform.eulerAngles.y;
            pitch = 0;
            if (eyes)
            {
                normalNearClip = eyes.nearClipPlane;
                if (hiddenPeek) eyes.nearClipPlane = .01f;
                eyes.transform.localRotation = Quaternion.identity;
                eyes.transform.localPosition = HiddenCameraLocalPosition;
            }
            if (flashlight)
            {
                normalFlashlightPosition = flashlight.transform.localPosition;
                normalFlashlightRotation = flashlight.transform.localRotation;
            }
            GameSession.Current.NoteChapterAction(ChapterAction.HidingEntered);
            Feedback.PlayHide(true);
            if (HidingOutcome == CabinetHidingOutcome.Defeated)
                GameSession.Current.TryDefeat("추격 중 캐비닛 은신", CabinetHidingRules.RiskExplanation);
        }
        bool IsPursuedForHiding(StalkerBrain[] stalkers)
        {
            foreach (var stalker in stalkers)
                if (stalker.isActiveAndEnabled && stalker.player == this &&
                    EnemyNavigation.SameActorFloor(stalker.GetComponent<UnityEngine.AI.NavMeshAgent>(), transform.position, stalker.HomeFloorY) &&
                    (stalker.state == StalkerBrain.State.Chase || stalker.AttackActive)) return true;
            // The school's other mobile threats share the same single entry draw.
            foreach (var mask in FindObjectsByType<LanternMaskEncounter>(FindObjectsSortMode.None))
                if (mask.isActiveAndEnabled && EnemyNavigation.SameFloor(transform.position, mask.transform.position.y) &&
                    (mask.State == LanternMaskEncounter.Phase.Chase || mask.AttackActive)) return true;
            foreach (var angel in FindObjectsByType<WeepingAngelEncounter>(FindObjectsSortMode.None))
                if (angel.isActiveAndEnabled && EnemyNavigation.SameFloor(transform.position, angel.transform.position.y) &&
                    (angel.Moving || angel.AttackActive)) return true;
            return false;
        }
        public void LeaveHiding()
        {
            if (!Hidden || Paused) return;
            // Stay inside if another collider currently blocks the real exit capsule.
            if (CapsuleBlocked(hideExit, controller.height, controller.center))
            {
                GameSession.Current.Notify("캐비닛 앞이 막혀 있습니다. 발소리가 멀어진 뒤 다시 시도하세요."); return;
            }
            transform.position = hideExit; Hidden = false; hidingPlace = null;
            fallSpeed = -2; controller.enabled = true;
            hiddenPeek = null;
            if (eyes) eyes.nearClipPlane = normalNearClip;
            if (flashlight)
            {
                flashlight.transform.localPosition = normalFlashlightPosition;
                flashlight.transform.localRotation = normalFlashlightRotation;
            }
            Feedback.PlayHide(false);
        }
    }
}
