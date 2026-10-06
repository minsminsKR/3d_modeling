using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using static HappyToy.V2.CloudTests.RuntimeAccess;

namespace HappyToy.V2.CloudTests
{
    public sealed class NativeResourceShaderRetentionTests
    {
        static string Hash(string path)
        {using(var hash=SHA256.Create())return BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(path))).Replace("-","");}
        [Test]
        public void BuildRetainsActualRuntimeShaderKeywordsWithoutChangingTheAuthoredScene()
        {
            const string scene="Assets/Annex/SchoolAnnex.unity";string before=Hash(scene);
            EditorSceneManager.OpenScene(scene,OpenSceneMode.Single);
            var helper=RequireType("Editor.RuntimeMaterialVariantAnchors","Assembly-CSharp-Editor");
            int count=(int)Call(helper,"Ensure",scene);
            var anchors=Resources.LoadAll<Material>("FeedbackShaderVariants");
            Assert.That(anchors.Length,Is.EqualTo(count));
            Assert.That(anchors.Any(x=>x.IsKeywordEnabled("_NORMALMAP") && !x.IsKeywordEnabled("_METALLICSPECGLOSSMAP")),Is.True);
            Assert.That(anchors.Any(x=>x.IsKeywordEnabled("_EMISSION")),Is.True);
            Assert.That(anchors.Any(x=>x.IsKeywordEnabled("_NORMALMAP") && x.IsKeywordEnabled("_METALLICSPECGLOSSMAP")),Is.True);
            Assert.That(anchors.Any(x=>x.IsKeywordEnabled("_NORMALMAP") && x.IsKeywordEnabled("_METALLICSPECGLOSSMAP") &&
                x.IsKeywordEnabled("_SPECULARHIGHLIGHTS_OFF") && x.IsKeywordEnabled("_ENVIRONMENTREFLECTIONS_OFF")),Is.True);
            foreach(var anchor in anchors)
            {
                string path=AssetDatabase.GetAssetPath(anchor);
                Assert.That(path,Does.StartWith("Assets/Resources/FeedbackShaderVariants/Lit-"));
                Assert.That(anchor.name,Is.EqualTo(Path.GetFileNameWithoutExtension(path)),"Asset name must match Unity's persisted file stem");
                Assert.That(anchor.name.Length,Is.EqualTo(68),"Expected Lit- plus the complete SHA256 signature");
            }
            var identities=anchors.Select(x=>AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(x))).OrderBy(x=>x).ToArray();
            Assert.That(Hash(scene),Is.EqualTo(before));
            Assert.That((int)Call(helper,"Ensure",scene),Is.EqualTo(count),"Repeat build added duplicate anchors");
            Assert.That(Resources.LoadAll<Material>("FeedbackShaderVariants").Select(x=>AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(x))).OrderBy(x=>x).ToArray(),
                Is.EqualTo(identities),"Repeat build changed existing anchor GUIDs");
        }
    }
}
