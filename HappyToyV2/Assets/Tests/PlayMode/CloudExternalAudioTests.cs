using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using static HappyToy.V2.CloudTests.RuntimeAccess;
using Object = UnityEngine.Object;

namespace HappyToy.V2.CloudTests
{
    // Imported assets and genuine production events in the protected school. The
    // PCM evidence is the Unity main output before the device, not a listening verdict.
    internal static class CloudExternalAudioTests
    {
        [Serializable] internal sealed class Segment
        {
            public string name, clip, cue, surface, collider;
            public int samples, rate, channels = 2, offset, clipped, emitted;
            public float master, volume;
            public bool paused, occluded;
            public Vector3 source, listener;
            public double peak, rms;
        }
        [Serializable] internal sealed class Report
        {
            public string capture = "UnityAudioRendererMainOutput";
            public string scope;
            public bool syntheticPcm = false, fullRouteEvidence = false, deviceListeningCertification = false;
            public Segment[] segments;
        }
        internal static AudioClip Shared(string cue, int variant = 0)
        { return (AudioClip)Call(RequireType("ExternalAudio"), "Shared", cue, variant); }
        internal static string Fingerprint(AudioClip clip)
        { return CloudEnemyAudioTests.Fingerprint(clip); }
        internal static bool MatchesFamily(AudioClip actual, string cue, int variants)
        {
            string actualHash = Fingerprint(actual);
            return Enumerable.Range(0, variants).Any(index => Fingerprint(Shared(cue, index)) == actualHash);
        }
        internal static Segment Measure(string label, List<float> values, int rate, int offset, AudioSource source,
            Vector3 listener, string cue = null, AudioClip clip = null)
        {
            Assert.That(values.Count, Is.GreaterThan(0), "No actual engine samples: " + label);
            Assert.That(values.All(value => !float.IsNaN(value) && !float.IsInfinity(value)), Is.True);
            var segment = new Segment {
                name = label, cue = cue, clip = clip ? clip.name : source && source.clip ? source.clip.name : "",
                samples = values.Count, offset = offset, rate = rate, master = AudioListener.volume, paused = AudioListener.pause,
                volume = source ? source.volume : 0, source = source ? source.transform.position : Vector3.zero,
                listener = listener, peak = values.Max(value => Math.Abs(value)),
                rms = Math.Sqrt(values.Average(value => value * (double)value)),
                clipped = values.Count(value => Math.Abs(value) >= 1)
            };
            Assert.That(segment.clipped, Is.Zero, "Imported sound clips the real mixer: " + label);
            return segment;
        }
    }

