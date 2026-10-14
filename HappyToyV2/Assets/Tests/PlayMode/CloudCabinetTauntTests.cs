using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using static HappyToy.V2.CloudTests.RuntimeAccess;

namespace HappyToy.V2.CloudTests
{
    public sealed partial class CloudPlayModeTests
    {
        void AssertExpressionEyes(Component actor,bool glowing)
        {
            var eye=actor.GetComponentInChildren(RequireType("MonsterRedEyes")) as Component;
            foreach(var renderer in Get<IReadOnlyList<Renderer>>(eye,"EmissionRenderers"))
                foreach(int slot in (int[])Call(eye,"GetAffectedSlots",renderer))
                {
                    var material=renderer.sharedMaterials[slot];
                    Assert.That(material.GetTexture("_EmissionMap"),Is.Not.Null);
                    float red=EyeBlockEmission(renderer,slot).r;
                    Assert.That(red,glowing?Is.GreaterThan(.5f):Is.LessThan(.0001f));
                }
        }
        IEnumerator RepeatVisibleCabinetEntry(string key)
        {
            IsolateThreats();Begin();((Behaviour)player).enabled=false;
            var cabinet=SchoolCabinet();var entrance=Get<Transform>(cabinet,"outside").position;
            PlacePlayer(entrance);var actor=CabinetActor(key,cabinet);yield return CabinetActorPursuing(key,actor);
            int samples=0;Set(player,"HidingRandomSample",(Func<float>)(()=>{samples++;return 0;}));
            for(int entry=1;entry<=3;entry++)
            {
                actor.transform.rotation=Quaternion.LookRotation(entrance-actor.transform.position);Physics.SyncTransforms();
                Assert.That((bool)Call(actor,"CanWitnessHiding",player,entrance),Is.True,key+" must actually see this entry");
                Call(cabinet,"Use",player);
                Assert.That(Get<int>(player,"CabinetTauntCount"),Is.EqualTo(entry),key);
                Assert.That(Get<bool>(player,"HidingForcedCapture"),Is.EqualTo(entry==3));
                if(entry==3)break;
                Assert.That(Get<object>(player,"HidingOutcome").ToString(),Is.EqualTo("Survived"));
                yield return Wait(()=>Get<string>(actor,"CabinetStage")=="Inspect",10,key+" skipped its calm scan at entry "+entry,
                    ()=>"stage="+Get<string>(actor,"CabinetStage")+" position="+actor.transform.position+" entrance="+entrance);
                yield return Wait(()=>Get<string>(actor,"CabinetStage")=="Retreat",4,key+" never turned away");
                Call(cabinet,"Use",player);Assert.That(Get<bool>(player,"Hidden"),Is.False);
                // Immediately re-enter while the original encounter is still
                // retreating, without manufacturing a new Chase state or actor.
            }
            Assert.That(Get<object>(player,"HidingOutcome").ToString(),Is.EqualTo("Defeated"));
            Assert.That(Get<string>(actor,"CabinetStage"),Is.EqualTo("Rush"),"Third overt entry must skip pretending to search");
            int id=Get<int>(player,"HidingEntryId");Call(cabinet,"Use",player);
            Assert.That(Get<bool>(player,"Hidden"),Is.True,"Exit/re-entry spam escaped a committed capture");
            Assert.That(Get<int>(player,"HidingEntryId"),Is.EqualTo(id));Assert.That(samples,Is.EqualTo(3));
            Call(shell,"Pause");yield return Delay(.2f);Assert.That(Get<bool>(session,"Finished"),Is.False);Call(shell,"Resume");
            yield return Wait(()=>Get<bool>(actor,"AttackActive"),8,key+" did not physically reach the third entry");
            yield return CabinetMustStayAliveFor(.3f);
            yield return Wait(()=>Get<bool>(session,"Finished"),3,key+" failed to commit the physical third strike");
        }
        [UnityTest,Timeout(60000)] public IEnumerator CyclopseThirdOvertCabinetEntryCannotRerollOrEscape() {yield return RepeatVisibleCabinetEntry("Cyclopse");}
        [UnityTest,Timeout(60000)] public IEnumerator UncatThirdOvertCabinetEntryCannotRerollOrEscape() {yield return RepeatVisibleCabinetEntry("Uncat");}
        [UnityTest,Timeout(60000)] public IEnumerator BabyThirdOvertCabinetEntryCannotRerollOrEscape() {yield return RepeatVisibleCabinetEntry("Baby");}
        [UnityTest,Timeout(60000)] public IEnumerator HwacatThirdOvertCabinetEntryCannotRerollOrEscape() {yield return RepeatVisibleCabinetEntry("Hwacat");}
        [UnityTest,Timeout(60000)] public IEnumerator MaskThirdOvertCabinetEntryCannotRerollOrEscape() {yield return RepeatVisibleCabinetEntry("Mask");}
        [UnityTest,Timeout(60000)] public IEnumerator MannequinThirdOvertCabinetEntryCannotRerollOrEscape() {yield return RepeatVisibleCabinetEntry("Mannequin");}
        [UnityTest,Timeout(60000)] public IEnumerator CabinetTauntsPersistThroughJsonRestoreAndQuietUseCannotEraseThem()
        {
            IsolateThreats();Begin();((Behaviour)player).enabled=false;var cabinet=SchoolCabinet();
            var entrance=Get<Transform>(cabinet,"outside").position;PlacePlayer(entrance);var actor=CabinetActor("Cyclopse",cabinet);
            Set(player,"HidingRandomSample",(Func<float>)(()=>0));
            for(int count=1;count<=2;count++)
            {
                Call(cabinet,"Use",player);Assert.That(Get<int>(player,"CabinetTauntCount"),Is.EqualTo(count));
                actor.gameObject.SetActive(false);Call(cabinet,"Use",player);Assert.That(Get<bool>(player,"Hidden"),Is.False);
                if(count==1)actor=CabinetActor("Cyclopse",cabinet);
            }
            var progress=Call(player,"CaptureProgress");string json=JsonUtility.ToJson(progress);
            Assert.That(json,Does.Contain("cabinetTaunts"));Call(player,"RestoreChapterProgress",JsonUtility.FromJson(json,progress.GetType()));
            // A quiet in/out while that monster is disabled cannot launder its memory.
            Call(cabinet,"Use",player);Assert.That(Get<object>(player,"HidingOutcome").ToString(),Is.EqualTo("Quiet"));Call(cabinet,"Use",player);
            actor=CabinetActor("Cyclopse",cabinet);Call(cabinet,"Use",player);
            Assert.That(Get<bool>(player,"HidingForcedCapture"),Is.True);Assert.That(Get<int>(player,"CabinetTauntCount"),Is.EqualTo(3));
            yield break;
        }
        [UnityTest,Timeout(60000)] public IEnumerator KnownCabinetCannotBeRerolledByEnteringBeforePatrolReacquiresChase()
        {
            IsolateThreats();Begin();((Behaviour)player).enabled=false;var cabinet=SchoolCabinet();
            var entrance=Get<Transform>(cabinet,"outside").position;PlacePlayer(entrance);
            Set(player,"HidingRandomSample",(Func<float>)(()=>0));
            Component actor=null;
            for(int entry=1;entry<=3;entry++)
            {
                actor=CabinetActor("Cyclopse",cabinet);
                if(entry==3)Set(actor,"state","Patrol"); // No extra Update to reacquire awareness.
                Assert.That((bool)Call(actor,"CanWitnessHiding",player,entrance),Is.True);
                Call(cabinet,"Use",player);Assert.That(Get<int>(player,"CabinetTauntCount"),Is.EqualTo(entry));
                if(entry<3){actor.gameObject.SetActive(false);Call(cabinet,"Use",player);Assert.That(Get<bool>(player,"Hidden"),Is.False);}
            }
            Assert.That(Get<bool>(player,"HidingForcedCapture"),Is.True);Assert.That(Get<string>(actor,"CabinetStage"),Is.EqualTo("Rush"));
            Call(cabinet,"Use",player);Assert.That(Get<bool>(player,"Hidden"),Is.True);
            yield break;
        }
    }
}
