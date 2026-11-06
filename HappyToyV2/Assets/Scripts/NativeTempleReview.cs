using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Rendering;

namespace HappyToy.V2
{
    // Explicit isolated player inspection. Poses/actions are scripted; ordinary
    // play and its input are untouched. No desktop/window input is injected.
    public sealed class NativeTempleReview : MonoBehaviour
    {
        // The central approach is stone. Start the separate forest-step audio
        // sample off that visible path, with an unobstructed forward route.
        static readonly Vector3 ForestWalkFeet = new Vector3(3, .04f, -20);
        static readonly Vector3 ForestWalkLook = new Vector3(3, 1.4f, 7);
        [Serializable] sealed class Mark { public string name; public float wall, game; public double dsp; }
        [Serializable] sealed class Timing
        {
            public string location; public int frames, gpuSamples;
            public double wallFps, cpuMedianMs, cpuP95Ms, gpuMedianMs, gpuP95Ms;
        }
        [Serializable] sealed class Result
        {
            public string scope = "Isolated Windows batch player; actual production eye camera and audio listener, scripted poses and interactions. Each timing submits one 1920x1080 offscreen eye render per update and synchronously reads one pixel to include GPU completion. Frames include readback/audit overhead and concurrent workloads; not presented/displayed FPS or manual play. Audio is the pre-device mixer, not device listening.";
            public string unity, device, graphics, phase, error;
        public bool batch, isolated, muted, frameTimingEnabled, corridorReached, candidateGate, walkCandidateGate, candidatePine, candidateAncient, candidateWide, candidateHall, candidateStone, candidateDepth, candidatePineBranches, candidatePineBranchesAll, productionAncient, productionPine, productionHall;
            public bool candidatePineFar, candidatePineFarAll, forcePineFar;
            public bool incenseHeldBeforePlacement, incenseHeldAfterPlacement;
            public int forestFootsteps; public string lastForestStep;
            public string[] images; public Mark[] marks; public Timing[] timings;
            public RouteAudioCapture.Report audio;
        }
        readonly Result result = new Result();
        readonly List<string> images = new List<string>();
        readonly List<Mark> marks = new List<Mark>();
        readonly List<Timing> timings = new List<Timing>();
        GameSession session; RouteAudioCapture capture; TempleGateReviewMotion reviewGate; string output, phase = "startup";
        bool walkGateReview;
        ProfilerRecorder gpuRecorder;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            var args = Environment.GetCommandLineArgs(); int i = Array.IndexOf(args, "-v2-temple-review-output");
            if (i < 0) return;
            if (i + 1 >= args.Length || !Application.isBatchMode || Application.platform != RuntimePlatform.WindowsPlayer ||
                !DiagnosticDesktopPolicy.SuppressCursorWrites || args.Contains("-noaudio") ||
                SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
            { Debug.LogError("Temple review requires an isolated graphics Windows batch player with audio."); Application.Quit(2); return; }
            string path = Path.GetFullPath(args[i + 1]);
            if (Directory.Exists(path)) { Debug.LogError("Temple review requires a fresh output directory."); Application.Quit(2); return; }
            Directory.CreateDirectory(path);
            var review = new GameObject("Explicit isolated temple review").AddComponent<NativeTempleReview>();
            review.output = path;
        }
        static void Require(bool ok, string message) { if (!ok) throw new InvalidOperationException(message); }
        IEnumerator Start()
        {
            Application.runInBackground = true;
            result.unity = Application.unityVersion; result.device = SystemInfo.graphicsDeviceName;
            result.graphics = SystemInfo.graphicsDeviceType.ToString(); result.batch = Application.isBatchMode;
            result.isolated = DiagnosticDesktopPolicy.SuppressCursorWrites;
            gpuRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "GPU Frame Time");
            var stack = new Stack<IEnumerator>(); stack.Push(Run()); string error = null;
            double deadline = Time.realtimeSinceStartupAsDouble + 240;
            while (stack.Count > 0 && error == null)
            {
                if (Time.realtimeSinceStartupAsDouble > deadline) { error = "Timed out at " + phase; break; }
                bool moved = false; object next = null;
                try { moved = stack.Peek().MoveNext(); if (moved) next = stack.Peek().Current; }
                catch (Exception ex) { error = ex.ToString(); }
                if (error != null) break;
                if (!moved) { (stack.Pop() as IDisposable)?.Dispose(); continue; }
                if (next is IEnumerator child) { stack.Push(child); continue; }
                yield return next;
            }
            while (stack.Count > 0) (stack.Pop() as IDisposable)?.Dispose();
            if (capture) result.audio = capture.Complete();
            result.phase = phase; result.error = error; result.images = images.ToArray();
            result.marks = marks.ToArray(); result.timings = timings.ToArray();
            result.frameTimingEnabled = FrameTimingManager.IsFeatureEnabled();
            gpuRecorder.Dispose(); DiagnosticAudioSilence.StopAndRestore();
            File.WriteAllText(Path.Combine(output, "review.json"), JsonUtility.ToJson(result, true));
            Application.Quit(error == null ? 0 : 2);
        }
        void MarkTime(string name) => marks.Add(new Mark { name = name, wall = Time.realtimeSinceStartup,
            game = session ? session.ElapsedPlayTime : 0, dsp = AudioSettings.dspTime });
        IEnumerator Until(Func<bool> ready, float seconds, string message)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (!ready() && Time.realtimeSinceStartup < end) yield return null;
            Require(ready(), message);
        }
        IEnumerator Run()
        {
            phase = "quiet-guard"; yield return DiagnosticAudioSilence.WaitForSafeAudio(output, true);
            Require(DiagnosticAudioSilence.DeviceSessionMuted, "Process audio session was not muted");
            result.muted = true; yield return null; yield return null;
            session = GameSession.Current;
            Require(session && session.player && session.Shell && session.Shell.Screen == GameShell.Page.Title, "Fresh title required");
            var forestMist = Resources.Load<Shader>("TempleForestAtmosphereMist");
            Require(forestMist && forestMist.isSupported, "Temple forest mist shader is unsupported in the player");
            var forestSky = Resources.Load<Shader>("TempleForestNightSky");
            Require(forestSky && forestSky.isSupported, "Temple forest night sky shader is unsupported in the player");
            session.ConfigureRecordDirectory(Path.Combine(output, "isolated-profile"));
            phase = "title-to-forest"; session.Shell.BeginTempleCinematic();
            yield return Until(() => session.PrologueMode && !session.Shell.IsReloading, 30, "Temple start did not finish");
            var forest = session.Prologue.GetComponent<TempleForestTrees>();
            result.productionAncient = forest && forest.AncientCount > 0;
            result.productionPine = forest && forest.PineCount > 0 && forest.TreeCount == 16;
            Require(result.productionAncient && result.productionPine, "Production forest trees are missing");
            result.productionHall = session.Prologue.GetComponent<TempleHallPresentation>();
            Require(result.productionHall, "Production temple hall is missing");
            var args = Environment.GetCommandLineArgs();
            bool pineOriginal = Array.IndexOf(args, "-v2-temple-pine-original-all") >= 0 ||
                Array.IndexOf(args, "-v2-temple-pine-original-candidate") >= 0;
            bool pineFar = Array.IndexOf(args, "-v2-temple-pine-far-original-all") >= 0 ||
                Array.IndexOf(args, "-v2-temple-pine-far-original-candidate") >= 0;
            Require(!(pineOriginal && pineFar), "Use one pine comparison at a time");
            if (Array.IndexOf(args, "-v2-temple-pine-far-original-all") >= 0)
            {
                session.Prologue.gameObject.AddComponent<TemplePineFarCandidate>().PrepareAll();
                result.candidatePineFarAll = true;
            }
            else if (Array.IndexOf(args, "-v2-temple-pine-far-original-candidate") >= 0)
            {
                session.Prologue.gameObject.AddComponent<TemplePineFarCandidate>().Prepare();
                result.candidatePineFar = true;
            }
            if (Array.IndexOf(args, "-v2-temple-pine-original-all") >= 0)
            {
                session.Prologue.gameObject.AddComponent<TemplePineBranchesCandidate>().PrepareAll();
                result.candidatePineBranchesAll = true;
            }
            else if (Array.IndexOf(args, "-v2-temple-pine-original-candidate") >= 0)
            {
                session.Prologue.gameObject.AddComponent<TemplePineBranchesCandidate>().Prepare();
                result.candidatePineBranches = true;
            }
            if (Array.IndexOf(args, "-v2-temple-depth-candidate") >= 0)
            {
                session.Prologue.gameObject.AddComponent<TempleForestDepthCandidate>().Prepare();
                result.candidateDepth = true;
            }
            if (Array.IndexOf(args, "-v2-temple-stone-candidate") >= 0)
            {
                session.Prologue.gameObject.AddComponent<TempleStonePathCandidate>().Prepare();
                result.candidateStone = true;
            }
            if (Array.IndexOf(args, "-v2-temple-pine-candidate") >= 0)
            { result.candidatePine = true; } // Compatibility flag; these pines are now the production art.
            if (Array.IndexOf(args, "-v2-temple-ancient-candidate") >= 0)
            {
                // Compatibility with the prior opt-in: the trees are now
                // always present, while this flag retains the extra close view.
                result.candidateAncient = true;
            }
            if (Array.IndexOf(args, "-v2-temple-wide-candidate") >= 0)
            {
                InstallWideTreeReview(session.Prologue);
                result.candidateWide = true;
            }
            if (Array.IndexOf(args, "-v2-temple-hall-candidate") >= 0)
            {
                // Compatibility flag: the reviewed hall is now production art.
                result.candidateHall = true;
            }
            result.forcePineFar = Array.IndexOf(args, "-v2-temple-force-pine-far") >= 0;
            if (result.forcePineFar)
            {
                Require(!result.candidatePineFar && !result.candidatePineBranches && !result.candidatePineBranchesAll,
                    "Force-far sampling requires either production pines or the all-pine far candidate");
                string forcedName = result.candidatePineFarAll ? "Candidate original far forest pine" : "Reviewed forest pine";
                for (int i = 0; i < 16; i++)
                {
                    if (i == 2 || i == 3) continue;
                    var visual = session.Prologue.transform.Find("Pine " + i + "/" + forcedName);
                    var group = visual ? visual.GetComponent<LODGroup>() : null;
                    Require(group, "Forced far pine is missing");
                    group.ForceLOD(1);
                }
            }
            walkGateReview = Array.IndexOf(args, "-v2-temple-gate-walk-candidate") >= 0;
            if (walkGateReview || Array.IndexOf(args, "-v2-temple-gate-candidate") >= 0)
            {
                reviewGate = walkGateReview ? session.Prologue.InstallGateWalkReview() : session.Prologue.InstallGateReview();
                result.candidateGate = true; result.walkCandidateGate = walkGateReview;
            }
            session.player.enabled = false; yield return null;
            AudioListener.volume = .8f; capture = RouteAudioCapture.Attach(Path.Combine(output, "audio"));
            MarkTime("first-day-forest");
            // Isolated review from inside each physical boundary, looking outward.
            // These poses never change the ordinary prologue or player saves.
            if (args.Contains("-v2-temple-horizon-review"))
            {
                Pose(new Vector3(-21,.04f,-2), new Vector3(-45,1.4f,-2)); yield return null; SaveCamera("horizon-west.png");
                Pose(new Vector3(21,.04f,-2), new Vector3(45,1.4f,-2)); yield return null; SaveCamera("horizon-east.png");
                Pose(new Vector3(0,.04f,-26), new Vector3(0,1.4f,-50)); yield return null; SaveCamera("horizon-south.png");
                Pose(new Vector3(0,.04f,28), new Vector3(0,1.4f,54)); yield return null; SaveCamera("horizon-north.png");
                Pose(new Vector3(-21,.04f,-26), new Vector3(-45,1.4f,-50)); yield return null; SaveCamera("horizon-southwest.png");
                Pose(new Vector3(21,.04f,-26), new Vector3(45,1.4f,-50)); yield return null; SaveCamera("horizon-southeast.png");
                Pose(new Vector3(-21,.04f,28), new Vector3(-45,1.4f,54)); yield return null; SaveCamera("horizon-northwest.png");
                Pose(new Vector3(21,.04f,28), new Vector3(45,1.4f,54)); yield return null; SaveCamera("horizon-northeast.png");
            }
            Pose(new Vector3(0,.04f,-20), new Vector3(0,1.4f,7)); yield return null; SaveCamera("01-forest-spawn.png");
            yield return Measure("forest-spawn");
            var eyePosition = session.player.eyes.transform.position;
            session.player.eyes.transform.position = eyePosition - Vector3.up * .65f;
            SaveCamera("01c-forest-low.png"); session.player.eyes.transform.position = eyePosition;
            Pose(new Vector3(-3.2f,.04f,-12), new Vector3(0,1.4f,12)); yield return null; SaveCamera("01d-forest-side.png");
            Pose(new Vector3(-3.3f,.04f,-20), new Vector3(-7.8f,2,-16.5f)); yield return null;
            SaveCamera("01e-ancient-close.png");
            Pose(new Vector3(2.5f,.04f,-19.3f), new Vector3(5.5f,2,-23)); yield return null; SaveCamera("01b-pine-close.png");
            // Compare the same physical tree at both LODs without moving the
            // camera, then restore ordinary distance selection before walking.
            var pineRoot = session.Prologue.transform.Find("Pine 1");
            string pineName = result.candidatePineFar || result.candidatePineFarAll ? "Candidate original far forest pine"
                : result.candidatePineBranches || result.candidatePineBranchesAll ? "Candidate original forest pine"
                : "Reviewed forest pine";
            var pineVisual = pineRoot ? pineRoot.Find(pineName) : null;
            var pineLods = pineVisual ? pineVisual.GetComponent<LODGroup>() : null;
            Require(pineLods, "Pine LOD comparison is missing");
            pineLods.ForceLOD(0); yield return null; SaveCamera("01f-pine-lod-near.png");
            pineLods.ForceLOD(1); yield return null; SaveCamera("01g-pine-lod-far.png");
            if (pineLods.lodCount > 2)
            {
                pineLods.ForceLOD(2); yield return null; SaveCamera("01h-pine-lod-very-far.png");
            }
            pineLods.ForceLOD(result.forcePineFar ? 1 : -1); yield return null;
            Pose(ForestWalkFeet, ForestWalkLook); yield return null;
            MarkTime("forest-walk-audio"); yield return WalkForAudio();
            Pose(new Vector3(0,.04f,-6), new Vector3(0,1.4f,12)); yield return null; SaveCamera("02-forest-approach.png");
            yield return Measure("forest-approach");
            var building = session.Prologue.transform.Find("Temple hall exterior");
            Require(building, "Temple model missing");
            var originalPosition = building.localPosition; var originalScale = building.localScale;
            var art = building.GetChild(0); var originalRotation = art.localRotation;
            for (int angle = 0; angle < 2; angle++)
            {
                art.localRotation = Quaternion.identity; building.localScale = Vector3.one;
                var rotation = angle == 0 ? Quaternion.identity : Quaternion.Euler(0, 180, 0);
                CorridorEntranceModels.GroundAtHeight(building, new Vector3(0, 0, 18), 6f, rotation);
                yield return null; SaveCamera("corrected-orientation-" + angle + ".png");
            }
            art.localRotation = originalRotation; building.localScale = originalScale; building.localPosition = originalPosition; yield return null;
            Pose(new Vector3(0,.04f,5.1f), new Vector3(0,1.0f,7.2f)); yield return null; SaveCamera("03-prayer-space.png");
            yield return Measure("prayer-space");
            phase = "first-prayer"; MarkTime("first-prayer");
            Require(session.Prologue.Director.PlaceIncense(), "First prayer did not start");
            var templePresentation = session.Prologue.GetComponent<TempleProloguePresentation>();
            var hands = session.player.Hands;
            Require(templePresentation && hands, "Incense hand presentation is unavailable");
            yield return Until(() => templePresentation.IncensePlaced ||
                hands.TryGetHeldRootWorld(PlayerItem.Incense, out _), 8, "Incense hand did not become ready");
            result.incenseHeldBeforePlacement = !templePresentation.IncensePlaced &&
                hands.TryGetHeldRootWorld(PlayerItem.Incense, out _);
            if (result.incenseHeldBeforePlacement) SaveHand("04a-hand-before-placement.png");
            yield return Until(() => templePresentation.IncensePlaced, 8, "Incense was not placed");
            yield return null; // Let the hands' LateUpdate consume the placement state.
            result.incenseHeldAfterPlacement = hands.TryGetHeldRootWorld(PlayerItem.Incense, out _);
            SaveHand("04b-hand-after-placement.png");
            Require(!result.incenseHeldAfterPlacement, "Placed incense remained visible in the hand");
            yield return new WaitForSeconds(1.3f); SaveCamera("04-first-incense.png");
            yield return Until(() => session.Prologue.Director.Phase == TemplePrologueSequence.Phase.FirstDeparture, 10, "First prayer did not finish");
            phase = "next-day"; Pose(new Vector3(0,.04f,-19), new Vector3(0,1.4f,7));
            yield return Until(() => session.Prologue.Director.Phase == TemplePrologueSequence.Phase.SecondApproach && !session.Shell.IsReloading, 20, "Second day did not finish");
            MarkTime("second-day-forest"); yield return null; SaveCamera("05-second-day.png");
            Pose(new Vector3(0,.04f,5.1f), new Vector3(0,1.0f,7.2f)); yield return null;
            phase = "second-prayer"; MarkTime("second-prayer");
            Require(session.Prologue.Director.PlaceIncense(), "Second prayer did not start");
            yield return Until(() => session.Prologue.GetComponent<TempleProloguePresentation>().IncensePlaced, 8, "Second incense was not placed");
            yield return new WaitForSeconds(1.3f); SaveCamera("06-second-incense.png");
            Pose(new Vector3(1.8f,.04f,7), new Vector3(0,1.5f,6.5f)); yield return null; SaveCamera("06b-smoke-side.png");
            Pose(new Vector3(0,.04f,5.1f), new Vector3(0,1.0f,7.2f)); yield return null;
            yield return Until(() => session.Prologue.Director.Phase == TemplePrologueSequence.Phase.GateRevealed, 10, "Gate phase did not begin");
            yield return null;
            Pose(new Vector3(0,.04f,-3), new Vector3(0,2,-8)); yield return null; SaveCamera("07-gate.png");
            yield return Measure("gate");
            phase = "corridor"; MarkTime("gate-open");
            var target = session.Prologue.transform.Find("나타난 대문");
            Require(target && target.gameObject.activeSelf, "Gate is absent");
            target.GetComponent<TemplePrologueTarget>().Use(session.player);
            if (reviewGate)
            {
                yield return Until(() => reviewGate.Progress >= .35f, 8, "Review gate did not begin opening");
                SaveCamera("07b-gate-opening.png");
                yield return Until(() => reviewGate.Progress >= .95f, 8, "Review gate did not finish opening");
                SaveCamera("07c-gate-open.png");
                // All views are captured in the same frame, before the masked
                // transition can advance. Return to the playable approach pose.
                Pose(new Vector3(-3.4f,.04f,-6), new Vector3(-1.8f,2,-8)); SaveCamera("07d-gate-open-left.png");
                Pose(new Vector3(3.4f,.04f,-6), new Vector3(1.8f,2,-8)); SaveCamera("07e-gate-open-right.png");
                Pose(new Vector3(0,.04f,-11), new Vector3(0,2,-8)); SaveCamera("07f-gate-open-back.png");
                Pose(new Vector3(0,.04f,-3), new Vector3(0,2,-8));
            }
            if (walkGateReview)
            {
                yield return Until(() => reviewGate.Progress >= 1, 8, "Review passage did not open");
                yield return WalkThroughGate();
            }
            yield return Until(() => session.CorridorMode && !session.Shell.IsReloading, 90, "Corridor transition did not finish");
            result.corridorReached = true; yield return null; SaveCamera("08-corridor.png"); MarkTime("corridor");
            yield return new WaitForSeconds(1); phase = "complete";
            Require(DiagnosticAudioSilence.DeviceSessionMuted, "Device mute was lost");
        }
        // A separate candidate replaces only one rendered tree. The original
        // root and collision capsule remain untouched for the comparison.
        public static void InstallWideTreeReview(TemplePrologueWorld world)
        {
            var prefab = Resources.Load<GameObject>("TempleForest/wide-zelkova-visual");
            var tree = world ? world.transform.Find("Pine 2") : null;
            var previous = tree ? tree.Find("Ancient forest tree") : null;
            var group = previous ? previous.GetComponent<LODGroup>() : null;
            if (!world || !world.Ready || !tree || !tree.GetComponent<CapsuleCollider>() ||
                !group || !group.enabled || tree.Find("Wide tree review") || !prefab || !prefab.GetComponent<LODGroup>() ||
                prefab.GetComponentInChildren<Collider>(true) || prefab.GetComponentInChildren<Rigidbody>(true))
                throw new InvalidOperationException("Fresh forest and visual-only wide tree are required");
            foreach (var level in group.GetLODs()) foreach (var renderer in level.renderers)
                if (renderer) renderer.enabled = false;
            group.enabled = false;
            var replacement = Instantiate(prefab, tree, false); replacement.name = "Wide tree review";
            replacement.transform.localPosition = previous.localPosition;
            replacement.transform.localRotation = previous.localRotation;
            replacement.transform.localScale = previous.localScale;
        }
        void Pose(Vector3 feet, Vector3 look)
        {
            Vector3 origin = TemplePrologueWorld.Origin;
            Vector3 heading = look - feet; heading.y = 0;
            session.player.RelocateWorld(origin + feet, Quaternion.LookRotation(heading));
            session.player.eyes.transform.rotation = Quaternion.LookRotation(origin + look - session.player.eyes.transform.position);
            Physics.SyncTransforms(); session.GetComponent<LocalShadowBudget>()?.RefreshNow();
        }
        IEnumerator WalkThroughGate()
        {
            var disabled = new List<InputDevice>(); var oldBackground = InputSystem.settings.backgroundBehavior;
            var previousKeyboard = Keyboard.current; Keyboard keyboard = null;
            var temple = session.Prologue;
            try
            {
                InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                foreach (var device in InputSystem.devices.ToArray())
                    if (device.enabled && (device is Keyboard || device is Mouse || device is Gamepad || device is Joystick))
                    { disabled.Add(device); InputSystem.DisableDevice(device); }
                keyboard = InputSystem.AddDevice<Keyboard>("Isolated gate traversal keyboard"); keyboard.MakeCurrent();
                session.player.enabled = true;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
                float end = Time.realtimeSinceStartup + 8;
                while (temple && !temple.TransitionRequested && Time.realtimeSinceStartup < end) yield return null;
                Require(temple && temple.TransitionRequested, "Walking through the review gate did not trigger the corridor");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null;
            }
            finally
            {
                session.player.enabled = false;
                if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
                foreach (var device in disabled) if (device.added) InputSystem.EnableDevice(device);
                if (previousKeyboard != null && previousKeyboard.added) previousKeyboard.MakeCurrent();
                InputSystem.settings.backgroundBehavior = oldBackground;
            }
        }
        IEnumerator WalkForAudio()
        {
            var disabled = new List<InputDevice>(); var oldBackground = InputSystem.settings.backgroundBehavior;
            var previousKeyboard = Keyboard.current; Keyboard keyboard = null;
            try
            {
                InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                foreach (var device in InputSystem.devices.ToArray())
                    if (device.enabled && (device is Keyboard || device is Mouse || device is Gamepad || device is Joystick))
                    { disabled.Add(device); InputSystem.DisableDevice(device); }
                keyboard = InputSystem.AddDevice<Keyboard>("Isolated temple review keyboard"); keyboard.MakeCurrent();
                session.player.enabled = true; int start = session.player.Feedback.FootstepsPlayed;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
                float end = Time.realtimeSinceStartup + 4;
                while (Time.realtimeSinceStartup < end) yield return null;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null;
                result.forestFootsteps = session.player.Feedback.FootstepsPlayed - start;
                result.lastForestStep = session.player.Feedback.LastFootstepClip ? session.player.Feedback.LastFootstepClip.name : "";
                Require(result.forestFootsteps > 0 && session.player.Feedback.LastFootstepSurface == "forest",
                    "Forest walking did not produce recorded steps");
            }
            finally
            {
                session.player.enabled = false;
                if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
                foreach (var device in disabled) if (device.added) InputSystem.EnableDevice(device);
                if (previousKeyboard != null && previousKeyboard.added) previousKeyboard.MakeCurrent();
                InputSystem.settings.backgroundBehavior = oldBackground;
            }
        }
        void SaveCamera(string filename)
        {
            var camera = session.player.eyes;
            var target = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var old = camera.targetTexture; var previous = RenderTexture.active; float aspect = camera.aspect; Texture2D frame = null;
            try
            {
                Require(target.Create(), "Camera target unavailable"); camera.targetTexture = target; camera.aspect = 1280f / 720;
                RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
                RenderTexture.active = target; frame = new Texture2D(1280, 720, TextureFormat.RGB24, false, false);
                frame.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); frame.Apply();
                File.WriteAllBytes(Path.Combine(output, filename), frame.EncodeToPNG()); images.Add(filename);
            }
            finally { camera.targetTexture = old; camera.aspect = aspect; RenderTexture.active = previous; if (frame) Destroy(frame); target.Release(); Destroy(target); }
        }
        void SaveHand(string filename)
        {
            var studio = session.player.transform.Find("First person hand studio");
            var camera = studio ? studio.GetComponent<Camera>() : null;
            Require(camera, "Player hand camera unavailable");
            var target = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var old = camera.targetTexture; var previous = RenderTexture.active; float aspect = camera.aspect; Texture2D frame = null;
            try
            {
                Require(target.Create(), "Hand target unavailable"); camera.targetTexture = target; camera.aspect = 1280f / 720;
                RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
                RenderTexture.active = target; frame = new Texture2D(1280, 720, TextureFormat.RGBA32, false, false);
                frame.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); frame.Apply();
                File.WriteAllBytes(Path.Combine(output, filename), frame.EncodeToPNG()); images.Add(filename);
            }
            finally { camera.targetTexture = old; camera.aspect = aspect; RenderTexture.active = previous; if (frame) Destroy(frame); target.Release(); Destroy(target); }
        }
        static double Percentile(List<double> values, float p)
        {
            if (values.Count == 0) return 0; values.Sort();
            return values[Mathf.CeilToInt((values.Count - 1) * p)];
        }
        IEnumerator Measure(string name)
        {
            var camera = session.player.eyes; var previous = camera.targetTexture; float aspect = camera.aspect;
            var previousActive = RenderTexture.active; var pixel = new Texture2D(1, 1, TextureFormat.RGB24, false);
            int oldFrameRate = Application.targetFrameRate, oldVsync = QualitySettings.vSyncCount;
            Application.targetFrameRate = 300; QualitySettings.vSyncCount = 0;
            var target = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
            Require(target.Create(), "Timing target unavailable"); camera.targetTexture = target; camera.aspect = 1920f / 1080;
            var cpu = new List<double>(); var gpu = new List<double>(); var ft = new FrameTiming[1]; ulong last = 0;
            try
            {
                for (int i = 0; i < 20; i++) { RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target }); FrameTimingManager.CaptureFrameTimings(); yield return null; }
                double start = Time.realtimeSinceStartupAsDouble;
                for (int i = 0; i < 120; i++)
                {
                    RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
                    RenderTexture.active = target; pixel.ReadPixels(new Rect(0, 0, 1, 1), 0, 0); pixel.Apply();
                    RenderTexture.active = previousActive;
                    FrameTimingManager.CaptureFrameTimings();
                    if (FrameTimingManager.GetLatestTimings(1, ft) > 0 && ft[0].frameStartTimestamp != last)
                    {
                        last = ft[0].frameStartTimestamp;
                        if (ft[0].cpuFrameTime > 0) cpu.Add(ft[0].cpuFrameTime);
                        if (ft[0].gpuFrameTime > 0) gpu.Add(ft[0].gpuFrameTime);
                    }
                    yield return null;
                }
                double seconds = Time.realtimeSinceStartupAsDouble - start;
                timings.Add(new Timing { location = name, frames = 120, gpuSamples = gpu.Count, wallFps = 120 / seconds,
                    cpuMedianMs = Percentile(cpu, .5f), cpuP95Ms = Percentile(cpu, .95f),
                    gpuMedianMs = Percentile(gpu, .5f), gpuP95Ms = Percentile(gpu, .95f) });
            }
            finally { camera.targetTexture = previous; camera.aspect = aspect; RenderTexture.active = previousActive;
                Destroy(pixel); target.Release(); Destroy(target);
                Application.targetFrameRate = oldFrameRate; QualitySettings.vSyncCount = oldVsync; }
        }
    }
}