    public sealed partial class CloudPlayModeTests
    {
        [UnityTest, Timeout(45000)]
        public IEnumerator ExternalCatalogContainsImportedRecordedCuesAndIndependentOwnedCopies()
        {
            var groups = new Dictionary<string, int> {
                {"step-wood",5}, {"step-stone",5}, {"step-wet",3},
                {"door-open",2}, {"door-close",4}, {"door-seat",1},
                {"cabinet",1}, {"cabinet-rustle",4}, {"flashlight",1}, {"discovery",3},
                {"cabinet-open",1}, {"cabinet-close",1},
                {"frame-strain",3}, {"frame-impact",3},
                {"ambience-ground",1}, {"ambience-upper",1}, {"ambience-basement",1}, {"ambience-basement-bed",1},
                {"enemy-cyclopse-movement",3}, {"enemy-cyclopse-attack",1},
                {"enemy-hwacat-movement",3}, {"enemy-hwacat-attack",1},
                {"enemy-uncat-movement",3}, {"enemy-uncat-attack",1},
                {"enemy-baby-movement",3}, {"enemy-baby-attack",1},
                {"enemy-lantern-movement",1}, {"enemy-lantern-attack",1},
                {"enemy-wraith-movement",1}, {"enemy-wraith-attack",1}
            };
            foreach (var group in groups)
            {
                var fingerprints = new HashSet<string>();
                for (int index = 0; index < group.Value; index++)
                {
                    var clip = CloudExternalAudioTests.Shared(group.Key, index);
                    Assert.That(clip, Is.Not.Null, "Required external cue missing: " + group.Key + "/" + index);
                    Assert.That(clip.loadState, Is.EqualTo(AudioDataLoadState.Loaded));
                    Assert.That(clip.channels, Is.InRange(1,2));
                    Assert.That(clip.frequency, Is.GreaterThanOrEqualTo(22050));
                    Assert.That(clip.length, Is.GreaterThan(.025f));
                    Assert.That(fingerprints.Add(CloudExternalAudioTests.Fingerprint(clip)), Is.True,
                        "Advertised variants repeat the same recording: " + group.Key);
#if UNITY_EDITOR
                    string path = UnityEditor.AssetDatabase.GetAssetPath(clip);
                    Assert.That(path, Does.StartWith("Assets/Resources/"), "Cue is a generated runtime clip rather than an imported resource");
                    Assert.That(path.EndsWith(".wav", StringComparison.OrdinalIgnoreCase) ||
                        path.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase), Is.True, path);
#endif
                }
            }
            foreach (string surface in new[] {"wood","stone","wet"})
            {
                string hash = CloudExternalAudioTests.Fingerprint(CloudExternalAudioTests.Shared("step-" + surface));
                foreach (string other in new[] {"wood","stone","wet"}.Where(value => value != surface))
                    Assert.That(hash, Is.Not.EqualTo(CloudExternalAudioTests.Fingerprint(CloudExternalAudioTests.Shared("step-" + other))),
                        "Different floor materials still share one footstep recording");
            }
            Assert.That(CloudExternalAudioTests.Fingerprint(CloudExternalAudioTests.Shared("door-open")),
                Is.Not.EqualTo(CloudExternalAudioTests.Fingerprint(CloudExternalAudioTests.Shared("door-close"))),
                "Closing is still only a pitch change of opening");

            var shared = CloudExternalAudioTests.Shared("frame-impact");
            var first = (AudioClip)Call(RequireType("ExternalAudio"), "Owned", "frame-impact", 0);
            var second = (AudioClip)Call(RequireType("ExternalAudio"), "Owned", "frame-impact", 0);
            try
            {
                Assert.That(first, Is.Not.SameAs(shared)); Assert.That(second, Is.Not.SameAs(shared));
                Assert.That(first, Is.Not.SameAs(second), "One actor's cleanup can destroy another actor's audio");
                string hash = CloudExternalAudioTests.Fingerprint(shared);
                Assert.That(CloudExternalAudioTests.Fingerprint(first), Is.EqualTo(hash));
                Assert.That(CloudExternalAudioTests.Fingerprint(second), Is.EqualTo(hash));
                Object.Destroy(first); yield return null;
                Assert.That(first == null, Is.True);
                Assert.That(shared && second, Is.True, "Destroying an owned voice destroyed an imported asset or another voice");
                Assert.That(CloudExternalAudioTests.Fingerprint(shared), Is.EqualTo(hash));
                Assert.That(CloudExternalAudioTests.Fingerprint(second), Is.EqualTo(hash));
            }
            finally { if (first) Object.Destroy(first); if (second) Object.Destroy(second); }
            Debug.Log("HAPPYTOY_EXTERNAL_AUDIO_PASS imported recorded resources, real distinct variants/materials/directions, independent owned copies");
        }

