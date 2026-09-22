using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace HappyToy.V2.Editor
{
    public static class ClassroomReview
    {
        public const string Baseline = "Assets/Generated/SchoolV2 17.unity";
        [Serializable] class Report
        {
            public string scene;
            public int renderers, missingScripts, missingMaterials, missingMeshes;
            public List<string> problems = new List<string>();
        }

        public static void BaselineCapture()
        {
            EditorSceneManager.OpenScene(Baseline);
            InspectAndCapture("Verification/classroom-baseline");
        }

        public static void InspectAndCapture(string output)
        {
            Directory.CreateDirectory(output);
            var scene = EditorSceneManager.GetActiveScene();
            var report = new Report { scene = scene.path };
            foreach (var root in scene.GetRootGameObjects())
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                int missing = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject);
                report.missingScripts += missing;
                if(missing > 0) report.problems.Add("Missing script: " + t.name);
                var mesh = t.GetComponent<MeshFilter>();
                if(mesh && !mesh.sharedMesh) { report.missingMeshes++; report.problems.Add("Missing mesh: " + t.name); }
                var skin = t.GetComponent<SkinnedMeshRenderer>();
                if(skin && !skin.sharedMesh) { report.missingMeshes++; report.problems.Add("Missing skin: " + t.name); }
                var renderer = t.GetComponent<Renderer>();
                if(!renderer) continue;
                report.renderers++;
                foreach(var mat in renderer.sharedMaterials)
                    if(!mat || !mat.shader) { report.missingMaterials++; report.problems.Add("Missing material: " + t.name); }
            }
            File.WriteAllText(Path.Combine(output,"scene-integrity.json"),JsonUtility.ToJson(report,true));
            var surfaces=new System.Text.StringBuilder();
            foreach(var root in scene.GetRootGameObjects())
            foreach(var renderer in root.GetComponentsInChildren<MeshRenderer>(true))
                if(renderer.name.StartsWith("CLASSROOM"))
                {
                    var mat=renderer.sharedMaterial;
                    surfaces.AppendLine(renderer.name+" | "+AssetDatabase.GetAssetPath(mat)+" | texture="+(mat?AssetDatabase.GetAssetPath(mat.mainTexture):"null")+" | scale="+(mat?mat.mainTextureScale.ToString():"null"));
                }
            File.WriteAllText(Path.Combine(output,"surfaces.txt"),surfaces.ToString());
            if(report.missingScripts + report.missingMeshes + report.missingMaterials > 0)
                throw new InvalidOperationException("Scene integrity failed. See " + output);
            Capture(output,"entry",new Vector3(-4.5f,1.6f,2.15f),new Vector3(-4.5f,1.2f,7.7f));
            Capture(output,"front-reverse",new Vector3(-3f,1.6f,7.9f),new Vector3(-5f,1.1f,3f));
            Capture(output,"teacher",new Vector3(-5.5f,1.6f,6.5f),new Vector3(-7f,.8f,8.1f));
            Capture(output,"washroom",new Vector3(-4.5f,1.6f,-2.1f),new Vector3(-4.4f,1.0f,-5.5f));
            Capture(output,"infirmary",new Vector3(5.5f,1.6f,2.0f),new Vector3(6,1.1f,5.7f));
            // Repeat the entry after the other views so first-request texture initialization is not the sole reference.
            Capture(output,"entry-warmed",new Vector3(-4.5f,1.6f,2.15f),new Vector3(-4.5f,1.2f,7.7f));
            Debug.Log("CLASSROOM_REVIEW_READY " + output + " renderers=" + report.renderers);
        }

        static void Capture(string output,string name,Vector3 position,Vector3 lookAt)
        {
            var go=new GameObject("Temporary review camera");
            var camera=go.AddComponent<Camera>();
            var source=UnityEngine.Object.FindFirstObjectByType<PlayerMotor>().eyes;
            camera.CopyFrom(source); camera.enabled=false; camera.fieldOfView=72;
            camera.GetUniversalAdditionalCameraData().renderPostProcessing=true;
            camera.transform.position=position;camera.transform.LookAt(lookAt);
            var target=new RenderTexture(1280,720,24);target.Create();
            var previous=RenderTexture.active;
            var frame=new Texture2D(1280,720,TextureFormat.RGB24,false);
            try
            {
                RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest { destination=target });
                RenderTexture.active=target;frame.ReadPixels(new Rect(0,0,1280,720),0,0);frame.Apply();
                File.WriteAllBytes(Path.Combine(output,name+".png"),frame.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active=previous;target.Release();
                UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(frame);UnityEngine.Object.DestroyImmediate(go);
            }
        }
    }
}
