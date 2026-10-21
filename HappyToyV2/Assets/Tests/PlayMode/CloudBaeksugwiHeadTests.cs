using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using static HappyToy.V2.CloudTests.RuntimeAccess;
using Object=UnityEngine.Object;

namespace HappyToy.V2.CloudTests
{
    public sealed partial class CloudPlayModeTests
    {
        [Test]
        public void BaeksugwiHeadHasPronouncedRollBothWaysWithUnequalHoldsAndSnaps()
        {
            var instance=Object.Instantiate(Resources.Load<GameObject>("Baeksugwi/Baeksugwi"));
            try
            {
                var clip=Resources.Load<AnimationClip>("Baeksugwi/Head_Writhe");
                Assert.That(clip.length,Is.GreaterThan(30),"A short repeated sway is too predictable");
                clip.SampleAnimation(instance,0);
                var head=instance.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="Head");
                var neutral=head.rotation;var previous=head.rotation;
                float minRoll=0,maxRoll=0,maxSpeed=0;int stillFrames=0,snapFrames=0,hold=0;
                var holds=new System.Collections.Generic.List<int>();
                for(int frame=1;frame<=Mathf.RoundToInt(clip.length*60);frame++)
                {
                    clip.SampleAnimation(instance,frame/60f);
                    var delta=Quaternion.Inverse(instance.transform.rotation)*head.rotation*Quaternion.Inverse(neutral)*instance.transform.rotation;
                    float roll=Vector3.SignedAngle(Vector3.up,Vector3.ProjectOnPlane(delta*Vector3.up,Vector3.forward),Vector3.forward);
                    minRoll=Mathf.Min(minRoll,roll);maxRoll=Mathf.Max(maxRoll,roll);
                    float speed=Quaternion.Angle(previous,head.rotation)*60;maxSpeed=Mathf.Max(maxSpeed,speed);
                    if(speed<12){stillFrames++;hold++;}else{if(hold>=5)holds.Add(hold);hold=0;}
                    if(speed>140)snapFrames++;
                    previous=head.rotation;
                }
                Assert.That(minRoll,Is.LessThan(-35));Assert.That(maxRoll,Is.GreaterThan(35));
                Assert.That(stillFrames,Is.GreaterThan(180));Assert.That(snapFrames,Is.GreaterThan(20));
                Assert.That(holds.Count,Is.GreaterThan(5));Assert.That(holds.Max()-holds.Min(),Is.GreaterThan(15),"Holds must have different durations");
                Assert.That(maxSpeed,Is.LessThan(2200),"Keep rotations continuous through fast cocks");
                Assert.That(Quaternion.Angle(neutral,head.rotation),Is.LessThan(.1f));
            }
            finally{Object.DestroyImmediate(instance);}
        }

        [Test]
        public void BaeksugwiHeadActionMovesOnlyHeadAndLoopsWithoutMovingAnyArm()
        {
            var instance=Object.Instantiate(Resources.Load<GameObject>("Baeksugwi/Baeksugwi"));
            try
            {
                Resources.Load<AnimationClip>("Baeksugwi/Run_InPlace").SampleAnimation(instance,0);
                var clip=Resources.Load<AnimationClip>("Baeksugwi/Head_Writhe");Assert.That(clip && clip.legacy,Is.True);
                var nodes=instance.GetComponentsInChildren<Transform>(true);
                var head=nodes.Single(t=>t.name=="Head");var baseline=head.localRotation;
                var others=nodes.Where(t=>t!=head).ToArray();var rotations=others.Select(t=>t.localRotation).ToArray();
                var positions=nodes.Select(t=>t.localPosition).ToArray();var scales=nodes.Select(t=>t.localScale).ToArray();
                clip.SampleAnimation(instance,0);var first=head.localRotation;float swing=0;
                Assert.That(Quaternion.Angle(baseline,first),Is.LessThan(.2f),"Neutral head must fit the existing rig");
                for(int sample=0;sample<=32;sample++)
                {
                    clip.SampleAnimation(instance,clip.length*sample/32f);swing=Mathf.Max(swing,Quaternion.Angle(first,head.localRotation));
                    for(int i=0;i<others.Length;i++)Assert.That(Quaternion.Angle(rotations[i],others[i].localRotation),Is.LessThan(.01f),others[i].name+" body rotation changed");
                    for(int i=0;i<nodes.Length;i++)
                    {
                        Assert.That(Vector3.Distance(positions[i],nodes[i].localPosition),Is.LessThan(.00001f),nodes[i].name+" joint translation changed");
                        Assert.That(Vector3.Distance(scales[i],nodes[i].localScale),Is.LessThan(.00001f));
                    }
                }
                Assert.That(swing,Is.GreaterThan(18));Assert.That(Quaternion.Angle(first,head.localRotation),Is.LessThan(.1f));
            }
            finally{Object.DestroyImmediate(instance);}
        }

