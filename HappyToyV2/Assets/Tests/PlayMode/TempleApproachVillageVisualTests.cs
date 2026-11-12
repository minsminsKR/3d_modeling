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
            var slateHouse = nearHouses.SingleOrDefault(house => house.GetComponentsInChildren<Transform>(true)
                .Any(node => node.name.StartsWith("SlateHousePbr", StringComparison.Ordinal)));
            Assert.That(slateHouse, Is.Not.Null, "Reviewed slate house was not placed in the lane");
            Assert.That(slateHouse.GetComponentsInChildren<Collider>(true), Is.Empty,
                "The visual house must not create obstacles");
            bool closerToCurvedWall = false;
            var houseBounds = new System.Collections.Generic.List<Bounds>();
            foreach (var house in nearHouses)
            {
                var renderers = house.GetComponentsInChildren<Renderer>(true);
                Assert.That(renderers.Length, Is.GreaterThan(0));
                var bounds = renderers[0].bounds;
                foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
                foreach (var previous in houseBounds)
                    Assert.That(bounds.Intersects(previous), Is.False, "Neighboring house bounds overlap");
                houseBounds.Add(bounds);
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
            var roofHeadings = nearHouses.Where(house => house.name == "Old Korean brick house")
                .Select(house => house.GetChild(0).localEulerAngles.y).ToArray();
            Assert.That(roofHeadings.Any(yaw => Mathf.Abs(Mathf.DeltaAngle(35, yaw)) < 15f), Is.True,
                "No angled house on the left");
            Assert.That(roofHeadings.Any(yaw => Mathf.Abs(Mathf.DeltaAngle(145, yaw)) < 15f), Is.True,
                "No angled house on the right");
            var shop = nodes.SingleOrDefault(node => node.name == "Authored Korean neighborhood shop");
            Assert.That(shop, Is.Not.Null, "Authored shop is missing");
            var shopRenderers = shop.GetComponentsInChildren<Renderer>(true).Where(renderer => renderer.enabled).ToArray();
            Assert.That(shopRenderers.Length, Is.GreaterThan(0));
            var shopBounds = shopRenderers[0].bounds;
            foreach (var renderer in shopRenderers.Skip(1)) shopBounds.Encapsulate(renderer.bounds);
            foreach (var bounds in houseBounds)
                Assert.That(bounds.Intersects(shopBounds), Is.False,
                    "A residential house overlaps the shop: " + bounds.center + " vs " + shopBounds.center);
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
            var background = nodes.Where(node => node.name == "Distant neighborhood roof" ||
                node.name == "Residential roofs behind shop").ToArray();
            Assert.That(background.Length, Is.EqualTo(4), "Background houses are missing");
            foreach (var house in background)
            {
                var filters = house.GetComponentsInChildren<MeshFilter>(true);
                Assert.That(filters.Length, Is.GreaterThan(0));
                long triangles = 0;
                foreach (var filter in filters)
                    for (int submesh = 0; submesh < filter.sharedMesh.subMeshCount; submesh++)
                        triangles += (long)filter.sharedMesh.GetIndexCount(submesh) / 3;
                Assert.That(triangles, Is.LessThanOrEqualTo(32000), "Background house used the full mesh");
                Assert.That(house.GetComponentsInChildren<Collider>(true), Is.Empty);
                Assert.That(house.GetComponentsInChildren<Renderer>(true).All(renderer => renderer.sharedMaterial &&
                    renderer.sharedMaterial.IsKeywordEnabled("_METALLICSPECGLOSSMAP")), Is.True,
                    "Background house lost its PBR material");
            }
            var backgroundBounds = new System.Collections.Generic.List<Bounds>();
            var overlaps = new System.Collections.Generic.List<string>();
            foreach (var house in background.Concat(farHouses))
            {
                var renderers = house.GetComponentsInChildren<Renderer>(true);
                var bounds = renderers[0].bounds;
                foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
                foreach (var near in houseBounds)
                    if (bounds.Intersects(near)) overlaps.Add(house.name + " " + bounds.center + " overlaps near " + near.center);
                foreach (var previous in backgroundBounds)
                    if (bounds.Intersects(previous)) overlaps.Add(house.name + " " + bounds.center + " overlaps background " + previous.center);
                backgroundBounds.Add(bounds);
            }
            Assert.That(overlaps, Is.Empty, string.Join("; ", overlaps));
            var rearGround = nodes.SingleOrDefault(node => node.name == "Residential ground behind shop");
            Assert.That(rearGround, Is.Not.Null, "Residential background ground is missing");
            Assert.That(rearGround.GetComponentsInChildren<Collider>(true), Is.Empty);
            var groundBounds = rearGround.GetComponent<Renderer>().bounds;
            foreach (var house in nearHouses.Concat(background).Concat(farHouses)
                .Where(node => node.position.x < alley.transform.position.x))
            {
                var renderers = house.GetComponentsInChildren<Renderer>(true);
                var bounds = renderers[0].bounds;
                foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
                Assert.That(bounds.min.x, Is.GreaterThanOrEqualTo(groundBounds.min.x), "House extends past rear ground");
                Assert.That(bounds.max.x, Is.LessThanOrEqualTo(groundBounds.max.x), "House extends toward the road past rear ground");
                Assert.That(bounds.min.z, Is.GreaterThanOrEqualTo(groundBounds.min.z), "House extends past rear ground start");
                Assert.That(bounds.max.z, Is.LessThanOrEqualTo(groundBounds.max.z), "House extends past rear ground end");
            }
            var rightRear = background.Single(house => house.name == "Distant neighborhood roof" &&
                house.position.x > alley.transform.position.x);
            Assert.That(rightRear.GetComponentsInChildren<Renderer>(true).Any(renderer => renderer.sharedMaterial &&
                renderer.sharedMaterial.name == "BrickHousePbr"), Is.True,
                "Right background lost the varied brick-house material");
            var formedWalls = nodes.Where(node => node.name == "Continuous curved wall skin")
                .Select(node => node.GetComponent<Renderer>()).Where(renderer => renderer &&
                    renderer.sharedMaterial && renderer.sharedMaterial.GetTag("GraphicsSurface", false) == "alley-form-wall").ToArray();
            Assert.That(formedWalls.Length, Is.EqualTo(1), "Formed-concrete wall is missing or repeated");
            Assert.That(formedWalls[0].sharedMaterial.IsKeywordEnabled("_NORMALMAP"), Is.True);
            Assert.That(formedWalls[0].sharedMaterial.IsKeywordEnabled("_METALLICSPECGLOSSMAP"), Is.True);
            var curvedCoping = nodes.Where(node => node.name == "Curved wall coping").ToArray();
            Assert.That(curvedCoping.Length, Is.EqualTo(36), "Curved wall coping is missing");
            foreach (var coping in curvedCoping)
            {
                var renderer = coping.GetComponent<Renderer>();
                Assert.That(renderer.sharedMaterial.GetTag("GraphicsSurface", false), Is.EqualTo("alley-concrete-wall"));
                Assert.That(coping.GetComponentsInChildren<Collider>(true), Is.Empty);
            }
            var agedBrickWalls = nodes.Where(node => node.name == "Continuous curved wall skin")
                .Select(node => node.GetComponent<Renderer>()).Where(renderer => renderer && renderer.sharedMaterial &&
                    renderer.sharedMaterial.GetTag("GraphicsSurface", false) == "alley-aged-brick").ToArray();
            Assert.That(agedBrickWalls.Length, Is.EqualTo(1), "Aged brick wall is missing or repeated");
            Assert.That(agedBrickWalls[0].sharedMaterial.IsKeywordEnabled("_NORMALMAP"), Is.True);
            Assert.That(agedBrickWalls[0].sharedMaterial.IsKeywordEnabled("_METALLICSPECGLOSSMAP"), Is.True);
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
            var farCurveGate = nodes.SingleOrDefault(node => node.name == "House gate" &&
                node.position.z > 410.5f && node.position.z < 411.5f);
            Assert.That(farCurveGate, Is.Not.Null, "Far curved-wall gate is missing");
            var nearGateMaterial = curveGate.GetComponent<Renderer>().sharedMaterial;
            var farGateMaterial = farCurveGate.GetComponent<Renderer>().sharedMaterial;
            Assert.That(nearGateMaterial, Is.Not.Null);
            Assert.That(farGateMaterial, Is.Not.Null);
            Assert.That(nearGateMaterial.GetColor("_BaseColor"), Is.Not.EqualTo(farGateMaterial.GetColor("_BaseColor")),
                "Curved gates lost their individual finishes");
            foreach (var gate in new[] { curveGate, farCurveGate })
            {
                var turn = Quaternion.Euler(0, gate.localEulerAngles.y, 0);
                foreach (float offset in new[] { -.78f, 0f, .78f })
                {
                    Vector3 start = gate.position + turn * new Vector3(-.5f, 0, offset);
                    Assert.That(Physics.Raycast(start, turn * Vector3.right, out var hit, 1f, 1 << 8,
                        QueryTriggerInteraction.Ignore), Is.True, "Curved gate has no wall support");
                    Assert.That(hit.collider.name, Is.EqualTo("Curved residential wall"),
                        "Curved gate extends beyond the wall");
                    // The ray starts 0.5 m in front of the gate centre;
                    // constrain the wall to within another 0.2 m.
                    Assert.That(hit.distance, Is.LessThan(.7f), "Curved gate floats away from the wall");
                }
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
            yield return ApproachWalkUntil(Key.W, () => player.transform.position.z >= 403.3f, 8,
                "Cannot walk alongside the formed-concrete wall");
            ApproachLookAt(alley.transform.TransformPoint(new Vector3(-1.95f, 1.35f, 5f))); yield return null;
            CaptureApproachReview("village-10-formed-wall");
            ApproachLookAt(curveGate.position); yield return null;
            CaptureApproachReview("village-11-near-curved-gate");
            ApproachLookAt(farCurveGate.position); yield return null;
            CaptureApproachReview("village-12-far-curved-gate");
            var slateBounds = slateHouse.GetComponentsInChildren<Renderer>(true)[0].bounds;
            ApproachLookAt(slateBounds.center + Vector3.up * (slateBounds.extents.y * .35f)); yield return null;
            CaptureApproachReview("village-13-slate-house");
            ApproachFaceForward();
        }
    }
}
