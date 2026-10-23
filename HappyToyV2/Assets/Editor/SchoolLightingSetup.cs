using System;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace HappyToy.V2.Editor
{
    /// <summary>Reproducible school-only Forward+ assets and player shader retention.</summary>
    public static class SchoolLightingSetup
    {
        public const string PipelinePath="Assets/Resources/SchoolRendering/SchoolPipeline.asset";
        public const string RendererPath="Assets/Resources/SchoolRendering/SchoolRenderer.asset";
        public const string ShaderQualityName="School runtime shader variants";
        public static void Ensure(UniversalRenderPipelineAsset template,UniversalRendererData sourceRenderer)
        {
            const string folder="Assets/Resources/SchoolRendering";
            if(!AssetDatabase.IsValidFolder(folder))AssetDatabase.CreateFolder("Assets/Resources","SchoolRendering");
            CopyMissing(AssetDatabase.GetAssetPath(template),PipelinePath);
            CopyMissing(AssetDatabase.GetAssetPath(sourceRenderer),RendererPath);
            var pipeline=AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
            var renderer=AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
            var ao=AssetDatabase.LoadAllAssetsAtPath(RendererPath).OfType<ScreenSpaceAmbientOcclusion>().Single();
            var sourceAo=sourceRenderer.rendererFeatures.OfType<ScreenSpaceAmbientOcclusion>().Single();
            // Keep independent AO pass lifetimes and stable GUIDs on repeated setup.
            EditorUtility.CopySerialized(template,pipeline);pipeline.name="SchoolPipeline";
            EditorUtility.CopySerialized(sourceRenderer,renderer);renderer.name="SchoolRenderer";
            EditorUtility.CopySerialized(sourceAo,ao);ao.name="School clustered contact AO";
            renderer.rendererFeatures.Clear();renderer.rendererFeatures.Add(ao);
            var rendererData=new SerializedObject(renderer);
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(ao,out string guid,out long localId);
            rendererData.FindProperty("m_RendererFeatureMap").GetArrayElementAtIndex(0).longValue=localId;
            rendererData.ApplyModifiedPropertiesWithoutUndo();
            renderer.renderingMode=RenderingMode.ForwardPlus;
            var data=new SerializedObject(pipeline);
            var list=data.FindProperty("m_RendererDataList");list.arraySize=1;list.GetArrayElementAtIndex(0).objectReferenceValue=renderer;
            data.FindProperty("m_DefaultRendererIndex").intValue=0;data.ApplyModifiedPropertiesWithoutUndo();
            renderer.SetDirty();EditorUtility.SetDirty(renderer);EditorUtility.SetDirty(ao);EditorUtility.SetDirty(pipeline);
            // Resources inclusion alone does not retain Forward+ shader variants.
            // Use the pinned Core/URP build collector's supported RP asset label.
            var included=GraphicsSettings.GetRenderPipelineSettings<IncludeAdditionalRPAssets>();
            if(included==null)throw new InvalidOperationException("URP additional-pipeline build settings missing");
            if(string.IsNullOrWhiteSpace(included.labelToInclude))included.labelToInclude="HappyToyRuntimePipeline";
            included.includeAssetsByLabel=true;
            AssetDatabase.SetLabels(pipeline,AssetDatabase.GetLabels(pipeline).Concat(new[]{included.labelToInclude}).Distinct().ToArray());
            var globals=GraphicsSettings.GetSettingsForRenderPipeline<UniversalRenderPipeline>();
            if(globals)EditorUtility.SetDirty(globals);
            EnsureNativeShaderInclusion(pipeline);
            AssetDatabase.SaveAssets();
            var nativeAssets=new List<RenderPipelineAsset>();
            QualitySettings.GetAllRenderPipelineAssetsForPlatform("Standalone",ref nativeAssets);
            if(!nativeAssets.Contains(pipeline)||!nativeAssets.Contains(template))
                throw new InvalidOperationException("Native Windows shader prefilter omitted a runtime pipeline");
            var buildAssets=new List<UniversalRenderPipelineAsset>();
            if(!BuildTarget.StandaloneWindows64.TryGetRenderPipelineAssets(buildAssets)||!buildAssets.Contains(pipeline)||!buildAssets.Contains(template))
                throw new InvalidOperationException("Windows shader collector omitted a runtime pipeline");
        }
        static void EnsureNativeShaderInclusion(UniversalRenderPipelineAsset pipeline)
        {
            // Unity 6000.6 ShaderKeywordFilterUtil reads native QualitySettings assets,
            // not the managed IncludeAdditionalRPAssets label collector. A reserved
            // appended tier retains clustered variants without replacing any existing
            // quality index, platform default or the active common Forward pipeline.
            var settings=new SerializedObject(QualitySettings.GetQualitySettings());
            var levels=settings.FindProperty("m_QualitySettings");
            if(levels==null||levels.arraySize==0)throw new InvalidOperationException("Missing authored quality levels");
            int index=-1;
            for(int i=0;i<levels.arraySize;i++)
                if(levels.GetArrayElementAtIndex(i).FindPropertyRelative("name").stringValue==ShaderQualityName)
                {
                    if(index>=0)throw new InvalidOperationException("Duplicate school shader quality tier");
                    index=i;
                }
            if(index<0)
            {
                // Duplicate the last authored tier. Its former index keeps an identical
                // copy, while the duplicate at the end becomes the reserved school tier.
                index=levels.arraySize;levels.InsertArrayElementAtIndex(index-1);
            }
            var school=levels.GetArrayElementAtIndex(index);
            school.FindPropertyRelative("name").stringValue=ShaderQualityName;
            school.FindPropertyRelative("customRenderPipeline").objectReferenceValue=pipeline;
            // The tier must participate in Standalone's native shader collection.
            school.FindPropertyRelative("excludedTargetPlatforms").arraySize=0;
            settings.ApplyModifiedPropertiesWithoutUndo();
        }
        static void CopyMissing(string source,string target)
        {
            if(!AssetDatabase.LoadMainAssetAtPath(target)&&!AssetDatabase.CopyAsset(source,target))
                throw new InvalidOperationException("Could not create school rendering asset: "+target);
        }
    }
}