        [UnityTest,Timeout(90000)]
        public IEnumerator BaeksugwiHeadWritheUsesChaseStateFreezesOnPauseAndStopsWhenCalm()
        {
            yield return PrepareCorridorMaskRunner();
            var actor=Get<Component>(Get<Component>(session,"Corridor"),"Mask");((Behaviour)actor).enabled=false;
            PlacePlayer(new Vector3(200,5,200),false);
            var state=Call(actor,"CaptureChapterProgress");Set(state,"state","Chase");Set(state,"memory",8f);
            Call(actor,"RestoreCorridorProgress",state);
            var art=actor.GetComponent(RequireType("MaskHorrorVisual"));var head=Get<Transform>(art,"HeadSocket");
            Assert.That(Get<AnimationClip>(art,"HeadMotionClip"),Is.SameAs(Resources.Load<AnimationClip>("Baeksugwi/Head_Writhe")));
            var model=Get<Transform>(art,"BodyModel");var arms=model.GetComponentsInChildren<Transform>(true).Where(t=>t.name.EndsWith("_Wrist")).ToArray();
            yield return null;var first=head.localRotation;var planted=arms.Select(t=>t.localRotation).ToArray();float swing=0;
            for(int i=0;i<10;i++) {yield return Delay(.12f);swing=Mathf.Max(swing,Quaternion.Angle(first,head.localRotation));}
            Assert.That(swing,Is.GreaterThan(12));Assert.That(Get<bool>(art,"HeadWrithing"),Is.True);
            for(int i=0;i<arms.Length;i++)Assert.That(Quaternion.Angle(planted[i],arms[i].localRotation),Is.LessThan(.01f),"Stopped chase must move only the head");
            Call(shell,"Pause");var paused=head.localRotation;float clock=Get<float>(art,"HeadMotionTime");yield return Delay(.2f);
            Assert.That(Quaternion.Angle(paused,head.localRotation),Is.LessThan(.01f));Assert.That(Get<float>(art,"HeadMotionTime"),Is.EqualTo(clock));
            Call(shell,"Resume");state=Call(actor,"CaptureChapterProgress");Set(state,"state","Wander");Set(state,"memory",0f);Call(actor,"RestoreCorridorProgress",state);
            yield return null;yield return null;Assert.That(Get<float>(art,"HeadMotionTime"),Is.Zero);Assert.That(Get<bool>(art,"HeadWrithing"),Is.False);
        }

