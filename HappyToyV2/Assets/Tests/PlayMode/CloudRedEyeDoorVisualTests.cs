using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using static HappyToy.V2.CloudTests.RuntimeAccess;

namespace HappyToy.V2.CloudTests
{
    public sealed partial class CloudPlayModeTests
    {
        [UnityTest, Timeout(60000)]
        public IEnumerator EveryHostileFaceHasTwoOpaqueHeadBoundRedCoresWithoutAdditionalLightsOrPhysics()
        {
            // Baby's authored actor can start inactive: Awake refinement is
            // deferred until activation. Isolate then activate source actors,
            // letting production initialization create their real eye pair.
            foreach (var motion in Components("V1MonsterMotion"))
            {
                var brain = motion.GetComponent(RequireType("StalkerBrain")) as Behaviour;
                if (!brain) continue;
                brain.enabled = false;
                var agent = motion.GetComponent<NavMeshAgent>(); if (agent) agent.enabled = false;
                motion.gameObject.SetActive(true);
            }
            yield return null;
            var eyes = Components("MonsterRedEyes");
            foreach (string key in new[] { "Cyclopse", "Uncat", "Hwacat_angry", "Baby", "LanternMask", "Mannequin" })
                Assert.That(eyes.Any(item => Get<string>(item, "ProfileKey") == key), Is.True, "Missing paired red eye face: " + key);
            var template = Resources.Load<Material>("GraphicsPbr/paper-aged/material-emissive");
            Assert.That(template, Is.Not.Null);
            foreach (var eye in eyes)
            {
                Assert.That(Get<bool>(eye, "Prepared"), Is.True);
                var head = Get<Transform>(eye, "Head");
                var anchors = Get<IReadOnlyList<Transform>>(eye, "EyeAnchors");
                var cores = Get<IReadOnlyList<MeshRenderer>>(eye, "Cores");
                Assert.That(anchors.Count, Is.EqualTo(2)); Assert.That(cores.Count, Is.EqualTo(2));
                Assert.That(eye.GetComponentsInChildren<Light>(true), Is.Empty, "Eyes added real illumination");
                Assert.That(eye.GetComponentsInChildren<Collider>(true), Is.Empty, "Eyes altered actor physics");
                Assert.That(eye.GetComponentsInChildren<NavMeshObstacle>(true), Is.Empty);
                Assert.That(Vector3.Distance(anchors[0].position, anchors[1].position), Is.GreaterThan(.025f));
                foreach (var core in cores)
                {
                    var material = core.sharedMaterial;
                    Assert.That(material.shader, Is.SameAs(template.shader));
                    Assert.That(material.shaderKeywords.OrderBy(value => value), Is.EqualTo(template.shaderKeywords.OrderBy(value => value)),
                        "Eye material enabled a stripped release-build shader combination");
                    Assert.That(material.GetColor("_EmissionColor").r, Is.GreaterThan(5));
                    Assert.That(material.GetColor("_EmissionColor").g, Is.LessThan(.1f));
                    Assert.That(material.GetFloat("_Surface"), Is.Zero, "Core must write ordinary opaque depth");
                    Assert.That(material.GetFloat("_Cull"), Is.EqualTo(2), "Front-only cores must cull their back faces");
                    Assert.That(core.shadowCastingMode, Is.EqualTo(ShadowCastingMode.Off));
                }
                var localPoints = anchors.Select(anchor => head.InverseTransformPoint(anchor.position)).ToArray();
                var initial = head.localRotation;
                head.localRotation = initial * Quaternion.Euler(0, 22, -13);
                for (int index = 0; index < anchors.Count; index++)
                {
                    Assert.That(anchors[index].IsChildOf(head), Is.True);
                    Assert.That(Vector3.Distance(anchors[index].position, head.TransformPoint(localPoints[index])), Is.LessThan(.00001f),
                        "A red core did not follow the animated face transform");
                }
                head.localRotation = initial;
            }
            yield return null;
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator RaisedDoorPullsStayOnBothMovingFacesDuringOpeningAndCheckpointPoseRestore()
        {
            Call(session, "CreateCorridor", 73); IsolateThreats(); Begin(); yield return null; yield return null;
            var run = Get<Component>(session, "Corridor");
            var doors = run.GetComponentsInChildren(RequireType("Interactable"), true).Cast<Component>()
                .Where(item => Get<object>(item, "kind").ToString() == "Door" && Get<Transform>(item, "movingLeaf")).ToArray();
            Assert.That(doors, Is.Not.Empty);
            var navigation = HauntedNavigationHash();
            foreach (var door in doors.Take(3))
            {
                var leaf = Get<Transform>(door, "movingLeaf");
                var hardware = leaf.GetComponentInChildren(RequireType("CorridorDoorHardware"), true);
                Assert.That(hardware, Is.Not.Null); Assert.That(Get<bool>(hardware, "Prepared"), Is.True);
                Assert.That(Get<Component>(hardware, "Owner"), Is.SameAs(door));
                Assert.That(Get<Transform>(hardware, "Leaf"), Is.SameAs(leaf));
                var faces = Get<IReadOnlyList<Transform>>(hardware, "FaceRoots");
                Assert.That(faces.Count, Is.EqualTo(2));
                Assert.That(hardware.GetComponentsInChildren<Collider>(true), Is.Empty);
                Assert.That(hardware.GetComponentsInChildren<NavMeshObstacle>(true), Is.Empty);
                Assert.That(hardware.GetComponentsInChildren(RequireType("Interactable"), true), Is.Empty);
                Assert.That(faces[0].localPosition.z, Is.LessThan(0)); Assert.That(faces[1].localPosition.z, Is.GreaterThan(0));
                Assert.That(Vector3.Dot(faces[0].forward, faces[1].forward), Is.LessThan(-.99f));
                foreach (var face in faces)
                {
                    var renderers = face.GetComponentsInChildren<MeshRenderer>(true);
                    Assert.That(renderers.Length, Is.GreaterThan(1));
                    var bounds = renderers[0].bounds; foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
                    Assert.That(bounds.size.y, Is.GreaterThan(.28f), "Pull shrank into the leaf texture");
                }
                var closed = leaf.localPosition; var scale = leaf.localScale; var rotation = leaf.localRotation;
                var anchors = faces.Select(face => leaf.InverseTransformPoint(face.position)).ToArray();
                var stableId = Get<string>(door, "stableId"); var colliders = leaf.GetComponentsInChildren<Collider>(true);
                Assert.That((bool)Call(door, "OpenForPursuer"), Is.True);
                yield return Wait(() => Get<bool>(door, "AtRequestedDoorPose"), 3, "Actual door never reached its requested open pose");
                for (int index = 0; index < faces.Count; index++)
                    Assert.That(Vector3.Distance(faces[index].position, leaf.TransformPoint(anchors[index])), Is.LessThan(.0001f));
                Call(door, "RestoreDoor", false, closed); yield return null;
                Assert.That(leaf.localPosition, Is.EqualTo(closed)); Assert.That(leaf.localScale, Is.EqualTo(scale));
                Assert.That(leaf.localRotation, Is.EqualTo(rotation)); Assert.That(Get<string>(door, "stableId"), Is.EqualTo(stableId));
                Assert.That(leaf.GetComponentsInChildren<Collider>(true), Is.EqualTo(colliders));
                for (int index = 0; index < faces.Count; index++)
                    Assert.That(Vector3.Distance(faces[index].position, leaf.TransformPoint(anchors[index])), Is.LessThan(.0001f));
            }
            Assert.That(HauntedNavigationHash(), Is.EqualTo(navigation), "Render-only handles changed the corridor navigation");
        }
    }
}
