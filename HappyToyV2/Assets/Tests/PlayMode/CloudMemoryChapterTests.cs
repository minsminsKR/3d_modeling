using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using static HappyToy.V2.CloudTests.RuntimeAccess;

namespace HappyToy.V2.CloudTests
{
    public sealed partial class CloudPlayModeTests
    {
        IEnumerator ChapterAwaitAppearance()
        {
            var shots=Get<Component>(Get<Component>(session,"Chapter"),"FirstAppearances");
            yield return Wait(()=>!Get<bool>(shots,"CameraOwned"),30,"School appearance did not return player control");
        }
        [UnityTest, Timeout(90000)]
        public IEnumerator ChapterMannequinMovesOnlyWhenBothUnseenAndFlashlightOn()
        {
            Call(shell,"BeginChapter");yield return null;
            var chapter=Get<Component>(session,"Chapter");var memories=Get<Component[]>(chapter,"Memories");
            Call(memories[0],"Use",player);yield return ChapterAwaitAppearance();
            Call(memories[1],"Use",player);yield return ChapterAwaitAppearance();
            // Isolate the gaze/light rule, keeping the production mannequin and navigation.
            Get<Component>(chapter,"Cyclopse").gameObject.SetActive(false);((Behaviour)player).enabled=false;
            var mannequin=Get<Component>(chapter,"Mannequin");var camera=Get<Camera>(player,"eyes");var light=Get<Light>(player,"flashlight");
            PlacePlayer(mannequin.transform.position+Vector3.forward*4);light.enabled=true;
            camera.transform.rotation=Quaternion.LookRotation(mannequin.transform.position+Vector3.up*1.25f-camera.transform.position);
            yield return Wait(()=>Get<bool>(mannequin,"Released"),5,"Second-memory mannequin never finished its first sight turn");
            var at=mannequin.transform.position;yield return Delay(.4f);
            Assert.That(Get<bool>(mannequin,"Observed"),Is.True);Assert.That(Vector3.Distance(at,mannequin.transform.position),Is.LessThan(.025f));
            camera.transform.rotation=Quaternion.LookRotation(Vector3.left);light.enabled=false;yield return Delay(.5f);
            Assert.That(Get<bool>(mannequin,"Observed"),Is.False);Assert.That(Get<bool>(mannequin,"Moving"),Is.False);
            Assert.That(Vector3.Distance(at,mannequin.transform.position),Is.LessThan(.025f),"Unseen mannequin moved with the light off");
            light.enabled=true;yield return Delay(.65f);
            Assert.That(Get<bool>(mannequin,"Moving"),Is.True);Assert.That(Vector3.Distance(at,mannequin.transform.position),Is.GreaterThan(.08f));
            camera.transform.rotation=Quaternion.LookRotation(mannequin.transform.position+Vector3.up*1.25f-camera.transform.position);yield return null;
            at=mannequin.transform.position;yield return Delay(.3f);
            Assert.That(Get<bool>(mannequin,"Observed"),Is.True);Assert.That(Get<bool>(mannequin,"Moving"),Is.False);
            Assert.That(Vector3.Distance(at,mannequin.transform.position),Is.LessThan(.025f),"Gaze did not stop pursuit");
        }
        [UnityTest, Timeout(120000)]
        public IEnumerator ChapterRealMenusRestartDeathAndTitleIntoAnEmptyFirstMemorySchool()
        {
            yield return RecoveryUiReady();yield return RecoveryClick("begin-school");
            Assert.That(Get<bool>(session,"ChapterMode"),Is.True);var original=session;
            yield return RecoveryPulse(UnityEngine.InputSystem.Key.Escape);yield return RecoveryClick("restart");yield return RecoveryRebind(original);
            Assert.That(Get<bool>(session,"ChapterMode"),Is.True);Assert.That(Get<int>(session,"RecordsRecovered"),Is.Zero);
            Assert.That(Components("StalkerBrain").All(x=>!x.gameObject.activeInHierarchy),Is.True);
            // Result rendering/retry probe; genuine fatal contact is evidenced by input run 02.
            Call(session,"Finish",false);yield return null;original=session;yield return RecoveryClick("restart");yield return RecoveryRebind(original);
            Assert.That(Get<bool>(session,"ChapterMode"),Is.True);Assert.That(RecoveryPage,Is.EqualTo("Playing"));
            original=session;yield return RecoveryPulse(UnityEngine.InputSystem.Key.Escape);yield return RecoveryClick("title");yield return RecoveryRebind(original);
            Assert.That(RecoveryPage,Is.EqualTo("Title"));yield return RecoveryClick("begin-school");Assert.That(Get<bool>(session,"ChapterMode"),Is.True);
            Assert.That(Get<int>(session,"RecordsRecovered"),Is.Zero);Assert.That(Components("StalkerBrain").All(x=>!x.gameObject.activeInHierarchy),Is.True);
            Call(shell,"Pause");Call(shell,"Journal");yield return null;
            Assert.That(Get< UnityEngine.UIElements.VisualElement>(One("GameShellView"),"Root").Q<UnityEngine.UIElements.Label>("record-4"),Is.Not.Null);
        }
        [UnityTest, Timeout(120000)]
        public IEnumerator ChapterStartsEmptyAndUsesFiveReachableMemoriesAcrossThreeFloors()
        {
            Call(shell,"BeginChapter");yield return null;yield return null;
            Assert.That(Get<string>(shell,"ReloadError"),Is.Empty);
            Assert.That(Get<bool>(session,"ChapterMode"),Is.True);
            var chapter=Get<Component>(session,"Chapter");
            var memories=Get<Component[]>(chapter,"Memories");
            Assert.That(memories.Length,Is.EqualTo(5));
            Assert.That(Vector3.Distance(player.transform.position,memories[0].transform.position),Is.LessThan(2));
            Assert.That(memories.Select(x=>Mathf.RoundToInt(x.transform.position.y-1)).ToArray(),Is.EqualTo(new[]{0,0,5,5,-5}));
            Assert.That(Components("StalkerBrain").All(x=>!x.gameObject.activeInHierarchy),Is.True,"An actor was present before the first memory");
            Assert.That(Get<Component>(chapter,"Mannequin").gameObject.activeSelf,Is.False);
            Assert.That(Get<Component>(chapter,"Mask").gameObject.activeSelf,Is.False);
            Assert.That(Get<bool>(Get<Component>(chapter,"Portrait"),"Triggered"),Is.False);
            Assert.That(Get<bool>(Get<Component>(chapter,"Nursery"),"Triggered"),Is.False);
            Call(memories[1],"Use",player);Assert.That(Get<int>(chapter,"Recovered"),Is.Zero);
            foreach(var door in Components("Interactable").Where(x=>Get<object>(x,"kind").ToString()=="Door")) Call(door,"OpenForPursuer");
            yield return Delay(2);
            Assert.That(NavMesh.SamplePosition(player.transform.position,out var start,1,NavMesh.AllAreas),Is.True);
            foreach(var memory in memories)
            {
                float floor=memory==memories[4]?-5:memory==memories[2]||memory==memories[3]?5:0;
                var approach=memory.transform.position+Vector3.back*.85f;approach.y=floor;
                Assert.That(NavMesh.SamplePosition(approach,out var hit,1.3f,NavMesh.AllAreas),Is.True,"No approach: "+memory.name);
                Assert.That(Mathf.Abs(hit.position.y-floor),Is.LessThan(.2f));
                var path=new NavMeshPath();Assert.That(NavMesh.CalculatePath(start.position,hit.position,NavMesh.AllAreas,path),Is.True);
                Assert.That(path.status,Is.EqualTo(NavMeshPathStatus.PathComplete),"Disconnected stairs or memory: "+memory.name);
            }
            var atmosphere=session.GetComponent(RequireType("ChapterAtmosphere"));
            Assert.That(Get<int>(atmosphere,"WallTreatments"),Is.GreaterThan(30));
            Assert.That(Get<int>(atmosphere,"DressingPieces"),Is.GreaterThan(200));
            // Controlled render tour for art review. This is not an input survival claim.
            var camera=Get<Camera>(player,"eyes");
            ((Behaviour)player).enabled=false;
            var views=new[]{new[]{new Vector3(-7.8f,.02f,0),new Vector3(0,1.4f,.4f)},new[]{new Vector3(25.3f,5.02f,22.8f),new Vector3(26.8f,6.7f,32.4f)},new[]{new Vector3(13.8f,-4.98f,-23),new Vector3(13.8f,-3.8f,-31)},new[]{new Vector3(34.6f,5.02f,31.8f),new Vector3(29.8f,6.6f,33.5f)}};
            for(int index=0;index<views.Length;index++)
            {
                var view=views[index];
                PlacePlayer(view[0]);camera.transform.rotation=Quaternion.LookRotation(view[1]-camera.transform.position);yield return Delay(.5f);
                var render=new RenderTexture(1280,720,24);render.Create();
                RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=render});
                var frame=CloudExperienceTests.Read(render);
                string name=new[]{"chapter-entrance.png","chapter-upper.png","chapter-basement.png","chapter-portrait.png"}[index];
                CloudExperienceTests.Artifact(name,frame.EncodeToPNG());Object.Destroy(frame);render.Release();Object.Destroy(render);
            }
        }
        [UnityTest, Timeout(120000)]
        public IEnumerator ChapterGatesEveryActorAndRequiresTheRealPortraitAndNurseryReveals()
        {
            Call(shell,"BeginChapter");yield return null;
            var chapter=Get<Component>(session,"Chapter");var memories=Get<Component[]>(chapter,"Memories");
            var cyclopse=Get<Component>(chapter,"Cyclopse");var mannequin=Get<Component>(chapter,"Mannequin");
            var mask=Get<Component>(chapter,"Mask");var portrait=Get<Component>(chapter,"Portrait");var nursery=Get<Component>(chapter,"Nursery");
            Call(memories[0],"Use",player);Assert.That(Get<int>(chapter,"Recovered"),Is.EqualTo(1));
            Assert.That(Get<bool>(player,"Paused"),Is.True);yield return ChapterAwaitAppearance();
            Assert.That(cyclopse.gameObject.activeSelf,Is.True);
            Assert.That(Get<bool>(Get<Component>(Get<Component>(chapter,"FirstAppearances"),"CyclopseIntro"),"Completed"),Is.True);
            Assert.That(mannequin.gameObject.activeSelf,Is.False);Assert.That(mask.gameObject.activeSelf,Is.False);
            Call(memories[1],"Use",player);Assert.That(Get<int>(chapter,"Recovered"),Is.EqualTo(2));Assert.That(mannequin.gameObject.activeSelf,Is.True);
            yield return ChapterAwaitAppearance();
            Assert.That(Get<int>(mannequin,"activationStep"),Is.EqualTo(2));Assert.That(mask.gameObject.activeSelf,Is.False);
            // Controlled encounter probe; no isolated actor result is presented as a full run.
            ((Behaviour)player).enabled=false;
            PlacePlayer(new Vector3(25.8f,5.02f,24.5f));
            Call(memories[2],"Use",player);yield return null;
            Assert.That(Get<int>(chapter,"Recovered"),Is.EqualTo(3));Assert.That(mask.gameObject.activeSelf,Is.True);
            Assert.That(mask.transform.position.y,Is.InRange(4.8f,5.2f));
            PlacePlayer(new Vector3(31.3f,5.02f,32.5f));
            var camera=Get<Camera>(player,"eyes");camera.transform.rotation=Quaternion.LookRotation(Vector3.forward);
            Call(memories[3],"Use",player);Assert.That(Get<int>(chapter,"Recovered"),Is.EqualTo(3));Assert.That(Get<bool>(portrait,"Triggered"),Is.True);
            Call(shell,"Pause");float elapsed=Get<float>(portrait,"RevealElapsed");yield return Delay(.25f);
            Assert.That(Get<float>(portrait,"RevealElapsed"),Is.EqualTo(elapsed));Call(shell,"Resume");
            // Keep outside attack range while the actual authored animation plays.
            PlacePlayer(new Vector3(34.7f,5.02f,32.2f));
            var target=Get<Vector3>(portrait,"spawn")+Vector3.up*.9f;camera.transform.rotation=Quaternion.LookRotation(target-camera.transform.position);
            yield return Wait(()=>Get<bool>(portrait,"ChapterWitnessed"),8,"Portrait became collectable without being visible");
            yield return Wait(()=>Get<bool>(portrait,"Completed"),8,"Authored portrait reveal never completed");
            Call(memories[3],"Use",player);Assert.That(Get<int>(chapter,"Recovered"),Is.EqualTo(4));
            Assert.That(Get<bool>(nursery,"Triggered"),Is.False);Call(memories[4],"Use",player);Assert.That(Get<int>(chapter,"Recovered"),Is.EqualTo(4));
            PlacePlayer(new Vector3(13.8f,-4.98f,-22));yield return null;
            Assert.That(Get<bool>(nursery,"Triggered"),Is.True);
            Call(shell,"Pause");elapsed=Get<float>(nursery,"RevealElapsed");yield return Delay(.25f);
            Assert.That(Get<float>(nursery,"RevealElapsed"),Is.EqualTo(elapsed));Call(shell,"Resume");
            yield return Wait(()=>Get<bool>(nursery,"Released"),8,"Baby reveal never reached crawling phase");
            Call(memories[4],"Use",player);Assert.That(Get<int>(chapter,"Recovered"),Is.EqualTo(5));
            Assert.That(Get<bool>(session,"EncountersResolved"),Is.False,"Fifth memory prematurely erased every actor");
            Call(session,"TryEscape");Assert.That(Get<bool>(session,"Escaped"),Is.True);
        }
    }
}
