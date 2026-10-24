using System;
using System.Collections;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using static HappyToy.V2.CloudTests.RuntimeAccess;

namespace HappyToy.V2.CloudTests
{
    public sealed partial class CloudPlayModeTests
    {
        [UnityTest,Timeout(180000)]
        public IEnumerator SchoolWalkerCheckpointJsonAndCopyRestoreTheExactPausedPassAndAuthoredPose()
        {
            yield return BeginObservationSchool();var walker=One("SchoolWalkerEncounter");
            PlacePlayer(LoopOrigin+new Vector3(.65f,.03f,31));var start=player.transform.position;
            player.transform.rotation=Quaternion.identity;Keys(Key.W);yield return Delay(.25f);Keys();yield return Delay(.25f);
            Assert.That(player.transform.position.z,Is.GreaterThan(start.z+.2f),"The fixture did not actually walk through the school");
            Assert.That(Get<float>(walker,"Distance"),Is.GreaterThan(.1f),"The pedestrian never began his real pass");
            Call(shell,"Pause");yield return null;
            var expected=Call(walker,"CaptureState");var motion=Get<Component>(walker,"Motion");
            float expectedWalkClock=Get<float>(motion,"Clock"),expectedIdleClock=Get<float>(motion,"IdleClock");
            var expectedFeet=walker.transform.position;var expectedFacing=walker.transform.rotation;
            var pose=CaptureSchoolWalkerSavedPose(Get<Transform>(walker,"Visual"));
            var checkpoint=Call(session,"CaptureChapterCheckpoint");string json=(string)Call(checkpoint,"ToJson");
            Assert.That(Regex.IsMatch(json,"\\\"walker\\\"\\s*:\\s*\\{"),Is.True,"The actual save omitted the live walker");
            var parsed=Call(RequireType("ChapterCheckpoint"),"FromJson",json);var copy=Call(parsed,"Copy");
            AssertSchoolWalkerStateClose(expected,Call(Get<object>(Get<object>(copy,"loop"),"walker"),"ToState"));
            var previous=session;Call(shell,"Restart",false);yield return RecoveryRebind(previous);
            Call(session,"CreateChapterForCheckpoint",copy);Call(session,"ApplyChapterCheckpoint",copy);
            walker=One("SchoolWalkerEncounter");motion=Get<Component>(walker,"Motion");
            AssertSchoolWalkerStateClose(expected,Call(walker,"CaptureState"));
            Assert.That(Vector3.Distance(walker.transform.position,expectedFeet),Is.LessThan(.00002f));
            Assert.That(Quaternion.Angle(walker.transform.rotation,expectedFacing),Is.LessThan(.02f));
            Assert.That(Get<float>(motion,"Clock"),Is.EqualTo(expectedWalkClock).Within(.00002f));
            Assert.That(Get<float>(motion,"IdleClock"),Is.EqualTo(expectedIdleClock).Within(.00002f));
            AssertSchoolWalkerSavedPose(pose,CaptureSchoolWalkerSavedPose(Get<Transform>(walker,"Visual")));
            yield return Delay(.2f);
            AssertSchoolWalkerStateClose(expected,Call(walker,"CaptureState"));
            Assert.That(Get<bool>(session,"InputAllowed"),Is.False,"Restoring a save started the walker before the player resumed");
            // Restoring must not bypass the new stationary-player rule. Hold
            // him first, then clear the lane before expecting gait to resume.
            PlacePlayer(walker.transform.position+walker.transform.forward*.81f,false);
            Begin();yield return Delay(.3f);
            Assert.That(Get<float>(walker,"Distance"),Is.EqualTo(Get<float>(expected,"Distance")).Within(.00001f));
            PlacePlayer(walker.transform.position+Vector3.left*1.5f,false);yield return Delay(.25f);
            Assert.That(Get<float>(walker,"Distance"),Is.GreaterThan(Get<float>(expected,"Distance")+.05f));
            Assert.That(Get<float>(motion,"Clock"),Is.GreaterThan(expectedWalkClock));
            Assert.That(Components("SchoolWalkerEncounter").Length,Is.EqualTo(1));
        }

