using UnityEngine;
using UnityEngine.InputSystem;

namespace HappyToy.V2
{
    // One active device for gameplay. UI Toolkit owns menu navigation/submit.
    [DefaultExecutionOrder(-100)]
    public sealed class PlayerControls : MonoBehaviour
    {
        public static bool UsingGamepad { get; private set; }
        Gamepad previousPad;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetDevice() { UsingGamepad = false; }
        // IgnoreFocus is reserved for opt-in deterministic input audits. Normal
        // play never consumes background keyboard/mouse/gamepad input.
        public static bool AcceptsInput => Application.isFocused ||
            InputSystem.settings.backgroundBehavior == InputSettings.BackgroundBehavior.IgnoreFocus;
        void Update()
        {
            var pad = Gamepad.current;
            if (UsingGamepad && previousPad != null && !previousPad.added)
            { UsingGamepad = false; GameSession.Current?.Shell?.Pause(); }
            previousPad = pad;
            if (!AcceptsInput) return;
            var keys = Keyboard.current; var mouse = Mouse.current;
            if (keys != null && keys.anyKey.wasPressedThisFrame || mouse != null &&
                (mouse.delta.ReadValue().sqrMagnitude > 1 || mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame))
                UsingGamepad = false;
            else if (pad != null && pad.enabled && (pad.leftStick.ReadValue().sqrMagnitude > .01f ||
                pad.rightStick.ReadValue().sqrMagnitude > .01f || pad.dpad.ReadValue().sqrMagnitude > .01f ||
                pad.buttonSouth.wasPressedThisFrame || pad.buttonEast.wasPressedThisFrame || pad.buttonNorth.wasPressedThisFrame ||
                pad.startButton.wasPressedThisFrame || pad.selectButton.wasPressedThisFrame ||
                pad.leftStickButton.wasPressedThisFrame || pad.leftTrigger.wasPressedThisFrame || pad.rightTrigger.wasPressedThisFrame))
                UsingGamepad = true;
            if (pad == null || !pad.enabled) UsingGamepad = false;
        }
        static Gamepad Pad => UsingGamepad ? Gamepad.current : null;
        public static Vector2 Movement => !AcceptsInput ? Vector2.zero : Pad != null ? Pad.leftStick.ReadValue() : Keyboard.current == null ? Vector2.zero : new Vector2(
            (Keyboard.current.dKey.isPressed ? 1 : 0) - (Keyboard.current.aKey.isPressed ? 1 : 0),
            (Keyboard.current.wKey.isPressed ? 1 : 0) - (Keyboard.current.sKey.isPressed ? 1 : 0));
        public static Vector2 Look(float sensitivity) => !AcceptsInput ? Vector2.zero : Pad != null ? Pad.rightStick.ReadValue() * (160f * Time.deltaTime * sensitivity / .09f) :
            (Mouse.current != null ? Mouse.current.delta.ReadValue() : Vector2.zero) * sensitivity;
        public static bool Sprint => AcceptsInput && (Pad != null ? Pad.leftStickButton.isPressed : Keyboard.current != null &&
            (Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed));
        public static bool Crouch => AcceptsInput && (Pad != null ? Pad.buttonEast.wasPressedThisFrame : Keyboard.current != null &&
            (Keyboard.current.cKey.wasPressedThisFrame || Keyboard.current.leftCtrlKey.wasPressedThisFrame || Keyboard.current.rightCtrlKey.wasPressedThisFrame));
        public static bool Interact => AcceptsInput && (Pad != null ? Pad.buttonSouth.wasPressedThisFrame : Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame);
        public static bool Flashlight => AcceptsInput && (Pad != null ? Pad.buttonNorth.wasPressedThisFrame : Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame);
        public static bool Throw => AcceptsInput && (Pad != null ? Pad.rightTrigger.wasPressedThisFrame : Keyboard.current != null && Keyboard.current.qKey.wasPressedThisFrame);
        public static bool AimHeld => AcceptsInput && (Pad != null ? Pad.leftTrigger.isPressed : Mouse.current != null && Mouse.current.rightButton.isPressed);
        public static bool AimPressed => AcceptsInput && (Pad != null ? Pad.leftTrigger.wasPressedThisFrame : Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame);
    }
}
