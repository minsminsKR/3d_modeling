using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using static HappyToy.V2.CloudTests.RuntimeAccess;
using Object = UnityEngine.Object;

namespace HappyToy.V2.CloudTests
{
    public sealed class CabinetPeekRayTests
    {
        [Test]
        public void OnlyRaysThroughTheOccupiedPhysicalSlitLeaveTheCabinetAtEveryRotationAndFitScale()
        {
            foreach(float yaw in new[] {0f,90f,180f,270f})
            {
                var cabinet=new GameObject("Cabinet slit ray fixture");var model=new GameObject("Fitted visual basis");
                try
                {
                    model.transform.SetParent(cabinet.transform,false);cabinet.transform.position=new Vector3(207,0,203);
                    cabinet.transform.rotation=Quaternion.Euler(0,yaw,0);model.transform.localScale=new Vector3(1.3f,.95f,.8f);
                    var peek=cabinet.AddComponent(RequireType("CabinetPeekWindow"));
                    peek.GetType().GetField("visual",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(peek,model.transform);
                    var bounds=Get<Bounds>(peek,"ApertureLocalBounds");var eye=Get<Vector3>(peek,"EyePosition");
                    Vector3 World(Vector3 point)=>model.transform.TransformPoint(point);
                    var centre=bounds.center;centre.z=bounds.min.z-3;
                    Assert.That((bool)Call(peek,"RayPassesAperture",eye,World(centre)),Is.True);
                    // Targets are constructed from actual front-plane crossings,
                    // so positive and negative samples differ only by opaque edge.
                    var origin=model.transform.InverseTransformPoint(eye);
                    foreach(var axis in new[] {0,1})foreach(int side in new[] {-1,1})
                    {
                        var crossing=bounds.center;crossing.z=bounds.min.z;
                        crossing[axis]=(side<0?bounds.min[axis]:bounds.max[axis])+side*.002f;
                        Assert.That((bool)Call(peek,"RayPassesAperture",eye,World(origin+(crossing-origin)*20)),Is.False);
                        crossing[axis]-=side*.004f;
                        Assert.That((bool)Call(peek,"RayPassesAperture",eye,World(origin+(crossing-origin)*20)),Is.True);
                    }
                    Assert.That((bool)Call(peek,"RayPassesAperture",eye,World(origin+Vector3.forward*3)),Is.False);
                    Assert.That((bool)Call(peek,"RayPassesAperture",World(origin+Vector3.up*.08f),World(centre)),Is.False);
                }
                finally {Object.DestroyImmediate(cabinet);}
            }
        }
    }
}
