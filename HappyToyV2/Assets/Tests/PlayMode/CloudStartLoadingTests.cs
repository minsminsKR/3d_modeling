using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using static HappyToy.V2.CloudTests.RuntimeAccess;

namespace HappyToy.V2.CloudTests
{
    public sealed partial class CloudPlayModeTests
    {
        [UnityTest,Timeout(120000)]
        public IEnumerator StartLoadingRealTitleButtonFinishesApproachBeforeBlackScreenPreparationAndGatesReveal()
        {
            yield return RecoveryUiReady();Call(shell,"RestoreDefaultSettings");yield return null;
            var view=One("GameShellView");var root=Get<VisualElement>(view,"Root");var button=root.Q<Button>("begin");
            var target=root.panel.Pick(button.worldBound.center);var position=button.worldBound.center;
            using(var down=PointerDownEvent.GetPooled(new Event{type=EventType.MouseDown,mousePosition=position,button=0,clickCount=1}))target.SendEvent(down);
            yield return Delay(.04f);
            using(var up=PointerUpEvent.GetPooled(new Event{type=EventType.MouseUp,mousePosition=position,button=0,clickCount=1}))target.SendEvent(up);
            var title=One("CinematicTitleScene");float minimumMenu=1,maximumBlack=0,previousDistance=0;
            int preparingFrames=0,movingFrames=0;bool revealWhileGated=false;float minimumFloorGap=float.PositiveInfinity;
            float deadline=Time.realtimeSinceStartup+50;
            while(Get<bool>(shell,"CinematicLoading"))
            {
                Assert.That(Time.realtimeSinceStartup,Is.LessThan(deadline));
                Assert.That(Get<bool>(session,"InputAllowed"),Is.False,"Input became active before the reveal completed");
                minimumMenu=Mathf.Min(minimumMenu,Get<float>(shell,"TransitionMenuOpacity"));
                maximumBlack=Mathf.Max(maximumBlack,Get<float>(shell,"TransitionBlack"));
                float distance=Get<float>(title,"ApproachDistance");
                if(Get<bool>(shell,"CorridorPreparing"))
                {
                    preparingFrames++;
                    Assert.That(Get<float>(shell,"TransitionBlack"),Is.GreaterThanOrEqualTo(.999f),"World preparation interrupted the visible approach");
                    Assert.That(Get<AudioSource>(title,"ApproachFootstepSource").isPlaying,Is.False,"Footsteps continued into heavy world preparation");
                }
                else if(RecoveryPage=="Title"&&distance>previousDistance+.0001f)movingFrames++;
                previousDistance=distance;
                if(RecoveryPage=="Title" && distance>.01f)minimumFloorGap=Mathf.Min(minimumFloorGap,Get<float>(title,"MovingFloorGap"));
                if(RecoveryPage=="Playing" && Get<float>(shell,"TransitionBlack")<.9f && Get<bool>(shell,"IsReloading"))revealWhileGated=true;
                yield return null;
            }
            Assert.That(preparingFrames,Is.GreaterThan(5));Assert.That(movingFrames,Is.GreaterThan(3),"The actual approach never moved before preparation");
            Assert.That(minimumMenu,Is.LessThan(.01f));Assert.That(maximumBlack,Is.GreaterThan(.99f));
            Assert.That(previousDistance,Is.GreaterThan(2));Assert.That(minimumFloorGap,Is.GreaterThan(-.015f));
            Assert.That(revealWhileGated,Is.True);Assert.That(Get<bool>(session,"InputAllowed"),Is.True);
            Assert.That(RecoveryPage,Is.EqualTo("Playing"));Assert.That(Get<bool>(session,"CorridorMode"),Is.True);
            Assert.That(Get<float>(shell,"TransitionBlack"),Is.Zero);Assert.That(Time.timeScale,Is.EqualTo(1));
            AssertMainCorridor();
            Assert.That(Get<int>(title,"ApproachStepsPlayed"),Is.GreaterThanOrEqualTo(2));
            var voice=Get<AudioSource>(title,"ApproachFootstepSource");
            Assert.That(voice.ignoreListenerPause,Is.True);Assert.That(voice.ignoreListenerVolume,Is.False);
            Assert.That(voice.spatialBlend,Is.Zero);Assert.That(voice.isPlaying,Is.False,"Title footsteps leaked into gameplay");
            // The game runner is dormant here, so Awake has not loaded its owned clips yet.
            var cue=(string)RequireType("CorridorMaskAudio").GetField("ContactCue").GetRawConstantValue();
            int variants=(int)RequireType("ExternalAudio").GetMethod("VariantCount").Invoke(null,new object[]{cue});
            var titleClip=Get<AudioClip>(title,"ApproachFootstepClip");
            Assert.That(CloudExternalAudioTests.MatchesFamily(titleClip,cue,variants),Is.True,
                "Title approach must use the current in-game Baeksugwi recordings");
            Assert.That(CloudExternalAudioTests.MatchesFamily(titleClip,"mask-heavy-step",3),Is.False);
        }

