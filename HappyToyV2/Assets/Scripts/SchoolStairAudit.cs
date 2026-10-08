using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace HappyToy.V2
{
    // Opt-in native locomotion proof. Released actor snapshots isolate routing;
    // the separate PlayMode fixtures cover the real intro/release progression.
    public sealed class SchoolStairAudit : MonoBehaviour
    {
        string output;
        GameSession session;
        Camera reviewCamera;
        readonly List<string> errors = new List<string>();
        readonly List<Leg> legs = new List<Leg>();
        readonly RaycastHit[] floorHits = new RaycastHit[32];
        [Serializable] sealed class Sample { public Vector3 position; public float seconds; }
        [Serializable] sealed class Leg
        {
            public string name, image;
            public Vector3 from, target, arrived;
            public bool completePath, completed;
            public int intermediateSamples, unsupportedSamples;
            public float maximumFrameTravel;
            public Sample[] samples;
        }
        [Serializable] sealed class Report
        {
            public string status;
            public string scope = "Controlled native Windows NavMeshAgent locomotion of released school mask/Baby snapshots on authored stairs, both directions. No speed overrides or actor warps during travel. Separate tests cover release, pause and saved-state restoration; not a survival/performance certification.";
            public bool developmentBuild;
            public Leg[] legs;
            public string[] errors;
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            var args = Environment.GetCommandLineArgs(); int at = Array.IndexOf(args, "-v2-school-stair-output");
            if (at < 0 || at + 1 >= args.Length) return;
            new GameObject("Controlled native school stair proof").AddComponent<SchoolStairAudit>().output = args[at + 1];
        }
        void OnEnable() { Application.logMessageReceived += Log; }
        void OnDisable() { Application.logMessageReceived -= Log; }
        void Log(string message, string stack, LogType type)
        { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors.Add(message); }
        IEnumerator Start()
        {
            Directory.CreateDirectory(output); Application.runInBackground = true;
            var routine = Run();
            while (true)
            {
                bool more; object current;
                try { more = routine.MoveNext(); current = routine.Current; }
                catch (Exception error) { errors.Add(error.ToString()); break; }
                if (!more) break;
                // Nested iterators are driven here too, so a motion failure still
                // produces a finite report instead of silently ending the audit.
                if (current is IEnumerator nested)
                {
                    while (true)
                    {
                        bool next; object yielded;
                        try { next = nested.MoveNext(); yielded = nested.Current; }
                        catch (Exception error) { errors.Add(error.ToString()); break; }
                        if (!next) break;
                        yield return yielded;
                    }
                    if (errors.Count > 0) break;
                }
                else yield return current;
            }
            var report = new Report { developmentBuild = Debug.isDebugBuild, legs = legs.ToArray(), errors = errors.ToArray() };
            report.status = errors.Count == 0 && legs.Count == 4 && legs.TrueForAll(x => x.completed && x.completePath &&
                x.intermediateSamples > 10 && x.unsupportedSamples == 0) ? "PASS" : "FAIL";
            File.WriteAllText(Path.Combine(output, "school-stairs.json"), JsonUtility.ToJson(report, true));
            if (reviewCamera) Destroy(reviewCamera.gameObject);
            Application.Quit(report.status == "PASS" ? 0 : 2);
        }
        IEnumerator Run()
        {
            yield return new WaitForSecondsRealtime(1);
            session = GameSession.Current;
            session.ConfigureRecordDirectory(Path.Combine(output, "isolated-profile"));
            session.Shell.BeginChapter();
            if (!session.ChapterMode) throw new InvalidOperationException("School chapter failed to start");
            session.player.enabled = false;
            foreach (var actor in FindObjectsByType<StalkerBrain>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                actor.gameObject.SetActive(false);
                foreach (var startup in actor.GetComponents<NavMeshStartup>()) { startup.StopAllCoroutines(); startup.enabled = false; }
            }
            foreach (var startup in session.Chapter.Mask.GetComponents<NavMeshStartup>()) { startup.StopAllCoroutines(); startup.enabled = false; }
            session.Chapter.Mannequin.gameObject.SetActive(false);
            foreach (var door in FindObjectsByType<Interactable>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (door.kind == Interactable.Kind.Door) door.OpenForPursuer();
            yield return new WaitForSecondsRealtime(1.5f);
            reviewCamera = new GameObject("Controlled stair camera; player remains at entrance").AddComponent<Camera>();
            reviewCamera.CopyFrom(session.player.eyes); reviewCamera.enabled = false;
            var settings = reviewCamera.GetUniversalAdditionalCameraData();
            var source = session.player.eyes.GetUniversalAdditionalCameraData();
            settings.renderPostProcessing = source.renderPostProcessing; settings.volumeLayerMask = source.volumeLayerMask;
            var mask = session.Chapter.Mask;
            var maskState = mask.CaptureChapterProgress(); maskState.active = maskState.introComplete = true;
            maskState.state = LanternMaskEncounter.Phase.Wander; maskState.position = Landing(new Vector3(29.8f, 5, 22));
            mask.RestoreChapterProgress(maskState);
            yield return Travel(mask.GetComponent<NavMeshAgent>(), new Vector3(29.8f, 0, 10), mask.HearNoise, "mask-upper-descent", new Vector3(29.8f, 1.63f, 9));
            yield return Travel(mask.GetComponent<NavMeshAgent>(), new Vector3(29.8f, 5, 22), mask.HearNoise, "mask-upper-ascent", new Vector3(29.8f, 1.63f, 9));
            mask.gameObject.SetActive(false);
            var baby = session.Chapter.Nursery.monster;
            var babyState = baby.CaptureProgress(); babyState.active = true; babyState.state = StalkerBrain.State.Patrol;
            babyState.position = Landing(new Vector3(13.8f, -5, -22)); babyState.floor = -5;
            baby.RestoreChapterProgress(babyState, Array.Empty<Interactable>());
            yield return Travel(baby.GetComponent<NavMeshAgent>(), new Vector3(13.8f, 0, -10), baby.HearNoise, "baby-basement-ascent", new Vector3(13.8f, 1.63f, -9));
            yield return Travel(baby.GetComponent<NavMeshAgent>(), new Vector3(13.8f, -5, -22), baby.HearNoise, "baby-basement-descent", new Vector3(13.8f, 1.63f, -9));
        }
        static Vector3 Landing(Vector3 point)
        {
            if (!NavMesh.SamplePosition(point, out var sample, .3f, NavMesh.AllAreas) || Mathf.Abs(sample.position.y - point.y) > .25f)
                throw new InvalidOperationException("No physical stair landing: " + point);
            return sample.position;
        }
        IEnumerator Travel(NavMeshAgent agent, Vector3 target, Func<Vector3, float, bool> hear, string name, Vector3 camera)
        {
            var leg = new Leg { name = name, from = agent.transform.position, target = Landing(target) }; legs.Add(leg);
            var path = new NavMeshPath();
            leg.completePath = agent.CalculatePath(leg.target, path) && path.status == NavMeshPathStatus.PathComplete;
            if (!leg.completePath || !hear(leg.target, 2)) throw new InvalidOperationException("Stair target was rejected: " + name);
            float started = Time.time, deadline = Time.realtimeSinceStartup + 65, nextSample = 0;
            var samples = new List<Sample>(); var previous = leg.from; bool captured = false;
            while (Vector3.Distance(agent.transform.position, leg.target) > .65f && Time.realtimeSinceStartup < deadline)
            {
                var position = agent.transform.position; float movement = Vector3.Distance(previous, position);
                leg.maximumFrameTravel = Mathf.Max(leg.maximumFrameTravel, movement);
                if (movement > Mathf.Max(.4f, agent.speed * Time.deltaTime * 2 + .15f)) throw new InvalidOperationException("Noncontinuous stair movement: " + name);
                previous = position;
                if (Time.time >= nextSample)
                {
                    samples.Add(new Sample { position = position, seconds = Time.time - started }); nextSample = Time.time + .1f;
                    if (Mathf.Abs(position.y - leg.from.y) > .5f && Mathf.Abs(position.y - leg.target.y) > .5f) leg.intermediateSamples++;
                    bool supported = false;
                    int count = Physics.RaycastNonAlloc(position + Vector3.up * .3f, Vector3.down, floorHits, .9f,
                        Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                    for (int i = 0; i < count; i++)
                        if (!floorHits[i].collider.transform.IsChildOf(agent.transform) && Mathf.Abs(position.y - floorHits[i].point.y) < .45f) supported = true;
                    if (!supported) leg.unsupportedSamples++;
                }
                if (!captured && Mathf.Abs(position.y - (leg.from.y + leg.target.y) * .5f) < .3f)
                {
                    reviewCamera.transform.position = camera; reviewCamera.transform.LookAt(position + Vector3.up);
                    yield return new WaitForEndOfFrame(); Capture(name + ".png"); leg.image = name + ".png"; captured = true;
                }
                yield return null;
            }
            leg.arrived = agent.transform.position; leg.samples = samples.ToArray();
            leg.completed = Vector3.Distance(leg.arrived, leg.target) <= .65f && Mathf.Abs(leg.arrived.y - leg.target.y) < .2f;
            if (!leg.completed) throw new InvalidOperationException("Actor did not arrive on the target stair floor: " + name);
        }
        void Capture(string filename)
        {
            var target = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear); target.Create();
            RenderPipeline.SubmitRenderRequest(reviewCamera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
            var previous = RenderTexture.active; RenderTexture.active = target;
            var pixels = new Texture2D(1280, 720, TextureFormat.RGBAHalf, false, true);
            pixels.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); pixels.Apply(); var colours = pixels.GetPixels();
            for (int i = 0; i < colours.Length; i++) colours[i] = colours[i].gamma;
            var png = new Texture2D(1280, 720, TextureFormat.RGB24, false); png.SetPixels(colours); png.Apply();
            File.WriteAllBytes(Path.Combine(output, filename), png.EncodeToPNG());
            RenderTexture.active = previous; Destroy(pixels); Destroy(png); target.Release(); Destroy(target);
        }
    }
}
