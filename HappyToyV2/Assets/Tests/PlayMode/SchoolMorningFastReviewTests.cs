using System;
using System.Collections;
using System.Reflection;
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
        public IEnumerator SchoolOnePassMorningReviewForwardSeam()
        {yield return CheckOnePassMorning(1);}

        [UnityTest,Timeout(180000)]
        public IEnumerator SchoolOnePassMorningReviewBackwardSeam()
        {yield return CheckOnePassMorning(-1);}

        IEnumerator CheckOnePassMorning(int direction)
        {
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"-v2-school-one-pass-morning")<0)
                yield break; // Opt-in review; ordinary suites must retain their normal rules.
            yield return BeginObservationSchool();
            var world=Get<Component>(Get<Component>(session,"Chapter"),"Loop");
            Assert.That(Get<bool>(world,"OnePassMorning"),Is.True);
            var director=Get<Component>(world,"Anomalies");
            Assert.That(Get<object>(director,"Kind").ToString(),Is.EqualTo("Normal"),
                "The first passage must be normal even when older review flags coexist");
            Assert.That(Get<int>(world,"ClockHours"),Is.Zero);
            yield return CrossSchoolJudgmentSeam(world,direction);
            Assert.That(Get<int>(world,"ClockHours"),Is.EqualTo(8));
            foreach(var clock in Get<Transform>(world,"Root").GetComponentsInChildren(RequireType("SchoolProgressClockDisplay")))
                Assert.That(Get<string>(clock,"DisplayText"),Is.EqualTo("08:00"));
            yield return Wait(()=>Get<bool>(world,"MorningStarted"),3,"The first transfer did not start sunrise");
            Assert.That(Get<Component>(world,"MorningBirds"),Is.Not.Null);
            Assert.That(Get<bool>(session,"Finished"),Is.False,"The review should leave time to inspect sunrise");
            if(direction>0)
            {
                int traversal=Get<int>(world,"Traversals");
                PlacePlayer(LoopOrigin+new Vector3(6.6f,.03f,46.1f));player.transform.rotation=Quaternion.identity;
                yield return null;Keys(Key.W);
                yield return Wait(()=>player.transform.position.z-LoopOrigin.z<0,3,"Morning seam did not return the player safely");
                Keys();yield return null;
                Assert.That(Get<int>(world,"Traversals"),Is.EqualTo(traversal),"Morning walking must not create new judgments");
                Assert.That(Get<int>(world,"ClockHours"),Is.EqualTo(8));
            }
            Call(shell,"Pause");var checkpoint=Call(session,"CaptureChapterCheckpoint");Assert.DoesNotThrow(()=>Call(checkpoint,"Validate"));
            Call(shell,"Resume");
            var elapsed=world.GetType().GetField("morningElapsed",BindingFlags.NonPublic|BindingFlags.Instance);
            Assert.That(elapsed,Is.Not.Null);elapsed.SetValue(world,14f);yield return null;yield return null;
            var birdSource=Get<AudioSource>(Get<Component>(world,"MorningBirds"),"Source");
            Assert.That(Get<bool>(session,"Finished"),Is.False,"The review must not use the ordinary 13-second cutoff");
            Assert.That(birdSource.volume,Is.GreaterThan(.5f),"Birdsong must continue while the review remains open");
            elapsed.SetValue(world,59.9f);
            yield return Wait(()=>Get<bool>(session,"Finished"),3,"The one-minute review never finished");
            Assert.That(Get<bool>(session,"Escaped"),Is.True);Assert.That(birdSource.volume,Is.Zero);
        }
    }
}