        [UnityTest,Timeout(120000)]
        public IEnumerator TitleApproachRecordedContactsReachTheMixerAndObeyMasterMute()
        {
            yield return RecoveryUiReady();Call(shell,"RestoreDefaultSettings");yield return null;
            var title=One("CinematicTitleScene");
            Assert.That(Get<int>(title,"ApproachStepsPlayed"),Is.Zero,"Idle title emitted footsteps");
            float captureDelta=Time.captureDeltaTime;bool started=false;
            try
            {
                Time.captureDeltaTime=1f/60;yield return null;
                started=StartOwnedAudioRenderer();Assert.That(started,Is.True,"Process-session silence guard/main output capture unavailable");
                Call(shell,"BeginCorridorCinematic");
                yield return Wait(()=>Get<AudioSource>(title,"ApproachFootstepSource").isPlaying,8,"Approach performance never started");
                // This fixture owns manual audio rendering. Advance native PCM
                // before observing its sample cursor; waiting alone cannot do so.
                yield return ExternalFlush(AudioSettings.outputSampleRate,.15f);
                Assert.That(Get<int>(title,"ApproachStepsPlayed"),Is.GreaterThan(0));
                Assert.That(AudioListener.pause,Is.True,"Gameplay listener must remain paused during loading");
                var samples=new System.Collections.Generic.List<float>();
                yield return CaptureAudio(samples,AudioSettings.outputSampleRate*2,8);
                double rms=System.Math.Sqrt(samples.Average(x=>x*(double)x));
                Assert.That(rms,Is.GreaterThan(.0001),"Paused title approach produced silent main-output PCM");
                CloudExperienceTests.Artifact("title-approach-footsteps.wav",CloudExperienceTests.Wave(samples,AudioSettings.outputSampleRate,2));
                AudioListener.volume=0;yield return ExternalFlush(AudioSettings.outputSampleRate,.2f);
                var muted=new System.Collections.Generic.List<float>();yield return CaptureAudio(muted,AudioSettings.outputSampleRate/3*2,5);
                Assert.That(muted.Max(x=>System.Math.Abs(x)),Is.LessThan(.00001),"Title voice bypassed user master mute");
                yield return Wait(()=>!Get<bool>(shell,"CinematicLoading"),50,"Loading never ended");
                Assert.That(Get<AudioSource>(title,"ApproachFootstepSource").isPlaying,Is.False);
            }
            finally {AudioListener.volume=0;if(started)StopOwnedAudioRenderer();Time.captureDeltaTime=captureDelta;}
        }

        [UnityTest,Timeout(120000)]
        public IEnumerator DrawerRealEOpensAndClosesAtTouchingDistanceWithoutShovingPlayerAndRestoresCollisionPairs()
        {
            Call(session,"CreateCorridor",73);IsolateThreats();Begin();yield return null;yield return null;
            foreach(string id in new[]{"drawer-atmosphere-0","drawer-supply-0"})
            {
            var drawer=FurnitureDrawers.First(x=>Get<string>(x,"StableId")==id);
            var furniture=drawer.GetComponentInParent(RequireType("CorridorFurniturePlacement"));
            var front=drawer.GetComponentsInChildren<Collider>(true).Single(x=>x.name=="Drawer physical front and handle target");
            var outward=furniture.transform.TransformDirection(Vector3.back).normalized;
            var controller=player.GetComponent<CharacterController>();
            Vector3 close=front.bounds.center+outward*(controller.radius+.022f+.035f);close.y=.03f;
            PlacePlayer(close);yield return null;yield return null;
            yield return FurniturePressE(DrawerInteraction(drawer));
            yield return Wait(()=>Get<bool>(drawer,"IsOpen")&&Get<bool>(drawer,"AtRequestedPose"),3,"Touching player still blocked drawer opening");
            Assert.That(Get<float>(drawer,"Travel"),Is.EqualTo(.3f).Within(.001f));
            Assert.That(Vector3.Distance(player.transform.position,close),Is.LessThan(.06f),"Opening shoved the player");
            Assert.That(Physics.GetIgnoreCollision(front,controller),Is.True,"Overlapping moving tray should not trap the controller");
            yield return FurniturePressE(DrawerInteraction(drawer));
            yield return Wait(()=>!Get<bool>(drawer,"IsOpen")&&Get<bool>(drawer,"AtRequestedPose"),3,"Touching player still blocked drawer closing");
            PlacePlayer(close+outward*1.2f);yield return null;yield return null;
            Assert.That(Physics.GetIgnoreCollision(front,controller),Is.False,"Tray collision pair was not restored after the player cleared it");
            // The furniture body continues to collide normally; only its tray/controller pairs change.
            foreach(var fixedPart in furniture.GetComponentsInChildren<Collider>().Where(x=>!x.transform.IsChildOf(drawer.transform)&&!x.isTrigger))
                Assert.That(Physics.GetIgnoreCollision(fixedPart,controller),Is.False);
            }
        }
    }
}
