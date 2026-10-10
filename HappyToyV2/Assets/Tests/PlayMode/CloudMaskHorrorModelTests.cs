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
        [Test]
        public void ReferenceWraithKeepsOriginalNavigationWithEightPairedHumanArms()
        {
            var encounter = One("LanternMaskEncounter");
            var visual = encounter.GetComponent(RequireType("MaskHorrorVisual"));
            Assert.That(visual, Is.Not.Null);
            Assert.That(Get<bool>(visual, "Prepared"), Is.True);
            Assert.That(Get<int>(visual, "ArmPairCount"), Is.EqualTo(8));
            Assert.That(Get<int>(visual, "SwingPivotCount"), Is.EqualTo(16));
            var body = Get<Transform>(visual, "BodyModel");
            var mask = Get<Transform>(visual, "MaskModel");
            Assert.That(body.parent, Is.EqualTo(Get<Transform>(encounter, "body")));
            Assert.That(mask.parent, Is.EqualTo(Get<Transform>(encounter, "mask")));
            Assert.That(body.GetComponentsInChildren<Collider>(true), Is.Empty);
            Assert.That(mask.GetComponentsInChildren<Collider>(true), Is.Empty);
            Assert.That(body.GetComponentsInChildren<Rigidbody>(true), Is.Empty);
            Assert.That(encounter.transform.localScale, Is.EqualTo(Vector3.one));
            var agent = encounter.GetComponent<NavMeshAgent>();
            Assert.That(agent.radius, Is.EqualTo(.3f).Within(.001f));
            Assert.That(agent.height, Is.EqualTo(2.1f).Within(.001f));
            var bodyBounds = ModelLocalVertexBounds(body);
            var face = mask.GetComponentsInChildren<MeshFilter>(true).Single(node => node.name.StartsWith("Aged human smiling mask shell", StringComparison.Ordinal));
            var facePoints = face.sharedMesh.vertices.Select(vertex => mask.parent.InverseTransformPoint(face.transform.TransformPoint(vertex))).ToArray();
            var faceBounds = new Bounds(facePoints[0], Vector3.zero); foreach (var point in facePoints) faceBounds.Encapsulate(point);
            // These bounds are in the parent attachment pivots, using imported
            // actual vertices. They do not certify an arbitrary moving pose.
            Assert.That(bodyBounds.size.x, Is.GreaterThan(2.1f));
            Assert.That(bodyBounds.size.x, Is.LessThan(2.7f));
            Assert.That(bodyBounds.size.z, Is.InRange(3.5f, 4.25f), "Reference body lost its long low trailing silhouette");
            Assert.That(bodyBounds.size.y, Is.InRange(1.35f, 1.85f));
            Assert.That(bodyBounds.size.z / bodyBounds.size.y, Is.GreaterThan(2), "Reference remains an upright four-limb humanoid");
            Assert.That(faceBounds.size.y, Is.InRange(.70f, .80f), "Reference human face lost its intended physical size");
            var arms = body.GetComponentsInChildren<Transform>(true).Where(node => node.name.StartsWith("ArmSwing", StringComparison.Ordinal)).ToArray();
            Assert.That(arms.Length, Is.EqualTo(16));
            Assert.That(body.GetComponentsInChildren<MeshFilter>(true).Count(node => node.name.StartsWith("Human five-finger arm", StringComparison.Ordinal)), Is.EqualTo(16));
            Assert.That(Get<Transform>(visual, "HeadSocket").IsChildOf(body), Is.True);
            Assert.That(Get<Transform>(visual, "FaceJoint").IsChildOf(mask), Is.True);
            Assert.That(Get<Animation>(encounter, "motion").GetClip("run"), Is.Not.Null,
                "Original running rig clip must survive modeled visual attachment");
        }

        [Test]
        public void ReferenceHumanMaskHasNarrowBlackEyeAperturesSmilingMouthHairAndHollowBells()
        {
            var prefab = Resources.Load<GameObject>("MaskHorror/wraith-mask");
            Assert.That(prefab, Is.Not.Null);
            var shell = prefab.GetComponentsInChildren<MeshFilter>(true)
                .Single(item => item.name.StartsWith("Aged human smiling mask shell", StringComparison.Ordinal));
            Assert.That(shell.sharedMesh.isReadable, Is.True);
            var points = shell.sharedMesh.vertices.Select(p =>
                prefab.transform.InverseTransformPoint(shell.transform.TransformPoint(p))).ToArray();
            var indices = shell.sharedMesh.triangles;
            Assert.That(points.Length, Is.GreaterThan(3000));
            var eyeParts = prefab.GetComponentsInChildren<MeshFilter>(true).Where(item => item.name.StartsWith("Black eye slit", StringComparison.Ordinal)).ToArray();
            Assert.That(eyeParts.Length, Is.EqualTo(2));
            foreach (var eye in eyeParts)
            {
                var eyePoints = eye.sharedMesh.vertices.Select(vertex => prefab.transform.InverseTransformPoint(eye.transform.TransformPoint(vertex))).ToArray();
                var centre = eyePoints.Aggregate(Vector3.zero, (sum, vertex) => sum + vertex) / eyePoints.Length;
                float width = eyePoints.Max(vertex => vertex.x) - eyePoints.Min(vertex => vertex.x);
                float height = eyePoints.Max(vertex => vertex.y) - eyePoints.Min(vertex => vertex.y);
                Assert.That(width, Is.GreaterThan(height * 2.5f), "Human mask eye aperture became a large round orbit");
                Assert.That(ShellHits(points, indices, new Vector2(centre.x, centre.y)), Is.False,
                    "Black eye backing sits behind painted closed porcelain instead of a real narrow aperture");
            }
            var teeth = prefab.GetComponentsInChildren<MeshFilter>(true).Where(item => item.name.StartsWith("Yellowed human tooth", StringComparison.Ordinal)).ToArray();
            Assert.That(teeth.Length, Is.GreaterThanOrEqualTo(8), "Wide human smile lost its individually modeled teeth");
            var mouth = prefab.transform.InverseTransformPoint(ModelPartCenter(prefab.transform, "Yellowed human tooth"));
            Assert.That(ShellHits(points, indices, new Vector2(mouth.x, mouth.y)), Is.False, "Human smile filled with white shell triangles");
            var bounds = new Bounds(points[0], Vector3.zero); foreach (var point in points) bounds.Encapsulate(point);
            Assert.That(bounds.size.x / bounds.size.y, Is.InRange(.50f, .78f), "Reference's narrow human face became an alien broad plate");
            Assert.That(ShellHits(points, indices, new Vector2(bounds.center.x, bounds.max.y - bounds.size.y * .15f)), Is.True,
                "Actual solid human forehead shell is missing");
            var hair = prefab.GetComponentsInChildren<MeshFilter>(true).Where(item => item.name.StartsWith("Wet hair strands", StringComparison.Ordinal)).ToArray();
            var bells = prefab.GetComponentsInChildren<MeshFilter>(true).Where(item => item.name.StartsWith("Hollow black bells", StringComparison.Ordinal)).ToArray();
            Assert.That(hair.Length, Is.GreaterThan(0)); Assert.That(hair.Sum(item => item.sharedMesh.vertexCount), Is.GreaterThan(300), "Wet hair is a flat card instead of modeled strands");
            Assert.That(bells.Length, Is.GreaterThan(0)); Assert.That(bells.Sum(item => item.sharedMesh.vertexCount), Is.GreaterThan(100), "Black bell cluster has no authored three-dimensional anatomy");
        }

        [Test]
        public void ReferenceHumanNoseSmileAndLeadingNeckFollowTheActualNavigationHeading()
        {
            var encounter = One("LanternMaskEncounter");
            var visual = encounter.GetComponent(RequireType("MaskHorrorVisual"));
            var mask = Get<Transform>(visual, "MaskModel");
            var body = Get<Transform>(visual, "BodyModel");
            var forward = encounter.transform.forward;
            // Actual human nose/teeth geometry must project ahead of the black
            // eye slits and lead the low trailing body along its navigation heading.
            var eyes = ModelPartCenter(mask, "Black eye slit");
            var nose = mask.GetComponentsInChildren<Transform>(true).Single(node => node.name == "Human nose tip landmark").position;
            var teeth = ModelPartCenter(mask, "Yellowed human tooth");
            Assert.That(Vector3.Dot(nose - eyes, forward), Is.GreaterThan(.03f),
                "Nasal geometry points away from the actor's navigation heading");
            Assert.That(Vector3.Dot(teeth - eyes, forward), Is.GreaterThan(.015f),
                "Teeth are behind the eyes from the player-facing front");
            var shell = mask.GetComponentsInChildren<MeshFilter>(true).Single(node => node.name.StartsWith("Aged human smiling mask shell", StringComparison.Ordinal));
            Assert.That(shell.sharedMesh.vertices.Min(vertex => Vector3.Distance(shell.transform.TransformPoint(vertex), nose)), Is.LessThan(.015f),
                "Nose landmark floats ahead of missing actual human nasal geometry");
            Assert.That(Vector3.Dot(Get<Vector3>(visual, "FaceForward"), forward), Is.GreaterThan(.8f));
            var bodyBounds = ModelLocalVertexBounds(body);
            var bodyCentre = body.parent.TransformPoint(bodyBounds.center);
            Assert.That(Vector3.Dot(Get<Transform>(visual, "HeadSocket").position - bodyCentre, forward), Is.GreaterThan(.6f),
                "Human neck is attached above the centre of the long back instead of its leading end");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator ReferenceManyHumanArmsMoveWithActualNavigationAndFreezeOnPause()
        {
            yield return PrepareCorridorMaskRunner();
            var actor = Get<Component>(Get<Component>(session, "Corridor"), "Mask");
            var visual = actor.GetComponent(RequireType("MaskHorrorVisual"));
            var model = Get<Transform>(visual, "BodyModel");
            var arms = model.GetComponentsInChildren<Transform>(true).Where(node => node.name.StartsWith("ArmSwing", StringComparison.Ordinal)).ToArray();
            Assert.That(arms.Length, Is.EqualTo(16));
            ((Behaviour)player).enabled = false; PlacePlayer(new Vector3(200, 5, 200), false);
            var start = actor.transform.position; float gait = Get<float>(visual, "GaitPhase");
            var initial = arms.Select(arm => arm.localRotation).ToArray();
            var initialPositions = arms.Select(arm => arm.localPosition).ToArray();
            yield return Wait(() => Vector3.Distance(start, actor.transform.position) > .1f && Mathf.Abs(Get<float>(visual, "GaitPhase") - gait) > .1f,
                5, "Actual running agent never drove reference many-arm movement");
            Assert.That(arms.Where((arm, index) => Quaternion.Angle(arm.localRotation, initial[index]) > .05f ||
                Vector3.Distance(arm.localPosition, initialPositions[index]) > .001f).Count(), Is.GreaterThanOrEqualTo(8),
                "Creature translates while its many human limbs stay rigid");
            var beforePause = arms.Select(arm => arm.localRotation).ToArray(); gait = Get<float>(visual, "GaitPhase");
            var positionsBeforePause = arms.Select(arm => arm.localPosition).ToArray();
            Call(shell, "Pause"); yield return Delay(.25f);
            Assert.That(Get<float>(visual, "GaitPhase"), Is.EqualTo(gait));
            for (int i = 0; i < arms.Length; i++)
            {
                Assert.That(Quaternion.Angle(arms[i].localRotation, beforePause[i]), Is.LessThan(.001f));
                Assert.That(Vector3.Distance(arms[i].localPosition, positionsBeforePause[i]), Is.LessThan(.00001f));
            }
            Assert.That(actor.transform.localScale, Is.EqualTo(Vector3.one));
            Assert.That(actor.GetComponent<NavMeshAgent>().radius, Is.EqualTo(.3f).Within(.001f));
        }

        [UnityTest, Timeout(90000)]
        public IEnumerator ReferenceTrailingBodyFollowsRealCornerWithoutRotatingHandsThroughTheFloor()
        {
            yield return PrepareCorridorMaskRunner();
            var run = Get<Component>(session, "Corridor"); var actor = Get<Component>(run, "Mask");
            var agent = actor.GetComponent<NavMeshAgent>(); var visual = actor.GetComponent(RequireType("MaskHorrorVisual"));
            var model = Get<Transform>(visual, "BodyModel");
            var segments = model.GetComponentsInChildren<Transform>(true)
                .Where(node => node.name.StartsWith("Segment", StringComparison.Ordinal) && node.name.Length == 9).OrderBy(node => node.name).ToArray();
            var contacts = model.GetComponentsInChildren<Transform>(true).Where(node => node.name.StartsWith("HandContact", StringComparison.Ordinal)).ToArray();
            Assert.That(segments.Length, Is.EqualTo(8)); Assert.That(contacts.Length, Is.EqualTo(16));
            var layout = Get<object>(run, "Layout"); var links = Get<int[]>(layout, "Connections");
            var route = new NavMeshPath(); Vector3 start = default, end = default, incoming = default; bool found = false;
            int topologicalCandidates = 0, supportedPairs = 0, completePaths = 0; float greatestPlannedBend = 0;
            int[] dx = { 0, 1, 0, -1 }, dz = { 1, 0, -1, 0 };
            for (int cell = 0; cell < links.Length && !found; cell++)
                for (int direction = 0; direction < 4 && !found; direction++)
                    foreach (int turn in new[] { 1, 3 })
                    {
                        int outbound = (direction + turn) % 4, entrance = (direction + 2) % 4;
                        if ((links[cell] & (1 << entrance)) == 0 || (links[cell] & (1 << outbound)) == 0) continue;
                        topologicalCandidates++;
                        int from = cell + dx[entrance] + dz[entrance] * 9, to = cell + dx[outbound] + dz[outbound] * 9;
                        // Room corners may round one right angle into several
                        // shallow NavMesh edges. Extend through actual connected
                        // straight cells so the independent trail fills before
                        // the bend instead of demanding one sharp corner vertex.
                        for (int extension = 0; extension < 2 && (links[from] & (1 << entrance)) != 0; extension++)
                            from += dx[entrance] + dz[entrance] * 9;
                        for (int extension = 0; extension < 2 && (links[to] & (1 << outbound)) != 0; extension++)
                            to += dx[outbound] + dz[outbound] * 9;
                        if (!NavMesh.SamplePosition((Vector3)Call(run, "CellPosition", from), out var first, .8f, NavMesh.AllAreas) ||
                            !NavMesh.SamplePosition((Vector3)Call(run, "CellPosition", to), out var last, .8f, NavMesh.AllAreas)) continue;
                        supportedPairs++;
                        if (!NavMesh.CalculatePath(first.position, last.position, new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask }, route) ||
                            route.status != NavMeshPathStatus.PathComplete || route.corners.Length < 3) continue;
                        completePaths++;
                        var firstHeading = (route.corners[1] - route.corners[0]).normalized;
                        float length = 0, total = 0; bool boundedCorner = false;
                        for (int i = 1; i < route.corners.Length; i++) total += Vector3.Distance(route.corners[i - 1], route.corners[i]);
                        for (int i = 1; i < route.corners.Length - 1; i++)
                        {
                            length += Vector3.Distance(route.corners[i - 1], route.corners[i]);
                            float bend = Vector3.Angle(firstHeading, route.corners[i + 1] - route.corners[i]);
                            greatestPlannedBend = Mathf.Max(greatestPlannedBend, bend);
                            if (length >= 4 && bend >= 65 && total - length >= 3) boundedCorner = true;
                        }
                        if (!boundedCorner) continue;
                        start = first.position; end = last.position; incoming = firstHeading; found = true; break;
                    }
            Assert.That(found, Is.True, "Actual seeded corridor has no supported complete long-entry cumulative corner fixture; topology=" +
                topologicalCandidates + " supported=" + supportedPairs + " complete=" + completePaths + " greatest bend=" + greatestPlannedBend);
            var destination = new GameObject("CloudQA actual reference corner patrol target"); destination.transform.position = end;
            CloudReferenceLatePoseRecorder recorder = null;
            try
            {
                ((Behaviour)actor).enabled = false; ((Behaviour)player).enabled = false; PlacePlayer(new Vector3(200, 5, 200), false);
                Set(actor, "patrol", new[] { destination.transform, destination.transform });
                var state = CopyAdvancedProgress(Call(actor, "CaptureChapterProgress"));
                Set(state, "position", start); Set(state, "rotation", Quaternion.LookRotation(incoming));
                Set(state, "target", end); Set(state, "waypoint", 0); Set(state, "state", "Wander"); Set(state, "memory", 0f); Set(state, "doorPassage", null);
                Call(actor, "RestoreCorridorProgress", state); ((Behaviour)actor).enabled = true;
                // Initial fixture placement ends here. The independent trail is
                // sampled only from subsequent real agent motion, without warps.
                var actualPath = new List<Vector3> { actor.transform.position }; float travelled = 0, maximumBend = 0; bool observed = false;
                var poses = new Queue<ReferenceLatePose>(); var lastRecordedPosition = actor.transform.position; float recordedTravel = 0;
                int attachmentBefore = Get<int>(actor, "AttachmentSamples");
                recorder = new GameObject("CloudQA reference actual late-pose recorder").AddComponent<CloudReferenceLatePoseRecorder>();
                recorder.Sample = () =>
                {
                    // This callback runs after production Update/LateUpdate.
                    // Store one atomic immutable numeric snapshot; the coroutine
                    // never rereads a later Visual-reset attachment pose.
                    var pose = new ReferenceLatePose {
                        position = actor.transform.position, forward = actor.transform.forward,
                        head = segments[0].position, tail = segments[7].position,
                        faceForward = Get<Vector3>(visual, "FaceForward"),
                        jointError = Vector3.Distance(Get<Transform>(visual, "HeadSocket").position, Get<Transform>(visual, "FaceJoint").position),
                        contactY = contacts.Min(hand => hand.position.y), floor = actor.transform.position.y - agent.baseOffset,
                        speedSquared = agent.velocity.sqrMagnitude, deltaTime = Time.deltaTime,
                        attachments = Get<int>(actor, "AttachmentSamples")
                    };
                    recordedTravel += Vector3.Distance(lastRecordedPosition, pose.position); lastRecordedPosition = pose.position;
                    var rigidTail = pose.position - pose.forward * Vector3.Distance(pose.head, pose.tail);
                    pose.hasGeometry = recordedTravel >= 4 && pose.speedSquared >= .25f &&
                        Vector3.Angle(incoming, pose.forward) >= 65 && HorizontalDistance(pose.tail, rigidTail) >= .35f;
                    if (pose.hasGeometry) pose.geometry = ModelWorldVertexBounds(model);
                    poses.Enqueue(pose);
                };
                float deadline = Time.realtimeSinceStartup + 12;
                while (Time.realtimeSinceStartup < deadline && !observed && recorder.Error == null)
                {
                    yield return null;
                    while (poses.Count > 0 && !observed)
                    {
                        var pose = poses.Dequeue(); var previous = actualPath[actualPath.Count - 1];
                        float step = Vector3.Distance(previous, pose.position);
                        Assert.That(step, Is.LessThan(7.2f * Mathf.Max(pose.deltaTime, .016f) + .15f), "Creature warped across the real corner");
                        if (step > .0001f) { actualPath.Add(pose.position); travelled += step; }
                        if (travelled < 4 || pose.speedSquared < .25f) continue;
                        float bend = Vector3.Angle(incoming, pose.forward); maximumBend = Mathf.Max(maximumBend, bend);
                        if (bend < 65) continue;
                        var rigidTail = pose.position - pose.forward * Vector3.Distance(pose.head, pose.tail);
                        if (HorizontalDistance(pose.tail, rigidTail) < .35f) continue;
                        Assert.That(pose.attachments, Is.GreaterThan(attachmentBefore), "Reference snapshot preceded production attachment");
                        Assert.That(pose.hasGeometry, Is.True, "Eligible moving pose lacks same-frame actual skin vertices");
                        Assert.That(HorizontalDistance(pose.head, pose.position), Is.LessThan(.25f), "Leading human neck left its original navigation root");
                        Assert.That(HorizontalPathDistance(pose.tail, actualPath), Is.LessThan(.35f), "Rear body projects through the corner instead of following the actual traveled hall");
                        // At the instant of a right-angle turn the old hallway can
                        // be perpendicular to the new heading. Require no lead;
                        // the independent real polyline proves the actual trail.
                        Assert.That(Vector3.Dot(pose.tail - pose.position, pose.forward), Is.LessThan(.1f), "Trailing body moved ahead of its leading face");
                        Assert.That(Vector3.Dot(pose.faceForward, pose.forward), Is.GreaterThan(.8f), "Human face points backward after an actual turn");
                        Assert.That(pose.jointError, Is.LessThan(.003f));
                        Assert.That(pose.contactY - pose.floor, Is.GreaterThanOrEqualTo(-.025f), "Actual moving human hand contacts sink through the floor");
                        Assert.That(pose.geometry.min.y - pose.floor, Is.GreaterThanOrEqualTo(-.03f), "Actual moving skin vertices rotated underneath the corridor floor");
                        Assert.That(pose.geometry.max.y - pose.floor, Is.LessThan(2.44f), "Moving segmented body exceeds the real corridor lintel");
                        observed = true;
                        Debug.Log("HAPPYTOY_REFERENCE_REAL_CORNER_PASS distance=" + travelled + " bend=" + bend + " tail-path-error=" + HorizontalPathDistance(pose.tail, actualPath));
                    }
                }
                Assert.That(recorder.Error, Is.Null, "Actual post-LateUpdate recorder failed: " + recorder.Error);
                Assert.That(observed, Is.True, "Actual native patrol never demonstrated its long body following a real corner; travelled=" + travelled + " max heading change=" + maximumBend);
                Assert.That(actor.transform.localScale, Is.EqualTo(Vector3.one)); Assert.That(agent.radius, Is.EqualTo(.3f).Within(.001f));
            }
            finally
            {
                if (recorder) { recorder.Sample = null; recorder.enabled = false; UnityEngine.Object.Destroy(recorder.gameObject); }
                UnityEngine.Object.Destroy(destination);
            }
        }

        struct ReferenceLatePose
        {
            public Vector3 position, forward, head, tail, faceForward;
            public float jointError, contactY, floor, speedSquared, deltaTime;
            public int attachments;
            public bool hasGeometry;
            public Bounds geometry;
        }

        static float HorizontalDistance(Vector3 a, Vector3 b) { a.y = b.y = 0; return Vector3.Distance(a, b); }
        static float HorizontalPathDistance(Vector3 point, List<Vector3> path)
        {
            float nearest = float.PositiveInfinity; point.y = 0;
            for (int i = 1; i < path.Count; i++)
            {
                var a = path[i - 1]; var b = path[i]; a.y = b.y = 0; var delta = b - a;
                var projection = a + delta * Mathf.Clamp01(Vector3.Dot(point - a, delta) / Mathf.Max(delta.sqrMagnitude, .000001f));
                nearest = Mathf.Min(nearest, Vector3.Distance(point, projection));
            }
            return nearest;
        }
        static Bounds ModelWorldVertexBounds(Transform model)
        {
            bool found = false; Bounds result = default;
            foreach (var filter in model.GetComponentsInChildren<MeshFilter>(true))
                foreach (var vertex in filter.sharedMesh.vertices)
                {
                    var point = filter.transform.TransformPoint(vertex);
                    if (!found) { result = new Bounds(point, Vector3.zero); found = true; } else result.Encapsulate(point);
                }
            Assert.That(found, Is.True); return result;
        }

        static Vector3 ModelPartCenter(Transform model, string prefix)
        {
            Vector3 sum = Vector3.zero; int count = 0;
            foreach (var filter in model.GetComponentsInChildren<MeshFilter>(true))
                if (filter.name.StartsWith(prefix, StringComparison.Ordinal))
                    foreach (var vertex in filter.sharedMesh.vertices)
                    { sum += filter.transform.TransformPoint(vertex); count++; }
            Assert.That(count, Is.GreaterThan(0), "Authored anatomy landmark missing: " + prefix);
            return sum / count;
        }

        static Bounds ModelLocalVertexBounds(Transform model)
        {
            bool found = false; Bounds result = default;
            foreach (var filter in model.GetComponentsInChildren<MeshFilter>(true))
            {
                Assert.That(filter.sharedMesh && filter.sharedMesh.isReadable, Is.True);
                foreach (var vertex in filter.sharedMesh.vertices)
                {
                    var point = model.parent.InverseTransformPoint(filter.transform.TransformPoint(vertex));
                    if (!found) { result = new Bounds(point, Vector3.zero); found = true; }
                    else result.Encapsulate(point);
                }
            }
            Assert.That(found, Is.True); return result;
        }

        static bool ShellHits(Vector3[] vertices, int[] triangles, Vector2 point)
        {
            var origin = new Vector3(point.x, point.y, -1); var direction = Vector3.forward;
            for (int i = 0; i < triangles.Length; i += 3)
            {
                Vector3 a = vertices[triangles[i]], edgeA = vertices[triangles[i+1]] - a,
                    edgeB = vertices[triangles[i+2]] - a;
                var h = Vector3.Cross(direction, edgeB); float determinant = Vector3.Dot(edgeA, h);
                if (Mathf.Abs(determinant) < .000001f) continue;
                float inverse = 1 / determinant; var s = origin - a;
                float u = inverse * Vector3.Dot(s, h); if (u < 0 || u > 1) continue;
                var q = Vector3.Cross(s, edgeA); float v = inverse * Vector3.Dot(direction, q);
                if (v < 0 || u + v > 1) continue;
                if (inverse * Vector3.Dot(edgeB, q) >= 0) return true;
            }
            return false;
        }
    }

    [DefaultExecutionOrder(32000)]
    public sealed class CloudReferenceLatePoseRecorder : MonoBehaviour
    {
        public Action Sample;
        public Exception Error { get; private set; }
        void LateUpdate()
        {
            if (Sample == null || Error != null) return;
            try { Sample(); }
            catch (Exception error) { Error = error; enabled = false; }
        }
    }
}
