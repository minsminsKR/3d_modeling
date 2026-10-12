using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using static HappyToy.V2.CloudTests.RuntimeAccess;

namespace HappyToy.V2.CloudTests
{
    public sealed partial class CloudPlayModeTests
    {
        [UnityTest,Timeout(120000)]
        public IEnumerator StartLoadingRealTitleButtonFadesMenusApproachesDuringPreparationAndRevealsCorridorBeforeInput()
        {
            yield return RecoveryUiReady();Call(shell,"RestoreDefaultSettings");yield return null;
            var view=One("GameShellView");var root=Get<VisualElement>(view,"Root");var button=root.Q<Button>("begin");
            var target=root.panel.Pick(button.worldBound.center);var position=button.worldBound.center;
            using(var down=PointerDownEvent.GetPooled(new Event{type=EventType.MouseDown,mousePosition=position,button=0,clickCount=1}))target.SendEvent(down);
            yield return Delay(.04f);
            using(var up=PointerUpEvent.GetPooled(new Event{type=EventType.MouseUp,mousePosition=position,button=0,clickCount=1}))target.SendEvent(up);
            var title=One("CinematicTitleScene");float minimumMenu=1,maximumBlack=0,previousDistance=0;
            int preparingFrames=0,movingFrames=0;bool revealWhileGated=false;float minimumFloorGap=float.PositiveInfinity;
            float deadline=Time.realtimeSinceStartup+50;
            while(Get<bool>(shell,"CinematicLoading"))
            {
                Assert.That(Time.realtimeSinceStartup,Is.LessThan(deadline));
                Assert.That(Get<bool>(session,"InputAllowed"),Is.False,"Input became active before the reveal completed");
                minimumMenu=Mathf.Min(minimumMenu,Get<float>(shell,"TransitionMenuOpacity"));
                maximumBlack=Mathf.Max(maximumBlack,Get<float>(shell,"TransitionBlack"));
                float distance=Get<float>(title,"ApproachDistance");
                if(Get<bool>(shell,"CorridorPreparing"))
                {preparingFrames++;if(distance>previousDistance+.0001f)movingFrames++;}
                previousDistance=distance;
                if(RecoveryPage=="Title" && distance>.01f)minimumFloorGap=Mathf.Min(minimumFloorGap,Get<float>(title,"MovingFloorGap"));
                if(RecoveryPage=="Playing" && Get<float>(shell,"TransitionBlack")<.9f && Get<bool>(shell,"IsReloading"))revealWhileGated=true;
                yield return null;
            }
            Assert.That(preparingFrames,Is.GreaterThan(5));Assert.That(movingFrames,Is.GreaterThan(3),"The actual title froze during world preparation");
            Assert.That(minimumMenu,Is.LessThan(.01f));Assert.That(maximumBlack,Is.GreaterThan(.99f));
            Assert.That(previousDistance,Is.GreaterThan(2));Assert.That(minimumFloorGap,Is.GreaterThan(-.015f));
            Assert.That(revealWhileGated,Is.True);Assert.That(Get<bool>(session,"InputAllowed"),Is.True);
            Assert.That(RecoveryPage,Is.EqualTo("Playing"));Assert.That(Get<bool>(session,"CorridorMode"),Is.True);
            Assert.That(Get<float>(shell,"TransitionBlack"),Is.Zero);Assert.That(Time.timeScale,Is.EqualTo(1));
            AssertMainCorridor();
        }

        [UnityTest,Timeout(120000)]
        public IEnumerator DrawerRealEOpensAndClosesAtTouchingDistanceWithoutShovingPlayerAndRestoresCollisionPairs()
        {
            Call(session,"CreateCorridor",73);IsolateThreats();Begin();yield return null;yield return null;
            foreach(string id in new[]{"drawer-atmosphere-0","drawer-supply-0"})
            {
            var drawer=FurnitureDrawers.First(x=>Get<string>(x,"StableId")==id);
            var furniture=drawer.GetComponentInParent(RequireType("CorridorFurniturePlacement"));
            var front=drawer.GetComponentsInChildren<Collider>(true).Single(x=>x.name=="Drawer physical front and handle target");
            var outward=furniture.transform.TransformDirection(Vector3.back).normalized;
            var controller=player.GetComponent<CharacterController>();
            Vector3 close=front.bounds.center+outward*(controller.radius+.022f+.035f);close.y=.03f;
            PlacePlayer(close);yield return null;yield return null;
            yield return FurniturePressE(DrawerInteraction(drawer));
            yield return Wait(()=>Get<bool>(drawer,"IsOpen")&&Get<bool>(drawer,"AtRequestedPose"),3,"Touching player still blocked drawer opening");
            Assert.That(Get<float>(drawer,"Travel"),Is.EqualTo(.3f).Within(.001f));
            Assert.That(Vector3.Distance(player.transform.position,close),Is.LessThan(.06f),"Opening shoved the player");
            Assert.That(Physics.GetIgnoreCollision(front,controller),Is.True,"Overlapping moving tray should not trap the controller");
            yield return FurniturePressE(DrawerInteraction(drawer));
            yield return Wait(()=>!Get<bool>(drawer,"IsOpen")&&Get<bool>(drawer,"AtRequestedPose"),3,"Touching player still blocked drawer closing");
            PlacePlayer(close+outward*1.2f);yield return null;yield return null;
            Assert.That(Physics.GetIgnoreCollision(front,controller),Is.False,"Tray collision pair was not restored after the player cleared it");
            // The furniture body continues to collide normally; only its tray/controller pairs change.
            foreach(var fixedPart in furniture.GetComponentsInChildren<Collider>().Where(x=>!x.transform.IsChildOf(drawer.transform)&&!x.isTrigger))
                Assert.That(Physics.GetIgnoreCollision(fixedPart,controller),Is.False);
            }
        }
    }
}
