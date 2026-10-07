using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UIElements;

namespace HappyToy.V2
{
    // Explicit native visual/audio assay. Poses and movement speed are controlled;
    // recognition, LOS, brain state, sound playback, URP and HUD are production code.
    // This does not certify survival difficulty or subjective human fear.
    public sealed class HorrorFeedbackAudit : MonoBehaviour
    {
        string output;
        float started;
        bool finished;
        GameSession session;
        PlayerMotor player;
        StalkerBrain actor;
        DetectionFeedback detection;
        RouteAudioCapture capture;
        readonly string[] floatKeys = { "v2.volume", "v2.sensitivity", "v2.fov" };
        readonly string[] optionKeys = { "v2.reducedMotion", "v2.subtitles", "v2.highContrast", "v2.largeText" };
        float[] oldFloats;
        int[] oldOptions;
        bool[] hadFloats, hadOptions;
        bool preferencesSaved, preferencesRestored;
        readonly List<string> errors = new List<string>();
        readonly List<Shot> shots = new List<Shot>();
        [Serializable] sealed class Shot
        {
            public string name, state;
            public float wallSeconds, impact, chase, peripheral, distortion, chromatic, noise, stress, targetStress, interferenceGain;
            public float noiseOnOffMeanDifference;
            public int cues;
            public int nativeHudPixels, noiseAffectedPixels;
            public bool reducedMotion, sight;
        }
        [Serializable] sealed class Report
        {
            public string status, scope = "Controlled native poses/zero movement speed; genuine LOS, recognition, Chase. Production URP camera and existing UI Toolkit panel directly render into the same native HDR target; bounded fresh-paint polling, no screenshot/backbuffer or synthetic composition. Listener DSP runs naturally while brief image captures freeze game time. Escape is controlled threat disengagement, not chapter completion.";
            public string unity, device, failure;
            public int width, height;
            public bool recognition, continuousChase, returnedToCalm, reducedMotionRespected, pauseFreezes;
            public bool discoveryNoiseRendered, pursuitNoiseRendered;
            public string[] errors;
            public Shot[] shots;
            public RouteAudioCapture.Report audio;
        }
        readonly Report report = new Report();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            var args = Environment.GetCommandLineArgs(); int i = Array.IndexOf(args, "-v2-horror-feedback-output");
            if (i < 0 || i + 1 >= args.Length) return;
            new GameObject("Explicit native horror feedback assay").AddComponent<HorrorFeedbackAudit>().output = Path.GetFullPath(args[i + 1]);
        }
        void OnEnable() { Application.logMessageReceived += Log; }
        void OnDisable() { Application.logMessageReceived -= Log; }
        void Log(string message, string trace, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors.Add(message);
        }
        void Update()
        {
            if (!finished && started > 0 && Time.realtimeSinceStartup - started > 28)
                Finish("Bounded native feedback assay timed out");
        }
        IEnumerator Start()
        {
            started = Time.realtimeSinceStartup; Application.runInBackground = true;
            Directory.CreateDirectory(output);
            var stack = new Stack<IEnumerator>(); stack.Push(Run());
            while (!finished)
            {
                object next = null; bool moved = false; string failure = null;
                try { moved = stack.Peek().MoveNext(); if (moved) next = stack.Peek().Current; }
                catch (Exception error) { failure = error.ToString(); }
                if (failure != null) { Finish(failure); yield break; }
                if (!moved)
                {
                    stack.Pop();
                    if (stack.Count == 0) { Finish(null); yield break; }
                    continue;
                }
                if (next is IEnumerator nested) { stack.Push(nested); continue; }
                yield return next;
            }
        }
        IEnumerator Run()
        {
            yield return null; yield return null;
            session = GameSession.Current;
            if (!session || !session.player || !session.Shell) throw new InvalidOperationException("Player/session unavailable");
            if (!session.InputAllowed) session.Shell.Begin();
            PreservePreferences();
            session.Shell.RestoreDefaultSettings();
            float deadline = Time.realtimeSinceStartup + 8;
            while (NavMesh.CalculateTriangulation().vertices.Length == 0 && Time.realtimeSinceStartup < deadline) yield return null;
            var director = FindFirstObjectByType<StoryDirector>();
            if (!director || !director.stalker) throw new InvalidOperationException("School's real cyclops actor unavailable");
            actor = director.stalker;
            foreach (var component in FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (component is StoryDirector || component is AnnexEncounter || component is UncatAnnexEvent ||
                    component is V1HwacatEvent || component is LanternMaskEncounter || component is WeepingAngelEncounter ||
                    component is LovelyDollGuide)
                { component.StopAllCoroutines(); component.enabled = false; }
            }
            foreach (var brain in FindObjectsByType<StalkerBrain>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                brain.gameObject.SetActive(false);
            foreach (var startup in actor.GetComponents<NavMeshStartup>()) { startup.StopAllCoroutines(); startup.enabled = false; }
            if (!NavMesh.SamplePosition(new Vector3(-6.5f, 0, 0), out var origin, 1.5f, NavMesh.AllAreas) ||
                !NavMesh.SamplePosition(origin.position + Vector3.right * 6, out var feet, 1, NavMesh.AllAreas))
                throw new InvalidOperationException("Protected corridor has no supported native assay pose");
            player = session.player; player.enabled = false;
            var controller = player.GetComponent<CharacterController>(); controller.enabled = false;
            player.transform.SetPositionAndRotation(feet.position + Vector3.up * .02f, Quaternion.Euler(0, -90, 0));
            controller.enabled = true; player.eyes.transform.localRotation = Quaternion.identity;
            player.flashlight.enabled = true;
            player.flashlight.transform.SetPositionAndRotation(player.eyes.transform.position, player.eyes.transform.rotation);
            detection = player.GetComponent<DetectionFeedback>();
            if (!detection) throw new InvalidOperationException("Player feedback did not install detection");
            var agent = actor.GetComponent<NavMeshAgent>(); agent.enabled = false;
            actor.enabled = false; actor.transform.SetPositionAndRotation(origin.position, Quaternion.LookRotation(Vector3.right));
            actor.player = player; actor.patrolSpeed = actor.chaseSpeed = 0; actor.state = StalkerBrain.State.Patrol;
            actor.gameObject.SetActive(true);
            agent.enabled = true;
            if (!agent.Warp(origin.position)) throw new InvalidOperationException("Real actor could not bind to corridor NavMesh");
            Physics.SyncTransforms();
            capture = RouteAudioCapture.Attach(Path.Combine(output, "listener-audio"));
            yield return new WaitForSecondsRealtime(.25f);
            yield return Capture("01-calm.png");
            actor.enabled = true;
            deadline = Time.realtimeSinceStartup + 3;
            while (detection.Strength < .7f && Time.realtimeSinceStartup < deadline) yield return null;
            report.recognition = detection.CuesPlayed == 1 && actor.state == StalkerBrain.State.Chase && actor.CanSeePlayer();
            if (!report.recognition) throw new InvalidOperationException("Real sight did not earn recognition/Chase");
            yield return Capture("02-detection.png");
            yield return new WaitForSecondsRealtime(1.3f);
            var tension = player.GetComponent<PerceivedTension>();
            report.continuousChase = !detection.Active && detection.ChaseStrength > .95f &&
                detection.PeripheralStrength > .32f && detection.NoiseStrength >= .34f && tension && tension.TargetStress > .75f &&
                tension.InterferenceGain > 0 && tension.InterferenceSource && tension.InterferenceSource.isPlaying;
            yield return Capture("03-chased.png");
            session.Shell.Pause();
            float frozen = detection.ChaseStrength, stress = tension.Stress;
            yield return new WaitForSecondsRealtime(.2f);
            report.pauseFreezes = detection.ChaseStrength == frozen && tension.Stress == stress && detection.PeripheralStrength == 0 &&
                detection.DistortionStrength == 0 && detection.NoiseStrength == 0 && tension.InterferenceGain == 0;
            session.Shell.Resume();
            session.Shell.ToggleReducedMotion();
            yield return new WaitForSecondsRealtime(.25f);
            report.reducedMotionRespected = detection.Softened && detection.DistortionStrength == 0 &&
                detection.ChromaticStrength == 0 && detection.NoiseStrength == 0 && detection.PeripheralStrength <= .1641f &&
                tension.PulseSource.pitch == 1;
            yield return Capture("04-reduced-motion.png");
            session.Shell.ToggleReducedMotion();
            actor.enabled = false;
            yield return new WaitForSecondsRealtime(6.7f);
            report.returnedToCalm = detection.ChaseStrength == 0 && detection.PeripheralStrength == 0 &&
                detection.DistortionStrength == 0 && detection.NoiseStrength == 0 && tension.Stress <= .0001f &&
                tension.InterferenceGain == 0 && !tension.InterferenceSource.isPlaying;
            yield return Capture("05-escaped-pressure.png");
        }
        IEnumerator Capture(string name)
        {
            var view = session.GetComponent<GameShellView>();
            var document = session.GetComponent<UIDocument>();
            if (!view || view.Root == null || !document || !document.panelSettings)
                throw new InvalidOperationException("Production UI panel is unavailable");
            var settings = document.panelSettings;
            var oldTarget = settings.targetTexture;
            bool oldClear = settings.clearColor, oldDepth = settings.clearDepthStencil;
            float oldScale = Time.timeScale;
            var target = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear) {
                name = "Native production camera with actual HUD paint"
            };
            target.Create();
            int paintedPixels = 0, noiseAffectedPixels = 0;
            float noiseDifference = 0;
            try
            {
                if (!target.IsCreated() || target.sRGB) throw new InvalidOperationException("Native HDR capture target unavailable");
                Time.timeScale = 0;
                settings.targetTexture = target;
                settings.clearColor = false; settings.clearDepthStencil = true;
                // First settle the actual LateUpdate lens/veil parameters and panel
                // target layout at this frozen, already-earned recognition moment.
                yield return null; yield return null;
                Color[] world = null, combined = null;
                float until = Time.realtimeSinceStartup + 3;
                for (int frame = 0; frame < 12 && Time.realtimeSinceStartup < until; frame++)
                {
                    // The camera supplies the genuine floating-point tone-mapped
                    // world. Normal UI Toolkit rendering then paints the same colour
                    // buffer. Refresh the world before every paint to avoid stacking
                    // a translucent veil over last frame's veil.
                    RefreshProductionVolumes();
                    RenderPipeline.SubmitRenderRequest(player.eyes,
                        new UniversalRenderPipeline.SingleCameraRequest { destination = target });
                    world = ReadLinear(target);
                    view.Root.MarkDirtyRepaint();
                    yield return null;
                    combined = ReadLinear(target);
                    paintedPixels = 0;
                    for (int i = 0; i < world.Length; i++)
                    {
                        var difference = combined[i] - world[i];
                        if (Mathf.Abs(difference.r) + Mathf.Abs(difference.g) + Mathf.Abs(difference.b) > .003f) paintedPixels++;
                    }
                    if (frame >= 2 && paintedPixels > 32) break;
                }
                if (paintedPixels <= 32 || world == null || combined == null)
                    throw new InvalidOperationException("Existing UI Toolkit panel did not freshly paint the actual camera target");
                SaveLinear(name.Replace(".png", "-world.png"), world);
                SaveLinear(name, combined);
                if (detection.NoiseStrength > .001f)
                {
                    // An identical frozen production pose with only the actual
                    // grain component disabled proves the native render pass
                    // applies noise. UI, camera, lights and actors stay untouched.
                    var scope = detection.GetComponentsInChildren<Volume>().Single(volume =>
                        volume.sharedProfile && volume.sharedProfile.TryGet<FilmGrain>(out _));
                    scope.sharedProfile.TryGet<FilmGrain>(out var grain);
                    bool wasActive = grain.active;
                    float appliedNoise = detection.NoiseStrength;
                    try
                    {
                        RefreshProductionVolumes();
                        RenderPipeline.SubmitRenderRequest(player.eyes,
                            new UniversalRenderPipeline.SingleCameraRequest { destination = target });
                        var withNoise = ReadLinear(target);
                        SaveLinear(name.Replace(".png", "-world-noise-enabled.png"), withNoise);
                        grain.active = false;
                        RefreshProductionVolumes();
                        RenderPipeline.SubmitRenderRequest(player.eyes,
                            new UniversalRenderPipeline.SingleCameraRequest { destination = target });
                        var withoutNoise = ReadLinear(target);
                        SaveLinear(name.Replace(".png", "-world-noise-disabled.png"), withoutNoise);
                        double sum = 0; int tested = 0;
                        for (int y = 144; y < 576; y++) for (int x = 128; x < 1152; x++)
                        {
                            int i = y * 1280 + x;
                            var difference = withNoise[i] - withoutNoise[i];
                            float magnitude = (Mathf.Abs(difference.r) + Mathf.Abs(difference.g) + Mathf.Abs(difference.b)) / 3;
                            sum += magnitude; tested++;
                            if (magnitude > .003f) noiseAffectedPixels++;
                        }
                        noiseDifference = (float)(sum / tested);
                        bool rendered = noiseDifference > .001f && noiseAffectedPixels > 4000;
                        if (name == "02-detection.png") report.discoveryNoiseRendered = appliedNoise >= .5f && rendered;
                        if (name == "03-chased.png") report.pursuitNoiseRendered = appliedNoise >= .34f && rendered;
                    }
                    finally { grain.active = wasActive; RefreshProductionVolumes(); }
                }
            }
            finally
            {
                settings.targetTexture = oldTarget; settings.clearColor = oldClear; settings.clearDepthStencil = oldDepth;
                Time.timeScale = oldScale;
                target.Release(); Destroy(target);
            }
            var tension = player.GetComponent<PerceivedTension>();
            shots.Add(new Shot { name = name, state = actor.state.ToString(), wallSeconds = Time.realtimeSinceStartup - started,
                impact = detection.Strength, chase = detection.ChaseStrength, peripheral = detection.PeripheralStrength,
                distortion = detection.DistortionStrength, chromatic = detection.ChromaticStrength, noise = detection.NoiseStrength, cues = detection.CuesPlayed,
                nativeHudPixels = paintedPixels, noiseOnOffMeanDifference = noiseDifference, noiseAffectedPixels = noiseAffectedPixels,
                reducedMotion = detection.Softened, sight = actor.isActiveAndEnabled && actor.CanSeePlayer(),
                stress = tension ? tension.Stress : 0, targetStress = tension ? tension.TargetStress : 0,
                interferenceGain = tension ? tension.InterferenceGain : 0 });
        }
        void RefreshProductionVolumes()
        {
            // URP's standalone SingleCameraRequest reuses the current volume
            // stack. Re-evaluate native blending after this one-component toggle.
            var data = player.eyes.GetUniversalAdditionalCameraData();
            var trigger = data.volumeTrigger ? data.volumeTrigger : player.eyes.transform;
            VolumeManager.instance.Update(data.volumeStack ?? VolumeManager.instance.stack, trigger, data.volumeLayerMask);
        }
        static Color[] ReadLinear(RenderTexture target)
        {
            var old = RenderTexture.active;
            var texture = new Texture2D(target.width, target.height, TextureFormat.RGBAHalf, false, true);
            try
            {
                RenderTexture.active = target;
                texture.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); texture.Apply();
                return texture.GetPixels();
            }
            finally { RenderTexture.active = old; Destroy(texture); }
        }
        void SaveLinear(string name, Color[] pixels)
        {
            var png = new Texture2D(1280, 720, TextureFormat.RGB24, false, false);
            try
            {
                png.SetPixels32(GraphicsPresentationAudit.EncodeToneMappedLinearToSrgb(pixels)); png.Apply();
                File.WriteAllBytes(Path.Combine(output, name), png.EncodeToPNG());
            }
            finally { Destroy(png); }
        }
        void Finish(string failure)
        {
            if (finished) return; finished = true;
            try
            {
                RestorePreferences();
                report.audio = capture ? capture.Complete() : null;
                if (string.IsNullOrEmpty(failure) && (report.audio == null || !report.audio.nonSilent || report.audio.truncated ||
                    report.audio.nonfiniteSamples > 0 || !string.IsNullOrEmpty(report.audio.writerError)))
                    failure = "Native listener DSP evidence unavailable or invalid";
                report.failure = failure; report.errors = errors.ToArray(); report.shots = shots.ToArray();
                report.unity = Application.unityVersion; report.device = SystemInfo.graphicsDeviceName;
                report.width = Screen.width; report.height = Screen.height;
                bool passed = string.IsNullOrEmpty(failure) && errors.Count == 0 && shots.Count == 5 && report.recognition &&
                    report.continuousChase && report.returnedToCalm && report.reducedMotionRespected && report.pauseFreezes &&
                    report.discoveryNoiseRendered && report.pursuitNoiseRendered;
                report.status = passed ? "PASS" : "FAIL";
                File.WriteAllText(Path.Combine(output, "horror-feedback.json"), JsonUtility.ToJson(report, true));
                Debug.Log("HAPPYTOY_HORROR_FEEDBACK_" + report.status);
                Application.Quit(passed ? 0 : 2);
            }
            catch (Exception error) { Debug.LogError("Horror feedback report failed: " + error); Application.Quit(2); }
        }
        void PreservePreferences()
        {
            oldFloats = new float[floatKeys.Length]; hadFloats = new bool[floatKeys.Length];
            oldOptions = new int[optionKeys.Length]; hadOptions = new bool[optionKeys.Length];
            for (int i = 0; i < floatKeys.Length; i++)
            { hadFloats[i] = PlayerPrefs.HasKey(floatKeys[i]); oldFloats[i] = PlayerPrefs.GetFloat(floatKeys[i]); }
            for (int i = 0; i < optionKeys.Length; i++)
            { hadOptions[i] = PlayerPrefs.HasKey(optionKeys[i]); oldOptions[i] = PlayerPrefs.GetInt(optionKeys[i]); }
            preferencesSaved = true;
        }
        void RestorePreferences()
        {
            if (!preferencesSaved || preferencesRestored) return;
            // Flush the audit's temporary values to clear Shell's dirty bit first.
            // Its later OnApplicationQuit/OnDestroy must not rewrite our restored keys.
            if (session && session.Shell) session.Shell.SendMessage("SaveSettings", SendMessageOptions.DontRequireReceiver);
            for (int i = 0; i < floatKeys.Length; i++)
            { if (hadFloats[i]) PlayerPrefs.SetFloat(floatKeys[i], oldFloats[i]); else PlayerPrefs.DeleteKey(floatKeys[i]); }
            for (int i = 0; i < optionKeys.Length; i++)
            { if (hadOptions[i]) PlayerPrefs.SetInt(optionKeys[i], oldOptions[i]); else PlayerPrefs.DeleteKey(optionKeys[i]); }
            PlayerPrefs.Save(); preferencesRestored = true;
        }
        void OnDestroy() { RestorePreferences(); }
    }
}
