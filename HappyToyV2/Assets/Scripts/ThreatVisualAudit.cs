using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace HappyToy.V2
{
    // Explicit native art audit only. Production visual snapshots are cloned into
    // an isolated render layer; no gallery is installed in normal gameplay.
    public sealed class ThreatVisualAudit : MonoBehaviour
    {
        const int Layer = 31, Width = 1920, Height = 1080;
        string output;
        GameSession session;
        Camera reviewCamera;
        Light torch;
        readonly List<string> errors = new List<string>();
        readonly List<View> views = new List<View>();
        readonly GraphicsSurfaceLibrary.Pool surfaces = new GraphicsSurfaceLibrary.Pool();
        [Serializable] sealed class View
        {
            public string profile, name, image;
            public float distance, cameraFov;
            public bool torch, blocked, back;
            public Vector3 cameraPosition, faceCentre;
            public int leftAttributableRedPixels, rightAttributableRedPixels;
            public int totalAttributableRedPixels;
        }
        [Serializable] sealed class Report
        {
            public string status;
            public string scope = "Controlled native render-only clones of all six live production hostile faces. Close 3m torch on/off, 15m dark distance, rear view, and opaque panel occlusion use the release player's reviewCamera post processing/materials. Red-pixel counts compare cores on/off in the identical scene. No AI, route, gameplay survival, audio or performance certification.";
            public bool developmentBuild;
            public View[] views;
            public string[] errors;
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            var args = Environment.GetCommandLineArgs(); int at = Array.IndexOf(args, "-v2-threat-visual-output");
            if (at < 0 || at + 1 >= args.Length) return;
            new GameObject("Opt-in native paired-eye visual gallery").AddComponent<ThreatVisualAudit>().output = args[at + 1];
        }
        void OnEnable() => Application.logMessageReceived += Log;
        void OnDisable() => Application.logMessageReceived -= Log;
        void Log(string message, string trace, LogType type)
        { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors.Add(message); }
        IEnumerator Start()
        {
            Directory.CreateDirectory(output); Application.runInBackground = true;
            var run = Run();
            while (true)
            {
                bool next; object yielded;
                try { next = run.MoveNext(); yielded = run.Current; }
                catch (Exception error) { errors.Add(error.ToString()); break; }
                if (!next) break;
                yield return yielded;
            }
            bool visible = views.Where(view => !view.back && !view.blocked).All(view =>
                view.leftAttributableRedPixels > 0 && view.rightAttributableRedPixels > 0);
            bool occluded = views.Where(view => view.back || view.blocked).All(view => view.totalAttributableRedPixels == 0);
            var report = new Report { developmentBuild = Debug.isDebugBuild, views = views.ToArray(), errors = errors.ToArray() };
            report.status = errors.Count == 0 && views.Count == 30 && visible && occluded ? "PASS" : "FAIL";
            File.WriteAllText(Path.Combine(output, "threat-visuals.json"), JsonUtility.ToJson(report, true));
            if (reviewCamera) Destroy(reviewCamera.gameObject);
            surfaces.Dispose(); Application.Quit(report.status == "PASS" ? 0 : 2);
        }
        IEnumerator Run()
        {
            yield return new WaitForSecondsRealtime(1);
            session = GameSession.Current;
            if (!session) throw new InvalidOperationException("Production session is unavailable");
            session.ConfigureRecordDirectory(Path.Combine(output, "isolated-profile"));
            session.Shell.BeginChapter();
            if (!session.ChapterMode) throw new InvalidOperationException("Production school chapter did not start");
            session.player.enabled = false;
            foreach (var agent in FindObjectsByType<NavMeshAgent>(FindObjectsInactive.Include, FindObjectsSortMode.None)) agent.enabled = false;
            foreach (var startup in FindObjectsByType<NavMeshStartup>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            { startup.StopAllCoroutines(); startup.enabled = false; }
            foreach (var brain in FindObjectsByType<StalkerBrain>(FindObjectsInactive.Include, FindObjectsSortMode.None)) brain.enabled = false;
            foreach (var encounter in FindObjectsByType<LanternMaskEncounter>(FindObjectsInactive.Include, FindObjectsSortMode.None)) encounter.enabled = false;
            foreach (var encounter in FindObjectsByType<WeepingAngelEncounter>(FindObjectsInactive.Include, FindObjectsSortMode.None)) encounter.enabled = false;
            foreach (var motion in FindObjectsByType<V1MonsterMotion>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (!motion.GetComponent<StalkerBrain>()) continue;
                motion.gameObject.SetActive(true); motion.enabled = false;
                if (motion.animationPlayer)
                {
                    motion.animationPlayer.Stop();
                    var clip = motion.animationPlayer.GetClip("patrol");
                    if (clip) clip.SampleAnimation(motion.animationPlayer.gameObject, 0);
                    motion.animationPlayer.enabled = false;
                }
            }
            session.Chapter.Mask.gameObject.SetActive(true); session.Chapter.Mannequin.gameObject.SetActive(true);
            Time.timeScale = 0;
            yield return null;
            reviewCamera = new GameObject("Isolated native threat inspection reviewCamera").AddComponent<Camera>();
            reviewCamera.CopyFrom(session.player.eyes); reviewCamera.enabled = false; reviewCamera.cullingMask = 1 << Layer;
            reviewCamera.clearFlags = CameraClearFlags.SolidColor; reviewCamera.backgroundColor = new Color(.008f, .01f, .014f);
            reviewCamera.aspect = Width / (float)Height;
            var settings = reviewCamera.GetUniversalAdditionalCameraData(); var source = session.player.eyes.GetUniversalAdditionalCameraData();
            settings.renderPostProcessing = source.renderPostProcessing; settings.volumeLayerMask = source.volumeLayerMask;
            settings.antialiasing = source.antialiasing; settings.antialiasingQuality = source.antialiasingQuality;
            var sourceTorch = session.player.flashlight;
            torch = new GameObject("Gallery uses production torch settings").AddComponent<Light>();
            torch.transform.SetParent(reviewCamera.transform, false); torch.type = LightType.Spot;
            torch.color = sourceTorch.color; torch.intensity = sourceTorch.intensity; torch.range = sourceTorch.range;
            torch.spotAngle = sourceTorch.spotAngle; torch.innerSpotAngle = sourceTorch.innerSpotAngle;
            torch.cookie = sourceTorch.cookie; torch.cullingMask = 1 << Layer; torch.shadows = LightShadows.None;
            var all = FindObjectsByType<MonsterRedEyes>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (string key in new[] { "Cyclopse", "Uncat", "Hwacat_angry", "Baby", "LanternMask", "Mannequin" })
            {
                var eyes = all.FirstOrDefault(item => item.ProfileKey == key && item.Prepared);
                if (!eyes) throw new InvalidOperationException("Prepared live hostile face is missing: " + key);
                Transform visual;
                if (key == "LanternMask") visual = eyes.GetComponentInParent<LanternMaskEncounter>().mask;
                else if (key == "Mannequin") visual = eyes.GetComponentInParent<WeepingAngelEncounter>().visual;
                else visual = eyes.GetComponentInParent<V1MonsterMotion>().model;
                var snapshot = Instantiate(visual.gameObject); snapshot.name = key + " frozen production visual snapshot";
                snapshot.SetActive(true);
                foreach (var node in snapshot.GetComponentsInChildren<Transform>(true)) node.gameObject.layer = Layer;
                foreach (var behaviour in snapshot.GetComponentsInChildren<Behaviour>(true)) behaviour.enabled = false;
                if (snapshot.GetComponentsInChildren<Collider>(true).Length != 0 || snapshot.GetComponentsInChildren<NavMeshAgent>(true).Length != 0)
                    throw new InvalidOperationException("Gallery clone contains gameplay physics: " + key);
                var anchors = eyes.EyeAnchors.Select(anchor => snapshot.transform.Find(PathFrom(visual, anchor))).ToArray();
                var cores = eyes.Cores.Select(core => snapshot.transform.Find(PathFrom(visual, core.transform)).GetComponent<MeshRenderer>()).ToArray();
                var centre = (anchors[0].position + anchors[1].position) * .5f;
                snapshot.transform.position += new Vector3(1000, 10, 1000) - centre;
                centre = (anchors[0].position + anchors[1].position) * .5f;
                var forward = (anchors[0].forward + anchors[1].forward).normalized;
                var up = Vector3.ProjectOnPlane(Vector3.up, forward).normalized;
                if (up.sqrMagnitude < .5f) up = Vector3.ProjectOnPlane(snapshot.transform.up, forward).normalized;
                for (int index = 0; index < 5; index++)
                {
                    bool back = index == 3, blocked = index == 4, lit = index == 0;
                    float distance = index == 2 ? 15 : 3;
                    reviewCamera.fieldOfView = index == 2 ? session.player.eyes.fieldOfView : 36;
                    reviewCamera.transform.position = centre + forward * (back ? -distance : distance);
                    reviewCamera.transform.rotation = Quaternion.LookRotation(centre - reviewCamera.transform.position, up);
                    torch.enabled = lit;
                    GameObject blocker = null;
                    if (blocked)
                    {
                        blocker = new GameObject("Opaque render-only occlusion panel"); blocker.layer = Layer;
                        blocker.transform.position = (reviewCamera.transform.position + centre) * .5f;
                        blocker.transform.rotation = reviewCamera.transform.rotation; blocker.transform.localScale = new Vector3(1.5f, 1.3f, .04f);
                        var material = surfaces.Get("wood-aged", new Color(.15f, .12f, .09f));
                        blocker.AddComponent<MeshFilter>().sharedMesh = surfaces.MetreBoxMesh(blocker.transform.localScale, .001f, GraphicsSurfaceLibrary.TileSpan(material));
                        blocker.AddComponent<MeshRenderer>().sharedMaterial = material;
                    }
                    yield return null;
                    string name = index == 0 ? "close-torch-on" : index == 1 ? "close-torch-off" :
                        index == 2 ? "far-15m-torch-off" : index == 3 ? "rear-torch-off" : "opaque-panel-occlusion";
                    foreach (var core in cores) core.enabled = false;
                    var without = Capture(null);
                    foreach (var core in cores) core.enabled = true;
                    string filename = key + "-" + name + ".png";
                    var after = Capture(filename);
                    var view = new View { profile = key, name = name, image = filename, distance = distance,
                        cameraFov = reviewCamera.fieldOfView, torch = lit, back = back, blocked = blocked,
                        cameraPosition = reviewCamera.transform.position, faceCentre = centre };
                    view.leftAttributableRedPixels = CountRed(without, after, PixelBounds(cores[0]));
                    view.rightAttributableRedPixels = CountRed(without, after, PixelBounds(cores[1]));
                    view.totalAttributableRedPixels = CountRed(without, after, new RectInt(0, 0, Width, Height));
                    views.Add(view); if (blocker) Destroy(blocker);
                    yield return null;
                }
                Destroy(snapshot); yield return null;
            }
        }
        static string PathFrom(Transform root, Transform node)
        {
            var names = new Stack<string>();
            while (node != root)
            {
                if (!node) throw new InvalidOperationException("Eye anchor left its production visual root");
                names.Push(node.name); node = node.parent;
            }
            return string.Join("/", names);
        }
        RectInt PixelBounds(Renderer renderer)
        {
            var bounds = renderer.bounds; float minX = Width, minY = Height, maxX = 0, maxY = 0;
            for (int corner = 0; corner < 8; corner++)
            {
                var sign = new Vector3((corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1);
                var point = reviewCamera.WorldToViewportPoint(bounds.center + Vector3.Scale(bounds.extents, sign));
                minX = Mathf.Min(minX, point.x * Width); maxX = Mathf.Max(maxX, point.x * Width);
                minY = Mathf.Min(minY, point.y * Height); maxY = Mathf.Max(maxY, point.y * Height);
            }
            int x = Mathf.Clamp(Mathf.FloorToInt(minX) - 3, 0, Width), y = Mathf.Clamp(Mathf.FloorToInt(minY) - 3, 0, Height);
            return new RectInt(x, y, Mathf.Clamp(Mathf.CeilToInt(maxX) + 3, x, Width) - x,
                Mathf.Clamp(Mathf.CeilToInt(maxY) + 3, y, Height) - y);
        }
        static int CountRed(Color[] before, Color[] after, RectInt region)
        {
            int count = 0;
            for (int y = region.yMin; y < region.yMax; y++) for (int x = region.xMin; x < region.xMax; x++)
            {
                int index = y * Width + x; var delta = after[index] - before[index]; var pixel = after[index];
                if (delta.r > .035f && delta.r > Mathf.Abs(delta.g) * 1.5f && delta.r > Mathf.Abs(delta.b) * 1.5f &&
                    pixel.r > .16f && pixel.r > pixel.g * 1.5f && pixel.r > pixel.b * 1.5f) count++;
            }
            return count;
        }
        Color[] Capture(string filename)
        {
            var target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear); target.Create();
            var previous = RenderTexture.active;
            Texture2D pixels = null, png = null;
            try
            {
                RenderPipeline.SubmitRenderRequest(reviewCamera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
                RenderTexture.active = target; pixels = new Texture2D(Width, Height, TextureFormat.RGBAHalf, false, true);
                pixels.ReadPixels(new Rect(0, 0, Width, Height), 0, 0); pixels.Apply(); var colours = pixels.GetPixels();
                for (int index = 0; index < colours.Length; index++) colours[index] = colours[index].gamma;
                if (filename != null)
                {
                    png = new Texture2D(Width, Height, TextureFormat.RGB24, false); png.SetPixels(colours); png.Apply();
                    File.WriteAllBytes(Path.Combine(output, filename), png.EncodeToPNG());
                }
                return colours;
            }
            finally
            {
                RenderTexture.active = previous; if (pixels) Destroy(pixels); if (png) Destroy(png);
                target.Release(); Destroy(target);
            }
        }
    }
}
