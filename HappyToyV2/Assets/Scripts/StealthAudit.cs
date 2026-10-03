using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace HappyToy.V2
{
    /// <summary>Opt-in real-input/physics checks. This is not a survival or balance playthrough.</summary>
    public sealed class StealthAudit : MonoBehaviour
    {
        [Serializable] sealed class Result
        {
            public string status = "FAIL";
            public string scope = "Controlled stance, headroom, actual footstep and pause integration; not enemy balance or a survival playthrough";
            public List<string> passed = new List<string>();
            public List<string> failed = new List<string>();
        }
        string output;
        Keyboard keys;
        PlayerMotor player;
        readonly Result result = new Result();
        float deadline;
        int noises;
        float lastRadius;
        bool complete;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            var args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, "-v2-stealth-output");
            if (i < 0 || i + 1 >= args.Length) return;
            Application.runInBackground = true;
            new GameObject("Stealth integration audit").AddComponent<StealthAudit>().output = args[i + 1];
        }
        IEnumerator Start()
        {
            deadline = Time.realtimeSinceStartup + 35;
            Directory.CreateDirectory(output);
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            keys = InputSystem.AddDevice<Keyboard>();
            yield return new WaitForSecondsRealtime(1);
            var session = GameSession.Current;
            if (!session || !session.player) { Check(false, "authored player exists"); Complete(); yield break; }
            player = session.player;
            player.FootstepNoiseEmitted += OnNoise;
            var controller = player.GetComponent<CharacterController>();
            var shell = session.Shell;
            shell.Begin();
            // Temporary geometry is far from authored enemies and is never saved to the scene.
            var origin = new Vector3(500, 0, 500);
            Cube("Audit floor", origin + Vector3.down * .2f, new Vector3(30, .4f, 30));
            controller.enabled = false;
            player.transform.SetPositionAndRotation(origin + Vector3.up * .02f, Quaternion.identity);
            controller.enabled = true;
            Physics.SyncTransforms();
            yield return new WaitForSecondsRealtime(.3f);
            float height = controller.height, feet = player.transform.position.y;
            Input(Key.C); yield return new WaitForSecondsRealtime(.2f); Input();
            Check(player.Crouching && controller.height < height * .7f, "C lowers the actual capsule");
            Check(Mathf.Abs(player.transform.position.y - feet) < .05f, "crouching preserves foot position");
            Check(player.eyes.transform.localPosition.y < 1.2f, "camera follows crouched stance");
            var ceiling = Cube("Audit low ceiling", player.transform.position + Vector3.up * 1.45f, new Vector3(2, .15f, 2));
            Physics.SyncTransforms();
            Check(!player.TrySetCrouching(false) && player.Crouching && player.StandingBlocked, "ceiling blocks standing");
            ceiling.SetActive(false); Physics.SyncTransforms();
            Check(player.TrySetCrouching(false) && !player.Crouching && !player.StandingBlocked, "clear headroom allows standing");
            Check(Mathf.Abs(controller.height - height) < .001f, "standing restores authored capsule height");

            int before = noises;
            Input(Key.LeftShift); yield return new WaitForSecondsRealtime(1); Input();
            Check(noises == before, "stationary sprint key emits no footsteps");
            Input(Key.W); yield return new WaitForSecondsRealtime(1.1f); Input();
            Check(noises > before && Mathf.Abs(lastRadius - 4) < .001f, "real dry walking emits four-meter steps");
            player.TrySetCrouching(true);
            before = noises;
            Input(Key.W, Key.LeftShift); yield return new WaitForSecondsRealtime(1.2f); Input();
            Check(player.Crouching && !player.Running, "sprint cannot override crouch");
            Check(noises > before && Mathf.Abs(lastRadius - 1.75f) < .001f, "real crouched contacts are quiet");

            shell.Pause(); yield return null;
            float soundTime = player.FootstepNoiseRemaining;
            before = noises;
            Input(Key.C, Key.W, Key.LeftShift); yield return new WaitForSecondsRealtime(.4f); Input();
            Check(player.Crouching && noises == before && player.FootstepNoiseRemaining == soundTime, "pause freezes stance input and noise lifetime");
            Check(!player.TrySetCrouching(false), "public stance request respects pause");
            shell.Resume(); yield return new WaitForSecondsRealtime(.1f);

            // A real blocked walk must also remain silent, not just a stationary key press.
            player.TrySetCrouching(false);
            Cube("Audit wall", player.transform.position + Vector3.forward * .65f + Vector3.up * 1.5f, new Vector3(3, 3, .2f));
            Physics.SyncTransforms();
            Input(Key.W, Key.LeftShift); yield return new WaitForSecondsRealtime(.7f);
            before = noises;
            yield return new WaitForSecondsRealtime(1.2f); Input();
            Check(noises == before && player.ActualSpeed < .12f, "sprinting into a wall emits no continuing footsteps");
            Complete();
        }
        static GameObject Cube(string label, Vector3 position, Vector3 scale)
        {
            var item = GameObject.CreatePrimitive(PrimitiveType.Cube);
            item.name = label; item.transform.position = position; item.transform.localScale = scale;
            return item;
        }
        void Input(params Key[] held) { InputSystem.QueueStateEvent(keys, new KeyboardState(held)); }
        void OnNoise(Vector3 point, float radius) { noises++; lastRadius = radius; }
        void Check(bool success, string name) { (success ? result.passed : result.failed).Add(name); }
        void Update()
        {
            if (!complete && deadline > 0 && Time.realtimeSinceStartup > deadline)
            { Check(false, "audit watchdog expired"); Complete(); }
        }
        void Complete()
        {
            if (complete) return;
            complete = true;
            if (keys != null) Input();
            result.status = result.failed.Count == 0 ? "PASS" : "FAIL";
            File.WriteAllText(Path.Combine(output, "stealth.json"), JsonUtility.ToJson(result, true));
            Application.Quit(result.failed.Count == 0 ? 0 : 1);
        }
        void OnDestroy()
        {
            if (player) player.FootstepNoiseEmitted -= OnNoise;
            if (keys != null && keys.added) InputSystem.RemoveDevice(keys);
        }
    }
}
