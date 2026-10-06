using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;
using static HappyToy.V2.CloudTests.RuntimeAccess;

namespace HappyToy.V2.CloudTests
{
    // Run after production grounding. Bone heights are timing proxies; they are
    // not a claim that a joint origin is the skin's contact point.
    [DefaultExecutionOrder(10000)]
    public sealed class CloudRefinedEnemyFoleyProbe : MonoBehaviour
    {
        internal Component motion, steps;
        internal Animation animation;
        internal NavMeshAgent agent;
        internal SkinnedMeshRenderer[] skins;
        internal Transform leftToe, rightToe, leftHand, rightHand;
        internal string clip;
        internal Action<RefinedFoleyFrame> receive;
        internal RefinedContactSkin[] contactGroups;
        byte[][] supportMasks,majorityMasks;
        Mesh mesh;
        void Awake() { mesh = new Mesh(); }
        internal void CacheContactVertices()
        {
            supportMasks=new byte[skins.Length][];majorityMasks=new byte[skins.Length][];
            contactGroups=new RefinedContactSkin[skins.Length];
            for(int index=0;index<skins.Length;index++)
            {
                var skin=skins[index];var source=skin.sharedMesh;
                var counts=source.GetBonesPerVertex();var weights=source.GetAllBoneWeights();
                if(counts.Length!=source.vertexCount||weights.Length==0)
                    throw new InvalidOperationException("Cannot independently inspect actual imported skin weights: "+source.name);
                var boneGroups=skin.bones.Select(bone=>ContactBoneGroup(bone.name)).ToArray();
                var support=new byte[source.vertexCount];var majority=new byte[source.vertexCount];int cursor=0;
                var supportCounts=new int[4];var majorityCounts=new int[4];
                for(int vertex=0;vertex<source.vertexCount;vertex++)
                {
                    float leftFoot=0,rightFoot=0,leftHand=0,rightHand=0;
                    for(int influence=0;influence<counts[vertex];influence++)
                    {
                        var weight=weights[cursor++];
                        if(weight.boneIndex<0||weight.boneIndex>=boneGroups.Length)throw new InvalidOperationException("Invalid imported bone index");
                        switch(boneGroups[weight.boneIndex])
                        {case 1:leftFoot+=weight.weight;break;case 2:rightFoot+=weight.weight;break;
                            case 4:leftHand+=weight.weight;break;case 8:rightHand+=weight.weight;break;}
                    }
                    var grouped=new[]{leftFoot,rightFoot,leftHand,rightHand};
                    for(int group=0;group<4;group++)
                    {
                        if(grouped[group]>.01f){support[vertex]|=(byte)(1<<group);supportCounts[group]++;}
                        if(grouped[group]>=.5f){majority[vertex]|=(byte)(1<<group);majorityCounts[group]++;}
                    }
                }
                if(cursor!=weights.Length)throw new InvalidOperationException("Actual bone weight stream did not match vertex order");
                supportMasks[index]=support;majorityMasks[index]=majority;
                var names=new[]{"leftFootToe","rightFootToe","leftHandFingers","rightHandFingers"};
                contactGroups[index]=new RefinedContactSkin {mesh=source.name,vertices=source.vertexCount,
                    groups=Enumerable.Range(0,4).Select(group=>new RefinedContactGroup {name=names[group],
                        supportVertices=supportCounts[group],majorityVertices=majorityCounts[group],
                        bones=skin.bones.Where((bone,boneIndex)=>boneGroups[boneIndex]==(1<<group)).Select(bone=>bone.name).ToArray()}).ToArray()};
                // Unity's weight/count NativeArrays reference the Mesh-owned stream.
                // Cache managed masks only; never modify/dispose the borrowed stream.
            }
            if(contactGroups.Any(skin=>skin.groups.Any(group=>group.majorityVertices==0)))
                throw new InvalidOperationException("Missing majority-weighted anatomical contact vertices");
        }
        static byte ContactBoneGroup(string name)
        {
            if(name.StartsWith("mixamorig:LeftFoot",StringComparison.Ordinal)||name.StartsWith("mixamorig:LeftToe",StringComparison.Ordinal))return 1;
            if(name.StartsWith("mixamorig:RightFoot",StringComparison.Ordinal)||name.StartsWith("mixamorig:RightToe",StringComparison.Ordinal))return 2;
            if(name.StartsWith("mixamorig:LeftHand",StringComparison.Ordinal))return 4;
            if(name.StartsWith("mixamorig:RightHand",StringComparison.Ordinal))return 8;
            return 0;
        }
        void LateUpdate()
        {
            if (receive == null || !animation || !animation[clip]) return;
            float sole = float.MaxValue;
            var support=new[]{float.MaxValue,float.MaxValue,float.MaxValue,float.MaxValue};
            var majority=new[]{float.MaxValue,float.MaxValue,float.MaxValue,float.MaxValue};
            for(int index=0;index<skins.Length;index++)
            {
                var skin=skins[index];
                skin.BakeMesh(mesh,true);
                var vertices=mesh.vertices;
                if(vertices.Length!=supportMasks[index].Length)throw new InvalidOperationException("Baked contact indices changed");
                for(int vertex=0;vertex<vertices.Length;vertex++)
                {
                    float y=skin.transform.TransformPoint(vertices[vertex]).y;sole=Mathf.Min(sole,y);
                    for(int group=0;group<4;group++)
                    {
                        if((supportMasks[index][vertex]&(1<<group))!=0)support[group]=Mathf.Min(support[group],y);
                        if((majorityMasks[index][vertex]&(1<<group))!=0)majority[group]=Mathf.Min(majority[group],y);
                    }
                }
            }
            if (!Physics.Raycast(transform.position+Vector3.up*.25f,Vector3.down,out var floor,.8f,
                Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore))
                throw new InvalidOperationException("No physical floor under live foley probe");
            var state = animation[clip];
            receive(new RefinedFoleyFrame {
                frame=Time.frameCount,time=Time.time,dspTime=AudioSettings.dspTime,clipTime=state.time,
                phase=Mathf.Repeat(state.time/state.length,1),speed=agent.velocity.magnitude,
                position=transform.position,steps=Get<int>(steps,"StepsPlayed"),
                cue=Get<AudioClip>(steps,"MovementClip").name,surface=Get<string>(steps,"LastSurface"),
                renderedSoleY=sole,floorY=floor.point.y,renderedGap=sole-floor.point.y,
                leftToeY=leftToe.position.y,rightToeY=rightToe.position.y,
                leftHandY=leftHand.position.y,rightHandY=rightHand.position.y,
                supportLeftFootY=support[0],supportRightFootY=support[1],supportLeftHandY=support[2],supportRightHandY=support[3],
                weightedLeftFootY=majority[0],weightedRightFootY=majority[1],weightedLeftHandY=majority[2],weightedRightHandY=majority[3],
                agentStopped=agent.isStopped,hasPath=agent.hasPath
            });
        }
        void OnDestroy() { if(mesh)Destroy(mesh); }
    }

