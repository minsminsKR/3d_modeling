using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using static HappyToy.V2.CloudTests.RuntimeAccess;
using Object = UnityEngine.Object;
using MouseButton = UnityEngine.InputSystem.LowLevel.MouseButton;

namespace HappyToy.V2.CloudTests
{
    // Explicitly controlled physics/camera/encounter fixtures. The existing ordinary
    // seven-record route remains the survival gate; these fixtures do not replace it.
    internal static class CloudFirecrackerAimTests
    {
        internal sealed class AimMouse : IDisposable
        {
            readonly Mouse previous = Mouse.current;
            internal readonly Mouse Device = InputSystem.AddDevice<Mouse>();
            internal void Hold(bool held)
            {
                Assert.That(Mouse.current, Is.SameAs(Device));
                InputSystem.QueueStateEvent(Device, new MouseState().WithButton(MouseButton.Right, held));
            }
            internal void LookWhileHeld(Vector2 delta)
            {
                Assert.That(Mouse.current, Is.SameAs(Device));
                InputSystem.QueueStateEvent(Device, new MouseState { delta = delta }.WithButton(MouseButton.Right));
            }
            public void Dispose()
            {
                if (Device.added) InputSystem.RemoveDevice(Device);
                if (previous != null && previous.added) previous.MakeCurrent();
            }
        }
        [Serializable] internal sealed class ContactSample
        {
            public string surface;
            public float requestedFrameSeconds, observedFrameSeconds, predictedTime, actualTime, centerError, pointError, normalError;
            public Vector3 predictedCenter, actualCenter, predictedContact, actualContact;
        }
        [Serializable] internal sealed class ContactReport
        {
            public bool controlledPhysicsFixture = true, hardwareFrameRateCertification = false, survivalEvidence = false;
            public string scope = "Real Unity spherecast forecasts versus real projectile Update first contacts; no bounce/landing guarantee";
            public float simulationStepSeconds = .02f, fuseHorizonSeconds = 1.2f;
            public List<ContactSample> samples = new List<ContactSample>();
        }
    }

    public sealed partial class CloudPlayModeTests
    {
        Component CurrentAim => Get<Component>(Get<Component>(player, "Firecrackers"), "Aim");
        IEnumerator AimFrames(int count = 3) { for (int i = 0; i < count; i++) yield return null; }
        IEnumerator AimHold(CloudFirecrackerAimTests.AimMouse mouse, bool held)
        {
            mouse.Hold(held);
            yield return Wait(() => mouse.Device.rightButton.isPressed == held, 2, "Queued RMB never reached the actual input device");
            yield return AimFrames(); // Include LateUpdate, where the production preview consumes the final eye pose.
        }
        IEnumerator FreshAim(CloudFirecrackerAimTests.AimMouse mouse)
        {
            yield return AimHold(mouse, false); yield return AimHold(mouse, true);
            Assert.That(Get<bool>(CurrentAim, "Visible"), Is.True, "Fresh eligible RMB press did not show its world forecast");
        }
        void AssertAimHidden(string reason)
        {
            var aim = CurrentAim;
            Assert.That(Get<bool>(aim, "Visible"), Is.False, reason);
            var line = Get<LineRenderer>(aim, "TrajectoryRenderer");
            var ring = Get<LineRenderer>(aim, "ContactRenderer");
            Assert.That(!line || !line.enabled || !line.gameObject.activeInHierarchy, Is.True, reason + " left a trajectory renderer");
            Assert.That(!ring || !ring.enabled || !ring.gameObject.activeInHierarchy, Is.True, reason + " left a contact renderer");
        }
        void AimFixturePose(Vector3 at, Vector3 direction)
        {
            ((Behaviour)player).enabled = false;
            PlacePlayer(at, false);
            Get<Camera>(player, "eyes").transform.rotation = Quaternion.LookRotation(direction);
            Physics.SyncTransforms();
        }
        object Forecast(Vector3 eyePosition, Vector3 forward, out Vector3 position, out Vector3 velocity, out Vector3[] points)
        {
            var type = RequireType("FirecrackerTrajectory");
            object[] launch = { eyePosition, forward, Vector3.zero, Vector3.zero };
            Call(type, "GetLaunch", launch);
            position = (Vector3)launch[2]; velocity = (Vector3)launch[3]; points = new Vector3[61];
            return Call(type, "Forecast", position, velocity, points);
        }

