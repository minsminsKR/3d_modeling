using System;
using System.Collections;
using System.IO;
using System.Linq;
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
        public IEnumerator SchoolRetractableGateKeepsGeneratedLodsAndTheRealSpawnRouteOpen()
        {
            yield return BeginObservationSchool();
            var world=Get<Component>(Get<Component>(session,"Chapter"),"Loop");
            var root=Get<Transform>(world,"Root");var centre=Get<Transform>(world,"Centre");
            var gates=root.GetComponentsInChildren(RequireType("SchoolRetractableGate"),true);
            Assert.That(gates.Length,Is.EqualTo(3));
            var source=Resources.Load<GameObject>("SchoolRetractableGate/retractable-gate-visual");
            Assert.That(source,Is.Not.Null);var lod=source.GetComponent<LODGroup>();
            Assert.That(lod.GetLODs().Length,Is.EqualTo(2));
            var sourceMeshes=source.GetComponentsInChildren<MeshFilter>(true).Select(f=>f.sharedMesh).ToArray();
            Assert.That(sourceMeshes.All(m=>m&&m.isReadable&&m.uv.Length==m.vertexCount),Is.True);
            Assert.That(sourceMeshes[0].triangles.Length/3,Is.InRange(10000,100004));
            Assert.That(sourceMeshes[1].triangles.Length/3,Is.InRange(1000,65004));
            foreach(var gate in gates)
            {
                var visual=Get<Transform>(gate,"Visual");var eye=Get<Transform>(gate,"PadlockAnchor");
                CollectionAssert.AreEquivalent(sourceMeshes,visual.GetComponentsInChildren<MeshFilter>(true).Select(f=>f.sharedMesh));
                Assert.That(visual.GetComponentsInChildren<Collider>(true),Is.Empty);
                Assert.That(visual.GetComponentsInChildren<Transform>(true).Count(t=>t.name=="Single padlock storage eye"),Is.EqualTo(1));
                Assert.That(eye.IsChildOf(visual),Is.True);Assert.That(eye.localPosition.y,Is.InRange(.7f,1.6f));
                Assert.That(Get<float>(gate,"ClearWidth"),Is.GreaterThan(3));
                foreach(var material in visual.GetComponentsInChildren<Renderer>(true).SelectMany(r=>r.sharedMaterials))
                    Assert.That(material&&material.GetTexture("_BaseMap")&&material.GetTexture("_MetallicGlossMap"),Is.True);
                Assert.That(gate.GetComponentsInChildren(RequireType("Interactable"),true),Is.Empty,
                    "The stored padlock is environmental art, not a new progression lock");
            }
            Assert.That(gates.Count(g=>Get<BoxCollider>(g,"FoldedBody")),Is.EqualTo(1));
            Assert.That(root.GetComponentsInChildren<Transform>(true).Any(t=>t.name=="Permanently open steel fire door"),Is.False);
            var from=LoopOrigin+new Vector3(-5.6f,.03f,2.2f);var to=LoopOrigin+new Vector3(-2.8f,.03f,2.2f);
            Assert.That(Physics.CapsuleCast(from+Vector3.up*.3f,from+Vector3.up*1.5f,.25f,Vector3.right,2.8f,1<<8),Is.False);
            var route=new NavMeshPath();Assert.That(NavMesh.CalculatePath(from,to,NavMesh.AllAreas,route),Is.True);
            Assert.That(route.status,Is.EqualTo(NavMeshPathStatus.PathComplete));
            PlacePlayer(from);player.transform.rotation=Quaternion.Euler(0,90,0);Keys(Key.W);yield return Delay(1.15f);Keys();
            Assert.That(player.transform.position.x,Is.GreaterThan(LoopOrigin.x-4.05f),"Real walking stopped at the parked gate");
            Assert.That(player.transform.position.y,Is.GreaterThan(-.10f));
        }

        [UnityTest,Timeout(180000)]
        public IEnumerator SchoolRetractableGateActualCameraShowsTheFoldedLeafAndHangingPadlock()
        {
            yield return BeginObservationSchool();yield return Delay(3.2f);
            var world=Get<Component>(Get<Component>(session,"Chapter"),"Loop");
            var centre=Get<Transform>(world,"Centre");var root=Get<Transform>(world,"Root");
            var lighting=root.GetComponent(RequireType("SchoolHorrorLighting"));
            yield return Wait(()=>Get<bool>(lighting,"ReflectionReady"),45,"School reflection initialization did not finish");
            var gate=centre.GetComponentsInChildren(RequireType("SchoolRetractableGate")).Single();
            var visual=Get<Transform>(gate,"Visual");var eye=Get<Transform>(gate,"PadlockAnchor");
            var camera=Get<Camera>(player,"eyes");((Behaviour)player).enabled=false;
            Get<Light>(player,"flashlight").enabled=false;Call(shell,"Pause");
            string output=Environment.GetEnvironmentVariable("HAPPYTOY_SCHOOL_GATE_REVIEW_DIR");
            if(!string.IsNullOrWhiteSpace(output))
            {
                output=Path.GetFullPath(output);string builds=Path.GetFullPath(Path.Combine(Application.dataPath,"../Builds"));
                Assert.That(output.StartsWith(builds+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase),Is.True);
                Directory.CreateDirectory(output);
            }
            var views=new[]{
                ("entry-gate-full",new Vector3(-2.65f,.03f,2.35f),LoopOrigin+new Vector3(-4.6f,1.32f,3.25f)),
                ("entry-padlock-close",new Vector3(-3.75f,.03f,3.0f),eye.position),
                ("entry-spawn",new Vector3(-4.6f,.03f,2.2f),LoopOrigin+new Vector3(0,1.6f,2.2f))};
            foreach(var view in views)
            {
                PlacePlayer(LoopOrigin+view.Item2,false);camera.transform.rotation=Quaternion.LookRotation(view.Item3-camera.transform.position);
                Get<Light>(player,"flashlight").enabled=view.Item1=="entry-padlock-close";
                Physics.SyncTransforms();Call(session.GetComponent(RequireType("LocalShadowBudget")),"RefreshNow");
                var frame=SchoolHorrorFrame(camera,1440,900);
                try
                {
                    if(!string.IsNullOrWhiteSpace(output))File.WriteAllBytes(Path.Combine(output,view.Item1+".png"),frame.EncodeToPNG());
                    Assert.That(frame.GetPixels32().Count(c=>c.r>180&&c.b>180&&c.g<50),Is.LessThan(40),"Gate shader is missing");
                    if(view.Item1=="entry-gate-full")
                    {
                        Texture2D absent=null;
                        try
                        {
                            visual.gameObject.SetActive(false);absent=SchoolHorrorFrame(camera,1440,900);
                            var a=frame.GetPixels32();var b=absent.GetPixels32();int changed=0;
                            for(int i=0;i<a.Length;i++)if(Mathf.Abs(a[i].r-b[i].r)+Mathf.Abs(a[i].g-b[i].g)+Mathf.Abs(a[i].b-b[i].b)>18)changed++;
                            Assert.That(changed,Is.GreaterThan(2500),"The real generated gate is not visible from the entry");
                        }
                        finally{visual.gameObject.SetActive(true);if(absent)Object.Destroy(absent);}
                    }
                }
                finally{Object.Destroy(frame);}
                yield return null;
            }
            Debug.Log("HAPPYTOY_SCHOOL_RETRACTABLE_GATE_CAMERA_PASS");
        }
    }
}
