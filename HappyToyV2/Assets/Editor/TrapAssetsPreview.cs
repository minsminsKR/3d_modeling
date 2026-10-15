using System.IO;
using System.Linq;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace HappyToy.V2.Editor
{
    // Explicit silent asset review; no gameplay scene or player is launched.
    public static class TrapAssetsPreview
    {
        public static void Render()
        {
            TrapAssetsImport.Ensure();
            const string output="Builds/Traps/review";
            Directory.CreateDirectory(output);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var root=new GameObject("Authored trap review");
            var trap=TrapVisuals.Attach(root.transform);
            RenderSettings.fog=false;RenderSettings.ambientMode=AmbientMode.Flat;
            RenderSettings.ambientLight=new Color(.26f,.28f,.31f);
            var key=new GameObject("Warm key").AddComponent<Light>();key.type=LightType.Directional;
            key.intensity=1.6f;key.color=new Color(1,.87f,.72f);key.transform.rotation=Quaternion.Euler(45,-30,0);
            var fill=new GameObject("Cool fill").AddComponent<Light>();fill.type=LightType.Directional;
            fill.intensity=.7f;fill.color=new Color(.63f,.76f,1);fill.transform.rotation=Quaternion.Euler(30,160,0);
            var camera=new GameObject("Trap asset camera").AddComponent<Camera>();camera.enabled=false;
            camera.orthographic=true;camera.orthographicSize=.43f;camera.aspect=1.28f;camera.nearClipPlane=.01f;camera.farClipPlane=10;
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.028f,.033f,.043f);
            camera.GetUniversalAdditionalCameraData().renderPostProcessing=false;
            var centre=new Vector3(0,.10f,0);camera.transform.position=new Vector3(.8f,.85f,-1);camera.transform.LookAt(centre);
            var target=new RenderTexture(896,700,24);target.Create();var image=new Texture2D(896,700,TextureFormat.RGB24,false);
            void Capture(string name)
            {
                RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=target});
                var previous=RenderTexture.active;RenderTexture.active=target;image.ReadPixels(new Rect(0,0,896,700),0,0);image.Apply();RenderTexture.active=previous;
                File.WriteAllBytes(output+"/"+name+".png",image.EncodeToPNG());
            }
            Bounds Bounds()
            {
                var renderers=root.GetComponentsInChildren<Renderer>();var b=renderers[0].bounds;
                foreach(var r in renderers.Skip(1))b.Encapsulate(r.bounds);return b;
            }
            try
            {
                trap.SetClosed(0);var open=Bounds();Capture("armed");
                trap.SetClosed(1);var closed=Bounds();Capture("caught");
                if(closed.max.y<open.max.y+.07f)throw new System.InvalidOperationException("Trap jaws did not close upward around their authored hinge");
                if(closed.min.y<open.min.y-.04f)throw new System.InvalidOperationException("Trap jaws moved below the floor");
                trap.SetClosed(.55f);Capture("released");
                camera.transform.position=new Vector3(-.8f,.70f,1);camera.transform.LookAt(centre);trap.SetClosed(1);Capture("caught-reverse");
                camera.transform.position=new Vector3(.8f,.85f,-1);camera.transform.LookAt(centre);
                for(int frame=0;frame<60;frame++)
                {
                    float t=frame/15f;
                    float amount=t<1?0:t<1.12f?(t-1)/.12f:t<3?1:Mathf.Lerp(1,.55f,Mathf.Clamp01((t-3)/.22f));
                    trap.SetClosed(amount);Capture($"frame-{frame:000}");
                }
                File.WriteAllText(output+"/review.json",JsonUtility.ToJson(new Review{open=open.size,closed=closed.size,frames=60},true));
                Debug.Log("TRAP_ASSET_PREVIEW_PASS "+output);
            }
            finally{target.Release();Object.DestroyImmediate(target);Object.DestroyImmediate(image);Object.DestroyImmediate(root);}
        }
        [System.Serializable]sealed class Review{public Vector3 open,closed;public int frames;}
    }
}
