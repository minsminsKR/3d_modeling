using System;
using UnityEngine;

namespace HappyToy.V2
{
    // Visual-only generated models; imported meshes/materials stay Resources-owned.
    public static class CorridorEntranceModels
    {
        // Temporary authoring bridge while the two designated webapp jobs finish.
        // The final entrance asset check requires this to be false.
        public const bool PendingAssetFallback=false;
        public static Transform Group(Transform parent,string name,Vector3 position,Quaternion rotation=default)
        {
            var result=new GameObject(name).transform;result.SetParent(parent,false);result.localPosition=position;
            result.localRotation=rotation.Equals(default(Quaternion))?Quaternion.identity:rotation;result.gameObject.layer=8;return result;
        }
        public static Transform Attach(string key,Transform parent,string name,Vector3 at,Quaternion rotation=default)
        {
            var source=Resources.Load<GameObject>("CorridorEntrance/"+key+"-visual");
            if(!source)throw new InvalidOperationException("Generated entrance visual prefab missing: "+key);
            if(source.GetComponentsInChildren<Collider>(true).Length!=0||source.GetComponentsInChildren<Rigidbody>(true).Length!=0)
                throw new InvalidOperationException("Entrance visual prefab unexpectedly owns physics: "+key);
            var holder=Group(parent,name,at,rotation);UnityEngine.Object.Instantiate(source,holder,false);
            foreach(var child in holder.GetComponentsInChildren<Transform>(true))child.gameObject.layer=8;
            return holder;
        }
        public static Transform Pending(string key,Transform parent,string name,Vector3 at,out bool fallback)
        {
            fallback=!Resources.Load<GameObject>("CorridorEntrance/"+key+"-visual");
            if(fallback&&!PendingAssetFallback)throw new InvalidOperationException("Required ritual entrance asset absent: "+key);
            return Attach(fallback?"cave-rock":key,parent,name,at);
        }
        public static void FitBox(Transform holder,Vector3 centre,Vector3 size,Quaternion artRotation)
        {
            holder.GetChild(0).localRotation=artRotation*holder.GetChild(0).localRotation;
            var bounds=GraphicsPropLibrary.LocalBounds(holder);
            var scale=new Vector3(size.x/Mathf.Max(bounds.size.x,.001f),size.y/Mathf.Max(bounds.size.y,.001f),size.z/Mathf.Max(bounds.size.z,.001f));
            holder.localScale=scale;holder.localPosition=centre-Vector3.Scale(bounds.center,scale);
        }
        public static void GroundAtHeight(Transform holder,Vector3 at,float height,Quaternion artRotation)
        {
            holder.GetChild(0).localRotation=artRotation*holder.GetChild(0).localRotation;
            var bounds=GraphicsPropLibrary.LocalBounds(holder);float scale=height/Mathf.Max(bounds.size.y,.001f);
            holder.localScale=Vector3.one*scale;
            holder.localPosition=at-new Vector3(bounds.center.x,bounds.min.y,bounds.center.z)*scale;
        }
    }
}