        [UnityTest,Timeout(180000)]
        public IEnumerator SchoolWalkerGenuinelyAbsentAndExplicitNullJsonRestoreOldV3WithoutPlayerOverlap()
        {
            yield return BeginObservationSchool();yield return Delay(.25f);Call(shell,"Pause");
            var captured=Call(session,"CaptureChapterCheckpoint");var loop=Get<object>(captured,"loop");
            Set(loop,"traversals",7);Set(Get<object>(loop,"walker"),"traversal",7);
            Set(loop,"anomalies",null); // This fixture represents a genuinely pre-anomaly save.
            var playerProgress=Get<object>(captured,"player");
            Set(playerProgress,"position",LoopOrigin+new Vector3(-.5f,.03f,33));
            string negativeJson=(string)Call(captured,"ToJson");
            Set(playerProgress,"position",LoopOrigin+new Vector3(.5f,.03f,33));
            string positiveJson=(string)Call(captured,"ToJson");
            // Mutate the serialized text, rather than merely assigning null to
            // an already materialized object. JsonUtility's old inline-class
            // defaults must be corrected at the real checkpoint codec boundary.
            string absent=RemoveSchoolWalkerJsonNode(negativeJson);
            string explicitNull=ReplaceSchoolWalkerJsonNode(positiveJson,"\"walker\": null");
            Assert.That(absent,Does.Not.Contain("\"walker\""));
            Assert.That(Regex.IsMatch(explicitNull,"\\\"walker\\\"\\s*:\\s*null"),Is.True);
            foreach(string legacyJson in new[]{absent,explicitNull})
            {
                var parsed=Call(RequireType("ChapterCheckpoint"),"FromJson",legacyJson);
                Assert.That(Get<object>(Get<object>(parsed,"loop"),"walker"),Is.Null,"The codec materialized a bogus old-save walker");
                var copy=Call(parsed,"Copy");Assert.That(Get<object>(Get<object>(copy,"loop"),"walker"),Is.Null);
                Assert.That(Get<int>(copy,"simulationVersion"),Is.EqualTo(3));Call(copy,"Validate");
                var previous=session;Call(shell,"Restart",false);yield return RecoveryRebind(previous);
                Call(session,"CreateChapterForCheckpoint",copy);Call(session,"ApplyChapterCheckpoint",copy);
                var world=Get<Component>(Get<Component>(session,"Chapter"),"Loop");var walker=One("SchoolWalkerEncounter");
                Assert.That(Get<int>(world,"Traversals"),Is.EqualTo(7));Assert.That(Get<int>(walker,"Traversal"),Is.EqualTo(7));
                Assert.That(Get<float>(walker,"Distance"),Is.Zero);Assert.That(walker.transform.position.z-LoopOrigin.z,Is.EqualTo(33).Within(.001f));
                float savedX=player.transform.position.x-LoopOrigin.x;
                Assert.That(Mathf.Abs(walker.transform.position.x-player.transform.position.x),Is.GreaterThanOrEqualTo(.84f));
                Assert.That(walker.transform.position.x-LoopOrigin.x,Is.EqualTo(savedX>=0?-.85f:.85f).Within(.001f));
                Assert.That(Components("SchoolWalkerEncounter").Length,Is.EqualTo(1));
            }
        }

