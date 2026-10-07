using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using static HappyToy.V2.CloudTests.RuntimeAccess;
using Object = UnityEngine.Object;

namespace HappyToy.V2.CloudTests
{
    internal static class CloudRoomPresentationTests
    {
        internal static Transform[] SceneTransforms()
        {
            return SceneManager.GetActiveScene().GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Transform>(true)).ToArray();
        }

        internal static Transform Named(string name)
        {
            var matches = SceneTransforms().Where(item => item.name == name).ToArray();
            Assert.That(matches.Length, Is.EqualTo(1), "Missing or duplicate presentation object: " + name);
            return matches[0];
        }

        internal static Vector3[] CornersIn(Transform space, Renderer renderer)
        {
            var bounds = renderer.localBounds;
            var corners = new List<Vector3>();
            foreach (int x in new[] { -1, 1 })
                foreach (int y in new[] { -1, 1 })
                    foreach (int z in new[] { -1, 1 })
                        corners.Add(space.InverseTransformPoint(renderer.transform.TransformPoint(bounds.center +
                            Vector3.Scale(bounds.extents, new Vector3(x, y, z)))));
            return corners.ToArray();
        }

        internal static void Fits(Transform support, Renderer lettering, float width, float height)
        {
            var points = CornersIn(support, lettering);
            Assert.That(points.All(point => !float.IsNaN(point.x) && !float.IsNaN(point.y)), Is.True, lettering.name);
            Assert.That(points.Max(point => point.x) - points.Min(point => point.x), Is.GreaterThan(.01f), "Text mesh has no width");
            Assert.That(points.Max(point => point.y) - points.Min(point => point.y), Is.GreaterThan(.01f), "Text mesh has no height");
            Assert.That(points.All(point => Mathf.Abs(point.x) <= width * .5f + .005f &&
                Mathf.Abs(point.y) <= height * .5f + .005f), Is.True,
                "Bundled-font lettering exceeds its physical support: " + lettering.transform.parent.name);
        }

        internal static void Mounted(Transform refresh, string name, Vector3 position, float yaw)
        {
            var label = Named(name);
            Assert.That(Vector3.Distance(label.position, position), Is.LessThan(.002f), name);
            Assert.That(Quaternion.Angle(label.rotation, Quaternion.Euler(0, yaw, 0)), Is.LessThan(.01f), name);
            Assert.That(label.gameObject.activeInHierarchy && label.GetComponent<MeshRenderer>().enabled, Is.True, name);
            var glyphs = label.GetComponent<Renderer>();
            Assert.That(glyphs.localBounds.size.x, Is.GreaterThan(.001f), "Sign text mesh has no width: " + name);
            Assert.That(glyphs.localBounds.size.y, Is.GreaterThan(.001f), "Sign text mesh has no height: " + name);
            // New plaques are deliberately non-colliding. Check actual renderer bounds,
            // not a physics hit or a self-reported count, behind each lettering plane.
            var candidates = refresh.GetComponentsInChildren<MeshRenderer>()
                .Where(renderer => !renderer.GetComponent<TextMesh>() && renderer.enabled &&
                    Vector3.Dot(renderer.bounds.center - label.position, label.forward) > 0 &&
                    Vector3.Dot(renderer.bounds.center - label.position, label.forward) < .10f)
                .Where(renderer =>
                {
                    var textPoints = CornersIn(renderer.transform, glyphs);
                    var bounds = renderer.localBounds;
                    return textPoints.All(point => point.x >= bounds.min.x - .005f && point.x <= bounds.max.x + .005f &&
                        point.y >= bounds.min.y - .005f && point.y <= bounds.max.y + .005f);
                }).ToArray();
            Assert.That(candidates, Is.Not.Empty, "No opaque backing contains the mounted text: " + name);
            Assert.That(candidates.Any(renderer => renderer.sharedMaterial && renderer.sharedMaterial.renderQueue < 3000),
                Is.True, "Sign backing must be opaque: " + name);
        }

