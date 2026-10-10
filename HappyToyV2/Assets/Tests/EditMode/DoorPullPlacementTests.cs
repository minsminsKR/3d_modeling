using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using static HappyToy.V2.CloudTests.RuntimeAccess;
using Object = UnityEngine.Object;

namespace HappyToy.V2.CloudTests
{
    public sealed class DoorPullPlacementTests
    {
        [Test]
        public void AutomaticPullsUseActualTravelOnBothFacesAndOpposingLeavesAtEveryCardinalRotation()
        {
            foreach (float yaw in new[] { 0f, 90f, 180f, 270f }) foreach (int direction in new[] { -1, 1 })
            {
                var root = new GameObject("Door pull cardinal fixture");
                try
                {
                    root.transform.rotation = Quaternion.Euler(0, yaw, 0);
                    var door = root.AddComponent(RequireType("Interactable"));
                    var leaves = new[] { new GameObject("Primary leaf").transform, new GameObject("Opposing leaf").transform };
                    for (int i=0;i<leaves.Length;i++)
                    {
                        leaves[i].SetParent(root.transform,false);leaves[i].localScale=new Vector3(2.6f,2.36f,.12f);
                        leaves[i].localPosition=new Vector3(i==0?-.7f:.7f,1.18f,0);
                    }
                    Set(door,"secondaryLeaf",leaves[1]);
                    Call(door,"ConfigureDoor",leaves[0],null,Vector3.right*(direction*2.8f));
                    var type=RequireType("CorridorDoorHardware");
                    foreach(var leaf in leaves)
                    {
                        var travel=(Vector3)Call(type,"OpeningTravel",door,leaf);
                        int expected=leaf==leaves[0]?-direction:direction;
                        var hardware=(Component)Call(type,"Attach",door,leaf,2.6f,2.36f,.12f,0);
                        Assert.That(Get<int>(hardware,"PullEdge"),Is.EqualTo(expected));
                        Assert.That(Get<Vector3>(hardware,"OpeningWorldDelta"),Is.EqualTo(travel));
                        var faces=Get<System.Collections.Generic.IReadOnlyList<Transform>>(hardware,"FaceRoots");
                        Assert.That(faces.Count,Is.EqualTo(2));
                        foreach(var face in faces)
                            Assert.That(Vector3.Dot(face.position-leaf.position,travel.normalized),Is.LessThan(-.9f),
                                "Pull must be near the trailing exposed leaf edge, including the rear face");
                        var anchors=faces.Select(face=>leaf.InverseTransformPoint(face.position)).ToArray();
                        leaf.localPosition+=leaf==leaves[0]?Get<Vector3>(door,"openOffset"):-Get<Vector3>(door,"openOffset");
                        for(int i=0;i<faces.Count;i++)
                            Assert.That(Vector3.Distance(faces[i].position,leaf.TransformPoint(anchors[i])),Is.LessThan(.0001f));
                        Assert.That(hardware.GetComponentsInChildren<Collider>(true),Is.Empty);
                    }
                }
                finally {Object.DestroyImmediate(root);}
            }
        }

        [Test]
        public void TrailingEdgeUsesPhysicalWidthEvenWhenLeafBasisIsReversedAndRejectsNonSlidingTravel()
        {
            var type=RequireType("CorridorDoorHardware");
            Assert.That((int)Call(type,"EdgeOppositeTravel",Vector3.right*2.8f,Vector3.left),Is.EqualTo(1));
            Assert.That((int)Call(type,"EdgeOppositeTravel",Vector3.left*2.8f,Vector3.left),Is.EqualTo(-1));
            foreach(var travel in new[] {Vector3.zero,Vector3.forward,new Vector3(float.NaN,0,0)})
                Assert.Throws<ArgumentException>(()=>Call(type,"EdgeOppositeTravel",travel,Vector3.right));
        }
    }
}
