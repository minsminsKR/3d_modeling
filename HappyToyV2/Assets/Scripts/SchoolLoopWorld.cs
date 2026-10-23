using System;
using System.Collections.Generic;
using System.Linq;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;

namespace HappyToy.V2
{
    // Owns the new map and seam traversal. Architecture and room art receive
    // explicit construction inputs; no old quest, stair or enemy is started.
    public sealed class SchoolLoopWorld : MonoBehaviour
    {
        public static readonly Vector3 Origin=new Vector3(0,0,45);
        public Transform Root {get;private set;}
        public Transform Centre {get;private set;}
        public int Traversals {get;private set;}
        readonly SchoolProgressClock progressClock=new SchoolProgressClock();
        readonly List<SchoolProgressClockDisplay> clocks=new List<SchoolProgressClockDisplay>();
        public int ClockHours=>progressClock.Hours;
        public IReadOnlyList<SchoolProgressClockDisplay> Clocks=>clocks;
        public IReadOnlyList<Light> Fixtures=>fixtures;
        public IReadOnlyList<Interactable> LockedDoors=>doors;
        public Transform MusicRoom {get;internal set;}
        public int StaticBatches {get;private set;}
        public string Objective=>"복도를 따라 앞으로 나아가세요. 끝을 통과할 때마다 입구의 디지털 시계가 한 시간씩 올라갑니다.";
        public GraphicsSurfaceLibrary.Pool Surfaces {get;}=new GraphicsSurfaceLibrary.Pool();
        public SchoolHorrorSurfaces HorrorSurfaces {get;private set;}
        internal readonly List<UnityEngine.Object> owned=new List<UnityEngine.Object>();
        internal readonly List<Light> fixtures=new List<Light>();
        internal readonly List<Interactable> doors=new List<Interactable>();
        readonly LegacySchoolRetirement retired=new LegacySchoolRetirement();
        readonly List<(TextMesh label,Vector2 size)> signs=new List<(TextMesh,Vector2)>();
        NavMeshSurface navigation;
        GameSession session;
        Vector3 previous;
        public void Prepare(GameSession owner)
        {
            if(Root)throw new InvalidOperationException("School loop already prepared");
            session=owner;retired.Apply(owner);
            Root=new GameObject("School — repeating observation corridor v3").transform;
            Root.SetParent(transform,false);Root.position=Origin;
            HorrorSurfaces=new SchoolHorrorSurfaces(Surfaces);
            var architecture=new SchoolLoopArchitecture(this);
            for(int index=-1;index<=1;index++)
            {
                var chunk=new GameObject("School loop section "+index).transform;
                chunk.SetParent(Root,false);
                if(index!=0)
                {
                    var continuation=SchoolLoopPlan.Continuation(index);
                    chunk.localPosition=new Vector3(continuation.X,0,continuation.Z);
                    chunk.localRotation=Quaternion.Euler(0,continuation.Yaw,0);
                }
                if(index==0)Centre=chunk;
                architecture.Build(chunk,index==0);
                clocks.Add(SchoolProgressClockDisplay.Build(chunk));
                new SchoolMusicRoomDressing(this).Build(chunk,index==0);
                new SchoolDecayDressing(this,HorrorSurfaces).Build(chunk,index==0);
            }
            Batch();Physics.SyncTransforms();
            navigation=Centre.gameObject.AddComponent<NavMeshSurface>();
            navigation.collectObjects=CollectObjects.Children;navigation.useGeometry=NavMeshCollectGeometry.PhysicsColliders;
            navigation.overrideVoxelSize=true;navigation.voxelSize=.10f;
            navigation.BuildNavMesh();
            Root.gameObject.AddComponent<SchoolHorrorLighting>().Prepare(this);
            session.player.RelocateWorld(Origin+new Vector3(-4.6f,.03f,2.2f),Quaternion.Euler(0,90,0));
            previous=session.player.transform.position;
        }
        public Vector3 ToWorld(Vector3 point)=>Origin+point;
        public void RestoreTraversal(int count){if(count<0||count>1000000)throw new ArgumentException("Invalid traversal count");Traversals=count;previous=session.player.transform.position;}
        public void RestoreClock(int hours)
        {
            progressClock.Restore(hours);
            foreach(var clock in clocks)if(clock)clock.SetHours(progressClock.Hours);
        }
        void LateUpdate()
        {
            if(!Root||!session||!session.InputAllowed)return;
            var feet=session.player.transform.position;
            var local=feet-Origin;var last=previous-Origin;
            int direction=SchoolLoopPlan.Crossing(last.x,local.x,local.z);
            if(direction!=0)
            {
                var pose=SchoolLoopPlan.ReturnToCentre(local.x,local.z,direction);
                feet=Origin+new Vector3(pose.X,local.y,pose.Z);
                session.player.RelocateWorld(feet,Quaternion.Euler(0,pose.Yaw,0)*session.player.transform.rotation);
                Traversals=Mathf.Clamp(Traversals+1,0,1000000);
                if(progressClock.AdvanceForTraversal(direction))
                    foreach(var clock in clocks)if(clock)clock.SetHours(progressClock.Hours);
            }
            previous=feet;
            foreach(var sign in signs)
            {
                if(!sign.label)continue;var size=sign.label.GetComponent<Renderer>().localBounds.size;
                if(size.x>.001f&&size.y>.001f)sign.label.transform.localScale=Vector3.one*Mathf.Min(1,sign.size.x/size.x,sign.size.y/size.y);
            }
        }
        internal GameObject Box(Transform parent,string name,Vector3 centre,Vector3 size,Material material,bool physical=false,bool floor=false)
        {
            var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));go.layer=8;
            go.transform.SetParent(parent,false);go.transform.localPosition=centre;go.transform.localScale=size;
            go.GetComponent<MeshFilter>().sharedMesh=Surfaces.MetreBoxMesh(size,floor?0:Mathf.Min(.004f,size.y*.1f),GraphicsSurfaceLibrary.TileSpan(material));
            var renderer=go.GetComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.receiveShadows=true;
            renderer.shadowCastingMode=ShadowCastingMode.On;
            if(physical)
            {
                go.AddComponent<BoxCollider>();
                if(!floor){var modifier=go.AddComponent<NavMeshModifier>();modifier.overrideArea=true;modifier.area=1;}
            }
            return go;
        }
        internal void Label(Transform parent,string value,Vector3 at,Quaternion rotation,Vector2 size,Color color)
        {
            var holder=new GameObject("Mounted Korean room lettering").transform;holder.SetParent(parent,false);
            holder.localPosition=at;holder.localRotation=rotation;
            var text=holder.gameObject.AddComponent<TextMesh>();text.text=value;text.fontSize=80;text.characterSize=.035f;
            text.anchor=TextAnchor.MiddleCenter;text.alignment=TextAlignment.Center;text.color=color;
            holder.gameObject.AddComponent<AnnexSignFont>().Apply();signs.Add((text,size));
        }
        internal Light Fixture(Transform parent,Vector3 at,bool warm,float intensity=3.4f)
        {
            return SchoolHorrorLighting.Fixture(this,parent,at,warm,intensity);
        }
        void Batch()
        {
            var renderers=Root.GetComponentsInChildren<MeshRenderer>().Where(r=>r.enabled&&!r.GetComponent<TextMesh>()&&
                !r.GetComponentInParent<LODGroup>()&&r.sharedMaterials.Length==1&&r.GetComponent<MeshFilter>()&&
                r.GetComponent<MeshFilter>().sharedMesh&&r.GetComponent<MeshFilter>().sharedMesh.isReadable&&
                GraphicsSurfaceLibrary.SurfaceKey(r.sharedMaterial).Length>0).ToArray();
            Transform Section(Transform item){while(item.parent!=Root)item=item.parent;return item;}
            var groups=renderers.GroupBy(r=>Section(r.transform).GetEntityId()+":"+r.name.ToLowerInvariant().Contains("ceiling")+":"+r.sharedMaterial.GetEntityId()+":"+
                r.shadowCastingMode+":"+r.receiveShadows+":"+r.lightProbeUsage+":"+r.reflectionProbeUsage+":"+
                Mathf.FloorToInt(r.bounds.center.x/8)+":"+Mathf.FloorToInt(r.bounds.center.z/8));
            foreach(var group in groups)
            {
                var members=group.ToArray();if(members.Length<2)continue;var parent=Section(members[0].transform);
                var mesh=new Mesh{name="Owned school loop spatial batch",indexFormat=IndexFormat.UInt32};owned.Add(mesh);
                mesh.CombineMeshes(members.Select(r=>new CombineInstance{mesh=r.GetComponent<MeshFilter>().sharedMesh,transform=parent.worldToLocalMatrix*r.transform.localToWorldMatrix}).ToArray());
                var go=new GameObject(members[0].name.ToLowerInvariant().Contains("ceiling")?"School loop ceiling batch":"School loop spatial batch",typeof(MeshFilter),typeof(MeshRenderer));go.layer=8;go.transform.SetParent(parent,false);
                go.GetComponent<MeshFilter>().sharedMesh=mesh;var renderer=go.GetComponent<MeshRenderer>();
                renderer.sharedMaterial=members[0].sharedMaterial;renderer.shadowCastingMode=members[0].shadowCastingMode;
                renderer.receiveShadows=members[0].receiveShadows;renderer.lightProbeUsage=members[0].lightProbeUsage;
                renderer.reflectionProbeUsage=members[0].reflectionProbeUsage;
                foreach(var member in members)member.enabled=false;StaticBatches++;
            }
        }
        void OnDestroy()
        {
            if(navigation){navigation.RemoveData();if(navigation.navMeshData)Destroy(navigation.navMeshData);}
            if(Root)Destroy(Root.gameObject);
            foreach(var item in owned)if(item)GraphicsSurfaceLibrary.DestroyOwned(item);
            HorrorSurfaces?.Dispose();
            Surfaces.Dispose();
            // A chapter changes by loading a fresh scene. Never reactivate old
            // agents/lights while their scene and NavMesh are being unloaded.
        }
    }
}
