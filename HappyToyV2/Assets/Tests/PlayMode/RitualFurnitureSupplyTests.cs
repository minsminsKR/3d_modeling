using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using static HappyToy.V2.CloudTests.RuntimeAccess;

namespace HappyToy.V2.CloudTests
{
    public sealed partial class CloudPlayModeTests
    {
        [UnityTest,Timeout(180000)]
        public IEnumerator RitualShelfAndDrawerSuppliesHaveRealSupportRealEAndFiniteSavedStock()
        {
            yield return RevisionPrepare();var run=Get<Component>(session,"Corridor");var items=RitualWorldPickups(run);
            var extra=items.Where(x=>Get<string>(x,"stableId").StartsWith("ritual-drawer-",StringComparison.Ordinal)||
                Get<string>(x,"stableId").StartsWith("ritual-shelf-",StringComparison.Ordinal)).ToArray();
            Assert.That(items.Length,Is.EqualTo(17));Assert.That(extra.Length,Is.EqualTo(10));
            Assert.That(extra.Count(x=>x.GetComponentInParent(RequireType("CorridorDrawer"))),Is.EqualTo(4));
            Assert.That(extra.Count(x=>Get<string>(x,"stableId").StartsWith("ritual-shelf-",StringComparison.Ordinal)),Is.EqualTo(6));
            foreach(var item in extra)AssertRitualVisualTouchesSupport(item);
            var ritual=Get<Component>(player,"Rituals");
            foreach(string kind in new[]{"IncenseSupply","TalismanFragment"})
            {
                var item=extra.First(x=>Get<object>(x,"kind").ToString()==kind&&x.GetComponentInParent(RequireType("CorridorDrawer")));
                var drawer=item.GetComponentInParent(RequireType("CorridorDrawer"));
                Assert.That(Get<bool>(item,"InteractionAvailable"),Is.False);
                Call(item,"Use",player);Assert.That(item.gameObject.activeSelf,Is.True,"A closed drawer dispensed ritual loot");
                var interaction=DrawerInteraction(drawer);
                Assert.That(FurnitureApproach(interaction,player.transform.position,out var approach),Is.True);
                PlacePlayer(approach);yield return null;yield return FurniturePressE(interaction);
                yield return Wait(()=>Get<bool>(drawer,"ExposesPickup")&&Get<bool>(drawer,"AtRequestedPose"),4,"Real E did not expose ritual drawer contents");
                AssertRitualVisualTouchesSupport(item);
                Assert.That(FurnitureApproach(item,player.transform.position,out approach),Is.True);
                PlacePlayer(approach);yield return null;yield return FurniturePressE(item);
                Assert.That(item.gameObject.activeSelf,Is.False);
            }
            // Collect a shelf supply through its actual interaction ray too.
            var shelf=extra.First(x=>Get<string>(x,"stableId").StartsWith("ritual-shelf-",StringComparison.Ordinal));
            Assert.That(FurnitureApproach(shelf,player.transform.position,out var shelfApproach),Is.True);
            PlacePlayer(shelfApproach);yield return null;yield return FurniturePressE(shelf);
            Assert.That(shelf.gameObject.activeSelf,Is.False);
            Assert.That(Get<int>(ritual,"Incense"),Is.EqualTo(3));Assert.That(Get<int>(ritual,"Fragments"),Is.EqualTo(1));
            Call(shell,"Pause");yield return null;var saved=CheckpointCopy(Call(session,"CaptureCheckpoint"));
            var stock=Get<object>(saved,"ritualItems");Assert.That(Get<int>(stock,"version"),Is.EqualTo(2));
            var states=Get<Array>(stock,"supplies").Cast<object>().ToDictionary(x=>Get<string>(x,"id"),x=>Get<bool>(x,"available"));
            yield return RestoreCheckpointInFreshScene(saved);run=Get<Component>(session,"Corridor");
            foreach(var item in RitualWorldPickups(run))Assert.That(item.gameObject.activeSelf,Is.EqualTo(states[Get<string>(item,"stableId")]));
            ritual=Get<Component>(player,"Rituals");Assert.That(Get<int>(ritual,"Incense"),Is.EqualTo(3));Assert.That(Get<int>(ritual,"Fragments"),Is.EqualTo(1));
        }

        [UnityTest,Timeout(120000)]
        public IEnumerator LegacyRitualSaveRetainsConsumedMemoriesAndIntroducesFurnitureSupplies()
        {
            yield return RevisionPrepare();Call(CheckpointItems("TalismanFragment").Single(x=>Get<string>(x,"stableId")=="ritual-fragment-0"),"Use",player);
            Call(shell,"Pause");yield return null;var saved=CheckpointCopy(Call(session,"CaptureCheckpoint"));var stock=Get<object>(saved,"ritualItems");
            var all=Get<Array>(stock,"supplies");
            var rows=all.Cast<object>().Where(x=>Get<string>(x,"id").StartsWith("ritual-fragment-",StringComparison.Ordinal)||Get<string>(x,"id").StartsWith("ritual-incense-",StringComparison.Ordinal)).ToArray();
            var legacy=Array.CreateInstance(all.GetType().GetElementType(),rows.Length);for(int i=0;i<rows.Length;i++)legacy.SetValue(rows[i],i);
            Set(stock,"version",1);Set(stock,"supplies",legacy);Call(saved,"Validate");
            yield return RestoreCheckpointInFreshScene(saved);
            var items=RitualWorldPickups(Get<Component>(session,"Corridor"));
            Assert.That(items.Single(x=>Get<string>(x,"stableId")=="ritual-fragment-0").gameObject.activeSelf,Is.False);
            Assert.That(items.Where(x=>Get<string>(x,"stableId").StartsWith("ritual-drawer-",StringComparison.Ordinal)||Get<string>(x,"stableId").StartsWith("ritual-shelf-",StringComparison.Ordinal)).All(x=>x.gameObject.activeSelf),Is.True);
            Assert.That(Get<int>(Get<Component>(player,"Rituals"),"Fragments"),Is.EqualTo(1));
            // New snapshots still reject a lost/unknown stash identity.
            var current=Call(RequireType("RitualWorld"),"Capture",player);var currentRows=Get<Array>(current,"supplies");
            Set(currentRows.GetValue(0),"id","ritual-unknown");
            Assert.Throws<ArgumentException>(()=>Call(RequireType("RitualWorld"),"ValidateRestore",current,player));
        }
    }
}
