using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace HappyToy.V2
{
    // Explicit opt-in inspection. This never runs in ordinary player sessions.
    // Views use the actual player camera/torch. Doors are opened physically and
    // the player is placed on validated floor points for controlled art review.
    // Planned complete paths are not a claim of a walked survival playthrough.
    public sealed class SchoolCampusAudit : MonoBehaviour
    {
        string output;
        GameSession session;
        MemoryChapter chapter;
        PlayerMotor player;
        SchoolCampusLayout campus;
        readonly List<string> errors = new List<string>();
        readonly List<RoomEvidence> rooms = new List<RoomEvidence>();
        readonly List<DoorEvidence> doors = new List<DoorEvidence>();
        [Serializable] sealed class RoomEvidence
        {
            public string id, label, image;
            public int floor;
            public Rect rect;
            public Vector3 feet, camera, target;
            public bool standingClear, forwardRoute, returnRoute, renderedContent;
        }
        [Serializable] sealed class DoorEvidence
        {
            public string id;
            public Vector3 a, b;
            public bool openPose, floorA, floorB, standingA, standingB, completeRoute, clearCapsule;
            public string[] blockers;
        }
        [Serializable] sealed class Report
        {
            public string status;
            public string scope = "Controlled native school-campus v2 structure and art inspection. Actual production player camera/torch, physical open doors, supported standing locations and complete planned room/door/return routes. Player placement is explicit; no AI, full-input survival, listening quality or target-hardware performance claim.";
            public bool developmentBuild;
            public int layoutVersion, groundRooms, upperRooms, basementRooms, currentDoors, generatedDoors, colliderCount;
            public float cameraFov, torchIntensity, torchRange;
            public RoomEvidence[] rooms;
            public DoorEvidence[] doors;
            public string[] errors;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            var args = Environment.GetCommandLineArgs(); int index = Array.IndexOf(args, "-v2-school-campus-output");
            if (index < 0 || index + 1 >= args.Length) return;
            var audit = new GameObject("Controlled native school campus inspection").AddComponent<SchoolCampusAudit>();
            audit.output = Path.GetFullPath(args[index + 1]);
        }
        void OnEnable() { Application.logMessageReceived += ObserveLog; }
        void OnDisable() { Application.logMessageReceived -= ObserveLog; }
        void ObserveLog(string message, string trace, LogType type)
        { if ((type == LogType.Error || type == LogType.Exception || type == LogType.Assert) && errors.Count < 64) errors.Add(message); }
        static void Require(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); }
        IEnumerator Start()
        {
            Directory.CreateDirectory(output);
            var pending = new Stack<IEnumerator>(); pending.Push(Run());
            try
            {
                while (pending.Count > 0 && errors.Count == 0)
                {
                    var routine = pending.Peek(); bool more; object current;
                    try { more = routine.MoveNext(); current = more ? routine.Current : null; }
                    catch (Exception error) { errors.Add(error.ToString()); break; }
                    if (!more) { pending.Pop(); (routine as IDisposable)?.Dispose(); continue; }
                    if (current is IEnumerator nested) { pending.Push(nested); continue; }
                    yield return current;
                }
            }
            finally
            {
                while (pending.Count > 0) (pending.Pop() as IDisposable)?.Dispose();
                WriteReport(); Debug.Log("HAPPYTOY_NATIVE_SCHOOL_CAMPUS_" + (errors.Count == 0 ? "PASS" : "FAIL"));
                Application.Quit(errors.Count == 0 ? 0 : 2);
            }
        }
        IEnumerator Run()
        {
            yield return null; yield return null;
            session = GameSession.Current;
            Require(session && session.player && session.Shell, "Missing real school session/player/shell");
            Require(session.Shell.Screen == GameShell.Page.Title, "Campus audit must retain title before explicit chapter entry");
            // Isolated profile protects the player's suspended runs and records.
            session.ConfigureRecordDirectory(Path.Combine(output, "audit-profile"));
            session.Shell.BeginChapter(); yield return null; yield return null;
            chapter = session.Chapter; player = session.player; campus = chapter ? chapter.Campus : null;
            Require(chapter && chapter.Ready && chapter.LayoutVersion == 2 && campus && campus.Root, "Version-two campus did not prepare");
            Require(chapter.Recovered == 0, "Structure inspection must not bypass memory/reveal gates");
            foreach (int floor in new[] { 0, 1, -1 }) Require(campus.RoomCount(floor) >= 5, "Too few distinct rooms on floor " + floor);
            var currentDoors = CurrentDoors(); Require(campus.Doors.Count >= 24, "Missing real campus room doors");
            foreach (var door in currentDoors) if (!door.IsOpen) Require(door.OpenForPursuer(), "Cannot request physical opening: " + door.name);
            float until = Time.realtimeSinceStartup + 6;
            while (!currentDoors.All(door => door.AtRequestedDoorPose) && Time.realtimeSinceStartup < until) yield return null;
            Require(currentDoors.All(door => door.AtRequestedDoorPose), "Current doors did not reach actual open geometry");
            Physics.SyncTransforms(); yield return null; yield return null;
            player.enabled = false; player.GetComponent<CharacterController>().enabled = false;
            if (player.flashlight) player.flashlight.enabled = true;
            Vector3 entrance = Floor(new Vector3(-6.4f, .03f, -.5f));
            foreach (var door in campus.Doors) InspectDoor(door);
            foreach (var room in campus.Spaces.Where(space => space.room).OrderBy(space => space.floor).ThenBy(space => space.id, StringComparer.Ordinal))
            {
                var evidence = new RoomEvidence { id = room.id, label = room.label, floor = room.floor, rect = room.rect };
                rooms.Add(evidence);
                Vector3 feet = Viewpoint(room); evidence.feet = feet; evidence.standingClear = StandingClear(feet);
                evidence.forwardRoute = CompleteRoute(entrance, feet); evidence.returnRoute = CompleteRoute(feet, entrance);
                Require(evidence.standingClear && evidence.forwardRoute && evidence.returnRoute, "Room has no usable connected inspection point: " + room.id);
                SetView(feet, room.Centre + Vector3.up * 1.2f);
                yield return null; yield return null;
                evidence.camera = player.eyes.transform.position; evidence.target = room.Centre + Vector3.up * 1.2f;
                evidence.image = "campus-room-" + room.id + ".png";
                evidence.renderedContent = Capture(evidence.image);
                Require(evidence.renderedContent, "Native room image is blank/flat: " + room.id);
            }
            foreach (var point in Enumerable.Range(2, 3).Select(MemoryApproach))
            {
                Vector3 floor = Floor(point);
                Require(StandingClear(floor) && CompleteRoute(entrance, floor) && CompleteRoute(floor, entrance), "Memory approach/return blocked at " + point);
            }
            Require(chapter.Recovered == 0, "Inspection unexpectedly collected a memory");
            Require(errors.Count == 0, "Native runtime logged an error");
        }
        Interactable[] CurrentDoors() => gameObject.scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<Interactable>(true))
            .Where(door => door.gameObject.activeInHierarchy && door.kind == Interactable.Kind.Door && door.movingLeaf).ToArray();
        Vector3 MemoryApproach(int index)
        {
            Vector3 point = chapter.Memories[index].transform.position;
            point += index == 4 ? Vector3.forward : Vector3.back * (index == 2 ? .85f : 1.02f);
            point.y = (index == 4 ? -5 : 5) + .03f;
            return point;
        }
        static Vector3 Floor(Vector3 requested)
        {
            Require(NavMesh.SamplePosition(requested, out var hit, .45f, NavMesh.AllAreas), "No native floor at " + requested);
            Require(Mathf.Abs(hit.position.y - requested.y) < .3f, "Native floor sample switched storeys: " + requested);
            return hit.position;
        }
        static bool CompleteRoute(Vector3 from, Vector3 to)
        {
            var path = new NavMeshPath(); return NavMesh.CalculatePath(from, to, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete;
        }
        bool StandingClear(Vector3 point)
        {
            var pose = player.CaptureProgress(); pose.position = point; pose.crouched = false;
            return player.CanRestoreChapterProgress(pose);
        }
        static bool Boundary(Rect room, Vector3 point)
        {
            const float tolerance = .35f;
            return point.x >= room.xMin - tolerance && point.x <= room.xMax + tolerance &&
                point.z >= room.yMin - tolerance && point.z <= room.yMax + tolerance &&
                (Mathf.Abs(point.x - room.xMin) < tolerance || Mathf.Abs(point.x - room.xMax) < tolerance ||
                    Mathf.Abs(point.z - room.yMin) < tolerance || Mathf.Abs(point.z - room.yMax) < tolerance);
        }
        Vector3 Viewpoint(SchoolCampusLayout.Space room)
        {
            foreach (var door in campus.Doors.Where(door => Mathf.Abs(door.transform.position.y - room.Height) < .3f && Boundary(room.rect, door.transform.position)))
                foreach (float side in new[] { 1f, -1f })
                {
                    Vector3 point = door.transform.position + door.DoorNormal * side * 1.25f; point.y = room.Height + .03f;
                    if (!room.rect.Contains(new Vector2(point.x, point.z)) || !NavMesh.SamplePosition(point, out var floor, .4f, NavMesh.AllAreas) ||
                        Mathf.Abs(floor.position.y - room.Height) > .3f || !StandingClear(floor.position)) continue;
                    return floor.position;
                }
            foreach (float x in new[] { .25f, .75f }) foreach (float z in new[] { .25f, .75f })
            {
                Vector3 point = new Vector3(Mathf.Lerp(room.rect.xMin, room.rect.xMax, x), room.Height + .03f, Mathf.Lerp(room.rect.yMin, room.rect.yMax, z));
                if (NavMesh.SamplePosition(point, out var floor, .45f, NavMesh.AllAreas) && Mathf.Abs(floor.position.y - room.Height) < .3f && StandingClear(floor.position)) return floor.position;
            }
            throw new InvalidOperationException("No supported native inspection view in " + room.id);
        }
        void InspectDoor(Interactable door)
        {
            var evidence = new DoorEvidence { id = door.stableId, openPose = door.AtRequestedDoorPose }; doors.Add(evidence);
            Vector3 center = door.transform.position + Vector3.up * .03f;
            evidence.a = Floor(center - door.DoorNormal * 1.15f); evidence.floorA = true;
            evidence.b = Floor(center + door.DoorNormal * 1.15f); evidence.floorB = true;
            evidence.standingA = StandingClear(evidence.a); evidence.standingB = StandingClear(evidence.b);
            evidence.completeRoute = CompleteRoute(evidence.a, evidence.b);
            var controller = player.GetComponent<CharacterController>(); float radius = controller.radius - .02f;
            float half = Mathf.Max(0, controller.height * .5f - controller.radius);
            Vector3 body = evidence.a + controller.center, delta = evidence.b - evidence.a;
            evidence.blockers = Physics.CapsuleCastAll(body - Vector3.up * half, body + Vector3.up * half, radius,
                delta.normalized, delta.magnitude, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                .Where(hit => !hit.collider.transform.IsChildOf(player.transform) && !hit.collider.GetComponentInParent<StalkerBrain>())
                .Select(hit => hit.collider.name).ToArray();
            evidence.clearCapsule = evidence.blockers.Length == 0;
            Require(evidence.openPose && evidence.standingA && evidence.standingB && evidence.completeRoute && evidence.clearCapsule,
                "Native door approaches/crossing blocked: " + door.stableId + " " + string.Join(",", evidence.blockers));
        }
        void SetView(Vector3 feet, Vector3 target)
        {
            var controller = player.GetComponent<CharacterController>(); controller.enabled = false;
            player.transform.position = feet;
            Vector3 facing = target - feet; facing.y = 0; player.transform.rotation = Quaternion.LookRotation(facing);
            player.eyes.transform.localPosition = Vector3.up * 1.65f;
            player.eyes.transform.rotation = Quaternion.LookRotation(target - player.eyes.transform.position);
            Physics.SyncTransforms();
        }
        bool Capture(string file)
        {
            var target = new RenderTexture(1280, 720, 24); target.Create();
            var camera = player.eyes; var original = camera.targetTexture; var active = RenderTexture.active;
            Texture2D image = null;
            try
            {
                camera.targetTexture = target;
                RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
                RenderTexture.active = target;
                image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); image.Apply();
                File.WriteAllBytes(Path.Combine(output, file), image.EncodeToPNG());
                float minimum = 1, maximum = 0; int visible = 0;
                for (int y = 0; y < 720; y += 9) for (int x = 0; x < 1280; x += 9)
                {
                    var color = image.GetPixel(x, y); float value = Mathf.Max(color.r, color.g, color.b);
                    minimum = Mathf.Min(minimum, value); maximum = Mathf.Max(maximum, value); if (value > .08f) visible++;
                }
                return maximum - minimum > .06f && visible > 30;
            }
            finally
            {
                camera.targetTexture = original; RenderTexture.active = active; target.Release(); Destroy(target);
                if (image) Destroy(image);
            }
        }
        void WriteReport()
        {
            var report = new Report { status = errors.Count == 0 ? "passed" : "failed", developmentBuild = Debug.isDebugBuild,
                layoutVersion = chapter ? chapter.LayoutVersion : 0, groundRooms = campus ? campus.RoomCount(0) : 0,
                upperRooms = campus ? campus.RoomCount(1) : 0, basementRooms = campus ? campus.RoomCount(-1) : 0,
                currentDoors = session ? CurrentDoors().Length : 0, generatedDoors = campus ? campus.Doors.Count : 0,
                colliderCount = campus && campus.Root ? campus.Root.GetComponentsInChildren<Collider>().Length : 0,
                cameraFov = player && player.eyes ? player.eyes.fieldOfView : 0,
                torchIntensity = player && player.flashlight ? player.flashlight.intensity : 0,
                torchRange = player && player.flashlight ? player.flashlight.range : 0,
                rooms = rooms.ToArray(), doors = doors.ToArray(), errors = errors.ToArray() };
            File.WriteAllText(Path.Combine(output, "school-campus-report.json"), JsonUtility.ToJson(report, true));
        }
    }
}
