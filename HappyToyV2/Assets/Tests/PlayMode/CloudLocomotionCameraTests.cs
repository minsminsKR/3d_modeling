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
        [UnityTest, Timeout(45000)]
        public IEnumerator CameraMotionRealWalkSprintWallPauseAndComfortStayGrounded()
        {
            IsolateThreats(); Begin(); Call(shell, "RestoreDefaultSettings");
            Cube("CloudQA camera gait floor", new Vector3(200, -.25f, 200), new Vector3(30, .5f, 60));
            PlacePlayer(new Vector3(200, .02f, 180)); Keys(); yield return Delay(.35f);
            var camera = Get<Camera>(player, "eyes");
            float minWalk = 10, maxWalk = -10, minRun = 10, maxRun = -10;
            yield return KeysObserved(Key.W);
            float until = Time.realtimeSinceStartup + 1.35f;
            while (Time.realtimeSinceStartup < until)
            {
                yield return null; float height = camera.transform.localPosition.y;
                minWalk = Mathf.Min(minWalk, height); maxWalk = Mathf.Max(maxWalk, height);
            }
            yield return KeysObserved(Key.W, Key.LeftShift); yield return Delay(.45f);
            until = Time.realtimeSinceStartup + 1.2f;
            while (Time.realtimeSinceStartup < until)
            {
                yield return null; float height = camera.transform.localPosition.y;
                minRun = Mathf.Min(minRun, height); maxRun = Mathf.Max(maxRun, height);
                Assert.That(Quaternion.Angle(camera.transform.localRotation, Get<Quaternion>(player, "LookRotation")), Is.LessThan(1.5f));
            }
            Assert.That(maxWalk - minWalk, Is.GreaterThan(.025f));
            Assert.That(maxRun - minRun, Is.GreaterThan((maxWalk - minWalk) * 1.35f));
            Assert.That(camera.fieldOfView, Is.GreaterThan(Get<float>(shell, "FieldOfView") + 2.5f));
            Call(shell, "Pause"); var pausedPosition = camera.transform.localPosition; var pausedRotation = camera.transform.localRotation;
            float pausedFov = camera.fieldOfView; yield return Delay(.2f);
            Assert.That(camera.transform.localPosition, Is.EqualTo(pausedPosition));
            Assert.That(camera.transform.localRotation, Is.EqualTo(pausedRotation)); Assert.That(camera.fieldOfView, Is.EqualTo(pausedFov));
            Call(shell, "Resume"); Keys(); yield return Delay(.7f);
            Assert.That(Vector3.Distance(camera.transform.localPosition, Vector3.up * 1.6f), Is.LessThan(.002f));
            Assert.That(Quaternion.Angle(camera.transform.localRotation, Get<Quaternion>(player, "LookRotation")), Is.LessThan(.01f));
            // Physical obstruction with held real sprint input must settle, rather than bob in place.
            var wall = Cube("CloudQA camera gait wall", player.transform.position + Vector3.forward * 1.3f + Vector3.up * 1.5f,
                new Vector3(4, 3, .4f)); Physics.SyncTransforms();
            yield return KeysObserved(Key.W, Key.LeftShift);
            yield return Wait(() => Get<float>(player, "ActualSpeed") < .05f, 2, "Player never contacted fixture wall");
            yield return Delay(.65f);
            Assert.That(Vector3.Distance(camera.transform.localPosition, Vector3.up * 1.6f), Is.LessThan(.002f));
            wall.SetActive(false); Call(shell, "ToggleReducedMotion"); yield return Delay(.3f);
            Assert.That(Get<float>(player, "ActualSpeed"), Is.GreaterThan(1));
            Assert.That(camera.transform.localPosition, Is.EqualTo(Vector3.up * 1.6f));
            Assert.That(camera.transform.localRotation, Is.EqualTo(Get<Quaternion>(player, "LookRotation")));
            Keys();
        }

        [UnityTest, Timeout(30000)]
        public IEnumerator CameraMotionCabinetLeaveDoesNotCarryRollOrPositionalDrift()
        {
            IsolateThreats(); Begin(); Call(shell, "RestoreDefaultSettings");
            var cabinet = Components("Interactable").First(item => Get<object>(item, "kind").ToString() == "HidingPlace");
            PlacePlayer(Get<Transform>(cabinet, "outside").position); Keys(); yield return Delay(.2f);
            Call(cabinet, "Use", player); yield return null; yield return null;
            var camera = Get<Camera>(player, "eyes");
            Assert.That(Get<bool>(player, "Hidden"), Is.True);
            Assert.That(camera.transform.localPosition, Is.EqualTo(Get<Vector3>(player, "HiddenCameraLocalPosition")));
            Call(cabinet, "Use", player); yield return Delay(.6f);
            Assert.That(Get<bool>(player, "Hidden"), Is.False);
            Assert.That(Vector3.Distance(camera.transform.localPosition, Vector3.up * 1.6f), Is.LessThan(.002f));
            Assert.That(Quaternion.Angle(camera.transform.localRotation, Get<Quaternion>(player, "LookRotation")), Is.LessThan(.01f));
        }

        [UnityTest, Timeout(30000)]
        public IEnumerator CameraMotionYieldsToSchoolShotAndResumesWithoutDriftAfterCancellation()
        {
            Call(shell, "RestoreDefaultSettings"); Call(shell, "BeginChapter"); yield return null; yield return null;
            var chapter = Get<Component>(session, "Chapter");
            var shots = Get<Component>(chapter, "FirstAppearances");
            var memories = Get<Component[]>(chapter, "Memories");
            var camera = Get<Camera>(player, "eyes");
            yield return KeysObserved(Key.W); yield return Delay(.25f); Keys();
            Call(memories[0], "Use", player);
            Assert.That(Get<bool>(shots, "CameraOwned"), Is.True); yield return Delay(.3f);
            var at = camera.transform.localPosition; var rotation = camera.transform.localRotation; float fov = camera.fieldOfView;
            FeedbackCall(Get<Component>(player, "Feedback"), "LateUpdate");
            Assert.That(camera.transform.localPosition, Is.EqualTo(at), "Locomotion moved a chapter-owned camera");
            Assert.That(camera.transform.localRotation, Is.EqualTo(rotation)); Assert.That(camera.fieldOfView, Is.EqualTo(fov));
            Call(shell, "Pause"); yield return Delay(.15f);
            Assert.That(camera.transform.localPosition, Is.EqualTo(at)); Assert.That(camera.transform.localRotation, Is.EqualTo(rotation));
            Call(shell, "Resume"); ((Behaviour)shots).enabled = false;
            Get<Component>(chapter, "Cyclopse").gameObject.SetActive(false);
            yield return Delay(.7f);
            Assert.That(Get<bool>(shots, "CameraOwned"), Is.False);
            Assert.That(Vector3.Distance(camera.transform.localPosition, Vector3.up * 1.6f), Is.LessThan(.002f));
            Assert.That(Quaternion.Angle(camera.transform.localRotation, Get<Quaternion>(player, "LookRotation")), Is.LessThan(.01f));
        }
    }
}
