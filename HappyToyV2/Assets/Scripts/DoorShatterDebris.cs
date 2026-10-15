using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace HappyToy.V2
{
    // Animates the retained webapp timber surfaces, then batches the same pieces
    // on the floor. Navigation and collisions never depend on this cosmetic prop.
    public sealed class DoorShatterDebris : MonoBehaviour
    {
        sealed class Piece
        {
            public Transform transform;public Vector3 start,rest;public Quaternion rotation;
            public Quaternion initial;public float lift;
        }
        readonly List<Piece> pieces=new List<Piece>();
        readonly List<Mesh> meshes=new List<Mesh>();
        Vector3 kick;
        Bounds aperture;
        bool hasAperture;
        DoorBreakDust dust;
        float elapsed;
        public int PieceCount {get;private set;}
        public bool Settled {get;private set;}
        public static DoorShatterDebris Create(Transform parent,IReadOnlyList<Renderer> panels,string identity)
        {
            var source=Resources.Load<GameObject>("DoorDebris/ShatteredDoor");
            if(!source)throw new InvalidOperationException("Missing authored splintered door fragments");
            var root=new GameObject("Persistent shattered door");root.layer=2;root.transform.SetParent(parent,false);
            var debris=root.AddComponent<DoorShatterDebris>();int hash=17;
            foreach(char c in identity??"door")hash=unchecked(hash*31+c);
            var random=new System.Random(hash);
            float R(float a,float b)=>Mathf.Lerp(a,b,(float)random.NextDouble());
            foreach(var panel in panels)
            {
                if(!panel)continue;
                var bounds=LocalBounds(parent,panel);
                if(!debris.hasAperture){debris.aperture=bounds;debris.hasAperture=true;}else debris.aperture.Encapsulate(bounds);
                var centre=bounds.center;var size=bounds.size;
                float scale=Mathf.Clamp(size.x*.9f,.6f,2.2f);
                foreach(var filter in source.GetComponentsInChildren<MeshFilter>())
                {
                    var mesh=filter.sharedMesh;var surface=filter.GetComponent<MeshRenderer>();
                    if(!mesh || !surface)continue;
                    var layout=filter.transform.localPosition;
                    var start=centre+new Vector3(layout.x*size.x,Mathf.Clamp(layout.z*size.y,-size.y*.45f,size.y*.45f),R(-.03f,.03f));
                    debris.Add(mesh,scale,start,surface.sharedMaterials,
                        new Vector3(centre.x+(layout.x+R(-.015f,.015f))*scale,0,centre.z+(layout.z+R(-.02f,.02f))*scale),
                        Quaternion.Euler(0,R(-18,18),0),Quaternion.Euler(90,0,R(-12,12)),R(.2f,.65f));
                }
            }
            debris.PieceCount=debris.pieces.Count;debris.Pose(0);
            return debris;
        }
        static Bounds LocalBounds(Transform root,Renderer renderer)
        {
            var local=renderer.localBounds;var bounds=new Bounds();bool first=true;
            for(int x=-1;x<=1;x+=2)for(int y=-1;y<=1;y+=2)for(int z=-1;z<=1;z+=2)
            {
                var point=root.InverseTransformPoint(renderer.transform.TransformPoint(local.center+Vector3.Scale(local.extents,new Vector3(x,y,z))));
                if(first){bounds=new Bounds(point,Vector3.zero);first=false;}else bounds.Encapsulate(point);
            }
            return bounds;
        }
        void Add(Mesh mesh,float scale,Vector3 start,Material[] surfaces,Vector3 rest,Quaternion rotation,Quaternion initial,float lift)
        {
            var obj=new GameObject("Modeled splintered timber");obj.layer=2;obj.transform.SetParent(transform,false);obj.transform.localPosition=start;
            obj.transform.localScale=Vector3.one*scale;obj.AddComponent<MeshFilter>().sharedMesh=mesh;
            obj.AddComponent<MeshRenderer>().sharedMaterials=surfaces;
            var world=transform.TransformPoint(rest);
            var hits=Physics.RaycastAll(new Vector3(world.x,transform.position.y+.45f,world.z),Vector3.down,1.1f,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore);
            float floor=transform.position.y;float best=float.MaxValue;
            foreach(var hit in hits)
            {
                if(hit.normal.y<.8f || Mathf.Abs(hit.point.y-transform.position.y)>.4f ||
                    hit.collider.GetComponentInParent<PlayerMotor>() || hit.collider.GetComponentInParent<NavMeshAgent>())continue;
                if(hit.distance<best){best=hit.distance;floor=hit.point.y;}
            }
            world.y=floor-mesh.bounds.min.y*scale+.006f;rest=transform.InverseTransformPoint(world);
            pieces.Add(new Piece{transform=obj.transform,start=start,rest=rest,rotation=rotation,initial=initial,lift=lift});
        }
        public void Launch(Vector3 worldDirection)
        {
            // A restored fracture is already batched; live creation requests keep
            // pieces available until the caller chooses launch or silent settling.
            if(Settled)return;
            kick=transform.InverseTransformDirection(Vector3.ProjectOnPlane(worldDirection,Vector3.up).normalized);
            elapsed=0;Pose(0);
            if(hasAperture && !dust)dust=DoorBreakDust.Create(transform,aperture,worldDirection);
        }
        public void Seal(){if(!Settled){Pose(1);Combine();Settled=true;enabled=false;}}
        void Update()
        {
            var session=GameSession.Current;if(Settled || !session || !session.InputAllowed)return;
            elapsed+=Time.deltaTime;Pose(Mathf.Clamp01(elapsed/1.15f));
            if(elapsed>=1.15f)Seal();
        }
        void Pose(float amount)
        {
            float travel=1-Mathf.Pow(1-amount,2);
            foreach(var p in pieces)
            {
                if(!p.transform)continue;
                float arc=Mathf.Sin(Mathf.PI*amount);
                p.transform.localPosition=Vector3.Lerp(p.start,p.rest,travel)+kick*(arc*.2f)+Vector3.up*(arc*p.lift);
                p.transform.localRotation=Quaternion.Slerp(p.initial,p.rotation,travel);
            }
        }
        void Combine()
        {
            var groups=new Dictionary<Material,List<CombineInstance>>();
            foreach(var p in pieces)
            {
                if(!p.transform)continue;
                var filter=p.transform.GetComponent<MeshFilter>();var renderer=p.transform.GetComponent<Renderer>();
                for(int sub=0;sub<filter.sharedMesh.subMeshCount;sub++)
                {
                    var material=renderer.sharedMaterials[sub];
                    if(!groups.TryGetValue(material,out var list)){list=new List<CombineInstance>();groups.Add(material,list);}
                    list.Add(new CombineInstance{mesh=filter.sharedMesh,subMeshIndex=sub,transform=transform.worldToLocalMatrix*p.transform.localToWorldMatrix});
                }
            }
            var combined=new List<CombineInstance>();var materials=new List<Material>();var temporary=new List<Mesh>();
            foreach(var entry in groups)
            {
                var mesh=new Mesh{indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};mesh.CombineMeshes(entry.Value.ToArray(),true,true);temporary.Add(mesh);
                combined.Add(new CombineInstance{mesh=mesh,transform=Matrix4x4.identity});materials.Add(entry.Key);
            }
            if(combined.Count>0)
            {
                var mesh=new Mesh{name="Authored settled broken door",indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};mesh.CombineMeshes(combined.ToArray(),false,true);meshes.Add(mesh);
                gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;gameObject.AddComponent<MeshRenderer>().sharedMaterials=materials.ToArray();
            }
            foreach(var mesh in temporary)Release(mesh);
            foreach(var p in pieces)if(p.transform){p.transform.gameObject.SetActive(false);Release(p.transform.gameObject);}
            pieces.Clear();
        }
        static void Release(UnityEngine.Object resource)
        { if(Application.isPlaying)Destroy(resource);else DestroyImmediate(resource); }
        void OnDestroy()
        {
            foreach(var mesh in meshes)if(mesh)Release(mesh);
        }
    }
}
