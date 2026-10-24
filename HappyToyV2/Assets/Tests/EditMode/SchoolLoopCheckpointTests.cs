using System;
using NUnit.Framework;
using UnityEngine;
using static HappyToy.V2.CloudTests.RuntimeAccess;

namespace HappyToy.V2.CloudTests
{
    public sealed class SchoolLoopCheckpointTests
    {
        [Test] public void LegacySchoolLoopProgressDoesNotInventClockHours()
        {
            // The real codec preserves omitted optional inline objects, which
            // raw JsonUtility alone materializes as uninitialized placeholders.
            string json="{\"version\":1,\"simulationVersion\":3,\"token\":\""+Guid.NewGuid().ToString("N")+
                "\",\"scene\":\"Assets/Annex/SchoolAnnex.unity\",\"seconds\":17,\"recovered\":0,"+
                "\"player\":{\"position\":{\"x\":0,\"y\":0.03,\"z\":64},\"stamina\":0.7,\"stock\":2},"+
                "\"loop\":{\"traversals\":7},\"lightingVersion\":1,"+
                "\"lighting\":{\"version\":2,\"charge\":10,\"packsCollected\":0,\"depletions\":0,\"batteries\":[],\"candles\":[]},"+
                "\"doors\":[],\"supplies\":[]}";
            var checkpoint=Call(RequireType("ChapterCheckpoint"),"FromJson",json);
            var data=Get<object>(checkpoint,"loop");Assert.DoesNotThrow(()=>Call(data,"Validate"));
            Assert.That(Get<int>(data,"traversals"),Is.EqualTo(7));Assert.That(Get<object>(data,"walker"),Is.Null);
            var clock=Get<object>(data,"clock");Assert.That(clock==null?0:Get<int>(clock,"hours"),Is.Zero);
        }
        [TestCase(-1)] [TestCase(1000001)]
        public void InvalidSchoolClockHoursAreRejected(int hours)
        {
            var data=JsonUtility.FromJson("{\"traversals\":7,\"clock\":{\"hours\":"+hours+"}}",RequireType("SchoolLoopProgress"));
            Assert.Throws<ArgumentOutOfRangeException>(()=>Call(data,"Validate"));
        }
        static object Lighting(int version,string batteries="[]",string candles="[]",int packs=0,float charge=10)
        {
            string json="{\"version\":"+version+",\"charge\":"+charge+",\"packsCollected\":"+packs+",\"depletions\":0,\"batteries\":"+batteries+",\"candles\":"+candles+"}";
            return JsonUtility.FromJson(json,RequireType("LightExplorationCheckpoint"));
        }
        [Test] public void ObservationLightingAcceptsAnExplicitStationlessSchema()
        {Assert.DoesNotThrow(()=>Call(Lighting(2),"Validate"));}
        [Test] public void LegacyLightingStillRejectsMissingRealStations()
        {Assert.Throws<ArgumentException>(()=>Call(Lighting(1),"Validate"));}
        [Test] public void ObservationLightingRejectsImportedLegacyStationLists()
        {Assert.Throws<ArgumentException>(()=>Call(Lighting(2,"[{\"id\":\"old-battery\",\"available\":true}]"),"Validate"));}
        [Test] public void ObservationLightingCannotClaimCollectedPacksWithoutPickups()
        {Assert.Throws<ArgumentException>(()=>Call(Lighting(2,packs:1),"Validate"));}
        [Test] public void ObservationLightingRejectsAnInvalidFlashlightCharge()
        {Assert.Throws<ArgumentException>(()=>Call(Lighting(2,charge:-1),"Validate"));}
    }
}
