using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;
using static HappyToy.V2.CloudTests.RuntimeAccess;

namespace HappyToy.V2.CloudTests
{
    public sealed partial class CloudPlayModeTests
    {
        Component[] CampusCurrentDoors() => Components("Interactable")
            .Where(item => item.gameObject.activeInHierarchy && Get<object>(item, "kind").ToString() == "Door" &&
                Get<Transform>(item, "movingLeaf")).ToArray();
        object[] CampusSpaces(Component campus) => ((IEnumerable)Get<object>(campus, "Spaces")).Cast<object>().ToArray();
        Component[] CampusDoors(Component campus) => ((IEnumerable)Get<object>(campus, "Doors")).Cast<Component>().ToArray();
        Vector3 CampusMemoryApproach(Component[] memories, int index)
        {
            Vector3 point = memories[index].transform.position;
            point += index == 4 ? Vector3.forward : Vector3.back * (index == 2 ? .85f : 1.02f);
            point.y = (index == 4 ? -5 : 5) + .03f;
            return point;
        }

        IEnumerator CampusOpenDoors()
        {
            var doors = CampusCurrentDoors();
            foreach (var door in doors)
                if (!Get<bool>(door, "IsOpen")) Assert.That((bool)Call(door, "OpenForPursuer"), Is.True);
            yield return Wait(() => doors.All(door => Get<bool>(door, "AtRequestedDoorPose")), 5,
                "Current-layout doors did not physically reach their requested open pose");
            Physics.SyncTransforms(); yield return null; yield return null;
        }

        Vector3 CampusFloor(Vector3 requested)
        {
            Assert.That(NavMesh.SamplePosition(requested, out var floor, .45f, NavMesh.AllAreas), Is.True,
                "Missing campus floor at " + requested);
            Assert.That(Mathf.Abs(floor.position.y - requested.y), Is.LessThan(.3f), "Floor sampling selected the wrong storey");
            return floor.position;
        }

        void CampusRoute(Vector3 from, Vector3 to, string name)
        {
            var path = new NavMeshPath();
            bool calculated = NavMesh.CalculatePath(from, to, NavMesh.AllAreas, path);
            string diagnostic = name + " from=" + from.ToString("F3") + " to=" + to.ToString("F3") +
                " calculated=" + calculated + " status=" + path.status + " corners=" +
                string.Join(" -> ", path.corners.Select(point => point.ToString("F3")));
            if (!calculated || path.status != NavMeshPathStatus.PathComplete)
            {
                TestContext.Out.WriteLine("HAPPYTOY_CAMPUS_ROUTE_FAILURE " + diagnostic);
                var camera = Get<Camera>(player, "eyes");
                Vector3 originalPosition = camera.transform.position; Quaternion originalRotation = camera.transform.rotation;
                try
                {
                    Vector3 endpoint = path.corners.Length > 0 ? path.corners.Last() : from;
                    camera.transform.SetPositionAndRotation(endpoint + Vector3.up * 1.65f,
                        Quaternion.LookRotation(to + Vector3.up * .45f - (endpoint + Vector3.up * 1.65f)));
                    var frame = SchoolCameraFrame(camera, out _, out _, out _);
                    try { CloudExperienceTests.Artifact("campus-route-failure-" + Guid.NewGuid().ToString("N").Substring(0, 8) + ".png", frame.EncodeToPNG()); }
                    finally { UnityEngine.Object.Destroy(frame); }
                    TestContext.Out.WriteLine("Controlled failure view: actual path endpoint=" + endpoint.ToString("F3") +
                        " camera=" + camera.transform.position.ToString("F3") + "; player pose and geometry unchanged.");
                }
                catch (Exception error) { TestContext.Out.WriteLine("Failure-view capture unavailable: " + error.Message); }
                finally { camera.transform.SetPositionAndRotation(originalPosition, originalRotation); }
            }
            Assert.That(calculated, Is.True, diagnostic);
            Assert.That(path.status, Is.EqualTo(NavMeshPathStatus.PathComplete),
                diagnostic + " endpoint=" + (path.corners.Length == 0 ? "none" : path.corners.Last().ToString("F3")));
        }

