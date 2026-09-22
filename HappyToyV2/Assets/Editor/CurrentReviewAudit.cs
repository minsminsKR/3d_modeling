using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace HappyToy.V2.Editor
{
    public static class CurrentReviewAudit
    {
        [Serializable] class Entry {public string path,guid,sha256;}
        [Serializable] class Report {public string scene,unityVersion;public Entry[] files;public string[] missingMetadata;}
        public static void Run()
        {
            var scenes=EditorBuildSettings.scenes.Where(s=>s.enabled).ToArray();
            if(scenes.Length!=1)throw new InvalidOperationException("Expected one selected review scene");
            var all=AssetDatabase.GetAllAssetPaths().Where(p=>p.StartsWith("Assets/")).ToArray();
            var seeds=all.Where(p=>p.Contains("/Resources/")||p.EndsWith(".cs")).Append(scenes[0].path)
                .Concat(new[]{AssetDatabase.GetAssetPath(GraphicsSettings.defaultRenderPipeline),AssetDatabase.GetAssetPath(QualitySettings.renderPipeline)})
                .Where(p=>!string.IsNullOrEmpty(p)&&File.Exists(p)).Distinct().ToArray();
            var paths=AssetDatabase.GetDependencies(seeds,true).Where(p=>p.StartsWith("Assets/")&&File.Exists(p)).OrderBy(p=>p).ToArray();
            var report=new Report{scene=scenes[0].path,unityVersion=Application.unityVersion,
                missingMetadata=all.Where(p=>!File.Exists(p+".meta")).ToArray(),
                files=paths.Select(p=>new Entry{path=p,guid=AssetDatabase.AssetPathToGUID(p),sha256=Hash(p)}).ToArray()};
            Directory.CreateDirectory("Verification/current-review");
            File.WriteAllText("Verification/current-review/dependencies.json",JsonUtility.ToJson(report,true));
            if(report.missingMetadata.Length>0)throw new InvalidOperationException("Asset metadata missing; see dependencies.json");
            EditorSceneManager.OpenScene(scenes[0].path);
            ClassroomReview.InspectAndCapture("Verification/current-review");
            Debug.Log("CURRENT_REVIEW_AUDIT_READY "+report.scene+" files="+paths.Length);
        }
        static string Hash(string path)
        {using(var sha=SHA256.Create())using(var stream=File.OpenRead(path))return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","").ToLowerInvariant();}
    }
}