    [Serializable] internal sealed class RefinedFoleyFrame
    {
        public int frame,steps;
        public float time,clipTime,phase,speed,renderedSoleY,floorY,renderedGap,leftToeY,rightToeY,leftHandY,rightHandY;
        public float supportLeftFootY,supportRightFootY,supportLeftHandY,supportRightHandY;
        public float weightedLeftFootY,weightedRightFootY,weightedLeftHandY,weightedRightHandY;
        public double dspTime;
        public string cue,surface;
        public Vector3 position;
        public bool agentStopped,hasPath;
    }
    [Serializable] internal sealed class RefinedContactGroup
    {public string name;public string[] bones;public int supportVertices,majorityVertices;}
    [Serializable] internal sealed class RefinedContactSkin
    {public string mesh;public int vertices;public RefinedContactGroup[] groups;}
    [Serializable] internal sealed class RefinedFoleyTrace
    {
        public string key,clip;
        public string scope="Natural-clock four-second production clip and real NavMesh travel; irregular observed late-frame joint/skin samples versus actual cue count. Retain at most 125Hz plus every cue transition; rendered frames target 8Hz without clock forcing. Joint heights are timing proxies; no synchronization, DSP performance or human fear certification.";
        public float seconds,realtimeSeconds,clipLength,captureDeltaTime,timeScale,targetCaptureHz=8,maximumRetainedHz=125;
        public Vector3 start,end;
        public int initialSteps,finalSteps,renderedFrames,nativeFramesObserved;
        public int tileWidth=320,tileHeight=180,stripColumns=4,stripRows=4;
        public string tileOrder="Left-to-right, rows bottom-to-top in Unity Texture2D; PNG rows top-to-bottom";
        public string[] images;
        public int[] imageFrameNumbers;
        public int[] stripTileCounts;
        public float[] imageTimes;
        public RefinedContactSkin[] weightedContactGroups;
        public string vertexGroupRule="Imported BoneWeight1 stream, exact current skin.bones indices; summed Foot+Toe / Hand+finger weights >.01 support, >=.5 majority; cached once and reused on same BakeMesh(true) vertices. Groups overlap only through real weights.";
        public RefinedFoleyFrame[] frames;
    }

