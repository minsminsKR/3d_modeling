using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;
using static HappyToy.V2.CloudTests.RuntimeAccess;

namespace HappyToy.V2.CloudTests
{
    public sealed partial class CloudPlayModeTests
    {
        [UnityTest,Timeout(60000)]
        public IEnumerator GraphicsSchoolPbrRefreshPreservesPhysicalRoutesAndRestoresOriginalRenderState()
        {
            Begin();Call(shell,"Pause");
            var surfaces=One("GraphicsSchoolSurfaces");
            ((Behaviour)surfaces).enabled=false;yield return null;yield return null;
            var colliders=Object.FindObjectsByType<Collider>(FindObjectsInactive.Include,FindObjectsSortMode.None)
                .Where(x=>x.gameObject.scene==session.gameObject.scene).ToArray();
            var physical=colliders.Select(x=>(x,x.transform.position,x.transform.rotation,x.transform.lossyScale,x.enabled,x.gameObject.layer)).ToArray();
            var renderers=Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include,FindObjectsSortMode.None)
                .Where(x=>x.gameObject.scene==session.gameObject.scene&&x.GetComponent<MeshFilter>()).ToArray();
            var original=renderers.Select(x=>(x,x.GetComponent<MeshFilter>().sharedMesh,x.sharedMaterials,x.enabled)).ToArray();
            var unreadable=original.Where(state=>state.enabled&&state.sharedMesh&&!state.sharedMesh.isReadable).ToArray();
            Assert.That(unreadable.Any(state=>new[]{"Flat floor plinth","Floor bolt","Floor bolt.001"}.Contains(state.sharedMesh.name)),Is.True,
                "Missing actual imported GPU-only school furniture regression target");
            var unreadableBounds=unreadable.ToDictionary(state=>state.x,state=>state.x.bounds);
            var before=new NavMeshPath();
            Assert.That(NavMesh.CalculatePath(new Vector3(-6.5f,.03f,0),new Vector3(7,.03f,0),NavMesh.AllAreas,before),Is.True);
            Assert.That(before.status,Is.EqualTo(NavMeshPathStatus.PathComplete));
            ((Behaviour)surfaces).enabled=true;Call(surfaces,"Refresh");yield return null;yield return null;
            Assert.That(Get<int>(surfaces,"SurfaceCount"),Is.GreaterThan(40));
            Assert.That(Get<int>(surfaces,"StaticBatches"),Is.GreaterThan(0));
            Assert.That(Get<int>(surfaces,"BevelledBoxes"),Is.GreaterThan(10));
            Assert.That(Get<int>(surfaces,"DoorJoineryCount"),Is.GreaterThan(0));
            foreach(var state in unreadable)
            {
                Assert.That(state.x.enabled,Is.EqualTo(state.enabled),"CPU-only batch hid GPU-only original: "+state.sharedMesh.name);
                Assert.That(state.x.GetComponent<MeshFilter>().sharedMesh,Is.SameAs(state.sharedMesh),"Imported asset geometry was copied or changed");
                Assert.That(state.sharedMesh.isReadable,Is.False,"Original importer/readback state was changed");
                Assert.That(state.x.bounds,Is.EqualTo(unreadableBounds[state.x]),"Unbatched original bounds changed");
            }
            var after=new NavMeshPath();
            Assert.That(NavMesh.CalculatePath(new Vector3(-6.5f,.03f,0),new Vector3(7,.03f,0),NavMesh.AllAreas,after),Is.True);
            Assert.That(after.status,Is.EqualTo(before.status));
            Assert.That(after.corners.Length,Is.EqualTo(before.corners.Length));
            for(int i=0;i<after.corners.Length;i++)Assert.That(Vector3.Distance(after.corners[i],before.corners[i]),Is.LessThan(.00001f));
            foreach(var p in physical)
            {
                Assert.That(p.x.transform.position,Is.EqualTo(p.position));Assert.That(p.x.transform.rotation,Is.EqualTo(p.rotation));
                Assert.That(p.x.transform.lossyScale,Is.EqualTo(p.lossyScale));Assert.That(p.x.enabled,Is.EqualTo(p.enabled));
                Assert.That(p.x.gameObject.layer,Is.EqualTo(p.layer));
            }
            Assert.That(Object.FindObjectsByType<Collider>(FindObjectsInactive.Include,FindObjectsSortMode.None)
                .Count(x=>x.gameObject.scene==session.gameObject.scene),Is.EqualTo(colliders.Length),"Visual refresh added physics");
            ((Behaviour)surfaces).enabled=false;yield return null;yield return null;
            foreach(var state in original)
            {
                Assert.That(state.x.GetComponent<MeshFilter>().sharedMesh,Is.SameAs(state.sharedMesh));
                Assert.That(state.x.sharedMaterials,Is.EqualTo(state.sharedMaterials));
                Assert.That(state.x.enabled,Is.EqualTo(state.enabled));
            }
        }
    }
}