        [UnityTest,Timeout(90000)]
        public IEnumerator BaeksugwiHeadWritheAlsoMovesTheSchoolFloatingMaskBeforeTransformation()
        {
            yield return RecoveryUiReady();yield return BeginLegacySchoolFixture();
            var actor=Get<Component>(Get<Component>(session,"Chapter"),"Mask");((Behaviour)actor).enabled=false;
            var state=Call(actor,"CaptureChapterProgress");Set(state,"active",true);Set(state,"introComplete",true);
            Set(state,"transformed",false);Set(state,"state","Chase");Set(state,"memory",3f);Call(actor,"RestoreChapterProgress",state);
            var mask=Get<Transform>(actor,"mask");var body=Get<Transform>(actor,"body");var art=actor.GetComponent(RequireType("MaskHorrorVisual"));
            Assert.That(body.gameObject.activeSelf,Is.False);yield return null;var first=mask.localRotation;float swing=0;
            for(int i=0;i<12;i++){yield return Delay(.1f);swing=Mathf.Max(swing,Quaternion.Angle(first,mask.localRotation));}
            Assert.That(swing,Is.GreaterThan(15));Assert.That(Get<bool>(art,"HeadWrithing"),Is.True);
            Call(shell,"Pause");var paused=mask.localRotation;yield return Delay(.2f);Assert.That(Quaternion.Angle(paused,mask.localRotation),Is.LessThan(.01f));
            Call(shell,"Resume");state=Call(actor,"CaptureChapterProgress");Set(state,"state","Wander");Set(state,"memory",0f);Call(actor,"RestoreChapterProgress",state);
            yield return null;var calm=mask.localRotation;yield return Delay(.2f);
            Assert.That(Get<bool>(art,"HeadWrithing"),Is.False);Assert.That(Quaternion.Angle(calm,mask.localRotation),Is.LessThan(.01f));
        }

        [UnityTest,Timeout(90000)]
        public IEnumerator BaeksugwiTitleHeadMovesAtZeroTimeScaleWithAllBodySupportsPlanted()
        {
            yield return RecoveryUiReady();Call(shell,"RestoreDefaultSettings");yield return null;yield return null;
            var title=One("CinematicTitleScene");Assert.That(Time.timeScale,Is.Zero);
            Assert.That(Get<AnimationClip>(title,"HeadMotionClip"),Is.SameAs(Resources.Load<AnimationClip>("Baeksugwi/Head_Writhe")));
            var creature=GameObject.Find("Actual Baeksugwi source clone");Assert.That(creature,Is.Not.Null);
            var nodes=creature.GetComponentsInChildren<Transform>(true);var head=nodes.Single(t=>t.name=="Head");
            var others=nodes.Where(t=>t!=head).ToArray();var rotations=others.Select(t=>t.localRotation).ToArray();var positions=others.Select(t=>t.localPosition).ToArray();
            var first=head.localRotation;float swing=0;
            float motionStart=Get<float>(title,"HeadMotionTime"),wallStart=Time.realtimeSinceStartup;
            while(Get<float>(title,"HeadMotionTime")-motionStart<2.4f && Time.realtimeSinceStartup-wallStart<18)
            {
                yield return Delay(.15f);swing=Mathf.Max(swing,Quaternion.Angle(first,head.localRotation));
                for(int i=0;i<others.Length;i++)
                {
                    Assert.That(Quaternion.Angle(rotations[i],others[i].localRotation),Is.LessThan(.01f),"Title body rotated: "+others[i].name);
                    Assert.That(Vector3.Distance(positions[i],others[i].localPosition),Is.LessThan(.00001f),"Title joint moved: "+others[i].name);
                }
            }
            Assert.That(Get<float>(title,"HeadMotionTime")-motionStart,Is.GreaterThanOrEqualTo(2.4f),"The looming title head did not advance");
            Assert.That((Get<float>(title,"HeadMotionTime")-motionStart)/(Time.realtimeSinceStartup-wallStart),Is.InRange(.12f,.25f),
                "The giant title head must turn slowly in unscaled time");
            Assert.That(swing,Is.GreaterThan(15));Assert.That(Get<int>(title,"PlantedLimbCount"),Is.EqualTo(12));
            Assert.That(Get<float>(title,"MaximumSupportFloorGap"),Is.LessThan(.006f));
            Call(title,"SetVisible",false);float clock=Get<float>(title,"HeadMotionTime");yield return Delay(.15f);
            Assert.That(Get<float>(title,"HeadMotionTime"),Is.EqualTo(clock));
            Call(title,"SetVisible",true);Call(shell,"ToggleReducedMotion");yield return Delay(1.1f);
            first=head.localRotation;clock=Get<float>(title,"HeadMotionTime");yield return Delay(1.1f);
            Assert.That(Get<float>(title,"HeadMotionTime"),Is.EqualTo(clock));Assert.That(Quaternion.Angle(first,head.localRotation),Is.LessThan(.01f));
        }

