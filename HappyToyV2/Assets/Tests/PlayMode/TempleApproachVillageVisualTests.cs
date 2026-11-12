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
            var closerRight = nearHouses.Single(house => house.GetComponentsInChildren<Transform>(true)
                .Any(node => node.name.StartsWith("TwoStoryHouseClean", StringComparison.Ordinal)));
            var rightNearEdge = closerRight.GetComponentsInChildren<Renderer>(true).Min(renderer => renderer.bounds.min.x);
            Assert.That(rightNearEdge, Is.LessThan(alley.transform.position.x + 7f),
                "The right two-story house was pushed out of the streetscape");
            var mountainLeft = nearHouses.Single(house => house.position.x < alley.transform.position.x &&
                house.position.z > alley.transform.position.z + 14f);
            var leftNearEdge = mountainLeft.GetComponentsInChildren<Renderer>(true).Max(renderer => renderer.bounds.max.x);
            Assert.That(leftNearEdge, Is.GreaterThan(alley.transform.position.x - 7f),
                "The left mountain-side house was pushed out of the streetscape");
            var slateHouse = nearHouses.SingleOrDefault(house => house.GetComponentsInChildren<Transform>(true)
                .Any(node => node.name.StartsWith("SlateHousePbr", StringComparison.Ordinal)));
            Assert.That(slateHouse, Is.Not.Null, "Reviewed slate house was not placed in the lane");
            Assert.That(slateHouse.GetComponentsInChildren<Collider>(true), Is.Empty,
                "The visual house must not create obstacles");
            var compactHouse = nearHouses.SingleOrDefault(house => house.GetComponentsInChildren<Transform>(true)
                .Any(node => node.name == "Compact brick house repaired ground window"));
            Assert.That(compactHouse, Is.Not.Null, "Reviewed compact two-story house was not placed");
            Assert.That(compactHouse.GetComponentsInChildren<Collider>(true), Is.Empty);
            Assert.That(compactHouse.GetComponentsInChildren<MeshFilter>(true).Any(filter =>
                filter.sharedMesh && filter.sharedMesh.vertexCount == 4 && filter.sharedMesh.GetIndexCount(0) == 6),
                Is.True, "Repaired ground window surface is missing");

            var compactFar = nearHouses.SingleOrDefault(house => house != compactHouse &&
                house.GetComponentsInChildren<Renderer>(true).Any(renderer => renderer.sharedMaterial &&
                    renderer.sharedMaterial.name == "CompactBrickHouseWarm"));
            Assert.That(compactFar, Is.Not.Null, "Reduced compact house is missing near the mountain");
            long compactFarFaces = 0;
            foreach (var filter in compactFar.GetComponentsInChildren<MeshFilter>(true))
                for (int submesh = 0; submesh < filter.sharedMesh.subMeshCount; submesh++)
                    compactFarFaces += (long)filter.sharedMesh.GetIndexCount(submesh) / 3;
            Assert.That(compactFarFaces, Is.LessThanOrEqualTo(102000), "Distant house uses full geometry");
            var compactFarRenderers = compactFar.GetComponentsInChildren<Renderer>(true);
            Assert.That(compactFarRenderers.All(renderer => renderer.sharedMaterial &&
                renderer.sharedMaterial.IsKeywordEnabled("_EMISSION") &&
                renderer.sharedMaterial.GetTexture("_EmissionMap")), Is.True,
                "Distant window emission is not configured");
            Assert.That(compactFar.GetComponentsInChildren<MeshFilter>(true).Any(filter =>
                filter.sharedMesh && filter.sharedMesh.subMeshCount > 0 && filter.sharedMesh.vertexCount == 4 &&
                filter.sharedMesh.GetIndexCount(0) == 6), Is.True, "Stable window surface is missing");
            Assert.That(compactHouse.GetComponentsInChildren<Renderer>(true).All(renderer =>
                !renderer.sharedMaterial.IsKeywordEnabled("_EMISSION")), Is.True,
                "Near house should retain its unlit windows");
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
            var authoredWall = alley.transform.Cast<Transform>().Where(node => node.name == "Authored curved wall section").ToArray();
            Assert.That(authoredWall.Length, Is.EqualTo(3), "Authored curved wall sections are missing");
            foreach (var section in authoredWall)
            {
                Assert.That(section.GetComponentsInChildren<Collider>(true), Is.Empty, "Wall visuals must not add collision");
                var sectionRenderers = section.GetComponentsInChildren<Renderer>(true);
                Assert.That(sectionRenderers.Length, Is.GreaterThan(0));
                Assert.That(sectionRenderers.All(renderer => renderer.sharedMaterial &&
                    renderer.sharedMaterial.name == "KoreanWall"), Is.True, "Wall section lost its original material");
                // Test-only mesh raycast: compare the visible face to the
                // original curved collision without retaining any new collider.
                var ray = new Ray(section.position + section.right * 1.5f + Vector3.up * 1.1f, -section.right);
                var originalWalls = alley.transform.Cast<Transform>().Where(node => node.name == "Curved residential wall")
                    .Select(node => node.GetComponent<BoxCollider>()).Where(collider => collider && collider.enabled);
                float collisionDistance = float.PositiveInfinity;
                foreach (var collider in originalWalls)
                    if (collider.Raycast(ray, out var hit, 3f)) collisionDistance = Mathf.Min(collisionDistance, hit.distance);
                Assert.That(float.IsInfinity(collisionDistance), Is.False, "Wall collision is missing behind its visual");
                var filter = section.GetComponentInChildren<MeshFilter>();
                var probe = filter.gameObject.AddComponent<MeshCollider>();
                try
                {
                    probe.sharedMesh = filter.sharedMesh;
                    Assert.That(probe.Raycast(ray, out var visualHit, 3f), Is.True, "Wall visual has no front face");
                    Assert.That(Mathf.Abs(visualHit.distance - collisionDistance), Is.LessThan(.25f),
                        "Visible wall diverges from retained collision: " + section.name + " visual=" +
                        visualHit.distance + " collision=" + collisionDistance);
                }
                finally { UnityEngine.Object.DestroyImmediate(probe); }
            }
            var weatheredWalls = alley.transform.Cast<Transform>().Where(node => node.name == "Weathered compound wall section").ToArray();
            Assert.That(weatheredWalls.Length, Is.EqualTo(4), "Weathered wall sections are missing");
            foreach (var section in weatheredWalls)
            {
                Assert.That(section.GetComponentsInChildren<Collider>(true), Is.Empty, "Weathered wall visuals must not add collision");
                var filters = section.GetComponentsInChildren<MeshFilter>(true);
                Assert.That(filters.Length, Is.GreaterThan(0));
                long faces = 0;
                foreach (var filter in filters)
                    for (int submesh = 0; submesh < filter.sharedMesh.subMeshCount; submesh++)
                        faces += (long)filter.sharedMesh.GetIndexCount(submesh) / 3;
                Assert.That(faces, Is.LessThanOrEqualTo(61000), "Weathered wall exceeded its geometry budget");
                Assert.That(section.GetComponentsInChildren<Renderer>(true).All(renderer => renderer.sharedMaterial &&
                    renderer.sharedMaterial.name == "WeatheredWall" && renderer.sharedMaterial.IsKeywordEnabled("_METALLICSPECGLOSSMAP")),
                    Is.True, "Weathered wall lost its original PBR material");
                var probe = filters[0].gameObject.AddComponent<MeshCollider>();
                try
                {
                    probe.sharedMesh = filters[0].sharedMesh;
                    var originalWalls = alley.transform.Cast<Transform>().Where(node => node.name == "Curved residential wall")
                        .Select(node => node.GetComponent<BoxCollider>()).Where(collider => collider && collider.enabled);
                    foreach (float offset in new[] { -.6f, 0f, .6f })
                    {
                        var ray = new Ray(section.position + section.right * 1.5f + section.forward * offset + Vector3.up,
                            -section.right);
                        float collisionDistance = float.PositiveInfinity;
                        foreach (var collider in originalWalls)
                            if (collider.Raycast(ray, out var hit, 3f)) collisionDistance = Mathf.Min(collisionDistance, hit.distance);
                        Assert.That(float.IsInfinity(collisionDistance), Is.False, "Weathered wall collision is missing");
                        Assert.That(probe.Raycast(ray, out var visualHit, 3f), Is.True, "Weathered wall has no visible surface");
                        Assert.That(Mathf.Abs(visualHit.distance - collisionDistance), Is.LessThan(.25f),
                            "Weathered wall surface and original collision diverge: visual=" + visualHit.distance + " collision=" + collisionDistance);
                    }
                }
                finally { UnityEngine.Object.DestroyImmediate(probe); }
            }
            var aerialAnchors = alley.transform.Cast<Transform>().Where(node => node.name == "Village rooftop aerial anchor").ToArray();
            Assert.That(aerialAnchors.Length, Is.EqualTo(4), "Rooftop aerial anchors are missing");
            Assert.That(aerialAnchors.All(node => !node.GetComponent<Collider>()), Is.True);
            var aerialMesh = alley.transform.Find("Batched vintage rooftop TV aerials");
            Assert.That(aerialMesh && aerialMesh.GetComponent<MeshFilter>().sharedMesh.vertexCount > 0, Is.True);
            Assert.That(aerialMesh.GetComponentsInChildren<Collider>(true), Is.Empty, "Rooftop details must not obstruct the player");
            var oldShopWindows = alley.transform.Cast<Transform>().Where(node => node.name == "Shop warm window").ToArray();
            Assert.That(oldShopWindows.Length, Is.EqualTo(3));
            Assert.That(oldShopWindows.All(node => !node.GetComponent<Renderer>().enabled), Is.True,
                "Legacy window panels must not cover the authored storefront");
            var sideWalls = alley.transform.Cast<Transform>().Where(node => node.name == "Shop interior side wall").ToArray();
            Assert.That(sideWalls.Length, Is.EqualTo(2));
            Assert.That(sideWalls.All(node => !node.GetComponent<Renderer>().enabled && node.GetComponent<Collider>().enabled), Is.True,
                "Exterior-facing side-wall placeholders must retain only collision");
            Assert.That(alley.transform.Cast<Transform>().Count(node => node.name == "Shop interior inset side wall" &&
                node.GetComponent<Renderer>().enabled), Is.EqualTo(2), "Inset interior side walls are missing");
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
            Assert.That(curvedCoping.Length, Is.EqualTo(34), "Curved wall coping is missing around the gate");
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
            Assert.That(curveGate.GetComponent<Renderer>().enabled, Is.False, "Placeholder gate still covers the authored gate");
            var authoredGate = alley.transform.Find("Authored curved Korean gate");
            Assert.That(authoredGate, Is.Not.Null, "Authored Korean gate is missing");
            Assert.That(authoredGate.GetComponentsInChildren<Collider>(true), Is.Empty, "Decorative gate must not add collision");
            var gateRenderers = authoredGate.GetComponentsInChildren<Renderer>(true);
            Assert.That(gateRenderers.Length, Is.GreaterThan(0));
            Assert.That(gateRenderers.All(renderer => renderer.sharedMaterial && renderer.sharedMaterial.name == "KoreanWall"), Is.True,
                "The gate lost the original authored material");
            var gateFilter = authoredGate.GetComponentInChildren<MeshFilter>();
            var gateProbe = gateFilter.gameObject.AddComponent<MeshCollider>();
            try
            {
                gateProbe.sharedMesh = gateFilter.sharedMesh;
                var originalWalls = alley.transform.Cast<Transform>().Where(node => node.name == "Curved residential wall")
                    .Select(node => node.GetComponent<BoxCollider>()).Where(collider => collider && collider.enabled);
                foreach (float offset in new[] { -.55f, 0f, .55f })
                {
                    var ray = new Ray(authoredGate.position - authoredGate.right * 1.5f + authoredGate.forward * offset + Vector3.up,
                        authoredGate.right);
                    float collisionDistance = float.PositiveInfinity;
                    foreach (var collider in originalWalls)
                        if (collider.Raycast(ray, out var hit, 3f)) collisionDistance = Mathf.Min(collisionDistance, hit.distance);
                    Assert.That(float.IsInfinity(collisionDistance), Is.False, "Gate has no original collision behind it");
                    Assert.That(gateProbe.Raycast(ray, out var visualHit, 3f), Is.True, "Gate door has no visible surface");
                    Assert.That(Mathf.Abs(visualHit.distance - collisionDistance), Is.LessThan(.30f),
                        "Gate surface and retained collision diverge: visual=" + visualHit.distance + " collision=" + collisionDistance);
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(gateProbe); }
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
            var allRunoff = nodes.Where(node => node.name == "Wall rain runoff").OrderBy(node => node.position.z).ToArray();
            Assert.That(allRunoff.Length, Is.EqualTo(6), "Wall rain runoff is missing");
            var runoff = allRunoff.Where(node => alley.transform.InverseTransformPoint(node.position).x < 0).ToArray();
            Assert.That(runoff.Length, Is.EqualTo(3), "Left wall runoff is missing");
            foreach (var patch in allRunoff)
            {
                Assert.That(patch.GetComponentsInChildren<Collider>(true), Is.Empty);
                var renderer = patch.GetComponent<Renderer>();
                Assert.That(renderer.sharedMaterial.GetTexture("_BaseMap"), Is.Not.Null);
                Assert.That(renderer.sharedMaterial.IsKeywordEnabled("_SURFACE_TYPE_TRANSPARENT"), Is.True);
                var intoWall = patch.forward;
                Assert.That(Physics.Raycast(patch.position - intoWall * .3f, intoWall, out var support, .42f,
                    1 << 8, QueryTriggerInteraction.Ignore), Is.True, "Runoff has no wall support");
                Assert.That(support.collider.name, Is.EqualTo("Curved residential wall"));
            }
            var numberPlates = nodes.Where(node => node.name == "Village house number plate").OrderBy(node => node.position.z).ToArray();
            Assert.That(numberPlates.Length, Is.EqualTo(2), "House number plates are missing");
            Assert.That(numberPlates.All(node => node.GetComponentsInChildren<Collider>(true).Length == 0),
                Is.True, "Wall details must not add obstacles");
            foreach (var plate in numberPlates)
            {
                var intoWall = plate.forward;
                Assert.That(Physics.Raycast(plate.position - intoWall * .3f, intoWall, out var support, .4f,
                    1 << 8, QueryTriggerInteraction.Ignore), Is.True, "House number floats off its wall");
                Assert.That(support.collider.name == "Weathered residential wall" ||
                    support.collider.name == "Curved residential wall", Is.True, "House number has no wall behind it");
                Assert.That(support.distance, Is.InRange(.29f, .36f), "House number is too far from the wall");
            }
            var numbers = nodes.Where(node => node.name == "House number lettering")
                .Select(node => node.GetComponent<TextMesh>().text).ToArray();
            Assert.That(numbers, Is.EquivalentTo(new[] { "23-4", "18" }));
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
            var approachFlow = UnityEngine.Object.FindObjectsOfType<MonoBehaviour>()
                .Single(x => x.GetType().Name == "TempleApproachFlow");
            Assert.That(Get<bool>(approachFlow, "MountainReady"), Is.True,
                "The mountain must be visible by the shop approach, before the dog encounter");
            CaptureApproachReview("village-04-shop-approach");
            eye = Get<Camera>(player, "eyes").transform.position;
            ApproachLookAt(eye + new Vector3(5, 1.3f, 4)); yield return null;
            CaptureApproachReview("village-05-shop-right-roofs");
            var compactBounds = compactHouse.GetComponentsInChildren<Renderer>(true)
                .OrderByDescending(renderer => renderer.bounds.size.sqrMagnitude).First().bounds;
            ApproachLookAt(compactBounds.center + Vector3.up * (compactBounds.extents.y * .35f)); yield return null;
            CaptureApproachReview("village-18-compact-house");

            ApproachLookAt(numberPlates[0].position); yield return null;
            CaptureApproachReview("village-14-house-number");


            ApproachFaceForward();
            yield return ApproachWalkUntil(Key.W, () => player.transform.position.z >= 399f, 15,
                "Cannot walk to the curved lane");
            CaptureApproachReview("village-06-curve-approach");
            eye = Get<Camera>(player, "eyes").transform.position;
            ApproachLookAt(eye + new Vector3(5, 1.3f, 4)); yield return null;
            CaptureApproachReview("village-07-curve-right-roofs");
            ApproachLookAt(eye + new Vector3(-5, 1.3f, 4)); yield return null;
            CaptureApproachReview("village-08-curve-left-roofs");
            ApproachFaceForward();
            yield return ApproachWalkUntil(Key.W, () => player.transform.position.z >= 400.4f, 6, "Cannot approach wall runoff");
            ApproachLookAt(runoff[0].position); yield return null;
            CaptureApproachReview("village-17-wall-runoff-close");
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
            ApproachLookAt(runoff[1].position); yield return null;
            CaptureApproachReview("village-16-wall-runoff");

            ApproachLookAt(curveGate.position); yield return null;
            CaptureApproachReview("village-11-near-curved-gate");
            ApproachLookAt(farCurveGate.position); yield return null;
            CaptureApproachReview("village-12-far-curved-gate");
            ApproachLookAt(numberPlates[1].position); yield return null;
            CaptureApproachReview("village-15-curved-house-number");
            var slateBounds = slateHouse.GetComponentsInChildren<Renderer>(true)[0].bounds;
            ApproachLookAt(slateBounds.center + Vector3.up * (slateBounds.extents.y * .35f)); yield return null;
            CaptureApproachReview("village-13-slate-house");
            ApproachFaceForward();
        }
    }
}
