using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using static HappyToy.V2.CloudTests.RuntimeAccess;
using MouseButton = UnityEngine.InputSystem.LowLevel.MouseButton;

namespace HappyToy.V2.CloudTests
{
    public sealed partial class CloudPlayModeTests
    {
        // Fixture-owned devices are cleaned even if a yielded assertion aborts.
        Mouse ritualMouse, previousRitualMouse;
        void AddRitualMouse() { previousRitualMouse = Mouse.current; ritualMouse = InputSystem.AddDevice<Mouse>(); }
        void RitualMouseState(Vector2 position, bool pressed = false)
        { InputSystem.QueueStateEvent(ritualMouse, new MouseState { position = position }.WithButton(MouseButton.Left, pressed)); }
        [TearDown]
        public void RemoveRitualMouse()
        {
            if (ritualMouse != null && ritualMouse.added) InputSystem.RemoveDevice(ritualMouse);
            if (previousRitualMouse != null && previousRitualMouse.added) previousRitualMouse.MakeCurrent();
            ritualMouse = previousRitualMouse = null;
        }
        Component ItemLoadout => Get<Component>(player, "Items");
        bool WheelOpen => Get<bool>(shell, "ItemWheelOpen");
        string SelectedItem => Get<object>(ItemLoadout, "Selected").ToString();
        Vector2 RitualSlot(int slot)
        {
            float angle = slot * Mathf.PI * 2 / 5;
            return new Vector2(Screen.width, Screen.height) * .5f +
                new Vector2(Mathf.Sin(angle), Mathf.Cos(angle)) * Mathf.Min(Screen.width, Screen.height) * .30f;
        }

        [UnityTest, Timeout(90000)]
        public IEnumerator ItemWheelTabSlowsTimeSelectsOnReleaseAndRequiresFreshUsePress()
        {
            Call(session, "CreateCorridor", 73); IsolateThreats(); Begin(); AddRitualMouse();
            PlacePlayer(Get<Vector3>(Get<Component>(Get<Component>(session,"Corridor"),"Entrance"),"StartPoint"));
            Keys(); RitualMouseState(RitualSlot(0)); yield return Delay(.08f);
            Keys(Key.Tab, Key.W);
            yield return Wait(() => WheelOpen, 2, "Tab did not open the real item wheel");
            Assert.That(Time.timeScale, Is.EqualTo(.25f)); Assert.That(AudioListener.pause, Is.False);
            var origin = player.transform.position;
            var facing = player.transform.rotation;
            var rituals = Get<Component>(player, "Rituals"); int incense = Get<int>(rituals, "Incense");
            RitualMouseState(RitualSlot(2), true); yield return Delay(.15f);
            Assert.That(Get<int>(rituals, "Incense"), Is.EqualTo(incense), "Clicking the wheel consumed an item");
            Assert.That((bool)Call(ItemLoadout, "UseSelected"), Is.False);
            int snapperCount=Get<int>(Get<Component>(player,"Firecrackers"),"Count");
            int trapCount=Get<int>(Get<Component>(player,"Traps"),"Count");bool torch=Get<Light>(player,"flashlight").enabled;
            Keys(Key.Tab,Key.W,Key.Q,Key.R,Key.F,Key.E);yield return Delay(.08f);
            Assert.That(Get<int>(Get<Component>(player,"Firecrackers"),"Count"),Is.EqualTo(snapperCount));
            Assert.That(Get<int>(Get<Component>(player,"Traps"),"Count"),Is.EqualTo(trapCount));
            Assert.That(Get<Light>(player,"flashlight").enabled,Is.EqualTo(torch),"Selection leaked a flashlight action");
            var displacement=player.transform.position-origin;displacement.y=0;
            Assert.That(displacement.magnitude, Is.InRange(.03f,.30f), "Held movement must continue at quarter time while choosing an item");
            Assert.That(Quaternion.Angle(facing,player.transform.rotation),Is.LessThan(.01f),"Selecting with the mouse also turned the player");
            Keys(Key.Tab);var stopped=player.transform.position;yield return Delay(.15f);
            Assert.That(Vector2.Distance(new Vector2(stopped.x,stopped.z),new Vector2(player.transform.position.x,player.transform.position.z)),Is.LessThan(.015f),
                "Releasing W while holding Tab retained a movement command");
            Assert.That(SelectedItem, Is.EqualTo("Snapper"), "Hovering changed the equipped item before release");
            Keys(); yield return Wait(() => !WheelOpen, 2, "Releasing Tab did not close the wheel");
            Assert.That(SelectedItem, Is.EqualTo("Incense")); Assert.That(Time.timeScale, Is.EqualTo(1));
            yield return Delay(.12f);
            Assert.That(Get<int>(rituals, "Incense"), Is.EqualTo(incense), "A held selection click leaked into item use");
            RitualMouseState(RitualSlot(2)); yield return Delay(.08f);
            RitualMouseState(RitualSlot(2), true);
            yield return Wait(() => Get<int>(rituals, "Incense") == incense - 1, 2, "Fresh LMB did not use the selected incense");
            Assert.That(Get<Component>(rituals, "Smoke"), Is.Not.Null);
            yield return Delay(.2f); Assert.That(Get<int>(rituals, "Incense"), Is.EqualTo(incense - 1));
        }