        static T TitleSceneField<T>(Component title,string name) => (T)title.GetType()
            .GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(title);

        static Color32[] TitleRenderedPixels(Camera camera,RenderTexture target,string artifact=null)
        {
            RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=target});
            var frame=CloudExperienceTests.Read(target);
            try
            {
                if(artifact!=null)CloudExperienceTests.Artifact(artifact,frame.EncodeToPNG());
                return frame.GetPixels32();
            }
            finally{Object.Destroy(frame);}
        }

        static int TitleEyeContribution(Color32[] bright,Color32[] dark,int width,int height,Vector2 eye,int radius)
        {
            int count=0,cx=Mathf.RoundToInt(eye.x*width),cy=Mathf.RoundToInt(eye.y*height);
            for(int y=Mathf.Max(0,cy-radius);y<=Mathf.Min(height-1,cy+radius);y++)
                for(int x=Mathf.Max(0,cx-radius);x<=Mathf.Min(width-1,cx+radius);x++)
                {
                    int i=y*width+x;
                    if(bright[i].r-dark[i].r>30 && bright[i].r>bright[i].g*1.5f && bright[i].r>100)count++;
                }
            return count;
        }

        IEnumerator CaptureLoomingTitleMenu(Component view,string artifact,int width,int height,List<string> layoutErrors)
        {
            // Retain the actual UI paint, PNG and font checks. The title's
            // safe area excludes the hidden, separately attached item wheel.
            yield return CaptureMenu(view,artifact,width,height,new List<string>());
            var root=Get<VisualElement>(view,"Root");var menu=root.Q<VisualElement>("safe-area");
            Assert.That(menu,Is.Not.Null);Assert.That(menu.visible,Is.True);
            Assert.That(menu.resolvedStyle.display,Is.Not.EqualTo(DisplayStyle.None));
            Assert.That(menu.Q<Label>("title-brand"),Is.Not.Null);
            Assert.That(menu.Q<Button>("begin").enabledInHierarchy,Is.True);
            var bounds=root.worldBound;var area=menu.worldBound;
            Assert.That(area.xMin,Is.GreaterThanOrEqualTo(bounds.xMin-1));Assert.That(area.xMax,Is.LessThanOrEqualTo(bounds.xMax+1));
            Assert.That(area.yMin,Is.GreaterThanOrEqualTo(bounds.yMin-1));Assert.That(area.yMax,Is.LessThanOrEqualTo(bounds.yMax+1));
            layoutErrors.AddRange(CloudExperienceTests.Layout(menu).Select(error=>artifact+": "+error));
        }

        [UnityTest,Timeout(90000)]
        public IEnumerator BaeksugwiTitleRendersLoomingDarkBodyAndStrongOriginalEyesAcrossMenuAspectRatios()
        {
            yield return RecoveryUiReady();Call(shell,"RestoreDefaultSettings");yield return null;yield return null;
            var title=One("CinematicTitleScene");var view=One("GameShellView");
            var camera=TitleSceneField<Camera>(title,"camera");var set=TitleSceneField<GameObject>(title,"set");
            var creature=TitleSceneField<GameObject>(title,"creature");var target=Get<RenderTexture>(title,"Image");
            var head=creature.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="Head");
            var renderers=set.GetComponentsInChildren<Renderer>(true);
            var priorMaterials=renderers.Select(renderer=>renderer.sharedMaterials).ToArray();
            var skins=creature.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            var creatureRenderers=new HashSet<Renderer>(skins);
            var eyeMaterials=skins.SelectMany(skin=>skin.sharedMaterials).Distinct().ToArray();
            var eyeGlow=eyeMaterials.Select(material=>material.GetFloat("_EyeGlow")).ToArray();
            bool titleEnabled=((Behaviour)title).enabled;var clear=camera.clearFlags;var background=camera.backgroundColor;
            var previousTarget=Get<RenderTexture>(view,"CaptureTarget");
            var previousSize=previousTarget?new Vector2Int(previousTarget.width,previousTarget.height):Vector2Int.zero;
            Material white=null,black=null;
            try
            {
                // Freeze only this owned visual scene while comparing the same real
                // pose/materials through its production camera. Game actors stay untouched.
                ((Behaviour)title).enabled=false;
                Assert.That(eyeGlow.All(value=>value>0),Is.True,"Both original eye surfaces need real emission");
                var eyes=new[]{"LeftEyeSurface","RightEyeSurface"}.Select(name=>
                    camera.WorldToViewportPoint(head.TransformPoint((Vector3)title.GetType().GetField(name).GetValue(null)))).ToArray();
                Assert.That(eyes.All(point=>point.z>camera.nearClipPlane),Is.True);
                var native=TitleRenderedPixels(camera,target,"title-looming-background.png");
                foreach(var material in eyeMaterials)material.SetFloat("_EyeGlow",0);
                var withoutEyes=TitleRenderedPixels(camera,target,"title-looming-eyes-off.png");
                for(int eye=0;eye<eyes.Length;eye++)
                    Assert.That(TitleEyeContribution(native,withoutEyes,target.width,target.height,eyes[eye],32),Is.GreaterThan(12),
                        "Original eye "+eye+" made no strong local contribution to the actual camera frame");
                for(int i=0;i<eyeMaterials.Length;i++)eyeMaterials[i].SetFloat("_EyeGlow",eyeGlow[i]);

                // GPU geometry mask includes real perspective, skinning and doorway
                // occlusion; a black environment cannot be mistaken for body pixels.
                white=new Material(Shader.Find("Universal Render Pipeline/Unlit"));white.SetColor("_BaseColor",Color.white);
                black=new Material(white);black.SetColor("_BaseColor",Color.black);
                camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;
                for(int i=0;i<renderers.Length;i++)renderers[i].sharedMaterials=priorMaterials[i]
                    .Select(_=>creatureRenderers.Contains(renderers[i])?white:black).ToArray();
                var geometry=TitleRenderedPixels(camera,target,"title-looming-visible-body-mask.png");
                var body=Enumerable.Range(0,geometry.Length).Where(i=>geometry[i].r>220&&geometry[i].g>220&&geometry[i].b>220).ToArray();
                Assert.That(body.Length/(float)geometry.Length,Is.GreaterThan(.07f),"Visible geometry became a small or obscured figure");
                float width=(body.Max(i=>i%target.width)-body.Min(i=>i%target.width))/(float)target.width;
                float height=(body.Max(i=>i/target.width)-body.Min(i=>i/target.width))/(float)target.height;
                Assert.That(width,Is.GreaterThan(.42f),"The creature no longer spans a large portion of the title frame");
                Assert.That(height,Is.GreaterThan(.48f),"The title silhouette lost its looming height");
                var luminance=body.Select(i=>(.2126f*withoutEyes[i].r+.7152f*withoutEyes[i].g+.0722f*withoutEyes[i].b)/255).OrderBy(value=>value).ToArray();
                float bodyP95=luminance[Mathf.FloorToInt((luminance.Length-1)*.95f)];
                Assert.That(bodyP95,Is.LessThan(.09f),"Bright skin still reveals the body instead of a dark silhouette");
                TestContext.Out.WriteLine("TITLE_LOOMING_RENDER visibleFraction="+body.Length/(float)geometry.Length+" width="+width+" height="+height+" eyeOffBodyP95="+bodyP95);
                for(int i=0;i<renderers.Length;i++)renderers[i].sharedMaterials=priorMaterials[i];
                camera.clearFlags=clear;camera.backgroundColor=background;
                TitleRenderedPixels(camera,target);

                var layoutErrors=new List<string>();
                yield return CaptureLoomingTitleMenu(view,"title-looming-default-1280x720.png",1280,720,layoutErrors);
                Call(shell,"ToggleLargeText");Call(shell,"ToggleHighContrast");yield return null;yield return null;
                foreach(var size in new[]{new Vector2Int(1280,720),new Vector2Int(1024,768),new Vector2Int(2560,1080)})
                {
                    for(int i=0;i<eyeMaterials.Length;i++)eyeMaterials[i].SetFloat("_EyeGlow",eyeGlow[i]);
                    TitleRenderedPixels(camera,target);
                    yield return CaptureLoomingTitleMenu(view,"title-looming-large-contrast-"+size.x+"x"+size.y+".png",size.x,size.y,layoutErrors);
                    var image=CloudExperienceTests.Read(Get<RenderTexture>(view,"CaptureTarget"));
                    Color32[] shown;try{shown=image.GetPixels32();}finally{Object.Destroy(image);}
                    foreach(var material in eyeMaterials)material.SetFloat("_EyeGlow",0);
                    TitleRenderedPixels(camera,target);for(int paint=0;paint<4;paint++)yield return null;
                    image=CloudExperienceTests.Read(Get<RenderTexture>(view,"CaptureTarget"));
                    Color32[] hidden;try{hidden=image.GetPixels32();}finally{Object.Destroy(image);}
                    var root=Get<VisualElement>(view,"Root");
                    var backdrop=root.Q<VisualElement>("menu-face-background");
                    Assert.That(backdrop.pickingMode,Is.EqualTo(PickingMode.Ignore));
                    float scale=Mathf.Max(size.x/(float)target.width,size.y/(float)target.height);
                    foreach(var eye in eyes)
                    {
                        var uv=new Vector2(.5f+(eye.x-.5f)*target.width*scale/size.x,.5f+(eye.y-.5f)*target.height*scale/size.y);
                        Assert.That(uv.x,Is.InRange(.03f,.97f),"Cover crop cut off an original eye at "+size);
                        Assert.That(uv.y,Is.InRange(.03f,.97f),"Cover crop cut off an original eye at "+size);
                        Assert.That(TitleEyeContribution(shown,hidden,size.x,size.y,uv,Mathf.CeilToInt(32*scale)),Is.GreaterThan(2),
                            "Menu scrim or crop hid an original glowing eye at "+size);
                        var point=new Vector2(root.worldBound.xMin+uv.x*root.worldBound.width,root.worldBound.yMax-uv.y*root.worldBound.height);
                        Assert.That(root.Query<Button>().ToList().Any(button=>button.worldBound.Contains(point)),Is.False,"An eye was placed under a menu button");
                    }
                    foreach(var button in root.Query<Button>().ToList().Where(button=>button.enabledInHierarchy))
                    {
                        var picked=root.panel.Pick(button.worldBound.center);
                        Assert.That(picked==button||picked!=null&&button.Contains(picked),Is.True,"The title art blocks menu input: "+button.name);
                    }
                }
                Assert.That(layoutErrors,Is.Empty,string.Join("\n",layoutErrors));
            }
            finally
            {
                for(int i=0;i<renderers.Length;i++)if(renderers[i])renderers[i].sharedMaterials=priorMaterials[i];
                for(int i=0;i<eyeMaterials.Length;i++)if(eyeMaterials[i])eyeMaterials[i].SetFloat("_EyeGlow",eyeGlow[i]);
                camera.clearFlags=clear;camera.backgroundColor=background;
                TitleRenderedPixels(camera,target);((Behaviour)title).enabled=titleEnabled;
                Call(shell,"RestoreDefaultSettings");
                if(previousSize!=Vector2Int.zero)Call(view,"SetCaptureSize",previousSize.x,previousSize.y);else Call(view,"RestoreDisplayOutput");
                if(white)Object.Destroy(white);if(black)Object.Destroy(black);
            }
        }
    }
}
