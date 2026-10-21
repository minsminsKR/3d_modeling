using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace HappyToy.V2
{
    /// <summary>One bounded HDR floor reflection, scoped to the actual school player's camera.</summary>
    [DisallowMultipleComponent]
    public sealed class SchoolPlanarReflection : MonoBehaviour
    {
        struct ShaderState
        {
            public Camera camera;public Texture texture;public Matrix4x4 projection;public Vector4 texelSize;
            public float available,capture;
            public void Restore()
            {
                Shader.SetGlobalTexture(TextureId,texture);Shader.SetGlobalMatrix(ProjectionId,projection);
                Shader.SetGlobalVector(TexelSizeId,texelSize);Shader.SetGlobalFloat(AvailableId,available);Shader.SetGlobalFloat(CaptureId,capture);
            }
        }
        static readonly int TextureId=Shader.PropertyToID("_SchoolPlanarTexture"),ProjectionId=Shader.PropertyToID("_SchoolPlanarVP"),
            TexelSizeId=Shader.PropertyToID("_SchoolPlanarTexture_TexelSize"),AvailableId=Shader.PropertyToID("_SchoolPlanarAvailable"),
            CaptureId=Shader.PropertyToID("_SchoolPlanarCapture");
        readonly Stack<ShaderState> scopes=new Stack<ShaderState>();
        SchoolLoopWorld world;GameSession session;Camera source,reflectionCamera;RenderTexture texture;
        Matrix4x4 reflectionProjection,lastSourceProjection;Vector3 lastPosition;Quaternion lastRotation;
        float lastAspect,nextCapture;bool subscribed,rendering,hasCapture;
        public bool Prepared {get;private set;}
        public Camera OwnedCamera=>reflectionCamera;
        public RenderTexture OwnedTexture=>texture;
        public int CaptureCount {get;private set;}
        public float PlaneHeight {get;private set;}
        public Matrix4x4 LastReflectionProjection=>reflectionProjection;
        public Vector4 LastClipPlane {get;private set;}
        public bool IsCapturing=>rendering;

        public void Prepare(SchoolLoopWorld owner)
        {
            if(Prepared||!owner||!owner.Root)throw new InvalidOperationException("A built school is required once for planar reflection");
            world=owner;session=owner.GetComponent<GameSession>();source=session?session.player.eyes:null;
            if(!source)throw new InvalidOperationException("The real school player camera is missing");
            PlaneHeight=world.Root.position.y+.004f;
            var holder=new GameObject("Owned school wet-floor planar reflection camera")
                {hideFlags=HideFlags.HideInHierarchy|HideFlags.DontSave};
            holder.transform.SetParent(transform,false);reflectionCamera=holder.AddComponent<Camera>();reflectionCamera.enabled=false;
            reflectionCamera.cameraType=CameraType.Reflection;
            var data=reflectionCamera.GetUniversalAdditionalCameraData();data.renderType=CameraRenderType.Base;
            data.renderPostProcessing=false;data.volumeLayerMask=0;data.renderShadows=false;
            data.antialiasing=AntialiasingMode.None;data.requiresDepthOption=CameraOverrideOption.On;
            data.requiresColorOption=CameraOverrideOption.Off;
            Prepared=true;Subscribe();
        }
        void Subscribe()
        {
            if(subscribed)return;subscribed=true;
            RenderPipelineManager.beginCameraRendering+=BeforeCamera;RenderPipelineManager.endCameraRendering+=AfterCamera;
        }
        static ShaderState ReadState(Camera camera)=>new ShaderState {camera=camera,texture=Shader.GetGlobalTexture(TextureId),
            projection=Shader.GetGlobalMatrix(ProjectionId),texelSize=Shader.GetGlobalVector(TexelSizeId),
            available=Shader.GetGlobalFloat(AvailableId),capture=Shader.GetGlobalFloat(CaptureId)};
        bool Eligible(Camera camera)=>Prepared&&isActiveAndEnabled&&world&&session&&session.ChapterMode&&session.Chapter.Loop==world&&
            camera==source&&source.transform.position.y>PlaneHeight+.12f;
        void BeforeCamera(ScriptableRenderContext context,Camera camera)
        {
            if(rendering||!Eligible(camera))return;
            var previous=ReadState(camera);scopes.Push(previous);
            try
            {
                bool moved=!hasCapture||(source.transform.position-lastPosition).sqrMagnitude>.04f||
                    Quaternion.Angle(source.transform.rotation,lastRotation)>5||Mathf.Abs(source.aspect-lastAspect)>.0001f||
                    source.projectionMatrix!=lastSourceProjection;
                bool animate=session.InputAllowed&&Time.unscaledTime>=nextCapture;
                if(moved||animate)Capture(context);
                Shader.SetGlobalTexture(TextureId,texture);
                Shader.SetGlobalMatrix(ProjectionId,reflectionProjection);
                Shader.SetGlobalVector(TexelSizeId,texture?new Vector4(1f/texture.width,1f/texture.height,texture.width,texture.height):Vector4.zero);
                Shader.SetGlobalFloat(AvailableId,hasCapture?1:0);Shader.SetGlobalFloat(CaptureId,0);
            }
            catch
            {
                if(scopes.Count>0)scopes.Pop().Restore();throw;
            }
        }
        void EnsureTexture()
        {
            int sourceWidth=Mathf.Max(1,source.pixelWidth),sourceHeight=Mathf.Max(1,source.pixelHeight);
            float scale=Mathf.Min(.5f,640f/sourceWidth,360f/sourceHeight);
            int width=Mathf.Max(160,Mathf.RoundToInt(sourceWidth*scale)),height=Mathf.Max(90,Mathf.RoundToInt(sourceHeight*scale));
            if(texture&&texture.width==width&&texture.height==height)return;
            if(texture){texture.Release();GraphicsSurfaceLibrary.DestroyOwned(texture);}
            texture=new RenderTexture(width,height,24,RenderTextureFormat.ARGBHalf,RenderTextureReadWrite.Linear)
                {name="Owned school planar reflection HDR (bounded half resolution)",useMipMap=true,autoGenerateMips=false,
                 filterMode=FilterMode.Trilinear,wrapMode=TextureWrapMode.Clamp,antiAliasing=1};
            if(!texture.Create())throw new InvalidOperationException("Could not allocate the school planar reflection target");
            hasCapture=false;
        }
        static Matrix4x4 FloorMirror(float height)
        {
            var mirror=Matrix4x4.identity;mirror.m11=-1;mirror.m13=height*2;return mirror;
        }
        void Capture(ScriptableRenderContext context)
        {
            EnsureTexture();reflectionCamera.CopyFrom(source);reflectionCamera.enabled=false;
            reflectionCamera.cameraType=CameraType.Reflection;reflectionCamera.targetTexture=texture;
            reflectionCamera.clearFlags=CameraClearFlags.SolidColor;reflectionCamera.backgroundColor=Color.black;
            reflectionCamera.allowHDR=true;reflectionCamera.allowMSAA=false;reflectionCamera.useOcclusionCulling=false;
            reflectionCamera.cullingMask=source.cullingMask&(1<<8);reflectionCamera.farClipPlane=Mathf.Min(source.farClipPlane,40);
            var mirror=FloorMirror(PlaneHeight);
            var position=mirror.MultiplyPoint(source.transform.position);
            var forward=mirror.MultiplyVector(source.transform.forward);var up=mirror.MultiplyVector(source.transform.up);
            reflectionCamera.transform.SetPositionAndRotation(position,Quaternion.LookRotation(forward,up));
            reflectionCamera.worldToCameraMatrix=source.worldToCameraMatrix*mirror;
            var view=reflectionCamera.worldToCameraMatrix;
            var clipPoint=view.MultiplyPoint(new Vector3(0,PlaneHeight+.015f,0));
            var clipNormal=view.MultiplyVector(Vector3.up).normalized;
            LastClipPlane=new Vector4(clipNormal.x,clipNormal.y,clipNormal.z,-Vector3.Dot(clipPoint,clipNormal));
            reflectionCamera.ResetProjectionMatrix();
            reflectionCamera.projectionMatrix=reflectionCamera.CalculateObliqueMatrix(LastClipPlane);
            reflectionCamera.cullingMatrix=reflectionCamera.projectionMatrix*reflectionCamera.worldToCameraMatrix;
            // This matrix projects into sampled texture UV, not the rasterizer's RT clip space.
            // URP flips raster clip Y for an RT; ComputeScreenPos normally cancels that sign.
            // Applying its raster-flipped GPU matrix here without that cancellation samples the opposite row.
            reflectionProjection=GL.GetGPUProjectionMatrix(reflectionCamera.projectionMatrix,false)*reflectionCamera.worldToCameraMatrix;
            bool previousCulling=GL.invertCulling;var previousActive=RenderTexture.active;
            float previousCapture=Shader.GetGlobalFloat(CaptureId),previousAvailable=Shader.GetGlobalFloat(AvailableId);
            try
            {
                rendering=true;GL.invertCulling=!previousCulling;
                Shader.SetGlobalFloat(CaptureId,1);Shader.SetGlobalFloat(AvailableId,0);
                // Pinned URP17.6 provides this in-context entry without invoking frame/camera callbacks.
                // Calling SubmitRenderRequest here would recursively enter the active frame/RenderGraph.
                // The source camera's school environment is already scoped by SchoolHorrorLighting.
#pragma warning disable CS0618
                UniversalRenderPipeline.RenderSingleCamera(context,reflectionCamera);
#pragma warning restore CS0618
                texture.GenerateMips();hasCapture=true;CaptureCount++;
                lastPosition=source.transform.position;lastRotation=source.transform.rotation;lastAspect=source.aspect;
                lastSourceProjection=source.projectionMatrix;nextCapture=Time.unscaledTime+1f/15;
            }
            finally
            {
                GL.invertCulling=previousCulling;RenderTexture.active=previousActive;
                Shader.SetGlobalFloat(CaptureId,previousCapture);Shader.SetGlobalFloat(AvailableId,previousAvailable);rendering=false;
            }
        }
        void AfterCamera(ScriptableRenderContext context,Camera camera)
        {if(scopes.Count>0&&scopes.Peek().camera==camera)scopes.Pop().Restore();}
        void OnEnable(){if(Prepared)Subscribe();}
        void OnDisable()
        {
            if(subscribed){RenderPipelineManager.beginCameraRendering-=BeforeCamera;RenderPipelineManager.endCameraRendering-=AfterCamera;subscribed=false;}
            while(scopes.Count>0)scopes.Pop().Restore();
        }
        void OnDestroy()
        {
            OnDisable();Prepared=false;
            if(reflectionCamera)GraphicsSurfaceLibrary.DestroyOwned(reflectionCamera.gameObject);
            if(texture){texture.Release();GraphicsSurfaceLibrary.DestroyOwned(texture);}texture=null;reflectionCamera=null;
        }
    }
}
