using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;

namespace HappyToy.V2.Editor
{
    public static class AnnexDevelopmentBuild
    {
        public static void Schedule()
        {
            if(EditorApplication.isPlaying||EditorApplication.isCompiling)throw new InvalidOperationException("Stop play and finish compilation first");
            Directory.CreateDirectory("Verification/annex");
            File.WriteAllText("Verification/annex/windows-build.json","{\"status\":\"running\"}");
            EditorApplication.delayCall+=Build;
        }
        static void Build()
        {
            try
            {
                ShellAssetBuilder.Ensure();Directory.CreateDirectory("Builds/Windows");
                var report=BuildPipeline.BuildPlayer(new[]{AnnexSchoolBuilder.ScenePath},"Builds/Windows/HappyToyV2.exe",BuildTarget.StandaloneWindows64,BuildOptions.Development);
                File.WriteAllText("Verification/annex/windows-build.json",Newtonsoft.Json.JsonConvert.SerializeObject(new{status=report.summary.result.ToString(),scene=AnnexSchoolBuilder.ScenePath,report.summary.totalErrors,report.summary.totalWarnings,report.summary.totalSize,seconds=report.summary.totalTime.TotalSeconds,finishedUtc=DateTime.UtcNow},Newtonsoft.Json.Formatting.Indented));
            }
            catch(Exception error)
            {
                File.WriteAllText("Verification/annex/windows-build.json",Newtonsoft.Json.JsonConvert.SerializeObject(new{status="Exception",error=error.ToString()}));
                UnityEngine.Debug.LogException(error);
            }
        }
    }
}