        internal static Transform AssertLayout(Component presentation)
        {
            Assert.That(Get<bool>(presentation, "Applied"), Is.True);
            Assert.That(Get<int>(presentation, "ChairCount"), Is.EqualTo(6));
            Assert.That(Get<int>(presentation, "AddedSeatCount"), Is.EqualTo(2));
            Assert.That(Get<int>(presentation, "AddedColliderCount"), Is.EqualTo(6));
            Assert.That(Get<int>(presentation, "CarvedObstacleCount"), Is.EqualTo(4));
            Assert.That(Get<int>(presentation, "WhiteKeyCount"), Is.EqualTo(28));
            Assert.That(Get<int>(presentation, "BlackKeyCount"), Is.EqualTo(20));
            Assert.That(Get<int>(presentation, "MountedSignCount"), Is.EqualTo(6));
            Assert.That(Get<string>(presentation, "Failure"), Is.Empty);
            Assert.That(Named("CLASSROOM sign interior").GetComponent<MeshRenderer>().enabled, Is.False,
                "The detached 1-2 interior text face is still visible");
            foreach (string name in new[] { "CLASSROOM sign", "CLASSROOM sign opaque backing", "WASHROOM sign interior", "INFIRMARY sign interior" })
                Assert.That(Named(name).GetComponent<MeshRenderer>().enabled, Is.True,
                    "Classroom cleanup removed unrelated wayfinding: " + name);
            Assert.That(Get<int>(presentation, "InitialColliderCount"), Is.GreaterThan(0));
            Assert.That(Get<int>(presentation, "FinalColliderCount"), Is.EqualTo(Get<int>(presentation, "InitialColliderCount") + 6));
            var refresh = Named("Annex presentation refresh");
            var addedColliders = refresh.GetComponentsInChildren<Collider>(true);
            Assert.That(addedColliders.Length, Is.EqualTo(6), "Only the two bounded new seats/stands may add collision");
            foreach (var collider in addedColliders)
            {
                Assert.That(collider.enabled && collider.gameObject.activeInHierarchy && !collider.isTrigger, Is.True);
                Assert.That(collider.GetComponentsInParent<Transform>().Any(item => item.name == "Additional choir chair" ||
                    item.name == "Additional choir music stand"), Is.True,
                    "Decorative dressing/sign backing added an unexpected collider: " + collider.name);
            }
            foreach (string name in new[] { "Additional choir chair", "Additional choir music stand" })
            {
                var additions = refresh.GetComponentsInChildren<Transform>().Where(item => item.name == name).OrderBy(item => item.position.x).ToArray();
                Assert.That(additions.Length, Is.EqualTo(2), name);
                bool isChair = name == "Additional choir chair";
                for (int i = 0; i < 2; i++)
                    Assert.That(Vector3.Distance(additions[i].position, new Vector3((i == 0 ? 21.1f : 25.7f) + (isChair ? 0 : .7f),
                        0, isChair ? 8.05f : 8.70f)), Is.LessThan(.003f), name);
            }
            var obstacles = refresh.GetComponentsInChildren<NavMeshObstacle>();
            Assert.That(obstacles.Length, Is.EqualTo(4));
            Assert.That(obstacles.All(item => item.enabled && item.carving), Is.True, "Visible added seating needs actual navigation carving");
            Assert.That(SceneTransforms().Count(item => item.name == "Abandoned choir chair"), Is.EqualTo(4));
            Assert.That(refresh.GetComponentsInChildren<Transform>().Count(item => item.name == "Ivory piano key"), Is.EqualTo(28));
            Assert.That(refresh.GetComponentsInChildren<Transform>().Count(item => item.name == "Raised black piano key"), Is.EqualTo(20));
            Assert.That(refresh.GetComponentsInChildren<Transform>().Count(item => item.name == "Sign oak backing"), Is.EqualTo(6));
            Mounted(refresh, "2층 · 액자실", new Vector3(33, 2.5f, 11.075f), 0);
            Mounted(refresh, "지하 · 침수된 인형방", new Vector3(17, 2.5f, -11.075f), 180);
            Mounted(refresh, "NURSERY", new Vector3(42.6f, 2.4f, 4.675f), 0);
            Mounted(refresh, "MUSIC", new Vector3(23.400002f, 2.4f, 14.25f), 0);
            Mounted(refresh, "ARCHIVE", new Vector3(23.400002f, 2.4f, -4.95f), 0);
            Mounted(refresh, "ANNEX / EAST WING", new Vector3(10.6f, 2.6f, 1.45f), 0);
            var board = Named("Choir rehearsal board");
            Assert.That(Vector3.Distance(board.position, new Vector3(28.06f, 1.8f, 12.8f)), Is.LessThan(.002f));
            Assert.That(Quaternion.Angle(board.rotation, Quaternion.Euler(0, 90, 0)), Is.LessThan(.01f));
            var text = board.GetComponentInChildren<TextMesh>();
            Assert.That(text, Is.Not.Null);
            Assert.That(text.font, Is.SameAs(Resources.Load<Font>("Fonts/Korean")));
            Fits(board, text.GetComponent<Renderer>(), 1.97f, .86f);
            var frame = board.Find("Oak frame").GetComponent<Renderer>().bounds;
            Assert.That(frame.min.z, Is.GreaterThanOrEqualTo(11.2f));
            Assert.That(frame.max.z, Is.LessThanOrEqualTo(14.4f));
            Assert.That(frame.max.x, Is.InRange(28.10f, 28.12f));
            return refresh;
        }
    }

    public sealed partial class CloudPlayModeTests
    {
        [UnityTest, Timeout(60000)]
        public IEnumerator AnnexPresentationStaysBoundedAcrossEnableAndReload()
        {
            yield return Wait(() => Components("AnnexRoomPresentation").Length == 1 &&
                Get<bool>(One("AnnexRoomPresentation"), "Applied"), 5, "Annex room presentation did not apply");
            yield return null; yield return null;
            var presentation = One("AnnexRoomPresentation");
            var refresh = CloudRoomPresentationTests.AssertLayout(presentation);
            int descendants = refresh.GetComponentsInChildren<Transform>(true).Length;
            var colliders = CloudRoomPresentationTests.SceneTransforms().SelectMany(item => item.GetComponents<Collider>())
                .Select(item => item.GetEntityId().ToString()).OrderBy(id => id).ToArray();
            var boardScale = CloudRoomPresentationTests.Named("Choir rehearsal board").GetComponentInChildren<TextMesh>().transform.localScale;
            Call(presentation, "Apply"); Call(presentation, "Apply");
            var behaviour = (Behaviour)presentation;
            behaviour.enabled = false; yield return null;
            behaviour.enabled = true; yield return null; yield return null;
            Assert.That(CloudRoomPresentationTests.AssertLayout(presentation), Is.SameAs(refresh));
            Assert.That(refresh.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(descendants), "Re-enable duplicated presentation geometry");
            Assert.That(CloudRoomPresentationTests.SceneTransforms().SelectMany(item => item.GetComponents<Collider>())
                .Select(item => item.GetEntityId().ToString()).OrderBy(id => id).ToArray(), Is.EqualTo(colliders));
            Assert.That(CloudRoomPresentationTests.Named("Choir rehearsal board").GetComponentInChildren<TextMesh>().transform.localScale,
                Is.EqualTo(boardScale), "Re-enable compounded the font fitting scale");
            var previous = session;
            Call(shell, "Restart", false);
            yield return Wait(() => Components("GameSession").Length == 1 && One("GameSession") != previous,
                25, "Presentation scene reload did not replace the session");
            yield return null; yield return null;
            session = One("GameSession"); shell = Get<Component>(session, "Shell"); player = Get<Component>(session, "player");
            yield return Wait(() => Components("AnnexRoomPresentation").Length == 1 &&
                Get<bool>(One("AnnexRoomPresentation"), "Applied"), 5, "Reload did not recreate room presentation");
            yield return null;
            var reloaded = CloudRoomPresentationTests.AssertLayout(One("AnnexRoomPresentation"));
            Assert.That(reloaded.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(descendants));
            Assert.That(Get<object>(shell, "Screen").ToString(), Is.EqualTo("Title"));
        }

        [UnityTest, Timeout(30000)]
        public IEnumerator AnnexPresentationPreservesOriginalInteractionAndCrossingAccess()
        {
            // Controlled geometry/access check. Threat isolation does not stand in for
            // the separate live-enemy input-driven seven-record survival route.
            IsolateThreats(); Begin();
            yield return Wait(() => NavMesh.CalculateTriangulation().vertices.Length > 0, 5, "Missing preserved navigation");
            var obstacles = CloudRoomPresentationTests.Named("Annex presentation refresh").GetComponentsInChildren<NavMeshObstacle>();
            Assert.That(obstacles.Length, Is.EqualTo(4));
            // An enabled carver/count alone does not prove it has changed the live mesh.
            // Wait for real holes at each added chair/stand footprint before path checks.
            yield return Wait(() => obstacles.All(item =>
            {
                var center = item.transform.TransformPoint(item.center); center.y = 0;
                return !NavMesh.SamplePosition(center, out var blocked, .12f, NavMesh.AllAreas);
            }), 6, "Added seating did not carve its real navigation footprints");
            var leaves = new Dictionary<Transform, Vector3>();
            foreach (var door in Components("Interactable").Where(item => Get<object>(item, "kind").ToString() == "Door"))
            {
                var leaf = Get<Transform>(door, "movingLeaf");
                var offset = Get<Vector3>(door, "openOffset");
                leaves.Add(leaf, leaf.localPosition + offset);
                var secondary = Get<Transform>(door, "secondaryLeaf");
                if (secondary) leaves.Add(secondary, secondary.localPosition - offset);
                Call(door, "Use", player);
            }
            yield return Wait(() => leaves.All(pair => Vector3.Distance(pair.Key.localPosition, pair.Value) < .02f),
                10, "Original doors did not open for the access check");
            Physics.SyncTransforms();
            var cabinet = Components("Interactable").Single(item => item.name == "음악실 은신함");
            foreach (var item in new[] { Record("music-roster"), Record("record"), cabinet })
                Assert.That(ReachableInteraction(item), Is.True, "Presentation obstructed original interaction: " + item.name);
            foreach (var point in new[] { new Vector3(23.4f, 0, 5.5f), new Vector3(23.4f, 0, 9.6f),
                new Vector3(23.4f, 0, 8.05f), new Vector3(19.8f, 0, 8.05f), new Vector3(27.4f, 0, 8.05f),
                new Vector3(27.5f, 0, 9.6f), new Vector3(29.8f, 0, 10.3f), new Vector3(29.8f, 5, 22.3f) })
            {
                Assert.That(NavMesh.SamplePosition(point, out var hit, .45f, NavMesh.AllAreas), Is.True, "Missing crossing anchor: " + point);
                Assert.That(Physics.CheckCapsule(hit.position + Vector3.up * .4f, hit.position + Vector3.up * 1.4f,
                    .3f, ~0, QueryTriggerInteraction.Ignore), Is.False, "Blocked original crossing: " + point);
                var path = new NavMeshPath();
                Assert.That(NavMesh.CalculatePath(player.transform.position, hit.position, NavMesh.AllAreas, path) &&
                    path.status == NavMeshPathStatus.PathComplete, Is.True, "Disconnected original crossing: " + point);
            }
        }

        [UnityTest, Timeout(30000)]
        public IEnumerator AnnexPresentationProducesActualPlayerEyeRoomFrames()
        {
            Assert.That(SystemInfo.graphicsDeviceType, Is.Not.EqualTo(GraphicsDeviceType.Null), "Real URP graphics are required");
            Call(shell, "RestoreDefaultSettings");
            Begin(); yield return null; yield return null;
            CloudRoomPresentationTests.AssertLayout(One("AnnexRoomPresentation"));
            var camera = Get<Camera>(player, "eyes");
            var originalPosition = camera.transform.localPosition;
            var originalRotation = camera.transform.localRotation;
            var originalPlayerPosition = player.transform.position;
            var originalPlayerRotation = player.transform.rotation;
            var motor = (Behaviour)player;
            var feedback = (Behaviour)Get<Component>(player, "Feedback");
            var controller = player.GetComponent<CharacterController>();
            bool motorEnabled = motor.enabled, feedbackEnabled = feedback.enabled, controllerEnabled = controller.enabled;
            Color originalFog = RenderSettings.fogColor;
            float originalFogDensity = RenderSettings.fogDensity;
            var eventStates = new Dictionary<Behaviour, bool>();
            foreach (string name in new[] { "StoryDirector", "AnnexEncounter", "UncatAnnexEvent", "V1HwacatEvent", "LanternMaskEncounter", "WeepingAngelEncounter" })
                foreach (var item in Components(name)) eventStates[(Behaviour)item] = ((Behaviour)item).enabled;
            var actorStates = Components("StalkerBrain").ToDictionary(item => item.gameObject, item => item.gameObject.activeSelf);
            var atmosphere = One("FloorAtmosphere");
            Assert.That(((Behaviour)atmosphere).enabled, Is.True, "Real floor atmosphere must remain enabled");
            var target = new RenderTexture(1280, 720, 24);
            target.Create();
            Call(shell, "Pause");
            try
            {
                // Explicit positioned-player fixture, not a walk-through. The real
                // player and listener occupy each floor while its real atmosphere
                // settles in Playing. Only motor/foley and threats are isolated;
                // no substitute lights, fog values or rendering materials are used.
                IsolateThreats();
                motor.enabled = false; feedback.enabled = false; controller.enabled = false;
                var names = new[] { "annex-music-entry.png", "annex-piano-board.png", "annex-upper-stair-approach.png",
                    "annex-upper-reverse.png", "annex-music-rows-reverse.png" };
                var eyes = new[] { new Vector3(23.4f, 1.6f, 5.5f), new Vector3(25, 1.6f, 11.1f),
                    new Vector3(29.8f, 1.6f, 8.2f), new Vector3(29.8f, 6.6f, 24), new Vector3(23.4f, 1.6f, 11.6f) };
                var aims = new[] { new Vector3(23.4f, 1.3f, 13.3f), new Vector3(25.1f, 1.5f, 13.4f),
                    new Vector3(31.7f, 2.1f, 11.15f), new Vector3(29.8f, 6.5f, 21.2f), new Vector3(23.4f, 1.1f, 5.4f) };
                for (int i = 0; i < names.Length; i++)
                {
                    PlacePlayer(eyes[i] - Vector3.up * 1.6f, false);
                    camera.transform.SetPositionAndRotation(eyes[i], Quaternion.LookRotation(aims[i] - eyes[i], Vector3.up));
                    Call(shell, "Resume");
                    Assert.That(Get<bool>(session, "InputAllowed"), Is.True, "Floor atmosphere needs actual Playing state");
                    float settleUntil = Time.time + 1.5f;
                    yield return Wait(() => Time.time >= settleUntil, 5, "Real floor atmosphere did not receive its settling time");
                    Call(shell, "Pause");
                    yield return null; yield return null;
                    Assert.That(Vector3.Distance(camera.transform.position, eyes[i]), Is.LessThan(.001f), "Capture viewpoint drifted");
                    Assert.That(Vector3.Distance(player.transform.position, eyes[i] - Vector3.up * 1.6f), Is.LessThan(.001f),
                        "Positioned fixture player drifted off the captured floor");
                    if (eyes[i].y > 5)
                    {
                        Assert.That(Mathf.Abs(RenderSettings.fogColor.r - .085f) + Mathf.Abs(RenderSettings.fogColor.g - .008f) +
                            Mathf.Abs(RenderSettings.fogColor.b - .012f), Is.LessThan(.02f), "Upper floor's real red fog did not settle");
                        Assert.That(RenderSettings.fogDensity, Is.EqualTo(.032f).Within(.003f));
                    }
                    var previousTarget = RenderTexture.active;
                    try { RenderTexture.active = target; GL.Clear(true, true, Color.clear); }
                    finally { RenderTexture.active = previousTarget; }
                    RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
                    CloudExperienceTests.SaveFrame(names[i], target);
                    TestContext.Out.WriteLine("HAPPYTOY_ROOM_FRAME name=" + names[i] + "; eye=" + eyes[i] +
                        "; aim=" + aims[i] + "; player=" + player.transform.position + "; authoredPlayerCamera=true; fixedPausedView=true; " +
                        "positionedPlayerFixture=true; threatsIsolated=true; realFloorAtmosphereSettledSeconds=1.5; fog=" +
                        RenderSettings.fogColor + "; fogDensity=" + RenderSettings.fogDensity + "; survivalEvidence=false");
                }
            }
            finally
            {
                Call(shell, "Pause");
                controller.enabled = false;
                player.transform.SetPositionAndRotation(originalPlayerPosition, originalPlayerRotation);
                camera.transform.localPosition = originalPosition;
                camera.transform.localRotation = originalRotation;
                RenderSettings.fogColor = originalFog;
                RenderSettings.fogDensity = originalFogDensity;
                foreach (var entry in eventStates) if (entry.Key) entry.Key.enabled = entry.Value;
                foreach (var entry in actorStates) if (entry.Key) entry.Key.SetActive(entry.Value);
                controller.enabled = controllerEnabled;
                motor.enabled = motorEnabled; feedback.enabled = feedbackEnabled;
                Physics.SyncTransforms();
                target.Release(); Object.Destroy(target);
                Call(shell, "Resume");
            }
        }
    }
}
