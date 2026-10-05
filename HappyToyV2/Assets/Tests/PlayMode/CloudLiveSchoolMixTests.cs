using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Unity.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;
using static HappyToy.V2.CloudTests.RuntimeAccess;
using Object = UnityEngine.Object;

namespace HappyToy.V2.CloudTests
{
    // This observer never changes audio gain, mute, playback, AI or navigation.
    // The existing complete school strategy supplies every route input/event.
    internal sealed class CloudLiveSchoolMixCapture
    {
        [Serializable] internal sealed class Source
        {
            public string name, clip;
            public bool playing, active, muted;
            public float gain, blend, pitch, listenerDistance;
            public Vector3 position;
        }
        [Serializable] internal sealed class Segment
        {
            public string name, file;
            public float gameSeconds, stamina, stress;
            public long routeSampleOffset;
            public int samples, records, steps, contacts, recognition, clipped;
            public bool hidden, paused;
            public double rms, peak;
            public Vector3 listenerPosition;
            public Source[] voices;
        }
        [Serializable] internal sealed class Floor
        {
            public string name;
            public long samples, clipped;
            public double rms, peak;
            [NonSerialized] internal double sum;
        }
        [Serializable] internal sealed class Report
        {
            public string capture = "UnityAudioRendererMainOutput";
            public string scope = "All live production voices during the existing complete keyboard/mouse five-memory school route. Streaming whole-route PCM health plus event-triggered excerpts; no source isolation, replacement PCM, enemy deactivation or speed edits. Camera images are actual Unity game-camera renders, not native executable screenshots or human listening/fear certification.";
            public int rate, channels = 2, outputFrames, emptyFrames, clipped, nonfinite;
            public long totalSamples;
            public double rms, peak, dspSeconds;
            public float gameSeconds;
            public Floor[] floors;
            public Segment[] contexts;
        }
        sealed class Window
        {
            internal Segment description;
            internal readonly List<float> samples = new List<float>();
            internal int target;
        }
        readonly Component session, player, feedback, tension, portrait;
        readonly Camera camera;
        readonly Dictionary<string, Window> windows = new Dictionary<string, Window>();
        readonly Floor[] floors = {new Floor {name="ground"},new Floor {name="upper"},new Floor {name="basement"}};
        readonly int rate;
        readonly double dspStart;
        int outputFrames,emptyFrames,clipped,nonfinite;
        int guidanceStep=-1,guidanceChangedFrame;
        long totalSamples;
        double sum,peak;
        bool entranceImage,portraitImage,nurseryImage;

