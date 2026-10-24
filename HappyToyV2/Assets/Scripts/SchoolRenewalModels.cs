using System;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace HappyToy.V2
{
    // Authored/generated resources only. Architecture retains its physical
    // envelopes; this boundary supplies immutable PBR geometry for presentation.
    public static class SchoolRenewalModels
    {
        public static readonly string[] Keys={"wall-panel","floor-slab","ceiling-panel","classroom-door","classroom-window",
            "pipe-manifold","pipe-run","shoe-bank","school-desk","school-chair","archive-shelf","metal-cabinet","timber-cabinet",
            "fluorescent-fixture","concrete-rubble","loose-stones","radiator","room-sign"};
        public const string Prefix="SchoolRenewal/";
        public static GameObject Source(string key)
        {
            if(!Keys.Contains(key))throw new ArgumentException("Unknown renewed school asset: "+key);
            var prefab=Resources.Load<GameObject>(Prefix+key+"/"+key+"-visual");
            if(!prefab||!prefab.GetComponent<LODGroup>())throw new InvalidOperationException("New school model/LODs missing: "+key);
            return prefab;
        }
        public static Transform Attach(string key,Transform parent,Bounds envelope,float yaw=0,bool spatialBatch=false,Material material=null,int detail=0)
        {
            if(!parent)throw new ArgumentNullException(nameof(parent));
            var source=Source(key);var holder=new GameObject("New school model — "+key).transform;
            holder.SetParent(parent,false);holder.localPosition=envelope.center;holder.localRotation=Quaternion.Euler(0,yaw,0);
            Transform visual;
            if(!spatialBatch)visual=UnityEngine.Object.Instantiate(source,holder,false).transform;
            else
            {
                visual=new GameObject("Shared generated near geometry for spatial batching").transform;visual.SetParent(holder,false);
                // Do not instantiate LODGroups only to destroy them next frame:
                // the world's construction-time batch must see the mesh now.
                foreach(var old in source.GetComponent<LODGroup>().GetLODs()[Mathf.Clamp(detail,0,1)].renderers)
                {
                    var filter=old.GetComponent<MeshFilter>();if(!filter||!filter.sharedMesh)throw new InvalidOperationException("Static school mesh missing");
                    var part=new GameObject(old.name,typeof(MeshFilter),typeof(MeshRenderer)).transform;part.SetParent(visual,false);
                    var matrix=source.transform.worldToLocalMatrix*old.transform.localToWorldMatrix;
                    part.localPosition=matrix.GetColumn(3);part.localRotation=matrix.rotation;part.localScale=matrix.lossyScale;
                    part.GetComponent<MeshFilter>().sharedMesh=filter.sharedMesh;var renderer=part.GetComponent<MeshRenderer>();
                    renderer.sharedMaterials=material?old.sharedMaterials.Select(_=>material).ToArray():old.sharedMaterials;
                    renderer.shadowCastingMode=ShadowCastingMode.On;renderer.receiveShadows=true;
                }
            }
            GraphicsPropLibrary.Fit(visual,new Bounds(Vector3.zero,envelope.size));
            foreach(var child in holder.GetComponentsInChildren<Transform>(true))child.gameObject.layer=8;
            if(holder.GetComponentsInChildren<Collider>(true).Length>0)throw new InvalidOperationException("New school visual must not own route collision");
            return holder;
        }
        public static Transform AtFeet(string key,Transform parent,Vector3 feet,Vector3 size,float yaw=0,bool spatialBatch=false,Material material=null,int detail=0)
            =>Attach(key,parent,new Bounds(feet+Vector3.up*size.y*.5f,size),yaw,spatialBatch,material,detail);
    }
}
