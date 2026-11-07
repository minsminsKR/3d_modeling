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
        public IEnumerator IncenseRibbonSurvivesSourceResetAndContinuesGrowing()
        {
            var smokeObject = new GameObject("Incense ribbon reset test");
            try
            {
                var trail = smokeObject.AddComponent(RequireType("IncenseSmokeTrail"));
                Call(trail, "Prepare", Get<Camera>(player, "eyes"));
                var mesh = smokeObject.GetComponent<MeshFilter>().sharedMesh;
                var source = new Vector3(0, 1, 0);
                Call(trail, "Advance", source, Vector3.forward, .01f, 1f, 1f);
                Assert.That(mesh.vertexCount, Is.Zero, "The ribbon appeared before its second sample");
                Call(trail, "Advance", source, Vector3.forward, .04f, 1f, 1f);
                Assert.That(mesh.vertexCount, Is.GreaterThan(0));
                Assert.That(mesh.GetIndexCount(0), Is.GreaterThan(0));
                var indices = mesh.triangles;
                Call(trail, "Advance", source + Vector3.right * .1f, Vector3.forward, .04f, 1f, 1f);
                CollectionAssert.AreEqual(indices, mesh.triangles, "The growing plume lost its surface");
                source += Vector3.right * 3;
                Call(trail, "Advance", source, Vector3.forward, .001f, 1f, 1f);
                Assert.That(mesh.vertexCount, Is.Zero, "A distant source retained the old trail");
                Call(trail, "Advance", source, Vector3.forward, .04f, 1f, 1f);
                Assert.That(mesh.vertexCount, Is.GreaterThan(0));
                CollectionAssert.AreEqual(indices, mesh.triangles, "The reset plume failed to rebuild its surface");
                Assert.That(mesh.bounds.center.x, Is.GreaterThan(2.5f), "The rebuilt plume remained at the old source");
            }
            finally { Object.Destroy(smokeObject); }
            yield return null;
        }
        [UnityTest, Timeout(90000)]
        public IEnumerator TempleProductionForestKeepsTrunkCapsulesAndReviewedPineLods()
        {
            Call(session, "ConfigureRecordDirectory", NewRecordFixture());
            Call(session, "CreatePrologue"); Begin(); yield return null;
            var world = Get<Component>(session, "Prologue");
            var roots = Enumerable.Range(0, 16).Select(i => world.transform.Find("Pine " + i)).ToArray();
            Assert.That(roots.All(root => root), Is.True);
            var colliders = roots.Select(root => root.GetComponent<CapsuleCollider>()).ToArray();
            var original = roots.Select(root => root.GetComponent<LODGroup>()).ToArray();
            var oldLevels = original.Select(group => group.GetLODs()).ToArray();
            var twoLevelPine = Resources.Load<GameObject>("TemplePrologue/pine-branches-visual");
            Assert.That(twoLevelPine, Is.Not.Null);
            var retainedFar = twoLevelPine.GetComponent<LODGroup>().GetLODs()[1].renderers[0]
                .GetComponent<MeshFilter>().sharedMesh;
            var forest = world.GetComponent(RequireType("TempleForestTrees"));
            Assert.That(Get<int>(forest, "TreeCount"), Is.EqualTo(16));
            Assert.That(Get<int>(forest, "AncientCount"), Is.EqualTo(2));
            Assert.That(Get<int>(forest, "PineCount"), Is.EqualTo(14));
            Physics.SyncTransforms();
            for (int i = 0; i < roots.Length; i++)
            {
                int row = i / 2;
                float side = i % 2 == 0 ? -1 : 1;
                float scale = .78f + (i % 4) * .11f;
                var expectedPosition = new Vector3(side * (5.5f + (row % 3) * 2.3f), -.15f, -23 + row * 6.5f);
                var originalRotation = Quaternion.Euler(0, (i * 83) % 360, 0);
                Assert.That(Vector3.Distance(roots[i].localPosition, expectedPosition), Is.LessThan(.0001f));
                Assert.That(Quaternion.Angle(roots[i].localRotation, originalRotation), Is.LessThan(.05f));
                Assert.That(Vector3.Distance(roots[i].localScale, Vector3.one * scale), Is.LessThan(.0001f));
                Assert.That(roots[i].GetComponent<CapsuleCollider>(), Is.SameAs(colliders[i]));
                Assert.That(colliders[i].enabled, Is.True);
                Assert.That(colliders[i].radius, Is.EqualTo(.35f).Within(.0001f));
                Assert.That(colliders[i].height, Is.EqualTo(4).Within(.0001f));
                Assert.That(colliders[i].center, Is.EqualTo(new Vector3(0, 2, 0)));
                Assert.That(colliders[i].direction, Is.EqualTo(1));
                var ray = new Ray(roots[i].TransformPoint(new Vector3(-2, 2, 0)), roots[i].TransformDirection(Vector3.right));
                Assert.That(colliders[i].Raycast(ray, out var hit, 4 * roots[i].lossyScale.x), Is.True);
                Assert.That(hit.distance, Is.EqualTo(1.65f * roots[i].lossyScale.x).Within(.002f),
                    "The original trunk collision radius changed in world space");
                Assert.That(original[i].enabled, Is.False);
                Assert.That(oldLevels[i].SelectMany(level => level.renderers).All(renderer => !renderer.enabled), Is.True);
                bool ancient = (bool)Call(forest, "IsAncient", roots[i]);
                Assert.That(ancient, Is.EqualTo(i == 2 || i == 3));
                var replacement = roots[i].Find(ancient ? "Ancient forest tree" : "Reviewed forest pine");
                Assert.That(replacement, Is.Not.Null);
                Assert.That(replacement.GetComponentInChildren<Collider>(true), Is.Null);
                var desiredRotation = Quaternion.Euler((row % 3 - 1) * 2.2f, (i * 83) % 360, side * (1.5f + row % 3));
                Assert.That(Quaternion.Angle(roots[i].localRotation * replacement.localRotation, desiredRotation), Is.LessThan(.05f));
                var expectedVisualScale = new Vector3(1 + .07f * (row % 3 - 1), 1, 1 + .05f * ((row + 1) % 3 - 1));
                Assert.That(Vector3.Distance(replacement.localScale, expectedVisualScale), Is.LessThan(.0001f));
                var group = replacement.GetComponent<LODGroup>(); var levels = group.GetLODs();
                Assert.That(levels.Length, Is.EqualTo(ancient ? 2 : 3));
                if (ancient) continue;
                Assert.That(levels[0].screenRelativeTransitionHeight, Is.EqualTo(.18f).Within(.0001f));
                Assert.That(levels[1].screenRelativeTransitionHeight, Is.EqualTo(.06f).Within(.0001f));
                Assert.That(levels[2].screenRelativeTransitionHeight, Is.EqualTo(.015f).Within(.0001f));
                Assert.That(levels[0].renderers[0].GetComponent<MeshFilter>().sharedMesh.GetIndexCount(0) / 3,
                    Is.InRange(250000, 260000));
                var middle = levels[1].renderers[0].GetComponent<MeshFilter>().sharedMesh;
                Assert.That(middle.GetIndexCount(0) / 3, Is.InRange(170000, 180000));
                var distant = levels[2].renderers[0].GetComponent<MeshFilter>().sharedMesh;
                Assert.That(distant, Is.SameAs(retainedFar), "The third LOD must reuse the existing distant pine mesh");
                Assert.That(distant, Is.Not.SameAs(middle));
                Assert.That(distant, Is.Not.SameAs(oldLevels[i][1].renderers[0].GetComponent<MeshFilter>().sharedMesh));
                Assert.That(distant.GetIndexCount(0) / 3, Is.EqualTo(40697));
                Assert.That(levels[0].renderers[0].sharedMaterial,
                    Is.SameAs(oldLevels[i][0].renderers[0].sharedMaterial));
                Assert.That(levels[1].renderers[0].sharedMaterial,
                    Is.SameAs(oldLevels[i][1].renderers[0].sharedMaterial));
                Assert.That(levels[2].renderers[0].sharedMaterial,
                    Is.SameAs(oldLevels[i][1].renderers[0].sharedMaterial));
                Assert.That(group.size, Is.EqualTo(original[i].size).Within(.25f));
            }
            Assert.Throws<System.InvalidOperationException>(() => Call(forest, "Prepare"));
        }
        [UnityTest, Timeout(90000)]
        public IEnumerator TempleNativeReviewWalkingFixtureProducesForestFootsteps()
        {
            Call(session, "ConfigureRecordDirectory", NewRecordFixture());
            Call(session, "CreatePrologue"); Begin(); yield return null;
            var field = RequireType("NativeTempleReview").GetField("ForestWalkFeet", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            PlacePlayer(new Vector3(500, 0, 500) + (Vector3)field.GetValue(null)); yield return null;
            var feedback = Get<Component>(player, "Feedback");
            int before = Get<int>(feedback, "FootstepsPlayed");
            try
            {
                yield return KeysObserved(Key.W); yield return Delay(4); Keys(); yield return null;
                Assert.That(Get<int>(feedback, "FootstepsPlayed") - before, Is.GreaterThan(0));
                Assert.That(Get<string>(feedback, "LastFootstepSurface"), Is.EqualTo("forest"),
                    "Native review's walking fixture must actually walk on forest soil");
            }
            finally { Keys(); }
        }
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
            Set(checkpoint, "phase", 1); Set(checkpoint, "firstClueRead", true); Set(checkpoint, "prayerElapsed", 0f); Set(checkpoint, "incensePlaced", false);
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
            Set(checkpoint, "phase", 1); Set(checkpoint, "firstClueRead", true); Set(checkpoint, "prayerElapsed", 0f); Set(checkpoint, "incensePlaced", false);
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
            var trail = world.transform.Find("Placed incense smoke")?.GetComponent(RequireType("IncenseSmokeTrail"));
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
            Set(checkpoint, "phase", 5); Set(checkpoint, "firstClueRead", true); Set(checkpoint, "secondClueRead", true); Set(checkpoint, "incensePlaced", true); Set(checkpoint, "gateVisible", false);
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
            Set(checkpoint, "phase", 5); Set(checkpoint, "firstClueRead", true); Set(checkpoint, "secondClueRead", true); Set(checkpoint, "incensePlaced", true); Set(checkpoint, "gateVisible", true);
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
            Assert.That(cryStart, Is.LessThanOrEqualTo(.075f), "Distant crying must remain a quiet background cue");
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
            bool original = System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-v2-forest-earth-original") >= 0;
            Assert.That(texture.wrapMode, Is.EqualTo(original ? TextureWrapMode.Mirror : TextureWrapMode.Repeat));
            Assert.That(renderer.sharedMaterial.GetTexture("_BumpMap"), Is.Not.Null);
            Assert.That(renderer.sharedMaterial.IsKeywordEnabled("_NORMALMAP"), Is.True);
            var pixels = texture.GetPixels32();
            int minimum = pixels.Min(pixel => (int)pixel.r), maximum = pixels.Max(pixel => (int)pixel.r);
            double mean = pixels.Average(pixel => (double)pixel.r);
            Assert.That(maximum - minimum, Is.GreaterThanOrEqualTo(5), "Forest soil lost its texture variation");
            Assert.That(mean, Is.InRange(30, 90), "Forest soil changed its overall brightness");
            // Repeat crosses opposite imported edges. Compare that actual
            // neighbour gradient against ordinary first/last interior steps;
            // forcing the opposite pixels equal would be the wrong criterion.
            // The normal map remains GPU-only and is reviewed separately.
            if (!original)
            {
                AssertImportedSoilRepeatEdge(pixels, texture.width, texture.height, true);
                AssertImportedSoilRepeatEdge(pixels, texture.width, texture.height, false);
            }
            Assert.That(texture.mipmapCount, Is.GreaterThan(1));
            Assert.That(texture.filterMode, Is.EqualTo(FilterMode.Trilinear));
        }
        static void AssertImportedSoilRepeatEdge(Color32[] pixels, int width, int height, bool vertical)
        {
            var edge = new int[256]; var inner = new int[256];
            int count = vertical ? height : width;
            double edgeSum = 0, innerSum = 0;
            void Add(Color32 a, Color32 b, int[] histogram, ref double sum)
            {
                int r = Mathf.Abs(a.r-b.r), g = Mathf.Abs(a.g-b.g), blue = Mathf.Abs(a.b-b.b);
                histogram[r]++; histogram[g]++; histogram[blue]++; sum += r+g+blue;
            }
            for (int i=0; i<count; i++)
            {
                int first = vertical ? i*width : i;
                int last = vertical ? first+width-1 : (height-1)*width+i;
                int next = vertical ? first+1 : first+width;
                int previous = vertical ? last-1 : last-width;
                Add(pixels[first], pixels[last], edge, ref edgeSum);
                Add(pixels[first], pixels[next], inner, ref innerSum);
                Add(pixels[last], pixels[previous], inner, ref innerSum);
            }
            float edgeMean=(float)(edgeSum/(count*3)), innerMean=(float)(innerSum/(count*6));
            int Percentile(int[] histogram, int total)
            {
                int target=Mathf.CeilToInt(total*.95f), seen=0;
                for (int n=0; n<histogram.Length; n++) { seen+=histogram[n]; if (seen>=target) return n; }
                return 255;
            }
            int edgeP95=Percentile(edge,count*3), innerP95=Percentile(inner,count*6);
            TestContext.Out.WriteLine($"Imported Repeat {(vertical ? "U" : "V")} edge mean {edgeMean:F2} vs {innerMean:F2}; p95 {edgeP95} vs {innerP95}");
            Assert.That(edgeMean, Is.LessThanOrEqualTo(Mathf.Max(innerMean*1.5f, innerMean+3f)), "Repeat edge average gradient exceeds neighbouring imported pixels");
            Assert.That(edgeP95, Is.LessThanOrEqualTo(Mathf.Max(innerP95*1.5f, innerP95+8f)), "Repeat edge high gradient exceeds neighbouring imported pixels");
        }
        [UnityTest, Timeout(90000)]
        public IEnumerator TempleGateAudioRestoresDuringSecondHingeCue()
        {
            Call(session, "ConfigureRecordDirectory", NewRecordFixture());
            Call(session, "CreatePrologue"); Begin(); yield return null;
            var world = Get<Component>(session, "Prologue");
            Call(world, "InstallGateWalkReview");
            var data = Call(world, "Capture", 0f);
            Set(data, "phase", 5); Set(data, "firstClueRead", true); Set(data, "secondClueRead", true); Set(data, "incensePlaced", true); Set(data, "gateVisible", true);
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
            Set(data, "phase", 5); Set(data, "firstClueRead", true); Set(data, "secondClueRead", true); Set(data, "incensePlaced", true); Set(data, "gateVisible", true);
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
            Set(data, "phase", 5); Set(data, "firstClueRead", true); Set(data, "secondClueRead", true); Set(data, "incensePlaced", true); Set(data, "gateVisible", true);
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
            Set(data, "phase", 5); Set(data, "firstClueRead", true); Set(data, "secondClueRead", true); Set(data, "incensePlaced", true); Set(data, "gateVisible", true);
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
            Call(world, "ReadStoryClue");
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
            Set(data, "phase", 2); Set(data, "firstClueRead", true); Set(data, "incensePlaced", true);
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
            Call(world, "ReadStoryClue");
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
            Set(data, "phase", 5); Set(data, "firstClueRead", true); Set(data, "secondClueRead", true); Set(data, "incensePlaced", true); Set(data, "gateVisible", false);
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
            Set(data, "phase", 5); Set(data, "firstClueRead", true); Set(data, "secondClueRead", true); Set(data, "incensePlaced", true); Set(data, "gateVisible", true);
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
        public IEnumerator TempleAltarBindingReusesPaperWithoutChangingInteractionOrLeavingResources()
        {
            Call(session, "ConfigureRecordDirectory", NewRecordFixture());
            Call(session, "CreatePrologue"); Begin(); yield return null;
            var world = Get<Component>(session, "Prologue");
            var vaporObject = world.transform.Find("Altar spectral vapor");
            Assert.That(vaporObject, Is.Not.Null);
            var vapor = vaporObject.GetComponent(RequireType("TempleAltarVapor"));
            Assert.That(vapor, Is.Not.Null);
            if (Get<bool>(shell, "ReducedMotion")) Call(shell, "ToggleReducedMotion");
            // The production effect intentionally sleeps beyond 20 metres.
            // Approach before checking its active simulation and pause behavior.
            PlacePlayer(new Vector3(500, .04f, 505.2f));
            yield return null;
            yield return Wait(() => Get<int>(vapor, "SampleCount") > 2, 5, "Nearby spectral altar vapor did not advance");
            var vaporMesh = vaporObject.GetComponent<MeshFilter>().sharedMesh;
            var vaporRenderer = vaporObject.GetComponent<MeshRenderer>();
            var vaporMaterial = vaporRenderer.sharedMaterial;
            Assert.That(vaporRenderer.enabled, Is.True);
            int nearSamples = Get<int>(vapor, "SampleCount");
            PlacePlayer(new Vector3(500, .04f, 480)); yield return null; yield return null;
            Assert.That(vaporRenderer.enabled, Is.False, "Distant altar vapor should not render");
            float distantTime = Get<float>(vapor, "SimulationTime"); yield return Delay(.2f);
            Assert.That(Get<float>(vapor, "SimulationTime"), Is.EqualTo(distantTime), "Distant vapor simulation must hold");
            Assert.That(Get<int>(vapor, "SampleCount"), Is.EqualTo(nearSamples), "Distant vapor history must not reset");
            PlacePlayer(new Vector3(500, .04f, 505.2f)); yield return null; yield return null;
            Assert.That(vaporRenderer.enabled, Is.True, "Returning to the altar must show the same effect");
            yield return Wait(() => Get<float>(vapor, "SimulationTime") > distantTime, 5, "Vapor must resume when approached");
            Assert.That(vaporObject.GetComponent<MeshFilter>().sharedMesh, Is.SameAs(vaporMesh), "Re-entry must reuse the existing mesh");
            Call(shell, "Pause"); float vaporTime = Get<float>(vapor, "SimulationTime");
            yield return Delay(.2f);
            Assert.That(Get<float>(vapor, "SimulationTime"), Is.EqualTo(vaporTime), "Pause must hold altar vapor");
            Call(shell, "Resume"); yield return null;
            Call(shell, "ToggleReducedMotion"); yield return null;
            float comfortTime = Get<float>(vapor, "SimulationTime"); yield return Delay(.2f);
            Assert.That(Get<float>(vapor, "SimulationTime"), Is.EqualTo(comfortTime), "Reduced motion must hold altar vapor");
            Call(shell, "ToggleReducedMotion"); yield return null;
            var binding = world.transform.Find("Intruding altar binding");
            Assert.That(binding, Is.Not.Null);
            Assert.That(binding.GetComponentsInChildren<Collider>(true), Is.Empty);
            Assert.That(binding.GetComponentsInChildren<Rigidbody>(true), Is.Empty);
            var seals = binding.Cast<Transform>().Where(x => x.name == "Inked altar paper seal").ToArray();
            Assert.That(seals.Length, Is.EqualTo(7));
            Assert.That(binding.GetComponentsInChildren<Transform>(true).Count(x => x.name == "Brush marks on altar paper"), Is.EqualTo(4));
            var foldedFilters = binding.GetComponentsInChildren<MeshFilter>(true)
                .Where(x => x.sharedMesh && x.sharedMesh.name == "Owned folded ritual paper").ToArray();
            Assert.That(foldedFilters.Length, Is.EqualTo(3));
            var folded = foldedFilters.Select(x => x.sharedMesh).ToArray();
            Assert.That(folded.All(x => x.vertexCount > 0 && x.uv.Length == x.vertexCount && x.normals.Length == x.vertexCount), Is.True);
            foreach (var filter in foldedFilters)
            {
                var holder = filter.transform;
                while (holder && holder.name != "Inked altar paper seal") holder = holder.parent;
                Assert.That(holder, Is.Not.Null);
                AssertTwoSidedRitualZigzag(filter, holder);
            }
            var cord = binding.Find("Weathered braided altar cord").GetComponent<MeshFilter>().sharedMesh;
            Assert.That(cord.vertexCount, Is.GreaterThan(0));
            Assert.That(cord.bounds.size.x, Is.GreaterThan(1.6f), "Cord must span the altar front");
            Assert.That(cord.bounds.size.y, Is.GreaterThan(.1f), "Cord should visibly sag across the altar");
            Assert.That(binding.GetComponentsInChildren<MeshRenderer>(true).Count(x => x.enabled), Is.InRange(1, 3));
            Assert.That(world.transform.Find("Prayer table top").GetComponent<BoxCollider>().enabled, Is.True);
            PlacePlayer(new Vector3(500, .04f, 505.2f));
            player.GetType().GetField("pitch", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(player, 12f);
            yield return null; yield return null;
            Assert.That(Get<Component>(player, "Focus")?.name, Is.EqualTo("향로"));
            // A restored placed incense keeps its ordinary white trail even
            // while the unrelated ambient altar effect is present.
            var presentation = world.GetComponent(RequireType("TempleProloguePresentation"));
            Call(presentation, "RestoreIncense", true, player); yield return null;
            var incenseObject = world.transform.Find("Placed incense smoke");
            Assert.That(incenseObject, Is.Not.Null);
            var incense = incenseObject.GetComponent(RequireType("IncenseSmokeTrail"));
            Assert.That(incense, Is.Not.Null); Assert.That(incense, Is.Not.SameAs(vapor));
            yield return Wait(() => Get<int>(incense, "SampleCount") > 2, 5, "Placed incense did not advance independently");
            Assert.That(incenseObject.GetComponent<MeshRenderer>().sharedMaterial, Is.Not.SameAs(vaporMaterial));
            Call(session, "RetirePrologue"); yield return null; yield return null;
            Assert.That(folded.All(x => !x), Is.True, "Folded copies must be released with the prologue");
            Assert.That(!cord, Is.True, "Generated cord must be released with the prologue");
            Assert.That(!vaporMesh && !vaporMaterial, Is.True, "Vapor mesh and material must be released with the prologue");
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
            var previousSky = RenderSettings.skybox;
            Call(session, "CreatePrologue");
            var skyShader = Resources.Load<Shader>("TempleForestNightSky");
            bool skySupported = SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null || skyShader.isSupported;
            Assert.That(eye.clearFlags, Is.EqualTo(skySupported ? CameraClearFlags.Skybox : CameraClearFlags.SolidColor));
            var ownedSky = RenderSettings.skybox;
            if (skySupported)
            {
                Assert.That(ownedSky, Is.Not.SameAs(previousSky));
                Assert.That(ownedSky.shader, Is.SameAs(skyShader));
            }
            Assert.That(eye.backgroundColor, Is.Not.EqualTo(background));
            Call(session, "RetirePrologue");
            Assert.That(RenderSettings.skybox, Is.SameAs(previousSky));
            Assert.That(eye.clearFlags, Is.EqualTo(flags));
            Assert.That(eye.backgroundColor, Is.EqualTo(background));
            Assert.That(RenderSettings.fog, Is.EqualTo(fog));
            Assert.That(RenderSettings.fogColor, Is.EqualTo(fogColor));
            Assert.That(RenderSettings.fogDensity, Is.EqualTo(fogDensity));
            Assert.That(RenderSettings.ambientLight, Is.EqualTo(ambient));
            yield return null;
            if (skySupported) Assert.That((bool)ownedSky, Is.False, "The owned sky material was not released");
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
                Set(data, "phase", phase); Set(data, "firstClueRead", phase >= 1); Set(data, "secondClueRead", phase >= 4); Set(data, "incensePlaced", incense);
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
            Set(data, "version", 1); Set(data, "storyVersion", 0); Set(data, "firstClueRead", false); Set(data, "secondClueRead", false); Set(data, "phase", 2); Set(data, "incensePlaced", false);
            string LegacyJson()
            {
                string json = JsonUtility.ToJson(data);
                json = System.Text.RegularExpressions.Regex.Replace(json, ",\"storyVersion\":-?[0-9]+", "");
                json = System.Text.RegularExpressions.Regex.Replace(json, ",\"legacyClueExemptMask\":-?[0-9]+", "");
                json = System.Text.RegularExpressions.Regex.Replace(json, ",\"firstClueRead\":(true|false)", "");
                json = System.Text.RegularExpressions.Regex.Replace(json, ",\"secondClueRead\":(true|false)", "");
                return json;
            }
            var migrated = Call(type, "FromJson", LegacyJson());
            Assert.That(Get<int>(migrated, "version"), Is.EqualTo(2));
            Assert.That(Get<bool>(migrated, "incensePlaced"), Is.True);
            Set(data, "phase", 1); Set(data, "firstClueRead", false); Set(data, "prayerElapsed", .12f);
            migrated = Call(type, "FromJson", LegacyJson());
            Assert.That(Get<bool>(migrated, "incensePlaced"), Is.False);
            Assert.That(Get<float>(migrated, "prayerElapsed"), Is.Zero);
            Set(data, "prayerElapsed", .5f);
            migrated = Call(type, "FromJson", LegacyJson());
            Assert.That(Get<bool>(migrated, "incensePlaced"), Is.True);
            Assert.That(Get<float>(migrated, "prayerElapsed"), Is.EqualTo(.5f));
        }
        // Real test devices aim at the physical interaction target; no OS
        // mouse or keyboard events are injected and no player teleport occurs.
        IEnumerator TempleAimAt(Transform target, Mouse mouse, bool gamepad)
        {
            Assert.That(target, Is.Not.Null);
            var pitch = player.GetType().GetField("pitch", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(pitch, Is.Not.Null);
            for (int frame = 0; frame < 240; frame++)
            {
                if (Get<Component>(player, "Focus")?.name == target.name)
                {
                    if (gamepad) { InputSystem.QueueStateEvent(auditPad, new GamepadState()); yield return null; }
                    else { InputSystem.QueueStateEvent(mouse, new MouseState()); yield return null; }
                    yield break;
                }
                var eye = Get<Camera>(player, "eyes").transform;
                var direction = target.position - eye.position;
                float desiredYaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
                float desiredPitch = -Mathf.Atan2(direction.y, new Vector2(direction.x, direction.z).magnitude) * Mathf.Rad2Deg;
                float yawError = Mathf.DeltaAngle(player.transform.eulerAngles.y, desiredYaw);
                float pitchError = desiredPitch - (float)pitch.GetValue(player);
                if (gamepad)
                {
                    var stick = Vector2.ClampMagnitude(new Vector2(yawError, -pitchError) * .035f, .8f);
                    InputSystem.QueueStateEvent(auditPad, new GamepadState { rightStick = stick });
                    yield return null;
                }
                else
                {
                    float sensitivity = Get<float>(player, "sensitivity");
                    InputSystem.QueueStateEvent(mouse, new MouseState { delta = new Vector2(yawError, -pitchError) / sensitivity });
                    yield return null;
                    InputSystem.QueueStateEvent(mouse, new MouseState()); yield return null;
                }
            }
            Assert.Fail("Could not aim at the physical temple target: " + target.name + "; " + InputDiagnostics());
        }
        IEnumerator TempleStepAside(bool gamepad)
        {
            // The incense target sits in front of the note from the central
            // path; walk right before aiming rather than raycasting through it.
            if (gamepad) yield return PadState(new GamepadState { leftStick = Vector2.right }, .01f);
            else yield return KeysObserved(Key.D);
            yield return Wait(() => player.transform.position.x >= 500.64f, 5,
                "The player could not step aside to inspect the note", InputDiagnostics);
            if (gamepad) yield return PadState(new GamepadState(), .01f);
            else { Keys(); yield return null; }
        }
        IEnumerator TempleFaceForward(Mouse mouse, bool gamepad)
        {
            for (int frame = 0; frame < 240; frame++)
            {
                float error = Mathf.DeltaAngle(player.transform.eulerAngles.y, 0);
                if (Mathf.Abs(error) < 1.5f)
                {
                    if (gamepad) InputSystem.QueueStateEvent(auditPad, new GamepadState());
                    else InputSystem.QueueStateEvent(mouse, new MouseState());
                    yield return null; yield break;
                }
                if (gamepad)
                {
                    // The gamepad stick applies a dead zone; proportional
                    // commands below it never turn the player. Keep a small
                    // usable input until the real yaw reaches the tolerance.
                    float stick = Mathf.Clamp(error * .035f, -.8f, .8f);
                    if (Mathf.Abs(stick) < .2f) stick = Mathf.Sign(error) * .2f;
                    InputSystem.QueueStateEvent(auditPad, new GamepadState { rightStick = new Vector2(stick, 0) });
                }
                else
                    InputSystem.QueueStateEvent(mouse, new MouseState { delta = new Vector2(error / Get<float>(player, "sensitivity"), 0) });
                yield return null;
                if (!gamepad) { InputSystem.QueueStateEvent(mouse, new MouseState()); yield return null; }
            }
            Assert.Fail("Could not face forward after inspecting the note; yaw=" + player.transform.eulerAngles.y + "; " + InputDiagnostics());
        }
        IEnumerator TempleReadClueWithMouse(Component world, Mouse mouse)
        {
            var note = world.transform.Find("이름이 비어 있는 쪽지");
            yield return TempleAimAt(note, mouse, false);
            yield return RecoveryPulse(Key.E);
            yield return null;
            yield return TempleAimAt(world.transform.Find("향로"), mouse, false);
        }
        IEnumerator TempleReadClueWithTemporaryMouse(Component world)
        {
            var mouse = InputSystem.AddDevice<Mouse>();
            try { yield return TempleReadClueWithMouse(world, mouse); }
            finally { if (mouse.added) InputSystem.RemoveDevice(mouse); }
        }
        IEnumerator TempleReadClueWithPad(Component world)
        {
            var note = world.transform.Find("이름이 비어 있는 쪽지");
            yield return TempleAimAt(note, null, true);
            yield return PadPulse(GamepadButton.South);
            yield return null;
            yield return TempleAimAt(world.transform.Find("향로"), null, true);
        }
        [UnityTest, Timeout(120000)]
        public IEnumerator TempleCluesRequireRealInputAndSurviveSavingAndRereading()
        {
            string directory = NewRecordFixture();
            Call(session, "ConfigureRecordDirectory", directory);
            Call(session, "CreatePrologue"); Begin(); yield return null;
            var world = Get<Component>(session, "Prologue");
            var director = Get<Component>(world, "Director");
            PlacePlayer(new Vector3(500, .04f, 505.8f)); yield return null;
            var mouse = InputSystem.AddDevice<Mouse>();
            try
            {
                yield return TempleAimAt(world.transform.Find("향로"), mouse, false);
                yield return RecoveryPulse(Key.E);
                Assert.That(Get<object>(director, "Phase").ToString(), Is.EqualTo("FirstApproach"));
                Assert.That(Get<string>(session, "Notice"), Does.Contain("쪽지"));
                yield return TempleFaceForward(mouse, false);
                yield return TempleStepAside(false);
                yield return TempleReadClueWithMouse(world, mouse);
                var first = Call(world, "Capture", 0f);
                Assert.That(Get<bool>(first, "firstClueRead"), Is.True);
                Assert.That(Get<bool>(first, "secondClueRead"), Is.False);
                Assert.That((bool)Call(Get<object>(session, "TempleSuspension"), "Save", first), Is.True);
                var saved = Get<object>(Get<object>(session, "TempleSuspension"), "Snapshot");
                Call(session, "RetirePrologue"); yield return null;
                Call(session, "CreatePrologueForCheckpoint", saved); yield return null;
                world = Get<Component>(session, "Prologue");
                yield return TempleReadClueWithMouse(world, mouse);
                var reread = Call(world, "Capture", 0f);
                Assert.That(Get<bool>(reread, "firstClueRead"), Is.True);
                Assert.That(Get<bool>(reread, "secondClueRead"), Is.False);
                Set(reread, "phase", 3);
                Call(session, "RetirePrologue"); yield return null;
                Call(session, "CreatePrologueForCheckpoint", reread); yield return null;
                world = Get<Component>(session, "Prologue"); director = Get<Component>(world, "Director");
                AddAuditPad();
                yield return TempleAimAt(world.transform.Find("향로"), null, true);
                yield return PadPulse(GamepadButton.South);
                Assert.That(Get<object>(director, "Phase").ToString(), Is.EqualTo("SecondApproach"));
                Assert.That(Get<string>(session, "Notice"), Does.Contain("쪽지"));
                yield return TempleReadClueWithPad(world);
                var second = Call(world, "Capture", 0f);
                Assert.That(Get<bool>(second, "firstClueRead"), Is.True);
                Assert.That(Get<bool>(second, "secondClueRead"), Is.True);
                yield return TempleReadClueWithPad(world);
                var repeated = Call(world, "Capture", 0f);
                Assert.That(Get<bool>(repeated, "firstClueRead"), Is.True);
                Assert.That(Get<bool>(repeated, "secondClueRead"), Is.True);
            }
            finally { if (mouse.added) InputSystem.RemoveDevice(mouse); }
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
                yield return Wait(() => player.transform.position.z >= 505.8f, 20, "Walking from spawn to incense was blocked", InputDiagnostics);
                Keys(); yield return null;
                yield return TempleStepAside(false);
                yield return TempleReadClueWithMouse(world, mouse);
                Assert.That(Get<bool>(Call(world, "Capture", 0f), "firstClueRead"), Is.True);
                yield return Wait(() => Get<Component>(player, "Focus")?.name == "향로", 3, "Mouse could not aim at the incense target", InputDiagnostics);
                yield return RecoveryPulse(Key.E);
                yield return Wait(() => Get<object>(director, "Phase").ToString() == "FirstDeparture", 8, "First prayer did not finish");
                yield return TempleFaceForward(mouse, false);
                yield return KeysObserved(Key.S);
                yield return Wait(() => Get<object>(director, "Phase").ToString() == "SecondApproach", 20, "Walking home did not start the next day", InputDiagnostics);
                Keys(); yield return Wait(() => !Get<bool>(shell, "IsReloading"), 8, "Next day did not reveal");
                yield return KeysObserved(Key.W);
                yield return Wait(() => player.transform.position.z >= 505.8f, 20, "Second walk to incense was blocked", InputDiagnostics);
                Keys(); yield return null;
                yield return TempleStepAside(false);
                yield return TempleReadClueWithMouse(world, mouse);
                Assert.That(Get<bool>(Call(world, "Capture", 0f), "secondClueRead"), Is.True);
                yield return Wait(() => Get<Component>(player, "Focus")?.name == "향로", 3, "Second incense focus missing", InputDiagnostics);
                yield return RecoveryPulse(Key.E);
                yield return Wait(() => Get<object>(director, "Phase").ToString() == "GateRevealed", 8, "Second prayer did not finish");
                yield return TempleFaceForward(mouse, false);
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
        [UnityTest, Timeout(180000)]
        public IEnumerator TempleFullWalkingRouteUsesGamepadWithoutTeleporting()
        {
            Call(session, "ConfigureRecordDirectory", NewRecordFixture());
            yield return RecoveryUiReady(); Call(shell, "RestoreDefaultSettings"); AddAuditPad();
            yield return PadPulse(GamepadButton.South); yield return RecoveryWaitForTempleEntry();
            var world = Get<Component>(session, "Prologue"); var director = Get<Component>(world, "Director");
            yield return PadState(new GamepadState { leftStick = Vector2.up });
            yield return Wait(() => player.transform.position.z >= 505.8f, 20, "Gamepad could not walk from spawn to the incense");
            yield return TempleStepAside(true);
            yield return TempleReadClueWithPad(world);
            Assert.That(Get<bool>(Call(world, "Capture", 0f), "firstClueRead"), Is.True);
            yield return Wait(() => Get<Component>(player, "Focus")?.name == "향로", 3, "Gamepad could not aim at the incense");
            yield return PadState(new GamepadState()); yield return PadPulse(GamepadButton.South);
            yield return Wait(() => Get<object>(director, "Phase").ToString() == "FirstDeparture", 8, "Gamepad first prayer did not finish");
            yield return TempleFaceForward(null, true);
            yield return PadState(new GamepadState { leftStick = Vector2.down });
            yield return Wait(() => Get<object>(director, "Phase").ToString() == "SecondApproach", 20, "Gamepad could not walk home");
            yield return PadState(new GamepadState());
            yield return Wait(() => !Get<bool>(shell, "IsReloading"), 8, "Gamepad next day did not reveal");
            yield return PadState(new GamepadState { leftStick = Vector2.up });
            yield return Wait(() => player.transform.position.z >= 505.8f, 20, "Gamepad second walk was blocked");
            yield return PadState(new GamepadState());
            yield return TempleStepAside(true);
            yield return TempleReadClueWithPad(world);
            Assert.That(Get<bool>(Call(world, "Capture", 0f), "secondClueRead"), Is.True);
            yield return Wait(() => Get<Component>(player, "Focus")?.name == "향로", 3, "Gamepad second incense focus missing");
            yield return PadPulse(GamepadButton.South);
            yield return Wait(() => Get<object>(director, "Phase").ToString() == "GateRevealed", 8, "Gamepad second prayer did not finish");
            yield return TempleFaceForward(null, true);
            yield return PadState(new GamepadState { leftStick = Vector2.down });
            yield return Wait(() => player.transform.position.z <= 494.6f, 12, "Gamepad walk back to the gate was blocked");
            yield return PadState(new GamepadState { rightStick = Vector2.right }, .01f);
            yield return Wait(() => Get<Component>(player, "Focus")?.name == "나타난 대문", 3, "Gamepad could not turn to the gate");
            yield return PadState(new GamepadState()); yield return PadPulse(GamepadButton.South);
            yield return Wait(() => Get<bool>(session, "CorridorMode") && !Get<bool>(shell, "IsReloading"), 60,
                "Gamepad gate interaction did not lead to the corridor");
        }
        [UnityTest, Timeout(90000)]
        public IEnumerator TempleSameTokenExternalEditIsPreservedBeforeSaveAndContinue()
        {
            string directory = NewRecordFixture();
            Call(session, "ConfigureRecordDirectory", directory);
            Call(session, "CreatePrologue"); Begin(); yield return null;
            var checkpoint = Call(Get<Component>(session, "Prologue"), "Capture", 0f);
            var initial = Get<object>(session, "TempleSuspension");
            Assert.That((bool)Call(initial, "Save", checkpoint), Is.True);
            var storeType = RequireType("TemplePrologueCheckpointStore");
            var staleSave = System.Activator.CreateInstance(storeType, directory, true);
            var staleConsume = System.Activator.CreateInstance(storeType, directory, true);
            var changed = Call(checkpoint, "Copy"); Set(changed, "phase", 3); Set(changed, "firstClueRead", true);
            string path = Path.Combine(directory, "temple-suspend-v1.json");
            string external = (string)Call(changed, "ToJson"); File.WriteAllText(path, external);
            Assert.That((bool)Call(staleSave, "Save", checkpoint), Is.False);
            Assert.That(File.ReadAllText(path), Is.EqualTo(external));
            Assert.That((bool)Call(staleConsume, "Consume", Get<string>(checkpoint, "token")), Is.False);
            Assert.That(Get<bool>(staleConsume, "Conflict"), Is.True);
            Assert.That(File.ReadAllText(path), Is.EqualTo(external));
            Assert.That(File.Exists(path + ".used"), Is.False);
            var fresh = System.Activator.CreateInstance(storeType, directory, true);
            Assert.That(Get<int>(Get<object>(fresh, "Snapshot"), "phase"), Is.EqualTo(3));
            Assert.That((bool)Call(fresh, "Consume", Get<string>(checkpoint, "token")), Is.True);
            Assert.That(File.ReadAllText(path + ".used"), Is.EqualTo(external));
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
            Call(world, "ReadStoryClue");
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
            PlacePlayer(origin + new Vector3(.65f, .04f, 5.8f));
            var pitch = player.GetType().GetField("pitch", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(pitch, Is.Not.Null);
            yield return TempleReadClueWithTemporaryMouse(world);
            Assert.That(Get<Component>(player, "Focus")?.name, Is.EqualTo("향로"));
            yield return RecoveryPulse(Key.E);
            Assert.That(Get<object>(director, "Phase").ToString(), Is.EqualTo("FirstPrayer"));
            var presentation = world.GetComponent(RequireType("TempleProloguePresentation"));
            Assert.That(Get<bool>(presentation, "IncensePending"), Is.True);
            Assert.That(Get<float>(director, "PrayerElapsed"), Is.Zero, "Prayer started before the hand placed the incense");
            yield return Wait(() => world.transform.Find("Placed incense smoke")?.GetComponent(RequireType("IncenseSmokeTrail")) != null, 3, "Incense did not appear after the authored release");
            Assert.That(Get<bool>(presentation, "IncensePlaced"), Is.True);
            Assert.That(Get<object>(Get<Component>(player, "Hands"), "PresentedItem").ToString(), Is.EqualTo("Incense"));
            var firstSmoke = world.transform.Find("Placed incense smoke")?.GetComponent(RequireType("IncenseSmokeTrail"));
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
            PlacePlayer(origin + new Vector3(.65f, .04f, 5.8f));
            yield return TempleReadClueWithTemporaryMouse(world);
            Assert.That(Get<Component>(player, "Focus")?.name, Is.EqualTo("향로"));
            yield return RecoveryPulse(Key.E);
            yield return Wait(() => (bool)(Transform)held.GetValue(hand), 3, "Second incense was not held before placement");
            yield return Wait(() => world.transform.Find("Placed incense smoke")?.GetComponent(RequireType("IncenseSmokeTrail")) != null, 3, "Second incense did not appear");
            yield return null;
            Assert.That((bool)(Transform)held.GetValue(hand), Is.False, "Second placed incense remained in the hand");
            var secondSmoke = world.transform.Find("Placed incense smoke")?.GetComponent(RequireType("IncenseSmokeTrail"));
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
