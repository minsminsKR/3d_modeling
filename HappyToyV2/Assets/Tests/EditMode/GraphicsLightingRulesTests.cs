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
            string pipelineGuid=AssetDatabase.AssetPathToGUID(pipelinePath),rendererGuid=AssetDatabase.AssetPathToGUID(rendererPath);
            Call(setup,"EnsureAndActivate");
            Assert.That(AssetDatabase.AssetPathToGUID(pipelinePath),Is.EqualTo(pipelineGuid));Assert.That(AssetDatabase.AssetPathToGUID(rendererPath),Is.EqualTo(rendererGuid));
            Assert.That(originals.Select(Hash).ToArray(),Is.EqualTo(hashes));
        }
    }
}
