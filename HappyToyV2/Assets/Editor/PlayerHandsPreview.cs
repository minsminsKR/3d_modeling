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
        // Opt-in frame sequence of the shipping presenter; no Play mode, world
        // simulation, replacement poses, prefab edits, or animation retiming.
        public static void RenderActions()
        {
            const int fps = 25;
            const float delta = 1f / fps;
            const int actionFrame = 15;
            string output = Path.GetFullPath(Path.Combine(Application.dataPath,"../../game/verification/ritual-items/hand-action-gifs"));
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i + 1 < args.Length; i++)
                if (args[i] == "-player-hands-preview-output") output = Path.GetFullPath(args[i + 1]);
            Directory.CreateDirectory(output);
            string repository = Path.GetFullPath(Path.Combine(Application.dataPath,"../.."));
            string commit;
            var git = new System.Diagnostics.ProcessStartInfo("git","rev-parse HEAD") {
                WorkingDirectory = repository, UseShellExecute = false,
                RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true
            };
            using (var process = System.Diagnostics.Process.Start(git))
            {
                commit = process.StandardOutput.ReadToEnd().Trim();
                process.WaitForExit();
                if (process.ExitCode != 0 || commit.Length != 40) throw new InvalidOperationException("Cannot identify preview source commit");
            }
            string Quote(string value) => "\"" + value.Replace("\\","\\\\").Replace("\"","\\\"").Replace("\r","\\r").Replace("\n","\\n") + "\"";
            string Hash(string path)
            {
                using (var hash = System.Security.Cryptography.SHA256.Create())
                using (var stream = File.OpenRead(path))
                    return BitConverter.ToString(hash.ComputeHash(stream)).Replace("-","").ToLowerInvariant();
            }
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            var type = typeof(FirstPersonHands);
            var draw = type.GetMethod("DrawPose",flags);
            var ageField = type.GetField("age",flags);
            var actionField = type.GetField("action",flags);
            var posedField = type.GetField("posed",flags);
            if (draw == null || ageField == null || actionField == null || posedField == null)
                throw new InvalidOperationException("Shipping hand presentation contract changed; review the explicit preview driver");
            var clips = new[] {
                new { id="snapper", item=PlayerItem.Snapper, seconds=2.4f, walking=false, talisman=false },
                new { id="trap", item=PlayerItem.Trap, seconds=2.4f, walking=false, talisman=false },
                new { id="incense", item=PlayerItem.Incense, seconds=2.4f, walking=false, talisman=false },
                new { id="flashlight", item=PlayerItem.Flashlight, seconds=3f, walking=true, talisman=false },
                new { id="talisman", item=PlayerItem.Talisman, seconds=11.2f, walking=false, talisman=true }
            };
            var invariant = System.Globalization.CultureInfo.InvariantCulture;
            var manifest = new System.Text.StringBuilder();
            manifest.Append("{\n  \"createdUtc\": ").Append(Quote(DateTime.UtcNow.ToString("O",invariant)));
            manifest.Append(",\n  \"sourceCommit\": ").Append(Quote(commit));
            manifest.Append(",\n  \"unityVersion\": ").Append(Quote(Application.unityVersion));
            manifest.Append(",\n  \"graphicsDevice\": ").Append(Quote(SystemInfo.graphicsDeviceType.ToString()));
            manifest.Append(",\n  \"presentationSource\": \"HappyToyV2/Assets/Scripts/FirstPersonHands.cs\"");
            manifest.Append(",\n  \"presentationSha256\": ").Append(Quote(Hash(Path.Combine(Application.dataPath,"Scripts/FirstPersonHands.cs"))));
            manifest.Append(",\n  \"helperSha256\": ").Append(Quote(Hash(Path.Combine(Application.dataPath,"Editor/PlayerHandsPreview.cs"))));
            manifest.Append(",\n  \"editorOnlyPreview\": true,\n  \"nativeGameplayCapture\": false,\n  \"silent\": true");
            manifest.Append(",\n  \"fps\": 25,\n  \"timeStepSeconds\": 0.04,\n  \"playbackRate\": 1,\n  \"width\": 1280,\n  \"height\": 720,\n  \"transparentBackground\": true");
            manifest.Append(",\n  \"driver\": \"FirstPersonHands.DrawPose invoked with continuous age/action/posed and the actual 1/25 gameplay time step; original prefabs/materials and action method\"");
            manifest.Append(",\n  \"scope\": \"Hands and held items only; no world projectile, placed trap, incense guidance, monster, or player execution\"");
            manifest.Append(",\n  \"clips\": [\n");
            float previousMonochrome = Shader.GetGlobalFloat("_RitualMonochrome");
            float previousVolume = AudioListener.volume;
            bool previousPause = AudioListener.pause;
            var previousTarget = RenderTexture.active;
            GameObject root = null;
            Texture2D pixels = null;
            try
            {
                AudioListener.volume = 0;
                AudioListener.pause = true;
                Shader.SetGlobalFloat("_RitualMonochrome",0);
                for (int clipIndex = 0; clipIndex < clips.Length; clipIndex++)
                {
                    var clip = clips[clipIndex];
                    string directory = Path.Combine(output,"frames",clip.id);
                    Directory.CreateDirectory(directory);
                    root = new GameObject("Opt-in hand action " + clip.id);
                    var hands = root.AddComponent<FirstPersonHands>();
                    // Initial setup only. Subsequent frames keep the same rig,
                    // material ownership, pose interpolation, and private state.
                    var camera = hands.PrepareEditorPreview(clip.item,-1);
                    camera.enabled = false;
                    ageField.SetValue(hands,0f);
                    actionField.SetValue(hands,0f);
                    if (!(bool)posedField.GetValue(hands)) throw new InvalidOperationException("Hand preview did not initialise its pose");
                    pixels = new Texture2D(hands.Output.width,hands.Output.height,TextureFormat.RGBA32,false);
                    int frameCount = Mathf.RoundToInt(clip.seconds * fps);
                    int burnFrames = 10 * fps;
                    for (int frame = 0; frame < frameCount; frame++)
                    {
                        if (!clip.walking && frame == actionFrame) hands.PlayAction(clip.item);
                        ageField.SetValue(hands,(float)ageField.GetValue(hands) + delta);
                        actionField.SetValue(hands,Mathf.Max(0,(float)actionField.GetValue(hands) - delta));
                        bool burning = clip.talisman && frame >= actionFrame && frame < actionFrame + burnFrames;
                        bool available = !clip.talisman || frame < actionFrame + burnFrames;
                        float burned = burning ? (frame - actionFrame) / (float)burnFrames : 0;
                        Shader.SetGlobalFloat("_RitualMonochrome",burning ? 1 : 0);
                        draw.Invoke(hands,new object[] { clip.item, burning, available, burned, delta, clip.walking, false, false });
                        RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest { destination=hands.Output });
                        RenderTexture.active = hands.Output;
                        pixels.ReadPixels(new Rect(0,0,pixels.width,pixels.height),0,0); pixels.Apply();
                        File.WriteAllBytes(Path.Combine(directory,"frame-"+frame.ToString("0000",invariant)+".png"),pixels.EncodeToPNG());
                        RenderTexture.active = previousTarget;
                        if (frame % fps == 0) Debug.Log("Hand action preview "+clip.id+": "+frame+" / "+frameCount);
                    }
                    if (clipIndex > 0) manifest.Append(",\n");
                    manifest.Append("    { \"id\": ").Append(Quote(clip.id));
                    manifest.Append(", \"item\": ").Append(Quote(clip.item.ToString()));
                    manifest.Append(", \"frames\": ").Append(Quote("frames/"+clip.id+"/frame-{0:0000}.png"));
                    manifest.Append(", \"frameCount\": ").Append(frameCount);
                    manifest.Append(", \"fps\": 25, \"durationSeconds\": ").Append(clip.seconds.ToString("0.0",invariant));
                    manifest.Append(", \"actionStartSeconds\": ").Append(clip.walking ? "null" : "0.6");
                    manifest.Append(", \"actionDurationSeconds\": ").Append(clip.walking ? "null" : "0.55");
                    manifest.Append(", \"walkingSway\": ").Append(clip.walking ? "true" : "false");
                    if (clip.talisman) manifest.Append(", \"burnStartFrame\": 15, \"burnFrameCount\": 250, \"burnDurationSeconds\": 10, \"finishedStartFrame\": 265, \"finishedAvailable\": false");
                    manifest.Append(" }");
                    UnityEngine.Object.DestroyImmediate(pixels); pixels = null;
                    UnityEngine.Object.DestroyImmediate(root); root = null;
                    Shader.SetGlobalFloat("_RitualMonochrome",0);
                }
                manifest.Append("\n  ]\n}\n");
                File.WriteAllText(Path.Combine(output,"manifest.json"),manifest.ToString());
                Debug.Log("Hand action preview complete: "+Path.Combine(output,"manifest.json"));
            }
            finally
            {
                RenderTexture.active = previousTarget;
                if (pixels) UnityEngine.Object.DestroyImmediate(pixels);
                if (root) UnityEngine.Object.DestroyImmediate(root);
                Shader.SetGlobalFloat("_RitualMonochrome",previousMonochrome);
                AudioListener.pause = previousPause;
                AudioListener.volume = previousVolume;
            }
        }
    }
}





