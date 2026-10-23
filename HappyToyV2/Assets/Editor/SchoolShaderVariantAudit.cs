using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

namespace HappyToy.V2.Editor
{
    /// <summary>Opt-in evidence of the school surface variants reaching the player compiler.</summary>
    public sealed class SchoolShaderVariantAudit : IPreprocessBuildWithReport, IPostprocessBuildWithReport, IPreprocessShaders
    {
        const string Variable="HAPPYTOY_SCHOOL_SHADER_AUDIT_DIR";
        [Serializable] sealed class Batch
        {
            public string platform,stage;
            public int retained,clusterOn,clusterOff;
        }
        [Serializable] sealed class Evidence
        {
            public string status="BUILD_STARTED",createdUtc,updatedUtc,target,result;
            public string shader="HappyToy/SchoolDecaySurface",pass="SchoolForward";
            public string scope="Final observed shader preprocess batches only. Missing callbacks may mean cached shader compilation; absence is not proof of stripped variants. Does not change variants, shaders or cache.";
            public int callbackOrder=int.MaxValue,observedPassCallbacks;
            public Batch[] batches=Array.Empty<Batch>();
        }
        static Evidence evidence;
        static string destination;
        static readonly List<Batch> batches=new List<Batch>();
        public int callbackOrder=>int.MaxValue;
        public void OnPreprocessBuild(BuildReport report)
        {
            destination=null;evidence=null;batches.Clear();
            string requested=Environment.GetEnvironmentVariable(Variable);
            if(string.IsNullOrWhiteSpace(requested))return;
            string builds=Path.GetFullPath(Path.Combine(Application.dataPath,"../Builds"));
            string folder=Path.GetFullPath(requested);
            if(!folder.StartsWith(builds+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))
                throw new BuildFailedException(Variable+" must point inside this project's Builds directory");
            Directory.CreateDirectory(folder);destination=Path.Combine(folder,"school-shader-variants.json");
            evidence=new Evidence{createdUtc=DateTime.UtcNow.ToString("o"),target=report.summary.platform.ToString()};
            Write();
        }
        public void OnProcessShader(Shader shader,ShaderSnippetData snippet,IList<ShaderCompilerData> data)
        {
            if(evidence==null||shader.name!=evidence.shader||snippet.passName!=evidence.pass)return;
            evidence.observedPassCallbacks++;
            var keyword=new ShaderKeyword(shader,"_CLUSTER_LIGHT_LOOP");
            foreach(var platform in data.GroupBy(item=>item.shaderCompilerPlatform))
            {
                int retained=platform.Count();
                int cluster=platform.Count(item=>item.shaderKeywordSet.IsEnabled(keyword));
                batches.Add(new Batch{platform=platform.Key.ToString(),stage=snippet.shaderType.ToString(),
                    retained=retained,clusterOn=cluster,clusterOff=retained-cluster});
            }
            if(data.Count==0)batches.Add(new Batch{platform="No retained variants",stage=snippet.shaderType.ToString()});
            evidence.status="OBSERVED";Write();
        }
        public void OnPostprocessBuild(BuildReport report)
        {
            if(evidence==null)return;
            evidence.result=report.summary.result.ToString();
            evidence.status=evidence.observedPassCallbacks==0?"NOT_OBSERVED_CACHE_OR_NO_CALLBACK":"OBSERVED";
            Write();destination=null;evidence=null;batches.Clear();
        }
        static void Write()
        {
            evidence.updatedUtc=DateTime.UtcNow.ToString("o");evidence.batches=batches.ToArray();
            File.WriteAllText(destination,JsonUtility.ToJson(evidence,true));
        }
    }
}