        [UnityTest, Timeout(90000)]
        public IEnumerator ItemWheelGamepadCancellationPreservesPauseFocusAndDeathTimeState()
        {
            Call(session, "CreateCorridor", 73); IsolateThreats(); Begin(); AddAuditPad();
            yield return PadState(new GamepadState());
            var origin=player.transform.position;var facing=player.transform.rotation;
            yield return PadState(new GamepadState { leftStick=Vector2.up,rightStick = new Vector2(-.59f, -.81f) }.WithButton(GamepadButton.RightShoulder));
            Assert.That(WheelOpen, Is.True); Assert.That(Time.timeScale, Is.EqualTo(.25f));
            yield return Delay(.2f);
            Assert.That(Vector3.Distance(origin,player.transform.position),Is.GreaterThan(.03f),"The gamepad left stick stopped while RB selected an item");
            Assert.That(Quaternion.Angle(facing,player.transform.rotation),Is.LessThan(.01f),"The selection stick rotated the camera");
            Assert.That(SelectedItem, Is.EqualTo("Snapper"));
            Call(shell, "Pause"); Assert.That(WheelOpen, Is.False); Assert.That(Time.timeScale, Is.Zero);
            Call(shell, "Resume"); yield return Delay(.15f);
            Assert.That(WheelOpen, Is.False, "Held RB reopened after Resume"); Assert.That(Time.timeScale, Is.EqualTo(1));
            Assert.That(SelectedItem, Is.EqualTo("Snapper"));
            yield return PadState(new GamepadState());
            yield return PadState(new GamepadState { rightStick = Vector2.right }.WithButton(GamepadButton.RightShoulder));
            Assert.That(WheelOpen, Is.True);
            shell.SendMessage("OnApplicationFocus", false); yield return Delay(.1f);
            Assert.That(WheelOpen, Is.False); Assert.That(Time.timeScale, Is.EqualTo(1));
            Assert.That(UnityEngine.Cursor.visible, Is.True); Assert.That(SelectedItem, Is.EqualTo("Snapper"));
            shell.SendMessage("OnApplicationFocus", true); yield return Delay(.1f);
            Assert.That(WheelOpen, Is.False, "A cancelled held key reopened after focus return");
            yield return PadState(new GamepadState());
            yield return PadState(new GamepadState().WithButton(GamepadButton.RightShoulder));
            Assert.That(WheelOpen, Is.True);
            Assert.That((bool)Call(session, "TryDefeat", "wheel-test", "controlled wheel transition"), Is.True);
            yield return null;
            Assert.That(WheelOpen, Is.False); Assert.That(Time.timeScale, Is.Zero);
            Assert.That(Get<object>(shell, "Screen").ToString(), Is.EqualTo("Result"));
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator ItemWheelLargeTextKeepsAllFiveIconsAndDescriptionsAfterPanelRebuild()
        {
            Call(session, "CreateCorridor", 73); IsolateThreats(); Begin(); AddAuditPad();
            yield return PadState(new GamepadState());
            yield return PadState(new GamepadState().WithButton(GamepadButton.RightShoulder));
            Assert.That(WheelOpen, Is.True);
            if (!Get<bool>(shell, "LargeText")) Call(shell, "ToggleLargeText");
            if (!Get<bool>(shell, "HighContrast")) Call(shell, "ToggleHighContrast");
            var view = One("GameShellView"); var errors = new List<string>();
            foreach (var size in new[] { new Vector2Int(1280, 720), new Vector2Int(1024, 768), new Vector2Int(2560, 1080) })
            {
                yield return CaptureMenu(view, "item-wheel-large-"+size.x+"x"+size.y+".png", size.x, size.y, errors);
                var root = Get<VisualElement>(view, "Root"); var wheel = root.Q<VisualElement>("item-wheel");
                Assert.That(wheel, Is.Not.Null); Assert.That(wheel.resolvedStyle.display, Is.EqualTo(DisplayStyle.Flex));
                for (int i = 0; i < 5; i++)
                {
                    var icon = wheel.Q<Image>("item-wheel-icon-"+i);
                    Assert.That(icon, Is.Not.Null); Assert.That(icon.image, Is.Not.Null);
                    Assert.That(root.worldBound.Contains(icon.worldBound.min) && root.worldBound.Contains(icon.worldBound.max - Vector2.one), Is.True, "Item icon left safe area");
                }
                Assert.That(WheelOpen, Is.True); Assert.That(Time.timeScale, Is.EqualTo(.25f));
                // Check every centre description too, including the longer
                // incense/talisman text, rather than only the initial snapper.
                for (int slot = 0; slot < 5; slot++)
                {
                    float angle = slot * Mathf.PI * 2 / 5;
                    yield return PadState(new GamepadState { rightStick = new Vector2(Mathf.Sin(angle), Mathf.Cos(angle)) }.WithButton(GamepadButton.RightShoulder));
                    foreach (string error in CloudExperienceTests.Layout(wheel)) errors.Add(size.x+"x"+size.y+" slot "+slot+": "+error);
                }
            }
            Assert.That(errors, Is.Empty);
            yield return PadState(new GamepadState { rightStick = new Vector2(.59f, -.81f) }.WithButton(GamepadButton.RightShoulder));
            yield return PadState(new GamepadState());
            Assert.That(SelectedItem, Is.EqualTo("Incense"), "Actual RB/right-stick selection did not equip incense");
        }
    }
}
