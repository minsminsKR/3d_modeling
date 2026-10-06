using System;
using NUnit.Framework;
using UnityEngine;
using static HappyToy.V2.CloudTests.RuntimeAccess;

namespace HappyToy.V2.Tests.EditMode
{
    public sealed class CloudNoiseInvestigationClockTests
    {
        object Clock() => Activator.CreateInstance(RequireType("NoiseInvestigationClock"));

        [Test]
        public void NoiseInvestigationClockTravelBeyondThreeSecondsKeepsFullArrivalDwellAndPause()
        {
            var clock=Clock(); var point=new Vector3(18,0,0);
            Call(clock,"Begin",point,3f,18f,1.45f,1);
            Assert.That(Call(clock,"Tick",4f,12f,true).ToString(),Is.EqualTo("Travelling"));
            Assert.That(Get<bool>(clock,"Arrived"),Is.False);
            Assert.That(Get<float>(clock,"DwellRemaining"),Is.EqualTo(3));
            Assert.That(Get<Vector3>(clock,"Point"),Is.EqualTo(point));
            var before=JsonUtility.ToJson(Call(clock,"Capture"));
            Call(clock,"Tick",.33333334f,12f,false);
            Assert.That(JsonUtility.ToJson(Call(clock,"Capture")),Is.EqualTo(before),"Pause consumed a real travel budget");
            Assert.That(Call(clock,"Tick",.1f,.5f,true).ToString(),Is.EqualTo("Inspecting"));
            Assert.That(Get<float>(clock,"DwellRemaining"),Is.EqualTo(3),"Travel consumed the arrival inspection");
            Call(clock,"Tick",.33333334f,.5f,true);
            Assert.That(Get<float>(clock,"DwellRemaining"),Is.EqualTo(3-.33333334f).Within(.00001));
            before=JsonUtility.ToJson(Call(clock,"Capture")); Call(clock,"Tick",100f,0f,false);
            Assert.That(JsonUtility.ToJson(Call(clock,"Capture")),Is.EqualTo(before),"Pause consumed arrival dwell");
            Assert.That(Call(clock,"Tick",300f,0f,true).ToString(),Is.EqualTo("Expired"));
            Assert.That(Get<float>(clock,"DwellRemaining"),Is.Zero);
            Call(Call(clock,"Capture"),"Validate");
        }

        [Test]
        public void NoiseInvestigationClockBlockedTravelIsFiniteAndNeverClaimsArrival()
        {
            var clock=Clock(); Call(clock,"Begin",new Vector3(38,0,0),3f,38f,0f,128);
            Assert.That(Get<float>(clock,"TravelRemaining"),Is.EqualTo(90));
            Assert.That(Call(clock,"Tick",1000f,38f,true).ToString(),Is.EqualTo("Expired"));
            Assert.That(Get<bool>(clock,"Arrived"),Is.False,"Timeout falsely claimed inspection of unreachable evidence");
            Assert.That(Get<float>(clock,"TravelRemaining"),Is.Zero);
            Assert.That(Call(clock,"Tick",1f,0f,true).ToString(),Is.EqualTo("Expired"),"Expired evidence reactivated without a new noise");
            Call(Call(clock,"Capture"),"Validate");
        }

        [Test]
        public void NoiseInvestigationClockJsonRestoresExactStageAndRejectsNonfiniteWithoutMutation()
        {
            var clock=Clock(); Call(clock,"Begin",new Vector3(12,0,0),3f,12f,1.15f,2);
            Call(clock,"Tick",4f,7f,true);
            var snapshot=Call(clock,"Capture"); var json=JsonUtility.ToJson(snapshot);
            var parsed=JsonUtility.FromJson(json,snapshot.GetType());
            var restored=Clock(); Call(restored,"Restore",parsed);
            Assert.That(JsonUtility.ToJson(Call(restored,"Capture")),Is.EqualTo(json));
            Assert.Throws<ArgumentException>(()=>Call(restored,"Tick",float.NaN,7f,true));
            Assert.Throws<ArgumentException>(()=>Call(restored,"Tick",float.PositiveInfinity,7f,true));
            Assert.That(JsonUtility.ToJson(Call(restored,"Capture")),Is.EqualTo(json));
            Call(restored,"Restore",new object[]{null});
            Assert.That(Get<bool>(restored,"Active"),Is.False);
            var empty=JsonUtility.FromJson("{}",snapshot.GetType()); Call(restored,"Restore",empty);
            Assert.That(Get<bool>(restored,"Active"),Is.False,"Unity's empty legacy nested object started a new inspection");
            var forged=JsonUtility.FromJson("{\"version\":0,\"active\":true,\"dwellDuration\":3,\"dwellRemaining\":3}",snapshot.GetType());
            Assert.Throws<ArgumentException>(()=>Call(restored,"Restore",forged));
        }
    }
}
