using System;
using UnityEngine;
namespace HappyToy.V2
{
    public static class PlayerItemVisuals
    {
        public static Transform Attach(string key,Transform parent)
        {
            var prefab=Resources.Load<GameObject>("PlayerItems/"+key);
            if (!prefab) throw new InvalidOperationException("Missing generated player item: "+key);
            return UnityEngine.Object.Instantiate(prefab,parent,false).transform;
        }
    }
}
