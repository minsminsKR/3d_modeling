using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;
using static HappyToy.V2.CloudTests.RuntimeAccess;
using Object=UnityEngine.Object;

namespace HappyToy.V2.CloudTests
{
    public sealed partial class CloudPlayModeTests
    {
        [Test]
        public void StudioWraithKeepsNavigationAndImportsEighteenSkinnedArms()
        {
            var actor=One("LanternMaskEncounter");var art=actor.GetComponent(RequireType("MaskHorrorVisual"));
            Assert.That(Get<bool>(art,"Prepared"),Is.True);
            var model=Get<Transform>(art,"BodyModel");
            Assert.That(model.parent.name,Is.EqualTo("Hallway visual fit"));
            Assert.That(model.parent.parent,Is.EqualTo(Get<Transform>(actor,"body")));
            Assert.That(model.GetComponentsInChildren<Transform>(true).Count(t=>t.name.EndsWith("_Wrist")),Is.EqualTo(18));
            var skins=model.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            var source=Resources.Load<GameObject>("Baeksugwi/Baeksugwi").GetComponentInChildren<SkinnedMeshRenderer>(true);
            var sourceMesh=source.sharedMesh;
            Assert.That(sourceMesh.vertexCount,Is.GreaterThan(900000),"The retained original/title mesh was replaced by a game LOD");
            var profile=Resources.Load("Baeksugwi/GameLod",RequireType("BaeksugwiGameLod"));
            Assert.That(profile,Is.Not.Null);Call(profile,"Validate");
            var levels=Get<System.Array>(profile,"levels");var lods=model.GetComponent<LODGroup>().GetLODs();
            Assert.That(skins.Length,Is.EqualTo(1));Assert.That(lods.Length,Is.EqualTo(1));
            for(int level=0;level<levels.Length;level++)
            {
                var data=levels.GetValue(level);var skin=(SkinnedMeshRenderer)lods[level].renderers.Single();
                Assert.That(skin.sharedMesh,Is.SameAs(Get<Mesh>(data,"mesh")));
                Assert.That(skin.sharedMesh,Is.Not.SameAs(sourceMesh));
                Assert.That(skin.sharedMesh.triangles.Length/3,Is.LessThanOrEqualTo(400000));
                Assert.That(skin.bones.Length,Is.EqualTo(63));Assert.That(skin.bones.Count(b=>b.name=="Head"),Is.EqualTo(1));
                Assert.That(skin.updateWhenOffscreen,Is.False);
                Assert.That(skin.sharedMaterials,Is.EqualTo(source.sharedMaterials),"LOD changed the original PBR surface");
                var names=Get<string[]>(data,"boneNames");var bind=skin.sharedMesh.bindposes;
                for(int bone=0;bone<names.Length;bone++)
                {
                    Assert.That(skin.bones[bone].name,Is.EqualTo(names[bone]));
                    int original=System.Array.FindIndex(source.bones,b=>b.name==names[bone]);Assert.That(original,Is.GreaterThanOrEqualTo(0));
                    for(int element=0;element<16;element++)Assert.That(bind[bone][element],Is.EqualTo(sourceMesh.bindposes[original][element]).Within(.0001f));
                }
            }
            var title=GameObject.Find("Actual Baeksugwi source clone");Assert.That(title,Is.Not.Null);
            Assert.That(title.GetComponentInChildren<SkinnedMeshRenderer>(true).sharedMesh,Is.SameAs(sourceMesh));
            Assert.That(Get<AnimationClip>(art,"HeadMotionClip"),Is.SameAs(Resources.Load<AnimationClip>("Baeksugwi/Head_Writhe")));
            Assert.That(model.GetComponentsInChildren<Collider>(true),Is.Empty);
            Assert.That(model.GetComponentsInChildren<Rigidbody>(true),Is.Empty);
            var agent=actor.GetComponent<NavMeshAgent>();
            Assert.That(agent.radius,Is.EqualTo(.3f).Within(.001f));
            Assert.That(agent.height,Is.EqualTo(2.1f).Within(.001f));
            Assert.That(actor.transform.localScale,Is.EqualTo(Vector3.one));
            Assert.That(Get<AnimationClip>(art,"RunClip").length,Is.InRange(.70f,.74f));
        }
        [Test]
        public void StudioImportedRunKeepsFloorRoofAndForwardFacingHead()
        {
            var instance=Object.Instantiate(Resources.Load<GameObject>("Baeksugwi/Baeksugwi"));
            try
            {
                var clip=Resources.Load<AnimationClip>("Baeksugwi/Run_InPlace");
                var skin=instance.GetComponentInChildren<SkinnedMeshRenderer>();
                var mesh=new Mesh();
                try
                {
                    for(int sample=0;sample<4;sample++)
                    {
                        clip.SampleAnimation(instance,sample*clip.length/4);
                        skin.BakeMesh(mesh,false);
                        var inherited=skin.transform.parent.lossyScale;
                        var points=mesh.vertices.Select(p=>skin.transform.TransformPoint(new Vector3(p.x/inherited.x,p.y/inherited.y,p.z/inherited.z))).ToArray();
                        Assert.That(points.Min(p=>p.y),Is.GreaterThan(-.008f),"Imported run penetrates the floor");
                        Assert.That(points.Max(p=>p.y),Is.LessThan(2.44f),"Imported creature penetrates the corridor roof");
                    }
                    var nodes=instance.GetComponentsInChildren<Transform>(true);
                    var head=nodes.Single(t=>t.name=="Head");var tail=nodes.Single(t=>t.name=="Tail");
                    Assert.That(Vector3.Dot(head.position-tail.position,Vector3.forward),Is.GreaterThan(2));
                }
                finally{Object.DestroyImmediate(mesh);}
            }
            finally{Object.DestroyImmediate(instance);}
        }
        [UnityTest,Timeout(60000)]
        public IEnumerator StudioRunMovesWithTheAgentAndFreezesOnPause()
        {
            yield return PrepareCorridorMaskRunner();
            var actor=Get<Component>(Get<Component>(session,"Corridor"),"Mask");
            var art=actor.GetComponent(RequireType("MaskHorrorVisual"));
            var model=Get<Transform>(art,"BodyModel");
            var elbows=model.GetComponentsInChildren<Transform>(true).Where(t=>t.name.EndsWith("_Elbow")).ToArray();
            ((Behaviour)player).enabled=false;PlacePlayer(new Vector3(200,5,200),false);
            var start=actor.transform.position;var first=elbows.Select(t=>t.localRotation).ToArray();
            yield return Wait(()=>Vector3.Distance(start,actor.transform.position)>.1f&&Get<float>(art,"GaitPhase")>.1f,5,"Agent did not drive the imported sprint");
            Assert.That(elbows.Where((t,i)=>Quaternion.Angle(t.localRotation,first[i])>.05f).Count(),Is.GreaterThanOrEqualTo(6));
            var rotations=elbows.Select(t=>t.localRotation).ToArray();float phase=Get<float>(art,"GaitPhase");
            Call(shell,"Pause");yield return Delay(.25f);
            Assert.That(Get<float>(art,"GaitPhase"),Is.EqualTo(phase));
            for(int i=0;i<elbows.Length;i++)Assert.That(Quaternion.Angle(rotations[i],elbows[i].localRotation),Is.LessThan(.001f));
        }
        [UnityTest,Timeout(60000)]
        public IEnumerator StudioCorridorCloneUsesOneReplacementAndHidesTheFloatingFace()
        {
            yield return PrepareCorridorMaskRunner();
            var actor=Get<Component>(Get<Component>(session,"Corridor"),"Mask");
            PlacePlayer(new Vector3(200,5,200),false);
            yield return null;
            var model=Get<Transform>(actor.GetComponent(RequireType("MaskHorrorVisual")),"BodyModel");
            Assert.That(Get<Transform>(actor,"body").GetComponentsInChildren<LODGroup>(true).Count(group=>group.enabled),Is.EqualTo(1));
            var lods=model.GetComponent<LODGroup>().GetLODs();
            Assert.That(lods.SelectMany(l=>l.renderers).Distinct().Count(),Is.EqualTo(1),"Game LOD duplicated the whole body clone");
            Assert.That(lods.All(l=>l.renderers.Length==1 && l.renderers[0].enabled),Is.True);
            Assert.That(Get<Transform>(actor,"mask").GetComponentsInChildren<Renderer>(true).All(r=>!r.enabled),Is.True);
            var body=Get<Transform>(actor,"body");var bodyScale=body.localScale;bool bodyActive=body.gameObject.activeSelf;
            Assert.That(body.gameObject.activeInHierarchy,Is.True,"The genuine third-memory release did not show its body");
            var navigation=actor.GetComponent<NavMeshAgent>();
            bool actorEnabled=((Behaviour)actor).enabled,navigationEnabled=navigation.enabled;
            ((Behaviour)actor).enabled=false;actor.GetComponent<NavMeshAgent>().enabled=false;
            var art=actor.GetComponent(RequireType("MaskHorrorVisual"));bool artEnabled=((Behaviour)art).enabled;((Behaviour)art).enabled=false;
            // Encounter.OnDisable intentionally hides its body and restores the
            // reveal defaults. Freeze the already-released visual explicitly;
            // otherwise this fixture captures only its black clear background.
            body.localScale=bodyScale;body.gameObject.SetActive(bodyActive);
            Get<AnimationClip>(art,"RunClip").SampleAnimation(model.gameObject,0);
            Get<AnimationClip>(art,"HeadMotionClip").SampleAnimation(model.gameObject,0);
            var camera=Get<Camera>(player,"eyes");var oldPosition=camera.transform.position;var oldRotation=camera.transform.rotation;
            var feedback=(Behaviour)Get<Component>(player,"Feedback");bool feedbackEnabled=feedback.enabled;
            int oldMask=camera.cullingMask;var oldClear=camera.clearFlags;var oldBackground=camera.backgroundColor;
            var nodes=model.GetComponentsInChildren<Transform>(true);var layers=nodes.Select(t=>t.gameObject.layer).ToArray();
            var key=new GameObject("CloudQA isolated original PBR light").AddComponent<Light>();
            key.type=LightType.Directional;key.intensity=2.5f;key.cullingMask=1<<31;
            var group=model.GetComponent<LODGroup>();var head=Get<Transform>(art,"HeadSocket");
            GameObject reference=null;Material silhouetteMaterial=null;
            Texture2D derivedSilhouette=null,referenceSilhouette=null;
            var eyePoints=new[]{"LeftEyeSurface","RightEyeSurface"}.Select(name=>
                head.TransformPoint((Vector3)RequireType("CinematicTitleScene").GetField(name).GetValue(null))).ToArray();
            try
            {
                // Its LateUpdate still owns eye position when the motor is
                // disabled, so freeze it before placing this controlled camera.
                feedback.enabled=false;
                foreach(var node in nodes)node.gameObject.layer=31;
                camera.cullingMask=1<<31;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;
                var eyeCentre=(eyePoints[0]+eyePoints[1])*.5f;
                camera.transform.position=eyeCentre+actor.transform.forward*2.4f;camera.transform.LookAt(eyeCentre);
                var capturePosition=camera.transform.position;var captureRotation=camera.transform.rotation;
                key.transform.rotation=camera.transform.rotation;
                var silhouetteShader=Shader.Find("Universal Render Pipeline/Unlit");
                Assert.That(silhouetteShader,Is.Not.Null,"The real silhouette comparison shader is missing");
                silhouetteMaterial=new Material(silhouetteShader);
                silhouetteMaterial.SetColor("_BaseColor",Color.white);
                silhouetteMaterial.SetTexture("_BaseMap",Texture2D.whiteTexture);
                Assert.That(group.enabled && group.gameObject.activeInHierarchy,Is.True,"Frozen released visual disabled its game LODGroup");
                // Let native skinning/culling observe the active visual and new
                // production-camera placement before the controlled render pair.
                yield return null;
                Assert.That(Vector3.Distance(camera.transform.position,capturePosition),Is.LessThan(.001f),"A live camera owner moved the controlled capture");
                Assert.That(Quaternion.Angle(camera.transform.rotation,captureRotation),Is.LessThan(.01f),"A live camera owner rotated the controlled capture");
                for(int level=0;level<lods.Length;level++)
                {
                    group.ForceLOD(level);var visible=(SkinnedMeshRenderer)lods[level].renderers.Single();
                    Assert.That(visible.gameObject.activeInHierarchy,Is.True,"The real game renderer is inactive before capture");
                    var original=visible.sharedMaterials;Material[] control=null;Texture2D shown=null,absent=null,plain=null;
                    try
                    {
                        shown=SchoolCameraFrame(camera,out var projection,out _,out var viewport);
                        CloudExperienceTests.Artifact("baeksugwi-game-lod"+level+"-original-pbr.png",shown.EncodeToPNG());
                        visible.enabled=false;absent=SchoolCameraFrame(camera,out _,out _,out _);visible.enabled=true;
                        var a=shown.GetPixels32();var b=absent.GetPixels32();
                        int contribution=Enumerable.Range(0,a.Length).Count(i=>Mathf.Abs(a[i].r-b[i].r)+Mathf.Abs(a[i].g-b[i].g)+Mathf.Abs(a[i].b-b[i].b)>24);
                        TestContext.Out.WriteLine("GAME_LOD_RENDER level="+level+" geometryContribution="+contribution+" rendererBounds="+visible.bounds);
                        Assert.That(contribution,Is.GreaterThan(500),"Forced game LOD "+level+" contributes no visible geometry through the real URP camera");
                        control=original.Select(material=>new Material(material)).ToArray();
                        foreach(var material in control)material.SetTexture("_BaseMap",Texture2D.whiteTexture);
                        visible.sharedMaterials=control;plain=SchoolCameraFrame(camera,out _,out _,out _);b=plain.GetPixels32();
                        foreach(var eye in eyePoints)
                        {
                            var clip=projection*new Vector4(eye.x,eye.y,eye.z,1);Assert.That(clip.w,Is.GreaterThan(0));
                            var uv=new Vector2(viewport.x+(clip.x/clip.w*.5f+.5f)*viewport.width,
                                viewport.y+(clip.y/clip.w*.5f+.5f)*viewport.height);
                            Assert.That(uv.x,Is.InRange(.05f,.95f));Assert.That(uv.y,Is.InRange(.05f,.95f));
                            int changed=0,x=Mathf.RoundToInt(uv.x*shown.width),y=Mathf.RoundToInt(uv.y*shown.height);
                            for(int row=Mathf.Max(0,y-14);row<=Mathf.Min(shown.height-1,y+14);row++)
                                for(int col=Mathf.Max(0,x-14);col<=Mathf.Min(shown.width-1,x+14);col++)
                                {int pixel=row*shown.width+col;if(Mathf.Abs(a[pixel].r-b[pixel].r)+Mathf.Abs(a[pixel].g-b[pixel].g)+Mathf.Abs(a[pixel].b-b[pixel].b)>24)changed++;}
                            Assert.That(changed,Is.GreaterThan(8),"LOD "+level+" lost the original eye-bearing atlas contribution");
                        }
                        visible.sharedMaterials=original.Select(_=>silhouetteMaterial).ToArray();
                        derivedSilhouette=SchoolCameraFrame(camera,out _,out _,out _);
                    }
                    finally
                    {
                        visible.enabled=true;visible.sharedMaterials=original;
                        if(control!=null)foreach(var material in control)Object.DestroyImmediate(material);
                        if(shown)Object.DestroyImmediate(shown);if(absent)Object.DestroyImmediate(absent);if(plain)Object.DestroyImmediate(plain);
                    }
                }
                reference=Object.Instantiate(Resources.Load<GameObject>("Baeksugwi/Baeksugwi"),model.parent,false);
                Get<AnimationClip>(art,"RunClip").SampleAnimation(reference,0);
                Get<AnimationClip>(art,"HeadMotionClip").SampleAnimation(reference,0);
                foreach(var node in reference.GetComponentsInChildren<Transform>(true))node.gameObject.layer=31;
                model.gameObject.SetActive(false);
                var originalFrame=SchoolCameraFrame(camera,out _,out _,out _);
                try {CloudExperienceTests.Artifact("baeksugwi-retained-original-pbr.png",originalFrame.EncodeToPNG());}
                finally {Object.DestroyImmediate(originalFrame);}
                foreach(var skin in reference.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    skin.sharedMaterials=skin.sharedMaterials.Select(_=>silhouetteMaterial).ToArray();
                referenceSilhouette=SchoolCameraFrame(camera,out _,out _,out _);
                var derivedPixels=derivedSilhouette.GetPixels32();var sourcePixels=referenceSilhouette.GetPixels32();
                Assert.That(derivedPixels.Length,Is.EqualTo(sourcePixels.Length));
                int intersection=0,union=0,sourceArms=0,missingArms=0;
                for(int pixel=0;pixel<sourcePixels.Length;pixel++)
                {
                    bool sourceVisible=sourcePixels[pixel].r>128,derivedVisible=derivedPixels[pixel].r>128;
                    if(sourceVisible&&derivedVisible)intersection++;
                    if(sourceVisible||derivedVisible)union++;
                    float u=(pixel%referenceSilhouette.width+.5f)/referenceSilhouette.width;
                    float v=(pixel/referenceSilhouette.width+.5f)/referenceSilhouette.height;
                    // The controlled camera centres the eyes. Exclude the head
                    // rectangle so its large preserved face cannot hide lost hands.
                    bool arms=u<.43f||u>.57f||v<.23f||v>.77f;
                    if(arms&&sourceVisible){sourceArms++;if(!derivedVisible)missingArms++;}
                }
                Assert.That(union,Is.GreaterThan(500));Assert.That(sourceArms,Is.GreaterThan(200));
                float overlap=intersection/(float)union,missingHands=missingArms/(float)sourceArms;
                CloudExperienceTests.Artifact("baeksugwi-game-silhouette.png",derivedSilhouette.EncodeToPNG());
                CloudExperienceTests.Artifact("baeksugwi-original-silhouette.png",referenceSilhouette.EncodeToPNG());
                TestContext.Out.WriteLine("GAME_LOD_SILHOUETTE intersectionOverUnion="+overlap+" missingArmHandFraction="+missingHands+" sourceArmHandPixels="+sourceArms);
                Assert.That(overlap,Is.GreaterThanOrEqualTo(.95f),"Derived geometry lost the original visible outline");
                Assert.That(missingHands,Is.LessThanOrEqualTo(.05f),"The preserved face hides missing arm or hand silhouettes");
            }
            finally
            {
                if(reference)Object.DestroyImmediate(reference);model.gameObject.SetActive(true);
                if(derivedSilhouette)Object.DestroyImmediate(derivedSilhouette);
                if(referenceSilhouette)Object.DestroyImmediate(referenceSilhouette);
                if(silhouetteMaterial)Object.DestroyImmediate(silhouetteMaterial);
                group.ForceLOD(-1);Object.DestroyImmediate(key.gameObject);
                camera.transform.SetPositionAndRotation(oldPosition,oldRotation);camera.cullingMask=oldMask;
                camera.clearFlags=oldClear;camera.backgroundColor=oldBackground;
                for(int i=0;i<nodes.Length;i++)nodes[i].gameObject.layer=layers[i];
                body.localScale=bodyScale;body.gameObject.SetActive(bodyActive);
                navigation.enabled=navigationEnabled;((Behaviour)actor).enabled=actorEnabled;((Behaviour)art).enabled=artEnabled;
                feedback.enabled=feedbackEnabled;
            }
        }
    }
    [DefaultExecutionOrder(32000)]
    public sealed class CloudReferenceLatePoseRecorder:MonoBehaviour
    {
        public System.Action Sample;
        public System.Exception Error {get;private set;}
        void LateUpdate(){if(Sample==null||Error!=null)return;try{Sample();}catch(System.Exception error){Error=error;enabled=false;}}
    }
}
