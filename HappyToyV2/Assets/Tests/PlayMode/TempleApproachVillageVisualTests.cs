using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using static HappyToy.V2.CloudTests.RuntimeAccess;

namespace HappyToy.V2.CloudTests
{
    public sealed partial class CloudPlayModeTests
    {
        // Focused, opt-in actual gameplay-camera views of the village only.
        [UnityTest, Timeout(180000)]
        public IEnumerator TempleApproachVillageVisualReview()
        {
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("HAPPYTOY_APPROACH_REVIEW_DIR")))
                Assert.Ignore("Set HAPPYTOY_APPROACH_REVIEW_DIR for village camera review");
            Call(session, "ConfigureRecordDirectory", NewRecordFixture());
            yield return RecoveryUiReady();
            yield return RecoveryClick("begin");
            yield return RecoveryWaitForTempleEntry();
            Assert.That(Get<bool>(Get<Component>(session, "Prologue"), "ApproachActive"), Is.True);
            var nodes = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            var alley = GameObject.Find("Korean alley approach");
            Assert.That(alley, Is.Not.Null);
            var nearHouses = alley.transform.Cast<Transform>()
                .Where(node => node.name == "Old Korean brick house" || node.name == "Foreground old Korean house").ToArray();
            Assert.That(nearHouses.Length, Is.EqualTo(7));
            bool closerToCurvedWall = false;
            foreach (var house in nearHouses)
            {
                var renderers = house.GetComponentsInChildren<Renderer>(true);
                Assert.That(renderers.Length, Is.GreaterThan(0));
                var bounds = renderers[0].bounds;
                foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
                bool right = house.position.x > alley.transform.position.x;
                int matchingWalls = 0;
                foreach (Transform wall in alley.transform)
                {
                    if ((wall.localPosition.x > 0) != right ||
                        (wall.name != "Curved residential wall" && wall.name != "Weathered residential wall")) continue;
                    var collider = wall.GetComponent<BoxCollider>();
                    if (!collider || !collider.enabled) continue;
                    var wallBounds = collider.bounds;
                    if (wallBounds.max.z < bounds.min.z || wallBounds.min.z > bounds.max.z) continue;
                    matchingWalls++;
                    float gap = right ? bounds.min.x - wallBounds.max.x : wallBounds.min.x - bounds.max.x;
                    Assert.That(gap, Is.GreaterThanOrEqualTo(.09f),
                        "House intersects or nearly touches the local wall at " + house.name);
                }
                Assert.That(matchingWalls, Is.GreaterThan(0), "No local wall was checked for " + house.name);
                if (house.name == "Old Korean brick house")
                    closerToCurvedWall |= right ? bounds.min.x < alley.transform.position.x + 4.80f :
                        bounds.max.x > alley.transform.position.x - 4.80f;
            }
            Assert.That(closerToCurvedWall, Is.True, "No curved-street house moved closer to its wall");
            var cleanedHouse = nearHouses.SingleOrDefault(house => house.GetComponentsInChildren<Transform>(true)
                .Any(child => child.name.StartsWith("TwoStoryHouseClean", StringComparison.Ordinal)));
            Assert.That(cleanedHouse, Is.Not.Null, "Cleaned two-story house was not applied");
            var cleanMeshes = cleanedHouse.GetComponentsInChildren<MeshFilter>(true);
            Assert.That(cleanMeshes.Length, Is.GreaterThan(0));
            long cleanTriangles = 0;
            foreach (var filter in cleanMeshes)
                for (int submesh = 0; submesh < filter.sharedMesh.subMeshCount; submesh++)
                    cleanTriangles += (long)filter.sharedMesh.GetIndexCount(submesh) / 3;
            Assert.That(cleanTriangles, Is.InRange(77000, 79000), "Unexpected cleaned house geometry");
            var farHouses = nodes.Where(node => node.name == "Far residential roofline").ToArray();
            Assert.That(farHouses.Length, Is.EqualTo(2), "Far neighborhood assets failed to load");
            Assert.That(farHouses.Any(house => house.GetComponentsInChildren<Transform>(true)
                .Any(child => child.name.StartsWith("geometry-clean-far", StringComparison.Ordinal))), Is.True,
                "Cleaned far house was not applied");
            foreach (var house in farHouses)
            {
                var filters = house.GetComponentsInChildren<MeshFilter>(true);
                Assert.That(filters.Length, Is.GreaterThan(0));
                long triangles = 0;
                foreach (var filter in filters)
                    for (int submesh = 0; submesh < filter.sharedMesh.subMeshCount; submesh++)
                        triangles += (long)filter.sharedMesh.GetIndexCount(submesh) / 3;
                Assert.That(triangles, Is.LessThanOrEqualTo(32000), "Far house exceeded its geometry budget");
                Assert.That(house.GetComponentsInChildren<Collider>(true), Is.Empty,
                    "Visual background must not create invisible obstacles");
            }
            var visibleMaterials = nodes.SelectMany(node => node.GetComponents<Renderer>())
                .SelectMany(renderer => renderer.sharedMaterials).Where(material => material).Distinct().ToArray();
            foreach (var variant in new[] { "BrickHousePbr", "PlasterHousePbr" })
            {
                var material = visibleMaterials.SingleOrDefault(candidate => candidate.name == variant);
                Assert.That(material, Is.Not.Null, "Original PBR material was not applied: " + variant);
                Assert.That(material.IsKeywordEnabled("_METALLICSPECGLOSSMAP"), Is.True);
                Assert.That(material.GetTexture("_MetallicGlossMap"), Is.Not.Null);
            }
            var yardWall = nodes.SingleOrDefault(node => node.name == "Shop rear courtyard wall");
            Assert.That(yardWall, Is.Not.Null, "Rear courtyard wall failed to load");
            Assert.That(yardWall.GetComponentsInChildren<Renderer>(true).Length, Is.GreaterThan(0));
            Assert.That(yardWall.GetComponentsInChildren<Collider>(true), Is.Empty);
            var wires = nodes.SingleOrDefault(node => node.name == "Batched sagging village cables");
            Assert.That(wires, Is.Not.Null, "Village wiring was not created");
            Assert.That(wires.GetComponent<MeshRenderer>(), Is.Not.Null);
            Assert.That(wires.GetComponentsInChildren<Collider>(true), Is.Empty);
            var curveGate = nodes.SingleOrDefault(node => node.name == "House gate" &&
                node.position.z > 403.5f && node.position.z < 404.5f);
            Assert.That(curveGate, Is.Not.Null, "Curved wall gate is missing");
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(0, curveGate.localEulerAngles.y)), Is.GreaterThan(5f),
                "Curved wall gate is not aligned to the bend");
            var straightGate = nodes.SingleOrDefault(node => node.name == "House gate" &&
                node.position.z > 395.5f && node.position.z < 395.7f);
            Assert.That(straightGate, Is.Not.Null, "Straight-wall gate is missing");
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(0, straightGate.localEulerAngles.y)), Is.LessThan(.1f),
                "Straight-wall gate is rotated into the bend");
            foreach (float offset in new[] { -.78f, 0f, .78f })
            {
                Vector3 start = straightGate.position + new Vector3(-.5f, 0, offset);
                Assert.That(Physics.Raycast(start, Vector3.right, out var hit, 1f, 1 << 8, QueryTriggerInteraction.Ignore),
                    Is.True, "Straight gate has no wall support behind it");
                Assert.That(hit.collider.name, Is.EqualTo("Weathered residential wall"),
                    "Straight gate extends past its wall");
            }
            Assert.That(nodes.Count(node => node.name == "Gate door pull"), Is.EqualTo(4));
            var notice = nodes.SingleOrDefault(node => node.name == "Weathered village fire notice");
            Assert.That(notice, Is.Not.Null, "Village notice is missing");
            Assert.That(notice.GetComponent<Renderer>().sharedMaterial, Is.Not.Null);
            Assert.That(notice.GetComponentsInChildren<Collider>(true), Is.Empty);
            var wallPipes = nodes.Where(node => node.name == "Weathered wall service pipe").ToArray();
            Assert.That(wallPipes.Length, Is.EqualTo(2), "Authored wall fittings are missing");
            foreach (var pipe in wallPipes)
            {
                Assert.That(pipe.GetComponentsInChildren<Renderer>(true).Length, Is.GreaterThan(0));
                Assert.That(pipe.GetComponentsInChildren<Collider>(true), Is.Empty,
                    "Visual wall fittings must not add collision");
            }

            CaptureApproachReview("village-01-start");
            var eye = Get<Camera>(player, "eyes").transform.position;
            ApproachLookAt(eye + new Vector3(5, 1.3f, 4)); yield return null;
            CaptureApproachReview("village-02-front-right-roofs");
            ApproachLookAt(eye + new Vector3(-5, 1.3f, 4)); yield return null;
            CaptureApproachReview("village-03-front-left-roofs");

            ApproachFaceForward();
            yield return ApproachWalkUntil(Key.W, () => player.transform.position.z >= 393f, 15,
                "Cannot walk to the shop frontage");
            CaptureApproachReview("village-04-shop-approach");
            eye = Get<Camera>(player, "eyes").transform.position;
            ApproachLookAt(eye + new Vector3(5, 1.3f, 4)); yield return null;
            CaptureApproachReview("village-05-shop-right-roofs");

            ApproachFaceForward();
            yield return ApproachWalkUntil(Key.W, () => player.transform.position.z >= 399f, 15,
                "Cannot walk to the curved lane");
            CaptureApproachReview("village-06-curve-approach");
            eye = Get<Camera>(player, "eyes").transform.position;
            ApproachLookAt(eye + new Vector3(5, 1.3f, 4)); yield return null;
            CaptureApproachReview("village-07-curve-right-roofs");
            ApproachLookAt(eye + new Vector3(-5, 1.3f, 4)); yield return null;
            CaptureApproachReview("village-08-curve-left-roofs");
            ApproachLookAt(notice.position);
            yield return ApproachWalkUntil(Key.W, () =>
                Vector2.Distance(new Vector2(player.transform.position.x, player.transform.position.z),
                    new Vector2(notice.position.x, notice.position.z)) <= 1.1f, 10,
                "Cannot approach the wall notice");
            ApproachLookAt(notice.position); yield return null;
            CaptureApproachReview("village-09-wall-notice-close");
            ApproachFaceForward();
        }
    }
}
