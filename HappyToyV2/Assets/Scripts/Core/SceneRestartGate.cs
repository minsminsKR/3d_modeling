using System;

namespace HappyToy.V2
{
    /// <summary>One scene reload at a time; a failed request cannot arm a future run.</summary>
    public sealed class SceneRestartGate
    {
        public bool Pending { get; private set; }
        public string ScenePath { get; private set; } = string.Empty;
        bool beginPlaying;

        public bool TryRequest(string scenePath, bool play)
        {
            if (Pending || string.IsNullOrWhiteSpace(scenePath)) return false;
            ScenePath = scenePath;
            beginPlaying = play;
            Pending = true;
            return true;
        }
        public bool TryConsume(string loadedScenePath, out bool play)
        {
            play = false;
            if (!Pending || !string.Equals(ScenePath, loadedScenePath, StringComparison.Ordinal)) return false;
            play = beginPlaying;
            Cancel();
            return true;
        }
        public void Cancel()
        {
            Pending = false;
            ScenePath = string.Empty;
            beginPlaying = false;
        }
    }
}