        [UnityTest, Timeout(55000)]
        public IEnumerator ExternalFootstepsFollowRealSchoolMaterialsAndFlashlightKeyboardInput()
        {
            Call(shell, "BeginChapter"); yield return null; IsolateThreats(); Call(shell, "RestoreDefaultSettings");
            var feedback = Get<Component>(player, "Feedback");
            var positions = new[] {new Vector3(-7.8f,.02f,0),new Vector3(-4.5f,.02f,-3.2f),new Vector3(13.8f,-4.98f,-25.5f)};
            var surfaces = new[] {"wood","stone","wet"}; var directions = new[] {Key.D,Key.D,Key.S};
            var records = new List<CloudExternalAudioTests.Segment>();
            for (int index = 0; index < positions.Length; index++)
            {
                Keys(); yield return null; PlacePlayer(positions[index]);
                yield return Wait(() => Get<bool>(player,"Grounded"), 3, "No actual school floor: " + surfaces[index]);
                int before = Get<int>(feedback,"FootstepsPlayed"); Vector3 origin = player.transform.position;
                yield return KeysObserved(directions[index]);
                yield return Wait(() => Get<int>(feedback,"FootstepsPlayed") > before, 2,
                    "Natural keyboard movement emitted no step on " + surfaces[index], InputDiagnostics);
                Keys(); yield return null;
                Assert.That(Vector3.Distance(origin,player.transform.position), Is.GreaterThan(.5f), "No real travel accompanied the imported step");
                Assert.That(Get<string>(feedback,"LastFootstepSurface"), Is.EqualTo(surfaces[index]),
                    "Wrong school floor cue: " + Get<string>(feedback,"LastFootstepColliderName"));
                var clip = Get<AudioClip>(feedback,"LastFootstepClip");
                Assert.That(CloudExternalAudioTests.MatchesFamily(clip,"step-"+surfaces[index],surfaces[index]=="wet"?3:5), Is.True,
                    "Footstep event did not use an imported recording for its physical floor");
                records.Add(new CloudExternalAudioTests.Segment {name="actual-school-footstep",surface=surfaces[index],
                    collider=Get<string>(feedback,"LastFootstepColliderName"),clip=clip.name,emitted=Get<int>(feedback,"FootstepsPlayed")-before,
                    source=Get<AudioSource>(feedback,"FootstepSource").transform.position,listener=Get<Camera>(player,"eyes").transform.position});
                int stopped=Get<int>(feedback,"FootstepsPlayed"); yield return Delay(.15f);
                Assert.That(Get<int>(feedback,"FootstepsPlayed"), Is.EqualTo(stopped), "Stationary school floor emits phantom steps");
                Call(shell,"Pause"); yield return Delay(.15f);
                Assert.That(Get<int>(feedback,"FootstepsPlayed"), Is.EqualTo(stopped)); Call(shell,"Resume");
            }
            var light=Get<Light>(player,"flashlight"); bool lit=light.enabled;int interactions=Get<int>(feedback,"InteractionCuesPlayed");
            yield return KeysObserved(Key.F); Keys(); yield return null;
            Assert.That(light.enabled, Is.EqualTo(!lit), "F input did not toggle the actual flashlight");
            Assert.That(Get<AudioSource>(feedback,"InteractionSource").isPlaying, Is.True, "Flashlight input made no production foley");
            Assert.That(Get<int>(feedback,"InteractionCuesPlayed"),Is.EqualTo(interactions+1));
            Assert.That(CloudExternalAudioTests.MatchesFamily(Get<AudioClip>(feedback,"LastInteractionClip"),"flashlight",1),Is.True,
                "Actual F input did not use the recorded flashlight switch");
            CloudExperienceTests.Artifact("external-school-materials.json",System.Text.Encoding.UTF8.GetBytes(JsonUtility.ToJson(
                new CloudExternalAudioTests.Report {scope="Actual keyboard/CharacterController contacts on ground wood, washroom stone and basement water; this is not a continuous survival route.",segments=records.ToArray()},true)));
            Debug.Log("HAPPYTOY_EXTERNAL_AUDIO_PASS real school wood/stone/water foot contacts and flashlight input, stationary/pause silence");
        }

