using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace HappyToy.V2.Editor
{
    // Explicit still-image authoring review; never starts play mode or audio.
    public static class PlayerHandsPreview
    {
        public static void ImportAndRender() { PlayerItemsImport.Apply(); Render(); }
        public static void Render()
        {
            string output = Path.GetFullPath(Path.Combine(Application.dataPath,"../../game/verification/ritual-items/hands"));
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i + 1 < args.Length; i++)
                if (args[i] == "-player-hands-preview-output") output = Path.GetFullPath(args[i + 1]);
            Directory.CreateDirectory(output);
            float monochrome = Shader.GetGlobalFloat("_RitualMonochrome");
            var root = new GameObject("Opt-in hands still review");
            try
            {
                Shader.SetGlobalFloat("_RitualMonochrome",0);
                var hands = root.AddComponent<FirstPersonHands>();
                void Capture(string name, PlayerItem item, float burned = -1, float action = -1, float fingers = -1)
                {
                    var camera = hands.PrepareEditorPreview(item,burned,action,fingers);
                    RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest { destination = hands.Output });
                    var previous = RenderTexture.active;
                    var pixels = new Texture2D(hands.Output.width,hands.Output.height,TextureFormat.RGBA32,false);
                    try
                    {
                        RenderTexture.active = hands.Output;
                        pixels.ReadPixels(new Rect(0,0,pixels.width,pixels.height),0,0); pixels.Apply();
                        File.WriteAllBytes(Path.Combine(output,name+".png"),pixels.EncodeToPNG());
                    }
                    finally { RenderTexture.active = previous; UnityEngine.Object.DestroyImmediate(pixels); }
                }
                Capture("flashlight",PlayerItem.Flashlight);
                Capture("snapper",PlayerItem.Snapper);
                Capture("snapper-release",PlayerItem.Snapper,-1,.28f);
                Capture("incense",PlayerItem.Incense);
                Capture("talisman-whole",PlayerItem.Talisman);
                Capture("talisman-burning-top",PlayerItem.Talisman,.12f);
                Capture("talisman-burning-middle",PlayerItem.Talisman,.5f);
                Capture("talisman-burning-bottom",PlayerItem.Talisman,.88f);
                Capture("trap",PlayerItem.Trap);
                Capture("hand-open",PlayerItem.Snapper,-1,-1,0);
                Capture("hand-curl20",PlayerItem.Snapper,-1,-1,20);
                Capture("hand-curl40",PlayerItem.Snapper,-1,-1,40);
                Capture("hand-curl60",PlayerItem.Snapper,-1,-1,60);
                Capture("flashlight-repeat",PlayerItem.Flashlight);
                Debug.Log("Player hands authoring stills: "+output);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                Shader.SetGlobalFloat("_RitualMonochrome",monochrome);
            }
        }
        public static void Inspect()
        {
            string output=Path.GetFullPath(Path.Combine(Application.dataPath,"../../game/verification/ritual-items/hand-inspect"));
            Directory.CreateDirectory(output);
            var root=UnityEngine.Object.Instantiate(Resources.Load<GameObject>("PlayerItems/hand"));
            var camera=new GameObject("Hand axis inspect").AddComponent<Camera>();camera.enabled=false;
            camera.orthographic=true;camera.aspect=1;camera.nearClipPlane=.01f;camera.farClipPlane=10;
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.025f,.03f,.04f);
            camera.GetUniversalAdditionalCameraData().renderPostProcessing=false;
            var target=new RenderTexture(900,900,24);target.Create();
            var text=new System.Text.StringBuilder();
            var material=new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            GameObject display=null;Mesh baked=null;
            try
            {
                var skin=root.GetComponentInChildren<SkinnedMeshRenderer>();
                text.AppendLine("renderer "+skin.transform.localToWorldMatrix+"\nworld "+skin.bounds+"\nmesh "+skin.sharedMesh.bounds);
                foreach(var bone in skin.bones) text.AppendLine(bone.name+" position="+bone.position.ToString("F6")+" rotation="+bone.rotation.eulerAngles+" local="+bone.localPosition.ToString("F6"));
                material.SetTexture("_BaseMap",skin.sharedMaterial.GetTexture("_BaseMap"));
                skin.sharedMaterial=material;
                void Capture(string label,Bounds bounds)
                {
                    camera.orthographicSize=Mathf.Max(bounds.size.x,bounds.size.y,bounds.size.z)*.62f;
                    int index=0;
                    foreach(var offset in new[]{Vector3.back,Vector3.right,Vector3.up})
                    {
                        camera.transform.position=bounds.center+offset*2;
                        camera.transform.LookAt(bounds.center,offset==Vector3.up?Vector3.forward:Vector3.up);
                        RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest { destination=target });
                        var old=RenderTexture.active;RenderTexture.active=target;
                        var pixels=new Texture2D(900,900,TextureFormat.RGB24,false);
                        pixels.ReadPixels(new Rect(0,0,900,900),0,0);pixels.Apply();
                        File.WriteAllBytes(Path.Combine(output,label+"-"+index+".png"),pixels.EncodeToPNG());
                        UnityEngine.Object.DestroyImmediate(pixels);RenderTexture.active=old;index++;
                    }
                }
                Capture("skinned",skin.bounds);
                display=new GameObject("Raw mesh");display.transform.SetParent(skin.transform,false);
                display.AddComponent<MeshFilter>().sharedMesh=skin.sharedMesh;
                var renderer=display.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;skin.enabled=false;
                Capture("raw",renderer.bounds);text.AppendLine("raw "+renderer.bounds);
                baked=new Mesh();skin.BakeMesh(baked);display.GetComponent<MeshFilter>().sharedMesh=baked;
                Capture("baked",renderer.bounds);text.AppendLine("baked "+renderer.bounds+" mesh "+baked.bounds);
                File.WriteAllText(Path.Combine(output,"axes.txt"),text.ToString());
            }
            finally
            {
                if(display)UnityEngine.Object.DestroyImmediate(display);
                if(baked)UnityEngine.Object.DestroyImmediate(baked);
                UnityEngine.Object.DestroyImmediate(root);UnityEngine.Object.DestroyImmediate(camera.gameObject);
                target.Release();UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(material);
            }
        }
    }
}




