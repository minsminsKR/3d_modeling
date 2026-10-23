using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using static HappyToy.V2.CloudTests.RuntimeAccess;

namespace HappyToy.V2.CloudTests
{
    public sealed class GraphicsLightingRulesTests
    {
        Array Candidates(params (int id,bool point,float distance)[] values)
        {
            var type=RequireType("ShadowAtlasPlan+Candidate");var result=Array.CreateInstance(type,values.Length);
            for(int i=0;i<values.Length;i++)
            {var item=Activator.CreateInstance(type);type.GetField("id").SetValue(item,values[i].id);type.GetField("point").SetValue(item,values[i].point);type.GetField("distanceSquared").SetValue(item,values[i].distance);result.SetValue(item,i);}
            return result;
        }
        [Test]
        public void AtlasAccountsForEveryPointFaceAndPreservesTheTorchBeforeLocalSpots()
        {
            var rules=RequireType("ShadowAtlasPlan");
            var plan=Call(rules,"Build",2048,1024,512,true,Candidates((1,true,1),(2,true,2),(3,false,3),(4,false,4)));
            Assert.That(Get<long>(plan,"pixels"),Is.EqualTo(2048L*2048));
            Assert.That(Get<int>(plan,"pointCasters"),Is.EqualTo(2));Assert.That(Get<int>(plan,"spotCasters"),Is.Zero);
            Assert.That(Get<int>(plan,"faces"),Is.EqualTo(13));
            var ids=((IEnumerable)Get<object>(plan,"selected")).Cast<int>().ToArray();Assert.That(ids,Is.EqualTo(new[]{1,2}));
        }
        [Test]
        public void FewerPointsUseAvailableTilesForTheActualNearestSpots()
        {
            var rules=RequireType("ShadowAtlasPlan");
            var plan=Call(rules,"Build",2048,1024,512,true,Candidates((1,true,1),(2,false,2),(3,false,3),(4,false,4),(5,false,5),(6,false,6),(7,false,7),(8,false,8)));
            Assert.That(Get<long>(plan,"pixels"),Is.EqualTo(2048L*2048));Assert.That(Get<int>(plan,"spotCasters"),Is.EqualTo(6));
            Assert.That(((IEnumerable)Get<object>(plan,"selected")).Cast<int>().ToArray(),Is.EqualTo(new[]{1,2,3,4,5,6,7}));
        }
        static string Hash(string path)
        {using(var hash=SHA256.Create())return BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(path))).Replace("-","");}
        [Test]
        public void OwnedPipelineRetainsSsaoHdrReflectionAndIdentityWithoutEditingSourceAssets()
        {
            string[] originals={"Assets/Generated/SchoolPipeline 17.asset","Assets/Generated/SchoolRenderer 17.asset","Assets/Annex/SchoolAnnex.unity"};
            var hashes=originals.Select(Hash).ToArray();var setup=RequireType("Editor.GraphicsLightingSetup","Assembly-CSharp-Editor");
            Call(setup,"EnsureAndActivate");
            const string pipelinePath="Assets/GraphicsUpgrade/Settings/GraphicsPipeline.asset",rendererPath="Assets/GraphicsUpgrade/Settings/GraphicsRenderer.asset";
            var pipeline=AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(pipelinePath);var renderer=AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(rendererPath);
            Assert.That(pipeline,Is.Not.Null);Assert.That(renderer,Is.Not.Null);
            Assert.That(Get<bool>(pipeline,"supportsHDR"),Is.True);Assert.That(Get<bool>(pipeline,"supportsCameraDepthTexture"),Is.True);
            Assert.That(Get<bool>(pipeline,"reflectionProbeBlending"),Is.True);Assert.That(Get<bool>(pipeline,"reflectionProbeBoxProjection"),Is.True);
            Assert.That(Get<int>(pipeline,"additionalLightsShadowmapResolution"),Is.EqualTo(2048));
            Assert.That(Get<int>(pipeline,"additionalLightsShadowResolutionTierHigh"),Is.EqualTo(1024));
            Assert.That(Get<int>(pipeline,"additionalLightsShadowResolutionTierMedium"),Is.EqualTo(512));
            Assert.That(Get<UnityEngine.Object>(renderer,"postProcessData"),Is.Not.Null,"Camera flag alone cannot enable the null-resource authored post passes");
            var features=((IEnumerable)Get<object>(renderer,"rendererFeatures")).Cast<UnityEngine.Object>().ToArray();
            Assert.That(features.Length,Is.EqualTo(1));Assert.That(features[0].GetType().Name,Is.EqualTo("ScreenSpaceAmbientOcclusion"));Assert.That(Get<bool>(features[0],"isActive"),Is.True);
            var settings=new SerializedObject(features[0]).FindProperty("m_Settings");
            var source=settings.FindPropertyRelative("Source");
            Assert.That(source.enumNames[source.enumValueIndex],Is.EqualTo("Depth"));
            Assert.That(settings.FindPropertyRelative("AfterOpaque").boolValue,Is.False,"Emission must not be multiplied by a post-lighting AO overlay");
            Assert.That(settings.FindPropertyRelative("Downsample").boolValue,Is.True);
            var normals=settings.FindPropertyRelative("NormalSamples");
            Assert.That(normals.enumNames[normals.enumValueIndex],Is.EqualTo("Low"));
            Assert.That(Get<int>(pipeline,"msaaSampleCount"),Is.EqualTo(4));
            string pipelineGuid=AssetDatabase.AssetPathToGUID(pipelinePath),rendererGuid=AssetDatabase.AssetPathToGUID(rendererPath);
            Call(setup,"EnsureAndActivate");
            Assert.That(AssetDatabase.AssetPathToGUID(pipelinePath),Is.EqualTo(pipelineGuid));Assert.That(AssetDatabase.AssetPathToGUID(rendererPath),Is.EqualTo(rendererGuid));
            Assert.That(originals.Select(Hash).ToArray(),Is.EqualTo(hashes));
        }
        [Test]
        public void SchoolClusteredPipelineRetainsIndependentAoAndBuildVariantsWithoutChangingDefaultForward()
        {
            Call(RequireType("Editor.GraphicsLightingSetup","Assembly-CSharp-Editor"),"EnsureAndActivate");
            const string path="Assets/Resources/SchoolRendering/SchoolPipeline.asset",rendererPath="Assets/Resources/SchoolRendering/SchoolRenderer.asset";
            var pipeline=AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
            var renderer=AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(rendererPath);
            var standard=AssetDatabase.LoadAssetAtPath<UnityEngine.Object>("Assets/GraphicsUpgrade/Settings/GraphicsRenderer.asset");
            Assert.That(Get<object>(renderer,"renderingMode").ToString(),Is.EqualTo("ForwardPlus"));
            Assert.That(Get<object>(standard,"renderingMode").ToString(),Is.EqualTo("Forward"));
            Assert.That(UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline,Is.Not.SameAs(pipeline));
            Assert.That(AssetDatabase.GetLabels(pipeline),Is.Not.Empty,"Player shader stripping must see the runtime pipeline");
            var ao=((IEnumerable)Get<object>(renderer,"rendererFeatures")).Cast<UnityEngine.Object>().Single();
            var standardAo=((IEnumerable)Get<object>(standard,"rendererFeatures")).Cast<UnityEngine.Object>().Single();
            Assert.That(ao,Is.Not.SameAs(standardAo));
            Assert.That(AssetDatabase.GetAssetPath(ao),Is.EqualTo(rendererPath));
            string pipelineGuid=AssetDatabase.AssetPathToGUID(path),rendererGuid=AssetDatabase.AssetPathToGUID(rendererPath);
            Call(RequireType("Editor.GraphicsLightingSetup","Assembly-CSharp-Editor"),"EnsureAndActivate");
            Assert.That(AssetDatabase.AssetPathToGUID(path),Is.EqualTo(pipelineGuid));
            Assert.That(AssetDatabase.AssetPathToGUID(rendererPath),Is.EqualTo(rendererGuid));
        }
        [Test]
        public void SchoolNativeShaderPrefilterIncludesBothPipelinesWithoutChangingExistingQualityChoices()
        {
            var originalNames=QualitySettings.names.ToArray();
            var originalPipelines=Enumerable.Range(0,originalNames.Length).Select(QualitySettings.GetRenderPipelineAssetAt).ToArray();
            int active=QualitySettings.GetQualityLevel();
            var getDefault=typeof(QualitySettings).GetMethod("GetDefaultQualityForPlatform",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic);
            Assert.That(getDefault,Is.Not.Null);
            int platformDefault=(int)getDefault.Invoke(null,new object[]{"Standalone"});
            var setup=RequireType("Editor.GraphicsLightingSetup","Assembly-CSharp-Editor");
            Call(setup,"EnsureAndActivate");
            const string reserved="School runtime shader variants";
            var names=QualitySettings.names;
            Assert.That(names.Take(originalNames.Length),Is.EqualTo(originalNames),"Existing quality indices must stay stable");
            for(int i=0;i<originalPipelines.Length;i++)
                Assert.That(QualitySettings.GetRenderPipelineAssetAt(i),Is.SameAs(originalPipelines[i]),"Existing quality pipeline changed at "+i);
            Assert.That(QualitySettings.GetQualityLevel(),Is.EqualTo(active));
            Assert.That((int)getDefault.Invoke(null,new object[]{"Standalone"}),Is.EqualTo(platformDefault));
            Assert.That(names.Count(name=>name==reserved),Is.EqualTo(1));
            int schoolIndex=Array.IndexOf(names,reserved);
            Assert.That(QualitySettings.GetActiveQualityLevelsForPlatform("Standalone"),Does.Contain(schoolIndex));
            var school=AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.RenderPipelineAsset>("Assets/Resources/SchoolRendering/SchoolPipeline.asset");
            Assert.That(QualitySettings.GetRenderPipelineAssetAt(schoolIndex),Is.SameAs(school));
            var nativeAssets=new System.Collections.Generic.List<UnityEngine.Rendering.RenderPipelineAsset>();
            QualitySettings.GetAllRenderPipelineAssetsForPlatform("Standalone",ref nativeAssets);
            Assert.That(nativeAssets,Does.Contain(school),"Labels alone do not feed the engine's native prefilter");
            Assert.That(nativeAssets,Does.Contain(UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline));
            Call(setup,"EnsureAndActivate");
            Assert.That(QualitySettings.names,Is.EqualTo(names),"Repeated setup must not append another reserved tier");
            Assert.That(QualitySettings.GetQualityLevel(),Is.EqualTo(active));
            Assert.That((int)getDefault.Invoke(null,new object[]{"Standalone"}),Is.EqualTo(platformDefault));
        }
        [Test]
        public void ReusedShadowPlanClearsOldSelectionsAndPreservesDistanceTiesAndTorchPriority()
        {
            var rules=RequireType("ShadowAtlasPlan");var type=RequireType("ShadowAtlasPlan+Candidate");
            var list=(IList)Activator.CreateInstance(typeof(System.Collections.Generic.List<>).MakeGenericType(type));
            foreach(var candidate in Candidates((3,false,2),(2,true,1),(1,false,1),(4,true,3)))list.Add(candidate);
            var plan=Activator.CreateInstance(rules);var original=Get<object>(plan,"selected");
            for(int frame=0;frame<4;frame++)
            {
                bool torch=frame%2==0;Call(plan,"Rebuild",2048,1024,512,torch,list);
                Assert.That(Get<object>(plan,"selected"),Is.SameAs(original));
                var ids=((IEnumerable)original).Cast<int>().ToArray();
                Assert.That(ids,Is.EqualTo(torch?new[]{1,2,3}:new[]{1,2,3,4}));
                Assert.That(Get<int>(plan,"faces"),Is.EqualTo(torch?9:14));
            }
            list.Clear();Call(plan,"Rebuild",2048,1024,512,false,list);
            Assert.That(((IEnumerable)original).Cast<int>(),Is.Empty);
            Assert.That(Get<long>(plan,"pixels"),Is.Zero);Assert.That(Get<int>(plan,"faces"),Is.Zero);
        }
    }
}
