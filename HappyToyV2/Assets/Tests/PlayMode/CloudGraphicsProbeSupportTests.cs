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
        [UnityTest,Timeout(60000)]
        public IEnumerator NativeGraphicsSubjectsExcludeThePreservedSchoolDuringCorridorCapture()
        {
            Call(session,"CreateCorridor",73);Begin();yield return null;yield return null;
            var run=Get<Component>(session,"Corridor");var world=Get<Component>(run,"Presentation").transform;
            var all=Components("Interactable");
            Assert.That(all.Any(item=>Get<object>(item,"kind").ToString()=="Door"&&!item.transform.IsChildOf(world)),Is.True,"Fixture must contain the preserved school doors that caused the native scope failure");
            var actual=((IEnumerable)Call(RequireType("GraphicsPresentationAudit"),"CorridorItems",session)).Cast<Component>().ToArray();
            Assert.That(actual.Length,Is.GreaterThan(0));Assert.That(actual.All(item=>item.transform.IsChildOf(world)),Is.True);
            foreach(var kind in new[]{"Door","HidingPlace","FlashlightBattery","CorridorMemory"})
                Assert.That(actual.Any(item=>Get<object>(item,"kind").ToString()==kind),Is.True,"Actual corridor subject missing: "+kind);
            Debug.Log("HAPPYTOY_GRAPHICS_SUBJECT_SCOPE_PASS actual corridor subjects exclude retained authored school doors");
        }
        [UnityTest,Timeout(60000)]
        public IEnumerator SchoolReflectionPlansUseActualThreeFloorSupportAndPauseOwnsRefresh()
        {
            Call(session,"CreateChapter");Begin();yield return null;yield return null;
            var graphics=session.GetComponent(RequireType("GraphicsLightingPresentation"));
            foreach(var feet in new[]{new Vector3(0,.033f,0),new Vector3(8.8f,.033f,0),new Vector3(26,5.033f,26),new Vector3(14,-4.967f,-28)})
            {
                var plan=Call(RequireType("GraphicsLightingPresentation"),"PlanSchoolReflection",feet);
                var floor=Get<Collider>(plan,"Floor");Assert.That(floor,Is.Not.Null,"Actual nearby authored support missing at "+feet);
                Assert.That(floor.name.ToLowerInvariant(),Does.Contain("floor"));
                var box=Get<Bounds>(plan,"Box");float height=Get<float>(plan,"SupportY");
                Assert.That(Mathf.Abs(height-feet.y),Is.LessThan(.1f));Assert.That(box.center.y,Is.EqualTo(height+1.5f).Within(.001f));
                Assert.That(box.Contains(new Vector3(feet.x,height+1.5f,feet.z)),Is.True,"Clamped large-floor box lost the observer");
                Assert.That(box.size.x,Is.InRange(2f,16f));Assert.That(box.size.z,Is.InRange(2f,16f));
            }
            var probe=Get<ReflectionProbe>(graphics,"OwnedProbe");var oldCenter=probe.transform.position;int requests=Get<int>(graphics,"ReflectionCaptures");
            Call(shell,"Pause");PlacePlayer(new Vector3(26,5.033f,26),false);yield return new WaitForSecondsRealtime(3.3f);
            Assert.That(probe.transform.position,Is.EqualTo(oldCenter));Assert.That(Get<int>(graphics,"ReflectionCaptures"),Is.EqualTo(requests),"Paused mode issued new GPU probe work");
            Call(RequireType("GraphicsLightingPresentation"),"EndMode",session);yield return null;yield return null;
            Assert.That(!probe,Is.True,"Owned probe survives teardown");
            Debug.Log("HAPPYTOY_GRAPHICS_SUPPORT_PROBE_PASS actual three-floor support/observer box/pause/owned teardown; GPU completion remains native-only");
        }
    }
}
