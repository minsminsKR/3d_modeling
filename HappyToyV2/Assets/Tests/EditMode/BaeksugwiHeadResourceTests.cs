using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HappyToy.V2.CloudTests
{
    public sealed class BaeksugwiHeadResourceTests
    {
        [Test]
        public void RuntimeCreatureKeyResolvesOnlyThePrefabAndPreservesSourceIdentity()
        {
            const string folder="Assets/Resources/Baeksugwi/";
            const string prefabPath=folder+"Baeksugwi.prefab";
            const string sourcePath=folder+"Baeksugwi_Source.fbx";
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Assert.That(prefab,Is.Not.Null);
            Assert.That(Resources.Load<GameObject>("Baeksugwi/Baeksugwi"),Is.SameAs(prefab),
                "The source FBX is missing the authored prefab's wrapper and component bindings");
            Assert.That(AssetDatabase.AssetPathToGUID(prefabPath),Is.EqualTo("264352150f46e9f41b200e822d6469aa"));
            Assert.That(AssetDatabase.AssetPathToGUID(sourcePath),Is.EqualTo("d0263503850c7a64ea5a5a0d3243c2ac"));
            Assert.That(Directory.GetFiles(folder,"Baeksugwi.*").Where(path=>!path.EndsWith(".meta",StringComparison.OrdinalIgnoreCase))
                .Select(path=>Path.GetExtension(path)).ToArray(),Is.EqualTo(new[]{".prefab"}),"The runtime creature Resources key must be unique");
            Assert.That(prefab.transform.Find("Rigged asset/Baeksugwi_Rig/BodyRoot/SpineFront/Chest/Neck/Head"),Is.Not.Null);
        }
        [Test]
        public void RuntimeHeadKeyResolvesOnlyTheAuthoredHeadClipAndPreservesSourceIdentity()
        {
            const string folder="Assets/Resources/Baeksugwi/";
            const string clipPath=folder+"Head_Writhe.anim";
            const string sourcePath=folder+"Head_Writhe_Source.fbx";
            var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            Assert.That(clip,Is.Not.Null);
            Assert.That(Resources.Load<AnimationClip>("Baeksugwi/Head_Writhe"),Is.SameAs(clip),
                "An FBX with the same Resources key can return its unretargeted Scene take");
            Assert.That(clip.name,Is.EqualTo("Head_Writhe"));
            Assert.That(AssetDatabase.AssetPathToGUID(clipPath),Is.EqualTo("475b0a716fde3f6419c6ccc490d02d30"));
            Assert.That(AssetDatabase.AssetPathToGUID(sourcePath),Is.EqualTo("62055e562b8c6554aa9f15a1aba41228"));
            Assert.That(Directory.GetFiles(folder,"Head_Writhe.*").Where(path=>!path.EndsWith(".meta",StringComparison.OrdinalIgnoreCase))
                .Select(path=>Path.GetExtension(path)).ToArray(),Is.EqualTo(new[]{".anim"}),"The runtime Resources key must be unique");
            var bindings=AnimationUtility.GetCurveBindings(clip);
            Assert.That(bindings.Length,Is.GreaterThanOrEqualTo(4));
            Assert.That(bindings.All(binding=>binding.path=="Rigged asset/Baeksugwi_Rig/BodyRoot/SpineFront/Chest/Neck/Head"),Is.True,
                "The extracted clip must retain the prefab-relative head binding");
        }
    }
}