        [UnityTest,Timeout(180000)]
        public IEnumerator SchoolWalkerCheckpointRejectsCorruptVersionNonfiniteMotionAndFuturePendingBeforeMutation()
        {
            yield return BeginObservationSchool();yield return Delay(.2f);Call(shell,"Pause");
            var captured=Call(session,"CaptureChapterCheckpoint");
            var previous=session;Call(shell,"Restart",false);yield return RecoveryRebind(previous);
            Call(session,"CreateChapterForCheckpoint",captured);
            var walker=One("SchoolWalkerEncounter");var unchangedState=Call(walker,"CaptureState");var unchangedPlayer=player.transform.position;
            var corruptions=new Action<object>[] {
                progress=>Set(progress,"version",999),
                progress=>Set(progress,"distance",float.NaN),
                progress=>Set(progress,"pendingTraversal",Get<int>(Get<object>(captured,"loop"),"traversals")+1)};
            foreach(var corrupt in corruptions)
            {
                var invalid=Call(captured,"Copy");var progress=Get<object>(Get<object>(invalid,"loop"),"walker");corrupt(progress);
                Assert.Catch<ArgumentException>(()=>Call(invalid,"Validate"));
                string invalidJson;
                if(float.IsNaN(Get<float>(progress,"distance")))
                {
                    string validJson=(string)Call(captured,"ToJson");
                    var node=Regex.Match(validJson,"\\\"walker\\\"\\s*:\\s*\\{[^{}]*\\}");Assert.That(node.Success,Is.True);
                    string poison=Regex.Replace(node.Value,"\\\"distance\\\"\\s*:\\s*[^,}]+","\"distance\": NaN");
                    Assert.That(poison,Does.Contain("NaN"));invalidJson=ReplaceSchoolWalkerJsonNode(validJson,poison);
                }
                else invalidJson=(string)Call(invalid,"ToJson");
                Assert.Catch<ArgumentException>(()=>Call(RequireType("ChapterCheckpoint"),"FromJson",invalidJson),
                    "A malformed present walker was silently accepted or converted to an old-save absence");
                Assert.Catch<ArgumentException>(()=>Call(session,"ApplyChapterCheckpoint",invalid));
                AssertSchoolWalkerStateClose(unchangedState,Call(walker,"CaptureState"));
                Assert.That(player.transform.position,Is.EqualTo(unchangedPlayer));
            }
            var valid=Call(captured,"Copy");Call(session,"ApplyChapterCheckpoint",valid);
            AssertSchoolWalkerStateClose(Call(Get<object>(Get<object>(valid,"loop"),"walker"),"ToState"),Call(walker,"CaptureState"));
        }

        sealed class SchoolWalkerSavedPart
        {
            public string path;
            public Vector3 position,scale;
            public Quaternion rotation;
        }
        static SchoolWalkerSavedPart[] CaptureSchoolWalkerSavedPose(Transform visual)
        {
            return visual.GetComponentsInChildren<Transform>(true).Select(part=>
            {
                string path=part.name;for(var ancestor=part.parent;ancestor&&ancestor!=visual.parent;ancestor=ancestor.parent)path=ancestor.name+"/"+path;
                return new SchoolWalkerSavedPart{path=path,position=part.localPosition,rotation=part.localRotation,scale=part.localScale};
            }).ToArray();
        }
        static void AssertSchoolWalkerSavedPose(SchoolWalkerSavedPart[] expected,SchoolWalkerSavedPart[] actual)
        {
            CollectionAssert.AreEqual(expected.Select(p=>p.path),actual.Select(p=>p.path));
            for(int index=0;index<expected.Length;index++)
            {
                Assert.That(Vector3.Distance(expected[index].position,actual[index].position),Is.LessThan(.00002f),expected[index].path+" position");
                Assert.That(Quaternion.Angle(expected[index].rotation,actual[index].rotation),Is.LessThan(.02f),expected[index].path+" rotation");
                Assert.That(Vector3.Distance(expected[index].scale,actual[index].scale),Is.LessThan(.00002f),expected[index].path+" scale");
            }
        }
        static void AssertSchoolWalkerStateClose(object expected,object actual)
        {
            foreach(string field in new[]{"Traversal","PendingTraversal","Leg","PassSide"})
                Assert.That(Get<int>(actual,field),Is.EqualTo(Get<int>(expected,field)),field);
            foreach(string field in new[]{"X","Z","Yaw","Distance","IdleBlend"})
                Assert.That(Get<float>(actual,field),Is.EqualTo(Get<float>(expected,field)).Within(.00001f),field);
            foreach(string field in new[]{"Elapsed","IdleElapsed"})
                Assert.That(Get<double>(actual,field),Is.EqualTo(Get<double>(expected,field)).Within(.000001),field);
        }
        static string ReplaceSchoolWalkerJsonNode(string json,string replacement)
        {
            var expression=new Regex("\\\"walker\\\"\\s*:\\s*\\{[^{}]*\\}");
            Assert.That(expression.Matches(json).Count,Is.EqualTo(1),"Expected one actual flat walker progress object in the captured checkpoint");
            return expression.Replace(json,replacement,1);
        }
        static string RemoveSchoolWalkerJsonNode(string json)
        {
            var expression=new Regex(",\\s*\\\"walker\\\"\\s*:\\s*\\{[^{}]*\\}");
            Assert.That(expression.Matches(json).Count,Is.EqualTo(1),"Expected the walker after the existing loop clock in the captured JSON");
            return expression.Replace(json,"",1);
        }
    }
}
