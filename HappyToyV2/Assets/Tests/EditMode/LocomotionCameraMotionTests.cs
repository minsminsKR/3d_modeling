using System;
using NUnit.Framework;
using UnityEngine;
using static HappyToy.V2.CloudTests.RuntimeAccess;

namespace HappyToy.V2.CloudTests
{
    public sealed class LocomotionCameraMotionTests
    {
        object Motion() => Activator.CreateInstance(RequireType("LocomotionCameraMotion"));
        void Step(object motion, Vector3 displacement, float speed, bool grounded, bool running, bool crouching, float dt)
            => Call(motion, "Step", displacement, speed, grounded, running, crouching, dt);

        [Test]
        public void GaitFollowsTravelledDistanceAcrossFrameRatesAndStopsAtWalls()
        {
            var low = Motion(); var high = Motion();
            foreach (var sample in new[] { (motion: low, frames: 30), (motion: high, frames: 120) })
            {
                float dt = 1f / sample.frames;
                for (int index = 0; index < sample.frames * 3; index++)
                    Step(sample.motion, Vector3.forward * (2.6f * dt), 2.6f, true, false, false, dt);
            }
            Assert.That(Get<float>(low, "DistancePhase"), Is.EqualTo(Get<float>(high, "DistancePhase")).Within(.0001f));
            Assert.That(Vector3.Distance(Get<Vector3>(low, "PositionOffset"), Get<Vector3>(high, "PositionOffset")), Is.LessThan(.009f));
            float phase = Get<float>(high, "DistancePhase");
            for (int index = 0; index < 240; index++) Step(high, Vector3.zero, 0, true, true, false, 1f / 120);
            Assert.That(Get<float>(high, "DistancePhase"), Is.EqualTo(phase), "Held sprint at a wall invented camera strides");
            Assert.That(Get<Vector3>(high, "PositionOffset").magnitude, Is.LessThan(.0001f));
            Assert.That(Get<Vector3>(high, "RotationOffset").magnitude, Is.LessThan(.001f));
        }

        [Test]
        public void SprintIsMorePhysicalThanWalkAndCrouchRemainsRestrained()
        {
            float[] verticalRange = new float[3]; float[] angularRange = new float[3];
            float[] fov = new float[3];
            for (int mode = 0; mode < 3; mode++)
            {
                var motion = Motion(); float dt = 1f / 120, speed = mode == 1 ? 4.4f : mode == 2 ? 1.43f : 2.6f;
                float min = 10, max = -10;
                for (int index = 0; index < 360; index++)
                {
                    Step(motion, Vector3.forward * (speed * dt), speed, true, mode == 1, mode == 2, dt);
                    if (index < 120) continue;
                    var offset = Get<Vector3>(motion, "PositionOffset");
                    min = Mathf.Min(min, offset.y); max = Mathf.Max(max, offset.y);
                    angularRange[mode] = Mathf.Max(angularRange[mode], Get<Vector3>(motion, "RotationOffset").magnitude);
                    Assert.That(offset.magnitude, Is.LessThan(.08f), "Excessive head translation");
                }
                verticalRange[mode] = max - min; fov[mode] = Get<float>(motion, "FovOffset");
            }
            Assert.That(verticalRange[0], Is.GreaterThan(.035f));
            Assert.That(verticalRange[1], Is.GreaterThan(verticalRange[0] * 1.45f));
            Assert.That(verticalRange[2], Is.LessThan(verticalRange[0] * .55f));
            Assert.That(angularRange[1], Is.GreaterThan(angularRange[0] * 1.35f));
            Assert.That(angularRange[1], Is.LessThan(1.5f));
            Assert.That(fov[0], Is.Zero); Assert.That(fov[1], Is.InRange(3.5f, 3.76f)); Assert.That(fov[2], Is.Zero);
        }

        [Test]
        public void LandingRespondsOnceAndTeleportsCannotBecomeImpacts()
        {
            var motion = Motion();
            Step(motion, Vector3.zero, 0, true, false, false, .02f);
            for (int index = 0; index < 20; index++) Step(motion, Vector3.down * .025f, 0, false, false, false, .02f);
            Step(motion, Vector3.zero, 0, true, false, false, .02f);
            Assert.That(Get<Vector3>(motion, "PositionOffset").y, Is.LessThan(-.0005f));
            for (int index = 0; index < 100; index++) Step(motion, Vector3.zero, 0, true, false, false, .02f);
            Assert.That(Get<Vector3>(motion, "PositionOffset").magnitude, Is.LessThan(.0001f));
            Step(motion, new Vector3(20, -5, 0), 4.4f, true, true, false, .02f);
            Assert.That(Get<Vector3>(motion, "PositionOffset"), Is.EqualTo(Vector3.zero));
            Assert.That(Get<Vector3>(motion, "RotationOffset"), Is.EqualTo(Vector3.zero));
            Assert.That(Get<float>(motion, "FovOffset"), Is.Zero);
        }
    }
}
