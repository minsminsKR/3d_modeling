using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using static HappyToy.V2.CloudTests.RuntimeAccess;

namespace HappyToy.V2.CloudTests
{
    public sealed partial class CloudPlayModeTests
    {
        Gamepad auditPad, previousAuditPad;
        void AddAuditPad() { previousAuditPad = Gamepad.current; auditPad = InputSystem.AddDevice<Gamepad>(); }
        IEnumerator PadState(GamepadState state, float seconds = .12f)
        { InputSystem.QueueStateEvent(auditPad, state); yield return Delay(seconds); }
        IEnumerator PadPulse(GamepadButton button)
        { yield return PadState(new GamepadState().WithButton(button)); yield return PadState(new GamepadState()); }
        [TearDown]
        public void RemoveAuditPad()
        {
            if (auditPad != null && auditPad.added) InputSystem.RemoveDevice(auditPad);
            if (previousAuditPad != null && previousAuditPad.added) previousAuditPad.MakeCurrent();
            auditPad = previousAuditPad = null;
        }
        [UnityTest, Timeout(90000)]
        public IEnumerator GamepadNavigatesRealMenusAndCancelsWithoutDuplicateSubmit()
        {
            AddAuditPad(); yield return null; yield return null;
            yield return Wait(() => Get<VisualElement>(One("GameShellView"), "Root")?.panel != null, 3, "Runtime screen panel missing");
            yield return PadPulse(GamepadButton.DpadDown);
            yield return PadPulse(GamepadButton.DpadDown);
            var root = Get<VisualElement>(One("GameShellView"), "Root");
            Assert.That(((VisualElement)root.panel.focusController.focusedElement).name, Is.EqualTo("settings"));
            yield return PadState(new GamepadState().WithButton(GamepadButton.South), .65f);
            yield return PadState(new GamepadState());
            Assert.That(RecoveryPage, Is.EqualTo("Settings"), "Native gamepad submit did not open focused settings");
            yield return PadPulse(GamepadButton.East); Assert.That(RecoveryPage, Is.EqualTo("Title"));
            yield return PadPulse(GamepadButton.South);
            Assert.That(Get<bool>(session, "CorridorMode"), Is.True); Assert.That(Get<bool>(session, "ChapterMode"), Is.False);
            Assert.That(RecoveryPage, Is.EqualTo("Playing"));
            yield return PadPulse(GamepadButton.Start); Assert.That(RecoveryPage, Is.EqualTo("Pause"));
            yield return PadPulse(GamepadButton.South); Assert.That(RecoveryPage, Is.EqualTo("Playing"));
            yield return PadPulse(GamepadButton.Select); Assert.That(RecoveryPage, Is.EqualTo("Journal"));
            bool previousStance=Get<bool>(player,"Crouching");
            yield return PadPulse(GamepadButton.East); Assert.That(RecoveryPage, Is.EqualTo("Playing"));
            Assert.That(Get<bool>(player,"Crouching"), Is.EqualTo(previousStance), "Menu cancel leaked into gameplay crouch");
            Call(shell,"Pause"); Call(shell,"Restart",false); var previous=session; yield return RecoveryRebind(previous);
            var failures = new List<string>(); Call(shell, "ToggleLargeText"); yield return null;
            foreach (var size in new[] { new Vector2Int(1280,720), new Vector2Int(1024,768), new Vector2Int(2560,1080) })
                yield return CaptureMenu(One("GameShellView"), "gamepad-title-large-"+size.x+"x"+size.y+".png", size.x, size.y, failures);
            Assert.That(failures, Is.Empty);
        }
        [UnityTest, Timeout(90000)]
        public IEnumerator GamepadMovesLooksUsesDoorsAndItemsAndPausesOnDisconnect()
        {
            AddAuditPad(); Call(session, "CreateCorridor", 73); Begin();
            foreach (var actor in Components("StalkerBrain")) actor.gameObject.SetActive(false);
            yield return PadState(new GamepadState { leftStick = new Vector2(.02f,.02f), rightStick = new Vector2(.02f,.02f) });
            var origin = player.transform.position; float yaw = player.transform.eulerAngles.y;
            yield return Delay(.2f); Assert.That(Vector3.Distance(origin,player.transform.position), Is.LessThan(.06f), "Stick deadzone drift");
            yield return PadState(new GamepadState { leftStick = Vector2.up }.WithButton(GamepadButton.LeftStick), .35f);
            Assert.That(Vector3.Distance(origin,player.transform.position), Is.GreaterThan(.6f));
            Assert.That(Get<bool>(player,"Running"), Is.True); Assert.That(Get<float>(player,"Stamina"), Is.LessThan(1));
            yield return PadState(new GamepadState { rightStick = new Vector2(.5f,.4f) }, .2f);
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(yaw,player.transform.eulerAngles.y)), Is.GreaterThan(5));
            yield return PadState(new GamepadState());
            yield return PadPulse(GamepadButton.East); Assert.That(Get<bool>(player,"Crouching"), Is.True);
            var light = Get<Light>(player,"flashlight"); bool beforeLight=light.enabled;
            yield return PadPulse(GamepadButton.North); Assert.That(light.enabled, Is.EqualTo(!beforeLight));
            // Controlled placement at a real generated door, followed by actual device interaction.
            var door=Components("Interactable").First(x=>x.name=="Corridor sliding door");
            PlacePlayer(door.transform.position-door.transform.forward*1.6f, true);
            player.transform.rotation=Quaternion.LookRotation(door.transform.forward);
            yield return PadState(new GamepadState());
            Assert.That(Get<Component>(player,"Focus"), Is.SameAs(door));
            yield return PadPulse(GamepadButton.South); Assert.That(Get<bool>(door,"IsOpen"), Is.True);
            var cabinet=Components("Interactable").First(x=>x.name=="Corridor hiding cabinet");
            PlacePlayer(Get<Transform>(cabinet,"outside").position,true);
            var eyes=Get<Camera>(player,"eyes");
            var target=cabinet.GetComponentsInChildren<Collider>().First(x=>x.enabled&&!x.isTrigger).bounds.center;
            float deadline=Time.realtimeSinceStartup+3;
            while (Get<Component>(player,"Focus")!=cabinet && Time.realtimeSinceStartup<deadline)
            {
                var delta=target-eyes.transform.position;
                float yawTarget=Mathf.Atan2(delta.x,delta.z)*Mathf.Rad2Deg;
                float pitchTarget=-Mathf.Atan2(delta.y,new Vector2(delta.x,delta.z).magnitude)*Mathf.Rad2Deg;
                var stick=new Vector2(Mathf.Clamp(Mathf.DeltaAngle(player.transform.eulerAngles.y,yawTarget)/30,-1,1),
                    -Mathf.Clamp(Mathf.DeltaAngle(eyes.transform.localEulerAngles.x,pitchTarget)/30,-1,1));
                InputSystem.QueueStateEvent(auditPad,new GamepadState { rightStick=stick }); yield return null;
            }
            Assert.That(Get<Component>(player,"Focus"), Is.SameAs(cabinet));
            yield return PadState(new GamepadState()); yield return PadPulse(GamepadButton.South);
            Assert.That(Get<bool>(player,"Hidden"), Is.True);
            yield return PadPulse(GamepadButton.South); Assert.That(Get<bool>(player,"Hidden"), Is.False);
            PlacePlayer(new Vector3(200,.03f,200),true); player.transform.rotation=Quaternion.identity;
            yield return PadState(new GamepadState());
            var stock=Get<Component>(player,"Firecrackers"); var aim=Get<Component>(stock,"Aim"); int before=Get<int>(stock,"Count");
            yield return PadState(new GamepadState { leftTrigger=1 }); Assert.That(Get<bool>(aim,"Visible"), Is.True);
            yield return PadState(new GamepadState { leftTrigger=1,rightTrigger=1 });
            Assert.That(Get<int>(stock,"Count"), Is.EqualTo(before-1));
            yield return Delay(.7f); Assert.That(Get<int>(stock,"Count"), Is.EqualTo(before-1), "Held RT duplicated throw");
            yield return PadState(new GamepadState());
            yield return PadPulse(GamepadButton.Start); var paused=player.transform.position;
            yield return PadState(new GamepadState { leftStick=Vector2.up,rightStick=Vector2.one,leftTrigger=1,rightTrigger=1 },.3f);
            Assert.That(player.transform.position, Is.EqualTo(paused)); Assert.That(Get<int>(stock,"Count"), Is.EqualTo(before-1));
            Assert.That(Get<bool>(aim,"Visible"), Is.False);
            yield return PadState(new GamepadState()); yield return PadPulse(GamepadButton.Start);
            InputSystem.RemoveDevice(auditPad); yield return null; yield return null;
            Assert.That(RecoveryPage, Is.EqualTo("Pause"), "Active controller disconnect did not pause");
            yield return RecoveryPulse(Key.Escape); Assert.That(RecoveryPage, Is.EqualTo("Playing"));
            origin=player.transform.position; yield return KeysObserved(Key.W); yield return Delay(.3f); Keys();
            Assert.That(Vector3.Distance(origin,player.transform.position), Is.GreaterThan(.2f));
        }
    }
}

