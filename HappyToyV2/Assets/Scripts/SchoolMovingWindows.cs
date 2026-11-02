using System.Collections.Generic;
using UnityEngine;

namespace HappyToy.V2
{
    // The fitted window visuals slide independently; the structural glass
    // collider remains in place so the room boundary stays sealed.
    public sealed class SchoolMovingWindows : MonoBehaviour
    {
        sealed class Window { public Transform visual; public Vector3 rest; public float travel; }
        readonly List<Window> windows=new List<Window>();
        bool active;float elapsed;
        public int Count=>windows.Count;
        public void Register(Transform visual,float travel)=>windows.Add(new Window{visual=visual,rest=visual.localPosition,travel=travel});
        public void SetActive(bool value)
        {
            active=value;elapsed=0;
            if(!active)foreach(var window in windows)if(window.visual)window.visual.localPosition=window.rest;
        }
        public void RestoreElapsed(float value){elapsed=value;Tick(0,true);}
        public void Tick(float delta,bool advancing)
        {
            if(!active||!advancing)return;
            elapsed=Mathf.Min(1000000,elapsed+Mathf.Max(0,delta));
            for(int i=0;i<windows.Count;i++)
            {
                var window=windows[i];if(!window.visual)continue;
                window.visual.localPosition=window.rest+Vector3.forward*(window.travel*SchoolFrenzyMotion.Amount(elapsed,i,2));
            }
        }
    }
}
