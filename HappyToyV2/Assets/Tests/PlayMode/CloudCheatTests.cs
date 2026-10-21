using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using static HappyToy.V2.CloudTests.RuntimeAccess;

namespace HappyToy.V2.CloudTests
{
    public sealed partial class CloudPlayModeTests
    {
        IEnumerator CheatTap(Key key)
        {
            Keys(key); yield return Delay(.08f);
            Keys(); yield return Delay(.08f);
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator CheatF1CollectsOnePerPressAndCannotDuplicateOrFinishTheCorridor()
        {
            Call(session, "ConfigureRecordDirectory", NewRecordFixture());
            Call(session, "CreateCorridor", 73); Begin(); yield return null;
            Call(session, "ToggleCheatSafeMode");
            var memories = Components("Interactable").Where(x => Get<object>(x, "kind").ToString() == "CorridorMemory").ToArray();
            Call(memories[2], "Use", player);
            Assert.That(Get<int>(session, "RecordsRecovered"), Is.EqualTo(1));
            Keys(Key.F1); yield return Delay(.4f);
            Assert.That(Get<int>(session, "RecordsRecovered"), Is.EqualTo(2), "Holding F1 must not repeat");
            Keys(); yield return Delay(.08f);
            for (int count = 3; count <= 5; count++)
            {
                yield return CheatTap(Key.F1);
                Assert.That(Get<int>(session, "RecordsRecovered"), Is.EqualTo(count));
            }
            yield return CheatTap(Key.F1);
            Assert.That(Get<int>(session, "RecordsRecovered"), Is.EqualTo(5));
            Assert.That(memories.All(x => !x.gameObject.activeSelf), Is.True);
            Assert.That(Get<bool>(session, "Finished"), Is.False);
            Call(shell, "Pause");
            Assert.DoesNotThrow(() => Call(session, "CaptureCheckpoint"));
            Keys(Key.F1, Key.F2); yield return Delay(.1f); Keys(); yield return null;
            Assert.That(Get<bool>(session, "CheatSafeMode"), Is.True, "Pause must ignore cheat keys");
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator CheatF2CancelsPursuitBlocksDeathSightAndSoundAndCanBeTurnedOff()
        {
            Call(session, "ConfigureRecordDirectory", NewRecordFixture());
            Call(session, "CreateCorridor", 73); Begin(); yield return null;
            var brain = Components("StalkerBrain").First(x => x.gameObject.activeInHierarchy);
            var chase = System.Enum.Parse(Get<object>(brain, "state").GetType(), "Chase");
            Set(brain, "state", chase);
            brain.GetType().GetMethod("BeginAttack", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .Invoke(brain, new object[] { false, session });
            Assert.That(Get<bool>(brain, "AttackActive"), Is.True);
            yield return CheatTap(Key.F2);
            Assert.That(Get<bool>(session, "CheatSafeMode"), Is.True);
            Assert.That(Get<object>(brain, "state").ToString(), Is.EqualTo("Patrol"));
            Assert.That(Get<bool>(brain, "AttackActive"), Is.False);
            Assert.That((bool)Call(brain, "CanSeePlayer"), Is.False);
            Assert.That((bool)Call(brain, "HearNoise", player.transform.position, 3f), Is.False);
            var mask = Get<Component>(Get<Component>(session, "Corridor"), "Mask");
            Assert.That((bool)Call(mask, "CanSeePlayer"), Is.False);
            Assert.That((bool)Call(mask, "HearNoise", player.transform.position, 3f), Is.False);
            Assert.That((bool)Call(session, "TryDefeat", "cheat regression", "test"), Is.False);
            Call(session, "Finish", false);
            Assert.That(Get<bool>(session, "Finished"), Is.False);
            Keys(Key.F2); yield return Delay(.4f);
            Assert.That(Get<bool>(session, "CheatSafeMode"), Is.False, "Holding F2 toggles only once");
            Keys(); yield return null;
            Assert.That((bool)Call(session, "TryDefeat", "cheat regression", "test"), Is.True);
            Assert.That(Get<bool>(session, "Finished"), Is.True);
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator CheatF2RestoresExhaustedStaminaDoublesOnlySprintAndReturnsToNormal()
        {
            IsolateThreats(); Begin(); Call(shell,"RestoreDefaultSettings");
            var origin=new Vector3(500,0,500);
            Cube("CloudQA F2 sprint floor",origin-Vector3.up*.2f,new Vector3(240,.4f,240));
            PlacePlayer(origin+Vector3.up*.02f);
            yield return Wait(()=>Get<bool>(player,"Grounded"),3,"F2 fixture never grounded");
            float run=Get<float>(player,"runSpeed"),walk=Get<float>(player,"walkSpeed");
            yield return KeysObserved(Key.W,Key.LeftShift);
            yield return Wait(()=>Get<bool>(player,"SprintExhausted"),9,"Normal sprint never exhausted");
            // Keep sprint held while turning F2 on: no release/recovery latch is required.
            yield return KeysObserved(Key.W,Key.LeftShift,Key.F2);
            yield return Wait(()=>Get<bool>(session,"CheatSafeMode") && Get<bool>(player,"Running") &&
                Get<float>(player,"ActualSpeed")>run*1.9f,2,"F2 did not unlock double-speed sprint");
            Keys(Key.W,Key.LeftShift);
            Vector3 start=player.transform.position;
            int steps=Get<int>(player,"MovementUpdates");
            yield return new WaitForSeconds(6);
            int moved=Get<int>(player,"MovementUpdates")-steps;
            Assert.That((player.transform.position.z-start.z)/(moved*Time.fixedDeltaTime),Is.EqualTo(run*2).Within(.15f));
            Assert.That(Get<float>(player,"Stamina"),Is.EqualTo(1));
            Assert.That(Get<bool>(player,"SprintExhausted"),Is.False);
            Keys(Key.W);yield return new WaitForSeconds(.15f);
            Assert.That(Get<float>(player,"ActualSpeed"),Is.EqualTo(walk).Within(.1f),"F2 accelerated walking");
            yield return CheatTap(Key.C);Keys(Key.W,Key.LeftShift);yield return new WaitForSeconds(.15f);
            Assert.That(Get<bool>(player,"Crouching"),Is.True);
            Assert.That(Get<float>(player,"ActualSpeed"),Is.EqualTo(walk*.55f).Within(.1f),"F2 accelerated crouching");
            yield return CheatTap(Key.C);Keys(Key.W,Key.LeftShift);yield return new WaitForSeconds(.15f);
            Call(shell,"Pause");start=player.transform.position;
            yield return Delay(.2f);Assert.That(player.transform.position,Is.EqualTo(start));
            Assert.That(Get<float>(player,"Stamina"),Is.EqualTo(1));
            Call(shell,"Resume");
            yield return CheatTap(Key.F2);yield return KeysObserved(Key.W,Key.LeftShift);
            yield return new WaitForSeconds(.5f);
            Assert.That(Get<bool>(session,"CheatSafeMode"),Is.False);
            Assert.That(Get<float>(player,"ActualSpeed"),Is.EqualTo(run).Within(.1f));
            Assert.That(Get<float>(player,"Stamina"),Is.LessThan(.95f));
            Assert.That(Get<float>(player,"runSpeed"),Is.EqualTo(run),"F2 mutated the authored run speed");
            Keys();
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator CheatF1SkipsSchoolRevealGatesAndKeepsCheckpointValid()
        {
            Call(session, "ConfigureRecordDirectory", NewRecordFixture());
            yield return RecoveryUiReady(); yield return RecoveryClick("begin-school"); yield return null;
            yield return CheatTap(Key.F2);
            var chapter = Get<Component>(session, "Chapter");
            for (int count = 1; count <= 5; count++)
            {
                yield return CheatTap(Key.F1);
                Assert.That(Get<int>(session, "RecordsRecovered"), Is.EqualTo(count));
                Assert.That(Get<bool>(Get<Component>(chapter, "FirstAppearances"), "CameraOwned"), Is.False);
            }
            Assert.That(Get<bool>(Get<Component>(chapter, "Portrait"), "Completed"), Is.True);
            Assert.That(Get<bool>(Get<Component>(chapter, "Portrait"), "ChapterWitnessed"), Is.True);
            Assert.That(Get<bool>(Get<Component>(chapter, "Nursery"), "Released"), Is.True);
            Assert.That(Get<Component[]>(chapter, "Memories").All(x => !x.gameObject.activeSelf), Is.True);
            Assert.That(Get<bool>(session, "Finished"), Is.False);
            Call(shell, "Pause");
            Assert.DoesNotThrow(() => Call(session, "CaptureChapterCheckpoint"));
        }
        bool CheatUseSelected(string item)
        {
            var selected=Get<object>(ItemLoadout,"Selected");
            Call(ItemLoadout,"Select",System.Enum.Parse(selected.GetType(),item));
            return (bool)Call(ItemLoadout,"UseSelected");
        }

        [UnityTest,Timeout(120000)]
        public IEnumerator CheatF2UsesEveryEmptyItemAndEmptyFlashlightShowsInfinityAndTurnsOffCleanly()
        {
            Call(session,"CreateCorridor",73);Begin();IsolateThreats();yield return null;
            yield return RecoveryUiReady();
            var snappers=Get<Component>(player,"Firecrackers");var traps=Get<Component>(player,"Traps");
            var rituals=Get<Component>(player,"Rituals");var charge=Get<Component>(player,"FlashlightSystem");
            Call(snappers,"RestoreStock",0,0f);Call(traps,"Restore",0,0f);Call(rituals,"Restore",0,0,0);
            Call(charge,"Restore",0f,0,0);yield return null;
            yield return CheatTap(Key.F2);
            Assert.That(Get<bool>(ItemLoadout,"Unlimited"),Is.True);
            var root=Get<VisualElement>(One("GameShellView"),"Root");
            for(int i=0;i<5;i++)Assert.That(root.Q<Label>("item-wheel-count-"+i).text,Is.EqualTo("∞"));
            Assert.That(Get<float>(root.Q("flashlight-battery-meter"),"Fraction"),Is.EqualTo(1));
            yield return CheatTap(Key.F);
            yield return Delay(.6f);
            Assert.That(Get<Light>(player,"flashlight").enabled,Is.True,"Empty cheat flashlight refused to stay lit");
            Assert.That(Get<float>(charge,"Charge"),Is.Zero);
            Assert.That(Get<bool>(charge,"Depleted"),Is.False);
            Assert.That(Get<int>(charge,"Depletions"),Is.Zero);
            var firstTrap=PlaceTestTrapThroughInventory(true);Assert.That(firstTrap,Is.Not.Null);
            yield return Delay(.6f);
            var secondTrap=PlaceTestTrapThroughInventory(true);Assert.That(secondTrap,Is.Not.SameAs(firstTrap));
            var selected=Get<object>(ItemLoadout,"Selected");
            Call(ItemLoadout,"Select",System.Enum.Parse(selected.GetType(),"Snapper"));
            Assert.That(Get<bool>(snappers,"CanAim"),Is.True,"Empty infinite stock cannot aim");
            for(int i=0;i<2;i++)
            {
                var previous=Get<Component>(snappers,"LastThrown");
                yield return CheatTap(Key.Q);
                Assert.That(Get<Component>(snappers,"LastThrown"),Is.Not.Null.And.Not.SameAs(previous));
                Assert.That(Get<int>(snappers,"Count"),Is.Zero);
                yield return Delay(.6f);
            }
            Assert.That(CheatUseSelected("Incense"),Is.True);
            Assert.That(Get<Component>(rituals,"Smoke"),Is.Not.Null);
            Assert.That(Get<int>(rituals,"Incense"),Is.Zero);
            Assert.That(CheatUseSelected("Talisman"),Is.True);
            Assert.That(Get<int>(rituals,"Talismans"),Is.Zero);
            yield return Wait(()=>!Get<bool>(rituals,"Burning"),12,"Infinite stock accidentally made a talisman burn forever");
            Call(shell,"Pause");
            var checkpoint=Call(session,"CaptureCheckpoint");Call(checkpoint,"Validate");
            Assert.That(Get<int>(Get<object>(checkpoint,"ritualItems"),"incense"),Is.Zero);
            Call(shell,"Resume");yield return null;
            yield return CheatTap(Key.F2);
            Assert.That(Get<bool>(ItemLoadout,"Unlimited"),Is.False);
            Assert.That(Get<Light>(player,"flashlight").enabled,Is.False);
            Assert.That(Get<bool>(charge,"Depleted"),Is.True);
            Assert.That((bool)Call(snappers,"TryThrow"),Is.False);
            Assert.That((bool)Call(traps,"TryPlace"),Is.False);
            Assert.That((bool)Call(rituals,"UseTalisman"),Is.False);
            Assert.That(Get<int>(traps,"Count"),Is.Zero);
            root=Get<VisualElement>(One("GameShellView"),"Root");
            for(int i=0;i<4;i++)Assert.That(root.Q<Label>("item-wheel-count-"+i).text,Is.EqualTo("없음"));
        }

        [UnityTest,Timeout(120000)]
        public IEnumerator CheatF2PreservesSchoolStocksAndBatteryThenOffSpendsNormally()
        {
            Call(session,"CreateChapter");Begin();IsolateThreats();yield return null;
            var snappers=Get<Component>(player,"Firecrackers");var traps=Get<Component>(player,"Traps");
            var rituals=Get<Component>(player,"Rituals");var charge=Get<Component>(player,"FlashlightSystem");
            Call(snappers,"RestoreStock",3,0f);Call(traps,"Restore",2,0f);Call(rituals,"Restore",2,3,2);
            Call(charge,"Restore",90f,0,0);Get<Light>(player,"flashlight").enabled=true;
            yield return CheatTap(Key.F2);
            float stored=Get<float>(charge,"Charge");yield return Delay(.6f);
            Assert.That(Get<float>(charge,"Charge"),Is.EqualTo(stored));
            PlaceTestTrapThroughInventory(true);
            Assert.That(CheatUseSelected("Snapper"),Is.True);
            Assert.That(Get<int>(snappers,"Count"),Is.EqualTo(3));
            Assert.That(CheatUseSelected("Incense"),Is.True);
            Assert.That(Get<int>(rituals,"Incense"),Is.EqualTo(2));
            Assert.That(CheatUseSelected("Talisman"),Is.True);
            Assert.That(Get<int>(rituals,"Talismans"),Is.EqualTo(2));
            Assert.That(Get<int>(rituals,"Fragments"),Is.EqualTo(3));
            Call(rituals,"EndEffect");Call(rituals,"RestoreSmoke",.05f);yield return Delay(.65f);
            yield return Wait(()=>Components("FirecrackerProjectile").Length==0,10,"School save still has a live snapper effect");
            Call(shell,"Pause");Call(Call(session,"CaptureChapterCheckpoint"),"Validate");Call(shell,"Resume");
            yield return CheatTap(Key.F2);
            Assert.That(Get<bool>(ItemLoadout,"Unlimited"),Is.False);
            yield return Delay(.2f);
            Assert.That(Get<float>(charge,"Charge"),Is.LessThan(stored).And.GreaterThan(stored-1));
            Assert.That(CheatUseSelected("Snapper"),Is.True);
            Assert.That(Get<int>(snappers,"Count"),Is.EqualTo(2));
            PlaceTestTrapThroughInventory();
            Assert.That(Get<int>(traps,"Count"),Is.EqualTo(1));
            Assert.That(CheatUseSelected("Incense"),Is.True);
            Assert.That(Get<int>(rituals,"Incense"),Is.EqualTo(1));
            Assert.That(CheatUseSelected("Talisman"),Is.True);
            Assert.That(Get<int>(rituals,"Talismans"),Is.EqualTo(1));
        }

    }
}
