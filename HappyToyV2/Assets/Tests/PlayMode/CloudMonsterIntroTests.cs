using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;
using static HappyToy.V2.CloudTests.RuntimeAccess;
using Object = UnityEngine.Object;

namespace HappyToy.V2.CloudTests
{
    // Positioned, isolated fixtures in the unchanged authored scene. Public story/
    // sight/proximity triggers drive every phase; no private reveal-state setters.
    // Camera images are real world renders, not a survival run or a fear verdict.
    internal static class CloudMonsterIntroTests
    {
        internal static void Capture(Camera camera, string file, Component owner, string phase, float elapsed)
        {
            Assert.That(SystemInfo.graphicsDeviceType, Is.Not.EqualTo(GraphicsDeviceType.Null));
            var target = new RenderTexture(640, 360, 24); target.Create();
            try
            {
                RenderPipeline.SubmitRenderRequest(camera,
                    new UniversalRenderPipeline.SingleCameraRequest { destination = target });
                CloudExperienceTests.SaveFrame(file, target);
                TestContext.Out.WriteLine("HAPPYTOY_INTRO_FRAME " + JsonUtility.ToJson(new Frame {
                    file = file, owner = owner.GetType().Name, phase = phase, elapsed = elapsed,
                    cameraPosition = camera.transform.position, cameraForward = camera.transform.forward,
                    transformProgress = owner.GetType().Name == "LanternMaskEncounter" ? Get<float>(owner, "TransformProgress") : -1,
                    ownerPosition = owner.transform.position }));
            }
            finally { target.Release(); Object.Destroy(target); }
        }
        [Serializable] internal sealed class Frame
        {
            public string file, owner, phase, scope = "Positioned fixture; actual authored player camera; public event clock";
            public float elapsed, transformProgress;
            public Vector3 cameraPosition, cameraForward, ownerPosition;
        }
        [Serializable] internal sealed class Segment
        {
            public string name, owner, phase, phaseEnd, clip, authoredClipSha256;
            public int offset, samples, rate, channels = 2, flushedSamples, clipped;
            public float elapsedStart, elapsedEnd, master, sourceVolume;
            public bool paused, reducedMotion;
            public Vector3 cameraPosition, sourcePosition;
            public double peak, rms;
        }
        [Serializable] internal sealed class Report
        {
            public string capture = "UnityAudioRendererMainOutput", wave = "monster-first-appearances.wav";
            public string scope = "Disjoint short genuine public-event excerpts with selected production sources. Native 48 kHz stereo, no resampling or synthetic replacement PCM. Fixture placements are not survival, continuous playthrough, device listening or human-fear certification.";
            public int rate, channels = 2, samples;
            public Segment[] segments;
            public string[] images = { "intro-portrait-before.png", "intro-portrait-angry.png", "intro-archive-emergence.png",
                "intro-lantern-light.png", "intro-lantern-mask.png", "intro-mannequin-turn.png", "intro-nursery-cry.png", "intro-nursery-prepare.png",
                "intro-wraith-half-grown.png", "intro-wraith-attached.png" };
        }
    }

