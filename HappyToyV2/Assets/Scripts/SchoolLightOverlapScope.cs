using System;
using UnityEngine.Rendering.Universal;

namespace HappyToy.V2
{
    /// <summary>Borrow only URP's per-object light count while the dense school is active.</summary>
    public sealed class SchoolLightOverlapScope : IDisposable
    {
        public const int MaximumLightsPerObject=8;
        readonly UniversalRenderPipelineAsset pipeline;
        readonly int previous;
        public bool Applied {get;private set;}
        public SchoolLightOverlapScope(UniversalRenderPipelineAsset source)
        {
            pipeline=source?source:throw new ArgumentNullException(nameof(source));
            previous=pipeline.maxAdditionalLightsCount;
            Apply();
        }
        public void Apply()
        {
            if(Applied||!pipeline)return;
            // Forward's four-light object list discarded diffuser/window spill when
            // its camera-sorted shortlist changed. Eight stays within the pinned
            // URP Forward limit; emitters and the separate shadow atlas do not grow.
            pipeline.maxAdditionalLightsCount=MaximumLightsPerObject;Applied=true;
        }
        public void Dispose()
        {
            if(!Applied)return;
            if(pipeline)pipeline.maxAdditionalLightsCount=previous;
            Applied=false;
        }
    }
}
