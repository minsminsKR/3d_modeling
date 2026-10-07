using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using static HappyToy.V2.CloudTests.RuntimeAccess;

namespace HappyToy.V2.CloudTests
{
    public sealed partial class CloudPlayModeTests
    {
        [UnityTest, Timeout(15000)]
        public IEnumerator WindowFocusChangesKeepRunAliveAndPreserveExplicitPause()
        {
            IsolateThreats(); Begin();
            Assert.That(Application.runInBackground, Is.True);
            float before = Get<float>(session, "ElapsedPlayTime");
            shell.SendMessage("OnApplicationFocus", false);
            shell.SendMessage("OnApplicationPause", true);
            yield return Delay(.25f);
            Assert.That(Get<object>(shell, "Screen").ToString(), Is.EqualTo("Playing"));
            Assert.That(Time.timeScale, Is.EqualTo(1));
            Assert.That(AudioListener.pause, Is.False);
            Assert.That(Get<float>(session, "ElapsedPlayTime"), Is.GreaterThan(before));
            shell.SendMessage("OnApplicationFocus", true);
            Call(shell, "Pause");
            shell.SendMessage("OnApplicationFocus", false);
            shell.SendMessage("OnApplicationPause", true);
            shell.SendMessage("OnApplicationPause", false);
            shell.SendMessage("OnApplicationFocus", true);
            yield return null;
            Assert.That(Get<object>(shell, "Screen").ToString(), Is.EqualTo("Pause"));
            Assert.That(Time.timeScale, Is.Zero);
            Assert.That(AudioListener.pause, Is.True);
            Call(shell, "Resume");
            Assert.That(Get<bool>(session, "InputAllowed"), Is.True);
        }

        [UnityTest, Timeout(15000)]
        public IEnumerator FocusDisabledGamepadDoesNotOpenPauseButDisconnectStillDoes()
        {
            IsolateThreats(); Begin(); AddAuditPad();
            yield return PadState(new GamepadState { rightStick = Vector2.right });
            Assert.That((bool)RequireType("PlayerControls").GetProperty("UsingGamepad").GetValue(null), Is.True);
            // Input System disables a device on focus loss without removing it.
            InputSystem.DisableDevice(auditPad);
            yield return Delay(.15f);
            Assert.That(Get<object>(shell, "Screen").ToString(), Is.EqualTo("Playing"));
            InputSystem.EnableDevice(auditPad);
            yield return PadState(new GamepadState { rightStick = Vector2.right });
            Assert.That((bool)RequireType("PlayerControls").GetProperty("UsingGamepad").GetValue(null), Is.True);
            InputSystem.RemoveDevice(auditPad);
            yield return Delay(.15f);
            Assert.That(Get<object>(shell, "Screen").ToString(), Is.EqualTo("Pause"));
        }
    }
}
