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
    public sealed class MaskRampageProbe : MonoBehaviour
    {
        string output; float started; bool finished;
        GameSession session; LanternMaskEncounter mask; RouteAudioCapture capture;
        readonly Dictionary<string, bool> checks = new Dictionary<string, bool>();
        readonly List<string> errors = new List<string>();
        float peakNativeSpeed, maximumAuthoredPatrol; int contacts, whistles, smashed;
        [Serializable] sealed class Check { public string name; public bool passed; }
        [Serializable] sealed class Report
        {
            public string status, failure, unity, graphics;
            public string scope = "Controlled native Mask fixture on actual seed-73 corridor. Third memory uses production release. Other monsters are disabled; player/Mask initial supported placements are explicit fixtures. Actual NavMesh runs, real locked/open scene doors, genuine solid-cover and cabinet entry, pause and checkpoint codec are production. Reference PNGs use the actual player camera after live attachment: front, an independently supported side or honestly labeled oblique position, and supported close face detail, with unchanged camera lens, scene lighting and model pose. Door locked state is fixture-authored because existing corridor ordinary leaves begin unlocked. Ordinary exit seals remain outside destructible Kind.Door. Native listener full mix is retained before the verified process-device mute; this does not certify natural survival, human listening, fear or hardware performance.";
            public Check[] checks; public string[] errors; public float peakNativeSpeed, maximumAuthoredPatrol;
            public int contacts, whistles, smashed; public RouteAudioCapture.Report audio;
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)] static void Install()
        {
            var args = Environment.GetCommandLineArgs(); int index = Array.IndexOf(args, "-v6-mask-output");
            if (index < 0 || index + 1 >= args.Length) return;
            var owner = new GameObject("Explicit silent Mask rampage diagnostic"); DontDestroyOnLoad(owner);
            owner.AddComponent<MaskRampageProbe>().output = Path.GetFullPath(args[index + 1]);
        }
        void OnEnable() { Application.logMessageReceived += Log; }
        void OnDisable() { Application.logMessageReceived -= Log; }
        void Log(string value, string trace, LogType type)
        { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors.Add(value); }
        void Update() { if (!finished && started > 0 && Time.realtimeSinceStartup - started > 120) Finish("Bounded native Mask fixture timed out"); }
        IEnumerator Start()
        {
            started = Time.realtimeSinceStartup; Application.runInBackground = true; Directory.CreateDirectory(output);
            var stack = new Stack<IEnumerator>(); stack.Push(Run());
            while (!finished)
            {
                object next = null; bool moved = false; string failure = null;
                try { moved = stack.Peek().MoveNext(); if (moved) next = stack.Peek().Current; }
                catch (Exception error) { failure = error.ToString(); }
                if (failure != null) { Finish(failure); yield break; }
                if (!moved) { stack.Pop(); if (stack.Count == 0) { Finish(null); yield break; } continue; }
                if (next is IEnumerator nested) { stack.Push(nested); continue; }
                yield return next;
            }
        }
        void CheckValue(string name, bool passed)
        { checks[name] = passed; if (!passed) throw new InvalidOperationException(name); }
        IEnumerator Await(Func<bool> condition, float seconds, string message)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (!condition()) { if (Time.realtimeSinceStartup >= end) throw new InvalidOperationException(message); yield return null; }
        }
        void PlacePlayer(Vector3 point)
        { session.player.transform.position = point + Vector3.up * .03f; Physics.SyncTransforms(); }
        Vector3 Remote(Vector3 from)
        {
            for (int cell = 0; cell < CorridorLayout.Count; cell++)
                if (NavMesh.SamplePosition(session.Corridor.CellPosition(cell), out var hit, 1, NavMesh.AllAreas) &&
                    Vector3.Distance(from, hit.position) > 18) return hit.position;
            throw new InvalidOperationException("No supported remote player pose");
        }
        Interactable RealDoor()
        {
            foreach (var door in session.Corridor.GetComponentsInChildren<Interactable>(true))
            {
                if (door.kind != Interactable.Kind.Door || !door.movingLeaf) continue;
                var a = door.transform.position - door.DoorNormal * 2.1f;
                var b = door.transform.position + door.DoorNormal * 2.1f; a.y = b.y = .03f;
                if (!NavMesh.SamplePosition(a, out var near, .3f, NavMesh.AllAreas) ||
                    !NavMesh.SamplePosition(b, out var far, .3f, NavMesh.AllAreas)) continue;
                var route = new NavMeshPath();
                if (NavMesh.CalculatePath(near.position, far.position, NavMesh.AllAreas, route) && route.status == NavMeshPathStatus.PathComplete)
                    return door;
            }
            throw new InvalidOperationException("No actual two-sided corridor door route");
        }
        Vector3 Side(Interactable door, float side)
        {
            var point = door.transform.position + door.DoorNormal * side; point.y = .03f;
            if (!NavMesh.SamplePosition(point, out var hit, .3f, NavMesh.AllAreas)) throw new InvalidOperationException("Unsupported real door side");
            return hit.position;
        }
        void PlaceMask(Vector3 point, Vector3 destination)
        {
            var state = mask.CaptureChapterProgress(); state.position = point; state.rotation = Quaternion.LookRotation(destination - point);
            state.target = destination; state.state = LanternMaskEncounter.Phase.Wander; state.memory = 0;
            state.doorPassage = null; state.investigation = null;
            mask.RestoreCorridorProgress(state);
        }
        void RealCabinetHide()
        {
            var player = session.player; var snapshot = player.CaptureProgress();
            foreach (var cabinet in session.Corridor.GetComponentsInChildren<Interactable>(true))
            {
                if (cabinet.kind != Interactable.Kind.HidingPlace || !cabinet.InteractionAvailable || !cabinet.inside || !cabinet.outside) continue;
                snapshot.position = cabinet.outside.position;
                if (!player.CanRestoreProgress(snapshot)) continue;
                var outward = cabinet.outside.position - cabinet.inside.position; outward.y = 0; outward.Normalize();
                if (!NavMesh.SamplePosition(cabinet.outside.position + outward * 3, out var support, .5f, NavMesh.AllAreas)) continue;
                PlacePlayer(cabinet.outside.position); PlaceMask(support.position, cabinet.outside.position);
                if (!mask.CanSeePlayer()) continue;
                CheckValue("actual cabinet approach provides clear near sight", true);
                cabinet.Use(player); CheckValue("actual cabinet entry", player.Hidden);
                CheckValue("close runner respects actual cabinet hiding", !mask.CanSeePlayer());
                cabinet.Use(player); CheckValue("actual cabinet exit", !player.Hidden);
                player.GetComponent<CharacterController>().enabled = false;
                return;
            }
            throw new InvalidOperationException("No actual clear near-range cabinet entry/exit fixture");
        }
        void CapturePlayerView(string name)
        {
            var camera = session.player.eyes;
            var art = mask.GetComponent<MaskHorrorVisual>();
            CheckValue("snapshot retains visible authored Mask", art && art.Prepared && mask.mask.gameObject.activeInHierarchy &&
                mask.isActiveAndEnabled && mask.body.gameObject.activeInHierarchy && art.MaskModel.GetComponentsInChildren<Renderer>(true)
                    .Any(renderer => renderer.enabled && renderer.gameObject.activeInHierarchy));
            CheckValue("reference snapshot uses the actual leading neck joint", art.HeadSocket && art.FaceJoint &&
                Vector3.Distance(art.HeadSocket.position, art.FaceJoint.position) < .003f);
            CheckValue("reference snapshot preserves many-arm forward anatomy", art.ArmPairCount == 8 && art.SwingPivotCount == 16 &&
                Vector3.Dot(art.FaceForward, mask.transform.forward) > .8f);
            CheckValue("fully grown reference disables extinguished lantern light", !mask.flameLight || !mask.flameLight.enabled);
            CheckValue("reference hair and body stay beneath actual corridor lintel", Mathf.Max(mask.LastBodyBounds.max.y, mask.LastMaskBounds.max.y) -
                mask.transform.position.y < MaskHorrorVisual.HallwayHeight);
            // Submit the actual player's camera through its existing URP renderer.
            // Keep camera pose/lens, volumes, materials and scene lights intact;
            // encode the render target directly instead of relying on an OS/window
            // screenshot path, which can fail in a hidden native test process.
            var target = new RenderTexture(1280, 720, 24); var previous = RenderTexture.active; Texture2D image = null;
            try
            {
                CheckValue("player-camera render target created", target.Create());
                RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
                RenderTexture.active = target;
                image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); image.Apply();
                var bytes = image.EncodeToPNG(); var path = Path.Combine(output, name);
                CheckValue("player-camera PNG encoded", bytes != null && bytes.Length > 8);
                File.WriteAllBytes(path, bytes);
                CheckValue("player-camera PNG retained", File.Exists(path) && new FileInfo(path).Length == bytes.Length);
            }
            finally
            {
                RenderTexture.active = previous;
                if (image) Destroy(image); target.Release(); Destroy(target);
            }
        }
        void CaptureSupportedSideView()
        {
            var camera = session.player.eyes;
            var originalPosition = session.player.transform.position; var originalRotation = camera.transform.rotation;
            var centre = mask.LastBodyBounds.center; centre.y = mask.transform.position.y;
            var focus = Vector3.Lerp(mask.LastBodyBounds.center, mask.LastMaskBounds.center, .35f);
            var saved = session.player.CaptureProgress(); bool captured = false, fullSide = false;
            var candidates = new List<Vector3>();
            foreach (float side in new[] { 1f, -1f })
                foreach (float distance in new[] { 2.7f, 2.35f, 2.0f, 1.7f })
                    candidates.Add(centre + mask.transform.right * side * distance + mask.transform.forward * .35f);
            int sideCandidates = candidates.Count;
            // Narrow halls may physically have no full-profile player position.
            // A supported front oblique view still shows the trailing anatomy;
            // label its file honestly instead of treating it as a studio profile.
            var headFloor = mask.LastMaskBounds.center; headFloor.y = mask.transform.position.y;
            foreach (float forward in new[] { 4f, 3.5f, 3f, 2.5f })
                foreach (float sideways in new[] { 1.1f, -.8f, .8f, -1.1f, 0f })
                    candidates.Add(headFloor + mask.transform.forward * forward + mask.transform.right * sideways);
            try
            {
                // The same live attached pose supplies both views. Move only the
                // explicitly controlled player onto existing supported side floor
                // before the next AI frame, preserving the real camera lens and
                // all model transforms, scene geometry, lights and materials.
                for (int index = 0; index < candidates.Count; index++)
                {
                    var proposed = candidates[index];
                    if (!NavMesh.SamplePosition(proposed, out var hit, .25f, NavMesh.AllAreas) ||
                        Vector3.Distance(hit.position, proposed) > .3f || !EnemyNavigation.SameFloor(hit.position, mask.transform.position.y)) continue;
                    saved.position = hit.position + Vector3.up * .03f;
                    if (!session.player.CanRestoreProgress(saved)) continue;
                    PlacePlayer(hit.position);
                    if (!EnemyNavigation.ClearSight(camera.transform.position, mask.LastMaskBounds.center)) continue;
                    camera.transform.rotation = Quaternion.LookRotation(focus - camera.transform.position);
                    var head = camera.WorldToViewportPoint(mask.LastMaskBounds.center);
                    if (head.z <= camera.nearClipPlane || head.x < .04f || head.x > .96f || head.y < .04f || head.y > .96f) continue;
                    int visibleSegments = mask.GetComponent<MaskHorrorVisual>().BodyModel.GetComponentsInChildren<Transform>(true)
                        .Where(node => node.name.StartsWith("Segment", StringComparison.Ordinal) && node.name.Length == 9)
                        .Count(node =>
                        {
                            var renderers = node.GetComponentsInChildren<MeshRenderer>(true); if (renderers.Length == 0) return false;
                            var bounds = renderers[0].bounds; foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
                            var point = camera.WorldToViewportPoint(bounds.center);
                            return point.z > camera.nearClipPlane && point.x >= .02f && point.x <= .98f && point.y >= .02f && point.y <= .98f;
                        });
                    if (visibleSegments < 6) continue;
                    fullSide = index < sideCandidates;
                    CapturePlayerView(fullSide ? "mask-reference-supported-side.png" : "mask-reference-supported-oblique.png"); captured = true; break;
                }
                CheckValue("reference second image uses existing supported floor and shows six actual body segments", captured);
            }
            finally
            {
                session.player.transform.position = originalPosition; camera.transform.rotation = originalRotation;
                Physics.SyncTransforms();
            }
        }
        void CaptureSupportedCloseView()
        {
            var player = session.player; var camera = player.eyes; var reference = mask.GetComponent<MaskHorrorVisual>();
            var originalPosition = player.transform.position; var originalRotation = camera.transform.rotation;
            var saved = player.CaptureProgress(); bool captured = false;
            try
            {
                // This face detail uses another supported standing player pose
                // within the same live attached frame. No AI frame, camera lens,
                // eye height, model pose or lighting changes are introduced.
                foreach (float distance in new[] { 2.2f, 2f, 1.8f })
                {
                    foreach (float lateral in new[] { .3f, -.3f, 0f })
                    {
                        var proposed = mask.transform.position + mask.transform.forward * distance + mask.transform.right * lateral;
                        if (!NavMesh.SamplePosition(proposed, out var hit, .25f, NavMesh.AllAreas) ||
                            Vector3.Distance(hit.position, proposed) > .3f || !EnemyNavigation.SameFloor(hit.position, mask.transform.position.y)) continue;
                        saved.position = hit.position + Vector3.up * .03f;
                        if (!player.CanRestoreProgress(saved)) continue;
                        PlacePlayer(hit.position);
                        if (!EnemyNavigation.ClearSight(camera.transform.position, reference.HeadFrontTarget)) continue;
                        camera.transform.rotation = Quaternion.LookRotation(reference.HeadFrontTarget - camera.transform.position);
                        var face = camera.WorldToViewportPoint(reference.HeadFrontTarget);
                        if (face.z <= camera.nearClipPlane || face.x < .04f || face.x > .96f || face.y < .04f || face.y > .96f) continue;
                        CapturePlayerView("mask-reference-supported-close.png"); captured = true; break;
                    }
                    if (captured) break;
                }
                CheckValue("reference close face image uses existing supported standing floor", captured);
            }
            finally
            {
                player.transform.position = originalPosition; camera.transform.rotation = originalRotation; Physics.SyncTransforms();
            }
        }
        IEnumerator Run()
        {
            yield return DiagnosticAudioSilence.WaitForSafeAudio(output);
            yield return null; yield return null; session = GameSession.Current;
            CheckValue("real native session", session && session.player && session.Shell);
            session.ConfigureRecordDirectory(Path.Combine(output, "isolated-profile", Guid.NewGuid().ToString("N")));
            session.CreateCorridor(73); session.Shell.Begin();
            mask = session.Corridor.Mask;
            maximumAuthoredPatrol = FindObjectsByType<StalkerBrain>(FindObjectsInactive.Include).Max(actor => actor.patrolSpeed);
            foreach (var other in FindObjectsByType<StalkerBrain>(FindObjectsInactive.Include)) other.gameObject.SetActive(false);
            session.player.enabled = false; session.player.GetComponent<CharacterController>().enabled = false;
            for (int i = 0; i < 3; i++) CheckValue("actual memory release " + i, session.Corridor.Collect("memory-" + i));
            yield return Await(() => mask.gameObject.activeSelf && EnemyNavigation.Ready(mask.GetComponent<NavMeshAgent>()), 4, "Third memory failed to release runner");
            CheckValue("already-grown runner release", mask.CorridorRunner && mask.IntroCompleted && mask.Transformed);
            capture = RouteAudioCapture.Attach(Path.Combine(output, "listener-audio"));
            var agent = mask.GetComponent<NavMeshAgent>(); var voice = mask.GetComponent<CorridorMaskAudio>();
            CheckValue("runner owns contact and whistle sources", voice && voice.ContactSource && voice.WhistleSource &&
                !mask.GetComponent<StalkerFootsteps>().enabled && voice.ContactSource.spatialBlend == 1 &&
                !voice.ContactSource.ignoreListenerPause && !voice.WhistleSource.ignoreListenerVolume);
            var door = RealDoor(); var leaf = door.movingLeaf.localPosition;
            var marker = new GameObject("Explicit native runner door target"); marker.transform.position = Side(door, 2.1f);
            mask.patrol = new[] { marker.transform, marker.transform };
            foreach (bool locked in new[] { true, false })
            {
                mask.enabled = false; door.RestoreDoor(false, leaf); door.DoorLocked = locked;
                if (locked) CheckValue("locked leaf rejects ordinary operation", !door.OpenForPursuer());
                else { CheckValue("ordinary leaf really opens", door.OpenForPursuer()); yield return Await(() => door.AtRequestedDoorPose, 3, "Open leaf did not slide aside"); }
                PlacePlayer(Remote(door.transform.position)); mask.enabled = true; PlaceMask(Side(door, -2.1f), marker.transform.position);
                var previous = mask.transform.position; float end = Time.realtimeSinceStartup + 10;
                while (Time.realtimeSinceStartup < end && (!door.DoorBroken || Vector3.Dot(mask.transform.position - door.transform.position, door.DoorNormal) < .9f))
                {
                    yield return null; peakNativeSpeed = Mathf.Max(peakNativeSpeed, agent.velocity.magnitude);
                    CheckValue("no warp during " + (locked ? "locked" : "open") + " impact", Vector3.Distance(previous, mask.transform.position) <=
                        LanternMaskEncounter.RunnerChaseSpeed * Mathf.Max(Time.deltaTime, .016f) + .15f);
                    previous = mask.transform.position;
                }
                CheckValue((locked ? "locked" : "open") + " real scene leaf shattered", door.DoorBroken && door.IsOpen && !door.obstacle.enabled &&
                    !door.movingLeaf.GetComponentsInChildren<Collider>().Any(collider => collider.enabled) && !door.InteractionAvailable);
                CheckValue((locked ? "locked" : "open") + " actual native crossing", Vector3.Dot(mask.transform.position - door.transform.position, door.DoorNormal) > .9f);
            }
            CheckValue("actual patrol exceeds every authored monster patrol", peakNativeSpeed > maximumAuthoredPatrol && agent.speed == LanternMaskEncounter.RunnerPatrolSpeed);
            CheckValue("real running emits heavy contacts and door smashes", voice.ContactsPlayed > 0 && voice.DoorSmashesPlayed >= 2);
            // A room-cover counterfactual uses a real opaque collider and keeps
            // player/monster on supported existing floor positions.
            mask.enabled = false; PlacePlayer(Side(door, 2.1f)); PlaceMask(Side(door, -2.1f), marker.transform.position);
            CheckValue("near real clear sight", mask.CanSeePlayer());
            var cover = GameObject.CreatePrimitive(PrimitiveType.Cube); cover.name = "Explicit native opaque room-cover counterfactual";
            cover.transform.position = door.transform.position + Vector3.up * 1.2f; cover.transform.rotation = door.transform.rotation;
            cover.transform.localScale = new Vector3(2.5f, 2.4f, .4f); Physics.SyncTransforms();
            CheckValue("room cover prevents close-range acquisition", !mask.CanSeePlayer());
            cover.SetActive(false); Destroy(cover); Physics.SyncTransforms(); CheckValue("cover removal restores physical sight", mask.CanSeePlayer());
            int beforeAttachment = mask.AttachmentSamples, beforeNearWhistle = voice.WhistlesPlayed; mask.enabled = true;
            // Restore places the fixture but live production LateUpdate owns the
            // head/body attachment. Capture that rendered pose before disabling
            // this controlled actor for the independent cabinet counterfactual.
            yield return null; yield return new WaitForEndOfFrame();
            CheckValue("snapshot follows actual live attachment", mask.isActiveAndEnabled && mask.AttachmentSamples > beforeAttachment);
            // The old floating mask pivot is reset by Visual before LateUpdate.
            // Aim only after the real leading-neck attachment has settled, using
            // actual body bounds and the authored front-face target. Keep the
            // player's standing eye height and camera lens unchanged.
            var reference = mask.GetComponent<MaskHorrorVisual>(); var camera = session.player.eyes;
            var focus = Vector3.Lerp(mask.LastBodyBounds.center, reference.HeadFrontTarget, .75f);
            camera.transform.rotation = Quaternion.LookRotation(focus - camera.transform.position);
            var headViewport = camera.WorldToViewportPoint(mask.LastMaskBounds.center);
            CheckValue("front snapshot looks at the actual attached head", headViewport.z > camera.nearClipPlane &&
                headViewport.x >= .04f && headViewport.x <= .96f && headViewport.y >= .04f && headViewport.y <= .96f);
            CapturePlayerView("mask-actual-player-view.png");
            CaptureSupportedSideView();
            CaptureSupportedCloseView();
            mask.enabled = false;
            RealCabinetHide();
            PlacePlayer(Side(door, 2.1f)); PlaceMask(Side(door, -2.1f), marker.transform.position);
            // Whistle is permitted at genuine near range. A short actual sight
            // chase is bounded before striking, then ordinary pause freezes it.
            // The live near-range screenshot may already admit the first actual
            // warning. Count from before that frame rather than demanding another
            // utterance inside its production cooldown while exposing the player.
            mask.enabled = true;
            yield return Await(() => voice.WhistlesPlayed > beforeNearWhistle, 2, "Near recorded whistle did not play");
            PlacePlayer(Remote(mask.transform.position)); yield return new WaitForSeconds(.9f);
            session.Shell.Pause(); var at = mask.transform.position; int beforeContact = voice.ContactsPlayed;
            yield return new WaitForSecondsRealtime(.25f);
            CheckValue("pause freezes native run and contact admission", AudioListener.pause && mask.transform.position == at && voice.ContactsPlayed == beforeContact);
            var checkpoint = session.CaptureCheckpoint(); var saved = JsonUtility.FromJson<CorridorCheckpoint>(JsonUtility.ToJson(checkpoint)); saved.Validate();
            CheckValue("JSON retains shattered ordinary door", saved.doors.Any(state => state.id == door.stableId && state.broken && state.open));
            session.Corridor.RestoreCheckpoint(saved);
            CheckValue("paused production restore keeps destroyed physics", door.DoorBroken && !door.obstacle.enabled &&
                !door.movingLeaf.GetComponentsInChildren<Collider>().Any(collider => collider.enabled));
            contacts = voice.ContactsPlayed; whistles = voice.WhistlesPlayed; smashed = voice.DoorSmashesPlayed;
            CheckValue("seal remains a separate non-destructible exit", session.Corridor.GetComponentsInChildren<Interactable>(true).Any(item => item.kind == Interactable.Kind.Exit));
            File.WriteAllText(Path.Combine(output, "mask-checkpoint.json"), JsonUtility.ToJson(saved, true));
        }
        void Finish(string failure)
        {
            if (finished) return; finished = true;
            try
            {
                var audio = capture ? capture.Complete() : null;
                if (failure == null && (audio == null || !audio.nonSilent || audio.truncated || audio.nonfiniteSamples != 0 || !string.IsNullOrEmpty(audio.writerError)))
                    failure = "Native pre-device full-mix retention incomplete";
                var report = new Report { status = failure == null && errors.Count == 0 && checks.Count > 0 && checks.Values.All(value => value) ? "PASS" : "FAIL",
                    failure = failure ?? "", unity = Application.unityVersion, graphics = SystemInfo.graphicsDeviceName,
                    checks = checks.Select(pair => new Check { name = pair.Key, passed = pair.Value }).ToArray(), errors = errors.ToArray(),
                    peakNativeSpeed = peakNativeSpeed, maximumAuthoredPatrol = maximumAuthoredPatrol, contacts = contacts, whistles = whistles, smashed = smashed, audio = audio };
                File.WriteAllText(Path.Combine(output, "mask-rampage.json"), JsonUtility.ToJson(report, true));
                Debug.Log("HAPPYTOY_MASK_RAMPAGE_" + report.status); Application.Quit(report.status == "PASS" ? 0 : 2);
            }
            catch (Exception error) { Debug.LogError("Mask report failed: " + error); Application.Quit(2); }
        }
    }
}
