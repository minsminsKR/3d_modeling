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
    // Opt-in, silent native rendering proof on actual corridor actors and pickups.
    public sealed class GameplayReadabilityProbe : MonoBehaviour
    {
        string output; bool finished; float started;
        readonly List<string> checks = new List<string>(), errors = new List<string>();
        [Serializable] sealed class Report
        {
            public string status, failure, unity, graphics;
            public string scope = "Controlled seed-73 native corridor. Actual NavMesh movement and imported rigs, enlarged patrol/chase gait, pickup outline visible/occluded image comparison. Brain isolated for repeatable art inspection; no survival balance certification.";
            public string[] checks, errors;
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)] static void Install()
        {
            var args = Environment.GetCommandLineArgs(); int index = Array.IndexOf(args, "-v2-readability-output");
            if (index >= 0 && index + 1 < args.Length)
                new GameObject("Explicit silent readability diagnostic").AddComponent<GameplayReadabilityProbe>().output = Path.GetFullPath(args[index + 1]);
        }
        void OnEnable() => Application.logMessageReceived += Log;
        void OnDisable() => Application.logMessageReceived -= Log;
        void Log(string message, string trace, LogType kind)
        { if (kind == LogType.Error || kind == LogType.Exception || kind == LogType.Assert) errors.Add(message); }
        void Update() { if (!finished && started > 0 && Time.realtimeSinceStartup - started > 120) Finish("Diagnostic timed out"); }
        IEnumerator Start()
        {
            started = Time.realtimeSinceStartup; Directory.CreateDirectory(output);
            var stack = new Stack<IEnumerator>(); stack.Push(Run());
            while (!finished)
            {
                bool moved = false; object next = null; string failure = null;
                try { moved = stack.Peek().MoveNext(); if (moved) next = stack.Peek().Current; }
                catch (Exception error) { failure = error.ToString(); }
                if (failure != null) { Finish(failure); yield break; }
                if (!moved) { stack.Pop(); if (stack.Count == 0) { Finish(null); yield break; } continue; }
                if (next is IEnumerator nested) { stack.Push(nested); continue; }
                yield return next;
            }
        }
        void Require(bool value, string check)
        { if (!value) throw new InvalidOperationException(check); checks.Add(check); }
        IEnumerator Run()
        {
            yield return DiagnosticAudioSilence.WaitForSafeAudio(output); yield return null; yield return null;
            var session = GameSession.Current; Require(session && session.player, "Actual game session");
            session.ConfigureRecordDirectory(Path.Combine(output, "isolated-profile"));
            session.CreateCorridor(73); session.Shell.Begin(); yield return null;
            var actors = FindObjectsByType<StalkerBrain>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var actor in actors) { actor.enabled = false; actor.gameObject.SetActive(false); }
            session.player.enabled = false; session.player.GetComponent<CharacterController>().enabled = false;
            session.player.flashlight.enabled = true;
            foreach (var door in session.Corridor.GetComponentsInChildren<Interactable>(true))
                if (door.kind == Interactable.Kind.Door) door.OpenForPursuer();
            yield return new WaitForSeconds(.8f);
            var camera = session.player.eyes;
            foreach (string key in new[] { "Cyclopse", "Uncat" })
            {
                var actor = actors.Single(x => x.name.EndsWith("— corridor") && x.name.Contains(key));
                actor.gameObject.SetActive(true); yield return null;
                var agent = actor.GetComponent<NavMeshAgent>(); var motion = actor.GetComponent<V1MonsterMotion>();
                Require(motion.LocomotionRigAvailable, key + " real locomotion joints");
                Vector3 start = actor.transform.position, destination = start; bool routeFound = false;
                for (int cell = 0; cell < 81 && !routeFound; cell++)
                {
                    start = session.Corridor.CellPosition(cell);
                    if (!NavMesh.SamplePosition(start, out var near, .4f, NavMesh.AllAreas)) continue;
                    for (int ray = 0; ray < 16; ray++)
                    {
                        var desired = near.position + Quaternion.Euler(0, ray * 22.5f, 0) * Vector3.forward * 8;
                        if (!NavMesh.SamplePosition(desired, out var far, .4f, NavMesh.AllAreas)) continue;
                        var path = new NavMeshPath();
                        if (!NavMesh.CalculatePath(near.position, far.position, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete) continue;
                        if (Physics.Linecast(near.position + Vector3.up, far.position + Vector3.up, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) continue;
                        start = near.position; destination = far.position; routeFound = true; break;
                    }
                }
                Require(routeFound, key + " supported actual straight corridor route");
                foreach (string action in new[] { "patrol", "chase" })
                {
                    agent.enabled = false; actor.transform.SetPositionAndRotation(start, Quaternion.LookRotation(destination - start));
                    foreach (var startup in actor.GetComponents<NavMeshStartup>()) startup.enabled = false;
                    actor.gameObject.SetActive(true); agent.enabled = true; Require(agent.Warp(start), key + " real route bind");
                    agent.isStopped = false; agent.speed = action == "patrol" ? 1.45f : 4.2f; agent.acceleration = 20;
                    actor.state = action == "patrol" ? StalkerBrain.State.Patrol : StalkerBrain.State.Chase;
                    Require(agent.SetDestination(destination), key + " actual destination");
                    for (int frame = 0; frame < 10; frame++)
                    {
                        yield return new WaitForSeconds(.08f);
                        ViewActor(session.player, motion, actor.transform);
                        Capture(camera, key + "-" + action + "-" + frame.ToString("D2") + ".png");
                    }
                    Require(Vector3.Distance(actor.transform.position, start) > .5f, key + " " + action + " actually moves");
                    Require(motion.LocomotionSwingDegrees >= (action == "patrol" ? 20 : 55), key + " " + action + " large limb amplitude");
                    Require(Mathf.Abs(motion.GroundGap) < .05f, key + " " + action + " grounded rendered skin");
                    actor.gameObject.SetActive(false);
                }
            }
            var items = session.Corridor.GetComponentsInChildren<Interactable>(true);
            foreach (var kind in new[] { Interactable.Kind.CorridorMemory, Interactable.Kind.FirecrackerSupply, Interactable.Kind.FlashlightBattery })
            {
                var item = items.First(x => x.kind == kind && x.InteractionAvailable);
                var outline = item.GetComponent<PickupOutline>(); Require(outline && outline.BorderCount > 0, kind + " outline resource");
                var source = item.GetComponentsInChildren<MeshRenderer>().First(x => x.enabled && x.name != "Pickup silhouette");
                ViewPoint(session.player, source.bounds.center, 1.5f, item.transform);
                var borders = item.GetComponentsInChildren<MeshRenderer>().Where(x => x.name == "Pickup silhouette" && x.enabled).ToArray();
                using (var images = new PixelPair(camera, borders))
                {
                    images.Capture(); Require(images.Changed > 5, kind + " visible white border pixels=" + images.Changed);
                    File.WriteAllBytes(Path.Combine(output, kind + "-outline.png"), images.Lit.EncodeToPNG());
                    var cover = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    var delta = source.bounds.center - camera.transform.position;
                    cover.transform.SetPositionAndRotation(camera.transform.position + delta * .5f, Quaternion.LookRotation(delta));
                    cover.transform.localScale = new Vector3(2, 2, .15f); Physics.SyncTransforms();
                    images.Capture(); Require(images.Changed == 0, kind + " border does not shine through opaque cover");
                    Destroy(cover); yield return null;
                }
            }
        }
        void ViewActor(PlayerMotor player, V1MonsterMotion motion, Transform actor)
        { ViewPoint(player, actor.position + Vector3.up * motion.PresentationHeight * .5f, 2.8f, actor); }
        void ViewPoint(PlayerMotor player, Vector3 point, float distance, Transform subject)
        {
            foreach (var direction in new[] { Vector3.right, Vector3.left, Vector3.forward, Vector3.back })
            {
                var desired = point + direction * distance; desired.y = .03f;
                if (!NavMesh.SamplePosition(desired, out var site, .45f, NavMesh.AllAreas)) continue;
                player.transform.position = site.position + Vector3.up * .03f; player.eyes.transform.LookAt(point); Physics.SyncTransforms();
                var delta = point - player.eyes.transform.position;
                if (Physics.RaycastAll(player.eyes.transform.position, delta.normalized, delta.magnitude,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore).All(hit =>
                        hit.collider.transform.IsChildOf(subject) || hit.collider.transform.IsChildOf(player.transform))) return;
            }
            throw new InvalidOperationException("No unobstructed actual camera site for " + point);
        }
        static Texture2D Render(Camera camera)
        {
            var target = new RenderTexture(1280, 720, 24); target.Create(); var previous = RenderTexture.active;
            RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
            RenderTexture.active = target; var image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); image.Apply();
            RenderTexture.active = previous; target.Release(); Destroy(target); return image;
        }
        void Capture(Camera camera, string name)
        { var image = Render(camera); File.WriteAllBytes(Path.Combine(output, name), image.EncodeToPNG()); Destroy(image); }
        sealed class PixelPair : IDisposable
        {
            readonly Camera camera; readonly MeshRenderer[] borders;
            public Texture2D Lit; Texture2D dark; public int Changed;
            public PixelPair(Camera view, MeshRenderer[] edges) { camera = view; borders = edges; }
            public void Capture()
            {
                Dispose(); foreach (var edge in borders) edge.enabled = false; dark = Render(camera);
                foreach (var edge in borders) edge.enabled = true; Lit = Render(camera);
                var a = Lit.GetPixels32(); var b = dark.GetPixels32(); Changed = 0;
                for (int i = 0; i < a.Length; i++) if (a[i].r > b[i].r + 20 && a[i].g > b[i].g + 20 && a[i].b > b[i].b + 20) Changed++;
            }
            public void Dispose() { if (Lit) Destroy(Lit); if (dark) Destroy(dark); }
        }
        void Finish(string failure)
        {
            if (finished) return; finished = true; bool passed = failure == null && errors.Count == 0;
            File.WriteAllText(Path.Combine(output, "readability.json"), JsonUtility.ToJson(new Report {
                status = passed ? "PASS" : "FAIL", failure = failure, unity = Application.unityVersion,
                graphics = SystemInfo.graphicsDeviceName, checks = checks.ToArray(), errors = errors.ToArray() }, true));
            Application.Quit(passed ? 0 : 1);
        }
    }
}
