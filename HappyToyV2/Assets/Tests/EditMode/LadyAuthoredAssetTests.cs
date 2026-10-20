using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HappyToy.V2.CloudTests
{
    public sealed class LadyAuthoredAssetTests
    {
        [Test]
        public void LadyOptimizedClipPreservesEveryAuthoredBonePoseAndRootSupportAcrossTheWholeAction()
        {
            const string folder="Assets/Resources/Lady/";
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(folder+"LadyRuntime.prefab");
            var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(folder+"chase.anim");
            var source=AssetDatabase.LoadAllAssetsAtPath(folder+"Lady.fbx").OfType<AnimationClip>()
                .Single(action=>!action.name.StartsWith("__preview__"));
            Assert.That(clip.length,Is.EqualTo(source.length).Within(.0001f));
            var bindings=AnimationUtility.GetCurveBindings(clip);
            Assert.That(bindings.Any(binding=>binding.propertyName.StartsWith("m_LocalScale.")),Is.False,
                "Numerical FBX scale noise should stay in the prefab's rest baseline");
            Assert.That(bindings.Any(binding=>binding.propertyName.StartsWith("m_LocalPosition.")),Is.True,
                "The authored vertical support curve must remain animated");
            var reference=UnityEngine.Object.Instantiate(prefab);var actual=UnityEngine.Object.Instantiate(prefab);
            try
            {
                reference.SetActive(false);actual.SetActive(false);
                var rawModel=reference.transform.Find("Model");
                var first=reference.GetComponentsInChildren<Transform>(true);
                var second=actual.GetComponentsInChildren<Transform>(true);
                Assert.That(first.Length,Is.EqualTo(second.Length));
                float largestAngle=0,largestPosition=0;string worst="";
                for(int frame=0;frame<=2238;frame++)
                {
                    float time=Mathf.Min(frame/60f,clip.length);
                    source.SampleAnimation(rawModel.gameObject,time);clip.SampleAnimation(actual,time);
                    for(int index=0;index<first.Length;index++)
                    {
                        float angle=Quaternion.Angle(first[index].localRotation,second[index].localRotation);
                        if(angle>largestAngle){largestAngle=angle;worst="frame="+frame+" reference="+first[index].name+" actual="+second[index].name+" q="+first[index].localRotation+" / "+second[index].localRotation;}
                        largestPosition=Mathf.Max(largestPosition,Vector3.Distance(first[index].position,second[index].position));
                    }
                }
                Assert.That(largestAngle,Is.LessThan(.08f),"Optimization changed a violent joint angle/head tremor: "+worst);
                Assert.That(largestPosition,Is.LessThan(.0005f),"Scale cleanup changed the real pose by over half a millimetre");
            }
            finally{UnityEngine.Object.DestroyImmediate(reference);UnityEngine.Object.DestroyImmediate(actual);}
        }

        [Test]
        public void LadyGameLodsKeepTheirRigAndReduceTheActualImportedVertexBudget()
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Lady/LadyRuntime.prefab");
            var skins=prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                .OrderByDescending(skin=>skin.sharedMesh.vertexCount).ToArray();
            Assert.That(skins.Length,Is.EqualTo(3));
            foreach(var skin in skins)
            {
                Assert.That(skin.bones.Length,Is.EqualTo(28));
                Assert.That(skin.bones.All(bone=>bone),Is.True);
            }
            Assert.That(skins[0].sharedMesh.vertexCount,Is.LessThan(125000));
            Assert.That(skins[1].sharedMesh.vertexCount,Is.LessThan(70000));
            Assert.That(skins[2].sharedMesh.vertexCount,Is.LessThan(40000));
            Assert.That(skins[2].sharedMesh.triangles.Length/3,Is.LessThan(20000));
        }

        [Test]
        public void LadySupportCurveKeepsTheFormerFloatingPoseCloseToTheFloorAcrossEveryLod()
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Lady/LadyRuntime.prefab");
            var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Resources/Lady/chase.anim");
            var root=UnityEngine.Object.Instantiate(prefab);
            try
            {
                root.SetActive(false);clip.SampleAnimation(root,19.7f);
                var helper=RuntimeAccess.RequireType("EnemyStudioRig").GetMethod("PosedVertices");
                foreach(var skin in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    var points=(Vector3[])helper.Invoke(null,new object[]{skin});
                    Assert.That(points.Min(point=>point.y),Is.InRange(-.015f,.065f),skin.name+" lost its visual support correction");
                }
            }
            finally{UnityEngine.Object.DestroyImmediate(root);}
        }

        [Test]
        public void LadyReducedMotionAttenuatesTheAuthoredSupportOffsetAlongWithHerJointAngles()
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Lady/LadyRuntime.prefab");
            var owner=new GameObject("Lady reduced-motion asset integration");
            var model=UnityEngine.Object.Instantiate(prefab,owner.transform);
            try
            {
                model.SetActive(false);
                var motion=owner.AddComponent(RuntimeAccess.RequireType("LadyMotion"));
                bool reduced=false;
                motion.GetType().GetMethod("Prepare").Invoke(motion,new object[]{model,(Func<bool>)(()=>true),(Func<bool>)(()=>reduced)});
                var fit=model.GetComponentsInChildren<Transform>(true).Single(bone=>bone.name=="Lady_Fit");
                Vector3 neutral=fit.localPosition;
                motion.GetType().GetMethod("RestoreClock").Invoke(motion,new object[]{19.7f});
                Vector3 full=fit.localPosition;
                Assert.That(Vector3.Distance(full,neutral),Is.GreaterThan(.05f));
                reduced=true;motion.GetType().GetMethod("RestoreClock").Invoke(motion,new object[]{19.7f});
                Assert.That(Vector3.Distance(fit.localPosition,Vector3.Lerp(neutral,full,.18f)),Is.LessThan(.00001f));
            }
            finally{UnityEngine.Object.DestroyImmediate(owner);}
        }
    }
}
