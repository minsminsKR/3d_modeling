using System.Collections.Generic;
using UnityEngine;

namespace HappyToy.V2
{
    public sealed class SchoolMovingDrawers : MonoBehaviour
    {
        readonly List<Transform> drawers=new List<Transform>();
        bool active;float elapsed;
        public int Count=>drawers.Count;
        public void RestoreElapsed(float value){elapsed=value;Tick(0,true);}
        public void Add(SchoolLoopWorld world,Transform desk,Material wood)
        {
            var drawer=new GameObject("School desk sliding drawer").transform;drawer.SetParent(desk,false);
            drawer.gameObject.AddComponent<SchoolDrawerMovingPart>();
            // A front, bottom and two side walls create a visible open compartment.
            world.Box(drawer,"School drawer front",new Vector3(0,.60f,-.30f),new Vector3(.43f,.12f,.025f),wood);
            world.Box(drawer,"School drawer bottom",new Vector3(0,.54f,-.14f),new Vector3(.41f,.018f,.31f),wood);
            foreach(float side in new[]{-.20f,.20f})
                world.Box(drawer,"School drawer side",new Vector3(side,.59f,-.14f),new Vector3(.018f,.10f,.31f),wood);
            drawers.Add(drawer);
        }
        public void SetActive(bool value)
        {
            active=value;elapsed=0;
            if(!active)foreach(var drawer in drawers)if(drawer)drawer.localPosition=Vector3.zero;
        }
        public void Tick(float delta,bool advancing)
        {
            if(!active||!advancing)return;
            elapsed=Mathf.Min(1000000,elapsed+Mathf.Max(0,delta));
            for(int i=0;i<drawers.Count;i++)if(drawers[i])
                drawers[i].localPosition=new Vector3(0,0,-.34f*(1-Mathf.Cos(elapsed*3.1f+i*.37f))*.5f);
        }
    }
}
