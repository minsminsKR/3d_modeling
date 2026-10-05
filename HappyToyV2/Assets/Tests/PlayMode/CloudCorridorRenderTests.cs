using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;
using static HappyToy.V2.CloudTests.RuntimeAccess;

namespace HappyToy.V2.CloudTests
{
    public sealed partial class CloudPlayModeTests
    {
        [UnityTest, Timeout(60000)]
        public IEnumerator CorridorRealCameraShowsFloorSurfaceAndLightingDiagnostics()
        {
            Call(session,"CreateCorridor",73); Begin(); yield return Delay(.3f);
            var camera=Get<Camera>(player,"eyes"); var run=Get<Component>(session,"Corridor");
            Debug.Log("CORRIDOR_RENDER camera="+camera.transform.position+" forward="+camera.transform.forward+" mask="+camera.cullingMask);
            foreach(var light in run.GetComponentsInChildren<Light>())
                if(Vector3.Distance(light.transform.position,camera.transform.position)<9)
                    Debug.Log("CORRIDOR_RENDER light="+light.name+" at="+light.transform.position+" active="+light.isActiveAndEnabled+" intensity="+light.intensity+" range="+light.range+" mask="+light.cullingMask);
            var hits=Physics.RaycastAll(player.transform.position+Vector3.up*1.6f,Vector3.down,4,~0,QueryTriggerInteraction.Ignore);
            foreach(var hit in hits.OrderBy(x=>x.distance)) Debug.Log("CORRIDOR_RENDER below="+hit.collider.name+" at="+hit.point+" root="+hit.collider.transform.root.name);
            var floor=run.GetComponentsInChildren<MeshRenderer>().First(x=>x.enabled&&x.sharedMaterial&&x.sharedMaterial.name.StartsWith("Worn wooden floor"));
            Debug.Log("CORRIDOR_RENDER floor="+floor.name+" bounds="+floor.bounds+" texture="+floor.sharedMaterial.mainTexture.name+" color="+floor.sharedMaterial.color+" shader="+floor.sharedMaterial.shader.name);
            Call(shell,"Pause"); camera.transform.localRotation=Quaternion.Euler(28,0,0);
            var target=new RenderTexture(1280,720,24); target.Create();
            try
            {
                RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest { destination=target });
                var image=CloudExperienceTests.Read(target);
                try { CloudExperienceTests.Artifact("corridor-floor-render-down.png",image.EncodeToPNG()); Assert.That(CloudExperienceTests.HasContent(image),Is.True); }
                finally { Object.Destroy(image); }
            }
            finally { target.Release(); Object.Destroy(target); }
        }
    }
}
