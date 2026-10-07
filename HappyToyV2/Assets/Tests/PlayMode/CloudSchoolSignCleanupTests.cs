using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using static HappyToy.V2.CloudTests.RuntimeAccess;
using Object = UnityEngine.Object;

namespace HappyToy.V2.CloudTests
{
    public sealed partial class CloudPlayModeTests
    {
        [UnityTest, Timeout(45000)]
        public IEnumerator SchoolClassroomDoorOpensWithoutFloatingInteriorSign()
        {
            Call(shell, "BeginChapter");
            yield return null; yield return null;
            IsolateThreats();
            Assert.That(Get<bool>(session, "ChapterMode"), Is.True);
            var detached = CloudRoomPresentationTests.Named("CLASSROOM sign interior");
            var exterior = CloudRoomPresentationTests.Named("CLASSROOM sign");
            var backing = CloudRoomPresentationTests.Named("CLASSROOM sign opaque backing");
            var lintel = CloudRoomPresentationTests.Named("CLASSROOM lintel").GetComponent<Collider>();
            var physicalLintel = lintel.bounds;
            Assert.That(detached.GetComponent<TextMesh>().text, Is.EqualTo("1학년 2반"));
            Assert.That(detached.GetComponent<MeshRenderer>().enabled, Is.False);
            Assert.That(exterior.GetComponent<TextMesh>().text, Is.EqualTo("1학년 2반"));
            Assert.That(exterior.GetComponent<MeshRenderer>().enabled && backing.GetComponent<MeshRenderer>().enabled, Is.True);

            var door = Components("Interactable").Single(item => item.name == "CLASSROOM sliding door");
            var primary = Get<Transform>(door, "movingLeaf");
            var secondary = Get<Transform>(door, "secondaryLeaf");
            Vector3 primaryOpen = primary.localPosition + Get<Vector3>(door, "openOffset");
            Vector3 secondaryOpen = secondary.localPosition - Get<Vector3>(door, "openOffset");
            Call(door, "Use", player);
            yield return Wait(() => Get<bool>(door, "IsOpen") && Get<bool>(door, "AtRequestedDoorPose"), 5,
                "Classroom sign cleanup prevented the production door from opening");
            Assert.That(Vector3.Distance(primary.localPosition, primaryOpen), Is.LessThan(.002f));
            Assert.That(Vector3.Distance(secondary.localPosition, secondaryOpen), Is.LessThan(.002f));
            Assert.That(lintel.enabled, Is.True);
            Assert.That(lintel.bounds.center, Is.EqualTo(physicalLintel.center));
            Assert.That(lintel.bounds.size, Is.EqualTo(physicalLintel.size));
            Assert.That(detached.GetComponent<MeshRenderer>().enabled, Is.False,
                "Opening the classroom door revived its detached label");

            // Look through the opening at eye level, where the runtime portrait
            // used to float. The old upward-only sign view missed this object.
            PlacePlayer(new Vector3(-4.5f, .02f, -.85f), false);
            var camera = Get<Camera>(player, "eyes");
            ((Behaviour)player).enabled = false;
            camera.transform.rotation = Quaternion.LookRotation(new Vector3(-4.8f, 1.75f, 1.46f) - camera.transform.position);
            yield return Delay(.25f);
            var doorwayFrame = SchoolCameraFrame(camera, out _, out _, out _);
            try { CloudExperienceTests.Artifact("school-classroom-open-doorway-front.png", doorwayFrame.EncodeToPNG()); }
            finally { Object.Destroy(doorwayFrame); }

            // The inside-room view also checks the original suppressed text face.
            PlacePlayer(new Vector3(-4.5f, .02f, 3.15f), false);
            camera.transform.rotation = Quaternion.LookRotation(detached.position - camera.transform.position);
            yield return Delay(.25f);
            var frame = SchoolCameraFrame(camera, out _, out _, out _);
            try { CloudExperienceTests.Artifact("school-classroom-door-no-floating-sign.png", frame.EncodeToPNG()); }
            finally { Object.Destroy(frame); }
            var opening = new Bounds(new Vector3(-4.5f, 1.3f, 1.6f), new Vector3(2.4f, 2.4f, .4f));
            var portraits = player.gameObject.scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .Where(item => item.name == "Empty school portrait" && opening.Contains(item.position)).ToArray();
            Assert.That(portraits, Is.Empty, "A generated picture frame still floats in the open 1-2 doorway");
            Debug.Log("HAPPYTOY_CLASSROOM_SIGN_PASS opened original 1-2 sliding door; no detached label or generated portrait inside its opening; exterior/backing and lintel collision retained");
        }
    }
}