        bool CampusStandingClear(Vector3 point)
        {
            var pose = Call(player, "CaptureProgress"); Set(pose, "position", point); Set(pose, "crouched", false);
            return (bool)Call(player, "CanRestoreChapterProgress", pose);
        }

        Vector3[] CampusRoomPoints(object space)
        {
            var rect = Get<Rect>(space, "rect"); float height = Get<float>(space, "Height");
            var points = new List<Vector3>();
            foreach (float x in new[] { .25f, .5f, .75f }) foreach (float z in new[] { .25f, .5f, .75f })
            {
                var requested = new Vector3(Mathf.Lerp(rect.xMin, rect.xMax, x), height + .03f, Mathf.Lerp(rect.yMin, rect.yMax, z));
                if (!NavMesh.SamplePosition(requested, out var floor, .45f, NavMesh.AllAreas) ||
                    Mathf.Abs(floor.position.y - height) > .3f || !rect.Contains(new Vector2(floor.position.x, floor.position.z)) ||
                    !CampusStandingClear(floor.position)) continue;
                if (points.All(other => Vector3.Distance(other, floor.position) > .5f)) points.Add(floor.position);
            }
            Assert.That(points.Count, Is.GreaterThanOrEqualTo(2),
                "Room lacks two actual clear standing locations: " + Get<string>(space, "id"));
            Assert.That(points.Any(a => points.Any(b => Vector3.Distance(a, b) > 1.5f)), Is.True,
                "Room's usable area collapsed to a tiny dead space: " + Get<string>(space, "id"));
            return points.ToArray();
        }