    public sealed partial class CloudPlayModeTests
    {
        [UnityTest,Timeout(120000)]
        public IEnumerator RefinedFourEnemiesRecordActualNavFoleyAndLimbPhaseWithoutChangingCues()
        {
            IsolateThreats();Call(shell,"BeginChapter");yield return null;yield return null;
            ((Behaviour)player).enabled=false;
            var camera=Get<Camera>(player,"eyes");Assert.That(camera,Is.SameAs(Camera.main));
            Get<Light>(player,"flashlight").enabled=true;
            var actors=Components("V1MonsterMotion").ToArray();
            float captureDelta=Time.captureDeltaTime;
            try
            {
                Time.captureDeltaTime=0;
                Assert.That(Time.timeScale,Is.EqualTo(1));
                foreach(string key in new[]{"Cyclopse","Uncat","Hwacat_angry","Baby"})
                {
                    var motion=actors.Single(item=>item.name.Contains(key));
                    var actor=motion.GetComponent(RequireType("StalkerBrain"));
                    ((Behaviour)actor).enabled=false;
                    foreach(var startup in actor.GetComponents(RequireType("NavMeshStartup")))
                    {((MonoBehaviour)startup).StopAllCoroutines();((Behaviour)startup).enabled=false;}
                    var agent=actor.GetComponent<NavMeshAgent>();agent.enabled=false;
                    actor.gameObject.SetActive(true);((Behaviour)motion).enabled=true;
                    var model=Get<Transform>(motion,"model");var animation=Get<Animation>(motion,"animationPlayer");
                    var steps=actor.GetComponent(RequireType("StalkerFootsteps"));
                    Assert.That(Get<bool>(motion,"RefinedVisual"),Is.True);
                    var allBones=model.GetComponentsInChildren<Transform>();
                    var probe=actor.gameObject.AddComponent<CloudRefinedEnemyFoleyProbe>();
                    probe.motion=motion;probe.steps=steps;probe.animation=animation;probe.agent=agent;
                    probe.skins=model.GetComponentsInChildren<SkinnedMeshRenderer>();
                    probe.leftToe=allBones.Single(bone=>bone.name=="mixamorig:LeftToeBase");
                    probe.rightToe=allBones.Single(bone=>bone.name=="mixamorig:RightToeBase");
                    probe.leftHand=allBones.Single(bone=>bone.name=="mixamorig:LeftHand");
                    probe.rightHand=allBones.Single(bone=>bone.name=="mixamorig:RightHand");
                    probe.CacheContactVertices();
                    foreach(string clip in new[]{"patrol","chase"})
                    {
                        agent.enabled=false;
                        Assert.That(NavMesh.SamplePosition(new Vector3(-7.3f,0,0),out var begin,1,NavMesh.AllAreas),Is.True);
                        Assert.That(NavMesh.SamplePosition(new Vector3(2,0,0),out var end,.2f,NavMesh.AllAreas),Is.True);
                        actor.transform.position=begin.position;agent.enabled=true;
                        Assert.That(agent.Warp(begin.position),Is.True);agent.isStopped=true;
                        var route=new NavMeshPath();Assert.That(agent.CalculatePath(end.position,route),Is.True);
                        Assert.That(route.status,Is.EqualTo(NavMeshPathStatus.PathComplete));
                        Assert.That(Vector3.Distance(route.corners.Last(),end.position),Is.LessThan(.15f));
                        Set(actor,"state",clip=="chase"?"Chase":"Patrol");
                        animation.Play(clip);animation[clip].time=0;animation.Sample();
                        agent.speed=Get<float>(actor,clip=="chase"?"chaseSpeed":"patrolSpeed");
                        agent.stoppingDistance=.05f;agent.isStopped=false;
                        Assert.That(agent.SetDestination(end.position),Is.True);
                        yield return Wait(()=>agent.hasPath&&!agent.pathPending,3,"Actual foley probe obtained no path");
                        var frames=new List<RefinedFoleyFrame>();var images=new List<string>();var imageFrames=new List<int>();
                        var imageTimes=new List<float>();var stripCounts=new List<int>();
                        int firstSteps=Get<int>(steps,"StepsPlayed");
                        float started=Time.time,realStarted=Time.realtimeSinceStartup,nextImage=0,lastRetained=-1;
                        int renderedFrames=0,nativeFramesObserved=0,lastSteps=firstSteps;
                        var strip=new Texture2D(320*4,180*4,TextureFormat.RGB24,false);
                        probe.clip=clip;
                        probe.receive=frame=>
                        {
                            nativeFramesObserved++;
                            if(frame.steps!=lastSteps||frame.time-lastRetained>=1f/125)
                            {frames.Add(frame);lastRetained=frame.time;lastSteps=frame.steps;}
                            if(renderedFrames>=32||frame.time-started+.0001f<nextImage)return;
                            nextImage=frame.time-started+1f/8;
                            var body=RefinedEnemyBodyBounds(model);
                            camera.transform.position=actor.transform.position+new Vector3(2,Mathf.Max(.7f,body.size.y*.7f),1.2f);
                            camera.transform.LookAt(body.center);
                            RefinedFoleyCaptureTile(camera,strip,renderedFrames%16);imageFrames.Add(frame.frame);imageTimes.Add(frame.time);renderedFrames++;
                            if(renderedFrames%16!=0)return;
                            string file="foley-nav-"+key.ToLowerInvariant()+"-"+clip+"-strip-"+(images.Count+1).ToString("000")+".png";
                            strip.Apply();CloudExperienceTests.Artifact(file,strip.EncodeToPNG());images.Add(file);stripCounts.Add(16);
                        };
                        bool returning=false;
                        while(Time.time-started<4)
                        {
                            if(!agent.pathPending&&agent.remainingDistance<.18f)
                            {
                                returning=!returning;
                                Assert.That(agent.SetDestination(returning?begin.position:end.position),Is.True);
                            }
                            yield return null;
                        }
                        probe.receive=null;agent.isStopped=true;agent.ResetPath();
                        if(renderedFrames%16!=0)
                        {
                            var blank=new Color[320*180];
                            for(int tile=renderedFrames%16;tile<16;tile++)
                                strip.SetPixels((tile%4)*320,(tile/4)*180,320,180,blank);
                            string file="foley-nav-"+key.ToLowerInvariant()+"-"+clip+"-strip-"+(images.Count+1).ToString("000")+".png";
                            strip.Apply();CloudExperienceTests.Artifact(file,strip.EncodeToPNG());images.Add(file);stripCounts.Add(renderedFrames%16);
                        }
                        UnityEngine.Object.Destroy(strip);
                        int finalSteps=Get<int>(steps,"StepsPlayed");
                        Assert.That(frames.Count,Is.GreaterThanOrEqualTo(24),"Insufficient observed natural-frame timing samples");
                        Assert.That(renderedFrames,Is.InRange(8,32),"Insufficient observed rendered frames");
                        Assert.That(images.Count,Is.InRange(1,2));
                        foreach(var file in images)
                            Assert.That(File.Exists(Path.GetFullPath(Path.Combine("Temp","HappyToyCloudEvidence",file))),Is.True);
                        CloudExperienceTests.Artifact("foley-nav-"+key.ToLowerInvariant()+"-"+clip+".json",Encoding.UTF8.GetBytes(JsonUtility.ToJson(
                            new RefinedFoleyTrace{key=key,clip=clip,seconds=Time.time-started,realtimeSeconds=Time.realtimeSinceStartup-realStarted,
                                clipLength=animation[clip].length,captureDeltaTime=Time.captureDeltaTime,timeScale=Time.timeScale,
                                start=begin.position,end=end.position,initialSteps=firstSteps,finalSteps=finalSteps,
                                frames=frames.ToArray(),images=images.ToArray(),imageFrameNumbers=imageFrames.ToArray(),imageTimes=imageTimes.ToArray(),
                                stripTileCounts=stripCounts.ToArray(),renderedFrames=renderedFrames,nativeFramesObserved=nativeFramesObserved,
                                weightedContactGroups=probe.contactGroups},true)));
                        Assert.That(finalSteps,Is.GreaterThan(firstSteps+1),"No production recorded cues during real navigation");
                        Assert.That(frames.Count(frame=>frame.speed>.1f),Is.GreaterThan(frames.Count/2));
                        Assert.That(frames.Max(frame=>Mathf.Abs(frame.renderedGap)),Is.LessThan(.045f),"Live travel leaves the physical floor");
                        yield return null;
                    }
                    UnityEngine.Object.Destroy(probe);actor.gameObject.SetActive(false);
                }
            }
            finally {Time.captureDeltaTime=captureDelta;}
            Debug.Log("HAPPYTOY_REFINED_FOLEY_DIAGNOSTIC_PASS: eight four-second actual NavMesh/unchanged authored-clip traces and native-frame cue/joint/skin evidence; contact synchronization is measured separately");
        }
        static void RefinedFoleyCaptureTile(Camera camera,Texture2D strip,int tile)
        {
            var target=RenderTexture.GetTemporary(320,180,24);
            var texture=new Texture2D(320,180,TextureFormat.RGB24,false);
            var previousTarget=camera.targetTexture;var previousActive=RenderTexture.active;
            try
            {
                camera.targetTexture=target;camera.Render();RenderTexture.active=target;
                texture.ReadPixels(new Rect(0,0,320,180),0,0);texture.Apply();
                // Preserve the actual rendered pixels. A bounded frame strip carries
                // sixteen frames in one immutable artifact, inside the original
                // file-count/byte limits. No source texture or model is edited.
                strip.SetPixels((tile%4)*320,(tile/4)*180,320,180,texture.GetPixels());
            }
            finally
            {camera.targetTexture=previousTarget;RenderTexture.active=previousActive;RenderTexture.ReleaseTemporary(target);UnityEngine.Object.Destroy(texture);}
        }
    }
}
