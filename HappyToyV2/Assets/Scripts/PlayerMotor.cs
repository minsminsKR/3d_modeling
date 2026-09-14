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
        public bool SprintExhausted { get; private set; }
        public bool Paused => GameSession.Current && !GameSession.Current.InputAllowed;
        public Interactable Focus { get; private set; }
        public int MovementUpdates { get; private set; }
        public CollisionFlags LastCollision { get; private set; }
        CharacterController controller;
        Interactable hidingPlace;
        Vector3 hideExit;
        Vector3 moveVelocity;
        float pitch, fallSpeed;

        void Awake() { controller = GetComponent<CharacterController>(); gameObject.layer=2; Lock(GameSession.Current&&GameSession.Current.InputAllowed); }
        void Lock(bool locked) { Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None; Cursor.visible = !locked; }
        void OnDisable() { Lock(false); }
        void Update()
        {
            var keys = Keyboard.current; var mouse = Mouse.current;
            if (Paused || GameSession.Current.Finished) { Running = false; moveVelocity=Vector3.zero; Focus=null; return; }
            var delta = (mouse != null ? mouse.delta.ReadValue() : Vector2.zero) * sensitivity;
            transform.Rotate(0, delta.x, 0);
            pitch = Mathf.Clamp(pitch - delta.y, -78, 78);
            eyes.transform.localRotation = Quaternion.Euler(pitch, 0, 0);
            if (keys != null && keys.fKey.wasPressedThisFrame && !Hidden) flashlight.enabled = !flashlight.enabled;
            var move = keys == null ? Vector2.zero : new Vector2((keys.dKey.isPressed ? 1 : 0) - (keys.aKey.isPressed ? 1 : 0),
                (keys.wKey.isPressed ? 1 : 0) - (keys.sKey.isPressed ? 1 : 0));
            move = Vector2.ClampMagnitude(move, 1);
            bool sprintHeld=keys!=null&&keys.leftShiftKey.isPressed;
            // Do not oscillate around the empty threshold while Shift remains held.
            // A meaningful recovery and release are both required before another sprint.
            if(SprintExhausted&&Stamina>=.25f&&!sprintHeld)SprintExhausted=false;
            if(Stamina<=.05f)SprintExhausted=true;
            Running = !Hidden && sprintHeld && move.sqrMagnitude > .01f && !SprintExhausted;
            Stamina = Mathf.Clamp01(Stamina + Time.deltaTime * (Running ? -.18f : .12f));
            if (!Hidden)
            {
                moveVelocity = (transform.right * move.x + transform.forward * move.y) * (Running ? runSpeed : walkSpeed);
            }
            Focus = null;
            if (Hidden) Focus = hidingPlace;
            else if (Physics.Raycast(eyes.transform.position, eyes.transform.forward, out var hit, 2.2f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                Focus = hit.collider.GetComponentInParent<Interactable>();
            if (keys != null && keys.eKey.wasPressedThisFrame && Focus != null) Focus.Use(this);
        }
        void FixedUpdate()
        {
            if (Paused || Hidden || !controller.enabled || !GameSession.Current || GameSession.Current.Finished) return;
            // A fixed physics step avoids sub-millimetre Move calls losing contact at very high FPS.
            fallSpeed=controller.isGrounded?-2:fallSpeed-20*Time.fixedDeltaTime;
            LastCollision=controller.Move((moveVelocity+Vector3.up*fallSpeed)*Time.fixedDeltaTime);
            MovementUpdates++;
        }
        public void Hide(Interactable place, Vector3 inside, Vector3 exit)
        {
            foreach(var stalker in FindObjectsByType<StalkerBrain>(FindObjectsSortMode.None))stalker.ObserveHiding(exit);
            hidingPlace = place; hideExit = exit; Hidden = true; Running = false;
            moveVelocity=Vector3.zero;
            controller.enabled = false; transform.position = inside; flashlight.enabled = false;
        }
        public void LeaveHiding()
        {
            // Stay inside if another collider currently blocks the exit capsule.
            if (Physics.CheckCapsule(hideExit + Vector3.up * .4f, hideExit + Vector3.up * 1.4f, .3f, ~0, QueryTriggerInteraction.Ignore)) return;
            transform.position = hideExit; Hidden = false; hidingPlace = null; controller.enabled = true;
        }
    }
}