        void ExternalMuteExcept(params AudioSource[] selected)
        {
            foreach (var source in Object.FindObjectsByType<AudioSource>(FindObjectsInactive.Include))
            { source.mute=!selected.Contains(source); if(source.mute)source.Stop(); }
        }
        IEnumerator ExternalFlush(int rate,float seconds=.2f)
        {var discard=new List<float>();yield return CaptureAudio(discard,Mathf.CeilToInt(rate*seconds)*2,8);}
        IEnumerator ExternalCapture(string name, AudioSource source, AudioClip clip, string cue, float seconds, int rate,
            List<float> montage,List<CloudExternalAudioTests.Segment> segments)
        {
            var samples=new List<float>();yield return CaptureAudio(samples,Mathf.CeilToInt(rate*seconds)*2,8);
            var segment=CloudExternalAudioTests.Measure(name,samples,rate,montage.Count,source,Get<Camera>(player,"eyes").transform.position,cue,clip);
            montage.AddRange(samples);segments.Add(segment);
        }

        [UnityTest, Timeout(90000)]
        public IEnumerator ExternalSchoolDoorMixFollowsLeafPauseDistanceMasterVolumeAndRetry()
        {
            Call(shell,"BeginChapter"); yield return null; IsolateThreats(); Call(shell,"RestoreDefaultSettings");
            ((Behaviour)player).enabled=false; ((Behaviour)Get<Component>(player,"Feedback")).enabled=false;
            var door=Components("Interactable").First(item=>Get<object>(item,"kind").ToString()=="Door" &&
                Get<Transform>(item,"movingLeaf") && item.name.IndexOf("WASHROOM",StringComparison.OrdinalIgnoreCase)>=0);
            var leaf=Get<Transform>(door,"movingLeaf"); Assert.That(Get<bool>(door,"IsOpen"),Is.False);
            // Listener sits on the same side of the actual authored door, outside
            // its capsule closure guard. Direct public Use isolates audio from aim.
            Vector3 near=leaf.position+door.transform.forward*2.4f-Get<Camera>(player,"eyes").transform.localPosition;
            PlacePlayer(near,false); ExternalMuteExcept();
            Component interaction=null;AudioSource source=null;AudioClip opening=null,closing=null;
            int rate=AudioSettings.outputSampleRate;Assert.That(AudioSettings.speakerMode,Is.EqualTo(AudioSpeakerMode.Stereo));
            var montage=new List<float>();var segments=new List<CloudExternalAudioTests.Segment>();
            float oldCaptureDelta=Time.captureDeltaTime;bool started=false;
            try
            {
                Time.captureDeltaTime=1f/60;yield return null;started=AudioRenderer.Start();Assert.That(started,Is.True);
                yield return ExternalFlush(rate);
                // The bounded engine clock is active before the genuine first Use.
                // Asset import/dressing startup must not consume the event before observation.
                Call(door,"Use",player);
                interaction=door.GetComponent(RequireType("InteractionAudio")); Assert.That(interaction,Is.Not.Null);
                source=Get<AudioSource>(interaction,"Source");opening=Get<AudioClip>(interaction,"LastClip");
                Assert.That(CloudExternalAudioTests.MatchesFamily(opening,"door-open",2),Is.True);
                CloudEnemyAudioTests.SourceContract(source);ExternalMuteExcept(source);
                Debug.Log("HAPPYTOY_EXTERNAL_DOOR_FIXTURE initial leaf="+leaf.localPosition+", open="+Get<bool>(door,"IsOpen")+
                    ", moving="+Get<bool>(interaction,"Moving")+", delta="+Time.deltaTime+", clip="+opening.name);
                yield return ExternalCapture("door-opening",source,opening,"door-open",.10f,rate,montage,segments);
                Assert.That(segments.Last().rms,Is.GreaterThan(.00002),"Actual door opening is silent at its listener");
                Assert.That(Get<bool>(interaction,"Moving"),Is.True,"Opening finished before its declared motion sample; leaf="+leaf.localPosition+
                    ", seat cues="+Get<int>(interaction,"SeatCuesPlayed")+", delta="+Time.deltaTime); Vector3 stopped=leaf.position,sourceStopped=source.transform.position;
                int cues=Get<int>(interaction,"CuesPlayed"), seats=Get<int>(interaction,"SeatCuesPlayed");
                Call(shell,"Pause");yield return ExternalFlush(rate);
                yield return ExternalCapture("door-paused",source,opening,"door-open",.25f,rate,montage,segments);
                Assert.That(segments.Last().peak,Is.LessThan(.00001),"Paused imported door cue leaks PCM");
                Assert.That(leaf.position,Is.EqualTo(stopped));Assert.That(source.transform.position,Is.EqualTo(sourceStopped));
                Assert.That(Get<int>(interaction,"CuesPlayed"),Is.EqualTo(cues));Assert.That(Get<int>(interaction,"SeatCuesPlayed"),Is.EqualTo(seats));
                Call(shell,"Resume");
                // No replay invocation: the same in-flight opening resumes.
                yield return ExternalCapture("door-resumed",source,opening,"door-open",.35f,rate,montage,segments);
                Assert.That(segments.Last().rms,Is.GreaterThan(.00002),"Same imported door cue never resumes");
                Assert.That(Vector3.Distance(leaf.position,stopped),Is.GreaterThan(.1f));
                Assert.That(Get<int>(interaction,"CuesPlayed"),Is.EqualTo(cues),"Resume retriggered the opening");
                yield return ExternalFlush(rate,.7f);
                Assert.That(Get<bool>(interaction,"Moving"),Is.False,"Real door never reached its open position");
                Assert.That(Get<int>(interaction,"SeatCuesPlayed"),Is.EqualTo(seats+1),"No single seating cue followed actual leaf completion");
                Assert.That(Vector3.Distance(source.transform.position,sourceStopped),Is.GreaterThan(.1f),"Spatial rail stayed at the old door location");

                PlacePlayer(near+door.transform.forward*(source.maxDistance+6),false);
                Call(door,"Use",player);closing=Get<AudioClip>(interaction,"LastClip");
                Assert.That(CloudExternalAudioTests.MatchesFamily(closing,"door-close",4),Is.True);
                yield return ExternalFlush(rate);
                yield return ExternalCapture("door-beyond-range",source,closing,"door-close",.35f,rate,montage,segments);
                Assert.That(segments.Last().peak,Is.LessThan(.00001),"Real door leaks beyond its finite audible range");
                yield return ExternalFlush(rate,.7f);
                PlacePlayer(near,false);Call(shell,"AdjustSettings",-1f,0f);Call(door,"Use",player);
                yield return ExternalFlush(rate);
                yield return ExternalCapture("door-master-zero",source,Get<AudioClip>(interaction,"LastClip"),"door-open",.35f,rate,montage,segments);
                Assert.That(AudioListener.volume,Is.Zero);Assert.That(segments.Last().peak,Is.LessThan(.00001),"Master mute leaks door PCM");
                Call(shell,"RestoreDefaultSettings");yield return ExternalFlush(rate,.7f);
                Call(door,"Use",player);closing=Get<AudioClip>(interaction,"LastClip");
                yield return ExternalCapture("door-closing-near",source,closing,"door-close",.35f,rate,montage,segments);
                Assert.That(segments.Last().rms,Is.GreaterThan(.00002),"Closing remains silent after unmute");
                Vector3 reversingAt=leaf.position;int beforeReversal=Get<int>(interaction,"CuesPlayed");
                Assert.That(Get<bool>(interaction,"Moving"),Is.True);
                Call(door,"Use",player);
                Assert.That(Get<int>(interaction,"CuesPlayed"),Is.EqualTo(beforeReversal+1));
                Assert.That(Get<bool>(interaction,"LastOpening"),Is.True);
                Assert.That(source.clip,Is.SameAs(opening),"Reversing retained the old closing voice");
                yield return ExternalCapture("door-reversed-in-flight",source,opening,"door-open",.2f,rate,montage,segments);
                Assert.That(segments.Last().rms,Is.GreaterThan(.00002));
                Assert.That(Vector3.Distance(leaf.position,reversingAt),Is.GreaterThan(.1f),"Door reversal did not change real leaf travel");
                CloudExperienceTests.Artifact("external-school-door.wav",CloudExperienceTests.Wave(montage,rate,2));
                CloudExperienceTests.Artifact("external-school-door.json",System.Text.Encoding.UTF8.GetBytes(JsonUtility.ToJson(
                    new CloudExternalAudioTests.Report {scope="Actual authored WASHROOM public door Use/leaf animation; near, frozen in-flight pause, same cue resume, finite distance, master mute. Positioned listener, not a continuous playthrough.",segments=segments.ToArray()},true)));
            }
            finally {if(started)AudioRenderer.Stop();Time.captureDeltaTime=oldCaptureDelta;}
            var previous=session;Call(shell,"Restart",true);
            yield return Wait(()=>Components("GameSession").Length==1 && One("GameSession")!=previous,25,"School retry never loaded fresh scene");
            yield return null;yield return null;
            Assert.That(door==null&&interaction==null&&source==null&&opening==null&&closing==null,Is.True,
                "School retry retains owned imported door clips/source");
            Assert.That(CloudExternalAudioTests.Shared("door-open"),Is.Not.Null,"Retry destroyed the shared imported asset");
            session=One("GameSession");player=Get<Component>(session,"player");shell=Get<Component>(session,"Shell");
            Debug.Log("HAPPYTOY_EXTERNAL_AUDIO_PASS real imported door PCM, leaf following/completion, in-flight pause/resume, distance/master mute and owned retry cleanup");
        }

