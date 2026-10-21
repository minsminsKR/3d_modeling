using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace HappyToy.V2
{
    /// <summary>Only the cave world camera/cubemaps lose ambient fill. Other rooms retain their scope.</summary>
    internal sealed class EntranceFirelightEnvironment : IDisposable
    {
        struct State
        {
            public Camera camera;
            public AmbientMode mode;
            public Color ambient,sky,equator,ground,fog;
            public float intensity;
            public SphericalHarmonicsL2 probe;
            public void Restore()
            {
                RenderSettings.ambientMode=mode;RenderSettings.ambientLight=ambient;
                RenderSettings.ambientSkyColor=sky;RenderSettings.ambientEquatorColor=equator;RenderSettings.ambientGroundColor=ground;
                RenderSettings.ambientIntensity=intensity;RenderSettings.ambientProbe=probe;RenderSettings.fogColor=fog;
            }
        }
        readonly Camera worldCamera;
        readonly Bounds volume;
        readonly Stack<State> renders=new Stack<State>();
        public EntranceFirelightEnvironment(Camera worldCamera,CorridorEntranceChamber chamber)
        {
            this.worldCamera=worldCamera;
            // Include the physical connecting tunnel while looking back into the cavern.
            volume=new Bounds(chamber.transform.position+new Vector3(0,6,2),new Vector3(29,12.5f,35));
            RenderPipelineManager.beginCameraRendering+=Before;
            RenderPipelineManager.endCameraRendering+=After;
        }
        void Before(ScriptableRenderContext context,Camera camera)
        {
            if(!camera||(camera!=worldCamera&&camera.cameraType!=CameraType.Reflection)||!volume.Contains(camera.transform.position))return;
            renders.Push(new State {camera=camera,mode=RenderSettings.ambientMode,ambient=RenderSettings.ambientLight,
                sky=RenderSettings.ambientSkyColor,equator=RenderSettings.ambientEquatorColor,ground=RenderSettings.ambientGroundColor,
                intensity=RenderSettings.ambientIntensity,probe=RenderSettings.ambientProbe,fog=RenderSettings.fogColor});
            RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=Color.black;
            RenderSettings.ambientSkyColor=RenderSettings.ambientEquatorColor=RenderSettings.ambientGroundColor=Color.black;
            RenderSettings.ambientIntensity=0;RenderSettings.ambientProbe=new SphericalHarmonicsL2();RenderSettings.fogColor=Color.black;
        }
        void After(ScriptableRenderContext context,Camera camera)
        {if(renders.Count>0&&renders.Peek().camera==camera)renders.Pop().Restore();}
        public void Dispose()
        {
            RenderPipelineManager.beginCameraRendering-=Before;RenderPipelineManager.endCameraRendering-=After;
            while(renders.Count>0)renders.Pop().Restore();
        }
    }
}
