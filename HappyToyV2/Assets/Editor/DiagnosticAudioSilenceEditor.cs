using UnityEditor;

namespace HappyToy.V2.Editor
{
    [InitializeOnLoad] public static class DiagnosticAudioSilenceEditor
    {
        static DiagnosticAudioSilenceEditor()
        {
            DiagnosticAudioSilence.Activate();
            EditorApplication.update += DiagnosticAudioSilence.Pump;
            EditorApplication.quitting += DiagnosticAudioSilence.StopAndRestore;
        }
    }
}