        [UnityTest, Timeout(65000)]
        public IEnumerator ExternalPortraitCreakAndImpactFollowTheActualFourthMemoryEvent()
        {
            Call(shell,"BeginChapter");yield return null;Call(shell,"RestoreDefaultSettings");
            var chapter=Get<Component>(session,"Chapter");var memories=Get<Component[]>(chapter,"Memories");
            foreach(var memory in memories.Take(3))Call(memory,"Use",player);
            Assert.That(Get<int>(chapter,"Recovered"),Is.EqualTo(3));
            // Do not disable the portrait being tested: its production OnDisable
            // deliberately cancels the encounter. Chapter preparation already
            // disabled unrelated story triggers; isolate only released pursuers.
            foreach(var brain in Components("StalkerBrain"))brain.gameObject.SetActive(false);
            Get<Component>(chapter,"Mannequin").gameObject.SetActive(false);
            Get<Component>(chapter,"Mask").gameObject.SetActive(false);
            var portrait=Get<Component>(chapter,"Portrait");
            Assert.That(Get<bool>(portrait,"Cancelled"),Is.False,"Portrait fixture cancelled its own production encounter");
            ((Behaviour)player).enabled=false;((Behaviour)Get<Component>(player,"Feedback")).enabled=false;
            PlacePlayer(new Vector3(31.3f,5.02f,32.5f),false);
            var camera=Get<Camera>(player,"eyes");camera.transform.rotation=Quaternion.LookRotation(Vector3.forward);
            var voice=portrait.GetComponent(RequireType("EncounterRevealAudio"));Assert.That(voice,Is.Not.Null);
            var source=Get<AudioSource>(voice,"Source");ExternalMuteExcept(source);
            int rate=AudioSettings.outputSampleRate;var montage=new List<float>();var segments=new List<CloudExternalAudioTests.Segment>();
            float oldCaptureDelta=Time.captureDeltaTime;bool started=false;
            try
            {
                Time.captureDeltaTime=1f/60;yield return null;started=AudioRenderer.Start();Assert.That(started,Is.True);
                yield return ExternalFlush(rate);
                Call(memories[3],"Use",player);
                Assert.That(Get<bool>(portrait,"Triggered"),Is.True,"Actual fourth memory never triggered prepared portrait; cancelled="+
                    Get<bool>(portrait,"Cancelled")+", phase="+Get<string>(portrait,"Phase")+", input="+Get<bool>(session,"InputAllowed"));
                Assert.That(Get<int>(chapter,"Recovered"),Is.EqualTo(3));
                Assert.That(Get<object>(voice,"LastCue").ToString(),Is.EqualTo("FrameStrain"));
                var strain=Get<AudioClip>(voice,"ActiveClip");Assert.That(CloudExternalAudioTests.MatchesFamily(strain,"frame-strain",3),Is.True);
                yield return ExternalCapture("fourth-memory-frame-strain",source,strain,"frame-strain",.25f,rate,montage,segments);
                Assert.That(segments.Last().rms,Is.GreaterThan(.00002),"Actual fourth-memory creak is inaudible");
                float elapsed=Get<float>(portrait,"RevealElapsed");int cues=Get<int>(voice,"CuesPlayed");
                Call(shell,"Pause");yield return ExternalFlush(rate);
                yield return ExternalCapture("fourth-memory-paused",source,strain,"frame-strain",.25f,rate,montage,segments);
                Assert.That(segments.Last().peak,Is.LessThan(.00001));Assert.That(Get<float>(portrait,"RevealElapsed"),Is.EqualTo(elapsed));
                Assert.That(Get<int>(voice,"CuesPlayed"),Is.EqualTo(cues));Call(shell,"Resume");
                // Render the real mixer continuously while waiting for the real
                // animation's impact, rather than advancing a separate fake clock.
                while(Get<int>(portrait,"FrameImpactCues")==0)
                {
                    Assert.That(Get<float>(portrait,"RevealElapsed"),Is.LessThan(3),"Portrait failed to emit the fall contact");
                    yield return ExternalFlush(rate,.05f);
                }
                Assert.That(Get<int>(portrait,"FrameStrainCues"),Is.EqualTo(1));Assert.That(Get<int>(portrait,"FrameImpactCues"),Is.EqualTo(1));
                Assert.That(Get<object>(voice,"LastCue").ToString(),Is.EqualTo("FrameImpact"));
                var impact=Get<AudioClip>(voice,"ActiveClip");Assert.That(CloudExternalAudioTests.MatchesFamily(impact,"frame-impact",3),Is.True);
                Assert.That(CloudExternalAudioTests.Fingerprint(impact),Is.Not.EqualTo(CloudExternalAudioTests.Fingerprint(strain)));
                yield return ExternalCapture("fourth-memory-frame-impact",source,impact,"frame-impact",.22f,rate,montage,segments);
                Assert.That(segments.Last().rms,Is.GreaterThan(.00002),"Actual fourth-memory frame landing is inaudible");
                Call(session,"Finish",false);yield return null;
                Assert.That(Get<AudioClip>(voice,"ActiveClip"),Is.Null);Assert.That(source.isPlaying,Is.False,"Result screen leaves the imported event voice playing");
                CloudExperienceTests.Artifact("external-school-portrait.wav",CloudExperienceTests.Wave(montage,rate,2));
                CloudExperienceTests.Artifact("external-school-portrait.json",System.Text.Encoding.UTF8.GetBytes(JsonUtility.ToJson(
                    new CloudExternalAudioTests.Report {scope="Actual fourth-memory public Use and authored portrait drop; imported creak/impact listener output, frozen event clock and result cancellation. Positioned listener, not a survival route.",segments=segments.ToArray()},true)));
            }
            finally{if(started)AudioRenderer.Stop();Time.captureDeltaTime=oldCaptureDelta;}
            Debug.Log("HAPPYTOY_EXTERNAL_AUDIO_PASS fourth-memory real portrait creak/fall contact, imported non-silent PCM, one cue per beat, pause and result cancellation");
        }

