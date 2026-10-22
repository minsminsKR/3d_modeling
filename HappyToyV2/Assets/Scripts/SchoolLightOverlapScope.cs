using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace HappyToy.V2
{
    /// <summary>Own the school's clustered pipeline, including reflection and hand cameras.</summary>
    public sealed class SchoolLightOverlapScope : IDisposable
    {
        public const string ResourcePath="SchoolRendering/SchoolPipeline";
        readonly UniversalRenderPipelineAsset pipeline;
        readonly RenderPipelineAsset previous;
        public bool Applied {get;private set;}
        public SchoolLightOverlapScope(UniversalRenderPipelineAsset source)
        {
            if(!source)throw new ArgumentNullException(nameof(source));
            previous=QualitySettings.renderPipeline;
            pipeline=Resources.Load<UniversalRenderPipelineAsset>(ResourcePath);
            if(!pipeline)throw new InvalidOperationException("School Forward+ pipeline is missing; run GraphicsLightingSetup");
            Apply();
        }
        public void Apply()
        {
            if(Applied||!pipeline)return;
            // Clustered lighting evaluates all lights affecting visible surfaces,
            // including emitters outside the view. Do not gate lights on Renderer.isVisible.
            QualitySettings.renderPipeline=pipeline;Applied=true;
        }
        public void Dispose()
        {
            if(!Applied)return;
            if(QualitySettings.renderPipeline==pipeline)QualitySettings.renderPipeline=previous;
            Applied=false;
        }
    }
}