        internal CloudLiveSchoolMixCapture(Component session,Component player)
        {
            this.session=session;this.player=player;
            feedback=Get<Component>(player,"Feedback");tension=player.GetComponent(RequireType("PerceivedTension"));
            portrait=Get<Component>(Get<Component>(session,"Chapter"),"Portrait");camera=Get<Camera>(player,"eyes");
            rate=AudioSettings.outputSampleRate;dspStart=AudioSettings.dspTime;
            Assert.That(AudioSettings.speakerMode,Is.EqualTo(AudioSpeakerMode.Stereo));
            Add("opening-ground-mix");
        }
        void Add(string name,float seconds=.8f)
        {
            if(windows.ContainsKey(name))return;
            var voices=Object.FindObjectsByType<AudioSource>(FindObjectsInactive.Include)
                .Where(source=>source.gameObject.scene==session.gameObject.scene).Select(source=>new Source {
                    name=source.name,clip=source.clip?source.clip.name:"one-shot or empty",gain=source.volume,
                    blend=source.spatialBlend,pitch=source.pitch,playing=source.isPlaying,active=source.isActiveAndEnabled,
                    muted=source.mute,position=source.transform.position,
                    listenerDistance=Vector3.Distance(source.transform.position,camera.transform.position)}).ToArray();
            Assert.That(voices.All(voice=>!voice.muted),Is.True,"A live route voice was isolated/muted by the test");
            windows.Add(name,new Window {target=Mathf.CeilToInt(rate*seconds)*2,description=new Segment {
                name=name,file="live-school-mix-"+name+".wav",routeSampleOffset=totalSamples,
                gameSeconds=Get<float>(session,"ElapsedPlayTime"),records=Get<int>(session,"RecordsRecovered"),
                steps=Get<int>(feedback,"FootstepsPlayed"),stamina=Get<float>(player,"Stamina"),
                hidden=Get<bool>(player,"Hidden"),paused=AudioListener.pause,
                stress=Get<float>(tension,"Stress"),contacts=Get<int>(tension,"ContactEvents"),
                recognition=Get<int>(tension,"RecognitionEvents"),listenerPosition=camera.transform.position,voices=voices}});
        }
        void Frame(string filename)
        {
            Assert.That(SystemInfo.graphicsDeviceType,Is.Not.EqualTo(GraphicsDeviceType.Null));
            var target=new RenderTexture(640,360,24);target.Create();
            try
            {
                RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest {destination=target});
                CloudExperienceTests.SaveFrame(filename,target);
            }
            finally{target.Release();Object.Destroy(target);}
        }
        void Observe()
        {
            int recovered=Get<int>(session,"RecordsRecovered");
            if(recovered!=guidanceStep){guidanceStep=recovered;guidanceChangedFrame=Time.frameCount;}
            else if(Get<bool>(session,"InputAllowed")&&Time.frameCount>guidanceChangedFrame+2)
            {
                string objective=Get<string>(session,"CurrentObjectiveId");
                foreach(var resonance in Components("MemoryResonance"))
                {
                    var item=resonance.GetComponent(RequireType("Interactable"));
                    bool expected=resonance.gameObject.activeInHierarchy&&Get<string>(item,"stableId")==objective;
                    Assert.That(Get<bool>(resonance,"GuidingCurrentMemory"),Is.EqualTo(expected),"Memory audio guides a future/uncollectable clue");
                    if(!expected)
                    {
                        var voice=Get<AudioSource>(resonance,"Source");
                        Assert.That(voice.isPlaying,Is.False,"A future memory rings under the full live scene mix");
                        if(resonance.gameObject.activeInHierarchy)Assert.That(voice.volume,Is.Zero);
                    }
                }
            }
            if(!entranceImage) {entranceImage=true;Frame("live-school-mix-entrance.png");}
            if(Get<int>(feedback,"FootstepsPlayed")>0)
            {
                string surface=Get<string>(feedback,"LastFootstepSurface");
                if(surface=="wood")Add("ground-walk");
                if(surface=="wet")Add("basement-wet");
                if(surface=="stone")Add("washroom-stone");
            }
            if(Get<bool>(player,"Running")&&Get<float>(player,"Stamina")<.5f)Add("physical-sprint");
            if(Get<int>(tension,"ContactEvents")>0)Add("heard-enemy",1.2f);
            if(Get<int>(tension,"RecognitionEvents")>0)Add("witnessed-recognition",1.2f);
            if(recovered>0)
            {
                string label="memory-"+recovered;
                if(!windows.ContainsKey(label))
                {
                    Assert.That(CloudExternalAudioTests.MatchesFamily(Get<AudioClip>(feedback,"LastInteractionClip"),"discovery",3),Is.True,
                        "Natural memory interaction did not bind recorded discovery audio");Add(label);
                }
            }
            foreach(var door in Components("InteractionAudio"))
                if(Get<int>(door,"CuesPlayed")>0)Add("authored-door");
            if(Get<int>(portrait,"FrameStrainCues")>0)
            {
                Add("portrait-strain");
                if(!portraitImage){portraitImage=true;Frame("live-school-mix-portrait.png");}
            }
            if(Get<int>(portrait,"FrameImpactCues")>0)Add("portrait-impact",.45f);
            if(player.transform.position.y< -4.5f&&!nurseryImage)
            {nurseryImage=true;Frame("live-school-mix-nursery.png");}
        }
        internal void Pump()
        {
            Observe();
            int count=AudioRenderer.GetSampleCountForCaptureFrame();
            Assert.That(count,Is.InRange(0,rate*2));
            if(count==0)
            {
                // A zero-length native render can make Unity's next output frame
                // available. It contributes no samples and never fabricates silence.
                emptyFrames++;
                using(var empty=new NativeArray<float>(0,Allocator.Temp))AudioRenderer.Render(empty);
                count=AudioRenderer.GetSampleCountForCaptureFrame();
                if(count==0)return;
            }
            Assert.That(count,Is.InRange(1,rate*2));
            using(var data=new NativeArray<float>(count*2,Allocator.Temp))
            {
                Assert.That(AudioRenderer.Render(data),Is.True,"Whole-school mixer output unavailable");outputFrames++;
                int floorIndex=player.transform.position.y>3?1:player.transform.position.y< -2?2:0;
                var floor=floors[floorIndex];
                var pending=windows.Values.Where(window=>window.samples.Count<window.target).ToArray();
                for(int i=0;i<data.Length;i++)
                {
                    float value=data[i];bool finite=!float.IsNaN(value)&&!float.IsInfinity(value);
                    if(!finite){nonfinite++;continue;}
                    double amplitude=Math.Abs(value);sum+=value*(double)value;peak=Math.Max(peak,amplitude);totalSamples++;
                    floor.samples++;floor.sum+=value*(double)value;floor.peak=Math.Max(floor.peak,amplitude);
                    if(amplitude>=1){clipped++;floor.clipped++;}
                    foreach(var window in pending)if(window.samples.Count<window.target)window.samples.Add(value);
                }
            }
        }
        internal void ExportAndAssert(float gameSeconds)
        {
            foreach(var window in windows.Values)
            {
                var values=window.samples;var context=window.description;
                context.samples=values.Count;
                context.peak=values.Count>0?values.Max(value=>Math.Abs(value)):0;
                context.rms=values.Count>0?Math.Sqrt(values.Average(value=>value*(double)value)):0;
                context.clipped=values.Count(value=>Math.Abs(value)>=1);
                CloudExperienceTests.Artifact(context.file,CloudExperienceTests.Wave(values,rate,2));
            }
            foreach(var floor in floors)floor.rms=floor.samples>0?Math.Sqrt(floor.sum/floor.samples):0;
            var report=new Report {rate=rate,outputFrames=outputFrames,emptyFrames=emptyFrames,totalSamples=totalSamples,
                rms=totalSamples>0?Math.Sqrt(sum/totalSamples):0,peak=peak,clipped=clipped,nonfinite=nonfinite,
                dspSeconds=AudioSettings.dspTime-dspStart,gameSeconds=gameSeconds,floors=floors,contexts=windows.Values.Select(window=>window.description).ToArray()};
            CloudExperienceTests.Artifact("live-school-whole-mix.json",System.Text.Encoding.UTF8.GetBytes(JsonUtility.ToJson(report,true)));
            Assert.That(nonfinite,Is.Zero,"Whole natural route produced nonfinite PCM");
            Assert.That(clipped,Is.Zero,"Whole natural school route clips the combined production mix");
            Assert.That(totalSamples,Is.GreaterThan(rate*20*2),"Only a narrow isolated sample was captured instead of the live route");
            foreach(string context in new[]{"opening-ground-mix","ground-walk","authored-door","physical-sprint","heard-enemy",
                "memory-1","memory-2","memory-3","memory-4","memory-5","portrait-strain","portrait-impact","basement-wet"})
                Assert.That(windows.ContainsKey(context),Is.True,"Natural school route never produced expected context: "+context);
            foreach(var window in windows.Values)
            {
                Assert.That(window.samples.Count,Is.EqualTo(window.target),"Incomplete genuine context recording: "+window.description.name);
                Assert.That(window.description.rms,Is.GreaterThan(.00002),"Whole production mix is silent in context: "+window.description.name);
            }
            foreach(var floor in floors)
            {
                Assert.That(floor.samples,Is.GreaterThan(rate*2));
                Assert.That(floor.rms,Is.GreaterThan(.00002),"Actual route floor mix is silent: "+floor.name);
            }
            Assert.That(entranceImage&&portraitImage&&nurseryImage,Is.True,"Missing real game-camera floor/event capture");
        }
    }

    public sealed partial class CloudPlayModeTests
    {
        [UnityTest,Timeout(750000)]
        public IEnumerator SituationalWholeSchoolMixSurvivesTheActualFiveMemoryInputRoute()
        {
            Call(session,"ConfigureRecordDirectory",NewRecordFixture());Call(shell,"RestoreDefaultSettings");
            float previousCapture=Time.captureDeltaTime;bool started=false;
            try
            {
                Time.captureDeltaTime=1f/60;yield return null;
                started=AudioRenderer.Start();Assert.That(started,Is.True);Call(shell,"BeginChapter");yield return null;
                var capture=new CloudLiveSchoolMixCapture(session,player);
                using(var route=new CloudSurvivalTests(session,player,shell,Keys,InputDiagnostics))
                {
                    // Flatten only the test route's enumerators so every genuine
                    // input frame also renders the actual native mixer once.
                    var stack=new Stack<IEnumerator>();stack.Push(route.Run());
                    while(stack.Count>0)
                    {
                        var current=stack.Peek();
                        if(!current.MoveNext()) {stack.Pop();(current as IDisposable)?.Dispose();continue;}
                        if(current.Current is IEnumerator nested){stack.Push(nested);continue;}
                        yield return current.Current;capture.Pump();
                    }
                }
                Assert.That(Get<bool>(session,"Escaped"),Is.True);Assert.That(Get<int>(session,"RecordsRecovered"),Is.EqualTo(5));
                capture.ExportAndAssert(Get<float>(session,"ElapsedPlayTime"));
                Debug.Log("HAPPYTOY_SITUATIONAL_MIX_PASS whole actual keyboard/mouse chapter route, all production audio voices, three floor mixes, discovery/door/portrait/wet/sprint/perceived-enemy contexts and real game-camera renders");
            }
            finally{Keys();if(started)AudioRenderer.Stop();Time.captureDeltaTime=previousCapture;}
        }

        [UnityTest,Timeout(120000)]
        public IEnumerator SituationalLiveExertionCabinetAndPauseMixSurvivesRealRetryCleanup()
        {
            Call(session,"ConfigureRecordDirectory",NewRecordFixture());Call(shell,"RestoreDefaultSettings");
            float previousCapture=Time.captureDeltaTime;bool started=false;
            var montage=new List<float>();var segments=new List<CloudExternalAudioTests.Segment>();
            AudioSource[] oldSources=null;AudioClip[] oldClips=null;
            try
            {
                Time.captureDeltaTime=1f/60;yield return null;started=AudioRenderer.Start();Assert.That(started,Is.True);
                Call(shell,"BeginChapter");yield return null;
                var feedback=Get<Component>(player,"Feedback");int rate=AudioSettings.outputSampleRate;
                Assert.That(Components("StalkerBrain").All(actor=>!actor.gameObject.activeInHierarchy),Is.True,
                    "Opening-school exercise must occur before the first real memory awakens threats");
                var ambient=One("RoomAmbience");var voices=((IEnumerable)Get<object>(ambient,"Voices")).Cast<object>().ToArray();
                var bed=Get<AudioSource>(voices[2],"source");
                var drip=Object.FindObjectsByType<AudioSource>(FindObjectsInactive.Include).Single(source=>source.name=="Basement distant water drops");
                Assert.That(CloudExternalAudioTests.MatchesFamily(bed.clip,"ambience-basement-bed",1),Is.True);
                Assert.That(CloudExternalAudioTests.MatchesFamily(drip.clip,"ambience-basement",1),Is.True);
                Assert.That(CloudExternalAudioTests.Fingerprint(bed.clip),Is.Not.EqualTo(CloudExternalAudioTests.Fingerprint(drip.clip)),
                    "Basement layers double the same recorded drip stream");
                PlacePlayer(new Vector3(-7.8f,.02f,0));yield return ExternalFlush(rate,.3f);
                yield return Wait(()=>Get<bool>(player,"Grounded"),3,"Real opening-school run never grounded");
                Vector3 began=player.transform.position;float stamina=Get<float>(player,"Stamina");int steps=Get<int>(feedback,"FootstepsPlayed");
                yield return KeysObserved(Key.D,Key.LeftShift);
                for(int index=0;index<4;index++)
                    yield return ExternalCapture("unselected-live-sprint-"+index,Get<AudioSource>(feedback,"FootstepSource"),
                        Get<AudioClip>(feedback,"LastFootstepClip"),"all-live-voices",.8f,rate,montage,segments);
                Keys();yield return null;
                Assert.That(Vector3.Distance(began,player.transform.position),Is.GreaterThan(8),"Exertion PCM had no substantial physical sprint");
                Assert.That(Get<float>(player,"Stamina"),Is.LessThan(stamina-.4f));
                Assert.That(Get<int>(feedback,"FootstepsPlayed"),Is.GreaterThan(steps+3));
                var breathing=Object.FindObjectsByType<AudioSource>().Single(source=>source.name=="Player breathing");
                Assert.That(breathing.isPlaying,Is.True);Assert.That(breathing.volume,Is.GreaterThan(.02f),"Real exertion did not raise the breathing layer");
                var cabinet=Components("Interactable").First(item=>Get<object>(item,"kind").ToString()=="HidingPlace"&&Mathf.Abs(item.transform.position.y)<.2f);
                PlacePlayer(Get<Transform>(cabinet,"outside").position);yield return null;
                Call(cabinet,"Use",player);Assert.That(Get<bool>(player,"Hidden"),Is.True);
                Assert.That(CloudExternalAudioTests.MatchesFamily(Get<AudioClip>(feedback,"LastInteractionClip"),"cabinet-close",1),Is.True);
                int hiddenSteps=Get<int>(feedback,"FootstepsPlayed");float resting=Get<float>(player,"Stamina");
                yield return ExternalCapture("unselected-live-cabinet-enter",Get<AudioSource>(feedback,"InteractionSource"),
                    Get<AudioClip>(feedback,"LastInteractionClip"),"all-live-voices",.5f,rate,montage,segments);
                Assert.That(Get<int>(feedback,"FootstepsPlayed"),Is.EqualTo(hiddenSteps));
                Assert.That(Get<float>(player,"Stamina"),Is.GreaterThan(resting+.035f));
                var camera=Get<Camera>(player,"eyes");var target=new RenderTexture(640,360,24);target.Create();
                try{RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=target});CloudExperienceTests.SaveFrame("live-school-mix-cabinet.png",target);}
                finally{target.Release();Object.Destroy(target);}
                Call(shell,"Pause");float pausedStamina=Get<float>(player,"Stamina");Vector3 pausedAt=player.transform.position;
                yield return ExternalFlush(rate,.2f);
                yield return ExternalCapture("unselected-live-paused",breathing,breathing.clip,"all-live-voices",.3f,rate,montage,segments);
                Assert.That(segments.Last().peak,Is.LessThan(.00001),"Pause leaks the combined live school mix");
                Assert.That(Get<float>(player,"Stamina"),Is.EqualTo(pausedStamina));Assert.That(player.transform.position,Is.EqualTo(pausedAt));
                Assert.That(Get<int>(feedback,"FootstepsPlayed"),Is.EqualTo(hiddenSteps));Call(shell,"Resume");
                yield return ExternalCapture("unselected-live-resumed",breathing,breathing.clip,"all-live-voices",.5f,rate,montage,segments);
                Assert.That(segments.Last().rms,Is.GreaterThan(.00002),"Whole scene mix never resumed from cabinet rest");
                Call(cabinet,"Use",player);Assert.That(Get<bool>(player,"Hidden"),Is.False);
                yield return ExternalCapture("unselected-live-cabinet-exit",Get<AudioSource>(feedback,"InteractionSource"),
                    Get<AudioClip>(feedback,"LastInteractionClip"),"all-live-voices",.3f,rate,montage,segments);
                Assert.That(Object.FindObjectsByType<AudioSource>(FindObjectsInactive.Include).All(source=>!source.mute),Is.True,
                    "Situational fixture muted a production voice");
                oldSources=Object.FindObjectsByType<AudioSource>(FindObjectsInactive.Include).Where(source=>source.gameObject.scene==session.gameObject.scene).ToArray();
                oldClips=oldSources.Select(source=>source.clip).Where(clip=>clip).Concat(new[]{Get<AudioClip>(feedback,"LastFootstepClip"),
                    Get<AudioClip>(feedback,"LastInteractionClip")}).Where(clip=>clip).Distinct().ToArray();
                CloudExperienceTests.Artifact("live-school-exertion-cabinet-pause.wav",CloudExperienceTests.Wave(montage,rate,2));
                CloudExperienceTests.Artifact("live-school-exertion-cabinet-pause.json",System.Text.Encoding.UTF8.GetBytes(JsonUtility.ToJson(
                    new CloudExternalAudioTests.Report {scope="All production voices together during actual opening-school keyboard sprint, positioned actual cabinet Use, rest, pause/resume and exit. No voice selection, replacement audio or threat deactivation. Separate complete input route covers live enemy mixing.",segments=segments.ToArray()},true)));
                var previous=session;Call(shell,"Restart",true);
                yield return Wait(()=>Components("GameSession").Length==1&&One("GameSession")!=previous,30,"Live school mix retry did not reload");
                yield return null;yield return null;
                Assert.That(oldSources.All(source=>source==null),Is.True,"Retry leaked old live audio emitters");
                Assert.That(oldClips.All(clip=>clip==null),Is.True,"Retry leaked scene-owned recorded/procedural clips");
                Assert.That(CloudExternalAudioTests.Shared("ambience-basement-bed"),Is.Not.Null,"Retry destroyed shared recorded bed");
                session=One("GameSession");player=Get<Component>(session,"player");shell=Get<Component>(session,"Shell");
                Assert.That(Get<int>(session,"RecordsRecovered"),Is.Zero);Assert.That(Get<bool>(session,"ChapterMode"),Is.True);
                Debug.Log("HAPPYTOY_SITUATIONAL_MIX_PASS actual sprint breathing and footsteps, recorded cabinet foley/continued rest, all-voice pause/resume, distinct B1 room bed versus drips, complete scene audio resource teardown on retry");
            }
            finally{Keys();if(started)AudioRenderer.Stop();Time.captureDeltaTime=previousCapture;}
        }
    }
}
