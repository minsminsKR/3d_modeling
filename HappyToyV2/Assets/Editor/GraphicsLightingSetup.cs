using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace HappyToy.V2.Editor
{
    // Editor-generated owned assets; never serialize scene or mutate source URP assets.
    public static class GraphicsLightingSetup
    {
        public const string SourcePipeline="Assets/Generated/SchoolPipeline 17.asset";
        public const string SourceRenderer="Assets/Generated/SchoolRenderer 17.asset";
        public const string PipelinePath="Assets/GraphicsUpgrade/Settings/GraphicsPipeline.asset";
        public const string RendererPath="Assets/GraphicsUpgrade/Settings/GraphicsRenderer.asset";
        const string Scene="Assets/Annex/SchoolAnnex.unity";
        [Serializable] sealed class Proof
        {
            public string unity,sourcePipelineSha256,sourceRendererSha256,sourceSceneSha256,pipelineGuid,rendererGuid;
            public string scope="Owned URP17.6 pipeline/renderer copied via AssetDatabase, HDR/depth/SMAA camera support, standard conservative SSAO and reflection variants retained by active build pipeline. Original assets/scene unchanged.";
            public int shadowAtlas=2048,torchTier=1024,localTier=512,aoSamples=4;public float aoIntensity=.65f,aoRadius=.22f;
        }
        public static void EnsureAndActivate()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Generate rendering settings outside Play mode");
            string pipelineHash=Hash(SourcePipeline),rendererHash=Hash(SourceRenderer),sceneHash=Hash(Scene);
            var sourcePipeline=AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(SourcePipeline);
            var sourceRenderer=AssetDatabase.LoadAssetAtPath<UniversalRendererData>(SourceRenderer);
            if(!sourcePipeline||!sourceRenderer)throw new InvalidOperationException("Missing actual authored URP17.6 assets");
            if(sourceRenderer.rendererFeatures.Count!=0)throw new InvalidOperationException("Review newly added source renderer features before copying ownership");
            EnsureFolder("Assets/GraphicsUpgrade/Settings");
            string oldPipelineGuid=AssetDatabase.AssetPathToGUID(PipelinePath),oldRendererGuid=AssetDatabase.AssetPathToGUID(RendererPath);
            if(!AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath)&&!AssetDatabase.CopyAsset(SourcePipeline,PipelinePath))
                throw new InvalidOperationException("Could not copy the owned graphics pipeline");
            if(!AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath)&&!AssetDatabase.CopyAsset(SourceRenderer,RendererPath))
                throw new InvalidOperationException("Could not copy the owned graphics renderer");
            var pipeline=AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
            var renderer=AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
            if(!pipeline||!renderer)throw new InvalidOperationException("Owned settings path contains an incompatible asset");
            var features=AssetDatabase.LoadAllAssetsAtPath(RendererPath).OfType<ScreenSpaceAmbientOcclusion>().ToArray();
            if(features.Length>1)throw new InvalidOperationException("Duplicate owned SSAO feature");
            // Repeat setup copies serialized settings in place, preserving generated GUIDs.
            EditorUtility.CopySerialized(sourcePipeline,pipeline);pipeline.name=Path.GetFileNameWithoutExtension(PipelinePath);
            EditorUtility.CopySerialized(sourceRenderer,renderer);renderer.name=Path.GetFileNameWithoutExtension(RendererPath);
            // The authored renderer serialized postProcessData=null. UniversalRenderer
            // disables its post passes in that case even when the camera flag is true.
            renderer.postProcessData=AssetDatabase.LoadAssetAtPath<PostProcessData>("Packages/com.unity.render-pipelines.universal/Runtime/Data/PostProcessData.asset");
            if(!renderer.postProcessData)throw new InvalidOperationException("Missing actual URP17.6 post-process shader resources");
            var ao=features.SingleOrDefault();
            if(!ao)
            {
                ao=ScriptableObject.CreateInstance<ScreenSpaceAmbientOcclusion>();ao.name="Graphics standard contact AO";
                AssetDatabase.AddObjectToAsset(ao,renderer);
            }
            ao.SetActive(true);
            var aoData=new SerializedObject(ao);var settings=Require(aoData,"m_Settings");
            SetEnum(settings,"AOMethod","InterleavedGradient");SetEnum(settings,"Source","DepthNormals");
            SetEnum(settings,"Samples","Low");SetEnum(settings,"NormalSamples","Medium");SetEnum(settings,"BlurQuality","Medium");
            Require(settings,"Downsample").boolValue=true;Require(settings,"AfterOpaque").boolValue=false;
            Require(settings,"Intensity").floatValue=.65f;Require(settings,"DirectLightingStrength").floatValue=.1f;
            Require(settings,"Radius").floatValue=.22f;Require(settings,"Falloff").floatValue=30;
            var mode=settings.FindPropertyRelative("Mode");if(mode!=null)SetEnum(settings,"Mode","Standard");
            aoData.ApplyModifiedPropertiesWithoutUndo();EditorUtility.SetDirty(ao);
            renderer.rendererFeatures.Clear();renderer.rendererFeatures.Add(ao);
            var rendererData=new SerializedObject(renderer);var featureMap=Require(rendererData,"m_RendererFeatureMap");
            featureMap.arraySize=1;
            if(!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(ao,out string aoGuid,out long localId))throw new InvalidOperationException("SSAO subasset has no persistent identity");
            featureMap.GetArrayElementAtIndex(0).longValue=localId;
            rendererData.ApplyModifiedPropertiesWithoutUndo();renderer.SetDirty();EditorUtility.SetDirty(renderer);
            var data=new SerializedObject(pipeline);var renderers=Require(data,"m_RendererDataList");renderers.arraySize=1;
            renderers.GetArrayElementAtIndex(0).objectReferenceValue=renderer;Require(data,"m_DefaultRendererIndex").intValue=0;
            Require(data,"m_SupportsHDR").boolValue=true;Require(data,"m_RequireDepthTexture").boolValue=true;
            Require(data,"m_MSAA").intValue=4;Require(data,"m_ColorGradingMode").intValue=(int)ColorGradingMode.HighDynamicRange;
            Require(data,"m_ReflectionProbeBlending").boolValue=true;Require(data,"m_ReflectionProbeBoxProjection").boolValue=true;
            Require(data,"m_AdditionalLightShadowsSupported").boolValue=true;Require(data,"m_SoftShadowsSupported").boolValue=true;
            Require(data,"m_AdditionalLightsShadowmapResolution").intValue=2048;
            Require(data,"m_AdditionalLightsShadowResolutionTierLow").intValue=256;
            Require(data,"m_AdditionalLightsShadowResolutionTierMedium").intValue=512;
            Require(data,"m_AdditionalLightsShadowResolutionTierHigh").intValue=1024;
            data.ApplyModifiedPropertiesWithoutUndo();EditorUtility.SetDirty(pipeline);
            AssetDatabase.SaveAssets();
            // Register the owned settings for shader stripping and for the shipped player.
            GraphicsSettings.defaultRenderPipeline=pipeline;QualitySettings.renderPipeline=pipeline;
            AssetDatabase.SaveAssets();
            if(GraphicsSettings.defaultRenderPipeline!=pipeline||QualitySettings.renderPipeline!=pipeline)
                throw new InvalidOperationException("Owned pipeline not registered as current graphics and quality settings");
            if(!pipeline.supportsHDR||!pipeline.supportsCameraDepthTexture||!pipeline.reflectionProbeBlending||!pipeline.reflectionProbeBoxProjection ||
                pipeline.additionalLightsShadowmapResolution!=2048||pipeline.additionalLightsShadowResolutionTierMedium!=512 ||
                pipeline.additionalLightsShadowResolutionTierHigh!=1024 || !renderer.postProcessData || renderer.rendererFeatures.Count!=1||renderer.rendererFeatures[0]!=ao||!ao.isActive)
                throw new InvalidOperationException("Saved rendering settings differ from the graphics contract");
            if(Hash(SourcePipeline)!=pipelineHash||Hash(SourceRenderer)!=rendererHash||Hash(Scene)!=sceneHash)
                throw new InvalidOperationException("Original URP asset/scene changed during owned graphics setup");
            string pipelineGuid=AssetDatabase.AssetPathToGUID(PipelinePath),rendererGuid=AssetDatabase.AssetPathToGUID(RendererPath);
            if(!string.IsNullOrEmpty(oldPipelineGuid)&&oldPipelineGuid!=pipelineGuid || !string.IsNullOrEmpty(oldRendererGuid)&&oldRendererGuid!=rendererGuid)
                throw new InvalidOperationException("Repeated setup changed owned asset GUIDs");
            Directory.CreateDirectory("Verification/graphics-upgrade");
            File.WriteAllText("Verification/graphics-upgrade/lighting-setup.json",JsonUtility.ToJson(new Proof {
                unity=Application.unityVersion,sourcePipelineSha256=pipelineHash,sourceRendererSha256=rendererHash,sourceSceneSha256=sceneHash,
                pipelineGuid=pipelineGuid,rendererGuid=rendererGuid},true));
            Debug.Log("HAPPYTOY_GRAPHICS_LIGHTING_SETUP_PASS HDR/depth/SSAO/reflection settings and original hashes");
        }
        static SerializedProperty Require(SerializedObject value,string name)
        {var found=value.FindProperty(name);if(found==null)throw new InvalidOperationException("URP17.6 serialized setting missing: "+name);return found;}
        static SerializedProperty Require(SerializedProperty value,string name)
        {var found=value.FindPropertyRelative(name);if(found==null)throw new InvalidOperationException("URP17.6 nested setting missing: "+name);return found;}
        static void SetEnum(SerializedProperty root,string name,string expected)
        {var property=Require(root,name);int index=Array.IndexOf(property.enumNames,expected);if(index<0)throw new InvalidOperationException("Unexpected URP enum "+name);property.enumValueIndex=index;}
        static void EnsureFolder(string path)
        {if(AssetDatabase.IsValidFolder(path))return;string parent=path.Substring(0,path.LastIndexOf('/'));EnsureFolder(parent);AssetDatabase.CreateFolder(parent,path.Substring(path.LastIndexOf('/')+1));}
        static string Hash(string path)
        {using(var hash=SHA256.Create())return BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(path))).Replace("-","").ToLowerInvariant();}
    }
}
