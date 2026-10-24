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
        static double LadyFrameMean(Texture2D frame)=>frame.GetPixels32().Average(p=>(.2126*p.r+.7152*p.g+.0722*p.b)/255);
        [UnityTest,Timeout(180000)]
        public IEnumerator LadyFinaleRealCameraNeedsTorchAndRestartRestoresNormalLighting()
        {
            yield return LadyPrepare();LadyCollect(0,3);
            var graphics=session.GetComponent(RequireType("GraphicsLightingPresentation"));
            var camera=Get<Camera>(player,"eyes");var torch=Get<Light>(player,"flashlight");
            ((Behaviour)player).enabled=false;torch.enabled=false;
            camera.transform.localRotation=Quaternion.Euler(30,0,0);
            yield return Delay(.5f);
            var normal=EntranceRitualFrame(camera,640,360);double before=LadyFrameMean(normal);
            try{CloudExperienceTests.Artifact("lady-finale-normal-torch-off.png",normal.EncodeToPNG());}
            finally{Object.Destroy(normal);}
            Assert.That(Get<bool>(graphics,"FinaleBlackout"),Is.False);
            LadyCollect(3,4);Call(shell,"Pause");
            // A new light created after the transition must not reopen a light
            // pool. Existing fixture flicker/eye writers must also stay black.
            var late=new GameObject("late finale light fixture");var light=late.AddComponent<Light>();
            light.transform.position=camera.transform.position;light.intensity=50;light.range=20;
            Texture2D dark=null,lit=null;
            try
            {
                dark=EntranceRitualFrame(camera,640,360);double after=LadyFrameMean(dark);
                CloudExperienceTests.Artifact("lady-finale-torch-off.png",dark.EncodeToPNG());
                Assert.That(before,Is.GreaterThan(.003),"Normal cavern view was already black; comparison is invalid");
                Assert.That(after,Is.LessThan(.001),"Residual environment/emission/reflection light survives without the flashlight");
                Assert.That(after,Is.LessThan(before*.05),"Finale did not darken the real production view");
                Assert.That(light.enabled,Is.False,"A new light bypassed the blackout");
                Assert.That(Object.FindObjectsByType<Light>(FindObjectsSortMode.None).All(l=>l==torch||!l.isActiveAndEnabled),Is.True);
                Assert.That(RenderSettings.ambientIntensity,Is.Zero);Assert.That(RenderSettings.reflectionIntensity,Is.Zero);
                torch.enabled=true;lit=EntranceRitualFrame(camera,640,360);
                CloudExperienceTests.Artifact("lady-finale-torch-on.png",lit.EncodeToPNG());
                // The narrow beam occupies a small fraction of the cavern view.
                // Require measurable energy AND a visible pool, not a raised
                // whole-frame exposure that would defeat this encounter.
                Assert.That(LadyFrameMean(lit),Is.GreaterThan(after+.001),"Flashlight no longer illuminates the actual world");
                Assert.That(lit.GetPixels32().Count(p=>p.r>20||p.g>20||p.b>20),Is.GreaterThan(1000));
                Assert.That(torch.enabled,Is.True,"Blackout took ownership of the player's torch switch");
            }
            finally{Object.Destroy(late);if(dark)Object.Destroy(dark);if(lit)Object.Destroy(lit);}
            var previous=session;Call(shell,"Restart",false);yield return RecoveryRebind(previous);
            Call(session,"CreateCorridor",73);IsolateThreats();Begin();yield return null;yield return null;
            graphics=session.GetComponent(RequireType("GraphicsLightingPresentation"));
            Assert.That(Get<bool>(graphics,"FinaleBlackout"),Is.False,"Finale scope leaked into a fresh game");
            Assert.That(RenderSettings.ambientIntensity,Is.GreaterThan(0));
            camera=Get<Camera>(player,"eyes");torch=Get<Light>(player,"flashlight");
            ((Behaviour)player).enabled=false;torch.enabled=false;camera.transform.localRotation=Quaternion.Euler(30,0,0);
            var restored=EntranceRitualFrame(camera,640,360);
            try{Assert.That(LadyFrameMean(restored),Is.GreaterThan(.003),"Normal lighting did not return after scene restart");}
            finally{Object.Destroy(restored);}
        }
    }
}
