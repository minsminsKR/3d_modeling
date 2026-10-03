using UnityEngine;
using UnityEngine.InputSystem;

namespace HappyToy.V2
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerMotor : MonoBehaviour
    {
        public Camera eyes;
        public Light flashlight;
        public float walkSpeed = 2.6f, runSpeed = 4.4f, sensitivity = .09f;
        public float Stamina { get; private set; } = 1;
        public bool Hidden { get; private set; }
        public bool Running { get; private set; }
        public bool Crouching { get; private set; }
        public bool StandingBlocked { get; private set; }
        public float CameraHeightOffset => Crouching ? standingHeight - crouchedHeight : 0;
        public float SightTargetHeight => Crouching ? crouchedHeight * .63f : standingHeight * .63f;
        public float FootstepNoiseRadius { get; private set; }
        public float FootstepNoiseRemaining { get; private set; }
        public event System.Action<Vector3, float> FootstepNoiseEmitted;
        public bool SprintExhausted { get; private set; }
        public bool Paused => !GameSession.Current || !GameSession.Current.InputAllowed;
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
        Interactable hidingPlace;
        Vector3 hideExit, moveVelocity;
        float pitch, fallSpeed, standingHeight, crouchedHeight;
        Vector3 standingCenter;
        readonly Collider[] stanceOverlaps = new Collider[32];
        bool flashlightBeforeHiding;

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
        void OnDisable() { moveVelocity = Vector3.zero; ActualSpeed = 0; Lock(false); }
        void Update()
        {
            var session = GameSession.Current;
            if (Paused || !eyes)
            {
                Running = false; moveVelocity = Vector3.zero; ActualSpeed = 0; Focus = null; return;
            }
            var keys = Keyboard.current;
            var mouse = Mouse.current;
            SlowRemaining = session.StoryStep >= 4 ? 0 : Mathf.Max(0, SlowRemaining - Time.deltaTime);
            FootstepNoiseRemaining = Mathf.Max(0, FootstepNoiseRemaining - Time.deltaTime);
            if (FootstepNoiseRemaining <= 0) FootstepNoiseRadius = 0;
            if (keys != null && (keys.cKey.wasPressedThisFrame || keys.leftCtrlKey.wasPressedThisFrame || keys.rightCtrlKey.wasPressedThisFrame))
                TrySetCrouching(!Crouching);
            var delta = (mouse != null ? mouse.delta.ReadValue() : Vector2.zero) * sensitivity;
            transform.Rotate(0, delta.x, 0);
            pitch = Mathf.Clamp(pitch - delta.y, -78, 78);
            eyes.transform.localRotation = Quaternion.Euler(pitch, 0, 0);
            if (keys != null && keys.fKey.wasPressedThisFrame && !Hidden && flashlight)
            {
                flashlight.enabled = !flashlight.enabled;
                Feedback.PlayFlashlight(flashlight.enabled);
            }
            var move = keys == null ? Vector2.zero : new Vector2(
                (keys.dKey.isPressed ? 1 : 0) - (keys.aKey.isPressed ? 1 : 0),
                (keys.wKey.isPressed ? 1 : 0) - (keys.sKey.isPressed ? 1 : 0));
            move = Vector2.ClampMagnitude(move, 1);
            bool sprintHeld = keys != null && (keys.leftShiftKey.isPressed || keys.rightShiftKey.isPressed);
            // Recover, then release Shift: no low-stamina sprint pulsing.
            if (SprintExhausted && Stamina >= .25f && !sprintHeld) SprintExhausted = false;
            if (Stamina <= .05f) SprintExhausted = true;
            Running = !Hidden && !Crouching && sprintHeld && move.sqrMagnitude > .01f && !SprintExhausted;
            Stamina = Mathf.Clamp01(Stamina + Time.deltaTime * (Running ? -.18f : .12f));
            moveVelocity = Hidden ? Vector3.zero :
                (transform.right * move.x + transform.forward * move.y) * (Crouching ? walkSpeed * .55f : Running ? runSpeed : walkSpeed) * MovementMultiplier;
            Focus = FindFocus();
            if (keys != null && keys.eKey.wasPressedThisFrame && Focus) Focus.Use(this);
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
            moveVelocity = Vector3.zero;
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
                return direct.collider.GetComponentInParent<Interactable>();
            // Tiny distant notes should not demand pixel-perfect aim. A wall still blocks this cast.
            if (!Physics.SphereCast(origin, .065f, direction, out var assist, 2.135f,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) return null;
            var candidate = assist.collider.GetComponentInParent<Interactable>();
            if (!candidate) return null;
            var toPoint = assist.point - origin;
            if (toPoint.sqrMagnitude > 2.2f * 2.2f) return null;
            if (Physics.Raycast(origin, toPoint.normalized, out var blocker, toPoint.magnitude + .015f,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) &&
                blocker.collider.GetComponentInParent<Interactable>() == candidate) return candidate;
            return null;
        }
        void FixedUpdate()
        {
            if (Paused || Hidden || !controller.enabled) { ActualSpeed = 0; return; }
            fallSpeed = controller.isGrounded ? -2 : Mathf.Max(fallSpeed - 20 * Time.fixedDeltaTime, -35);
            var previous = transform.position;
            LastCollision = controller.Move((moveVelocity + Vector3.up * fallSpeed) * Time.fixedDeltaTime);
            var displacement = transform.position - previous; displacement.y = 0;
            ActualSpeed = displacement.magnitude / Time.fixedDeltaTime;
            MovementUpdates++;
        }
        public void Hide(Interactable place, Vector3 inside, Vector3 exit)
        {
            if (Hidden || !place || Paused) return;
            foreach (var stalker in FindObjectsByType<StalkerBrain>(FindObjectsSortMode.None)) stalker.ObserveHiding(exit);
            hidingPlace = place; hideExit = exit; Hidden = true; Running = false;
            FootstepNoiseRemaining = FootstepNoiseRadius = 0;
            moveVelocity = Vector3.zero; ActualSpeed = 0;
            flashlightBeforeHiding = flashlight && flashlight.enabled;
            controller.enabled = false; transform.position = inside;
            if (flashlight) flashlight.enabled = false;
            Feedback.PlayHide(true);
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
            if (flashlight) flashlight.enabled = flashlightBeforeHiding;
            Feedback.PlayHide(false);
        }
    }
}
