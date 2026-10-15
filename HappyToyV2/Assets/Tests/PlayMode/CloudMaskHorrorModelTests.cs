using System.Collections;
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
        [Test]
        public void StudioWraithKeepsNavigationAndImportsEighteenSkinnedArms()
        {
            var actor=One("LanternMaskEncounter");var art=actor.GetComponent(RequireType("MaskHorrorVisual"));
            Assert.That(Get<bool>(art,"Prepared"),Is.True);
            var model=Get<Transform>(art,"BodyModel");
            Assert.That(model.parent.name,Is.EqualTo("Hallway visual fit"));
            Assert.That(model.parent.parent,Is.EqualTo(Get<Transform>(actor,"body")));
            Assert.That(model.GetComponentsInChildren<Transform>(true).Count(t=>t.name.EndsWith("_Wrist")),Is.EqualTo(18));
            var skins=model.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            Assert.That(skins.Length,Is.EqualTo(1));Assert.That(skins[0].sharedMesh.vertexCount,Is.GreaterThan(900000));
            Assert.That(skins[0].bones.Count(b=>b.name=="Head"),Is.EqualTo(1));
            Assert.That(model.GetComponentsInChildren<Collider>(true),Is.Empty);
            Assert.That(model.GetComponentsInChildren<Rigidbody>(true),Is.Empty);
            var agent=actor.GetComponent<NavMeshAgent>();
            Assert.That(agent.radius,Is.EqualTo(.3f).Within(.001f));
            Assert.That(agent.height,Is.EqualTo(2.1f).Within(.001f));
            Assert.That(actor.transform.localScale,Is.EqualTo(Vector3.one));
            Assert.That(Get<AnimationClip>(art,"RunClip").length,Is.InRange(.70f,.74f));
        }
        [Test]
        public void StudioImportedRunKeepsFloorRoofAndForwardFacingHead()
        {
            var instance=Object.Instantiate(Resources.Load<GameObject>("Baeksugwi/Baeksugwi"));
            try
            {
                var clip=Resources.Load<AnimationClip>("Baeksugwi/Run_InPlace");
                var skin=instance.GetComponentInChildren<SkinnedMeshRenderer>();
                var mesh=new Mesh();
                try
                {
                    for(int sample=0;sample<4;sample++)
                    {
                        clip.SampleAnimation(instance,sample*clip.length/4);
                        skin.BakeMesh(mesh,false);
                        var inherited=skin.transform.parent.lossyScale;
                        var points=mesh.vertices.Select(p=>skin.transform.TransformPoint(new Vector3(p.x/inherited.x,p.y/inherited.y,p.z/inherited.z))).ToArray();
                        Assert.That(points.Min(p=>p.y),Is.GreaterThan(-.008f),"Imported run penetrates the floor");
                        Assert.That(points.Max(p=>p.y),Is.LessThan(2.44f),"Imported creature penetrates the corridor roof");
                    }
                    var nodes=instance.GetComponentsInChildren<Transform>(true);
                    var head=nodes.Single(t=>t.name=="Head");var tail=nodes.Single(t=>t.name=="Tail");
                    Assert.That(Vector3.Dot(head.position-tail.position,Vector3.forward),Is.GreaterThan(2));
                }
                finally{Object.DestroyImmediate(mesh);}
            }
            finally{Object.DestroyImmediate(instance);}
        }
        [UnityTest,Timeout(60000)]
        public IEnumerator StudioRunMovesWithTheAgentAndFreezesOnPause()
        {
            yield return PrepareCorridorMaskRunner();
            var actor=Get<Component>(Get<Component>(session,"Corridor"),"Mask");
            var art=actor.GetComponent(RequireType("MaskHorrorVisual"));
            var model=Get<Transform>(art,"BodyModel");
            var elbows=model.GetComponentsInChildren<Transform>(true).Where(t=>t.name.EndsWith("_Elbow")).ToArray();
            ((Behaviour)player).enabled=false;PlacePlayer(new Vector3(200,5,200),false);
            var start=actor.transform.position;var first=elbows.Select(t=>t.localRotation).ToArray();
            yield return Wait(()=>Vector3.Distance(start,actor.transform.position)>.1f&&Get<float>(art,"GaitPhase")>.1f,5,"Agent did not drive the imported sprint");
            Assert.That(elbows.Where((t,i)=>Quaternion.Angle(t.localRotation,first[i])>.05f).Count(),Is.GreaterThanOrEqualTo(6));
            var rotations=elbows.Select(t=>t.localRotation).ToArray();float phase=Get<float>(art,"GaitPhase");
            Call(shell,"Pause");yield return Delay(.25f);
            Assert.That(Get<float>(art,"GaitPhase"),Is.EqualTo(phase));
            for(int i=0;i<elbows.Length;i++)Assert.That(Quaternion.Angle(rotations[i],elbows[i].localRotation),Is.LessThan(.001f));
        }
        [UnityTest,Timeout(60000)]
        public IEnumerator StudioCorridorCloneUsesOneReplacementAndHidesTheFloatingFace()
        {
            yield return PrepareCorridorMaskRunner();
            var actor=Get<Component>(Get<Component>(session,"Corridor"),"Mask");
            PlacePlayer(new Vector3(200,5,200),false);
            yield return null;
            Assert.That(Get<Transform>(actor,"body").GetComponentsInChildren<SkinnedMeshRenderer>(true).Count(r=>r.enabled),Is.EqualTo(1));
            Assert.That(Get<Transform>(actor,"mask").GetComponentsInChildren<Renderer>(true).All(r=>!r.enabled),Is.True);
        }
    }
    [DefaultExecutionOrder(32000)]
    public sealed class CloudReferenceLatePoseRecorder:MonoBehaviour
    {
        public System.Action Sample;
        public System.Exception Error {get;private set;}
        void LateUpdate(){if(Sample==null||Error!=null)return;try{Sample();}catch(System.Exception error){Error=error;enabled=false;}}
    }
}
