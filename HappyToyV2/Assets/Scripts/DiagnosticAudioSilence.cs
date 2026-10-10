using System;
using System.Collections;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using UnityEngine;

namespace HappyToy.V2
{
    // Device mute is downstream of the listener DSP. Ordinary play never enters.
    public static class DiagnosticAudioSilence
    {
        static bool active, prefsRestored, ending;
        static volatile bool deviceMuted, stopping;
        static volatile bool gainNeedsRestore, nativeTouched, restoreAttempted, nativeRestoreSucceeded;
        static volatile int restoredSessions;
        static bool workerJoined, preferenceRestoreSucceeded, sessionMuteVerified;
        static volatile int matched;
        static string failure = "Not acquired", prefix, lastOutput, restoreFailure = "", preferenceRestoreFailure = "";
        static Thread worker; static GameShell lastShell;
        static readonly string[] FloatKeys = { "v2.volume", "v2.sensitivity", "v2.fov" };
        static readonly string[] IntKeys = { "v2.reducedMotion", "v2.subtitles", "v2.highContrast", "v2.largeText" };
        static readonly Guid Context = new Guid("e9c8fb4a-2fd9-4f34-b7ba-2eb3979b2a01");
        public static bool Enabled => active;
        public static bool DeviceSessionMuted => deviceMuted;
        public static bool Requested(string[] args) => args.Contains("-runTests") || args.Contains("-quiet-diagnostics") ||
            args.Any(a => a.StartsWith("-v", StringComparison.Ordinal) && a.EndsWith("-output", StringComparison.Ordinal) && a != "-v2-build-output");
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSplashScreen)] static void Early() { Activate(); }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)] static void BeforeScene()
        {
            Activate(); if (!active) return;
            if (!UnityEngine.Object.FindAnyObjectByType<DiagnosticAudioSilenceGuard>())
            { var owner = new GameObject("Diagnostic process audio silence guard"); UnityEngine.Object.DontDestroyOnLoad(owner); owner.AddComponent<DiagnosticAudioSilenceGuard>(); }
        }
        public static void Activate()
        {
            if (active || !Requested(Environment.GetCommandLineArgs())) return;
            active = true; AudioListener.volume = 0;
            var process = Process.GetCurrentProcess();
            prefix = "HAPPYTOY_QUIET_" + process.Id + "_" + process.StartTime.ToUniversalTime().Ticks + "_";
            // Only this process's environment survives managed-domain reloads.
            // It never changes the user's/system's environment or another app.
            if (Get("BACKUP") != "1")
            {
                foreach (string key in FloatKeys.Concat(IntKeys))
                {
                    Put("HAS_" + key, PlayerPrefs.HasKey(key) ? "1" : "0");
                    Put("VALUE_" + key, FloatKeys.Contains(key) ? PlayerPrefs.GetFloat(key).ToString("R", CultureInfo.InvariantCulture) : PlayerPrefs.GetInt(key).ToString(CultureInfo.InvariantCulture));
                }
                Put("BACKUP", "1");
            }
            PlayerPrefs.SetFloat("v2.volume", 0); // GameShell.Awake begins silent.
            if (Application.platform != RuntimePlatform.WindowsEditor && Application.platform != RuntimePlatform.WindowsPlayer)
            { failure = "Process session mute unavailable; listener retained at zero"; return; }
            worker = new Thread(Work) { IsBackground = true, Name = "HappyToy diagnostic CoreAudio mute" };
            worker.Start(); AudioSettings.OnAudioConfigurationChanged += DeviceChanged;
        }
        public static float FilterVolume(float requested) => active && (ending || !deviceMuted) ? 0 : requested;
        static void DeviceChanged(bool changed)
        { if (active) { deviceMuted = false; gainNeedsRestore = true; AudioListener.volume = 0; } }
        public static void Pump()
        {
            if (!active || ending) return;
            if (!deviceMuted) { gainNeedsRestore = true; AudioListener.volume = 0; return; }
            if (!prefsRestored) { RestorePreferenceKeys(); prefsRestored = true; }
            var shell = GameSession.Current ? GameSession.Current.Shell : null;
            if (shell && !ReferenceEquals(shell, lastShell))
            {
                lastShell = shell; float volume = Get("HAS_v2.volume") == "1" ? SavedFloat("v2.volume") : .8f;
                shell.AdjustSettings(volume - shell.Volume, 0);
            }
            else if (shell && gainNeedsRestore) AudioListener.volume = FilterVolume(shell.Volume);
            gainNeedsRestore = false;
        }
        public static IEnumerator WaitForSafeAudio(string directory, bool requiresPositiveMixer = true)
        {
            Activate(); float deadline = Time.realtimeSinceStartup + 6;
            while (active && !deviceMuted && Time.realtimeSinceStartup < deadline) { AudioListener.volume = 0; Pump(); yield return null; }
            WriteEvidence(directory);
            if (deviceMuted) sessionMuteVerified = true;
            if (active && !deviceMuted)
            {
                AudioListener.volume = 0;
                if (!requiresPositiveMixer) yield break;
                Application.Quit(2); throw new InvalidOperationException("Quiet guard failed: listener stays zero; positive PCM not verified. " + failure);
            }
        }
        [Serializable] sealed class Evidence
        {
            public bool diagnosticRequested, deviceSessionMuted, sessionMuteVerified, currentProcessOnly = true;
            public bool stopRequested, workerJoined, nativeRestoreRequired, nativeRestoreAttempted, nativeRestoreSucceeded, preferenceRestoreSucceeded;
            public int matchedSessions, restoredSessions;
            public float listenerVolume;
            public string failure, nativeRestoreFailure, preferenceRestoreFailure;
            public string scope = "Current-process Windows render-session mute/readback, downstream of Unity listener DSP; device-configuration change immediately gates listener gain to zero. Polling does not certify unobserved newly created sessions. An unavailable guard retains zero gain and cannot prove positive PCM; visual-only diagnostics may continue silent. Exit records live-session original-mute readback and seven preference keys. Ordinary play does not activate the guard.";
        }
        public static void WriteEvidence(string directory)
        {
            if (string.IsNullOrEmpty(directory)) return; lastOutput = directory; Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, "diagnostic-audio-silence.json"), JsonUtility.ToJson(new Evidence {
                diagnosticRequested = active, deviceSessionMuted = deviceMuted, sessionMuteVerified = sessionMuteVerified || deviceMuted,
                matchedSessions = matched, failure = deviceMuted || sessionMuteVerified ? "" : failure,
                stopRequested = ending, workerJoined = workerJoined, nativeRestoreRequired = nativeTouched,
                nativeRestoreAttempted = restoreAttempted, nativeRestoreSucceeded = nativeRestoreSucceeded,
                nativeRestoreFailure = restoreFailure, restoredSessions = restoredSessions,
                preferenceRestoreSucceeded = preferenceRestoreSucceeded, preferenceRestoreFailure = preferenceRestoreFailure,
                listenerVolume = AudioListener.volume }, true));
        }
        static string Get(string key) => Environment.GetEnvironmentVariable(prefix + key);
        static void Put(string key, string value) => Environment.SetEnvironmentVariable(prefix + key, value, EnvironmentVariableTarget.Process);
        static float SavedFloat(string key) => float.Parse(Get("VALUE_" + key), CultureInfo.InvariantCulture);
        static void RestorePreferenceKeys()
        {
            foreach (string key in FloatKeys.Concat(IntKeys))
            {
                if (Get("HAS_" + key) != "1") PlayerPrefs.DeleteKey(key);
                else if (FloatKeys.Contains(key)) PlayerPrefs.SetFloat(key, SavedFloat(key));
                else PlayerPrefs.SetInt(key, int.Parse(Get("VALUE_" + key), CultureInfo.InvariantCulture));
            }
        }
        public static void StopAndRestore()
        {
            if (!active || ending) return; ending = true;
            AudioListener.volume = 0; stopping = true;
            try
            {
                workerJoined = worker == null || !worker.IsAlive || worker.Join(700);
                if (!workerJoined) restoreFailure = "CoreAudio worker did not finish before the bounded exit wait";
                if (!nativeTouched && workerJoined) nativeRestoreSucceeded = true;
            }
            catch (Exception error) { restoreFailure = "Worker join failed: " + error.GetType().Name; }
            try
            {
                if (GameSession.Current && GameSession.Current.Shell) GameSession.Current.Shell.SendMessage("SaveSettings", SendMessageOptions.DontRequireReceiver);
                RestorePreferenceKeys(); PlayerPrefs.Save();
                preferenceRestoreSucceeded = FloatKeys.Concat(IntKeys).All(key =>
                    PlayerPrefs.HasKey(key) == (Get("HAS_" + key) == "1") &&
                    (Get("HAS_" + key) != "1" || (FloatKeys.Contains(key) ?
                        PlayerPrefs.GetFloat(key).Equals(SavedFloat(key)) :
                        PlayerPrefs.GetInt(key) == int.Parse(Get("VALUE_" + key), CultureInfo.InvariantCulture))));
                if (!preferenceRestoreSucceeded) preferenceRestoreFailure = "Preference readback did not match the seven backed-up keys";
            }
            catch (Exception error) { preferenceRestoreFailure = "Preference restore failed: " + error.GetType().Name; }
            AudioListener.volume = 0;
            AudioSettings.OnAudioConfigurationChanged -= DeviceChanged;
            WriteEvidence(lastOutput);
        }
        static void Work()
        {
            int initialized = CoInitializeEx(IntPtr.Zero, 0);
            if (initialized < 0) { failure = "COM initialization failed: " + initialized.ToString("X8"); return; }
            try
            {
                while (!stopping)
                {
                    try { matched = VisitSessions(false); deviceMuted = matched > 0; if (!deviceMuted) gainNeedsRestore = true;
                        failure = matched > 0 ? "" : "No render session for this process yet"; }
                    catch (Exception error) { matched = 0; deviceMuted = false; gainNeedsRestore = true; failure = "CoreAudio mute failed: " + error; }
                    Thread.Sleep(80);
                }
                // Listener is already zero; drain device buffers before restoring.
                Thread.Sleep(200); restoreAttempted = nativeTouched;
                try { restoredSessions = nativeTouched ? VisitSessions(true) : 0; nativeRestoreSucceeded = true; }
                catch (Exception error) { nativeRestoreSucceeded = false; restoreFailure = "Original session mute restore/readback failed: " + error; }
                deviceMuted = false;
            }
            finally { CoUninitialize(); }
        }
        // Unity's Windows Mono cannot reliably cast its COM RCWs to these
        // imported interfaces. Use COM-owned pointers and explicit QueryInterface
        // instead; every acquired interface is released once on this MTA thread.
        static int VisitSessions(bool restore)
        {
            uint pid = (uint)Process.GetCurrentProcess().Id; int count = 0;
            IntPtr native = IntPtr.Zero, devices = IntPtr.Zero;
            try
            {
                Guid clsid = new Guid("BCDE0395-E52F-467C-8E3D-C4579291692E");
                Guid iid = new Guid("A95664D2-9614-4F35-A746-DE8DB63617E6");
                Check(CoCreateInstance(ref clsid, IntPtr.Zero, 1, ref iid, out native), "CoCreateInstance(IMMDeviceEnumerator)");
                Check(Method<EnumEndpoints>(native, 3)(native, 0, 1, out devices), "EnumAudioEndpoints");
                Check(Method<GetCountUInt>(devices, 3)(devices, out uint total), "Device.GetCount");
                for (uint i = 0; i < total; i++)
                {
                    IntPtr device = IntPtr.Zero, manager = IntPtr.Zero, sessions = IntPtr.Zero;
                    try
                    {
                        Check(Method<GetDevice>(devices, 4)(devices, i, out device), "Device.Item");
                        string deviceId = StringAt(device, 5, "Device.GetId");
                        iid = new Guid("77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F");
                        Check(Method<ActivateDevice>(device, 3)(device, ref iid, 23, IntPtr.Zero, out manager), "Device.Activate(IAudioSessionManager2)");
                        Check(Method<GetInterface>(manager, 5)(manager, out sessions), "GetSessionEnumerator");
                        Check(Method<GetCountInt>(sessions, 3)(sessions, out int n), "Session.GetCount");
                        for (int j = 0; j < n; j++)
                        {
                            IntPtr control = IntPtr.Zero, detail = IntPtr.Zero, volume = IntPtr.Zero;
                            try
                            {
                                Check(Method<GetSession>(sessions, 4)(sessions, j, out control), "GetSession");
                                detail = Query(control, "BFB7FF88-7239-4FC9-8FA2-07C950BE9C6D", "IAudioSessionControl2");
                                int ownership = Method<GetProcessId>(detail, 14)(detail, out uint owner);
                                Check(ownership, "GetProcessId"); if (owner != pid) continue;
                                if (ownership != 0) throw new InvalidOperationException("Shared process session cannot be isolated");
                                string id = StringAt(detail, 12, "GetSessionIdentifier"), key;
                                using (var hash = SHA256.Create()) key = "SESSION_" + BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(deviceId + id))).Replace("-", "");
                                volume = Query(control, "87CE5498-68D6-44E5-9215-6DA47EF883D8", "ISimpleAudioVolume");
                                if (Get(key) == null)
                                {
                                    Check(Method<GetMute>(volume, 6)(volume, out int original), "GetMute(original)");
                                    Put(key, original != 0 ? "1" : "0");
                                }
                                int expected = restore ? (Get(key) == "1" ? 1 : 0) : 1;
                                Guid context = Context;
                                Check(Method<SetMute>(volume, 5)(volume, expected, ref context), restore ? "SetMute(restore)" : "SetMute(true)");
                                if (!restore) nativeTouched = true;
                                Check(Method<GetMute>(volume, 6)(volume, out int muted), "GetMute(readback)");
                                if ((muted != 0) != (expected != 0)) throw new InvalidOperationException(restore ? "Original mute readback failed" : "Mute readback failed");
                                count++;
                            }
                            finally { Release(volume); Release(detail); Release(control); }
                        }
                    }
                    finally { Release(sessions); Release(manager); Release(device); }
                }
            }
            finally { Release(devices); Release(native); }
            return count;
        }
        static T Method<T>(IntPtr instance, int slot) where T : Delegate
        {
            if (instance == IntPtr.Zero) throw new InvalidOperationException("Null CoreAudio interface at slot " + slot);
            return Marshal.GetDelegateForFunctionPointer<T>(Marshal.ReadIntPtr(Marshal.ReadIntPtr(instance), slot * IntPtr.Size));
        }
        static IntPtr Query(IntPtr instance, string id, string name)
        {
            Guid iid = new Guid(id); IntPtr result = IntPtr.Zero;
            int hr = Method<QueryInterface>(instance, 0)(instance, ref iid, out result);
            if (hr < 0) { Release(result); Check(hr, "QueryInterface(" + name + ")"); }
            return result;
        }
        static string StringAt(IntPtr instance, int slot, string operation)
        {
            IntPtr text = IntPtr.Zero;
            try { Check(Method<GetInterface>(instance, slot)(instance, out text), operation); return Marshal.PtrToStringUni(text); }
            finally { if (text != IntPtr.Zero) Marshal.FreeCoTaskMem(text); }
        }
        static void Check(int result, string operation)
        { if (result < 0) throw new COMException(operation + " failed (HRESULT 0x" + result.ToString("X8") + ")", result); }
        static void Release(IntPtr instance) { if (instance != IntPtr.Zero) Marshal.Release(instance); }
        [DllImport("ole32.dll")] static extern int CoInitializeEx(IntPtr reserved, uint mode);
        [DllImport("ole32.dll")] static extern void CoUninitialize();
        [DllImport("ole32.dll", ExactSpelling = true)] static extern int CoCreateInstance(ref Guid clsid, IntPtr outer, uint context, ref Guid iid, out IntPtr instance);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int QueryInterface(IntPtr self, ref Guid iid, out IntPtr result);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int EnumEndpoints(IntPtr self, int flow, uint state, out IntPtr devices);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int GetCountUInt(IntPtr self, out uint count);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int GetDevice(IntPtr self, uint index, out IntPtr device);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int GetInterface(IntPtr self, out IntPtr result);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int ActivateDevice(IntPtr self, ref Guid iid, uint context, IntPtr parameters, out IntPtr result);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int GetCountInt(IntPtr self, out int count);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int GetSession(IntPtr self, int index, out IntPtr session);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int GetProcessId(IntPtr self, out uint pid);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int GetMute(IntPtr self, out int muted);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int SetMute(IntPtr self, int muted, ref Guid context);
    }
}
