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
            var oppositeWeathered = alley.transform.Cast<Transform>()
                .Where(node => node.name == "Weathered compound wall right section").ToArray();
            Assert.That(oppositeWeathered.Length, Is.EqualTo(2), "Opposite wall sections are missing");
            foreach (var section in oppositeWeathered)
            {
                Assert.That(section.GetComponentsInChildren<Collider>(true), Is.Empty);
                var filter = section.GetComponentInChildren<MeshFilter>();
                Assert.That(filter, Is.Not.Null);
                var probe = filter.gameObject.AddComponent<MeshCollider>();
                try
                {
                    probe.sharedMesh = filter.sharedMesh;
                    var walls = alley.transform.Cast<Transform>().Where(node => node.name == "Curved residential wall")
                        .Select(node => node.GetComponent<BoxCollider>()).Where(collider => collider && collider.enabled);
                    foreach (float offset in new[] { -.6f, 0f, .6f })
                    {
                        var ray = new Ray(section.position - section.right * 1.5f + section.forward * offset + Vector3.up * 1.1f, section.right);
                        float collisionDistance = float.PositiveInfinity;
                        foreach (var collider in walls)
                            if (collider.Raycast(ray, out var hit, 3f)) collisionDistance = Mathf.Min(collisionDistance, hit.distance);
                        Assert.That(float.IsInfinity(collisionDistance), Is.False, "Right wall collision is missing");
                        Assert.That(probe.Raycast(ray, out var visualHit, 3f), Is.True, "Right wall visual is missing");
                        Assert.That(Mathf.Abs(visualHit.distance - collisionDistance), Is.LessThan(.25f),
                            "Right wall visual and collision diverge: " + visualHit.distance + "/" + collisionDistance);
                    }
                }
                finally { UnityEngine.Object.DestroyImmediate(probe); }
            }
            var rightPipe = nodes.Where(node => node.name == "Weathered wall service pipe")
                .OrderByDescending(node => node.position.x).First();
            var pipeRenderers = rightPipe.GetComponentsInChildren<Renderer>(true);
            var pipeBounds = pipeRenderers[0].bounds;
            foreach (var renderer in pipeRenderers.Skip(1)) pipeBounds.Encapsulate(renderer.bounds);
            var pipeRay = new Ray(new Vector3(pipeBounds.center.x - 1f, 1.1f, pipeBounds.center.z), Vector3.right);
            float pipeWallX = float.PositiveInfinity;
            foreach (var section in oppositeWeathered)
            {
                var filter = section.GetComponentInChildren<MeshFilter>();
                var probe = filter.gameObject.AddComponent<MeshCollider>();
                try
                {
                    probe.sharedMesh = filter.sharedMesh;
                    if (probe.Raycast(pipeRay, out var hit, 3f)) pipeWallX = Mathf.Min(pipeWallX, hit.point.x);
                }
                finally { UnityEngine.Object.DestroyImmediate(probe); }
            }
            Assert.That(float.IsInfinity(pipeWallX), Is.False, "Right pipe has no wall support");
            Assert.That(Mathf.Abs(pipeBounds.max.x - pipeWallX), Is.LessThan(.04f),
                "Right pipe is buried or detached from its visible wall");
            var rearBoundary = alley.transform.Find("Alley rear boundary");
            Assert.That(rearBoundary, Is.Not.Null);
            var rearCollider = rearBoundary.GetComponent<BoxCollider>();
            Assert.That(rearCollider && rearCollider.enabled && !rearBoundary.GetComponent<Renderer>().enabled, Is.True,
                "Rear wall must preserve collision while using the authored surface");
            var pavement = alley.transform.Find("Alley pavement").GetComponent<BoxCollider>();
            Assert.That(pavement, Is.Not.Null);
            Assert.That(pavement.bounds.min.z, Is.LessThan(rearCollider.bounds.min.z - .1f),
                "Pavement stops short of the rear gate and exposes the sky");
            Assert.That(pavement.bounds.max.z, Is.EqualTo(alley.transform.position.z + 14.5f).Within(.01f),
                "The forward edge of the alley pavement moved");
            var rearGate = alley.transform.Find("Authored rear courtyard gate");
            Assert.That(rearGate, Is.Not.Null, "Rear residential gate is missing");
            Assert.That(rearGate.GetComponentsInChildren<Collider>(true), Is.Empty,
                "Rear gate must not add collision");
            var rearFilters = rearGate.GetComponentsInChildren<MeshFilter>(true);
            Assert.That(rearFilters.Length, Is.GreaterThan(0));
            foreach (float offset in new[] { -3f, 0f, 3f })
            {
                var ray = new Ray(alley.transform.TransformPoint(new Vector3(offset, 1.1f, -15.7f)), Vector3.back);
                Assert.That(rearCollider.Raycast(ray, out var collision, 3f), Is.True, "Rear collision is missing");
                float visualDistance = float.PositiveInfinity;
                foreach (var filter in rearFilters)
                {
                    var probe = filter.gameObject.AddComponent<MeshCollider>();
                    try
                    {
                        probe.sharedMesh = filter.sharedMesh;
                        if (probe.Raycast(ray, out var hit, 3f)) visualDistance = Mathf.Min(visualDistance, hit.distance);
                    }
                    finally { UnityEngine.Object.DestroyImmediate(probe); }
                }
                Assert.That(float.IsInfinity(visualDistance), Is.False, "Rear gate has a visible opening");
                Assert.That(Mathf.Abs(visualDistance - collision.distance), Is.LessThan(.4f),
                    "Rear gate surface and collision diverge");
            }
            var backstreet = alley.transform.Find("Backstreet roof beyond rear wall");
            Assert.That(backstreet, Is.Not.Null, "Backstreet roof is missing");
            Assert.That(backstreet.GetComponentsInChildren<Collider>(true), Is.Empty);
            var backstreetRenderers = backstreet.GetComponentsInChildren<Renderer>(true);
            Assert.That(backstreetRenderers.Length, Is.GreaterThan(0));
            Assert.That(backstreetRenderers.All(renderer => renderer.bounds.max.z < rearCollider.bounds.min.z), Is.True,
                "Backstreet house crosses the rear boundary");
            var backstreetBounds = backstreetRenderers[0].bounds;
            foreach (var renderer in backstreetRenderers.Skip(1)) backstreetBounds.Encapsulate(renderer.bounds);
            foreach (var near in houseBounds)
                Assert.That(backstreetBounds.Intersects(near), Is.False, "Backstreet house overlaps a near house");
            var backCorner = alley.transform.Find("Backstreet slate roof");
            Assert.That(backCorner, Is.Not.Null, "Second backstreet roof is missing");
            Assert.That(backCorner.GetComponentsInChildren<Collider>(true), Is.Empty);
            var cornerRenderers = backCorner.GetComponentsInChildren<Renderer>(true);
            Assert.That(cornerRenderers.Length, Is.GreaterThan(0));
            Assert.That(cornerRenderers.All(renderer => renderer.sharedMaterial &&
                renderer.sharedMaterial.name == "SlateHousePbr"), Is.True);
            long cornerTriangles = 0;
            foreach (var filter in backCorner.GetComponentsInChildren<MeshFilter>(true))
                for (int submesh = 0; submesh < filter.sharedMesh.subMeshCount; submesh++)
                    cornerTriangles += (long)filter.sharedMesh.GetIndexCount(submesh) / 3;
            Assert.That(cornerTriangles, Is.InRange(1, 32000), "Backstreet slate roof used the full mesh");
            var backCornerBounds = cornerRenderers[0].bounds;
            foreach (var renderer in cornerRenderers.Skip(1)) backCornerBounds.Encapsulate(renderer.bounds);
            Assert.That(backCornerBounds.max.z, Is.LessThan(rearCollider.bounds.min.z), "Slate house crosses the rear boundary");
            Assert.That(backCornerBounds.Intersects(backstreetBounds), Is.False, "Backstreet houses overlap");
            foreach (var near in houseBounds)
                Assert.That(backCornerBounds.Intersects(near), Is.False, "Slate backstreet house overlaps a near house");
            var rearRightGround = alley.transform.Find("Residential ground behind rear-right");
            Assert.That(rearRightGround, Is.Not.Null);
            Assert.That(rearRightGround.GetComponentsInChildren<Collider>(true), Is.Empty);
            var rearRightBounds = rearRightGround.GetComponent<Renderer>().bounds;
            Assert.That(backCornerBounds.min.x, Is.GreaterThanOrEqualTo(rearRightBounds.min.x));
            Assert.That(backCornerBounds.max.x, Is.LessThanOrEqualTo(rearRightBounds.max.x));
            Assert.That(backCornerBounds.min.z, Is.GreaterThanOrEqualTo(rearRightBounds.min.z));
            Assert.That(backCornerBounds.max.z, Is.LessThanOrEqualTo(rearRightBounds.max.z));
            var aerialAnchors = alley.transform.Cast<Transform>().Where(node => node.name == "Village rooftop aerial anchor").ToArray();
            Assert.That(aerialAnchors.Length, Is.EqualTo(5), "Rooftop aerial anchors are missing");
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
            Assert.That(background.Length, Is.EqualTo(5), "Background houses are missing");
            foreach (var house in background)
            {
                var filters = house.GetComponentsInChildren<MeshFilter>(true);
                Assert.That(filters.Length, Is.GreaterThan(0));
                long triangles = 0;
                foreach (var filter in filters)
                    for (int submesh = 0; submesh < filter.sharedMesh.subMeshCount; submesh++)
                        triangles += (long)filter.sharedMesh.GetIndexCount(submesh) / 3;
                bool waterTank = house.GetComponentsInChildren<Renderer>(true).Any(renderer =>
                    renderer.sharedMaterial && renderer.sharedMaterial.name == "WaterTankHouse");
                bool usesTiledHouse = house.GetComponentsInChildren<Renderer>(true).Any(renderer =>
                    renderer.sharedMaterial && renderer.sharedMaterial.name == "TiledHouse");
                Assert.That(triangles, Is.LessThanOrEqualTo(waterTank || usesTiledHouse ? 126000 : 32000), "Background house used the full mesh");
                Assert.That(house.GetComponentsInChildren<Collider>(true), Is.Empty);
                Assert.That(house.GetComponentsInChildren<Renderer>(true).All(renderer => renderer.sharedMaterial &&
                    renderer.sharedMaterial.IsKeywordEnabled("_METALLICSPECGLOSSMAP")), Is.True,
                    "Background house lost its PBR material");
            }
            var rearSlate = background.SingleOrDefault(house => house.GetComponentsInChildren<Renderer>(true)
                .Any(renderer => renderer.sharedMaterial && renderer.sharedMaterial.name == "SlateHousePbr"));
            Assert.That(rearSlate, Is.Not.Null, "The rear slate-roof house is missing");
            var tankHouse = background.SingleOrDefault(house => house.GetComponentsInChildren<Renderer>(true)
                .Any(renderer => renderer.sharedMaterial && renderer.sharedMaterial.name == "WaterTankHouse"));
            Assert.That(tankHouse, Is.Not.Null, "Water-tank house is missing behind the shop");
            var tiledHouse = background.SingleOrDefault(house => house.GetComponentsInChildren<Renderer>(true)
                .Any(renderer => renderer.sharedMaterial && renderer.sharedMaterial.name == "TiledHouse"));
            Assert.That(tiledHouse, Is.Not.Null, "Tiled-roof house is missing behind the shop");
            Assert.That(tiledHouse.position.z, Is.LessThan(alley.transform.position.z - 16f),
                "The tiled-roof background does not extend behind the shop");
            var backgroundBounds = new System.Collections.Generic.List<Bounds>();
            var overlaps = new System.Collections.Generic.List<string>();
            foreach (var house in background.Concat(farHouses))
            {
                var renderers = house.GetComponentsInChildren<Renderer>(true);
                var bounds = renderers[0].bounds;
                foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
                if (bounds.Intersects(backstreetBounds))
                    overlaps.Add(house.name + " " + bounds.center + " overlaps backstreet " + backstreetBounds.center);
                if (bounds.Intersects(backCornerBounds))
                    overlaps.Add(house.name + " " + bounds.center + " overlaps slate backstreet " + backCornerBounds.center);
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
            var groundExtension = nodes.SingleOrDefault(node => node.name == "Residential ground behind village");
            Assert.That(groundExtension, Is.Not.Null, "Rear background ground is missing");
            Assert.That(groundExtension.GetComponentsInChildren<Collider>(true), Is.Empty);
            foreach (var ground in new[] { rearGround, groundExtension })
            {
                var mesh = ground.GetComponent<MeshFilter>().sharedMesh;
                Assert.That(mesh, Is.Not.Null);
                var uv = mesh.uv;
                Assert.That(uv.Max(point => point.x) - uv.Min(point => point.x), Is.GreaterThan(15f),
                    "Rear ground texture is stretched across the whole yard");
                Assert.That(uv.Max(point => point.y) - uv.Min(point => point.y), Is.GreaterThan(15f),
                    "Rear ground texture lacks detail along its length");
            }
            var extensionBounds = groundExtension.GetComponent<Renderer>().bounds;
            Assert.That(extensionBounds.max.z, Is.GreaterThan(groundBounds.min.z), "Rear ground slabs do not meet");
            Assert.That(extensionBounds.min.x, Is.LessThan(alley.transform.position.x - 50f), "Rear ground misses the hillside edge");
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
            var nearBrickWalls = alley.transform.Cast<Transform>().Where(node => node.name == "Weathered residential wall" &&
                node.localPosition.x > 0 && (Mathf.Approximately(node.localPosition.z, -12f) ||
                Mathf.Approximately(node.localPosition.z, -6f))).ToArray();
            Assert.That(nearBrickWalls.Length, Is.EqualTo(2));
            foreach (var wall in nearBrickWalls)
            {
                var renderer = wall.GetComponent<Renderer>();
                Assert.That(wall.GetComponent<BoxCollider>().enabled, Is.True, "Near wall collision must remain");
                if (Mathf.Approximately(wall.localPosition.z, -12f))
                    Assert.That(renderer.enabled, Is.False, "Authored wall must hide the original surface");
                else
                {
                    Assert.That(renderer.enabled && renderer.sharedMaterial.GetTag("GraphicsSurface", false) == "alley-aged-brick", Is.True,
                        "Visible near brick wall lost its weathered PBR surface");
                    Assert.That(renderer.sharedMaterial.IsKeywordEnabled("_NORMALMAP") &&
                        renderer.sharedMaterial.IsKeywordEnabled("_METALLICSPECGLOSSMAP"), Is.True);
                }
            }
            var signFrame = nodes.Single(node => node.name == "Projecting shop sign frame");
            var signFaces = nodes.Where(node => node.name == "Baked Korean shop sign face").ToArray();
            Assert.That(signFaces.Length, Is.EqualTo(2));
            var signBounds = signFrame.GetComponent<Renderer>().bounds;
            Assert.That(signFaces.Min(face => face.position.z), Is.LessThan(signBounds.min.z));
            Assert.That(signFaces.Max(face => face.position.z), Is.GreaterThan(signBounds.max.z),
                "Rear sign face is hidden inside the opaque frame");
            var signBrackets = nodes.Where(node => node.name == "Projecting sign mounting bracket").ToArray();
            Assert.That(signBrackets.Length, Is.EqualTo(2), "Projecting sign lacks its two mounting brackets");
            foreach (var bracket in signBrackets)
            {
                Assert.That(bracket.GetComponentsInChildren<Collider>(true), Is.Empty);
                var bracketBounds = bracket.GetComponent<Renderer>().bounds;
                Assert.That(bracketBounds.Intersects(signBounds), Is.True, "Bracket does not meet sign frame");
                Assert.That(bracketBounds.Intersects(shopBounds), Is.True, "Bracket does not reach shop frontage");
                var ray = new Ray(bracket.TransformPoint(Vector3.back * .5f), bracket.forward);
                bool reachedShop = false;
                bool previousBackfaces = Physics.queriesHitBackfaces;
                try
                {
                    Physics.queriesHitBackfaces = true;
                    foreach (var filter in shop.GetComponentsInChildren<MeshFilter>(true))
                    {
                        if (!filter.sharedMesh) continue;
                        var probe = filter.gameObject.AddComponent<MeshCollider>();
                        try
                        {
                            probe.sharedMesh = filter.sharedMesh;
                            if (probe.Raycast(ray, out _, bracket.localScale.z)) reachedShop = true;
                        }
                        finally { UnityEngine.Object.DestroyImmediate(probe); }
                    }
                }
                finally { Physics.queriesHitBackfaces = previousBackfaces; }
                Assert.That(reachedShop, Is.True, "Bracket does not intersect the actual shop mesh");
            }
            var yardWall = nodes.SingleOrDefault(node => node.name == "Shop rear courtyard wall");
            Assert.That(yardWall, Is.Not.Null, "Rear courtyard wall failed to load");
            Assert.That(yardWall.GetComponentsInChildren<Renderer>(true).Length, Is.GreaterThan(0));
            Assert.That(yardWall.GetComponentsInChildren<Collider>(true), Is.Empty);
            var courtyardReturn = nodes.Where(node => node.name == "Rear courtyard return wall").ToArray();
            Assert.That(courtyardReturn.Length, Is.EqualTo(2), "Rear courtyard wall is disconnected");
            foreach (var section in courtyardReturn)
            {
                Assert.That(section.GetComponentsInChildren<Collider>(true), Is.Empty);
                Assert.That(section.GetComponentsInChildren<Renderer>(true).Length, Is.GreaterThan(0));
            }
            var returnBounds = courtyardReturn.Select(section => section.GetComponentsInChildren<Renderer>(true)[0].bounds)
                .OrderBy(bounds => bounds.center.x).ToArray();
            var yardRenderers = yardWall.GetComponentsInChildren<Renderer>(true);
            var yardBounds = yardRenderers[0].bounds;
            foreach (var renderer in yardRenderers.Skip(1)) yardBounds.Encapsulate(renderer.bounds);
            Assert.That(returnBounds[0].Intersects(yardBounds), Is.True,
                "Courtyard return does not meet the authored wall");
            Assert.That(returnBounds[1].max.x, Is.LessThan(alley.transform.position.x - 3.6f),
                "Courtyard return encroaches on the alley");
            Assert.That(returnBounds[0].max.x, Is.GreaterThan(returnBounds[1].min.x), "Courtyard wall has a visible seam");
            Assert.That(returnBounds[1].max.x, Is.GreaterThan(alley.transform.position.x - 4.1f),
                "Courtyard wall does not meet the original rear boundary");
            var cornerPier = nodes.SingleOrDefault(node => node.name == "Rear wall corner pier");
            Assert.That(cornerPier, Is.Not.Null, "Rear courtyard corner is uncovered");
            Assert.That(cornerPier.GetComponentsInChildren<Collider>(true), Is.Empty);
            var cornerBounds = cornerPier.GetComponent<Renderer>().bounds;
            Assert.That(cornerBounds.Intersects(returnBounds[1]), Is.True, "Corner pier misses courtyard wall");
            Assert.That(rearGate.GetComponentsInChildren<Renderer>(true).Any(renderer =>
                renderer.bounds.Intersects(cornerBounds)), Is.True, "Corner pier misses rear gate");
            var wires = nodes.SingleOrDefault(node => node.name == "Batched sagging village cables");
            Assert.That(wires, Is.Not.Null, "Village wiring was not created");
            Assert.That(wires.GetComponent<MeshRenderer>(), Is.Not.Null);
            Assert.That(wires.GetComponentsInChildren<Collider>(true), Is.Empty);
            var servicePoles = alley.transform.Cast<Transform>().Where(node => node.name == "Secondary village utility pole").ToArray();
            Assert.That(servicePoles.Length, Is.EqualTo(2), "Secondary utility supports are missing");
            Assert.That(servicePoles.All(pole => pole.GetComponentsInChildren<Collider>(true).Length == 0), Is.True);
            Assert.That(servicePoles.All(pole => pole.GetComponent<Renderer>().shadowCastingMode ==
                UnityEngine.Rendering.ShadowCastingMode.Off), Is.True, "Secondary supports cast distracting shadows");
            var wireVertices = wires.GetComponent<MeshFilter>().sharedMesh.vertices;
            foreach (var pole in servicePoles)
            {
                var top = pole.position + Vector3.up * 2.62f;
                float distance = wireVertices.Min(vertex => Vector3.Distance(wires.TransformPoint(vertex), top));
                Assert.That(distance, Is.LessThan(.05f), "A secondary support has no attached cable");
            }
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
            var coalNotice = nodes.SingleOrDefault(node => node.name == "Weathered coal delivery notice");
            Assert.That(coalNotice, Is.Not.Null, "Coal delivery notice is missing");
            Assert.That(coalNotice.GetComponentsInChildren<Collider>(true), Is.Empty);
            Assert.That(coalNotice.GetComponent<Renderer>().sharedMaterial.GetTexture("_BaseMap"), Is.Not.Null);
            Assert.That(Physics.Raycast(coalNotice.position - coalNotice.forward * .3f, coalNotice.forward,
                out var coalSupport, .4f, 1 << 8, QueryTriggerInteraction.Ignore), Is.True,
                "Coal notice must be supported by the wall");
            Assert.That(coalSupport.collider.name, Is.EqualTo("Curved residential wall"));
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
            ApproachLookAt(eye + new Vector3(0, .3f, -5)); yield return null;
            CaptureApproachReview("village-21-rear-boundary");
            ApproachFaceForward(); yield return null;
            ApproachLookAt(eye + new Vector3(5, 1.3f, 4)); yield return null;
            CaptureApproachReview("village-02-front-right-roofs");
            ApproachLookAt(eye + new Vector3(-5, 1.3f, 4)); yield return null;
            CaptureApproachReview("village-03-front-left-roofs");
            var tankBounds = tankHouse.GetComponentsInChildren<Renderer>(true)[0].bounds;
            ApproachLookAt(tankBounds.center + Vector3.up * tankBounds.extents.y * .72f); yield return null;
            // This ray crossed the exposed sky wedge before the rear ground
            // extension. The test-only collider never remains in gameplay.
            var groundProbe = groundExtension.gameObject.AddComponent<BoxCollider>();
            try
            {
                Physics.SyncTransforms();
                var gapRay = Get<Camera>(player, "eyes").ViewportPointToRay(new Vector2(.10f, .27f));
                Assert.That(groundProbe.Raycast(gapRay, out var groundHit, 200f), Is.True,
                    "Dusk sky is visible through the rear-ground wedge");
                Assert.That(groundHit.point.y, Is.InRange(-.06f, .01f), "Unexpected rear-ground surface");
            }
            finally { UnityEngine.Object.DestroyImmediate(groundProbe); }
            CaptureApproachReview("village-19-water-tank-house");
            var tiledBounds = tiledHouse.GetComponentsInChildren<Renderer>(true)[0].bounds;
            ApproachLookAt(tiledBounds.center + Vector3.up * tiledBounds.extents.y * .45f); yield return null;
            CaptureApproachReview("village-23-tiled-house");

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
            ApproachLookAt(rearGate.position + Vector3.up * .7f); yield return null;
            CaptureApproachReview("village-24-rear-gate-floor");
            ApproachLookAt(backCornerBounds.center + Vector3.up * backCornerBounds.extents.y * .7f); yield return null;
            CaptureApproachReview("village-27-backstreet-slate");
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
            ApproachLookAt(coalNotice.position); yield return null;
            CaptureApproachReview("village-29-coal-notice");
            var slateBounds = slateHouse.GetComponentsInChildren<Renderer>(true)[0].bounds;
            ApproachLookAt(slateBounds.center + Vector3.up * (slateBounds.extents.y * .35f)); yield return null;
            CaptureApproachReview("village-13-slate-house");
            // A diagnostic close view after the route checks; the dog barrier
            // remains active and no gameplay state is changed by this camera probe.
            Vector3 nearCoal = coalNotice.position - coalNotice.forward * 1.3f;
            player.transform.position = new Vector3(nearCoal.x, player.transform.position.y, nearCoal.z);
            Physics.SyncTransforms(); yield return null;
            ApproachLookAt(coalNotice.position); yield return null;
            CaptureApproachReview("village-30-coal-notice-close");
            ApproachFaceForward();
        }
    }
}
