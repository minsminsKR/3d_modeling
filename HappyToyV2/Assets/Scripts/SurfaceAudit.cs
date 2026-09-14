using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
namespace HappyToy.V2
{
    // Diagnostic material comparison only; restores materials before exiting.
    public sealed class SurfaceAudit:MonoBehaviour
    {
        string output;
        float deadline;
        void Update(){if(deadline>0&&Time.realtimeSinceStartup>deadline){Debug.LogError("Surface audit timeout");Application.Quit(2);}}
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]static void Install()
        {var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,"-v2-surface-output");if(i<0||i+1>=args.Length)return;Application.runInBackground=true;new GameObject("Skin surface audit").AddComponent<SurfaceAudit>().output=args[i+1];}
        IEnumerator Start()
        {
            deadline=Time.realtimeSinceStartup+30;Directory.CreateDirectory(output);yield return new WaitForSecondsRealtime(1);
            var brain=FindFirstObjectByType<StoryDirector>().stalker;brain.transform.SetPositionAndRotation(Vector3.zero,Quaternion.Euler(0,-90,0));brain.gameObject.SetActive(true);brain.enabled=false;
            var motion=brain.GetComponent<V1MonsterMotion>();yield return new WaitForSecondsRealtime(.3f);yield return new WaitForEndOfFrame();
            var skins=brain.GetComponentsInChildren<SkinnedMeshRenderer>();var originals=new Material[skins.Length][];
            for(int i=0;i<skins.Length;i++)originals[i]=skins[i].sharedMaterials;
            Capture("lit");
            var template=Resources.Load<Material>("SurfaceAudit");if(!template){Debug.LogError("Missing surface diagnostic material");Application.Quit(1);yield break;}
            var unlit=Instantiate(template);unlit.SetTexture("_BaseMap",originals[0][0].GetTexture("_BaseMap"));unlit.SetColor("_BaseColor",Color.white);
            foreach(var skin in skins){var materials=new Material[skin.sharedMaterials.Length];for(int i=0;i<materials.Length;i++)materials[i]=unlit;skin.sharedMaterials=materials;}
            unlit.SetFloat("_Cull",2);Capture("unlit-back-cull");unlit.SetFloat("_Cull",0);Capture("unlit-two-sided");
            for(int i=0;i<skins.Length;i++)skins[i].sharedMaterials=originals[i];Destroy(unlit);
            Application.Quit(0);
        }
        void Capture(string name)
        {
            var go=new GameObject("Surface camera");var camera=go.AddComponent<Camera>();camera.CopyFrom(GameSession.Current.player.eyes);camera.enabled=false;camera.fieldOfView=60;
            camera.transform.position=new Vector3(-3.1f,1.3f,-.8f);camera.transform.LookAt(Vector3.up*1.05f);
            var target=new RenderTexture(960,960,24);target.Create();RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=target});
            var old=RenderTexture.active;RenderTexture.active=target;var frame=new Texture2D(960,960,TextureFormat.RGB24,false);frame.ReadPixels(new Rect(0,0,960,960),0,0);frame.Apply();
            File.WriteAllBytes(Path.Combine(output,name+".png"),frame.EncodeToPNG());RenderTexture.active=old;target.Release();Destroy(target);Destroy(frame);Destroy(go);
        }
    }
}
