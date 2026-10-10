using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;
using static HappyToy.V2.CloudTests.RuntimeAccess;
using Object = UnityEngine.Object;

namespace HappyToy.V2.CloudTests
{
    public sealed partial class CloudPlayModeTests
    {
        Mesh MannequinOpenBodyMesh()
        {
            var geometry=Activator.CreateInstance(RequireType("GraphicsSurfaceLibrary+Geometry"),Vector3.zero,.55f);
            // One mesh with a torso-sized real opening. Its overall AABB still
            // spans every mannequin head/torso sight sample, like separated arms.
            foreach(float side in new[]{-1f,1f})
            {
                Call(geometry,"Box",new Vector3(side*1.275f,0,0),new Vector3(.6f,3,.30f),Quaternion.identity,.001f);
                Call(geometry,"Box",new Vector3(0,side*1.25f,0),new Vector3(3.15f,.5f,.30f),Quaternion.identity,.001f);
            }
            return (Mesh)Call(geometry,"Mesh","Counterfactual opaque NPC with actual torso opening");
        }

        [UnityTest,Timeout(60000)]
        public IEnumerator MannequinRevealKeepsRealOpeningsInReadableGpuAndCurrentSkinnedBodiesAndDisposesSnapshots()
        {
            Call(shell,"BeginChapter");yield return null;((Behaviour)player).enabled=false;
            var chapter=Get<Component>(session,"Chapter");var shots=Get<Component>(chapter,"FirstAppearances");
            Call(shots,"RestoreProgress",1);var memories=Get<Component[]>(chapter,"Memories");
            Call(memories[0],"Use",player);Call(memories[1],"Use",player);
            var actor=Get<Component>(chapter,"Mannequin");Get<Light>(shots,"MannequinSpotlight").enabled=false;
            foreach(var enemy in Components("StalkerBrain"))enemy.gameObject.SetActive(false);
            foreach(var guide in Components("LovelyDollGuide"))guide.gameObject.SetActive(false);
            PlacePlayer(new Vector3(13.8f,.03f,.1f));var eye=Get<Camera>(player,"eyes");eye.transform.rotation=Quaternion.LookRotation(Vector3.back);
            Physics.SyncTransforms();yield return Delay(.6f);
            Assert.That((bool)Call(actor,"FirstSightVisibleTo",eye),Is.True);
            var cyclops=Get<Component>(chapter,"Cyclopse");((Behaviour)cyclops).enabled=false;
            cyclops.GetComponent<NavMeshAgent>().enabled=false;cyclops.gameObject.SetActive(true);
            foreach(var renderer in cyclops.GetComponentsInChildren<Renderer>(true))renderer.enabled=false;
            var material=Resources.Load<Material>("GraphicsPbr/wood-aged/material");Assert.That(material,Is.Not.Null);
            var meshes=new List<Mesh>();var roots=new List<GameObject>();
            try
            {
                foreach(int mode in new[]{0,1,2})
                {
                    var source=MannequinOpenBodyMesh();meshes.Add(source);
                    var root=new GameObject("Actual open NPC mesh mode "+mode);roots.Add(root);
                    root.transform.SetPositionAndRotation(new Vector3(13.8f,1.5f,-3.65f),Quaternion.identity);
                    root.transform.SetParent(cyclops.transform,true);root.layer=8;
                    Renderer cover;Transform bone=null;
                    if(mode<2)
                    {
                        root.AddComponent<MeshFilter>().sharedMesh=source;
                        cover=root.AddComponent<MeshRenderer>();
                        if(mode==1)source.UploadMeshData(true);
                    }
                    else
                    {
                        var weights=new BoneWeight[source.vertexCount];
                        for(int i=0;i<weights.Length;i++)weights[i]=new BoneWeight{boneIndex0=0,weight0=1};
                        source.boneWeights=weights;source.bindposes=new[]{Matrix4x4.identity};
                        bone=new GameObject("Counterfactual actual skin bone").transform;bone.SetParent(root.transform,false);
                        var skin=root.AddComponent<SkinnedMeshRenderer>();skin.sharedMesh=source;skin.bones=new[]{bone};
                        skin.rootBone=bone;skin.localBounds=source.bounds;skin.updateWhenOffscreen=true;cover=skin;
                    }
                    cover.sharedMaterial=material;yield return Delay(.6f);
                    foreach(var local in new[]{new Vector3(0,1.65f,0),new Vector3(-.25f,1.1f,0),new Vector3(.25f,1.1f,0)})
                    {
                        var point=actor.transform.TransformPoint(local);var delta=point-eye.transform.position;
                        Assert.That(cover.bounds.IntersectRay(new Ray(eye.transform.position,delta.normalized),out float distance),Is.True);
                        Assert.That(distance,Is.LessThan(delta.magnitude-.025f),"Counterfactual failed to overlap old broad-phase rays");
                    }
                    int reads=Get<int>(actor,"RevealGpuReadbackMeshes");
                    Assert.That((bool)Call(actor,"FirstSightVisibleTo",eye),Is.True,"AABB empty space became opaque in mode "+mode);
                    if(mode==1)
                    {
                        Assert.That(source.isReadable,Is.False,"GPU reading changed the original mesh readability");
                        Assert.That(Get<int>(actor,"RevealGpuReadbackMeshes"),Is.GreaterThan(reads),"Unreadable mesh skipped exact GPU geometry");
                    }
                    if(mode==2)
                    {
                        // Change actual bone pose in a later frame. The left arm
                        // now physically spans the head/torso rays; the same cache
                        // and reused bake must see it, rather than the old hole.
                        bone.localPosition=Vector3.right*1.275f;yield return null;yield return null;
                        Assert.That((bool)Call(actor,"FirstSightVisibleTo",eye),Is.False,"Posed skin did not close its real visible opening");
                        bone.localPosition=Vector3.zero;yield return null;yield return null;
                        Assert.That((bool)Call(actor,"FirstSightVisibleTo",eye),Is.True,"Skin reused an old blocked pose after the opening returned");
                    }
                    Assert.That(root.GetComponentsInChildren<Collider>(true),Is.Empty);
                    cover.enabled=false;
                }
                var owned=Get<IReadOnlyList<Mesh>>(actor,"RevealOwnedMeshes").ToArray();
                Assert.That(owned,Is.Not.Empty,"No posed mesh was owned by the reveal query");
                ((Behaviour)shots).enabled=false;Object.Destroy(actor);yield return null;yield return null;
                Assert.That(owned.All(mesh=>!mesh),Is.True,"Reveal owner leaked reusable baked meshes");
                Assert.That(meshes.All(mesh=>mesh),Is.True,"Owner destroyed original/static source meshes");
                Assert.That(meshes[1].isReadable,Is.False);
            }
            finally
            {
                foreach(var root in roots)if(root)Object.Destroy(root);
                foreach(var mesh in meshes)if(mesh)Object.Destroy(mesh);
            }
            Debug.Log("HAPPYTOY_MANNEQUIN_REAL_MESH_OCCLUSION_PASS actual open triangles preserve reveal despite overlapping AABBs; immutable unreadable GPU/static meshes, changing skin pose, no colliders and owned bake cleanup");
        }