        [UnityTest, Timeout(90000)]
        public IEnumerator ExternalChapterRoomLoopsAreAudibleAndOccludedAcrossTheAuthoredFloor()
        {
            Call(shell,"BeginChapter");yield return null;IsolateThreats();Call(shell,"RestoreDefaultSettings");
            ((Behaviour)player).enabled=false;((Behaviour)Get<Component>(player,"Feedback")).enabled=false;
            var ambience=One("RoomAmbience");var voices=((IEnumerable)Get<object>(ambience,"Voices")).Cast<object>().ToArray();
            Assert.That(voices.Length,Is.EqualTo(3));
            var sources=voices.Select(voice=>Get<AudioSource>(voice,"source")).ToArray();
            var cues=new[]{"ambience-ground","ambience-upper","ambience-basement-bed"};
            var positions=new[]{new Vector3(-3,.02f,-3.3f),new Vector3(29.8f,5.02f,30),new Vector3(13.8f,-4.98f,-28)};
            for(int i=0;i<3;i++)
            {
                Assert.That(sources[i].loop,Is.True);Assert.That(sources[i].ignoreListenerPause||sources[i].ignoreListenerVolume,Is.False);
                Assert.That(CloudExternalAudioTests.MatchesFamily(sources[i].clip,cues[i],1),Is.True,"Wrong recorded floor ambience: "+i);
            }
            int rate=AudioSettings.outputSampleRate;Assert.That(AudioSettings.speakerMode,Is.EqualTo(AudioSpeakerMode.Stereo));
            var montage=new List<float>();var segments=new List<CloudExternalAudioTests.Segment>();
            float oldCaptureDelta=Time.captureDeltaTime;bool started=false;
            try
            {
                Time.captureDeltaTime=1f/60;yield return null;started=AudioRenderer.Start();Assert.That(started,Is.True);
                for(int i=0;i<3;i++)
                {
                    ExternalMuteExcept(sources[i]);sources[i].Play();PlacePlayer(positions[i],false);yield return ExternalFlush(rate,1.4f);
                    yield return ExternalCapture("near-"+cues[i],sources[i],sources[i].clip,cues[i],.7f,rate,montage,segments);
                    Assert.That(segments.Last().rms,Is.GreaterThan(.00002),"Recorded room ambience is inaudible at its actual floor: "+cues[i]);
                    segments.Last().occluded=Get<bool>(voices[i],"occluded");
                }
                var upper=sources[1];ExternalMuteExcept(upper);upper.Play();
                PlacePlayer(positions[1],false);yield return ExternalFlush(rate,1.4f);
                yield return ExternalCapture("upper-clear",upper,upper.clip,cues[1],.7f,rate,montage,segments);
                var clear=segments.Last();Assert.That(Get<bool>(voices[1],"occluded"),Is.False,"Declared near upper room fixture is obstructed");
                // Same X/Z, below the actual Floor 2F slab. This is a positioned
                // listener fixture; no replacement wall supplies separation.
                PlacePlayer(positions[1]-Vector3.up*5,false);yield return ExternalFlush(rate,1.4f);
                yield return ExternalCapture("upper-through-school-floor",upper,upper.clip,cues[1],.7f,rate,montage,segments);
                segments.Last().occluded=Get<bool>(voices[1],"occluded");
                Assert.That(segments.Last().occluded,Is.True,"Actual school floor did not block the upper-room loop");
                Assert.That(Get<AudioLowPassFilter>(voices[1],"filter").cutoffFrequency,Is.LessThan(1200));
                Assert.That(segments.Last().rms,Is.LessThan(clear.rms*.65),"Real school floor failed to separate the recorded ambience");
                Call(shell,"Pause");yield return ExternalFlush(rate);
                yield return ExternalCapture("ambience-paused",upper,upper.clip,cues[1],.3f,rate,montage,segments);
                Assert.That(segments.Last().peak,Is.LessThan(.00001),"Paused recorded ambience leaks PCM");Call(shell,"Resume");
                CloudExperienceTests.Artifact("external-school-ambience.wav",CloudExperienceTests.Wave(montage,rate,2));
                CloudExperienceTests.Artifact("external-school-ambience.json",System.Text.Encoding.UTF8.GetBytes(JsonUtility.ToJson(
                    new CloudExternalAudioTests.Report {scope="Three actual chapter room loop sources and positioned listener; actual Floor 2F collider/low-pass separation and listener pause. Isolated main-output excerpts, not device listening certification.",segments=segments.ToArray()},true)));
            }
            finally{if(started)AudioRenderer.Stop();Time.captureDeltaTime=oldCaptureDelta;}
            Debug.Log("HAPPYTOY_EXTERNAL_AUDIO_PASS imported loops at real ground/upper/basement positions, real upper slab attenuation and listener pause");
        }
    }
}
