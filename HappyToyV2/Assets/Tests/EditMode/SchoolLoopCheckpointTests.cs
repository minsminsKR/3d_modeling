using System;
using NUnit.Framework;
using UnityEngine;
using static HappyToy.V2.CloudTests.RuntimeAccess;

namespace HappyToy.V2.CloudTests
{
    public sealed class SchoolLoopCheckpointTests
    {
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
