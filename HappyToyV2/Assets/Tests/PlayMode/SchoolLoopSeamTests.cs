using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using static HappyToy.V2.CloudTests.RuntimeAccess;
using Object=UnityEngine.Object;

namespace HappyToy.V2.CloudTests
{
    public sealed partial class CloudPlayModeTests
    {
        [UnityTest,Timeout(180000)]
        public IEnumerator SchoolTurnbackWallsHideTheMainHallAcrossTheFullTransferWidth()
        {
            yield return BeginObservationSchool();
            var world=Get<Component>(Get<Component>(session,"Chapter"),"Loop");
            foreach(float side in new[]{-1.5f,0f,1.5f})foreach(float height in new[]{1.05f,1.65f})
                foreach(int end in new[]{-1,1})
                {
                    var eye=LoopOrigin+new Vector3(end*6.6f+side,height,end<0?-7.9f:46.3f);
                    foreach(float x in new[]{-1.5f,0f,1.5f})foreach(float z in new[]{1f,8f,19f,30f,37f})
                    {
                        var ray=LoopOrigin+new Vector3(x,height,z)-eye;
                        Assert.That(Physics.RaycastAll(eye,ray.normalized,ray.magnitude-.05f,1<<8,QueryTriggerInteraction.Ignore)
                            .Any(hit=>hit.collider.name=="School solid wall"),Is.True,
                            "Main hall is directly visible from transfer leg "+end+" side "+side+" target "+x+","+z);
                    }
                }
            foreach(var destination in new[]{new Vector3(-6.6f,.03f,-6),new Vector3(6.6f,.03f,45)})
            {
                var path=new NavMeshPath();
                Assert.That(NavMesh.CalculatePath(LoopOrigin+new Vector3(0,.03f,19),LoopOrigin+destination,NavMesh.AllAreas,path),Is.True);
                Assert.That(path.status,Is.EqualTo(NavMeshPathStatus.PathComplete));
                Assert.That(path.corners.Length,Is.GreaterThanOrEqualTo(3),"Navigation cut through an opaque bend");
            }
            Assert.That(Get<int>(world,"Traversals"),Is.Zero);
        }
        [UnityTest,Timeout(180000)]
        public IEnumerator SchoolBothOpaqueBendsAreWalkableAndOnlyTheExtendedEndsTeleport()
        {
            yield return BeginObservationSchool();
            var world=Get<Component>(Get<Component>(session,"Chapter"),"Loop");
            PlacePlayer(LoopOrigin+new Vector3(0,.03f,32));
            yield return WalkSchoolConnectorLeg(new Vector3(0,.03f,36.2f),0);
            yield return WalkSchoolConnectorLeg(new Vector3(6.6f,.03f,36.2f),90);
            Assert.That(Get<int>(world,"Traversals"),Is.Zero,"The old horizontal connector still teleports");
            yield return WalkSchoolConnectorLeg(new Vector3(6.6f,.03f,45.5f),0);
            Assert.That(Get<int>(world,"Traversals"),Is.Zero,"Teleport occurred before the end of the masking leg");
            Keys(Key.W);yield return Wait(()=>Get<int>(world,"Traversals")==1,3,"Forward turnback did not transfer");Keys();
            Assert.That(player.transform.position.z-LoopOrigin.z,Is.InRange(-8f,-7.2f));
            Assert.That(Mathf.DeltaAngle(player.transform.eulerAngles.y,0),Is.EqualTo(0).Within(.01f));
            Call(shell,"Pause");UseSchoolAnomalyFixture(world,"Normal");Call(shell,"Resume");
            yield return WalkSchoolConnectorLeg(new Vector3(-6.6f,.03f,2.2f),0);
            yield return WalkSchoolConnectorLeg(new Vector3(0,.03f,2.2f),90);
            yield return WalkSchoolConnectorLeg(new Vector3(0,.03f,6.4f),0);
            yield return WalkSchoolConnectorLeg(new Vector3(0,.03f,2.2f),180);
            yield return WalkSchoolConnectorLeg(new Vector3(-6.6f,.03f,2.2f),270);
            Assert.That(Get<int>(world,"Traversals"),Is.EqualTo(1));
            yield return WalkSchoolConnectorLeg(new Vector3(-6.6f,.03f,-7.1f),180);
            Keys(Key.W);yield return Wait(()=>Get<int>(world,"Traversals")==2,3,"Rear turnback did not transfer");Keys();
            Assert.That(player.transform.position.z-LoopOrigin.z,Is.InRange(-8f,-7.2f));
            Assert.That(Mathf.DeltaAngle(player.transform.eulerAngles.y,0),Is.EqualTo(0).Within(.01f));
            Assert.That(Get<int>(world,"ClockHours"),Is.EqualTo(0),"A normal backward passage must reset judgment progress");
        }
        IEnumerator WalkSchoolConnectorLeg(Vector3 local,float yaw)
        {
            player.transform.rotation=Quaternion.Euler(0,yaw,0);var heading=player.transform.forward;
            float deadline=Time.realtimeSinceStartup+15;Keys(Key.W);
            while(Vector3.Dot(LoopOrigin+local-player.transform.position,heading)>.08f)
            {Assert.That(Time.realtimeSinceStartup,Is.LessThan(deadline),"Physical connector route blocked at "+local);yield return null;}
            Keys();yield return null;
            var delta=player.transform.position-LoopOrigin-local;delta.y=0;
            Assert.That(delta.magnitude,Is.LessThan(.25f),"Connector walking missed its actual waypoint");
        }
        [UnityTest,Timeout(120000)]
        public IEnumerator SchoolHalfTurnPreservesMotionBeforeTheNextInputUpdate()
        {
            yield return BeginObservationSchool();
            PlacePlayer(LoopOrigin+new Vector3(-6.6f,.03f,-5));player.transform.rotation=Quaternion.Euler(0,180,0);
            Call(player,"TrySetCrouching",true);Keys(Key.W);yield return Delay(.12f);
            // Observe the engine phase before the next Update re-samples input.
            const BindingFlags state=BindingFlags.Instance|BindingFlags.NonPublic;
            var movement=(Vector3)player.GetType().GetField("moveDirection",state).GetValue(player);
            var fall=player.GetType().GetField("fallSpeed",state);float falling=(float)fall.GetValue(player);
            Assert.That(movement.magnitude,Is.GreaterThan(.9f));
            var look=Get<Camera>(player,"eyes").transform.localRotation;
            Call(player,"TraverseWorldSeam",LoopOrigin+new Vector3(-6.6f,.03f,-7.7f),Quaternion.Euler(0,180,0));
            Assert.That(Get<bool>(player,"Crouching"),Is.True);
            Assert.That(Get<Camera>(player,"eyes").transform.localRotation,Is.EqualTo(look));
            Assert.That((float)fall.GetValue(player),Is.EqualTo(falling));
            var before=player.transform.position;
            player.GetType().GetMethod("FixedUpdate",state).Invoke(player,null);
            Assert.That(player.transform.position.z-before.z,Is.GreaterThan(.01f),"The first physics step after transfer lost held movement");
            Keys();
        }
        [UnityTest,Timeout(180000)]
        public IEnumerator SchoolBothSeamViewsMatchTheirVisibleContinuationWithoutMirroring()
        {
            yield return BeginObservationSchool();yield return Delay(3.2f);
            var world=Get<Component>(Get<Component>(session,"Chapter"),"Loop");var root=Get<Transform>(world,"Root");
            var centre=Get<Transform>(world,"Centre");var camera=Get<Camera>(player,"eyes");
            ((Behaviour)player).enabled=false;Get<Light>(player,"flashlight").enabled=false;Call(shell,"Pause");
            string output=Environment.GetEnvironmentVariable("HAPPYTOY_SCHOOL_LOOP_REVIEW_DIR");
            if(!string.IsNullOrWhiteSpace(output))
            {
                output=Path.GetFullPath(output);string builds=Path.GetFullPath(Path.Combine(Application.dataPath,"../Builds"));
                Assert.That(output.StartsWith(builds+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase),Is.True);
                Directory.CreateDirectory(output);
            }
            var points=new[]{new Vector3(-6.6f,1.8f,-4f),new Vector3(-2.03f,2.76f,6.75f),
                new Vector3(-2.03f,2.76f,15.15f),new Vector3(-2.03f,2.76f,25.95f)};
            foreach(int direction in new[]{1,-1})
            {
                var preview=root.Find("School loop section "+direction);
                Assert.That(preview.lossyScale,Is.EqualTo(Vector3.one),"Never mirror Korean lettering or model winding");
                var feet=direction>0?new Vector3(6.6f,.03f,46.5f):new Vector3(-6.6f,.03f,-8.1f);
                var facing=Quaternion.Euler(0,direction>0?0:180,0);
                Call(player,"RelocateWorld",LoopOrigin+feet,facing);camera.transform.localRotation=Quaternion.Euler(8,0,0);
                var projected=new Vector3[points.Length];for(int i=0;i<points.Length;i++)projected[i]=camera.WorldToViewportPoint(preview.TransformPoint(points[i]));
                var before=SchoolHorrorFrame(camera,960,540);
                try
                {
                    // Independent expected entrance pose for each physical plane.
                    Call(player,"RelocateWorld",LoopOrigin+new Vector3(-6.6f,.03f,-7.9f),Quaternion.identity);
                    for(int i=0;i<points.Length;i++)
                        Assert.That(Vector3.Distance(projected[i],camera.WorldToViewportPoint(centre.TransformPoint(points[i]))),Is.LessThan(.001f),
                            "The visible neighbour and destination disagree at landmark "+i+" departure "+direction);
                    var after=SchoolHorrorFrame(camera,960,540);
                    try
                    {
                        float first=SchoolDisplayLuma(before,new Rect(0,0,1,1)),second=SchoolDisplayLuma(after,new Rect(0,0,1,1));
                        Assert.That(Mathf.Abs(first-second),Is.LessThan(.025f),"Seam relocation caused a whole-view lighting flash");
                        Debug.Log("HAPPYTOY_SCHOOL_LEFT_SEAM_VIEW direction="+direction+" before="+first+" after="+second);
                        if(!string.IsNullOrWhiteSpace(output))
                        {
                            File.WriteAllBytes(Path.Combine(output,"departure-"+direction+"-before.png"),before.EncodeToPNG());
                            File.WriteAllBytes(Path.Combine(output,"departure-"+direction+"-after.png"),after.EncodeToPNG());
                        }
                    }
                    finally{Object.Destroy(after);}
                }
                finally{Object.Destroy(before);}
            }
        }
    }
}