        [UnityTest,Timeout(60000)]
        public IEnumerator MannequinRevealWaitsForAnOpaqueMonsterBodyWithoutAPhysicalCollider()
        {
            Call(shell,"BeginChapter");yield return null;
            ((Behaviour)player).enabled=false;
            var chapter=Get<Component>(session,"Chapter");var shots=Get<Component>(chapter,"FirstAppearances");
            Call(shots,"RestoreProgress",1);
            var memories=Get<Component[]>(chapter,"Memories");Call(memories[0],"Use",player);Call(memories[1],"Use",player);
            var actor=Get<Component>(chapter,"Mannequin");var spot=Get<Light>(shots,"MannequinSpotlight");spot.enabled=false;
            foreach(var enemy in Components("StalkerBrain"))enemy.gameObject.SetActive(false);
            foreach(var guide in Components("LovelyDollGuide"))guide.gameObject.SetActive(false);
            PlacePlayer(new Vector3(13.8f,.03f,.1f));var eye=Get<Camera>(player,"eyes");eye.transform.rotation=Quaternion.LookRotation(Vector3.back);
            Physics.SyncTransforms();yield return Delay(.6f);
            Assert.That((bool)Call(actor,"FirstSightVisibleTo",eye),Is.True);
            var cyclops=Get<Component>(chapter,"Cyclopse");((Behaviour)cyclops).enabled=false;
            cyclops.GetComponent<NavMeshAgent>().enabled=false;cyclops.gameObject.SetActive(true);
            // A controlled opaque render body on an actual monster hierarchy,
            // with collision disabled, separates renderer cover from wall rays.
            var cover=GameObject.CreatePrimitive(PrimitiveType.Cube);cover.name="Non-collider monster render cover";
            cover.transform.SetPositionAndRotation(new Vector3(13.8f,1.5f,-3.65f),Quaternion.identity);
            cover.transform.localScale=new Vector3(3.15f,3,.3f);cover.GetComponent<Collider>().enabled=false;
            cover.transform.SetParent(cyclops.transform,true);Physics.SyncTransforms();yield return Delay(.6f);
            spot.enabled=true;yield return Delay(.15f);
            Assert.That((bool)Call(actor,"FirstSightVisibleTo",eye),Is.False);
            Assert.That(Get<bool>(shots,"CameraOwned"),Is.False);Assert.That(Get<bool>(actor,"Triggered"),Is.False);
            cover.GetComponent<Renderer>().enabled=false;
            yield return Wait(()=>Get<bool>(shots,"CameraOwned"),2,"Removing only rendered cover did not release the actual-view mannequin zoom");
            Assert.That(Get<bool>(actor,"Triggered"),Is.True);
        }
    }
}