        void CampusDoorPhysicalCrossing(Component door)
        {
            Vector3 normal = Get<Vector3>(door, "DoorNormal");
            Vector3 at = door.transform.position; at.y += .03f;
            Vector3 a = CampusFloor(at - normal * 1.15f), b = CampusFloor(at + normal * 1.15f);
            Assert.That(CampusStandingClear(a) && CampusStandingClear(b), Is.True,
                "Door approaches lead into furniture, a wall or unsupported space: " + Get<string>(door, "stableId"));
            CampusRoute(a, b, "Door connects both occupied sides: " + Get<string>(door, "stableId"));
            var controller = player.GetComponent<CharacterController>();
            float radius = controller.radius - .02f, half = Mathf.Max(0, controller.height * .5f - controller.radius);
            Vector3 center = a + controller.center, segment = b - a;
            var blockers = Physics.CapsuleCastAll(center - Vector3.up * half, center + Vector3.up * half, radius,
                segment.normalized, segment.magnitude, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                .Where(hit => !hit.collider.transform.IsChildOf(player.transform) &&
                    !hit.collider.GetComponentInParent(RequireType("StalkerBrain"))).ToArray();
            Assert.That(blockers, Is.Empty, "Open campus doorway blocks the real standing capsule: " +
                Get<string>(door, "stableId") + " | " + string.Join("; ", blockers.Select(hit => hit.collider.name + " at " + hit.point)));
        }

        void CampusAimAtMemory(Component memory, Vector3 feet)
        {
            PlacePlayer(feet); var camera = Get<Camera>(player, "eyes");
            var target = memory.GetComponentsInChildren<Collider>().Where(collider => collider.enabled && !collider.isTrigger)
                .OrderBy(collider => Vector3.Distance(collider.bounds.center, camera.transform.position)).First();
            Vector3 delta = target.bounds.center - camera.transform.position;
            var pose = Call(player, "CaptureProgress");
            Set(pose, "yaw", Mathf.Repeat(Mathf.Atan2(delta.x, delta.z) * Mathf.Rad2Deg, 360));
            Set(pose, "pitch", -Mathf.Atan2(delta.y, new Vector2(delta.x, delta.z).magnitude) * Mathf.Rad2Deg);
            Call(player, "RestoreChapterProgress", pose); Physics.SyncTransforms();
        }

        Vector3Int CampusVertexKey(Vector3 point) => new Vector3Int(
            Mathf.RoundToInt(point.x * 1000), Mathf.RoundToInt(point.y * 1000), Mathf.RoundToInt(point.z * 1000));
        readonly Dictionary<Vector3Int,List<Vector3>> campusBatchPositions=new Dictionary<Vector3Int,List<Vector3>>();

        HashSet<Vector3Int> CampusRenderedBatchVertices(Transform root)
        {
            var vertices = new HashSet<Vector3Int>();
            campusBatchPositions.Clear();
            foreach (var filter in root.GetComponentsInChildren<MeshFilter>().Where(filter =>
                filter.name == "Campus architectural spatial batch" && filter.sharedMesh &&
                filter.GetComponent<MeshRenderer>() && filter.GetComponent<MeshRenderer>().enabled))
                foreach (var vertex in filter.sharedMesh.vertices)
                {
                    var point=filter.transform.TransformPoint(vertex);var key=CampusVertexKey(point);vertices.Add(key);
                    if(!campusBatchPositions.TryGetValue(key,out var points)){points=new List<Vector3>();campusBatchPositions.Add(key,points);}points.Add(point);
                }
            return vertices;
        }

        bool CampusPartIsRendered(MeshFilter filter, HashSet<Vector3Int> batches)
        {
            var renderer = filter.GetComponent<MeshRenderer>();
            if (!renderer || !filter.gameObject.activeInHierarchy || !filter.sharedMesh) return false;
            if (renderer.enabled) return true;
            // Batching disables source renderers. Three actual source vertices
            // must remain in a live combined mesh; component counts alone do
            // not prove that disabled copied detail reaches the player camera.
            var vertices = filter.sharedMesh.vertices;
            return vertices.Length > 2 && new[] { 0, vertices.Length / 2, vertices.Length - 1 }
                .All(index => CampusVertexIsRendered(filter.transform.TransformPoint(vertices[index]),batches));
        }
        bool CampusVertexIsRendered(Vector3 point,HashSet<Vector3Int> batches)
        {
            // Source and CombineMeshes multiply the same matrices in different
            // orders. Half-millimetre keys can round to adjacent bins by one ULP.
            // Adjacent bins only narrow the search; real world positions must
            // still agree within one millimetre.
            var key=CampusVertexKey(point);
            for(int x=-1;x<=1;x++)for(int y=-1;y<=1;y++)for(int z=-1;z<=1;z++)
                if(campusBatchPositions.TryGetValue(key+new Vector3Int(x,y,z),out var points) &&
                    points.Any(candidate=>(candidate-point).sqrMagnitude<=.000001f))return true;
            return false;
        }

        Bounds CampusMeshBounds(IEnumerable<MeshFilter> filters)
        {
            var bounds = new Bounds(); bool found = false;
            foreach (var filter in filters)
            {
                var local = filter.sharedMesh.bounds;
                for (int corner = 0; corner < 8; corner++)
                {
                    Vector3 point = filter.transform.TransformPoint(local.center + Vector3.Scale(local.extents,
                        new Vector3((corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1)));
                    if (!found) { bounds = new Bounds(point, Vector3.zero); found = true; } else bounds.Encapsulate(point);
                }
            }
            Assert.That(found, Is.True, "Furniture has no real mesh geometry"); return bounds;
        }

        [UnityTest, Timeout(45000)]
        public IEnumerator SchoolCampusMusicFurnitureKeepsFullMetreAssembliesGroundedSupportsAndRenderedScores()
        {
            Call(shell, "BeginChapter"); yield return null; yield return null;
            var campus = Get<Component>(Get<Component>(session, "Chapter"), "Campus");
            var root = Get<Transform>(campus, "Root"); var batches = CampusRenderedBatchVertices(root);
            Transform Furniture(string name) => root.GetComponentsInChildren<Transform>()
                .Single(item => item.name == "Campus reused " + name);
            MeshFilter[] Visible(Transform furniture) => furniture.GetComponentsInChildren<MeshFilter>()
                .Where(filter => CampusPartIsRendered(filter, batches)).ToArray();
            MeshFilter[] Detail(Transform furniture, string name, int count)
            {
                var parts = furniture.GetComponentsInChildren<MeshFilter>().Where(filter => filter.name == name).ToArray();
                Assert.That(parts.Length, Is.EqualTo(count), "Lost complete furniture detail: " + name);
                Assert.That(parts.All(filter => CampusPartIsRendered(filter, batches)), Is.True,
                    "Copied detail is absent from both live renderers and static batches: " + name);
                return parts;
            }

            var piano = Furniture("Music upright piano"); var pianoBounds = CampusMeshBounds(Visible(piano));
            Assert.That(pianoBounds.size.x, Is.EqualTo(2.2f).Within(.01f), "Piano was copied with primitive unit scale");
            Assert.That(pianoBounds.size.y, Is.EqualTo(1.5225f).Within(.01f));
            Assert.That(pianoBounds.min.y, Is.EqualTo(5).Within(.005f));
            var keys = Detail(piano, "Ivory piano key", 28); Detail(piano, "Raised black piano key", 20);
            Detail(piano, "Piano upper lid", 1); Detail(piano, "Piano pedal", 3);
            var pianoCollision = piano.GetComponent<BoxCollider>().bounds;
            Assert.That(pianoCollision.size.y, Is.EqualTo(1.5f).Within(.015f));
            Assert.That(CampusMeshBounds(keys).min.z, Is.LessThan(pianoCollision.min.z - .10f),
                "Decorative keyboard overhang was included in the navigation collision body");

            var bench = Furniture("Piano bench"); var benchBounds = CampusMeshBounds(Visible(bench));
            Assert.That(benchBounds.size.y, Is.EqualTo(.48f).Within(.005f));
            Assert.That(benchBounds.min.y, Is.EqualTo(5).Within(.005f));
            var legs = Detail(bench, "Grounded bench leg", 4);
            Assert.That(legs.All(leg => Mathf.Abs(CampusMeshBounds(new[] { leg }).min.y - 5) < .005f), Is.True);
            var seat = bench.GetComponentsInChildren<MeshFilter>().Single(filter => filter.name == "Piano bench");
            Assert.That(CampusMeshBounds(new[] { seat }).center.y, Is.EqualTo(5.4f).Within(.005f));
            Assert.That(bench.GetComponent<BoxCollider>().bounds.min.y, Is.EqualTo(5).Within(.005f),
                "Bench support collision floats above the floor");

            var chairs = root.GetComponentsInChildren<Transform>().Where(item => item.name == "Campus reused Abandoned choir chair").ToArray();
            Assert.That(chairs.Length, Is.GreaterThanOrEqualTo(4));
            foreach (var chair in chairs)
            {
                foreach (var slat in Detail(chair, "Plywood chair back slat", 4))
                {
                    var size = CampusMeshBounds(new[] { slat }).size;
                    Assert.That(Vector3.Distance(size, new Vector3(.105f, .32f, .045f)), Is.LessThan(.003f),
                        "Plywood back slat was distorted by the copied body's scale");
                }
                Detail(chair, "Chair back top rail", 1);
                Assert.That(chair.GetComponentsInChildren<MeshRenderer>().Single(renderer => renderer.name == "Chair back").enabled, Is.False);
                Assert.That(chair.GetComponent<BoxCollider>().bounds.max.y, Is.GreaterThan(5.92f),
                    "Replacement chair back was omitted from the physical body");
            }

            var stands = root.GetComponentsInChildren<Transform>().Where(item => item.name == "Campus reused Choir music stand").ToArray();
            Assert.That(stands.Length, Is.GreaterThanOrEqualTo(4));
            foreach (var stand in stands)
            {
                var sheet = Detail(stand, "Readable score sheet", 1).Single();
                Detail(stand, "Music-rest rim", 1); Detail(stand, "Music-rest lower ledge", 1);
                Detail(stand, "Printed musical staff", 10); Detail(stand, "Printed note head", 5); Detail(stand, "Printed note stem", 5);
                Assert.That(Mathf.Abs(Vector3.Dot(sheet.transform.forward, Vector3.up)), Is.GreaterThan(.1f), "Score lost its authored tilt");
                Assert.That(Vector3.Angle(sheet.transform.forward, stand.forward), Is.GreaterThan(20), "Score lost its authored singer-facing angle");
                Assert.That(CampusMeshBounds(new[] { sheet }).max.y, Is.GreaterThan(stand.GetComponent<BoxCollider>().bounds.max.y + .02f),
                    "Non-colliding angled score was included in the physical stand body");
            }
        }

        [UnityTest, Timeout(150000)]
        public IEnumerator SchoolCampusHasSixDistinctRoomsPerFloorConnectedDoorsAndReturnRoutesToAllMemories()
        {
            Call(shell, "BeginChapter"); yield return null; yield return null;
            var chapter = Get<Component>(session, "Chapter"); var campus = Get<Component>(chapter, "Campus");
            Assert.That(Get<int>(chapter, "LayoutVersion"), Is.EqualTo(2)); Assert.That(campus, Is.Not.Null);
            var spaces = CampusSpaces(campus);
            Assert.That(spaces.Select(space => Get<string>(space, "id")).Distinct().Count(), Is.EqualTo(spaces.Length));
            foreach (int floor in new[] { 0, 1, -1 })
            {
                Assert.That((int)Call(campus, "RoomCount", floor), Is.GreaterThanOrEqualTo(5));
                Assert.That(spaces.Count(space => Get<int>(space, "floor") == floor && Get<bool>(space, "room")),
                    Is.EqualTo((int)Call(campus, "RoomCount", floor)));
            }
            var doors = CampusDoors(campus); Assert.That(doors.Length, Is.GreaterThanOrEqualTo(24));
            Assert.That(doors.Select(door => Get<string>(door, "stableId")).Distinct().Count(), Is.EqualTo(doors.Length));
            Assert.That(doors.All(door => door.gameObject.activeInHierarchy), Is.True);
            yield return CampusOpenDoors();
            Vector3 entrance = CampusFloor(new Vector3(-6.4f, .03f, -.5f));
            foreach (var room in spaces.Where(space => Get<bool>(space, "room")))
            {
                Assert.That(Get<string>(room, "label"), Is.Not.Empty);
                var points = CampusRoomPoints(room);
                foreach (var point in points.Take(2))
                {
                    CampusRoute(entrance, point, "Reachable room " + Get<string>(room, "id"));
                    CampusRoute(point, entrance, "Room return route " + Get<string>(room, "id"));
                }
            }
            foreach (var door in doors) CampusDoorPhysicalCrossing(door);
            var memories = Get<Component[]>(chapter, "Memories");
            foreach (var point in Enumerable.Range(2, 3).Select(index => CampusMemoryApproach(memories, index)))
            {
                Vector3 floor = CampusFloor(point); Assert.That(CampusStandingClear(floor), Is.True);
                CampusRoute(entrance, floor, "Memory approach"); CampusRoute(floor, entrance, "Memory return");
            }
            for (int index = 2; index < 5; index++)
            {
                var feet = CampusMemoryApproach(memories, index);
                CampusAimAtMemory(memories[index], CampusFloor(feet));
                yield return Wait(() => Get<Component>(player, "Focus") == memories[index], 3,
                    "New campus geometry blocks production focus on memory " + index);
            }
            Assert.That(Get<int>(chapter, "Recovered"), Is.Zero, "Structure QA must not bypass original memory/reveal gates");
        }

        [UnityTest, Timeout(150000)]
        public IEnumerator SchoolCampusVersionTwoCheckpointKeepsUpperPlayerPoseExplicitDoorGeometryAndFiniteLighting()
        {
            string directory = NewRecordFixture(); Call(session, "ConfigureRecordDirectory", directory);
            Call(shell, "BeginChapter"); yield return null; yield return null; yield return CampusOpenDoors();
            var chapter = Get<Component>(session, "Chapter"); var campus = Get<Component>(chapter, "Campus");
            Vector3 pose = CampusFloor(CampusMemoryApproach(Get<Component[]>(chapter, "Memories"), 2)); PlacePlayer(pose);
            Call(shell, "Pause"); var saved = SchoolCheckpointCopy(Call(session, "CaptureChapterCheckpoint"));
            Assert.That(Get<int>(saved, "simulationVersion"), Is.EqualTo(2));
            Assert.That(Get<int>(saved, "recovered"), Is.Zero);
            string[] ids = Get<Array>(saved, "doors").Cast<object>().Select(door => Get<string>(door, "id")).ToArray();
            var generated = CampusDoors(campus).Select(door => Get<string>(door, "stableId")).ToArray();
            Assert.That(generated.All(id => ids.Contains(id)), Is.True, "Checkpoint dropped current explicit campus door identities");
            string lighting = JsonUtility.ToJson(Get<object>(saved, "lighting"));
            var previous = session; Call(shell, "Restart", false); yield return RecoveryRebind(previous);
            Call(session, "ConfigureRecordDirectory", directory); Call(session, "CreateChapterForCheckpoint", saved);
            chapter = Get<Component>(session, "Chapter"); Assert.That(Get<int>(chapter, "LayoutVersion"), Is.EqualTo(2));
            Call(chapter, "PrepareCheckpointNavigation", saved); yield return null; yield return null;
            Call(session, "ApplyChapterCheckpoint", saved);
            Assert.That(Vector3.Distance(player.transform.position, Get<Vector3>(Get<object>(saved, "player"), "position")), Is.LessThan(.001f));
            Assert.That(Get<bool>(session, "InputAllowed"), Is.False);
            Assert.That(JsonUtility.ToJson(Call(Get<Component>(chapter, "Lighting"), "Capture")), Is.EqualTo(lighting),
                "Lighting changed during paused restoration");
            Call(shell, "Begin"); Call(shell, "Pause");
            var recaptured = Call(session, "CaptureChapterCheckpoint");
            Assert.That(Get<Array>(recaptured, "doors").Cast<object>().Select(door => Get<string>(door, "id")), Is.EqualTo(ids));
            Assert.That(CampusDoors(Get<Component>(chapter, "Campus")).All(door => Get<bool>(door, "IsOpen")), Is.True);
        }

        [UnityTest, Timeout(150000)]
        public IEnumerator SchoolCampusLegacyVersionOneCheckpointSelectsOriginalSchoolAndRestoresItsDoorIdentitySet()
        {
            string directory = NewRecordFixture(); Call(session, "ConfigureRecordDirectory", directory);
            Call(session, "CreateChapterVersion", 1); Begin(); yield return null; yield return null;
            var chapter = Get<Component>(session, "Chapter");
            Assert.That(Get<int>(chapter, "LayoutVersion"), Is.EqualTo(1)); Assert.That(Get<Component>(chapter, "Campus"), Is.Null);
            Call(shell, "Pause"); var saved = SchoolCheckpointCopy(Call(session, "CaptureChapterCheckpoint"));
            Assert.That(Get<int>(saved, "simulationVersion"), Is.EqualTo(1));
            string[] ids = Get<Array>(saved, "doors").Cast<object>().Select(door => Get<string>(door, "id")).ToArray();
            var previous = session; Call(shell, "Restart", false); yield return RecoveryRebind(previous);
            Call(session, "ConfigureRecordDirectory", directory); Call(session, "CreateChapterForCheckpoint", saved);
            chapter = Get<Component>(session, "Chapter"); Assert.That(Get<int>(chapter, "LayoutVersion"), Is.EqualTo(1));
            Assert.That(Get<Component>(chapter, "Campus"), Is.Null);
            Call(chapter, "PrepareCheckpointNavigation", saved); yield return null; yield return null;
            Call(session, "ApplyChapterCheckpoint", saved); Begin(); Call(shell, "Pause");
            Assert.That(Get<Array>(Call(session, "CaptureChapterCheckpoint"), "doors").Cast<object>().Select(door => Get<string>(door, "id")), Is.EqualTo(ids));
            Assert.That(Get<int>(chapter, "Recovered"), Is.Zero);
        }
    }
}
