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
            public string profile, name, image, emissionOffImage;
            public float distance, cameraFov;
            public bool torch, blocked, back;
            public Vector3 cameraPosition, faceCentre;
            public int eyeCount;
            public bool originalGeometryAndBaseMapsPreserved;
            public EyeView[] eyes;
            public int totalAttributableRedPixels;
        }
        [Serializable] sealed class EyeView
        {
            public int index, attributableRedPixels;
            public Vector3 anchor;
            public Vector2 pixelCentre;
            public float worldRadius, projectedRadius, pupilBefore, pupilAfter, irisBefore, irisAfter;
            public float centralRgbMeanError, centralRgbMaxError, centralBeforeDeviation, centralAfterDeviation, centralFeatureCorrelation;
            public int centralSamples;
            public bool centralPreservationApplicable, pupilContrastApplicable, originalCentralFeaturePreserved, originalPupilVisible;
        }
        sealed class Binding
        {
            public Renderer source, snapshot;
            public int[] slots;
            public readonly Dictionary<int, MaterialPropertyBlock> on = new Dictionary<int, MaterialPropertyBlock>();
        }
        [Serializable] sealed class Report
        {
            public string status;
            public string scope = "Controlled native render-only snapshots of all six live production hostile faces, including Cyclopse's single original eye. Close3m torch on/off, 15m dark distance, rear view and opaque-panel occlusion use production camera post processing/materials. Emission-on/off captures retain identical original meshes, renderer enabled states and BaseMap/iris/pupil textures; only indexed snapshot property-block emission colour changes. Original central RGB and existing local feature pattern are compared pixel-for-pixel; dark-pupil contrast is additionally required where the emission-off eye is actually dark. Empty mask apertures retain their original centre/mesh rather than gain a substitute eyeball. No AI, route, gameplay survival, audio or performance certification.";
            public bool developmentBuild;
            public View[] views;
            public string[] errors;
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            var args = Environment.GetCommandLineArgs(); int at = Array.IndexOf(args, "-v2-threat-visual-output");
            if (at < 0 || at + 1 >= args.Length) return;
            new GameObject("Opt-in original-eye preservation gallery").AddComponent<ThreatVisualAudit>().output = args[at + 1];
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
                view.eyes.Length == view.eyeCount && view.eyes.All(eye => eye.attributableRedPixels > 0));
            bool occluded = views.Where(view => view.back || view.blocked).All(view =>
                // The mask has open holes rather than a solid rear head.
                // Its original inner aperture lips can genuinely be visible
                // through those holes. Every opaque panel must still hide all
                // emission, and solid heads must hide it from behind.
                view.back && !view.blocked && view.profile == "LanternMask"
                    ? view.totalAttributableRedPixels == view.eyes.Sum(eye => eye.attributableRedPixels)
                    : view.totalAttributableRedPixels == 0);
            bool preserved = views.All(view => view.originalGeometryAndBaseMapsPreserved &&
                view.eyeCount == (view.profile == "Cyclopse" ? 1 : 2)) && views.Where(view => view.name == "close-torch-on")
                .All(view => view.eyes.All(eye => !eye.centralPreservationApplicable || eye.originalCentralFeaturePreserved));
            var report = new Report { developmentBuild = Debug.isDebugBuild, views = views.ToArray(), errors = errors.ToArray() };
            report.status = errors.Count == 0 && views.Count == 30 && visible && occluded && preserved ? "PASS" : "FAIL";
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
                // The original appearance gate may leave a visual child
                // inactive even though its AI-disabled actor root is active.
                // Activate the real source child before snapshotting so its
                // eye owner's OnEnable binds the owned emission material.
                visual.gameObject.SetActive(true);
                typeof(MonsterRedEyes).GetMethod("SetEmissionEnabled").Invoke(eyes, new object[] { true });
                var snapshot = Instantiate(visual.gameObject); snapshot.name = key + " frozen production visual snapshot";
                snapshot.SetActive(true);
                foreach (var node in snapshot.GetComponentsInChildren<Transform>(true)) node.gameObject.layer = Layer;
                foreach (var behaviour in snapshot.GetComponentsInChildren<Behaviour>(true)) behaviour.enabled = false;
                if (snapshot.GetComponentsInChildren<Collider>(true).Length != 0 || snapshot.GetComponentsInChildren<NavMeshAgent>(true).Length != 0)
                    throw new InvalidOperationException("Gallery clone contains gameplay physics: " + key);
                if (eyes.GetComponentsInChildren<Renderer>(true).Length != 0 || eyes.GetComponentsInChildren<MeshFilter>(true).Length != 0)
                    throw new InvalidOperationException("Opaque added eye geometry hides original eye anatomy: " + key);
                var anchors = eyes.EyeAnchors.Select(anchor => snapshot.transform.Find(PathFrom(visual, anchor))).ToArray();
                var radii = EyeRadii(eyes).ToArray();
                if (anchors.Length != (key == "Cyclopse" ? 1 : 2) || radii.Length != anchors.Length)
                    throw new InvalidOperationException("Native eye count/extent disagrees with anatomy: " + key);
                var bindings = EmissionRenderers(eyes).Select(renderer => new Binding { source = renderer,
                    snapshot = snapshot.transform.Find(PathFrom(visual, renderer.transform)).GetComponent<Renderer>(),
                    slots = AffectedSlots(eyes, renderer) }).ToArray();
                foreach (var binding in bindings)
                {
                    var global = new MaterialPropertyBlock(); binding.source.GetPropertyBlock(global); binding.snapshot.SetPropertyBlock(global);
                    for (int slot = 0; slot < binding.source.sharedMaterials.Length; slot++)
                    {
                        var originalBlock = new MaterialPropertyBlock(); binding.source.GetPropertyBlock(originalBlock, slot);
                        binding.snapshot.SetPropertyBlock(originalBlock, slot);
                    }
                    foreach (int slot in binding.slots)
                    {
                        var baseline = (Material)typeof(MonsterRedEyes).GetMethod("GetOriginalMaterial").Invoke(eyes, new object[] { binding.source, slot });
                        if (!OriginalAppearancePreserved(binding.source.sharedMaterials[slot], baseline))
                            throw new InvalidOperationException("Original BaseMap/iris/pupil material changed before gallery capture: " + key);
                        var originalBlock = new MaterialPropertyBlock(); binding.source.GetPropertyBlock(originalBlock, slot);
                        if (originalBlock.GetColor("_EmissionColor").maxColorComponent <= 0)
                            originalBlock.SetColor("_EmissionColor", EmissionColor(eyes, binding.source, slot));
                        binding.on.Add(slot, originalBlock);
                    }
                }
                var originalMeshes = bindings.Select(binding => OriginalMesh(binding.snapshot)).ToArray();
                var originalMaterials = bindings.Select(binding => binding.snapshot.sharedMaterials).ToArray();
                var originalEnabled = bindings.Select(binding => binding.snapshot.enabled).ToArray();
                var originalBaseMaps = originalMaterials.Select(materials => materials.Select(material => material.GetTexture("_BaseMap")).ToArray()).ToArray();
                var centre = anchors.Aggregate(Vector3.zero, (sum, anchor) => sum + anchor.position) / anchors.Length;
                snapshot.transform.position += new Vector3(1000, 10, 1000) - centre;
                centre = anchors.Aggregate(Vector3.zero, (sum, anchor) => sum + anchor.position) / anchors.Length;
                var forward = anchors.Aggregate(Vector3.zero, (sum, anchor) => sum + anchor.forward).normalized;
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
                    string filename = key + "-" + name + ".png";
                    string offFilename = key + "-" + name + "-emission-off.png";
                    ToggleSnapshot(bindings, false);
                    var without = Capture(offFilename);
                    ToggleSnapshot(bindings, true);
                    var after = Capture(filename);
                    var view = new View { profile = key, name = name, image = filename, distance = distance,
                        cameraFov = reviewCamera.fieldOfView, torch = lit, back = back, blocked = blocked,
                        cameraPosition = reviewCamera.transform.position, faceCentre = centre, emissionOffImage = offFilename,
                        eyeCount = anchors.Length, eyes = new EyeView[anchors.Length] };
                    view.originalGeometryAndBaseMapsPreserved = bindings.Select((binding, at) =>
                        OriginalMesh(binding.snapshot) == originalMeshes[at] && binding.snapshot.enabled == originalEnabled[at] &&
                        binding.snapshot.sharedMaterials.SequenceEqual(originalMaterials[at]) &&
                        binding.snapshot.sharedMaterials.Select(material => material.GetTexture("_BaseMap")).SequenceEqual(originalBaseMaps[at])).All(value => value);
                    for (int eye = 0; eye < anchors.Length; eye++)
                    {
                        var viewport = reviewCamera.WorldToViewportPoint(anchors[eye].position);
                        var extent = reviewCamera.WorldToViewportPoint(anchors[eye].position + reviewCamera.transform.right * radii[eye]);
                        var point = new Vector2(viewport.x * Width, viewport.y * Height);
                        float radius = Mathf.Max(1, Vector2.Distance(point, new Vector2(extent.x * Width, extent.y * Height)));
                        var observed = new EyeView { index = eye, anchor = anchors[eye].position, pixelCentre = point, worldRadius = radii[eye], projectedRadius = radius,
                            attributableRedPixels = CountRed(without, after, EyePixelBounds(point, radius)),
                            centralPreservationApplicable = lit && !back && !blocked };
                        if (observed.centralPreservationApplicable)
                        {
                            observed.pupilBefore = MedianLuminance(without, point, radius, 0, .12f);
                            observed.pupilAfter = MedianLuminance(after, point, radius, 0, .12f);
                            observed.irisBefore = MedianLuminance(without, point, radius, .42f, .85f);
                            observed.irisAfter = MedianLuminance(after, point, radius, .42f, .85f);
                            // Some original eyes are pale stone or contain a
                            // bright specular centre. Preserve their own RGB
                            // and local feature pattern rather than require
                            // every source asset to have a black pupil.
                            MeasureCentralFeature(without, after, point, radius, observed);
                            observed.pupilContrastApplicable = key != "LanternMask" &&
                                observed.irisBefore > observed.pupilBefore + .012f && observed.pupilBefore < observed.irisBefore * .85f;
                            bool contrastSurvives = !observed.pupilContrastApplicable ||
                                observed.irisAfter > observed.pupilAfter + .012f && observed.pupilAfter < observed.irisAfter * .85f;
                            bool patternSurvives = observed.centralBeforeDeviation < .003f ||
                                observed.centralAfterDeviation >= observed.centralBeforeDeviation * .75f && observed.centralFeatureCorrelation >= .95f;
                            observed.originalCentralFeaturePreserved = observed.centralSamples > 0 &&
                                observed.centralRgbMeanError <= .01f && observed.centralRgbMaxError <= .03f && patternSurvives && contrastSurvives;
                            observed.originalPupilVisible = observed.originalCentralFeaturePreserved;
                        }
                        view.eyes[eye] = observed;
                    }
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
        static IReadOnlyList<float> EyeRadii(MonsterRedEyes eyes) =>
            (IReadOnlyList<float>)typeof(MonsterRedEyes).GetProperty("EyeRadii").GetValue(eyes);
        static IReadOnlyList<Renderer> EmissionRenderers(MonsterRedEyes eyes) =>
            (IReadOnlyList<Renderer>)typeof(MonsterRedEyes).GetProperty("EmissionRenderers").GetValue(eyes);
        static int[] AffectedSlots(MonsterRedEyes eyes, Renderer renderer) =>
            (int[])typeof(MonsterRedEyes).GetMethod("GetAffectedSlots").Invoke(eyes, new object[] { renderer });
        static Color EmissionColor(MonsterRedEyes eyes, Renderer renderer, int slot) =>
            (Color)typeof(MonsterRedEyes).GetMethod("GetEmissionColor").Invoke(eyes, new object[] { renderer, slot });
        static Mesh OriginalMesh(Renderer renderer) => renderer is SkinnedMeshRenderer skin ? skin.sharedMesh : renderer.GetComponent<MeshFilter>().sharedMesh;
        static bool OriginalAppearancePreserved(Material actual, Material original)
        {
            if (!actual || !original || actual.shader != original.shader ||
                !actual.shaderKeywords.Where(keyword => keyword != "_EMISSION").OrderBy(keyword => keyword)
                    .SequenceEqual(original.shaderKeywords.Where(keyword => keyword != "_EMISSION").OrderBy(keyword => keyword))) return false;
            for (int index = 0; index < original.shader.GetPropertyCount(); index++)
            {
                string property = original.shader.GetPropertyName(index);
                if (property == "_EmissionColor" || property == "_EmissionMap") continue;
                switch (original.shader.GetPropertyType(index))
                {
                    case ShaderPropertyType.Color: if (actual.GetColor(property) != original.GetColor(property)) return false; break;
                    case ShaderPropertyType.Vector: if (actual.GetVector(property) != original.GetVector(property)) return false; break;
                    case ShaderPropertyType.Float:
                    case ShaderPropertyType.Range: if (actual.GetFloat(property) != original.GetFloat(property)) return false; break;
                    case ShaderPropertyType.Texture:
                        if (actual.GetTexture(property) != original.GetTexture(property) ||
                            actual.GetTextureScale(property) != original.GetTextureScale(property) ||
                            actual.GetTextureOffset(property) != original.GetTextureOffset(property)) return false; break;
                }
            }
            return true;
        }
        static void ToggleSnapshot(IEnumerable<Binding> bindings, bool enabled)
        {
            foreach (var binding in bindings) foreach (int slot in binding.slots)
            {
                if (enabled) binding.snapshot.SetPropertyBlock(binding.on[slot], slot);
                else
                {
                    var block = new MaterialPropertyBlock(); binding.snapshot.GetPropertyBlock(block, slot);
                    block.SetColor("_EmissionColor", Color.black); binding.snapshot.SetPropertyBlock(block, slot);
                }
            }
        }
        static RectInt EyePixelBounds(Vector2 point, float radius)
        {
            float extent = radius * 1.25f + 3;
            int x = Mathf.Clamp(Mathf.FloorToInt(point.x - extent), 0, Width), y = Mathf.Clamp(Mathf.FloorToInt(point.y - extent), 0, Height);
            return new RectInt(x, y, Mathf.Clamp(Mathf.CeilToInt(point.x + extent), x, Width) - x,
                Mathf.Clamp(Mathf.CeilToInt(point.y + extent), y, Height) - y);
        }
        static float MedianLuminance(Color[] pixels, Vector2 centre, float radius, float inner, float outer)
        {
            var samples = new List<float>(); var region = EyePixelBounds(centre, radius);
            for (int y = region.yMin; y < region.yMax; y++) for (int x = region.xMin; x < region.xMax; x++)
            {
                float distance = Vector2.Distance(new Vector2(x + .5f, y + .5f), centre) / radius;
                if (distance < inner || distance > outer) continue;
                var pixel = pixels[y * Width + x]; samples.Add(pixel.r * .2126f + pixel.g * .7152f + pixel.b * .0722f);
            }
            if (samples.Count == 0) return 0;
            samples.Sort(); return samples[samples.Count / 2];
        }
        static void MeasureCentralFeature(Color[] before, Color[] after, Vector2 centre, float radius, EyeView result)
        {
            var region = EyePixelBounds(centre, radius);
            double sumBefore = 0, sumAfter = 0, squaresBefore = 0, squaresAfter = 0, products = 0, errors = 0;
            float maximum = 0; int count = 0;
            for (int y = region.yMin; y < region.yMax; y++) for (int x = region.xMin; x < region.xMax; x++)
            {
                // Radius is the widest eye extent. Inspect the pupil core:
                // a larger circular window crosses the deliberately glowing
                // iris of narrow, tilted eyes, even with an untouched pupil.
                if (Vector2.Distance(new Vector2(x + .5f, y + .5f), centre) > radius * .12f) continue;
                int index = y * Width + x; var a = before[index]; var b = after[index];
                float dr = Mathf.Abs(b.r - a.r), dg = Mathf.Abs(b.g - a.g), db = Mathf.Abs(b.b - a.b);
                errors += (dr + dg + db) / 3; maximum = Mathf.Max(maximum, Mathf.Max(dr, Mathf.Max(dg, db)));
                double la = a.r * .2126 + a.g * .7152 + a.b * .0722, lb = b.r * .2126 + b.g * .7152 + b.b * .0722;
                sumBefore += la; sumAfter += lb; squaresBefore += la * la; squaresAfter += lb * lb; products += la * lb; count++;
            }
            result.centralSamples = count; if (count == 0) return;
            double varianceBefore = Math.Max(0, squaresBefore / count - Math.Pow(sumBefore / count, 2));
            double varianceAfter = Math.Max(0, squaresAfter / count - Math.Pow(sumAfter / count, 2));
            double covariance = products / count - sumBefore / count * (sumAfter / count);
            result.centralRgbMeanError = (float)(errors / count); result.centralRgbMaxError = maximum;
            result.centralBeforeDeviation = (float)Math.Sqrt(varianceBefore); result.centralAfterDeviation = (float)Math.Sqrt(varianceAfter);
            result.centralFeatureCorrelation = varianceBefore < 1e-10 || varianceAfter < 1e-10 ?
                (maximum <= .01f ? 1 : 0) : (float)Math.Max(-1, Math.Min(1, covariance / Math.Sqrt(varianceBefore * varianceAfter)));
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
