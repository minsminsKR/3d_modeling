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
    // Explicit quiet native proof. Never installed in ordinary play.
    public sealed class CabinetExpressionAudit : MonoBehaviour
    {
        string output, creature = "Cyclopse"; float started; bool finished, taunt;
        readonly List<string> checks = new List<string>(), errors = new List<string>();
        [Serializable] sealed class Report
        {
            public string status, failure, unity, creature;
            public bool repeatedEntry;
            public string scope = "Fresh native controlled creature chase/cabinet scan/retreat/rush/defeat, actual lit shader/camera renders. Not a survival playthrough or audio listening certification.";
            public string[] checks, errors;
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            var args = Environment.GetCommandLineArgs(); int index = Array.IndexOf(args, "-v2-cabinet-expression-output");
            if (index >= 0 && index + 1 < args.Length)
            {
                var audit = new GameObject("Explicit quiet cabinet expression audit").AddComponent<CabinetExpressionAudit>();
                audit.output = args[index + 1];
                int key = Array.IndexOf(args, "-v2-expression-creature");
                if (key >= 0 && key + 1 < args.Length) audit.creature = args[key + 1];
                audit.taunt=Array.IndexOf(args,"-v2-cabinet-taunt")>=0;
            }
        }
        void OnEnable() => Application.logMessageReceived += Log;
        void OnDisable() => Application.logMessageReceived -= Log;
        void Log(string message, string trace, LogType type) { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors.Add(message); }
        void Update() { if (!finished && started > 0 && Time.realtimeSinceStartup - started > 100) Finish("Native cabinet audit timed out"); }
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
                if (next is IEnumerator child) { stack.Push(child); continue; } yield return next;
            }
        }
        void Require(bool value, string text) { if (!value) throw new InvalidOperationException(text); checks.Add(text); }
        IEnumerator Wait(Func<bool> ready, float seconds, string text)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (!ready() && Time.realtimeSinceStartup < end) yield return null;
            Require(ready(), text);
        }
        void Place(PlayerMotor player, Vector3 position)
        { var capsule = player.GetComponent<CharacterController>(); capsule.enabled = false; player.transform.position = position; capsule.enabled = true; Physics.SyncTransforms(); }
        void Prepare(StalkerBrain actor, PlayerMotor player, Vector3 point, Vector3 entrance)
        {
            actor.gameObject.SetActive(false); var agent = actor.GetComponent<NavMeshAgent>(); agent.enabled = false;
            actor.transform.SetPositionAndRotation(point, Quaternion.LookRotation(entrance - point));
            agent.enabled = true; actor.player = player; actor.enabled = true; actor.gameObject.SetActive(true); agent.Warp(point);
            actor.state = StalkerBrain.State.Chase; actor.chaseSpeed = 0;
        }
        IEnumerator Run()
        {
            yield return DiagnosticAudioSilence.WaitForSafeAudio(output, false); yield return null; yield return null;
            var session = GameSession.Current; var player = session.player;
            foreach (var actor in FindObjectsByType<StalkerBrain>(FindObjectsInactive.Include, FindObjectsSortMode.None)) actor.gameObject.SetActive(false);
            foreach (var behaviour in FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (behaviour is LanternMaskEncounter || behaviour is WeepingAngelEncounter || behaviour is StoryDirector ||
                    behaviour is AnnexEncounter || behaviour is UncatAnnexEvent || behaviour is V1HwacatEvent) behaviour.enabled = false;
            session.ConfigureRecordDirectory(Path.Combine(output, "isolated-profile")); session.Shell.Begin(); player.enabled = false;
            var cabinet = FindObjectsByType<Interactable>(FindObjectsSortMode.None).Single(item => item.name == "음악실 은신함");
            var entrance = cabinet.outside.position; var away = entrance - cabinet.inside.position; away.y = 0; away.Normalize();
            Require(NavMesh.SamplePosition(entrance + away * 3, out var anchor, .6f, NavMesh.AllAreas), "Real cabinet approach on NavMesh");
            var cyclopse = FindObjectsByType<StalkerBrain>(FindObjectsInactive.Include, FindObjectsSortMode.None).First(actor => actor.name.Contains(creature));
            Place(player, entrance); Prepare(cyclopse, player, anchor.position, entrance);
            var face = cyclopse.GetComponent<EnemyChaseFace>();
            yield return Wait(() => face && face.Weight > .95f, 3, "Actual chase raised generated face");
            Require(cyclopse.GetComponentInChildren<SkinnedMeshRenderer>().sharedMaterials.All(m => m.GetTexture("_ChaseFace")), "Actual native face materials retained their generated textures");
            Capture(session, cyclopse, "01-chase-face.png");
            cyclopse.chaseSpeed = 3.5f; player.HidingRandomSample = () => .2f; cabinet.Use(player);
            Require(player.Hidden && player.HidingOutcome == CabinetHidingOutcome.Survived, "Actual protected cabinet entry");
            yield return Wait(() => cyclopse.CabinetStage == "Inspect" && face.Weight == 0, 10, "Calm face restored during cabinet scan");
            Capture(session, cyclopse, "02-calm-scan.png");
            var facing = cyclopse.transform.rotation; yield return new WaitForSeconds(.3f);
            Require(Quaternion.Angle(facing, cyclopse.transform.rotation) > 5, "Visible look-around at the cabinet");
            session.Shell.Pause(); float weight = face.Weight; var position = cyclopse.transform.position;
            yield return new WaitForSecondsRealtime(.2f);
            Require(face.Weight == weight && cyclopse.transform.position == position, "Pause froze expression and navigation"); session.Shell.Resume();
            yield return Wait(() => cyclopse.CabinetStage == "Complete", 12, "Successful hiding completed physical retreat");
            Require(!session.Finished && Vector3.Distance(cyclopse.transform.position, entrance) > 2.5f, "Retreat left the player alive");
            cyclopse.gameObject.SetActive(false); Physics.SyncTransforms(); player.LeaveHiding(); Require(!player.Hidden, "Normal cabinet exit remained usable");
            if(taunt)
            {
                for(int entry=2;entry<=3;entry++)
                {
                    Place(player,entrance);Prepare(cyclopse,player,anchor.position,entrance);
                    yield return Wait(()=>face.Weight>.95f,3,"Repeat pursuit restored the generated face and eye owner");
                    if(entry==3)cyclopse.state=StalkerBrain.State.Patrol; // Same-frame repeat cannot exploit the awareness delay.
                    cyclopse.chaseSpeed=3.5f;player.HidingRandomSample=()=>0;cabinet.Use(player);
                    Require(player.CabinetTauntCount==entry,"One actual close witness per entry: "+entry);
                    if(entry==2)
                    {
                        Require(player.HidingProtected && !player.HidingForcedCapture,"Second overt entry still permits its single normal result");
                        yield return Wait(()=>cyclopse.CabinetStage=="Complete",15,"Second protected entry retreated normally");
                        cyclopse.gameObject.SetActive(false);Physics.SyncTransforms();player.LeaveHiding();
                        Require(!player.Hidden,"Second ordinary exit remained usable");
                    }
                }
                Require(player.HidingForcedCapture && !player.HidingProtected,"Third entry forced detection despite zero-valued safe RNG");
                int id=player.HidingEntryId;cabinet.Use(player);
                Require(player.Hidden && player.HidingEntryId==id,"Immediate exit/re-entry could not reroll the committed capture");
                yield return Wait(()=>cyclopse.AttackActive && face.Weight>.95f,8,"Third entry physically rushed to its fresh door warning");
                Capture(session,cyclopse,"04-third-entry-forced-capture.png");
                yield return Wait(()=>session.Finished,3,"Third entry committed its physical strike");
                Require(!session.Escaped && player.HidingRolls==3,"Exactly three shared decisions ended in certain capture");
                yield break;
            }
            Place(player, entrance); Prepare(cyclopse, player, anchor.position, entrance);
            yield return Wait(() => face.Weight > .95f, 3, "Fresh pursuit reactivated the same face owner");
            cyclopse.chaseSpeed = 3.5f; player.HidingRandomSample = () => .95f; cabinet.Use(player);
            Require(player.Hidden && player.HidingOutcome == CabinetHidingOutcome.Defeated, "Single vulnerable entry result retained");
            yield return Wait(() => cyclopse.CabinetStage == "Inspect" && face.Weight == 0, 10, "Risky entry first returned to calm scan");
            yield return Wait(() => cyclopse.AttackActive && face.Weight > .95f, 10, "Detection restored grin and physically rushed to door warning");
            Capture(session, cyclopse, "03-detected-rush-face.png");
            yield return Wait(() => session.Finished, 3, "Fresh cabinet strike killed after warning");
            Require(!session.Escaped && player.HidingRolls == 2, "Defeat used two entries and exactly two shared decisions");
        }
        void Capture(GameSession session, StalkerBrain actor, string filename)
        {
            var eyes = actor.GetComponentInChildren<MonsterRedEyes>().EyeAnchors.ToArray();
            var centre = eyes.Aggregate(Vector3.zero, (sum, eye) => sum + eye.position) / eyes.Length - Vector3.up * .10f;
            var forward = actor.transform.forward;
            if (eyes.Length == 2) { forward = Vector3.Cross(eyes[1].position - eyes[0].position, Vector3.up).normalized; if (Vector3.Dot(forward, actor.transform.forward) < 0) forward = -forward; }
            var camera = new GameObject("Owned native expression proof camera").AddComponent<Camera>(); camera.CopyFrom(session.player.eyes); camera.enabled = false;
            camera.aspect = 1; camera.nearClipPlane = .01f;
            var lamp = camera.gameObject.AddComponent<Light>(); lamp.type = LightType.Spot; lamp.range = 8;
            lamp.spotAngle = 90;
            var target = new RenderTexture(768, 768, 24); target.Create(); var image = new Texture2D(768, 768, TextureFormat.RGB24, false);
            try
            {
                var right = Vector3.Cross(Vector3.up, forward).normalized;
                bool captured = false; int attempt = 0;
                // A clear centre ray can still put the lens beside the cabinet's
                // edge. Check the actual image, then try closer/oblique viewpoints
                // while keeping all world geometry and the same quality limits.
                foreach (var direction in new[] { forward, (forward + right * .65f).normalized,
                    (forward - right * .65f).normalized, (forward + Vector3.up * .5f).normalized })
                {
                    foreach (float distance in new[] { 1.8f, 1.2f, .8f, .6f, .4f, .3f })
                    {
                        var position = centre + direction * distance;
                        if (Physics.Linecast(position, centre, out var hit, ~0, QueryTriggerInteraction.Ignore) && !hit.transform.IsChildOf(actor.transform)) continue;
                        camera.transform.position = position; camera.transform.LookAt(centre);
                        camera.fieldOfView = Mathf.Clamp(2 * Mathf.Atan(.55f / distance) * Mathf.Rad2Deg, 30, 90);
                        lamp.intensity = 3 * distance * distance / (1.8f * 1.8f);
                        RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
                        var previous = RenderTexture.active; RenderTexture.active = target; image.ReadPixels(new Rect(0, 0, 768, 768), 0, 0); image.Apply(); RenderTexture.active = previous;
                        var pixels = image.GetPixels32();
                        Require(pixels.Count(p => p.r > 220 && p.b > 220 && p.g < 60) < 30, filename + " retained native shader");
                        if (pixels.Count(p => p.r > 248 && p.g > 248 && p.b > 248) >= pixels.Length / 2)
                        { File.WriteAllBytes(Path.Combine(output, filename + ".rejected-" + attempt++ + ".png"), image.EncodeToPNG()); continue; }
                        Require(true, filename + " close-up camera remained outside opaque cabinet geometry");
                        Require(true, filename + " was not an overexposed cabinet wall");
                        File.WriteAllBytes(Path.Combine(output, filename), image.EncodeToPNG());
                        VerifyEyeEmission(actor,camera,target,image,pixels,filename);
                        captured = true; break;
                    }
                    if (captured) break;
                }
                Require(captured, filename + " found a clear native face portrait without hiding world geometry");
            }
            finally { target.Release(); Destroy(target); Destroy(image); Destroy(camera.gameObject); }
        }
        void VerifyEyeEmission(StalkerBrain actor,Camera camera,RenderTexture target,Texture2D image,Color32[] on,string filename)
        {
            var eyes=actor.GetComponentInChildren<MonsterRedEyes>();Require(eyes.EmissionEnabled,filename+" retained its active eye-glow owner");
            Color32[] off;
            try
            {
                eyes.SetEmissionEnabled(false);
                RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest {destination=target});
                var previous=RenderTexture.active;RenderTexture.active=target;image.ReadPixels(new Rect(0,0,768,768),0,0);image.Apply();RenderTexture.active=previous;
                off=image.GetPixels32();File.WriteAllBytes(Path.Combine(output,Path.GetFileNameWithoutExtension(filename)+"-eyes-off.png"),image.EncodeToPNG());
            }
            finally {eyes.SetEmissionEnabled(true);}
            foreach(var eye in eyes.EyeAnchors)
            {
                var uv=camera.WorldToViewportPoint(eye.position);int x=(int)(uv.x*768),y=(int)(uv.y*768),lit=0;
                for(int row=Mathf.Max(0,y-48);row<Mathf.Min(768,y+48);row++)
                    for(int column=Mathf.Max(0,x-48);column<Mathf.Min(768,x+48);column++)
                    {
                        int p=row*768+column;
                        if(on[p].r>off[p].r+6 && on[p].r>on[p].g*1.2f && on[p].r>on[p].b*1.1f)lit++;
                    }
                Require(lit>=4,filename+" actual lit eye pixels disappeared when original-mask emission was disabled: "+lit);
            }
        }
        void Finish(string failure)
        {
            if (finished) return; finished = true;
            bool passed = failure == null && errors.Count == 0;
            File.WriteAllText(Path.Combine(output, "cabinet-expression.json"), JsonUtility.ToJson(new Report {
                status = passed ? "PASS" : "FAIL", failure = failure, creature = creature, repeatedEntry=taunt, unity = Application.unityVersion, checks = checks.ToArray(), errors = errors.ToArray() }, true));
            Application.Quit(passed ? 0 : 1);
        }
    }
}
