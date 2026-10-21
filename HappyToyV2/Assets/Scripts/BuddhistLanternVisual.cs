using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace HappyToy.V2
{
    // Shared visual boundary for the entrance and maze. Imported PBR and LODs
    // remain Resources-owned; callers own placement and the real light source.
    public static class BuddhistLanternVisual
    {
        public const string ResourceKey="CorridorEntrance/buddhist-lantern-visual";
        public static Transform Attach(Transform parent,float height,Quaternion rotation=default)
        {
            if(!parent||height<=0)throw new ArgumentException("Lantern needs a parent and positive height");
            var prefab=Resources.Load<GameObject>(ResourceKey);
            if(!prefab||!prefab.GetComponent<LODGroup>())throw new InvalidOperationException("Generated Buddhist lantern/LODs missing");
            if(prefab.GetComponentsInChildren<Collider>(true).Length!=0||prefab.GetComponentsInChildren<Rigidbody>(true).Length!=0)
                throw new InvalidOperationException("Lantern visual must not own physics");
            var holder=new GameObject("Generated hexagonal Buddhist lantern").transform;
            holder.SetParent(parent,false);UnityEngine.Object.Instantiate(prefab,holder,false);
            var bounds=GraphicsPropLibrary.LocalBounds(holder);
            float scale=height/Mathf.Max(.001f,bounds.size.y);
            var visual=holder.GetChild(0);
            visual.localPosition-=bounds.center;
            holder.localScale=Vector3.one*scale;
            holder.localRotation=rotation.Equals(default(Quaternion))?Quaternion.identity:rotation;
            foreach(var child in holder.GetComponentsInChildren<Transform>(true))child.gameObject.layer=8;
            foreach(var renderer in holder.GetComponentsInChildren<MeshRenderer>(true))
            {renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=true;}
            return holder;
        }
    }
}
