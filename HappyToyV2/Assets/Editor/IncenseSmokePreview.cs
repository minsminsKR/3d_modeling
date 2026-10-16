using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace HappyToy.V2.Editor
{
    // Explicit, silent presentation comparison. No game scene or player is run.
    public static class IncenseSmokePreview
    {
        const string BeforeRevision = "c5d8afc31425680f860c4c7dd7503e4a7ecdb7c8";
        const int Layer = 29, Width = 1280, Height = 720, Fps = 25, Frames = 100;
        const float Delta = 1f / Fps;
        const int WarmupFrames = 95; // 3.8 seconds at the same fixed gameplay step.
        const string BeforeShader = @"Shader ""Hidden/HappyToy/IncenseSmokeBeforeSnapshot""
{
    SubShader
    {
        Tags { ""RenderPipeline""=""UniversalPipeline"" ""Queue""=""Transparent"" ""RenderType""=""Transparent"" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include ""Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl""
            #include ""Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl""
            struct A { float4 p:POSITION; float2 uv:TEXCOORD0; half4 c:COLOR; };
            struct V { float4 p:SV_POSITION; float2 uv:TEXCOORD0; half4 c:COLOR; float4 screen:TEXCOORD1; float eye:TEXCOORD2; };
            V vert(A a) { V o; o.p=TransformObjectToHClip(a.p.xyz); o.uv=a.uv; o.c=a.c; o.screen=ComputeScreenPos(o.p); o.eye=-TransformWorldToView(TransformObjectToWorld(a.p.xyz)).z; return o; }
            half4 frag(V i):SV_Target
            {
                float r=length(i.uv*2-1); float a=pow(saturate(1-r),2);
                float edge=LinearEyeDepth(SampleSceneDepth(i.screen.xy/i.screen.w),_ZBufferParams)-i.eye;
                return half4(i.c.rgb,i.c.a*a*saturate(edge*8));
            }
            ENDHLSL
        }
    }
}";

        public static void Render()
        {
            string output=Path.GetFullPath(Path.Combine(Application.dataPath,"../../game/verification/ritual-items/incense-smoke-review"));
            string[] args=Environment.GetCommandLineArgs();
            for(int i=0;i+1<args.Length;i++)
                if(args[i]=="-incense-smoke-preview-output")output=Path.GetFullPath(args[i+1]);
            Directory.CreateDirectory(output);
            var previousScene=SceneManager.GetActiveScene();
            var previousTarget=RenderTexture.active;
            var previousAmbientMode=RenderSettings.ambientMode;
            var previousAmbient=RenderSettings.ambientLight;
            bool previousFog=RenderSettings.fog, previousPause=AudioListener.pause;
            float previousVolume=AudioListener.volume, previousMonochrome=Shader.GetGlobalFloat("_RitualMonochrome");
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,Application.isBatchMode?NewSceneMode.Single:NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            var owned=new List<UnityEngine.Object>();
            GameObject stage=null,handRoot=null,smokeRoot=null;
            RenderTexture target=null;
            Texture2D pixels=null,handPixels=null;
            Shader beforeShader=null;
            try
            {
                AudioListener.volume=0;AudioListener.pause=true;
                Shader.SetGlobalFloat("_RitualMonochrome",0);
                RenderSettings.fog=false;RenderSettings.ambientMode=AmbientMode.Flat;
                RenderSettings.ambientLight=new Color(.065f,.077f,.09f);
                stage=new GameObject("Opt-in narrow corridor smoke stage");
                stage.transform.position=new Vector3(1000,100,1000);
                var wall=new Material(Shader.Find("Universal Render Pipeline/Lit"));owned.Add(wall);
                wall.SetColor("_BaseColor",new Color(.105f,.116f,.12f));wall.SetFloat("_Smoothness",.12f);
                var floor=new Material(wall);owned.Add(floor);floor.SetColor("_BaseColor",new Color(.064f,.052f,.039f));
                var trim=new Material(wall);owned.Add(trim);trim.SetColor("_BaseColor",new Color(.048f,.038f,.03f));
                GameObject Box(string name,Vector3 position,Vector3 size,Material material)
                {
                    var box=GameObject.CreatePrimitive(PrimitiveType.Cube);box.name=name;
                    box.transform.SetParent(stage.transform,false);box.layer=Layer;
                    box.transform.localPosition=position;box.transform.localScale=size;
                    box.GetComponent<Renderer>().sharedMaterial=material;
                    UnityEngine.Object.DestroyImmediate(box.GetComponent<Collider>());return box;
                }
                Box("Worn floor",new Vector3(0,-.1f,1),new Vector3(3,.2f,12),floor);
                Box("Ceiling",new Vector3(0,2.8f,1),new Vector3(3,.2f,12),trim);
                Box("Far wall",new Vector3(0,1.35f,7),new Vector3(3,2.7f,.2f),wall);
                foreach(int side in new[]{-1,1})
                {
                    Box("Near corridor wall",new Vector3(side*1.5f,1.35f,-1.4f),new Vector3(.2f,2.7f,4.8f),wall);
                    Box("Far corridor wall",new Vector3(side*1.5f,1.35f,4.5f),new Vector3(.2f,2.7f,5),wall);
                    Box("Door lintel",new Vector3(side*1.48f,2.49f,1.5f),new Vector3(.24f,.42f,1.05f),trim);
                    Box("Low wood rail",new Vector3(side*1.38f,.48f,-.9f),new Vector3(.055f,.075f,5.6f),trim);
                    for(int p=0;p<4;p++)Box("Wall post",new Vector3(side*1.36f,1.35f,-3.3f+p*2.6f),new Vector3(.075f,2.7f,.085f),trim);
                }
                var lamp=new GameObject("Dim corridor fill").AddComponent<Light>();lamp.transform.SetParent(stage.transform,false);
                lamp.type=LightType.Point;lamp.range=7;lamp.intensity=1.35f;lamp.color=new Color(.71f,.81f,.85f);
                lamp.transform.localPosition=new Vector3(-.3f,2.25f,.6f);lamp.cullingMask=1<<Layer;lamp.shadows=LightShadows.None;
                var key=new GameObject("Soft warm entry light").AddComponent<Light>();key.transform.SetParent(stage.transform,false);
                key.type=LightType.Spot;key.range=9;key.intensity=3.2f;key.spotAngle=67;key.innerSpotAngle=34;
                key.color=new Color(.92f,.84f,.65f);key.cullingMask=1<<Layer;key.shadows=LightShadows.None;
                key.transform.localPosition=new Vector3(-.25f,1.4f,-2.4f);key.transform.localRotation=Quaternion.Euler(3,8,0);
                var camera=new GameObject("Smoke review camera").AddComponent<Camera>();camera.transform.SetParent(stage.transform,false);
                camera.enabled=false;camera.transform.localPosition=new Vector3(0,1.4f,-2.6f);
                camera.fieldOfView=58;camera.aspect=(float)Width/Height;camera.nearClipPlane=.02f;camera.farClipPlane=16;
                camera.cullingMask=1<<Layer;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.018f,.022f,.026f,1);
                var data=camera.GetUniversalAdditionalCameraData();data.requiresDepthTexture=true;data.renderPostProcessing=false;data.volumeLayerMask=0;
                target=new RenderTexture(Width,Height,24,RenderTextureFormat.ARGB32);target.Create();
                pixels=new Texture2D(Width,Height,TextureFormat.RGBA32,false);
                handPixels=new Texture2D(Width,Height,TextureFormat.RGBA32,false);
                handRoot=new GameObject("Original generated incense and hand pose");
                var hands=handRoot.AddComponent<FirstPersonHands>();
                var handCamera=hands.PrepareEditorPreview(PlayerItem.Incense,-1);handCamera.enabled=false;
                if(!hands.TryGetIncenseTip(out var cameraLocalTip))throw new InvalidOperationException("The real held incense tip is unavailable");
                RenderPipeline.SubmitRenderRequest(handCamera,new UniversalRenderPipeline.SingleCameraRequest{destination=hands.Output});
                RenderTexture.active=hands.Output;handPixels.ReadPixels(new Rect(0,0,Width,Height),0,0);handPixels.Apply();
                RenderTexture.active=previousTarget;var overlay=handPixels.GetPixels32();
                var source=camera.transform.TransformPoint(cameraLocalTip);
                var direction=(Vector3.forward+Vector3.right*.24f).normalized;
                beforeShader=ShaderUtil.CreateShaderAsset(BeforeShader,true);
                if(!beforeShader || ShaderUtil.GetShaderMessages(beforeShader).Any(m=>m.severity.ToString()=="Error"))throw new InvalidOperationException("The archived before shader did not compile");
                var afterShader=Resources.Load<Shader>("IncenseSmoke");
                if(!afterShader)throw new InvalidOperationException("Missing current incense smoke shader");
                for(int pass=0;pass<2;pass++)
                {
                    string label=pass==0?"before":"after";string frames=Path.Combine(output,"frames",label);Directory.CreateDirectory(frames);
                    smokeRoot=new GameObject(label+" incense visual");smokeRoot.transform.SetParent(stage.transform,false);smokeRoot.layer=Layer;
                    ParticleSystem legacy=null;Material legacyMaterial=null;IncenseSmokeTrail trail=null;
                    if(pass==0)
                    {
                        legacy=smokeRoot.AddComponent<ParticleSystem>();var main=legacy.main;
                        main.playOnAwake=false;main.simulationSpace=ParticleSystemSimulationSpace.World;main.maxParticles=480;main.simulationSpeed=0;
                        var emission=legacy.emission;emission.enabled=false;legacy.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);legacy.Play(false);legacy.Pause(false);
                        legacyMaterial=new Material(beforeShader);owned.Add(legacyMaterial);
                        var renderer=legacy.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=legacyMaterial;renderer.renderMode=ParticleSystemRenderMode.Billboard;
                        renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
                    }
                    else
                    {
                        trail=smokeRoot.AddComponent<IncenseSmokeTrail>();trail.Prepare(camera);
                        for(int warm=0;warm<WarmupFrames;warm++)trail.Advance(source,direction,Delta,30-(warm+1)*Delta);
                    }
                    var oldParticles=new ParticleSystem.Particle[480];
                    var oldDirections=new[]{(Vector3.forward-Vector3.right*.50f).normalized,direction,(Vector3.forward+Vector3.right*.65f).normalized};
                    for(int frame=0;frame<Frames;frame++)
                    {
                        float age=(WarmupFrames+frame+1)*Delta,remaining=30-age;
                        if(pass==0)
                        {
                            int count=0;
                            for(int branch=0;branch<oldDirections.Length;branch++)
                            {
                                var toward=oldDirections[branch];var side=Vector3.Cross(Vector3.up,toward);
                                for(int i=0;i<64;i++)
                                {
                                    float t=Mathf.Repeat(i/64f+age*.26f+branch*.113f,1);
                                    float fade=Mathf.Sin(t*Mathf.PI)*Mathf.Min(1,remaining)*.28f;
                                    oldParticles[count++]=new ParticleSystem.Particle {
                                        position=source+toward*(t*2.8f)+Vector3.up*(t*.48f)+side*(Mathf.Sin(t*13-age*1.3f+branch)*.035f*(1+t*2)),
                                        startSize=.045f+t*.16f,startColor=new Color(.74f,.81f,.78f,fade),
                                        startLifetime=1,remainingLifetime=1,rotation=i*47+age*14
                                    };
                                }
                            }
                            legacy.SetParticles(oldParticles,count);
                        }
                        else trail.Advance(source,direction,Delta,remaining);
                        RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=target});
                        RenderTexture.active=target;pixels.ReadPixels(new Rect(0,0,Width,Height),0,0);RenderTexture.active=previousTarget;
                        var rgb=pixels.GetRawTextureData<Color32>();
                        for(int pixel=0;pixel<rgb.Length;pixel++)
                        {
                            var top=overlay[pixel];if(top.a==0)continue;var under=rgb[pixel];int a=top.a,inv=255-a;
                            rgb[pixel]=new Color32((byte)((top.r*a+under.r*inv+127)/255),(byte)((top.g*a+under.g*inv+127)/255),(byte)((top.b*a+under.b*inv+127)/255),255);
                        }
                        pixels.Apply(false,false);
                        File.WriteAllBytes(Path.Combine(frames,"frame-"+frame.ToString("0000",CultureInfo.InvariantCulture)+".png"),pixels.EncodeToPNG());
                        if(frame%Fps==0)Debug.Log("Incense smoke "+label+" "+frame+" / "+Frames);
                    }
                    if(pass==1 && ShaderUtil.GetShaderMessages(afterShader).Any(m=>m.severity.ToString()=="Error"))throw new InvalidOperationException("The current incense smoke shader did not compile");
                    UnityEngine.Object.DestroyImmediate(smokeRoot);smokeRoot=null;
                }
                File.WriteAllText(Path.Combine(output,"manifest.json"),
                    "{\n  \"fps\":25,\n  \"frameCountPerClip\":100,\n  \"durationSeconds\":4,\n  \"warmupSeconds\":3.8,\n  \"width\":1280,\n  \"height\":720,\n  \"playbackRate\":1,\n  \"editorOnlyPreview\":true,\n  \"nativeGameplayCapture\":false,\n  \"silent\":true,\n  \"beforeRevision\":\""+BeforeRevision+"\",\n  \"before\":\"Archived original shader and exact 3 x 64 billboard equations\",\n  \"after\":\"Current IncenseSmokeTrail.Prepare/Advance and current Resources/IncenseSmoke shader, one direction\",\n  \"source\":\"Actual FirstPersonHands.TryGetIncenseTip, original generated hand/incense prefab overlay\",\n  \"framePattern\":\"frames/{before,after}/frame-{0:0000}.png\"\n}\n");
                Debug.Log("Incense smoke visual comparison complete: "+output);
            }
            finally
            {
                RenderTexture.active=previousTarget;
                if(smokeRoot)UnityEngine.Object.DestroyImmediate(smokeRoot);
                if(handRoot)UnityEngine.Object.DestroyImmediate(handRoot);
                if(stage)UnityEngine.Object.DestroyImmediate(stage);
                if(target){target.Release();UnityEngine.Object.DestroyImmediate(target);}
                if(pixels)UnityEngine.Object.DestroyImmediate(pixels);
                if(handPixels)UnityEngine.Object.DestroyImmediate(handPixels);
                foreach(var item in owned)if(item)UnityEngine.Object.DestroyImmediate(item);
                if(beforeShader)UnityEngine.Object.DestroyImmediate(beforeShader);
                RenderSettings.ambientMode=previousAmbientMode;RenderSettings.ambientLight=previousAmbient;RenderSettings.fog=previousFog;
                if(scene.IsValid()&&!Application.isBatchMode)EditorSceneManager.CloseScene(scene,true);
                if(previousScene.IsValid()&&previousScene.isLoaded)SceneManager.SetActiveScene(previousScene);

                Shader.SetGlobalFloat("_RitualMonochrome",previousMonochrome);
                AudioListener.pause=previousPause;AudioListener.volume=previousVolume;
            }
        }
    }
}



