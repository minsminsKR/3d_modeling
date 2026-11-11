using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using static HappyToy.V2.CloudTests.RuntimeAccess;

namespace HappyToy.V2.CloudTests
{
    public sealed partial class CloudPlayModeTests
    {
        [UnityTest, Timeout(120000)]
        public IEnumerator UnloadingVillageAfterTempleRetirementPreservesPriorEnvironment()
        {
            var originalSky = RenderSettings.skybox;
            var originalAmbient = RenderSettings.ambientLight;
            var originalFog = RenderSettings.fogColor;
            var originalDensity = RenderSettings.fogDensity;
            var originalFogEnabled = RenderSettings.fog;
            Call(session, "CreatePrologue"); IsolateThreats();
            var temple = Get<Component>(session, "Prologue");
            Call(temple, "BeginApproach", new Action(() => { }));
            Begin();
            var flow = temple.GetComponent(RequireType("TempleApproachFlow"));
            yield return Wait(() => Get<bool>(flow, "VillageReady"), 30, "Village additive scene did not load");
            yield return Wait(() => RenderSettings.skybox && RenderSettings.skybox.shader.name == "HappyToy/AlleyDuskSky",
                10, "Village dusk environment did not activate");
            var village = SceneManager.GetSceneByPath("Assets/Approach/Village.unity");
            Assert.That(village.IsValid() && village.isLoaded, Is.True);
            Call(session, "RetirePrologue");
            yield return Wait(() => !village.IsValid() || !village.isLoaded, 30, "Village scene did not unload");
            yield return null;
            Assert.That(RenderSettings.skybox, Is.SameAs(originalSky));
            Assert.That(RenderSettings.ambientLight, Is.EqualTo(originalAmbient));
            Assert.That(RenderSettings.fogColor, Is.EqualTo(originalFog));
            Assert.That(RenderSettings.fogDensity, Is.EqualTo(originalDensity));
            Assert.That(RenderSettings.fog, Is.EqualTo(originalFogEnabled));
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator DistantVillageUnloadsAndReloadsOnReturnWithoutResettingInventory()
        {
            Call(session, "CreatePrologue"); IsolateThreats();
            var temple = Get<Component>(session, "Prologue");
            Call(temple, "BeginApproach", new Action(() => { }));
            Begin();
            var flow = temple.GetComponent(RequireType("TempleApproachFlow"));
            yield return Wait(() => Get<bool>(flow, "VillageReady"), 30, "Initial village load failed");
            var encounter = session.GetComponentsInChildren<Component>(true).First(c => c && c.GetType().Name == "AlleySnapperEncounter");
            var pickup = encounter.transform.Find("Alley snapper supply").GetComponent(RequireType("Interactable"));
            Call(pickup, "Use", player);
            var inventory = Get<Component>(player, "Firecrackers");
            Assert.That(Get<int>(inventory, "Count"), Is.EqualTo(6));
            Call(flow, "PreloadMountain");
            yield return Wait(() => Get<bool>(flow, "MountainReady"), 60, "Mountain preload failed");
            // Simulate being far enough past the transition, then reverse as
            // soon as unload starts, including when unload is still in flight.
            PlacePlayer(new Vector3(500, .1f, 469));
            yield return Wait(() => !Get<bool>(flow, "VillageReady"), 30, "Distant village did not begin unloading");
            PlacePlayer(new Vector3(500, 1.4f, 444));
            yield return Wait(() => Get<bool>(flow, "VillageReady"), 60, "Returning village did not reload");
            var village = SceneManager.GetSceneByPath("Assets/Approach/Village.unity");
            Assert.That(village.IsValid() && village.isLoaded, Is.True, "Reload fell back instead of loading the scene");
            Assert.That(Get<int>(inventory, "Count"), Is.EqualTo(6));
            Assert.That(Get<bool>(encounter, "Acquired"), Is.True);
            Assert.That(player.transform.position.z, Is.LessThan(445));
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator ApproachClearsHorizonAndTreesWithoutChangingLegacyTemple()
        {
            Call(session, "CreatePrologue"); IsolateThreats();
            var temple = Get<Component>(session, "Prologue");
            var horizon = temple.transform.Find("Forest horizon terrain");
            var depth = temple.transform.Find("Natural distant forest depth");
            Assert.That(horizon, Is.Not.Null); Assert.That(depth, Is.Not.Null);
            var horizonMesh = horizon.GetComponent<MeshFilter>().sharedMesh;
            var originalVertices = horizonMesh.vertices;
            var haze = horizon.Find("Far forest terrain haze");
            var hazeMesh = haze ? haze.GetComponent<MeshFilter>().sharedMesh : null;
            var originalHazeVertices = hazeMesh ? hazeMesh.vertices : null;
            var depthTrees = depth.Cast<Transform>().Where(node => node.name.StartsWith("Forest depth pine")).ToArray();
            var horizonTrees = horizon.Cast<Transform>().Where(node => node.name.StartsWith("Distant forest pine")).ToArray();
            Assert.That(depthTrees.Length, Is.EqualTo(64));
            Assert.That(horizonTrees.Length, Is.EqualTo(32));
            Assert.That(depthTrees.All(node => node.gameObject.activeSelf), Is.True);
            Assert.That(horizonTrees.All(node => node.gameObject.activeSelf), Is.True);

            var temporary = new GameObject("Approach horizon collision probes");
            GameObject horizonProbe = null, hazeProbe = null;
            try
            {
                var mountain = (Component)Call(RequireType("MountainApproachWorld"), "Create", temporary.transform);
                Call(RequireType("TempleApproachConnector"), "Create", mountain.transform);
                var uphill = mountain.transform.Find("Walkable mountain soil").GetComponent<MeshCollider>();
                var banks = new[] {
                    mountain.transform.Find("Mountain raised soil bank -1").GetComponent<MeshCollider>(),
                    mountain.transform.Find("Mountain raised soil bank 1").GetComponent<MeshCollider>() };
                Assert.That(banks.All(bank => bank), Is.True, "Visible mountain banks must support the player");
                var connector = mountain.transform.Find("Mountain to temple connector");
                var downhill = connector.GetComponent<MeshCollider>();
                var connectorBanks = new[] {
                    connector.Find("Connector raised bank transitions/Connector raised bank -1").GetComponent<MeshCollider>(),
                    connector.Find("Connector raised bank transitions/Connector raised bank 1").GetComponent<MeshCollider>() };
                Assert.That(connectorBanks.All(bank => bank && bank.sharedMesh == bank.GetComponent<MeshFilter>().sharedMesh), Is.True,
                    "Connector banks must share their visible and physical mesh");
                Call(temple, "BeginApproach", new Action(() => { }));
                Begin(); yield return null;
                Func<Transform, bool> inCorridor = node => Mathf.Abs(node.position.x - 500f) <= 13f &&
                    node.position.z >= 414f && node.position.z <= 474f;
                Assert.That(depthTrees.Count(inCorridor), Is.GreaterThan(0));
                Assert.That(horizonTrees.Count(inCorridor), Is.GreaterThan(0));
                Assert.That(depthTrees.Where(inCorridor).Any(node => node.gameObject.activeSelf), Is.False,
                    "An old background tree still blocks the walk-in visually");
                Assert.That(horizonTrees.Where(inCorridor).Any(node => node.gameObject.activeSelf), Is.False,
                    "An old horizon tree still blocks the walk-in visually");

                // Cook only after the opt-in vertex change; compare actual
                // rendered terrain and haze to the live ground collision.
                horizonProbe = new GameObject("Horizon visual probe");
                horizonProbe.transform.SetParent(horizon, false);
                var horizonCollider = horizonProbe.AddComponent<MeshCollider>(); horizonCollider.sharedMesh = horizonMesh;
                MeshCollider hazeCollider = null;
                if (hazeMesh)
                {
                    hazeProbe = new GameObject("Haze visual probe"); hazeProbe.transform.SetParent(horizon, false);
                    hazeCollider = hazeProbe.AddComponent<MeshCollider>(); hazeCollider.sharedMesh = hazeMesh;
                }
                Physics.SyncTransforms();
                // The village is enclosed by the southern horizon rings. A
                // narrow corridor alone leaves tall triangles over its roofs.
                foreach (float x in new[] { 440f, 460f, 500f, 540f, 560f })
                foreach (float z in new[] { 390f, 400f, 410f })
                {
                    var ray = new Ray(new Vector3(x, 30, z), Vector3.down);
                    Assert.That(horizonCollider.Raycast(ray, out var villageTerrain, 60), Is.True,
                        "Village horizon missing at " + x + "," + z);
                    Assert.That(villageTerrain.point.y, Is.LessThanOrEqualTo(.05f),
                        "Old horizon rises into the village at " + x + "," + z);
                    if (hazeCollider && hazeCollider.Raycast(ray, out var villageHaze, 60))
                        Assert.That(villageHaze.point.y, Is.LessThanOrEqualTo(.05f),
                            "Old horizon haze rises into the village at " + x + "," + z);
                }
                foreach (float x in new[] { 488.5f, 497f, 500f, 503f, 511.5f })
                foreach (float z in new[] { 414.1f, 420f, 424f, 430f, 440f, 447.9f, 448.1f, 450f, 454f, 458f, 462f, 466f })
                {
                    var ray = new Ray(new Vector3(x, 30, z), Vector3.down);
                    var walkable = z < 448 ? uphill : downhill;
                    Assert.That(walkable.Raycast(ray, out var ground, 60), Is.True, "Walkable ground missing at " + x + "," + z);
                    // The base plane continues below the two raised bank
                    // colliders. Compare to the highest real support hit,
                    // which is the surface the player actually stands on.
                    float supportHeight = ground.point.y;
                    foreach (var bank in z < 448 ? banks : connectorBanks)
                        if (bank.Raycast(ray, out var bankHit, 60)) supportHeight = Mathf.Max(supportHeight, bankHit.point.y);
                    Assert.That(horizonCollider.Raycast(ray, out var visual, 60), Is.True, "Horizon missing at " + x + "," + z);
                    Assert.That(visual.point.y, Is.LessThanOrEqualTo(supportHeight + .01f),
                        "Horizon hides the walking surface at " + x + "," + z);
                    if (hazeCollider && hazeCollider.Raycast(ray, out var mist, 60))
                        Assert.That(mist.point.y, Is.LessThanOrEqualTo(supportHeight + .01f),
                            "Haze hides the walking surface at " + x + "," + z);
                }

                var presentation = temple.GetComponent(RequireType("TempleProloguePresentation"));
                horizonCollider.sharedMesh = null;
                if (hazeCollider) hazeCollider.sharedMesh = null;
                Call(presentation, "SetApproachEntranceOpen", false);
                CollectionAssert.AreEqual(originalVertices, horizonMesh.vertices, "Legacy horizon vertices were not restored");
                if (hazeMesh) CollectionAssert.AreEqual(originalHazeVertices, hazeMesh.vertices, "Legacy haze vertices were not restored");
                Assert.That(depthTrees.All(node => node.gameObject.activeSelf), Is.True, "Legacy 64-tree ring was not restored");
                Assert.That(horizonTrees.All(node => node.gameObject.activeSelf), Is.True, "Legacy horizon trees were not restored");
            }
            finally
            {
                if (horizonProbe) UnityEngine.Object.Destroy(horizonProbe);
                if (hazeProbe) UnityEngine.Object.Destroy(hazeProbe);
                UnityEngine.Object.Destroy(temporary);
            }
        }
    }
}
