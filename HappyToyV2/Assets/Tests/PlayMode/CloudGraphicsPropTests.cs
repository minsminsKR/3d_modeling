using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using static HappyToy.V2.CloudTests.RuntimeAccess;

namespace HappyToy.V2.CloudTests
{
    public sealed partial class CloudPlayModeTests
    {
        IEnumerator GraphicsPropAimOnly(Component target)
        {
            var previous=Mouse.current;var mouse=InputSystem.AddDevice<Mouse>();
            try
            {
                float deadline=Time.realtimeSinceStartup+5;
                while(Get<Component>(player,"Focus")!=target && Time.realtimeSinceStartup<deadline)
                {
                    var eyes=Get<Camera>(player,"eyes");var direction=target.GetComponent<Collider>().bounds.center-eyes.transform.position;
                    float yaw=Mathf.Atan2(direction.x,direction.z)*Mathf.Rad2Deg;
                    float pitch=Mathf.Clamp(-Mathf.Atan2(direction.y,new Vector2(direction.x,direction.z).magnitude)*Mathf.Rad2Deg,-77,77);
                    var pixels=new Vector2(Mathf.Clamp(Mathf.DeltaAngle(player.transform.eulerAngles.y,yaw),-40,40),
                        -Mathf.Clamp(Mathf.DeltaAngle(eyes.transform.localEulerAngles.x,pitch),-30,30))/Get<float>(player,"sensitivity");
                    InputSystem.QueueDeltaStateEvent(mouse.delta,pixels);yield return null;
                }
                Assert.That(Get<Component>(player,"Focus"),Is.SameAs(target));
            }
            finally {if(mouse.added)InputSystem.RemoveDevice(mouse);if(previous!=null && previous.added)previous.MakeCurrent();}
        }
        [UnityTest,Timeout(90000)]
        public IEnumerator RealModeledCandleKeepsActualInteractionPhysicsPauseAndPhotographicFlame()
        {
            Call(session,"CreateCorridor",73);IsolateThreats();Begin();yield return null;
            var target=LightTargets("Candle")[0];var candle=target.GetComponent(RequireType("WaymarkCandle"));
            var box=target.GetComponent<BoxCollider>();Assert.That(box.size,Is.EqualTo(new Vector3(.3f,.35f,.25f)));
            Assert.That(box.center,Is.EqualTo(Vector3.up*.08f));
            Assert.That(target.GetComponentsInChildren<Collider>(true).Length,Is.EqualTo(1),"Modeled visuals added a physical obstacle");
            var meshes=target.GetComponentsInChildren<MeshFilter>(true);Assert.That(meshes.Length,Is.EqualTo(2));
            Assert.That(meshes.Count(f=>Enumerable.Range(0,f.sharedMesh.subMeshCount).Sum(i=>(int)f.sharedMesh.GetIndexCount(i)/3)>3000),Is.EqualTo(1),"Real wax/tray mesh absent");
            var fire=Get<Transform>(candle,"Flame");Assert.That(fire.gameObject.activeSelf,Is.False);
            Assert.That(fire.GetComponentInChildren<MeshFilter>(true).sharedMesh.vertexCount,Is.LessThan(200),"Old sphere substituted for flame ribbons");
            var fireMaterial=fire.GetComponentInChildren<MeshRenderer>(true).sharedMaterial;
            Assert.That(fireMaterial.shader.name,Is.EqualTo("HappyToy/GraphicsUpgrade/CandleFlame"));
            Assert.That(fireMaterial.GetTexture("_FlameTex").name,Is.EqualTo("candle-flame-v3"));
            var camera=Get<Camera>(player,"eyes");PlacePlayer(LightApproach(target));yield return null;
            yield return GraphicsPropAimOnly(target);
            var image=SchoolCameraFrame(camera,out _,out _,out _);
            try {CloudExperienceTests.Artifact("graphics-candle-v3-unlit.png",image.EncodeToPNG());}
            finally {Object.Destroy(image);}
            yield return LightAim(target);
            Assert.That(Get<bool>(candle,"Lit"),Is.True);Assert.That(Get<int>(candle,"Ignitions"),Is.EqualTo(1));
            Assert.That(fire.gameObject.activeSelf,Is.True);
            image=SchoolCameraFrame(camera,out _,out _,out _);
            try {CloudExperienceTests.Artifact("graphics-candle-v3-lit.png",image.EncodeToPNG());}
            finally {Object.Destroy(image);}
            Call(shell,"Pause");yield return null;
            var position=fire.localPosition;var rotation=fire.localRotation;var scale=fire.localScale;
            yield return Delay(.2f);
            Assert.That(fire.localPosition,Is.EqualTo(position));Assert.That(fire.localRotation,Is.EqualTo(rotation));Assert.That(fire.localScale,Is.EqualTo(scale));
            var data=Call(session,"CaptureCheckpoint");Assert.That(Get<int>(data,"lightingVersion"),Is.EqualTo(1));
            Assert.That(Get<int>(candle,"Ignitions"),Is.EqualTo(1));
        }
        [UnityTest,Timeout(60000)]
        public IEnumerator ModeledCorridorStationFeetMeetActualFloorWhileTargetsAndLightsStayExact()
        {
            Call(session,"CreateCorridor",73);IsolateThreats();Begin();yield return null;
            var stations=LightTargets("Candle").Concat(LightTargets("FlashlightBattery")).ToArray();
            Assert.That(stations.Length,Is.EqualTo(21));
            foreach(var station in stations)
            {
                bool isCandle=Get<object>(station,"kind").ToString()=="Candle";
                Assert.That(station.transform.position.y,Is.EqualTo(isCandle?1.09f:1.15f).Within(.0001f),"Visual grounding moved the original station root");
                var colliders=station.GetComponentsInChildren<Collider>(true);Assert.That(colliders.Length,Is.EqualTo(1));
                var target=(BoxCollider)colliders[0];
                Assert.That(target.size,Is.EqualTo(isCandle?new Vector3(.3f,.35f,.25f):new Vector3(.32f,.28f,.23f)));
                Assert.That(target.center,Is.EqualTo(Vector3.up*(isCandle?.08f:.09f)));
                var floor=Physics.RaycastAll(station.transform.position+Vector3.up*.45f,Vector3.down,2.5f,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore)
                    .Where(hit=>hit.collider.gameObject.name=="Floor").OrderBy(hit=>hit.distance).First();
                var body=station.GetComponentsInChildren<MeshRenderer>(true).Single(renderer=>renderer.transform.parent.name=="Authored realistic prop — "+(isCandle?"candle-waymark":"battery-supply") ||
                    renderer.GetComponentsInParent<Transform>(true).Any(parent=>parent.name=="Authored realistic prop — "+(isCandle?"candle-waymark":"battery-supply")));
                Assert.That(Mathf.Abs(body.bounds.min.y-floor.point.y),Is.LessThan(.002f),Get<string>(station,"stableId")+" rendered foot floats above actual floor");
                if(isCandle)
                {
                    var candle=station.GetComponent(RequireType("WaymarkCandle"));
                    Assert.That(Get<Light>(candle,"LocalLight").transform.localPosition,Is.EqualTo(new Vector3(0,.29f,0)),"Visual grounding moved original pointlight");
                    Assert.That(Get<bool>(candle,"Lit"),Is.False);Assert.That(Get<int>(candle,"Ignitions"),Is.Zero);
                }
            }
        }
    }
}
