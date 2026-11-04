using System.Collections;
using System.Reflection;
using System.IO;
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
        [UnityTest, Timeout(180000)]
        public IEnumerator TempleFullWalkingRouteUsesKeyboardAndMouseWithoutTeleporting()
        {
            Call(session, "ConfigureRecordDirectory", NewRecordFixture());
            yield return RecoveryUiReady(); Call(shell, "RestoreDefaultSettings"); yield return RecoveryClick("begin");
            var mouse = InputSystem.AddDevice<Mouse>();
            try
            {
                var world = Get<Component>(session, "Prologue"); var director = Get<Component>(world, "Director");
                yield return KeysObserved(Key.W);
                yield return Wait(() => player.transform.position.z >= 505.0f, 20, "Walking from spawn to incense was blocked", InputDiagnostics);
                Keys(); yield return null;
                InputSystem.QueueStateEvent(mouse, new MouseState { delta = new Vector2(0, -133) });
                yield return null; InputSystem.QueueStateEvent(mouse, new MouseState()); yield return null;
                yield return Wait(() => Get<Component>(player, "Focus")?.name == "향로", 3, "Mouse could not aim at the incense target", InputDiagnostics);
                yield return RecoveryPulse(Key.E);
                yield return Wait(() => Get<object>(director, "Phase").ToString() == "FirstDeparture", 8, "First prayer did not finish");
                yield return KeysObserved(Key.S);
                yield return Wait(() => Get<object>(director, "Phase").ToString() == "SecondApproach", 20, "Walking home did not start the next day", InputDiagnostics);
                Keys(); yield return Wait(() => !Get<bool>(shell, "IsReloading"), 8, "Next day did not reveal");
                yield return KeysObserved(Key.W);
                yield return Wait(() => player.transform.position.z >= 505.0f, 20, "Second walk to incense was blocked", InputDiagnostics);
                Keys(); yield return null;
                yield return Wait(() => Get<Component>(player, "Focus")?.name == "향로", 3, "Second incense focus missing", InputDiagnostics);
                yield return RecoveryPulse(Key.E);
                yield return Wait(() => Get<object>(director, "Phase").ToString() == "GateRevealed", 8, "Second prayer did not finish");
                yield return KeysObserved(Key.S);
                yield return Wait(() => player.transform.position.z <= 494.6f, 12, "Walking back to the gate was blocked", InputDiagnostics);
                Keys(); yield return null;
                InputSystem.QueueStateEvent(mouse, new MouseState { delta = new Vector2(2000, 0) });
                yield return null; InputSystem.QueueStateEvent(mouse, new MouseState()); yield return null;
                yield return Wait(() => Get<Component>(player, "Focus")?.name == "나타난 대문", 3, "Mouse could not turn to the gate", InputDiagnostics);
                yield return RecoveryPulse(Key.E);
                yield return Wait(() => Get<bool>(session, "CorridorMode") && !Get<bool>(shell, "IsReloading"), 60, "Gate did not lead to the corridor");
            }
            finally { Keys(); if (mouse.added) InputSystem.RemoveDevice(mouse); }
        }
        [UnityTest, Timeout(90000)]
        public IEnumerator TempleSaveRejectsConcurrentCorruptionWithoutOverwriting()
        {
            string directory = NewRecordFixture(); Call(session, "ConfigureRecordDirectory", directory);
            yield return RecoveryUiReady(); yield return RecoveryClick("begin");
            string path = Path.Combine(directory, "temple-suspend-v1.json");
            const string raw = "외부에서 변경된 손상 기록";
            File.WriteAllText(path, raw);
            Call(shell, "Pause"); yield return RecoveryClick("suspend-temple");
            Assert.That(RecoveryPage, Is.EqualTo("Pause"));
            Assert.That(Get<bool>(session, "PrologueMode"), Is.True);
            Assert.That(File.ReadAllText(path), Is.EqualTo(raw));
            Assert.That(Get<string>(shell, "ReloadError"), Is.Not.Empty);
        }
        [UnityTest, Timeout(180000)]
        public IEnumerator TempleSuspendDuringPrayerRestoresOnce()
        {
            string directory = NewRecordFixture(); Call(session, "ConfigureRecordDirectory", directory);
            yield return RecoveryUiReady(); yield return RecoveryClick("begin");
            var world = Get<Component>(session, "Prologue"); var director = Get<Component>(world, "Director");
            PlacePlayer(new Vector3(500, .04f, 505.2f));
            player.GetType().GetField("pitch", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(player, 12f);
            yield return null; yield return null; yield return RecoveryPulse(Key.E);
            Assert.That(Get<object>(director, "Phase").ToString(), Is.EqualTo("FirstPrayer"));
            Call(shell, "Pause"); var source = Call(world, "Capture", Get<float>(session, "ElapsedPlayTime"));
            var previous = session; yield return RecoveryClick("suspend-temple"); yield return RecoveryRebind(previous);
            yield return RefreshSlotTitle(directory);
            Assert.That(File.Exists(Path.Combine(directory, "temple-suspend-v1.json")), Is.True);
            yield return RecoveryClick("continue-temple");
            Assert.That(RecoveryPage, Is.EqualTo("Pause"));
            Assert.That(Get<bool>(session, "PrologueMode"), Is.True);
            world = Get<Component>(session, "Prologue"); director = Get<Component>(world, "Director");
            Assert.That(Get<object>(director, "Phase").ToString(), Is.EqualTo("FirstPrayer"));
            Assert.That(Get<float>(director, "PrayerElapsed"), Is.EqualTo(Get<float>(source, "prayerElapsed")).Within(.03f));
            Assert.That(Vector3.Distance(player.transform.position, Get<Vector3>(Get<object>(source, "player"), "position")), Is.LessThan(.01f));
            Assert.That(File.Exists(Path.Combine(directory, "temple-suspend-v1.json")), Is.False);
            Assert.That(File.Exists(Path.Combine(directory, "temple-suspend-v1.json.used")), Is.True);
            yield return RecoveryClick("resume");
            yield return Wait(() => Get<object>(director, "Phase").ToString() == "FirstDeparture", 8, "Restored prayer did not finish");
        }
        [UnityTest, Timeout(180000)]
        public IEnumerator TempleRealTitleAndInteractionReachCorridor()
        {
            string directory = NewRecordFixture(); Call(session, "ConfigureRecordDirectory", directory);
            yield return RecoveryUiReady(); yield return RecoveryClick("begin");
            Assert.That(Get<bool>(session, "PrologueMode"), Is.True);
            Assert.That(Get<bool>(session, "CorridorMode"), Is.False);
            var world = Get<Component>(session, "Prologue");
            var director = Get<Component>(world, "Director");
            Assert.That(Get<object>(director, "Phase").ToString(), Is.EqualTo("FirstApproach"));
            Assert.That(Get<int>(session, "TotalRecords"), Is.Zero);
            Assert.That(world.transform.Find("나타난 대문").gameObject.activeSelf, Is.False);
            var pine = world.transform.Find("Pine 0");
            Assert.That(pine, Is.Not.Null); Assert.That(pine.GetComponent<CapsuleCollider>(), Is.Not.Null);
            var lod = pine.GetComponent<LODGroup>(); Assert.That(lod, Is.Not.Null);
            Assert.That(lod.GetLODs().Length, Is.EqualTo(2));
            Assert.That(lod.GetLODs()[1].renderers[0].GetComponent<MeshFilter>().sharedMesh.GetIndexCount(0) / 3, Is.LessThanOrEqualTo(50000));
            Assert.That(world.transform.Find("Distant child crying").GetComponent<AudioSource>().clip, Is.Not.Null);
            var origin = new Vector3(500, 0, 500);
            var start = player.transform.position;
            yield return KeysObserved(Key.W); yield return Delay(1.2f); Keys(); yield return null;
            Assert.That(player.transform.position.z, Is.GreaterThan(start.z + 1f), "Forest path blocked normal walking");
            var feedback = Get<Component>(player, "Feedback");
            Assert.That(Get<string>(feedback, "LastFootstepSurface"), Is.EqualTo("forest"));
            Assert.That(Get<AudioClip>(feedback, "LastFootstepClip"), Is.Not.Null);
            Assert.That(CloudExternalAudioTests.MatchesFamily(Get<AudioClip>(feedback, "LastFootstepClip"), "step-forest", 4), Is.True);
            PlacePlayer(origin + new Vector3(0, .04f, 5.2f));
            var pitch = player.GetType().GetField("pitch", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(pitch, Is.Not.Null); pitch.SetValue(player, 12f);
            yield return null; yield return null;
            Assert.That(Get<Component>(player, "Focus")?.name, Is.EqualTo("향로"));
            yield return RecoveryPulse(Key.E);
            Assert.That(Get<object>(director, "Phase").ToString(), Is.EqualTo("FirstPrayer"));
            yield return Wait(() => world.GetComponentInChildren(RequireType("IncenseSmokeTrail")) != null, 3, "Incense did not appear after the authored release");
            var firstSmoke = world.GetComponentInChildren(RequireType("IncenseSmokeTrail"));
            yield return Delay(1.1f);
            var positions = firstSmoke.GetType().GetField("positions", BindingFlags.Instance | BindingFlags.NonPublic);
            var firstPoints = (Vector3[])positions.GetValue(firstSmoke);
            float firstDrift = firstPoints[Get<int>(firstSmoke, "SampleCount") - 1].z - firstPoints[0].z;
            Call(shell, "Pause"); float elapsed = Get<float>(director, "PrayerElapsed");
            float smokeTime = Get<float>(firstSmoke, "SimulationTime");
            yield return Delay(.3f); Assert.That(Get<float>(director, "PrayerElapsed"), Is.EqualTo(elapsed));
            Assert.That(Get<float>(firstSmoke, "SimulationTime"), Is.EqualTo(smokeTime));
            Call(shell, "Resume");
            yield return Wait(() => Get<object>(director, "Phase").ToString() == "FirstDeparture", 8, "First prayer did not finish");
            PlacePlayer(origin + new Vector3(0, .04f, -19));
            yield return Wait(() => Get<object>(director, "Phase").ToString() == "SecondApproach" && !Get<bool>(shell, "IsReloading"), 8, "Next day did not load");
            Assert.That(player.transform.position.z, Is.LessThan(origin.z - 18));
            PlacePlayer(origin + new Vector3(0, .04f, 5.2f)); pitch.SetValue(player, 12f);
            yield return null; yield return null;
            Assert.That(Get<Component>(player, "Focus")?.name, Is.EqualTo("향로"));
            yield return RecoveryPulse(Key.E);
            yield return Wait(() => world.GetComponentInChildren(RequireType("IncenseSmokeTrail")) != null, 3, "Second incense did not appear");
            var secondSmoke = world.GetComponentInChildren(RequireType("IncenseSmokeTrail"));
            yield return Delay(1.1f);
            var secondPoints = (Vector3[])positions.GetValue(secondSmoke);
            float secondDrift = secondPoints[Get<int>(secondSmoke, "SampleCount") - 1].z - secondPoints[0].z;
            Assert.That(secondDrift, Is.LessThan(firstDrift - .12f), "Second-day smoke did not flow behind the player");
            yield return Wait(() => Get<object>(director, "Phase").ToString() == "GateRevealed", 8, "Second prayer did not finish");
            yield return null;
            Assert.That(world.transform.Find("나타난 대문").gameObject.activeSelf, Is.True, "The unseen gate did not appear behind the player");
            PlacePlayer(origin + new Vector3(0, .04f, -5.4f)); player.transform.rotation = Quaternion.Euler(0, 180, 0); pitch.SetValue(player, 0f);
            yield return null; yield return null;
            Assert.That(Get<Component>(player, "Focus")?.name, Is.EqualTo("나타난 대문"));
            yield return RecoveryPulse(Key.E);
            yield return Wait(() => Get<bool>(session, "CorridorMode") && !Get<bool>(shell, "IsReloading"), 60, "Gate did not enter the real corridor");
            Assert.That(Get<bool>(session, "PrologueMode"), Is.False);
            Assert.That(Get<object>(shell, "Screen").ToString(), Is.EqualTo("Playing"));
            Assert.That(Get<int>(session, "TotalRecords"), Is.EqualTo(4));
        }
    }
}
