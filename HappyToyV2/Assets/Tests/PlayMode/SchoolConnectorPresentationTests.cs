using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;
using static HappyToy.V2.CloudTests.RuntimeAccess;
using Object=UnityEngine.Object;

namespace HappyToy.V2.CloudTests
{
    public sealed partial class CloudPlayModeTests
    {
        [UnityTest,Timeout(180000)]
        public IEnumerator SchoolConnectorsKeepCeilingExitFixtureClearanceAndTheirFullWalkingRoute()
        {
            yield return BeginObservationSchool();
            var world=Get<Component>(Get<Component>(session,"Chapter"),"Loop");var root=Get<Transform>(world,"Root");var centre=Get<Transform>(world,"Centre");
            var exits=root.GetComponentsInChildren(RequireType("SchoolExitSign"),true);
            Assert.That(exits.Length,Is.EqualTo(3));
            foreach(var exit in exits)
            {
                Assert.That(exit.transform.localPosition,Is.EqualTo(new Vector3(0,2.91f,35.9f)));
                Assert.That(exit.GetComponentsInChildren<TextMesh>(true).Count(t=>t.text=="비상구"),Is.EqualTo(2));
                Assert.That(Get<Transform>(exit,"Visual").GetComponent<LODGroup>().GetLODs().Length,Is.EqualTo(2));
                Assert.That(exit.GetComponentsInChildren<Collider>(true),Is.Empty);
                var light=Get<Light>(exit,"Spill");Assert.That(light.color.g,Is.GreaterThan(light.color.r*3));
                Assert.That(light.range,Is.LessThan(2));Assert.That(light.shadows,Is.EqualTo(LightShadows.None));
                Assert.That(exit.GetComponentsInChildren<Transform>().Count(t=>t.name=="Exit sign suspension rod"),Is.EqualTo(2));
            }
            Assert.That(root.GetComponentsInChildren<Transform>(true).Any(t=>t.name=="Small red alarm lens"||t.name=="Distant failed school alarm indicator"),Is.False);
            var lintel=centre.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="Fire gate lintel").GetComponent<Renderer>().bounds;
            var lamps=centre.GetComponentsInChildren(RequireType("SchoolFluorescent"),true);
            foreach(var lamp in lamps)foreach(var renderer in lamp.GetComponentsInChildren<MeshRenderer>(true))
                Assert.That(renderer.bounds.Intersects(lintel),Is.False,"Fluorescent housing intersects the physical gate beam: "+lamp.transform.localPosition);
            var histories=centre.GetComponentsInChildren<Transform>(true).Where(t=>t.name=="Abandoned school connector history").ToArray();
            Assert.That(histories.Length,Is.EqualTo(2));
            foreach(var history in histories)
            {
                Assert.That(history.GetComponentsInChildren<Transform>(true).Count(t=>t.name=="Soaked wrinkled torn school exercise paper"),Is.GreaterThan(65));
                Assert.That(history.GetComponentsInChildren<Transform>(true).Count(t=>t.name=="Irregular shallow connector leak water film"),Is.EqualTo(2));
                Assert.That(history.GetComponentsInChildren<MeshFilter>(true).Count(f=>f.sharedMesh&&f.sharedMesh.vertexCount>100),Is.GreaterThanOrEqualTo(2));
                var from=history.position+new Vector3(0,.03f,.6f);var to=history.position+new Vector3(0,.03f,7.4f);
                Assert.That(Physics.CapsuleCast(from+Vector3.up*.30f,from+Vector3.up*1.5f,.30f,Vector3.forward,6.8f,1<<8),Is.False);
                var route=new NavMeshPath();Assert.That(NavMesh.CalculatePath(from,to,NavMesh.AllAreas,route),Is.True);
                Assert.That(route.status,Is.EqualTo(NavMeshPathStatus.PathComplete));
                var damage=centre.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="Connector scan rubble and broken wall finish"&&Vector3.Distance(t.position,history.position)<.01f);
                Assert.That(damage.GetComponentsInChildren<Transform>(true).Count(t=>t.name.StartsWith("Acquired scan —")),Is.GreaterThanOrEqualTo(20));
                Assert.That(damage.GetComponentsInChildren<Collider>(true),Is.Empty);
                Assert.That(damage.GetComponentsInChildren<Transform>(true).Count(t=>t.name=="Connector raised curling wall coating"),Is.GreaterThan(40));
            }
        }

        [UnityTest,Timeout(180000)]
        public IEnumerator SchoolConnectorNormalCameraReviewsExitGateAndBothDressedLegs()
        {
            yield return BeginObservationSchool();yield return Delay(3.2f);
            var world=Get<Component>(Get<Component>(session,"Chapter"),"Loop");var root=Get<Transform>(world,"Root");
            var lighting=root.GetComponent(RequireType("SchoolHorrorLighting"));
            yield return Wait(()=>Get<bool>(lighting,"ReflectionReady"),45,"School reflection initialization did not finish");
            var camera=Get<Camera>(player,"eyes");((Behaviour)player).enabled=false;
            Get<Light>(player,"flashlight").enabled=false;Call(shell,"Pause");
            string output=Environment.GetEnvironmentVariable("HAPPYTOY_SCHOOL_CONNECTOR_REVIEW_DIR");
            if(!string.IsNullOrWhiteSpace(output))
            {
                output=Path.GetFullPath(output);var builds=Path.GetFullPath(Path.Combine(Application.dataPath,"../Builds"));
                Assert.That(output.StartsWith(builds+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase),Is.True);Directory.CreateDirectory(output);
            }
            var views=new[]{
                ("ceiling-emergency-exit",new Vector3(0,.03f,30.7f),new Vector3(0,2.86f,35.9f)),
                ("gate-lamp-clearance",new Vector3(-5.6f,.03f,1.2f),new Vector3(-4.8f,2.94f,2.65f)),
                ("entry-connector",new Vector3(-6.6f,.03f,-.35f),new Vector3(-6.6f,1.48f,-7.7f)),
                ("exit-connector",new Vector3(6.6f,.03f,38.75f),new Vector3(6.6f,1.48f,46.1f))};
            foreach(var view in views)
            {
                PlacePlayer(LoopOrigin+view.Item2,false);camera.transform.rotation=Quaternion.LookRotation(LoopOrigin+view.Item3-camera.transform.position);
                Physics.SyncTransforms();Call(session.GetComponent(RequireType("LocalShadowBudget")),"RefreshNow");
                var frame=SchoolHorrorFrame(camera,1600,900);
                try
                {
                    if(!string.IsNullOrWhiteSpace(output))File.WriteAllBytes(Path.Combine(output,view.Item1+".png"),frame.EncodeToPNG());
                    Assert.That(frame.GetPixels32().Count(c=>c.r>180&&c.b>180&&c.g<50),Is.LessThan(40),"School asset shader is missing");
                    Assert.That(SchoolDisplayLuma(frame,new Rect(.15f,.15f,.7f,.7f)),Is.GreaterThan(.025f),"The new passage cannot be read with the normal game camera");
                }
                finally{Object.Destroy(frame);}
                yield return null;
            }
            Debug.Log("HAPPYTOY_SCHOOL_CONNECTOR_PRESENTATION_CAMERA_PASS");
        }
    }
}
