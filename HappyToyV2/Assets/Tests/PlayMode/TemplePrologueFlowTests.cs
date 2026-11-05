using System.Collections;
using System.Reflection;
using System.IO;
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
        [UnityTest, Timeout(90000)]
        public IEnumerator TempleBoundaryContactsRemainSaveableAndRestorable()
        {
            Call(session, "ConfigureRecordDirectory", NewRecordFixture());
            Call(session, "CreatePrologue"); Begin(); yield return null;
            var world = Get<Component>(session, "Prologue");
            try
            {
                foreach (var sample in new[] {
                    (new Vector3(521, .04f, 500), Key.D),
                    (new Vector3(479, .04f, 500), Key.A),
                    (new Vector3(515, .04f, 528), Key.W),
                    (new Vector3(515, .04f, 474), Key.S) })
                {
                    PlacePlayer(sample.Item1); yield return null;
                    yield return KeysObserved(sample.Item2); yield return Delay(1f);
                    Assert.That((Get<CollisionFlags>(player, "LastCollision") & CollisionFlags.Sides) != 0, Is.True,
                        "The player did not physically reach the boundary: " + player.transform.position);
                    Keys(); yield return new WaitForFixedUpdate();
                    var pose = player.transform.position;
                    object checkpoint = null;
                    Assert.DoesNotThrow(() => checkpoint = Call(world, "Capture", 0f),
                        "A reachable boundary position could not be saved: " + pose);
                    Assert.That((bool)Call(player, "CanRestoreTempleProgress", Get<object>(checkpoint, "player")), Is.True,
                        "A boundary position accepted by save could not be restored: " + pose);
                    Call(session, "RetirePrologue"); yield return null;
                    Assert.DoesNotThrow(() => Call(session, "CreatePrologueForCheckpoint", checkpoint),
                        "A reachable boundary position failed to restore in the rebuilt world: " + pose);
                    world = Get<Component>(session, "Prologue");
                    Assert.That(Vector3.Distance(player.transform.position, pose), Is.LessThan(.001f));
                }
            }
            finally { Keys(); }
        }
        [UnityTest, Timeout(90000)]
        public IEnumerator TempleObstructedResumePreservesPlayerSelectionAndEnvironment()
        {
            Call(session, "ConfigureRecordDirectory", NewRecordFixture());
            Call(session, "CreatePrologue"); Begin(); yield return null;
            var checkpoint = Call(Get<Component>(session, "Prologue"), "Capture", 0f);
            Call(session, "RetirePrologue"); yield return null;
            var items = Get<Component>(player, "Items");
            var snapper = System.Enum.Parse(RequireType("PlayerItem"), "Snapper");
            Call(items, "Select", snapper);
            var originalPosition = player.transform.position;
            var fog = RenderSettings.fog; var fogColor = RenderSettings.fogColor;
            var camera = Get<Camera>(player, "eyes"); var background = camera.backgroundColor;
            Set(checkpoint, "phase", 1); Set(checkpoint, "prayerElapsed", 0f); Set(checkpoint, "incensePlaced", false);
            Set(Get<object>(checkpoint, "player"), "position", new Vector3(500, .04f, 507.35f));
            Assert.Throws<System.InvalidOperationException>(() => Call(session, "CreatePrologueForCheckpoint", checkpoint));
            Assert.That(Get<bool>(session, "PrologueMode"), Is.False);
            Assert.That(Get<object>(items, "Selected"), Is.EqualTo(snapper), "A failed restore changed the player's selected item");
            Assert.That(player.transform.position, Is.EqualTo(originalPosition));
            Assert.That(RenderSettings.fog, Is.EqualTo(fog));
            Assert.That(RenderSettings.fogColor, Is.EqualTo(fogColor));
            Assert.That(camera.backgroundColor, Is.EqualTo(background));
            yield return null;
        }
        [UnityTest, Timeout(120000)]
        public IEnumerator TempleObstructedContinueKeepsSaveAndShowsErrorOnTitle()
        {
            string directory = NewRecordFixture();
            Call(session, "ConfigureRecordDirectory", directory);
            Call(session, "CreatePrologue"); yield return null;
            var checkpoint = Call(Get<Component>(session, "Prologue"), "Capture", 0f);
            Call(session, "RetirePrologue"); yield return null;
            Set(checkpoint, "phase", 1); Set(checkpoint, "prayerElapsed", 0f); Set(checkpoint, "incensePlaced", false);
            Set(Get<object>(checkpoint, "player"), "position", new Vector3(500, .04f, 507.35f));
            Assert.That((bool)Call(Get<object>(session, "TempleSuspension"), "Save", checkpoint), Is.True);
            string path = Path.Combine(directory, "temple-suspend-v1.json");
            string original = File.ReadAllText(path);
            yield return RecoveryUiReady(); yield return RefreshSlotTitle(directory);
            yield return RecoveryClick("continue-temple");
            Assert.That(RecoveryPage, Is.EqualTo("Title"));
            Assert.That(Get<bool>(session, "PrologueMode"), Is.False);
            Assert.That(File.ReadAllText(path), Is.EqualTo(original));
            Assert.That(File.Exists(path + ".used"), Is.False);
            Assert.That(Get<bool>(Get<object>(session, "TempleSuspension"), "HasRun"), Is.True);
            string error = Get<string>(shell, "ReloadError");
            Assert.That(error, Does.Contain("중단 기록을 보존했습니다"));
            yield return Wait(() => Get<VisualElement>(One("GameShellView"), "Root")
                .Query<Label>().ToList().Any(label => label.text == error), 3,
                "The title did not show the restore failure");
        }
        [UnityTest, Timeout(90000)]
        public IEnumerator TemplePlacedIncenseSmokeStartsAtVisibleMeshTip()
        {
            Call(session, "ConfigureRecordDirectory", NewRecordFixture());
            Call(session, "CreatePrologue"); Begin(); yield return null;
            var world = Get<Component>(session, "Prologue");
            var presentation = world.GetComponent(RequireType("TempleProloguePresentation"));
            Call(presentation, "PlaceIncense"); yield return null; yield return null;
            var trail = world.GetComponentInChildren(RequireType("IncenseSmokeTrail"));
            AssertTempleSmokeAtVisibleTip(world, trail);
        }
        static void AssertTempleSmokeAtVisibleTip(Component world, Component trail)
        {
            var item = world.transform.Find("Placed incense");
            Assert.That(item, Is.Not.Null);
            var renderers = item.GetComponentsInChildren<Renderer>(true);
            Assert.That(renderers, Is.Not.Empty);
            var bounds = renderers[0].bounds;
            foreach (var part in renderers.Skip(1)) bounds.Encapsulate(part.bounds);
            var expected = new Vector3(bounds.center.x, bounds.max.y, bounds.center.z);
            Assert.That(trail, Is.Not.Null);
            var field = trail.GetType().GetField("positions", BindingFlags.Instance | BindingFlags.NonPublic);
            var points = (Vector3[])field.GetValue(trail);
            Assert.That(Get<int>(trail, "SampleCount"), Is.GreaterThan(0));
            Assert.That(Vector3.Distance(points[0], expected), Is.LessThan(.005f),
                "The smoke must emerge from the actual placed incense tip");
        }
        [UnityTest, Timeout(90000)]
        public IEnumerator TempleItemWheelStaysDisarmedUntilHeldInputIsReleased()
        {
            Call(session, "ConfigureRecordDirectory", NewRecordFixture());
            Call(session, "CreatePrologue"); Begin(); yield return null; yield return null;
            var wheel = player.GetComponent(RequireType("PlayerItemWheel"));
            Assert.That(wheel, Is.Not.Null);
            var selected = Get<object>(Get<Component>(player, "Items"), "Selected");
            yield return KeysObserved(Key.Tab); yield return null;
            Assert.That(Get<bool>(shell, "ItemWheelOpen"), Is.False);
            Assert.That(Time.timeScale, Is.EqualTo(1).Within(.001f));
            Assert.That(Get<object>(Get<Component>(player, "Items"), "Selected"), Is.EqualTo(selected));
            // A held key must not arm an item-wheel request merely because
            // the prologue ends; require a fresh release and press.
            Call(session, "RetirePrologue"); yield return null; yield return null;
            Assert.That(Get<bool>(shell, "ItemWheelOpen"), Is.False, "Held temple input opened the wheel after the mode changed");
            Keys(); yield return null; yield return KeysObserved(Key.Tab); yield return null;
            Assert.That(Get<bool>(shell, "ItemWheelOpen"), Is.True, "A fresh post-temple press should open the normal wheel");
            Keys(); yield return null;
        }
        [UnityTest, Timeout(120000)]
        public IEnumerator TempleHiddenGateGuidesBlockedAndWatchingPlayersWithoutAffectingOtherHud()
        {
            Call(session, "ConfigureRecordDirectory", NewRecordFixture());
            Call(session, "CreatePrologue"); Begin(); yield return null;
            var initialRoot = Get<VisualElement>(One("GameShellView"), "Root");
            yield return Wait(() => initialRoot.Q<Label>("temple-story-notice")?.text == Get<string>(session, "Notice"),
                3, "Initial temple instruction was not shown");
            Assert.That(initialRoot.Q("temple-story-notice-panel").resolvedStyle.display, Is.Not.EqualTo(DisplayStyle.None));
            var checkpoint = Call(Get<Component>(session, "Prologue"), "Capture", 0f);
            Call(session, "RetirePrologue"); yield return null;
            Set(checkpoint, "phase", 5); Set(checkpoint, "incensePlaced", true); Set(checkpoint, "gateVisible", false);
            Set(Get<object>(checkpoint, "player"), "position", new Vector3(500, .04f, 492.87f));
            Set(Get<object>(checkpoint, "player"), "yaw", 180f);
            Call(session, "CreatePrologueForCheckpoint", checkpoint); yield return null;
            var world = Get<Component>(session, "Prologue");
            var gate = world.transform.Find("나타난 대문");
            Assert.That(gate.gameObject.activeSelf, Is.False);
            Call(shell, "Pause"); int revision = Get<int>(session, "NoticeRevision");
            yield return Delay(1f);
            Assert.That(Get<int>(session, "NoticeRevision"), Is.EqualTo(revision), "Paused time must not advance gate hints");
            Call(shell, "Resume");
            yield return Wait(() => Get<string>(session, "Notice").Contains("물러서"), 12, "No hint when the player occupies the hidden gate");
            var root = Get<VisualElement>(One("GameShellView"), "Root");
            yield return Wait(() => root.Q<Label>("temple-story-notice")?.text == Get<string>(session, "Notice"),
                3, "Gate clearance hint was not rendered");
            Assert.That(root.Q("temple-story-notice-panel").resolvedStyle.display, Is.Not.EqualTo(DisplayStyle.None));
            PlacePlayer(new Vector3(500, .04f, 494.4f)); player.transform.rotation = Quaternion.Euler(0, 180, 0);
            yield return Wait(() => Get<string>(session, "Notice").Contains("시선을"), 3, "No hint while watching the empty gate position");
            Assert.That(gate.gameObject.activeSelf, Is.False, "The gate appeared inside the player's view");
            player.transform.rotation = Quaternion.identity;
            yield return Wait(() => gate.gameObject.activeSelf, 3, "The gate did not appear after the player looked away");
            Assert.That(Get<string>(session, "Notice"), Does.Contain("문"));
            Call(session, "RetirePrologue"); yield return null; yield return null;
            root = Get<VisualElement>(One("GameShellView"), "Root");
            var label = root.Q<Label>("temple-story-notice");
            if (label != null) Assert.That(label.text, Is.Empty, "Temple guidance leaked into another mode");
        }
        [UnityTest, Timeout(180000)]
        public IEnumerator TempleGateTransitionFadesAmbienceWhileNormalPauseStillHoldsIt()
        {
            Call(session, "ConfigureRecordDirectory", NewRecordFixture());
            Call(session, "CreatePrologue"); Begin(); yield return null;
            var checkpoint = Call(Get<Component>(session, "Prologue"), "Capture", 0f);
            Call(session, "RetirePrologue"); yield return null;
            Set(checkpoint, "phase", 5); Set(checkpoint, "incensePlaced", true); Set(checkpoint, "gateVisible", true);
            Call(session, "CreatePrologueForCheckpoint", checkpoint); yield return null;
            var world = Get<Component>(session, "Prologue");
            var callback = shell.GetType().GetMethod("TempleEnterCorridor", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(callback, Is.Not.Null);
            world.GetType().GetEvent("CorridorRequested").AddEventHandler(world,
                System.Delegate.CreateDelegate(typeof(System.Action), shell, callback));
            var air = world.transform.Find("Forest air").GetComponent<AudioSource>();
            var cry = world.transform.Find("Distant child crying").GetComponent<AudioSource>();
            var gate = world.transform.Find("Gate opening").GetComponent<AudioSource>();
            PlacePlayer(new Vector3(500, .04f, 494.6f)); player.transform.rotation = Quaternion.Euler(0, 180, 0);
            yield return null; yield return null;
            Assert.That(air.ignoreListenerPause, Is.False); Assert.That(cry.ignoreListenerPause, Is.False);
            Call(shell, "Pause");
            Assert.That(AudioListener.pause, Is.True);
            Assert.That(air.ignoreListenerPause, Is.False); Assert.That(cry.ignoreListenerPause, Is.False);
            Call(shell, "Resume"); yield return null;
            float airStart = air.volume, cryStart = cry.volume;
            Assert.That(airStart, Is.GreaterThan(0)); Assert.That(cryStart, Is.GreaterThan(0));
            Assert.That(Get<Component>(player, "Focus")?.name, Is.EqualTo("나타난 대문"));
            yield return RecoveryPulse(Key.E);
            Assert.That(Get<bool>(shell, "IsReloading"), Is.True);
            Assert.That(AudioListener.pause, Is.True);
            Assert.That(air.ignoreListenerPause, Is.True); Assert.That(cry.ignoreListenerPause, Is.True);
            yield return Wait(() => Get<float>(shell, "TransitionBlack") >= .4f, 3, "Temple fade did not begin");
            float black = Get<float>(shell, "TransitionBlack");
            Assert.That(air.volume, Is.EqualTo(airStart * (1 - black)).Within(.001f));
            Assert.That(cry.volume, Is.EqualTo(cryStart * (1 - black)).Within(.001f));
            Assert.That(gate.volume, Is.EqualTo(.65f).Within(.001f), "Gate cue must remain independent of ambience fade");
            yield return Wait(() => Get<float>(shell, "TransitionBlack") >= 1, 3, "Temple fade did not finish");
            Assert.That(air.volume, Is.EqualTo(0).Within(.0001f));
            Assert.That(cry.volume, Is.EqualTo(0).Within(.0001f));
            yield return Wait(() => Get<bool>(session, "CorridorMode") && !Get<bool>(shell, "IsReloading"), 60, "Corridor did not open");
            yield return null;
            var hud = Get<VisualElement>(One("GameShellView"), "Root");
            var templeNotice = hud.Q<Label>("temple-story-notice");
            if (templeNotice != null)
            {
                Assert.That(templeNotice.text, Is.Empty, "Temple notice leaked into the corridor");
                Assert.That(hud.Q("temple-story-notice-panel").resolvedStyle.display, Is.EqualTo(DisplayStyle.None));
            }
        }
        [UnityTest, Timeout(90000)]
        public IEnumerator TempleForestSoilRepeatsWithoutHardSeams()
        {
            Call(session, "ConfigureRecordDirectory", NewRecordFixture());
            Call(session, "CreatePrologue"); Begin(); yield return null;
            var world = Get<Component>(session, "Prologue");
            var renderer = world.transform.Find("Forest ground").GetComponent<Renderer>();
            var texture = renderer.sharedMaterial.GetTexture("_BaseMap") as Texture2D;
            Assert.That(texture, Is.Not.Null);
            Assert.That(texture.wrapMode, Is.EqualTo(TextureWrapMode.Repeat));
            var pixels = texture.GetPixels32(); int w = texture.width, h = texture.height;
            int minimum = pixels.Min(pixel => (int)pixel.r), maximum = pixels.Max(pixel => (int)pixel.r);
            double mean = pixels.Average(pixel => (double)pixel.r);
            Assert.That(maximum - minimum, Is.GreaterThanOrEqualTo(5), "Forest soil lost its texture variation");
            Assert.That(mean, Is.InRange(30, 60), "Forest soil changed its overall brightness");
            double seamX = 0, seamY = 0, insideX = 0, insideY = 0;
            for (int y = 0; y < h; y++)
            {
                seamX += System.Math.Abs(pixels[y*w].r - pixels[y*w+w-1].r);
                for (int x = 1; x < w; x++) insideX += System.Math.Abs(pixels[y*w+x].r - pixels[y*w+x-1].r);
            }
            for (int x = 0; x < w; x++)
            {
                seamY += System.Math.Abs(pixels[x].r - pixels[(h-1)*w+x].r);
                for (int y = 1; y < h; y++) insideY += System.Math.Abs(pixels[y*w+x].r - pixels[(y-1)*w+x].r);
            }
            seamX /= h; seamY /= w; insideX /= h*(w-1); insideY /= w*(h-1);
            TestContext.WriteLine($"Forest soil mean red-step: seamX={seamX:F3}, insideX={insideX:F3}, seamY={seamY:F3}, insideY={insideY:F3}");
            Assert.That(seamX, Is.LessThanOrEqualTo(insideX * 1.5 + .25), "Horizontal wrap creates an unusually hard seam");
            Assert.That(seamY, Is.LessThanOrEqualTo(insideY * 1.5 + .25), "Vertical wrap creates an unusually hard seam");
        }
        [UnityTest, Timeout(90000)]
        public IEnumerator TempleGateAudioRestoresDuringSecondHingeCue()
        {
            Call(session, "ConfigureRecordDirectory", NewRecordFixture());
            Call(session, "CreatePrologue"); Begin(); yield return null;
            var world = Get<Component>(session, "Prologue");
            Call(world, "InstallGateWalkReview");
            var data = Call(world, "Capture", 0f);
            Set(data, "phase", 5); Set(data, "incensePlaced", true); Set(data, "gateVisible", true);
            Set(data, "reviewGateStarted", true); Set(data, "reviewGateProgress", .75f);
            Call(shell, "Pause"); Call(session, "RetirePrologue"); yield return null;
            Call(session, "CreatePrologueForCheckpoint", data);
            world = Get<Component>(session, "Prologue");
            var audio = world.GetComponent(RequireType("TempleGateReviewAudio"));
            var right = world.transform.Find("Review gate right hinge").GetComponent<AudioSource>();
            Assert.That(Get<bool>(audio, "SecondTriggered"), Is.True);
            Assert.That(right.ignoreListenerPause, Is.False);
            Assert.That(right.time, Is.EqualTo((.75f * 3f - 1.65f) * .70f).Within(.03f));
        }
        [UnityTest, Timeout(90000)]
        public IEnumerator TempleWalkReviewCheckpointValidatesAndRestoresBeforeOpening()
        {
            Call(session, "ConfigureRecordDirectory", NewRecordFixture());
            Call(session, "CreatePrologue"); Begin(); yield return null;
            var world = Get<Component>(session, "Prologue");
            var normal = Call(world, "Capture", 0f);
            Assert.That(Get<int>(normal, "version"), Is.EqualTo(2));
            Call(world, "InstallGateWalkReview");
            var data = Call(world, "Capture", 0f);
            Assert.That(Get<int>(data, "version"), Is.EqualTo(3));
            Assert.That(Get<bool>(data, "reviewGateWalk"), Is.True);
            Assert.That(Get<bool>(data, "reviewGateStarted"), Is.False);
            var copy = Call(data, "Copy");
            Set(data, "reviewGateProgress", .5f);
            Assert.Throws<System.ArgumentException>(() => Call(data, "Validate"));
            Set(data, "reviewGateProgress", float.NaN);
            Assert.Throws<System.ArgumentException>(() => Call(data, "Validate"));
            Set(data, "reviewGateProgress", float.PositiveInfinity);
            Assert.Throws<System.ArgumentException>(() => Call(data, "Validate"));
            Set(data, "reviewGateProgress", 0f); Set(data, "reviewGateStarted", true);
            Assert.Throws<System.ArgumentException>(() => Call(data, "Validate"));
            Set(normal, "reviewGateWalk", true);
            Assert.Throws<System.ArgumentException>(() => Call(normal, "Validate"));
            Call(session, "RetirePrologue"); yield return null;
            Call(session, "CreatePrologueForCheckpoint", copy); yield return null;
            world = Get<Component>(session, "Prologue");
            var review = world.GetComponent(RequireType("TempleGateReviewMotion"));
            Assert.That(review, Is.Not.Null);
            Assert.That(Get<bool>(review, "Started"), Is.False);
            Assert.That(Get<float>(review, "Progress"), Is.Zero);
            Assert.That(world.transform.Find("나타난 대문").gameObject.activeSelf, Is.False);
        }
        [UnityTest, Timeout(90000)]
        public IEnumerator TempleWalkReviewPreservesCrossingInterruptedByPause()
        {
            Call(session, "ConfigureRecordDirectory", NewRecordFixture());
            Call(session, "CreatePrologue"); Begin(); yield return null;
            var world = Get<Component>(session, "Prologue");
            Call(world, "InstallGateWalkReview");
            var data = Call(world, "Capture", 0f);
            Set(data, "phase", 5); Set(data, "incensePlaced", true); Set(data, "gateVisible", true);
            Set(data, "reviewGateStarted", true); Set(data, "reviewGateProgress", 1f);
            Set(data, "reviewPreviousPosition", new Vector3(500, .45f, 491.1f));
            Set(Get<object>(data, "player"), "position", new Vector3(500, .04f, 490.6f));
            Call(shell, "Pause"); Call(session, "RetirePrologue"); yield return null;
            Call(session, "CreatePrologueForCheckpoint", data);
            world = Get<Component>(session, "Prologue");
            yield return Delay(.2f);
            Assert.That(Get<bool>(world, "TransitionRequested"), Is.False, "Paused restore transferred too early");
            Call(shell, "Resume");
            yield return Wait(() => Get<bool>(world, "TransitionRequested"), 3, "Crossing was lost after pause and restore");
        }
        [UnityTest, Timeout(120000)]
        public IEnumerator TempleImmediateGateReviewStillTransitionsAfterOpening()
        {
            Call(session, "ConfigureRecordDirectory", NewRecordFixture());
            Call(session, "CreatePrologue"); Begin(); yield return null;
            var data = Call(Get<Component>(session, "Prologue"), "Capture", 0f);
            Call(session, "RetirePrologue"); yield return null;
            Set(data, "phase", 5); Set(data, "incensePlaced", true); Set(data, "gateVisible", true);
            Call(session, "CreatePrologueForCheckpoint", data); yield return null;
            var world = Get<Component>(session, "Prologue");
            var callback = shell.GetType().GetMethod("TempleEnterCorridor", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(callback, Is.Not.Null);
            world.GetType().GetEvent("CorridorRequested").AddEventHandler(world,
                System.Delegate.CreateDelegate(typeof(System.Action), shell, callback));
            var review = (Component)Call(world, "InstallGateReview");
            var forestAir = world.transform.Find("Forest air").GetComponent<AudioSource>();
            float forestStart = forestAir.volume;
            PlacePlayer(new Vector3(500, .04f, 494.6f)); player.transform.rotation = Quaternion.Euler(0, 180, 0);
            player.GetType().GetField("pitch", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(player, 0f);
            yield return null; yield return null;
            Assert.That(Get<Component>(player, "Focus")?.name, Is.EqualTo("나타난 대문"));
            yield return RecoveryPulse(Key.E);
            Assert.That(Get<bool>(shell, "IsReloading"), Is.True);
            Assert.That(world.transform.Find("Review gate left hinge").GetComponent<AudioSource>().ignoreListenerPause, Is.True);
            Assert.That(world.transform.Find("Review gate right hinge").GetComponent<AudioSource>().ignoreListenerPause, Is.True);
            yield return Wait(() => Get<float>(review, "Progress") > .2f, 5, "Immediate review gate did not open");
            Assert.That(forestAir.ignoreListenerPause, Is.True);
            Assert.That(forestAir.volume, Is.EqualTo(forestStart).Within(.001f), "Ambient bed faded before the gate finished opening");
            yield return Wait(() => Get<bool>(session, "CorridorMode") && !Get<bool>(shell, "IsReloading"), 60,
                "Immediate gate review did not enter the corridor");
        }
        [UnityTest, Timeout(120000)]
        public IEnumerator TempleReviewGateAllowsPhysicalWalkingThroughOpenLeaves()
        {
            Call(session, "ConfigureRecordDirectory", NewRecordFixture());
            Call(session, "CreatePrologue"); Begin(); yield return null;
            var data = Call(Get<Component>(session, "Prologue"), "Capture", 0f);
            Call(session, "RetirePrologue"); yield return null;
            Set(data, "phase", 5); Set(data, "incensePlaced", true); Set(data, "gateVisible", true);
            Call(session, "CreatePrologueForCheckpoint", data); yield return null;
            var world = Get<Component>(session, "Prologue");
            var motion = (Component)Call(world, "InstallGateReview");
            var review = world.transform.Find("Review gate (opt-in)");
            var gate = world.transform.Find("나타난 대문");
            var blocker = gate.GetComponent<BoxCollider>();
            var colliders = new System.Collections.Generic.List<MeshCollider>();
            foreach (var filter in review.GetComponentsInChildren<MeshFilter>(true))
            {
                var collider = filter.gameObject.AddComponent<MeshCollider>();
                collider.sharedMesh = filter.sharedMesh; colliders.Add(collider);
            }
            Assert.That(colliders.Count, Is.EqualTo(3));
            try
            {
                PlacePlayer(new Vector3(500, .04f, 494.6f));
                player.transform.rotation = Quaternion.Euler(0, 180, 0);
                Physics.SyncTransforms();
                yield return KeysObserved(Key.W); yield return Delay(1.4f); Keys(); yield return null;
                Assert.That(player.transform.position.z, Is.GreaterThan(493f), "Closed gate did not stop the player");

                Call(motion, "SetProgress", 1f);
                Assert.That(blocker.enabled, Is.False, "Open review gate kept its closed blocker");
                var ramp = world.transform.Find("Review gate stair collision");
                Assert.That(ramp, Is.Not.Null);
                Assert.That(ramp.GetComponent<BoxCollider>().enabled, Is.True);
                Physics.SyncTransforms();
                var failures = new System.Collections.Generic.List<string>();
                foreach (float x in new[] { -.6f, 0f, .6f })
                {
                    PlacePlayer(new Vector3(500 + x, .04f, 494.6f));
                    player.transform.rotation = Quaternion.Euler(0, 180, 0);
                    Physics.SyncTransforms();
                    yield return KeysObserved(Key.W);
                    float end = Time.realtimeSinceStartup + 5;
                    while (player.transform.position.z >= 490.6f && Time.realtimeSinceStartup < end) yield return null;
                    Keys(); yield return null;
                    if (player.transform.position.z >= 490.6f)
                    {
                        var controller = player.GetComponent<CharacterController>();
                        float radius = Mathf.Max(.01f, controller.radius - .01f);
                        float half = Mathf.Max(0, controller.height * .5f - controller.radius);
                        Vector3 center = controller.transform.TransformPoint(controller.center);
                        var hits = Physics.CapsuleCastAll(center - Vector3.up * half, center + Vector3.up * half,
                            radius, Vector3.back, .5f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                            .Where(hit => !hit.collider.transform.IsChildOf(player.transform))
                            .OrderBy(hit => hit.distance)
                            .Select(hit => hit.collider.name + "@" + hit.distance.ToString("F3") +
                                " point=" + hit.point.ToString("F3") + " normal=" + hit.normal.ToString("F3"));
                        failures.Add("x=" + x + " z=" + player.transform.position.z.ToString("F3") +
                            " hits=" + string.Join(",", hits));
                    }
                }
                Assert.That(failures, Is.Empty, "Open review gate blocked walking: " + string.Join("; ", failures));
            }
            finally
            {
                Keys(); Call(motion, "SetProgress", 0f);
                foreach (var collider in colliders) if (collider) Object.Destroy(collider);
            }
        }
        [UnityTest, Timeout(90000)]
        public IEnumerator TempleIncensePlacementHoldsMovementUntilRelease()
        {
            Call(session, "ConfigureRecordDirectory", NewRecordFixture());
            yield return RecoveryUiReady(); yield return RecoveryClick("begin");
            var world = Get<Component>(session, "Prologue");
            var presentation = world.GetComponent(RequireType("TempleProloguePresentation"));
            PlacePlayer(new Vector3(500, .04f, 505.2f));
            player.GetType().GetField("pitch", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(player, 12f);
            yield return null; yield return null;
            Assert.That(Get<Component>(player, "Focus")?.name, Is.EqualTo("향로"));
            Vector3 start = player.transform.position;
            try
            {
                yield return KeysObserved(Key.E, Key.S, Key.LeftShift);
                yield return Wait(() => Get<bool>(presentation, "IncensePending"), 2, "Incense interaction did not start");
                Keys(Key.S, Key.LeftShift);
                while (Get<bool>(presentation, "IncensePending"))
                {
                    Vector3 offset = player.transform.position - start; offset.y = 0;
                    Assert.That(offset.magnitude, Is.LessThan(.03f), "Player walked away before placing the incense");
                    yield return null;
                }
                Assert.That(Get<bool>(presentation, "IncensePlaced"), Is.True);
                yield return Wait(() => player.transform.position.z < start.z - .3f, 3,
                    "Held movement did not resume after incense placement");
            }
            finally { Keys(); }
        }
        [UnityTest, Timeout(90000)]
        public IEnumerator TempleRestoredPlacedIncenseDoesNotReturnToTheHand()
        {
            Call(session, "ConfigureRecordDirectory", NewRecordFixture());
            Call(session, "CreatePrologue"); Begin(); yield return null;
            var data = Call(Get<Component>(session, "Prologue"), "Capture", 0f);
            Call(session, "RetirePrologue"); yield return null;
            Set(data, "phase", 2); Set(data, "incensePlaced", true);
            Call(session, "CreatePrologueForCheckpoint", data);
            var world = Get<Component>(session, "Prologue");
            var presentation = world.GetComponent(RequireType("TempleProloguePresentation"));
            Assert.That(Get<bool>(presentation, "IncensePlaced"), Is.True);
            var hands = Get<Component>(player, "Hands");
            var held = hands.GetType().GetField("held", BindingFlags.Instance | BindingFlags.NonPublic);
            Call(Get<Component>(player, "Items"), "Select", System.Enum.Parse(RequireType("PlayerItem"), "Incense"));
            yield return Wait(() => Get<object>(hands, "PresentedItem").ToString() == "Incense" &&
                Get<bool>(hands, "EquipmentSettled"), 4, "Restored incense selection did not settle");
            Assert.That((bool)(Transform)held.GetValue(hands), Is.False, "Restored placed incense returned to the hand");
            Assert.That(Get<int>(Get<Component>(player, "Rituals"), "Incense"), Is.GreaterThan(0));
        }
        [UnityTest, Timeout(90000)]
        public IEnumerator TempleIncensePlacementKeepsFlashlightFromInterruptingItsHandAction()
        {
            Call(session, "ConfigureRecordDirectory", NewRecordFixture());
            yield return RecoveryUiReady(); yield return RecoveryClick("begin");
            var world = Get<Component>(session, "Prologue");
            var presentation = world.GetComponent(RequireType("TempleProloguePresentation"));
            var actions = Get<Component>(player, "HandActions");
            var hands = Get<Component>(player, "Hands");
            var heldField = hands.GetType().GetField("held", BindingFlags.Instance | BindingFlags.NonPublic);
            var incenseItem = System.Enum.Parse(RequireType("PlayerItem"), "Incense");
            int incenseStock = Get<int>(Get<Component>(player, "Rituals"), "Incense");
            var flashlight = Get<Light>(player, "flashlight");
            bool initialLight = flashlight.enabled;
            PlacePlayer(new Vector3(500, .04f, 505.2f));
            player.GetType().GetField("pitch", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(player, 12f);
            yield return null; yield return null; yield return RecoveryPulse(Key.E);
            Assert.That(Get<bool>(presentation, "IncensePending"), Is.True);
            yield return RecoveryPulse(Key.F);
            Assert.That(Get<bool>(actions, "Busy"), Is.False, "Flashlight interrupted incense equip");
            yield return RecoveryPulse(Key.Tab);
            Assert.That(Get<bool>(shell, "ItemWheelOpen"), Is.False, "Item wheel interrupted incense placement");
            Assert.That(Get<object>(Get<Component>(player, "Items"), "Selected"), Is.EqualTo(incenseItem));
            var pending = presentation.GetType().GetField("pendingIncense", BindingFlags.Instance | BindingFlags.NonPublic);
            yield return Wait(() => (float)pending.GetValue(presentation) > 0, 3, "Incense action did not start");
            Assert.That((bool)(Transform)heldField.GetValue(hands), Is.True, "Incense was not held before placement");
            Assert.That((bool)Call(hands, "TryGetHeldRootWorld", incenseItem, Vector3.zero), Is.True,
                "Review did not see the held incense before placement");
            yield return RecoveryPulse(Key.F);
            Assert.That(Get<bool>(actions, "Busy"), Is.False, "Flashlight interrupted the incense release");
            Assert.That(flashlight.enabled, Is.EqualTo(initialLight));
            yield return Wait(() => Get<bool>(presentation, "IncensePlaced"), 3, "Incense did not reach its release marker");
            yield return null;
            Assert.That((bool)(Transform)heldField.GetValue(hands), Is.False, "Placed incense remained in the hand");
            Assert.That((bool)Call(hands, "TryGetHeldRootWorld", incenseItem, Vector3.zero), Is.False,
                "Review still saw held incense after placement");
            Assert.That(Get<int>(Get<Component>(player, "Rituals"), "Incense"), Is.EqualTo(incenseStock), "Story placement spent corridor stock");
            yield return RecoveryPulse(Key.F);
            yield return Wait(() => flashlight.enabled != initialLight, 3, "Flashlight did not work after incense placement");
            yield return Wait(() => Get<object>(Get<Component>(world, "Director"), "Phase").ToString() == "FirstDeparture", 8,
                "Prayer did not finish after incense placement");
        }
        [UnityTest, Timeout(90000)]
        public IEnumerator TempleGateWaitsUntilPlayerClearsItsCollidersAfterRestore()
        {
            Call(session, "ConfigureRecordDirectory", NewRecordFixture());
            Call(session, "CreatePrologue"); Begin(); yield return null;
            PlacePlayer(new Vector3(500, .04f, 492.87f));
            var data = Call(Get<Component>(session, "Prologue"), "Capture", 0f);
            Call(session, "RetirePrologue"); yield return null;
            Set(data, "phase", 5); Set(data, "incensePlaced", true); Set(data, "gateVisible", false);
            Call(session, "CreatePrologueForCheckpoint", data); yield return null;
            var world = Get<Component>(session, "Prologue");
            var gate = world.transform.Find("나타난 대문");
            Assert.That(gate.gameObject.activeSelf, Is.False, "Gate appeared around the restored player");
            foreach (float x in new[] { -3f, 3f })
            {
                PlacePlayer(new Vector3(500 + x, .04f, 492.87f));
                yield return null;
                Assert.That(gate.gameObject.activeSelf, Is.False, "Gate post appeared around the player");
                Assert.That(Get<bool>(Call(world, "Capture", 0f), "gateVisible"), Is.False);
            }
            PlacePlayer(new Vector3(500, .04f, 494.4f));
            yield return null; yield return null;
            Assert.That(gate.gameObject.activeSelf, Is.True, "Gate did not appear after the player cleared it");
            var controller = player.GetComponent<CharacterController>();
            foreach (var box in gate.GetComponentsInChildren<BoxCollider>())
                Assert.That(Physics.ComputePenetration(controller, controller.transform.position, controller.transform.rotation,
                    box, box.transform.position, box.transform.rotation, out _, out _), Is.False);
        }
        [UnityTest, Timeout(90000)]
        public IEnumerator TempleReviewGateLeavesClearTheOpening()
        {
            Call(session, "ConfigureRecordDirectory", NewRecordFixture());
            Call(session, "CreatePrologue"); Begin(); yield return null;
            var data = Call(Get<Component>(session, "Prologue"), "Capture", 0f);
            Call(session, "RetirePrologue"); yield return null;
            Set(data, "phase", 5); Set(data, "incensePlaced", true); Set(data, "gateVisible", true);
            Call(session, "CreatePrologueForCheckpoint", data); yield return null;
            var world = Get<Component>(session, "Prologue");
            var motion = (Component)Call(world, "InstallGateReview");
            var review = world.transform.Find("Review gate (opt-in)");
            Assert.That(review, Is.Not.Null); Assert.That(review.gameObject.activeSelf, Is.True);
            Assert.That(review.GetComponentsInChildren<Collider>(true), Is.Empty, "Review art must not change production physics");
            var blocker = world.transform.Find("나타난 대문").GetComponent<BoxCollider>();
            var ramp = world.transform.Find("Review gate stair collision").GetComponent<BoxCollider>();
            Call(motion, "SetProgress", .5f);
            Assert.That(blocker.enabled, Is.True, "Passage opened before the leaves finished moving");
            Assert.That(ramp.enabled, Is.False);
            var colliders = new System.Collections.Generic.List<MeshCollider>();
            foreach (var filter in review.GetComponentsInChildren<MeshFilter>(true))
            {
                var collider = filter.gameObject.AddComponent<MeshCollider>(); collider.sharedMesh = filter.sharedMesh;
                colliders.Add(collider);
            }
            Assert.That(colliders.Count, Is.EqualTo(3));
            foreach (float progress in new[] { 0f, 1f })
            {
                Call(motion, "SetProgress", progress); Physics.SyncTransforms();
                Assert.That(blocker.enabled, Is.EqualTo(progress == 0));
                Assert.That(ramp.enabled, Is.EqualTo(progress == 1));
                foreach (float x in new[] { -.6f, -.3f, 0, .3f, .6f })
                {
                    var ray = new Ray(new Vector3(500 + x, 1.63f, 497), Vector3.back);
                    bool hit = colliders.Any(c => c.Raycast(ray, out _, 16));
                    Assert.That(hit, Is.EqualTo(progress == 0), "Gate opening ray at x=" + x + ", progress=" + progress);
                }
            }
            foreach (var collider in colliders) Object.Destroy(collider);
            yield return null;
        }
        [UnityTest, Timeout(90000)]
        public IEnumerator TemplePrayerFurnishingsPreserveInteractionAndPhysics()
        {
            Call(session, "ConfigureRecordDirectory", NewRecordFixture());
            Call(session, "CreatePrologue"); Begin(); yield return null;
            var world = Get<Component>(session, "Prologue");
            var table = world.transform.Find("Authored wooden prayer table");
            var jar = world.transform.Find("Authored ceramic offering jar");
            var vessel = world.transform.Find("Incense vessel");
            Assert.That(table, Is.Not.Null); Assert.That(jar, Is.Not.Null); Assert.That(vessel, Is.Not.Null);
            Assert.That(table.GetComponentsInChildren<Collider>(true), Is.Empty);
            Assert.That(jar.GetComponentsInChildren<Collider>(true), Is.Empty);
            Assert.That(vessel.GetComponentsInChildren<Collider>(true), Is.Empty);
            Assert.That(table.GetComponentsInChildren<MeshRenderer>(true).Count(r => r.enabled), Is.GreaterThan(0).And.LessThanOrEqualTo(7));
            Assert.That(jar.GetComponentsInChildren<MeshRenderer>(true).Any(r => r.enabled), Is.True);
            Assert.That(world.transform.Find("Prayer table top").GetComponent<BoxCollider>(), Is.Not.Null);
            Assert.That(world.transform.Find("Prayer table top").GetComponent<Renderer>().enabled, Is.False);
            PlacePlayer(new Vector3(500, .04f, 505.2f));
            player.GetType().GetField("pitch", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(player, 12f);
            yield return null; yield return null;
            Assert.That(Get<Component>(player, "Focus")?.name, Is.EqualTo("향로"));
        }
        [UnityTest, Timeout(90000)]
        public IEnumerator TempleOutdoorCameraAndFogAreRestoredWhenLeaving()
        {
            Call(session, "ConfigureRecordDirectory", NewRecordFixture());
            var eye = Get<Camera>(player, "eyes");
            var flags = eye.clearFlags; var background = eye.backgroundColor;
            var fog = RenderSettings.fog; var fogColor = RenderSettings.fogColor;
            var fogDensity = RenderSettings.fogDensity; var ambient = RenderSettings.ambientLight;
            Call(session, "CreatePrologue");
            Assert.That(eye.clearFlags, Is.EqualTo(CameraClearFlags.SolidColor));
            Assert.That(eye.backgroundColor, Is.Not.EqualTo(background));
            Call(session, "RetirePrologue");
            Assert.That(eye.clearFlags, Is.EqualTo(flags));
            Assert.That(eye.backgroundColor, Is.EqualTo(background));
            Assert.That(RenderSettings.fog, Is.EqualTo(fog));
            Assert.That(RenderSettings.fogColor, Is.EqualTo(fogColor));
            Assert.That(RenderSettings.fogDensity, Is.EqualTo(fogDensity));
            Assert.That(RenderSettings.ambientLight, Is.EqualTo(ambient));
            yield return null;
        }
        [UnityTest, Timeout(90000)]
        public IEnumerator TempleAllSavedPhasesRestoreTheirWorldAndVisibleGate()
        {
            Call(session, "ConfigureRecordDirectory", NewRecordFixture());
            Call(session, "CreatePrologue"); Begin(); yield return null;
            var world = Get<Component>(session, "Prologue");
            var data = Call(world, "Capture", 0f);
            Call(session, "RetirePrologue"); yield return null;
            string[] phases = { "FirstApproach", "FirstPrayer", "FirstDeparture", "SecondApproach", "SecondPrayer", "GateRevealed" };
            for (int phase = 0; phase < phases.Length; phase++)
            {
                bool incense = phase == 1 || phase == 2 || phase == 4 || phase == 5;
                Set(data, "phase", phase); Set(data, "incensePlaced", incense);
                Set(data, "prayerElapsed", phase == 1 || phase == 4 ? 1.5f : 0f);
                Set(data, "gateVisible", phase == 5);
                Call(session, "CreatePrologueForCheckpoint", data);
                world = Get<Component>(session, "Prologue");
                var director = Get<Component>(world, "Director");
                var presentation = world.GetComponent(RequireType("TempleProloguePresentation"));
                Assert.That(Get<object>(director, "Phase").ToString(), Is.EqualTo(phases[phase]));
                Assert.That(Get<bool>(presentation, "IncensePlaced"), Is.EqualTo(incense), phases[phase]);
                Assert.That(Get<bool>(presentation, "GateVisible"), Is.EqualTo(phase == 5), phases[phase]);
                Assert.That(Get<float>(director, "PrayerElapsed"), Is.EqualTo(phase == 1 || phase == 4 ? 1.5f : 0f));
                Call(world, "Capture", 0f);
                Call(session, "RetirePrologue"); yield return null;
            }
        }
        [UnityTest, Timeout(90000)]
        public IEnumerator TempleVersionOneCheckpointMigratesIncensePlacement()
        {
            Call(session, "ConfigureRecordDirectory", NewRecordFixture());
            Call(session, "CreatePrologue"); Begin(); yield return null;
            var world = Get<Component>(session, "Prologue");
            var data = Call(world, "Capture", 0f);
            var type = RequireType("TemplePrologueCheckpoint");
            Set(data, "version", 1); Set(data, "phase", 2); Set(data, "incensePlaced", false);
            var migrated = Call(type, "FromJson", JsonUtility.ToJson(data));
            Assert.That(Get<int>(migrated, "version"), Is.EqualTo(2));
            Assert.That(Get<bool>(migrated, "incensePlaced"), Is.True);
            Set(data, "phase", 1); Set(data, "prayerElapsed", .12f);
            migrated = Call(type, "FromJson", JsonUtility.ToJson(data));
            Assert.That(Get<bool>(migrated, "incensePlaced"), Is.False);
            Assert.That(Get<float>(migrated, "prayerElapsed"), Is.Zero);
            Set(data, "prayerElapsed", .5f);
            migrated = Call(type, "FromJson", JsonUtility.ToJson(data));
            Assert.That(Get<bool>(migrated, "incensePlaced"), Is.True);
            Assert.That(Get<float>(migrated, "prayerElapsed"), Is.EqualTo(.5f));
        }
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
            Assert.That(Get<bool>(source, "incensePlaced"), Is.False, "The checkpoint should preserve the pending hand placement");
            var previous = session; yield return RecoveryClick("suspend-temple"); yield return RecoveryRebind(previous);
            yield return RefreshSlotTitle(directory);
            Assert.That(File.Exists(Path.Combine(directory, "temple-suspend-v1.json")), Is.True);
            yield return RecoveryClick("continue-temple");
            Assert.That(RecoveryPage, Is.EqualTo("Pause"));
            Assert.That(Get<bool>(session, "PrologueMode"), Is.True);
            world = Get<Component>(session, "Prologue"); director = Get<Component>(world, "Director");
            Assert.That(Get<object>(director, "Phase").ToString(), Is.EqualTo("FirstPrayer"));
            var restoredPresentation = world.GetComponent(RequireType("TempleProloguePresentation"));
            Assert.That(Get<bool>(restoredPresentation, "IncensePending"), Is.True);
            Assert.That(Get<bool>(restoredPresentation, "IncensePlaced"), Is.False);
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
            var air = world.transform.Find("Forest air").GetComponent<AudioSource>();
            Assert.That(air.clip, Is.Not.Null);
            Assert.That(air.clip.name, Is.EqualTo("ambience-forest-0"));
            Assert.That(air.loop, Is.True);
            Assert.That(air.spatialBlend, Is.Zero, "Forest wind should not fade with distance from one point");
            var origin = new Vector3(500, 0, 500);
            Assert.That((bool)Call(world, "IsStoneApproach", origin + new Vector3(1.69f, .04f, -5)), Is.True);
            Assert.That((bool)Call(world, "IsStoneApproach", origin + new Vector3(1.71f, .04f, -5)), Is.False);
            Assert.That((bool)Call(world, "IsStoneApproach", origin + new Vector3(0, .6f, -5)), Is.False);
            var start = player.transform.position;
            yield return KeysObserved(Key.W); yield return Delay(1.2f); Keys(); yield return null;
            Assert.That(player.transform.position.z, Is.GreaterThan(start.z + 1f), "Forest path blocked normal walking");
            var feedback = Get<Component>(player, "Feedback");
            Assert.That(Get<string>(feedback, "LastFootstepSurface"), Is.EqualTo("stone"));
            Assert.That(Get<AudioClip>(feedback, "LastFootstepClip"), Is.Not.Null);
            Assert.That(CloudExternalAudioTests.MatchesFamily(Get<AudioClip>(feedback, "LastFootstepClip"), "step-stone", 5), Is.True);
            PlacePlayer(origin + new Vector3(3, .04f, -15)); yield return null;
            int beforeForestStep = Get<int>(feedback, "FootstepsPlayed");
            yield return KeysObserved(Key.W);
            yield return Wait(() => Get<int>(feedback, "FootstepsPlayed") > beforeForestStep, 4, "Walking on forest soil produced no step", InputDiagnostics);
            Keys(); yield return null;
            Assert.That(Get<string>(feedback, "LastFootstepSurface"), Is.EqualTo("forest"));
            Assert.That(CloudExternalAudioTests.MatchesFamily(Get<AudioClip>(feedback, "LastFootstepClip"), "step-forest", 4), Is.True);
            PlacePlayer(origin + new Vector3(0, .04f, 5.2f));
            var pitch = player.GetType().GetField("pitch", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(pitch, Is.Not.Null); pitch.SetValue(player, 12f);
            yield return null; yield return null;
            Assert.That(Get<Component>(player, "Focus")?.name, Is.EqualTo("향로"));
            yield return RecoveryPulse(Key.E);
            Assert.That(Get<object>(director, "Phase").ToString(), Is.EqualTo("FirstPrayer"));
            var presentation = world.GetComponent(RequireType("TempleProloguePresentation"));
            Assert.That(Get<bool>(presentation, "IncensePending"), Is.True);
            Assert.That(Get<float>(director, "PrayerElapsed"), Is.Zero, "Prayer started before the hand placed the incense");
            yield return Wait(() => world.GetComponentInChildren(RequireType("IncenseSmokeTrail")) != null, 3, "Incense did not appear after the authored release");
            Assert.That(Get<bool>(presentation, "IncensePlaced"), Is.True);
            Assert.That(Get<object>(Get<Component>(player, "Hands"), "PresentedItem").ToString(), Is.EqualTo("Incense"));
            var firstSmoke = world.GetComponentInChildren(RequireType("IncenseSmokeTrail"));
            AssertTempleSmokeAtVisibleTip(world, firstSmoke);
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
            var hand = Get<Component>(player, "Hands");
            var held = hand.GetType().GetField("held", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That((bool)(Transform)held.GetValue(hand), Is.False, "Incense reappeared before the second prayer");
            PlacePlayer(origin + new Vector3(0, .04f, 5.2f)); pitch.SetValue(player, 12f);
            yield return null; yield return null;
            Assert.That(Get<Component>(player, "Focus")?.name, Is.EqualTo("향로"));
            yield return RecoveryPulse(Key.E);
            yield return Wait(() => (bool)(Transform)held.GetValue(hand), 3, "Second incense was not held before placement");
            yield return Wait(() => world.GetComponentInChildren(RequireType("IncenseSmokeTrail")) != null, 3, "Second incense did not appear");
            yield return null;
            Assert.That((bool)(Transform)held.GetValue(hand), Is.False, "Second placed incense remained in the hand");
            var secondSmoke = world.GetComponentInChildren(RequireType("IncenseSmokeTrail"));
            AssertTempleSmokeAtVisibleTip(world, secondSmoke);
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
            var review = (Component)Call(world, "InstallGateWalkReview");
            yield return RecoveryPulse(Key.E);
            Assert.That(Get<bool>(review, "Started"), Is.True);
            var leftHinge = world.transform.Find("Review gate left hinge").GetComponent<AudioSource>();
            var rightHinge = world.transform.Find("Review gate right hinge").GetComponent<AudioSource>();
            var gateAudio = world.GetComponent(RequireType("TempleGateReviewAudio"));
            Assert.That(leftHinge.clip.name, Is.EqualTo("door-open-1"));
            Assert.That(rightHinge.clip.name, Is.EqualTo("door-open-0"));
            Assert.That(leftHinge.pitch, Is.EqualTo(.85f)); Assert.That(rightHinge.pitch, Is.EqualTo(.70f));
            Assert.That(leftHinge.clip.length / leftHinge.pitch, Is.GreaterThanOrEqualTo(1.65f));
            Assert.That(1.65f + rightHinge.clip.length / rightHinge.pitch, Is.InRange(2.9f, 3.05f));
            Assert.That(Get<bool>(gateAudio, "SecondTriggered"), Is.False);
            Assert.That(Get<bool>(shell, "IsReloading"), Is.False, "Opening should not transfer the player");
            yield return Wait(() => Get<float>(review, "Progress") > .2f, 5, "Review gate did not begin opening");
            Call(shell, "Pause");
            float opening = Get<float>(review, "Progress");
            yield return Delay(.3f);
            Assert.That(Get<float>(review, "Progress"), Is.EqualTo(opening), "Gate moved while paused");
            Assert.That(leftHinge.ignoreListenerPause, Is.False);
            Assert.That(rightHinge.ignoreListenerPause, Is.False);
            Assert.That(Get<bool>(gateAudio, "SecondTriggered"), Is.False);
            var openingSave = Call(world, "Capture", Get<float>(session, "ElapsedPlayTime"));
            Assert.That(Get<int>(openingSave, "version"), Is.EqualTo(3));
            Assert.That(Get<bool>(openingSave, "reviewGateStarted"), Is.True);
            Assert.That(Get<float>(openingSave, "reviewGateProgress"), Is.EqualTo(opening));
            var oldSession = session;
            yield return RecoveryClick("suspend-temple"); yield return RecoveryRebind(oldSession);
            yield return RefreshSlotTitle(directory); yield return RecoveryClick("continue-temple");
            Assert.That(RecoveryPage, Is.EqualTo("Pause"));
            world = Get<Component>(session, "Prologue");
            review = world.GetComponent(RequireType("TempleGateReviewMotion"));
            gateAudio = world.GetComponent(RequireType("TempleGateReviewAudio"));
            Assert.That(review, Is.Not.Null);
            Assert.That(Get<float>(review, "Progress"), Is.EqualTo(opening).Within(.001f));
            Assert.That(Get<bool>(gateAudio, "SecondTriggered"), Is.False);
            Assert.That(world.transform.Find("나타난 대문").GetComponent<BoxCollider>().enabled, Is.True);
            yield return RecoveryClick("resume");
            yield return Wait(() => Get<bool>(gateAudio, "SecondTriggered"), 5, "Second recorded hinge cue was not scheduled");
            Assert.That(Get<float>(review, "Progress"), Is.GreaterThanOrEqualTo(.55f));
            yield return Wait(() => Get<float>(review, "Progress") == 1f, 6, "Review gate did not open");
            Assert.That(Get<bool>(session, "CorridorMode"), Is.False, "Opening alone entered the corridor");
            PlacePlayer(origin + new Vector3(4, .04f, -10)); yield return null; yield return null;
            Assert.That(Get<bool>(world, "TransitionRequested"), Is.False, "Crossing outside the opening triggered transfer");
            PlacePlayer(origin + new Vector3(0, .04f, -5.4f)); player.transform.rotation = Quaternion.Euler(0, 180, 0);
            yield return null; yield return KeysObserved(Key.W);
            yield return Wait(() => player.transform.position.z < 491.85f, 8, "Player did not reach the gate ramp");
            Keys(); yield return null; Call(shell, "Pause");
            var openSave = Call(world, "Capture", Get<float>(session, "ElapsedPlayTime"));
            var savedPosition = Get<Vector3>(Get<object>(openSave, "player"), "position");
            Assert.That(Get<float>(openSave, "reviewGateProgress"), Is.EqualTo(1));
            oldSession = session;
            yield return RecoveryClick("suspend-temple"); yield return RecoveryRebind(oldSession);
            yield return RefreshSlotTitle(directory); yield return RecoveryClick("continue-temple");
            Assert.That(RecoveryPage, Is.EqualTo("Pause"));
            world = Get<Component>(session, "Prologue");
            review = world.GetComponent(RequireType("TempleGateReviewMotion"));
            gateAudio = world.GetComponent(RequireType("TempleGateReviewAudio"));
            Assert.That(Get<float>(review, "Progress"), Is.EqualTo(1));
            Assert.That(Get<bool>(gateAudio, "SecondTriggered"), Is.True);
            Assert.That(world.transform.Find("나타난 대문").GetComponent<BoxCollider>().enabled, Is.False);
            Assert.That(world.transform.Find("Review gate stair collision").GetComponent<BoxCollider>().enabled, Is.True);
            Assert.That(Vector3.Distance(player.transform.position, savedPosition), Is.LessThan(.03f));
            yield return RecoveryClick("resume");
            yield return KeysObserved(Key.W);
            yield return Wait(() => Get<bool>(world, "TransitionRequested"), 8, "Walking through the gate did not trigger transfer");
            Keys();
            yield return Wait(() => Get<bool>(session, "CorridorMode") && !Get<bool>(shell, "IsReloading"), 60, "Gate did not enter the real corridor");
            Assert.That(Get<bool>(session, "PrologueMode"), Is.False);
            Assert.That(Get<object>(shell, "Screen").ToString(), Is.EqualTo("Playing"));
            Assert.That(Get<int>(session, "TotalRecords"), Is.EqualTo(4));
        }
    }
}
