using System;
using NUnit.Framework;

namespace HappyToy.V2.CoreTests
{
    public sealed class SceneRestartGateTests
    {
        [Test]
        public void DuplicateRequestsKeepOriginalDestinationAndConsumeOnce()
        {
            var gate = new SceneRestartGate();
            Assert.That(gate.TryRequest("School", true), Is.True);
            Assert.That(gate.TryRequest("School", false), Is.False);
            Assert.That(gate.TryRequest("Other", false), Is.False);
            Assert.That(gate.TryConsume("Other", out bool play), Is.False);
            Assert.That(play, Is.False); Assert.That(gate.Pending, Is.True);
            Assert.That(gate.TryConsume("School", out play), Is.True); Assert.That(play, Is.True);
            Assert.That(gate.TryConsume("School", out play), Is.False); Assert.That(play, Is.False);
        }

        [Test]
        public void CancellationAndInvalidTargetNeverArmAFutureRun()
        {
            var gate = new SceneRestartGate();
            foreach (string target in new[] { null, "", " \t\n" })
                Assert.That(gate.TryRequest(target, true), Is.False);
            gate.TryRequest("School", true); gate.Cancel();
            Assert.That(gate.TryConsume("School", out bool play), Is.False); Assert.That(play, Is.False);
            Assert.That(gate.ScenePath, Is.Empty);
            Assert.That(gate.TryRequest("School", false), Is.True);
            Assert.That(gate.TryConsume("School", out play), Is.True); Assert.That(play, Is.False);
        }
    }
}