    public sealed partial class CloudPlayModeTests
    {
        static readonly string[] IntroOwners = { "StoryDirector", "V1HwacatEvent", "UncatAnnexEvent", "AnnexEncounter", "LanternMaskEncounter", "WeepingAngelEncounter" };
        void IntroSetup(params string[] keep)
        {
            foreach (var name in IntroOwners)
                if (!keep.Contains(name)) foreach (var owner in Components(name)) ((Behaviour)owner).enabled = false;
            foreach (var brain in Components("StalkerBrain")) brain.gameObject.SetActive(false);
            Call(shell, "RestoreDefaultSettings"); Begin(); ((Behaviour)player).enabled = false;
            Get<Light>(player, "flashlight").enabled = true;
        }
        Camera IntroCamera => Get<Camera>(player, "eyes");
        void IntroLook(Vector3 target)
        {
            IntroCamera.transform.rotation = Quaternion.LookRotation(target - IntroCamera.transform.position);
            Physics.SyncTransforms();
        }
        void IntroPlace(Vector3 anchor, Vector3 target, float distance = 3)
        {
            // Select a genuine same-floor authored NavMesh view, never move actors or walls.
            foreach (var direction in new[] { Vector3.back, Vector3.left, Vector3.right, Vector3.forward })
            {
                if (!NavMesh.SamplePosition(anchor + direction * distance, out var hit, .45f, NavMesh.AllAreas) ||
                    Mathf.Abs(hit.position.y - anchor.y) > .4f) continue;
                PlacePlayer(hit.position, false); IntroLook(target);
                if (!Physics.Linecast(IntroCamera.transform.position, target, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) return;
            }
            Assert.Fail("No same-floor NavMesh/clear authored-camera fixture around " + anchor);
        }
        static string IntroPhase(Component owner) => Get<object>(owner, owner.GetType().Name == "LanternMaskEncounter" ||
            owner.GetType().Name == "WeepingAngelEncounter" ? "IntroPhase" : "Phase").ToString();
        static float IntroClock(Component owner) => Get<float>(owner, owner.GetType().Name == "V1CyclopseIntro" ? "AnticipationElapsed" :
            owner.GetType().Name == "LanternMaskEncounter" || owner.GetType().Name == "WeepingAngelEncounter" ? "IntroElapsed" : "RevealElapsed");
        static Component IntroVoice(Component owner)
        {
            var voice = owner.GetComponent(RequireType("EncounterRevealAudio"));
            Assert.That(voice, Is.Not.Null, owner.GetType().Name + " has no owned first-appearance voice"); return voice;
        }
        void IntroFrame(Component owner, string file) => CloudMonsterIntroTests.Capture(IntroCamera, file, owner, IntroPhase(owner), IntroClock(owner));
        static void IntroBrainSafe(Component brain)
        {
            Assert.That(((Behaviour)brain).enabled, Is.False, "AI acquired/attacked during its protected first appearance");
            Assert.That(Get<int>(brain, "AttacksStarted"), Is.Zero);
        }
        IEnumerator IntroPause(Component owner, Action extra = null)
        {
            Call(shell, "Pause"); float elapsed = IntroClock(owner); string phase = IntroPhase(owner);
            Vector3 at = owner.transform.position; yield return Delay(.18f);
            Assert.That(IntroClock(owner), Is.EqualTo(elapsed).Within(.0001f)); Assert.That(IntroPhase(owner), Is.EqualTo(phase));
            Assert.That(Vector3.Distance(at, owner.transform.position), Is.LessThan(.015f));
            Assert.That(AudioListener.pause, Is.True); extra?.Invoke(); Call(shell, "Resume");
        }
        IEnumerator IntroLanternPause(Component owner, bool toggleComfort, bool afterAttachedLateUpdate = false)
        {
            var mask = Get<Transform>(owner, "mask"); var body = Get<Transform>(owner, "body");
            var motion = Get<Animation>(owner, "motion");
            Vector3 rootAt = default, maskAt = default, bodyScale = default;
            Quaternion maskTurn = default;
            float elapsed = 0, progress = 0, animationTime = 0;
            Action pauseAndSnapshot = () => {
                Call(shell, "Pause");
                rootAt = owner.transform.position; maskAt = mask.position; bodyScale = body.localScale;
                maskTurn = mask.localRotation;
                elapsed = Get<float>(owner, "IntroElapsed"); progress = Get<float>(owner, "TransformProgress");
                animationTime = motion && motion.GetClip("run") ? motion["run"].time : 0;
            };
            if (afterAttachedLateUpdate)
            {
                var observer = new GameObject("CloudQA post-LateUpdate attachment observer").AddComponent<CloudIntroLateUpdateObserver>();
                try
                {
                    observer.ObserveOnce = () => {
                        Assert.That(Get<bool>(owner, "Transformed"), Is.True);
                        Assert.That(Get<float>(owner, "TransformProgress"), Is.EqualTo(1));
                        var skin = body.GetComponentInChildren<SkinnedMeshRenderer>();
                        Assert.That(skin, Is.Not.Null, "Completed wraith has no rendered skin for its attachment");
                        var bounds = skin.bounds;
                        Vector3 attached = new Vector3(bounds.center.x, bounds.max.y + .15f, bounds.center.z) + owner.transform.forward * .03f;
                        Assert.That(Vector3.Distance(mask.position, attached), Is.LessThan(.0001f),
                            "Post-LateUpdate mask was not attached to the actual rendered wraith bounds");
                        pauseAndSnapshot();
                    };
                    yield return Wait(() => observer.Completed, 2, "Post-LateUpdate attachment observation never ran");
                    Assert.That(observer.Error, Is.Null, "Post-LateUpdate attachment assertion failed: " + observer.Error);
                    Assert.That(Get<bool>(session, "InputAllowed"), Is.False);
                    Assert.That(observer.enabled, Is.False, "One-shot observer could pause again after resume");
                }
                finally { Object.Destroy(observer.gameObject); }
                // The previous callback paused the attached render pose itself; this
                // camera request cannot see a later unpaused Update's reset pose.
                IntroFrame(owner, "intro-wraith-attached.png");
            }
            else pauseAndSnapshot();
            if (toggleComfort) Call(shell, "ToggleReducedMotion");
            yield return Delay(.18f);
            Assert.That(owner.transform.position, Is.EqualTo(rootAt));
            Assert.That(Vector3.Distance(mask.position, maskAt), Is.LessThan(.0001f));
            Assert.That(Quaternion.Angle(mask.localRotation, maskTurn), Is.LessThan(.001f));
            Assert.That(body.localScale, Is.EqualTo(bodyScale));
            Assert.That(Get<float>(owner, "IntroElapsed"), Is.EqualTo(elapsed));
            Assert.That(Get<float>(owner, "TransformProgress"), Is.EqualTo(progress));
            if (motion && motion.GetClip("run")) Assert.That(motion["run"].time, Is.EqualTo(animationTime).Within(.0001f));
            Call(shell, "Resume");
        }
        void IntroCollectThrough(int step)
        {
            var ids = new[] { "register", "ribbon", "record" };
            while (Get<int>(session, "StoryStep") < step)
                Assert.That((bool)Call(session, "Collect", ids[Get<int>(session, "StoryStep")]), Is.True);
        }

        [UnityTest, Timeout(45000)]
        public IEnumerator PortraitFirstAppearanceUsesPhysicalFrameThenProtectedAngryGrace()
        {
            IntroSetup("V1HwacatEvent"); var owner = One("V1HwacatEvent");
            var painting = Get<Transform>(owner, "painting"); var brain = Get<Component>(owner, "angry");
            Vector3 spawn = Get<Vector3>(owner, "spawn"), original = painting.position;
            Quaternion rotation = painting.rotation;
            IntroPlace(spawn, spawn + Vector3.up * 1.25f, 3);
            // Let the real floor owner converge; never substitute RenderSettings.
            yield return new WaitForSeconds(1.5f);
            Assert.That(RenderSettings.fogDensity, Is.EqualTo(.032f).Within(.0015f));
            Assert.That(RenderSettings.fogColor.r, Is.EqualTo(.085f).Within(.008f));
            Assert.That(RenderSettings.fogColor.g, Is.EqualTo(.008f).Within(.008f));
            Assert.That(RenderSettings.fogColor.b, Is.EqualTo(.012f).Within(.008f));
            IntroFrame(owner, "intro-portrait-before.png");
            IntroCollectThrough(3);
            Assert.That(Get<bool>(owner, "Triggered"), Is.True); Assert.That(IntroPhase(owner), Is.EqualTo("frameStrain"));
            Assert.That(Get<GameObject>(owner, "normal").activeSelf || brain.gameObject.activeSelf, Is.False);
            yield return IntroPause(owner);
            Call(shell, "ToggleReducedMotion"); yield return null;
            Assert.That(Quaternion.Angle(painting.rotation, rotation), Is.LessThan(.01f), "Comfort mode retained frame rocking");
            yield return Wait(() => IntroPhase(owner) == "paintingDrop", 3, "The real record never dropped the portrait");
            yield return Wait(() => IntroPhase(owner) == "standUp", 3, "Portrait never revealed the normal doll");
            Assert.That(Vector3.Distance(painting.position, original), Is.GreaterThan(.5f));
            Assert.That(Get<GameObject>(owner, "normal").activeSelf, Is.True);
            yield return Wait(() => IntroPhase(owner) == "transform", 8, "Stand/dance/stillness did not reach the angry reveal");
            IntroBrainSafe(brain); Assert.That(brain.gameObject.activeSelf, Is.True);
            IntroFrame(owner, "intro-portrait-angry.png");
            Assert.That(Get<float>(owner, "PhaseElapsed"), Is.LessThan(.4f));
            yield return IntroPause(owner, () => IntroBrainSafe(brain));
            PlacePlayer(spawn + Vector3.up * 5, false); IntroLook(spawn + Vector3.up * 8);
            yield return Wait(() => Get<bool>(owner, "Completed"), 3, "Looking away/retreat deadlocked the portrait handoff");
            Assert.That(Get<int>(owner, "FrameStrainCues"), Is.EqualTo(1));
            Assert.That(Get<int>(owner, "FrameImpactCues"), Is.EqualTo(1));
            Assert.That(Get<int>(owner, "ToyMechanismCues"), Is.EqualTo(1));
            Assert.That(Get<int>(owner, "TransformationCues"), Is.EqualTo(1));
            Assert.That((bool)Call(session, "Collect", "record"), Is.False);
            yield return null; Assert.That(Get<int>(owner, "FrameStrainCues"), Is.EqualTo(1));
            Call(session, "Finish", false); yield return null;
            Assert.That(Get<bool>(owner, "Cancelled"), Is.True); Assert.That(brain.gameObject.activeSelf, Is.False);
            Assert.That(painting.position, Is.EqualTo(original)); Assert.That(Quaternion.Angle(painting.rotation, rotation), Is.LessThan(.01f));
            Debug.Log("HAPPYTOY_INTRO_PASS portrait: public record trigger, strain/drop/stand/dance/stillness/angry grace, pause/comfort, safe release after retreat, once-per-run and restoration");
        }

        [UnityTest, Timeout(45000)]
        public IEnumerator ArchiveScrapePrecedesRealEmergenceAndRetryResetsItsOwner()
        {
            IntroSetup("UncatAnnexEvent"); var owner = One("UncatAnnexEvent");
            var brain = Get<Component>(owner, "monster"); var light = Get<Light>(owner, "corridorLight");
            float baseline = light.intensity; Vector3 start = brain.transform.position;
            Assert.That(NavMesh.SamplePosition(new Vector3(26.2f, 0, -13.6f), out var cornerView, .45f, NavMesh.AllAreas), Is.True,
                "Authored archive-corner view has no same-floor NavMesh");
            PlacePlayer(cornerView.position, false); IntroLook(new Vector3(28.5f, 1.2f, -9.6f));
            bool startOccluded = Physics.Linecast(IntroCamera.transform.position, start + Vector3.up * 1.1f,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            Inspect("archive-record"); Assert.That(IntroPhase(owner), Is.EqualTo("shelfScrape"));
            var ownedClip = Get<AudioClip>(IntroVoice(owner), "ActiveClip"); Assert.That(ownedClip, Is.Not.Null);
            Assert.That(brain.gameObject.activeSelf, Is.False, "Scrape anticipation was already a visible monster");
            yield return IntroPause(owner);
            yield return Wait(() => brain.gameObject.activeSelf, 2, "Scrape never released actual emergence");
            IntroBrainSafe(brain);
            yield return Wait(() => Vector3.Distance(start, brain.transform.position) > .5f, 3, "Uncat did not physically travel from its authored start");
            yield return Wait(() => !Physics.Linecast(IntroCamera.transform.position, brain.transform.position + Vector3.up * 1.1f,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore), 3, "The authored corner view never saw actual emergence");
            Assert.That(Get<bool>(owner, "Released"), Is.False, "Corner keyframe missed the harmless emergence window");
            IntroFrame(owner, "intro-archive-emergence.png");
            TestContext.Out.WriteLine("HAPPYTOY_ARCHIVE_CORNER startBodyRayOccluded=" + startOccluded + "; visibleAt=" + brain.transform.position);
            Call(shell, "Pause"); float frozen = IntroClock(owner); Call(shell, "ToggleReducedMotion"); yield return null;
            Assert.That(light.intensity, Is.EqualTo(baseline).Within(.001f)); yield return Delay(.15f);
            Assert.That(IntroClock(owner), Is.EqualTo(frozen)); Call(shell, "Resume");
            PlacePlayer(start + Vector3.up * 5, false);
            yield return Wait(() => Get<bool>(owner, "Released"), 6, "Retreat stalled the bounded emergence/listening handoff");
            Assert.That(Get<int>(owner, "ScrapeCues"), Is.EqualTo(1));
            Inspect("archive-record"); yield return null; Assert.That(Get<int>(owner, "ScrapeCues"), Is.EqualTo(1));
            var voice = IntroVoice(owner); var source = Get<AudioSource>(voice, "Source");
            Call(session, "Finish", false); yield return null;
            Assert.That(Get<bool>(owner, "Cancelled"), Is.True); Assert.That(brain.gameObject.activeSelf, Is.False);
            Assert.That(light.intensity, Is.EqualTo(baseline).Within(.001f)); Assert.That(source.isPlaying, Is.False);
            var previous = session; Call(shell, "Restart", true); yield return RecoveryRebind(previous);
            Assert.That(owner == null && brain == null && voice == null && source == null && ownedClip == null, Is.True);
            Assert.That(Get<bool>(One("UncatAnnexEvent"), "Triggered"), Is.False);
            IntroSetup("UncatAnnexEvent"); var fresh = One("UncatAnnexEvent");
            Inspect("archive-record"); Assert.That(IntroPhase(fresh), Is.EqualTo("shelfScrape"));
            var freshVoice = IntroVoice(fresh); var freshSource = Get<AudioSource>(freshVoice, "Source");
            ((Behaviour)fresh).enabled = false; yield return null;
            Assert.That(Get<bool>(fresh, "Cancelled"), Is.True); Assert.That(freshSource.isPlaying, Is.False);
            Assert.That(Get<Component>(fresh, "monster").gameObject.activeSelf, Is.False);
            ((Behaviour)fresh).enabled = true; yield return new WaitForSeconds(.9f);
            Assert.That(Get<bool>(fresh, "Released"), Is.False, "Disable/re-enable resumed a terminally cancelled reveal");
            Assert.That(Get<int>(fresh, "ScrapeCues"), Is.EqualTo(1));
            Debug.Log("HAPPYTOY_INTRO_PASS archive: public inspect, inactive scrape, authored physical travel, pause/comfort, retreat completion, once-only cue, result and native retry cleanup");
        }

        [UnityTest, Timeout(40000)]
        public IEnumerator LanternFirstSightRequiresFloorViewportAndLosBeforeSafeLightThenMask()
        {
            IntroSetup("LanternMaskEncounter"); var owner = One("LanternMaskEncounter");
            var mask = Get<Transform>(owner, "mask"); var light = Get<Light>(owner, "flameLight");
            IntroCollectThrough(2); Vector3 anchor = owner.transform.position;
            PlacePlayer(anchor + Vector3.up * 5, false); IntroLook(anchor + Vector3.up * 1.4f);
            yield return new WaitForSeconds(.2f); Assert.That(Get<bool>(owner, "IntroStarted"), Is.False, "Different-floor sight triggered a first appearance");
            IntroPlace(owner.transform.position, owner.transform.position + Vector3.up * 1.4f);
            Vector3 cameraAt = IntroCamera.transform.position; IntroLook(cameraAt - (mask.position - cameraAt));
            yield return new WaitForSeconds(.2f); Assert.That(Get<bool>(owner, "IntroStarted"), Is.False, "Offscreen first appearance consumed its reveal");
            IntroLook(owner.transform.position + Vector3.up * 1.4f);
            var obstruction = Cube("CloudQA intro sight blocker", Vector3.Lerp(IntroCamera.transform.position, owner.transform.position + Vector3.up, .5f), new Vector3(1.2f, 3, 1.2f));
            Physics.SyncTransforms(); yield return new WaitForSeconds(.2f);
            Assert.That(Get<bool>(owner, "IntroStarted"), Is.False, "Occluded mask consumed its reveal");
            obstruction.SetActive(false); Physics.SyncTransforms(); IntroLook(owner.transform.position + Vector3.up * 1.4f);
            yield return Wait(() => Get<bool>(owner, "IntroStarted"), 2, "Legitimate same-floor visible lantern never began");
            Assert.That(IntroPhase(owner), Is.EqualTo("LanternTicks")); Assert.That(mask.gameObject.activeSelf, Is.False);
            Assert.That(light.enabled, Is.True); IntroFrame(owner, "intro-lantern-light.png");
            Vector3 fixedAt = owner.transform.position;
            yield return IntroLanternPause(owner, true);
            yield return Wait(() => IntroPhase(owner) == "StillBeat", 3, "Lantern never raised its mask");
            Assert.That(mask.gameObject.activeSelf, Is.True); IntroFrame(owner, "intro-lantern-mask.png");
            Assert.That(Vector3.Distance(fixedAt, owner.transform.position), Is.LessThan(.015f));
            Assert.That(Get<int>(owner, "AttacksStarted"), Is.Zero); Assert.That(Get<int>(owner, "CursesApplied"), Is.Zero);
            Call(shell, "ToggleReducedMotion");
            PlacePlayer(owner.transform.position + Vector3.up * 5, false);
            yield return Wait(() => Get<bool>(owner, "IntroCompleted"), 3, "Looking away/retreat held the lantern intro hostage");
            int cues = Get<int>(IntroVoice(owner), "CuesPlayed");
            yield return new WaitForSeconds(.25f); Assert.That(Get<int>(IntroVoice(owner), "CuesPlayed"), Is.EqualTo(cues));
            Assert.That(Get<int>(owner, "CursesApplied"), Is.Zero); Assert.That(Get<bool>(session, "Finished"), Is.False);
            Call(session, "Finish", false); yield return null;
            Assert.That(Get<object>(owner, "State").ToString(), Is.EqualTo("Resolved")); Assert.That(mask.gameObject.activeSelf, Is.False);
            Debug.Log("HAPPYTOY_INTRO_PASS lantern: same-floor/on-screen/physical-LOS admission, light before mask, stationary pause-safe lock, comfort, no forced curse and retreat completion");
        }

        [UnityTest, Timeout(40000)]
        public IEnumerator MannequinFirstTurnRemainsStationaryAndReleasesWithoutGazeDeadlock()
        {
            IntroSetup("WeepingAngelEncounter"); var owner = One("WeepingAngelEncounter");
            var visual = Get<Transform>(owner, "visual"); var light = Get<Light>(owner, "displayLight");
            float baseline = light.intensity; Vector3 start = owner.transform.position;
            IntroPlace(start, start + Vector3.up * 1.25f); IntroCollectThrough(1);
            yield return Wait(() => Get<bool>(owner, "Triggered"), 2, "Legitimate mannequin sight never triggered the first turn");
            yield return IntroPause(owner);
            yield return Wait(() => IntroPhase(owner) == "RigidHold", 3, "Mannequin never reached its deliberate split-turn hold");
            IntroFrame(owner, "intro-mannequin-turn.png"); Quaternion hold = visual.localRotation;
            Assert.That(Get<bool>(owner, "Moving"), Is.False); Assert.That(Get<int>(owner, "AttacksStarted"), Is.Zero);
            Call(shell, "Pause"); Call(shell, "ToggleReducedMotion"); yield return null;
            Assert.That(light.intensity, Is.EqualTo(baseline).Within(.001f));
            Assert.That(Quaternion.Angle(hold, visual.localRotation), Is.LessThan(.01f)); Call(shell, "Resume");
            PlacePlayer(start + Vector3.up * 5, false); IntroLook(start + Vector3.up * 10);
            yield return Wait(() => Get<bool>(owner, "Released"), 3, "Looking away or changing floor deadlocked the turn");
            Assert.That(Vector3.Distance(owner.transform.position, start), Is.LessThan(.015f));
            Assert.That(Get<int>(owner, "AttacksStarted"), Is.Zero);
            int cues = Get<int>(IntroVoice(owner), "CuesPlayed");
            IntroPlace(start, start + Vector3.up * 1.25f); yield return new WaitForSeconds(.2f);
            Assert.That(Get<int>(IntroVoice(owner), "CuesPlayed"), Is.EqualTo(cues), "Repeat viewing restarted the introduction");
            Assert.That(Get<bool>(owner, "Observed"), Is.True); Assert.That(Get<bool>(owner, "Moving"), Is.False);
            Call(session, "Finish", false); yield return null;
            Assert.That(Get<bool>(owner, "Resolved"), Is.True); Assert.That(visual.gameObject.activeSelf, Is.False);
            Debug.Log("HAPPYTOY_INTRO_PASS mannequin: real sight trigger, split turn and rigid hold, no translation/attack, paused comfort, retreat completion and no repeat intro");
        }

        [UnityTest, Timeout(40000)]
        public IEnumerator NurseryFirstCryAndCrawlPreparationKeepFullFiveSecondSafetyWindow()
        {
            IntroSetup("AnnexEncounter"); var owner = One("AnnexEncounter"); var brain = Get<Component>(owner, "monster");
            Vector3 room = Get<Vector3>(owner, "roomCenter"), start = brain.transform.position;
            IntroPlace(room, start + Vector3.up * .65f, 2);
            // Story step zero prevents the nursery event while its actual basement
            // atmosphere settles around the positioned player.
            yield return new WaitForSeconds(1.5f);
            Assert.That(Get<bool>(owner, "Triggered"), Is.False);
            Assert.That(RenderSettings.fogDensity, Is.EqualTo(.038f).Within(.0015f));
            Assert.That(RenderSettings.fogColor.r, Is.EqualTo(.018f).Within(.008f));
            Assert.That(RenderSettings.fogColor.g, Is.EqualTo(.045f).Within(.008f));
            Assert.That(RenderSettings.fogColor.b, Is.EqualTo(.045f).Within(.008f));
            IntroCollectThrough(1);
            yield return Wait(() => Get<bool>(owner, "Triggered"), 2, "Legitimate nursery proximity did not begin the cry");
            IntroBrainSafe(brain); Assert.That(brain.GetComponent<NavMeshAgent>().enabled, Is.False);
            yield return Wait(() => IntroClock(owner) >= .6f, 2, "Nursery clock did not advance");
            Assert.That(IntroPhase(owner), Is.EqualTo("cry")); IntroFrame(owner, "intro-nursery-cry.png");
            yield return IntroPause(owner, () => IntroBrainSafe(brain));
            yield return Wait(() => IntroPhase(owner) == "crawlReady", 6, "Nursery never prepared its real crawl animation");
            IntroBrainSafe(brain); Assert.That(brain.GetComponent<NavMeshAgent>().enabled, Is.False);
            Assert.That(Get<int>(owner, "PreparationContacts"), Is.EqualTo(1));
            IntroFrame(owner, "intro-nursery-prepare.png");
            Assert.That(Vector3.Distance(start, brain.transform.position), Is.LessThan(.015f));
            PlacePlayer(room + Vector3.up * 5, false);
            yield return Wait(() => Get<bool>(owner, "Released"), 3, "Retreat deadlocked nursery release");
            Assert.That(IntroClock(owner), Is.GreaterThanOrEqualTo(5)); Assert.That(Get<bool>(session, "Finished"), Is.False);
            Assert.That(Get<int>(owner, "PreparationContacts"), Is.EqualTo(1));
            Call(session, "Finish", false); yield return null;
            Assert.That(Get<bool>(owner, "Cancelled"), Is.True); Assert.That(brain.gameObject.activeSelf, Is.False);
            Debug.Log("HAPPYTOY_INTRO_PASS nursery: public room proximity, cry/stillness/crawl preparation, real event elapsed, full five-second brain/navigation lock and one preparation contact");
        }

        IEnumerator IntroFlush(float seconds, int rate)
        {
            var discarded = new List<float>();
            yield return CaptureAudio(discarded, Mathf.RoundToInt(rate * seconds) * 2, 8);
        }
        IEnumerator IntroAudioSegment(string name, Component owner, AudioSource source, string clip, float seconds,
            int rate, List<float> montage, List<CloudMonsterIntroTests.Segment> segments, int flushed = 0)
        {
            foreach (var candidate in Object.FindObjectsByType<AudioSource>(FindObjectsInactive.Include)) candidate.mute = candidate != source;
            var segment = new CloudMonsterIntroTests.Segment {
                name = name, owner = owner.GetType().Name, phase = IntroPhase(owner), clip = clip,
                elapsedStart = IntroClock(owner), offset = montage.Count, rate = rate, flushedSamples = flushed,
                paused = AudioListener.pause, master = AudioListener.volume, reducedMotion = Get<bool>(shell, "ReducedMotion"),
                sourceVolume = source ? source.volume : 0, sourcePosition = source ? source.transform.position : Vector3.zero,
                cameraPosition = IntroCamera.transform.position
            };
            var values = new List<float>(); yield return CaptureAudio(values, Mathf.RoundToInt(rate * seconds) * 2, 8);
            segment.samples = values.Count; segment.elapsedEnd = IntroClock(owner); segment.phaseEnd = IntroPhase(owner);
            segment.peak = values.Max(v => Math.Abs(v)); segment.rms = Math.Sqrt(values.Average(v => v * (double)v));
            segment.clipped = values.Count(v => Math.Abs(v) >= 1);
            Assert.That(segment.clipped, Is.Zero, "First-appearance PCM clips: " + name);
            Assert.That(segment.peak, Is.LessThan(.65), "First-appearance cue exceeded its restrained peak ceiling: " + name);
            montage.AddRange(values); segments.Add(segment);
        }
        IEnumerator IntroCueSegment(string name, Component owner, float seconds, int rate,
            List<float> montage, List<CloudMonsterIntroTests.Segment> segments)
        {
            var voice = IntroVoice(owner); var source = Get<AudioSource>(voice, "Source");
            var clip = Get<AudioClip>(voice, "ActiveClip"); Assert.That(clip, Is.Not.Null);
            CloudEnemyAudioTests.SourceContract(source);
            yield return IntroAudioSegment(name, owner, source, clip.name, seconds, rate, montage, segments);
            segments[segments.Count - 1].authoredClipSha256 = CloudEnemyAudioTests.Fingerprint(clip);
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator FirstAppearanceAudioComesFromRealEventsWithQuietPauseMuteAndComfortControls()
        {
            IntroSetup(IntroOwners); int rate = AudioSettings.outputSampleRate;
            Assert.That(rate, Is.EqualTo(48000), "Bounded first-appearance evidence retains the native cloud48k stereo contract");
            var mannequin = One("WeepingAngelEncounter"); var nursery = One("AnnexEncounter");
            var archive = One("UncatAnnexEvent"); var lantern = One("LanternMaskEncounter"); var portrait = One("V1HwacatEvent");
            var montage = new List<float>(); var segments = new List<CloudMonsterIntroTests.Segment>();
            float oldCapture = Time.captureDeltaTime; bool started = false;
            try
            {
                Time.captureDeltaTime = .01f;
                foreach (var source in Object.FindObjectsByType<AudioSource>(FindObjectsInactive.Include)) source.mute = true;
                started = AudioRenderer.Start(); Assert.That(started, Is.True, "Actual first-appearance main-output PCM capture is unavailable");
                yield return IntroFlush(.25f, rate);
                yield return IntroAudioSegment("quiet-baseline", portrait, null, "none", .15f, rate, montage, segments, rate / 2);

                IntroPlace(mannequin.transform.position, mannequin.transform.position + Vector3.up * 1.25f);
                IntroCollectThrough(1);
                yield return Wait(() => Get<bool>(mannequin, "Triggered"), 2, "Natural mannequin trigger missing for PCM");
                yield return IntroCueSegment("mannequin-joint-tension", mannequin, .3f, rate, montage, segments);
                yield return Wait(() => IntroPhase(mannequin) == "PartialTurn", 2, "Natural first turn missing for PCM");
                yield return IntroCueSegment("mannequin-moving-joint", mannequin, .3f, rate, montage, segments);
                ((Behaviour)mannequin).enabled = false; // Declared source isolation after its genuine beats.

                Vector3 room = Get<Vector3>(nursery, "roomCenter"); var baby = Get<Component>(nursery, "monster");
                IntroPlace(room, baby.transform.position + Vector3.up * .65f, 2);
                yield return Wait(() => Get<bool>(nursery, "Triggered"), 2, "Natural nursery trigger missing for PCM");
                var cry = nursery.GetComponent<AudioSource>(); Assert.That(cry, Is.Not.Null);
                // The long breath rises gradually: sample its real onset after .25s,
                // not a replay or a synthetic stand-in for the room event.
                cry.mute = false; yield return IntroFlush(.25f, rate);
                yield return IntroAudioSegment("nursery-cry", nursery, cry, "First appearance NurseryWhimper", .3f, rate, montage, segments, rate / 2);
                Call(shell, "Pause"); float pausedClock = IntroClock(nursery);
                yield return IntroFlush(.2f, rate);
                yield return IntroAudioSegment("paused-live-cry", nursery, cry, "First appearance NurseryWhimper", .15f, rate, montage, segments, rate * 2 / 5);
                Assert.That(IntroClock(nursery), Is.EqualTo(pausedClock)); Call(shell, "Resume");
                yield return IntroAudioSegment("same-cry-resumed", nursery, cry, "First appearance NurseryWhimper", .25f, rate, montage, segments);
                Call(shell, "AdjustSettings", -1f, 0f); yield return IntroFlush(.2f, rate);
                yield return IntroAudioSegment("master-zero-live-cry", nursery, cry, "First appearance NurseryWhimper", .15f, rate, montage, segments, rate * 2 / 5);
                Call(shell, "RestoreDefaultSettings"); Call(shell, "ToggleReducedMotion");
                yield return Wait(() => IntroPhase(nursery) == "stillness", 4, "Nursery never entered its authored quiet gap");
                yield return IntroFlush(.2f, rate);
                yield return IntroAudioSegment("nursery-stillness-quiet", nursery, cry, "stopped original cry", .15f, rate, montage, segments, rate * 2 / 5);
                Assert.That(cry.isPlaying, Is.False);
                yield return Wait(() => IntroPhase(nursery) == "crawlReady", 3, "Natural nursery preparation contact missing for PCM");
                yield return IntroCueSegment("comfort-nursery-preparation", nursery, .25f, rate, montage, segments);
                Assert.That(Get<int>(nursery, "PreparationContacts"), Is.EqualTo(1)); ((Behaviour)nursery).enabled = false;
                Call(shell, "RestoreDefaultSettings");

                // The scrape originates at its real shelf endcap, not at a fabricated
                // off-scene acoustic fixture. The actor remains inactive at this beat.
                var shelf = Get<Transform>(archive, "ScrapeShelf"); Assert.That(shelf, Is.Not.Null);
                var shelfPoint = Get<Vector3>(archive, "ShelfCuePosition");
                IntroPlace(new Vector3(shelfPoint.x, Get<Component>(archive, "monster").transform.position.y, shelfPoint.z), shelfPoint, 2);
                Inspect("archive-record"); Assert.That(Get<Component>(archive, "monster").gameObject.activeSelf, Is.False);
                yield return IntroCueSegment("archive-shelf-scrape", archive, .3f, rate, montage, segments);
                ((Behaviour)archive).enabled = false;

                IntroPlace(lantern.transform.position, lantern.transform.position + Vector3.up * 1.25f);
                IntroCollectThrough(2);
                yield return Wait(() => Get<bool>(lantern, "IntroStarted"), 2, "Natural lantern sight missing for PCM");
                yield return IntroCueSegment("lantern-first-metal-ticks", lantern, .3f, rate, montage, segments);
                yield return Wait(() => IntroPhase(lantern) == "MaskRise", 2, "Natural mask rise missing for PCM");
                yield return IntroCueSegment("lantern-mask-rise", lantern, .3f, rate, montage, segments);
                ((Behaviour)lantern).enabled = false;

                // The real ribbon-driven StoryDirector now owns the breath and the
                // unchanged corner walk. Keep the same protected view as the original
                // full corner test; do not invoke helper.Play or mutate intro state.
                PlacePlayer(Vector3.zero, false); IntroLook(new Vector3(13.8f, 1.6f, 0));
                yield return Wait(() => Components("V1CyclopseIntro").Any(item => Get<string>(item, "Phase") == "anticipation"), 8,
                    "The real ribbon event never reached its occluded Cyclopse breath");
                var cyclopse = One("V1CyclopseIntro");
                yield return IntroCueSegment("cyclopse-hidden-breath", cyclopse, .3f, rate, montage, segments);
                Assert.That(Get<int>(cyclopse, "BreathCues"), Is.EqualTo(1)); Call(cyclopse, "Cancel");

                // Optional wraith escalation remains earned through its actual curse,
                // with the original five-second stationary escape interval intact.
                ((Behaviour)lantern).enabled = true; PlaceForLanternContact(lantern);
                yield return Wait(() => Get<int>(lantern, "CursesApplied") == 1, 6, "Legitimate contact never began wraith growth");
                Assert.That(Get<bool>(lantern, "Transformed"), Is.False);
                yield return IntroCueSegment("optional-wraith-growth", lantern, .3f, rate, montage, segments);
                Assert.That(Get<float>(lantern, "TransformProgress"), Is.LessThan(.5f));
                yield return IntroLanternPause(lantern, true);
                float progress = Get<float>(lantern, "TransformProgress");
                Vector3 growthOrigin = lantern.transform.position;
                // Move only the fixture player to a safe same-floor NavMesh view;
                // the actual cursed actor remains stationary for its original5s.
                IntroPlace(growthOrigin, growthOrigin + Vector3.up * 1.25f, 3);
                Assert.That(Vector3.Distance(player.transform.position, growthOrigin), Is.InRange(2.5f, 3.5f));
                yield return Wait(() => Get<float>(lantern, "TransformProgress") > progress + .02f, 2, "Wraith growth did not resume after pause");
                yield return Wait(() => Get<float>(lantern, "TransformProgress") >= .5f, 4, "Wraith never reached half-growth");
                Assert.That(Get<float>(lantern, "TransformProgress"), Is.LessThan(.7f));
                Assert.That(Vector3.Distance(lantern.transform.position, growthOrigin), Is.LessThan(.015f));
                IntroFrame(lantern, "intro-wraith-half-grown.png");
                yield return Wait(() => Get<bool>(lantern, "Transformed"), 7, "Original five-second transformation did not finish");
                // Observe and pause after the actual production attachment, before
                // another Update can reset its pose. No batch-mode end-of-frame wait.
                yield return IntroLanternPause(lantern, true, true);
                Assert.That(Vector3.Distance(lantern.transform.position, growthOrigin), Is.LessThan(.015f));
                PlacePlayer(growthOrigin + Vector3.up * 5, false);
                Assert.That(Get<bool>(session, "Finished"), Is.False);
                Assert.That(Get<int>(lantern, "CursesApplied"), Is.EqualTo(1));
                ((Behaviour)lantern).enabled = false;
                Call(shell, "RestoreDefaultSettings");

                Vector3 spawn = Get<Vector3>(portrait, "spawn"); IntroPlace(spawn, spawn + Vector3.up * 1.25f);
                IntroCollectThrough(3);
                yield return IntroCueSegment("portrait-frame-strain", portrait, .3f, rate, montage, segments);
                yield return Wait(() => IntroPhase(portrait) == "standUp", 3, "Real portrait impact missing for PCM");
                yield return IntroCueSegment("portrait-frame-impact", portrait, .3f, rate, montage, segments);
                yield return Wait(() => IntroPhase(portrait) == "dance", 5, "Real toy mechanism missing for PCM");
                yield return IntroCueSegment("portrait-toy-mechanism", portrait, .3f, rate, montage, segments);
                yield return Wait(() => IntroPhase(portrait) == "transform", 3, "Real angry swap missing for PCM");
                yield return IntroCueSegment("portrait-angry-jaw", portrait, .3f, rate, montage, segments);
                Call(session, "Finish", false); yield return null; yield return IntroFlush(.2f, rate);
                yield return IntroAudioSegment("result-silence", portrait, Get<AudioSource>(IntroVoice(portrait), "Source"), "stopped event", .15f, rate, montage, segments, rate * 2 / 5);

                Assert.That(44L + montage.Count * 2L, Is.LessThanOrEqualTo(1500000));
                CloudExperienceTests.Artifact("monster-first-appearances.wav", CloudExperienceTests.Wave(montage, rate, 2));
                CloudExperienceTests.Artifact("monster-first-appearances.json", System.Text.Encoding.UTF8.GetBytes(JsonUtility.ToJson(
                    new CloudMonsterIntroTests.Report { rate = rate, samples = montage.Count, segments = segments.ToArray() }, true)));
                var silent = new[] { "quiet-baseline", "paused-live-cry", "master-zero-live-cry", "nursery-stillness-quiet", "result-silence" };
                foreach (var segment in segments)
                    if (silent.Contains(segment.name)) Assert.That(segment.peak, Is.LessThanOrEqualTo(.00001), "Expected genuine silence: " + segment.name);
                    else Assert.That(segment.rms, Is.GreaterThan(.00002), "Intended actual event cue was inaudible: " + segment.name);
                Assert.That(segments.Where(item => !silent.Contains(item.name)).Select(item => item.clip).Distinct().Count(), Is.GreaterThanOrEqualTo(12));
                Assert.That(segments.Where(item => !string.IsNullOrEmpty(item.authoredClipSha256))
                    .Select(item => item.authoredClipSha256).Distinct().Count(), Is.EqualTo(12), "First-appearance cues reused a waveform");
                Debug.Log("HAPPYTOY_INTRO_PASS audio: actual public-event first appearances, native strict-renderer PCM, distinct spatial sources, low/unclipped envelope, real cry pause/resume/master zero/quiet gap, comfort and result silence");
            }
            finally
            {
                if (started) AudioRenderer.Stop(); Time.captureDeltaTime = oldCapture;
            }
        }
    }
}
