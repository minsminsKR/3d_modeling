using System;

namespace HappyToy.V2.Editor
{
    // Exercises the exact runtime request gate; no Unity or mirrored scene-loading model.
    public static class SceneRestartGateChecks
    {
        public static int Run()
        {
            int count = 0;
            Action<bool, string> check = (condition, message) =>
            {
                count++;
                if (!condition) throw new InvalidOperationException("SceneRestartGate: " + message);
            };
            const string school = "Assets/Annex/SchoolAnnex.unity";
            const string other = "Assets/Another.unity";
            var gate = new SceneRestartGate();
            bool play;
            check(!gate.Pending && gate.ScenePath == string.Empty, "new application has no queued restart");
            check(!gate.TryConsume(school, out play) && !play, "normal launch cannot consume stale play intent");
            check(!gate.TryRequest(null, true), "null scene rejected");
            check(!gate.TryRequest(string.Empty, true), "empty scene rejected");
            check(!gate.TryRequest(" \t\n", true), "blank scene rejected");
            check(!gate.Pending && gate.ScenePath == string.Empty, "invalid target never arms a future run");
            check(gate.TryRequest(school, true) && gate.Pending && gate.ScenePath == school, "valid restart is pending");
            check(!gate.TryRequest(school, true), "repeated same request rejected");
            check(!gate.TryRequest(school, false), "mixed repeat cannot replace destination with Title");
            check(!gate.TryRequest(other, false) && gate.ScenePath == school, "another target cannot replace the active request");
            check(!gate.TryConsume(null, out play) && !play && gate.Pending, "unidentified scene cannot consume intent");
            check(!gate.TryConsume(other, out play) && !play && gate.Pending, "unrelated scene cannot consume intent");
            check(gate.TryConsume(school, out play) && play, "matching loaded scene receives original play intent");
            check(!gate.Pending && gate.ScenePath == string.Empty, "successful handoff clears request");
            check(!gate.TryConsume(school, out play) && !play, "a request is consumed only once");
            check(gate.TryRequest(school, false), "return-to-title is an explicit request");
            check(!gate.TryRequest(school, true), "repeat cannot turn return-to-title into play");
            check(gate.TryConsume(school, out play) && !play, "explicit Title is distinguishable from no request");
            check(gate.TryRequest(school, true), "new request permitted after completed handoff");
            gate.Cancel();
            check(!gate.Pending && gate.ScenePath == string.Empty, "failed load rollback clears target and gate");
            check(!gate.TryConsume(school, out play) && !play, "failed load cannot auto-start a later scene");
            gate.Cancel();
            check(!gate.Pending && gate.ScenePath == string.Empty, "repeated rollback is harmless");
            check(gate.TryRequest(school, false), "retry allowed after failure");
            check(gate.TryConsume(school, out play) && !play, "retry keeps its own destination");
            check(gate.TryRequest(other, true), "a later independent scene can request a reload");
            gate.Cancel();
            check(!gate.TryConsume(other, out play) && !play, "new application reset drops any queued handoff");
            return count;
        }
    }
}