        [UnityTest, Timeout(100000)]
        public IEnumerator HeldAimCancelsAcrossRealInputMenusHidingDeathAndRetryWithoutSpending()
        {
            IsolateThreats(); yield return RecoveryUiReady();
            using (var mouse = new CloudFirecrackerAimTests.AimMouse())
            {
                var inventory = Get<Component>(player, "Firecrackers");
                yield return AimHold(mouse, true); AssertAimHidden("Title");
                yield return RecoveryClick("begin"); yield return AimFrames();
                IsolateThreats();
                // The production title now enters a corridor with one initial item.
                int initialStock = Get<int>(inventory, "Count");
                Assert.That(initialStock, Is.EqualTo(Get<bool>(session, "CorridorMode") ? 1 : 2));
                AssertAimHidden("RMB inherited from title");
                yield return FreshAim(mouse);
                Quaternion beforeLook = Get<Camera>(player, "eyes").transform.rotation;
                Vector3 beforeForecast = Get<Vector3>(CurrentAim, "EndPosition");
                mouse.LookWhileHeld(new Vector2(0, -180)); yield return AimFrames();
                Assert.That(Quaternion.Angle(beforeLook, Get<Camera>(player, "eyes").transform.rotation), Is.GreaterThan(1), "RMB preview swallowed actual mouse look");
                Assert.That(Vector3.Distance(beforeForecast, Get<Vector3>(CurrentAim, "EndPosition")), Is.GreaterThan(.05f), "Forecast did not follow actual mouse look");
                Assert.That(Get<int>(inventory, "Count"), Is.EqualTo(initialStock));
                yield return AimHold(mouse, false); AssertAimHidden("Release cancel");
                Assert.That(Get<bool>(inventory, "CanAim"), Is.True, "Cancel consumed cooldown");
                Assert.That(Get<Component>(inventory, "LastThrown"), Is.Null);
                Assert.That(Components("FirecrackerProjectile"), Is.Empty);
                yield return FreshAim(mouse);
                yield return RecoveryPulse(Key.Escape); AssertAimHidden("Escape pause");
                yield return RecoveryClick("settings"); AssertAimHidden("Settings");
                yield return RecoveryClick("back"); yield return RecoveryClick("resume");
                yield return AimFrames(); AssertAimHidden("Held RMB after resume");
                yield return FreshAim(mouse);
                yield return RecoveryPulse(Key.J); AssertAimHidden("Journal");
                yield return RecoveryPulse(Key.J); yield return AimFrames(); AssertAimHidden("Held RMB after journal");
                yield return FreshAim(mouse);
                var cabinet = Get<bool>(session, "CorridorMode") ? Components("Interactable").First(item => item.name == "Corridor hiding cabinet") :
                    Components("Interactable").Single(item => item.name == "음악실 은신함");
                PlacePlayer(Get<Transform>(cabinet, "outside").position);
                Call(cabinet, "Use", player); Assert.That(Get<bool>(player, "Hidden"), Is.True);
                yield return AimFrames(); AssertAimHidden("Cabinet entry");
                Call(cabinet, "Use", player); Assert.That(Get<bool>(player, "Hidden"), Is.False);
                yield return AimFrames(); AssertAimHidden("Held RMB after cabinet exit");
                Assert.That(Get<int>(inventory, "Count"), Is.EqualTo(initialStock));
                Assert.That(Get<bool>(inventory, "CanAim"), Is.True);
                yield return FreshAim(mouse);
                var repeatedLine = Get<LineRenderer>(CurrentAim, "TrajectoryRenderer");
                var repeatedRing = Get<LineRenderer>(CurrentAim, "ContactRenderer");
                var repeatedMaterial = repeatedLine.sharedMaterial;
                for (int i = 0; i < 3; i++)
                {
                    yield return AimHold(mouse, false); AssertAimHidden("Repeated release"); yield return FreshAim(mouse);
                    Assert.That(Get<LineRenderer>(CurrentAim, "TrajectoryRenderer"), Is.SameAs(repeatedLine));
                    Assert.That(Get<LineRenderer>(CurrentAim, "ContactRenderer"), Is.SameAs(repeatedRing));
                    Assert.That(repeatedLine.sharedMaterial, Is.SameAs(repeatedMaterial));
                    Assert.That(CurrentAim.GetComponentsInChildren<LineRenderer>(true).Length, Is.EqualTo(2), "Repeated aim duplicated native renderers");
                }
                ((Behaviour)CurrentAim).enabled = false; yield return AimFrames(); AssertAimHidden("Aim component disable");
                ((Behaviour)CurrentAim).enabled = true; yield return AimFrames(); AssertAimHidden("Held RMB after component re-enable");
                yield return FreshAim(mouse);
                var oldAim = CurrentAim;
                var oldLine = Get<LineRenderer>(oldAim, "TrajectoryRenderer");
                var oldMaterial = oldLine.sharedMaterial;
                // Genuine sight/attack death, with close-range placement explicitly a fixture.
                Vector3 origin = MainCorridorPoint(); AimFixturePose(origin + Vector3.right * 1.15f, Vector3.right);
                Get<Light>(player, "flashlight").enabled = true;
                var enemy = StalkerAt(origin); enemy.transform.rotation = Quaternion.LookRotation(Vector3.right);
                yield return Wait(() => Get<bool>(session, "Finished"), 5, "Real enemy attack failed to reach defeat while aiming");
                Assert.That(Get<int>(enemy, "AttacksStarted"), Is.EqualTo(1));
                Assert.That(Get<string>(session, "DefeatSource"), Is.EqualTo(enemy.name));
                yield return AimFrames(); AssertAimHidden("Actual defeat");
                var previous = session;
                yield return RecoveryClick("restart"); yield return RecoveryRebind(previous);
                IsolateThreats(); yield return AimFrames();
                inventory = Get<Component>(player, "Firecrackers");
                Assert.That(oldAim == null && oldLine == null && oldMaterial == null, Is.True, "Retry leaked owned preview resources");
                Assert.That(Components("FirecrackerAim").Length, Is.EqualTo(1));
                AssertAimHidden("Held RMB inherited by retry");
                Assert.That(Get<int>(inventory, "Count"), Is.EqualTo(initialStock));
                if (Get<bool>(session, "CorridorMode"))
                {
                    // Controlled collectible fixture, not a survival traversal.
                    var supply = Components("Interactable").First(item => Get<object>(item,"kind").ToString()=="FirecrackerSupply");
                    Call(supply,"Use",player);
                    Assert.That(supply.gameObject.activeSelf, Is.False);
                }
                Assert.That(Get<int>(inventory, "Count"), Is.EqualTo(2));
                yield return FreshAim(mouse); yield return AimHold(mouse, false);
                Assert.That(Get<bool>(inventory, "CanAim"), Is.True, "Repeated aim cancellation spent cooldown");
                // Existing Q is still a press-to-throw, with no preceding aim required.
                Keys(Key.Q);
                yield return Wait(() => Get<int>(inventory, "Count") == 1, 2, "Quick Q did not throw immediately");
                Assert.That(keyboard.qKey.isPressed, Is.True, "Q throw waited for release");
                var first = Get<Component>(inventory, "LastThrown"); Assert.That(first, Is.Not.Null);
                yield return new WaitForSeconds(.65f);
                Assert.That(Get<int>(inventory, "Count"), Is.EqualTo(1), "Held Q repeated after cooldown");
                Keys(); yield return AimFrames();
                yield return FreshAim(mouse);
                Keys(Key.Q);
                yield return Wait(() => Get<int>(inventory, "Count") == 0, 2, "Aimed Q did not use the last real item");
                Keys(); yield return AimFrames(); AssertAimHidden("Empty inventory after Q");
                yield return AimHold(mouse, false); yield return AimHold(mouse, true); AssertAimHidden("Fresh RMB with no inventory");
                Assert.That((bool)Call(inventory, "TryThrow"), Is.False);
                Assert.That(Get<int>(inventory, "Count"), Is.Zero);
                Debug.Log("HAPPYTOY_AIM_PASS lifecycle: real RMB cancel, title/pause/settings/journal/hide/death/retry no ghost hold; native preview cleanup; Q press immediate and nonrepeating; finite two-item inventory; controlled encounter fixture, not survival");
            }
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator ForecastMatchesPhysicalFirstContactAtLowAndHighSimulatedFrameRates()
        {
            IsolateThreats(); Begin();
            AimFixturePose(new Vector3(500, 0, 500), Vector3.forward);
            var floor = Cube("CloudQA aim physical floor fixture", new Vector3(500, -.2f, 500), new Vector3(80, .4f, 80));
            var wall = Cube("CloudQA aim physical wall fixture", new Vector3(500, 3, 505), new Vector3(20, 8, .3f));
            var ceiling = Cube("CloudQA aim physical ceiling fixture", new Vector3(500, 3.6f, 500), new Vector3(20, .3f, 20));
            float oldCapture = Time.captureDeltaTime;
            var report = new CloudFirecrackerAimTests.ContactReport();
            try
            {
                foreach (float frame in new[] { .1f, 1f / 120 })
                {
                    Time.captureDeltaTime = frame; yield return AimFrames();
                    Assert.That(Time.deltaTime, Is.EqualTo(frame).Within(.0002f), "Requested controlled frame duration was not applied");
                    foreach (string surface in new[] { "floor", "wall", "ceiling" })
                    {
                        wall.SetActive(surface == "wall"); ceiling.SetActive(surface == "ceiling"); Physics.SyncTransforms();
                        Vector3 direction = surface == "floor" ? new Vector3(0, -.65f, 1).normalized :
                            surface == "ceiling" ? new Vector3(0, .65f, 1).normalized : Vector3.forward;
                        var forecast = Forecast(new Vector3(500, 1.7f, 500), direction, out var position, out var velocity, out var points);
                        Assert.That(Get<bool>(forecast, "InitialOverlap"), Is.False);
                        Assert.That(Get<bool>(forecast, "HasContact"), Is.True, surface + " fixture did not forecast its collider");
                        Assert.That(Get<int>(forecast, "PointCount"), Is.InRange(2, 61));
                        Assert.That(Get<float>(forecast, "Time"), Is.InRange(.001f, 1.2f));
                        Vector3 normal = surface == "floor" ? Vector3.up : surface == "wall" ? Vector3.back : Vector3.down;
                        Assert.That(Vector3.Dot(Get<Vector3>(forecast, "ContactNormal"), normal), Is.GreaterThan(.999f), "Forecast hit a different surface");
                        var root = new GameObject("CloudQA real projectile first-contact fixture " + surface);
                        root.transform.position = position;
                        var fire = root.AddComponent(RequireType("FirecrackerProjectile")); Call(fire, "Launch", velocity);
                        try
                        {
                            yield return Wait(() => Get<bool>(fire, "HasFirstContact"), 8, "Actual projectile never reached " + surface);
                            var sample = new CloudFirecrackerAimTests.ContactSample {
                                surface = surface, requestedFrameSeconds = frame, observedFrameSeconds = Time.deltaTime,
                                predictedTime = Get<float>(forecast, "Time"), actualTime = Get<float>(fire, "FirstContactTime"),
                                predictedCenter = Get<Vector3>(forecast, "EndPosition"), actualCenter = Get<Vector3>(fire, "FirstContactPosition"),
                                predictedContact = Get<Vector3>(forecast, "ContactPoint"), actualContact = Get<Vector3>(fire, "FirstContactPoint")
                            };
                            sample.centerError = Vector3.Distance(sample.predictedCenter, sample.actualCenter);
                            sample.pointError = Vector3.Distance(sample.predictedContact, sample.actualContact);
                            sample.normalError = Vector3.Distance(Get<Vector3>(forecast, "ContactNormal"), Get<Vector3>(fire, "FirstContactNormal"));
                            report.samples.Add(sample);
                            Assert.That(sample.centerError, Is.LessThan(.002f), surface + " first-contact center diverged");
                            Assert.That(sample.pointError, Is.LessThan(.002f)); Assert.That(sample.normalError, Is.LessThan(.002f));
                            Assert.That(sample.actualTime, Is.EqualTo(sample.predictedTime).Within(.0002f));
                            Assert.That(points[Get<int>(forecast, "PointCount") - 1], Is.EqualTo(sample.predictedCenter));
                        }
                        finally { Object.Destroy(root); }
                        yield return null;
                    }
                }
                wall.SetActive(false); ceiling.SetActive(false); Physics.SyncTransforms();
                var horizon = Forecast(new Vector3(500, 30, 500), Vector3.forward, out var freePosition, out var freeVelocity, out var freePoints);
                Assert.That(Get<bool>(horizon, "HasContact"), Is.False);
                Assert.That(Get<bool>(horizon, "ReachedFuseHorizon"), Is.True);
                Assert.That(Get<int>(horizon, "PointCount"), Is.EqualTo(61));
                Assert.That(Get<float>(horizon, "Time"), Is.EqualTo(1.2f));
                var blocked = Cube("CloudQA containing-origin fixture", new Vector3(500, 30, 500), new Vector3(1, 1, .04f));
                Physics.SyncTransforms();
                var overlap = Forecast(blocked.transform.position, Vector3.forward, out var overlappedPosition, out _, out _);
                Assert.That(overlappedPosition, Is.EqualTo(blocked.transform.position), "Launch offset jumped out of a containing thin wall");
                Assert.That(Get<bool>(overlap, "InitialOverlap"), Is.True);
                Assert.That(Get<bool>(overlap, "HasContact"), Is.False, "Overlap invented a surface normal/contact");
                Assert.That(Get<int>(overlap, "PointCount"), Is.EqualTo(1));
                Assert.That(Get<float>(overlap, "Time"), Is.Zero);
                // Production inventory must reject the same ambiguous launch before spending.
                Get<Camera>(player, "eyes").transform.position = blocked.transform.position;
                var inventory = Get<Component>(player, "Firecrackers");
                Assert.That((bool)Call(inventory, "TryThrow"), Is.False);
                Assert.That(Get<int>(inventory, "Count"), Is.EqualTo(2));
                Assert.That(Get<Component>(inventory, "LastThrown"), Is.Null);
                blocked.SetActive(false); Physics.SyncTransforms();
                Assert.That(Get<bool>(inventory, "CanAim"), Is.True, "Rejected overlap consumed cooldown");
                Assert.That((bool)Call(inventory, "TryThrow"), Is.True, "Clear origin stayed blocked after an overlap rejection");
                CloudExperienceTests.Artifact("firecracker-first-contact-physics.json", System.Text.Encoding.UTF8.GetBytes(JsonUtility.ToJson(report, true)));
                Debug.Log("HAPPYTOY_AIM_PASS physics: actual floor/wall/ceiling contacts at .1s and 1/120s simulated frames; shared .02s step; first-contact cutoff/fuse horizon and overlap denial; no hardware FPS or landing guarantee");
            }
            finally { Time.captureDeltaTime = oldCapture; }
        }

        [UnityTest, Timeout(35000)]
        public IEnumerator AimedQStillDistractsNaturallyAndCannotEraseGenuineChase()
        {
            IsolateThreats(); Begin();
            using (var mouse = new CloudFirecrackerAimTests.AimMouse())
            {
                Vector3 origin = MainCorridorPoint();
                AimFixturePose(origin + Vector3.right * 2, new Vector3(1, -.65f, 0).normalized);
                var enemy = MovingStalker(origin, Vector3.left);
                var inventory = Get<Component>(player, "Firecrackers");
                yield return FreshAim(mouse);
                Keys(Key.Q); yield return Wait(() => Get<int>(inventory, "Count") == 1, 2, "Aimed Q did not launch"); Keys();
                var first = Get<Component>(inventory, "LastThrown");
                // Separate player recognition from acoustic acceptance, as an explicit encounter fixture.
                AimFixturePose(origin + Vector3.up * 5, Vector3.right);
                yield return Wait(() => Get<bool>(first, "Exploded") && Get<int>(enemy, "NoisesAccepted") > 0, 5, "Aimed physical fuse did not naturally reach enemy hearing");
                Assert.That(Get<int>(first, "Attracted"), Is.GreaterThan(0));
                Assert.That(Get<object>(enemy, "state").ToString(), Is.EqualTo("Investigate"));
                yield return Wait(() => Vector3.Distance(enemy.transform.position, origin) > .3f, 3, "Actual heard decoy never moved the enemy");
                enemy.gameObject.SetActive(false);
                AimFixturePose(origin + Vector3.right * 6, new Vector3(-1, -.65f, 0).normalized);
                Get<Light>(player, "flashlight").enabled = true;
                var pursuer = StalkerAt(origin); pursuer.transform.rotation = Quaternion.LookRotation(Vector3.right);
                yield return Wait(() => Get<object>(pursuer, "state").ToString() == "Chase", 3, "Actual sight did not establish chase");
                int accepted = Get<int>(pursuer, "NoisesAccepted");
                yield return FreshAim(mouse);
                Keys(Key.Q); yield return Wait(() => Get<int>(inventory, "Count") == 0, 2, "Second aimed Q did not consume final item"); Keys();
                var second = Get<Component>(inventory, "LastThrown");
                yield return Wait(() => Get<int>(second, "PopsPlayed") >= 2, 5, "Chase test never received actual fuse pulses");
                Assert.That(Get<int>(pursuer, "NoisesAccepted"), Is.EqualTo(accepted), "A real firecracker pulse overwrote established chase");
                Assert.That(Get<object>(pursuer, "state").ToString(), Is.EqualTo("Chase"));
                Assert.That(Get<int>(second, "Attracted"), Is.Zero);
                Assert.That(Get<int>(inventory, "Count"), Is.Zero);
                Assert.That((bool)Call(inventory, "TryThrow"), Is.False);
                Assert.That(Get<bool>(session, "Finished"), Is.False);
                Debug.Log("HAPPYTOY_AIM_PASS enemy: real aimed Q/fuse/accepted noise/physical NavMesh movement; subsequent genuine sight-acquired chase rejects real pulses; exactly two resources; controlled fixture, not survival");
            }
        }

        [UnityTest, Timeout(35000)]
        public IEnumerator AuthoredCameraRendersWorldAimAndStopsAtPhysicalWall()
        {
            IsolateThreats(); Begin(); Call(shell, "RestoreDefaultSettings");
            Assert.That(SystemInfo.graphicsDeviceType, Is.Not.EqualTo(GraphicsDeviceType.Null));
            using (var mouse = new CloudFirecrackerAimTests.AimMouse())
            {
                Vector3 origin = MainCorridorPoint();
                AimFixturePose(origin, new Vector3(1, -.65f, 0).normalized);
                yield return FreshAim(mouse);
                var aim = CurrentAim;
                Assert.That(Get<bool>(aim, "HasContact"), Is.True);
                Assert.That(Get<Vector3>(aim, "ContactNormal").y, Is.GreaterThan(.9f));
                CaptureWorldAim("firecracker-aim-downward-world.png");
                var line = Get<LineRenderer>(aim, "TrajectoryRenderer");
                var material = line.sharedMaterial;
                // The authored west-end wall, not a fabricated screenshot obstacle.
                var wall = CloudRoomPresentationTests.Named("West end").GetComponent<Collider>();
                Assert.That(wall && wall.enabled && !wall.isTrigger, Is.True, "Authored West end must have its real opaque collider");
                var camera = Get<Camera>(player, "eyes");
                camera.transform.rotation = Quaternion.LookRotation(Vector3.left);
                Physics.SyncTransforms();
                Assert.That(Physics.SphereCast(camera.transform.position, .08f, camera.transform.forward, out var wallHit,
                    6, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore), Is.True);
                Assert.That(wallHit.collider, Is.SameAs(wall), "Blocked-world view did not face the authored West end wall");
                Call(shell, "ToggleHighContrast"); Call(shell, "ToggleReducedMotion"); Call(shell, "ToggleLargeText");
                yield return AimFrames();
                var view = One("GameShellView");
                foreach (var size in new[] { new Vector2Int(1280, 720), new Vector2Int(1024, 768), new Vector2Int(2560, 1080) })
                {
                    Call(view, "SetCaptureSize", size.x, size.y); yield return AimFrames(5);
                    var root = Get<VisualElement>(view, "Root");
                    Assert.That(root.Q<Label>("item-action-feedback").text, Is.EqualTo(Get<string>(aim, "Hint")), "Aim-visible large-text HUD used stale feedback");
                    Assert.That(CloudExperienceTests.Layout(root), Is.Empty, "Aim-visible large-text HUD clipped at " + size);
                }
                Assert.That(Get<bool>(aim, "HasContact"), Is.True);
                Assert.That(Vector3.Dot(Get<Vector3>(aim, "ContactNormal"), Vector3.right), Is.GreaterThan(.99f));
                Assert.That(Get<Vector3>(aim, "EndPosition").x, Is.GreaterThan(wall.bounds.max.x));
                Assert.That(line, Is.SameAs(Get<LineRenderer>(aim, "TrajectoryRenderer")));
                Assert.That(material, Is.SameAs(line.sharedMaterial), "Aim recreated its material each update");
                for (int i = 0; i < line.positionCount; i++)
                    Assert.That(line.GetPosition(i).x, Is.GreaterThan(wall.bounds.max.x), "Trajectory continued behind the authored West end wall");
                CaptureWorldAim("firecracker-aim-blocked-wall-world.png");
                yield return AimHold(mouse, false); AssertAimHidden("Screenshot fixture release");
                var inventory = Get<Component>(player, "Firecrackers");
                Assert.That(Get<int>(inventory, "Count"), Is.EqualTo(2));
                yield return FreshAim(mouse);
                Assert.That((bool)Call(session, "Collect", "register"), Is.True);
                Assert.That((bool)Call(session, "Collect", "ribbon"), Is.True);
                Assert.That((bool)Call(session, "Collect", "record"), Is.True);
                foreach (string id in new[] { "music-roster", "archive-record", "nursery-tag" }) Inspect(id);
                Assert.That((bool)Call(session, "Collect", "restore"), Is.True);
                yield return AimFrames(); AssertAimHidden("Restoration with held RMB");
                Assert.That(Get<int>(session, "StoryStep"), Is.EqualTo(4));
                Assert.That(Get<bool>(inventory, "CanAim"), Is.False);
                Keys(Key.Q); yield return AimFrames(); Keys();
                Assert.That(Get<int>(inventory, "Count"), Is.EqualTo(2), "Restored-state Q consumed an item");
                Assert.That(Get<Component>(inventory, "LastThrown"), Is.Null);
                Assert.That(Components("FirecrackerProjectile"), Is.Empty);
                yield return AimHold(mouse, false); yield return AimHold(mouse, true);
                AssertAimHidden("Fresh RMB after restoration");
            }
        }
        void CaptureWorldAim(string filename)
        {
            var aim = CurrentAim; var camera = Get<Camera>(player, "eyes");
            var line = Get<LineRenderer>(aim, "TrajectoryRenderer"); var ring = Get<LineRenderer>(aim, "ContactRenderer");
            Assert.That(line.enabled && line.gameObject.activeInHierarchy && line.useWorldSpace, Is.True);
            Assert.That(line.positionCount, Is.EqualTo(Get<int>(aim, "ForecastPointCount")));
            Assert.That(line.positionCount, Is.InRange(2, 61));
            Assert.That(line.startColor.a, Is.InRange(.5f, 1f));
            Assert.That(line.startWidth, Is.InRange(.003f, .006f));
            Assert.That(line.endWidth, Is.InRange(.015f, .026f));
            Assert.That(line.endWidth, Is.GreaterThan(line.startWidth * 3), "Near-eye line must taper to avoid a perspective wedge");
            Assert.That(ring.enabled && ring.gameObject.activeInHierarchy && ring.useWorldSpace, Is.True);
            Assert.That(line.sharedMaterial.GetInt("_ZTest"), Is.EqualTo((int)CompareFunction.LessEqual));
            var target = new RenderTexture(960, 540, 24); target.Create();
            Texture2D shown = null, hidden = null;
            try
            {
                RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
                shown = CloudExperienceTests.Read(target);
                CloudExperienceTests.Artifact(filename, shown.EncodeToPNG());
                Assert.That(CloudExperienceTests.HasContent(shown), Is.True, "Real world preview camera was blank");
                line.enabled = ring.enabled = false;
                RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
                hidden = CloudExperienceTests.Read(target);
                var visiblePixels = shown.GetPixels32(); var hiddenPixels = hidden.GetPixels32(); int changed = 0;
                for (int i = 0; i < visiblePixels.Length; i++)
                    if (Mathf.Abs(visiblePixels[i].r - hiddenPixels[i].r) + Mathf.Abs(visiblePixels[i].g - hiddenPixels[i].g) +
                        Mathf.Abs(visiblePixels[i].b - hiddenPixels[i].b) > 24) changed++;
                Assert.That(changed, Is.GreaterThan(15), "World render contains no observable contribution from actual aim geometry");
                TestContext.Out.WriteLine("HAPPYTOY_AIM_RENDER " + filename + "; realAuthoredCamera=true; controlledPose=true; UIOnly=false; changedWorldPixels=" + changed +
                    "; points=" + line.positionCount + "; firstContact=" + Get<Vector3>(aim, "ContactPoint") + "; survivalEvidence=false");
            }
            finally
            {
                line.enabled = ring.enabled = true;
                if (shown) Object.Destroy(shown); if (hidden) Object.Destroy(hidden);
                target.Release(); Object.Destroy(target);
            }
        }
    }
}
