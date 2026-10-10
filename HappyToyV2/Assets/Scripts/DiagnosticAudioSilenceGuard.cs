using UnityEngine;

namespace HappyToy.V2
{
    [DefaultExecutionOrder(-32000)] public sealed class DiagnosticAudioSilenceGuard : MonoBehaviour
    {
        void Update() { DiagnosticAudioSilence.Pump(); }
        void LateUpdate() { if (DiagnosticAudioSilence.Enabled && !DiagnosticAudioSilence.DeviceSessionMuted) AudioListener.volume = 0; }
        void OnApplicationQuit() { DiagnosticAudioSilence.StopAndRestore(); }
        void OnDestroy() { if (!Application.isEditor) DiagnosticAudioSilence.StopAndRestore(); }
    }
}
