using System.Collections.Generic;
using UnityEngine;

namespace HappyToy.V2
{
    // Two bounded incense histories, without grazing floor-fog sheets. No global
    // fog, particle spawning, scene capture or readback; paused time never catches up.
    public sealed class CorridorEntranceVapour : MonoBehaviour
    {
        readonly IncenseSmokeTrail[] plumes=new IncenseSmokeTrail[2];
        readonly Vector3[] sources=new Vector3[2];
        Camera eye;float accumulated;
        public float SimulationTime {get;private set;}
        public int PlumeCount=>plumes.Length;
        public static CorridorEntranceVapour Attach(Transform parent,IReadOnlyList<Vector3> incenseSources)
        {
            var root=CorridorEntranceModels.Group(parent,"Owned local incense and ground vapour",Vector3.zero);
            var result=root.gameObject.AddComponent<CorridorEntranceVapour>();
            for(int i=0;i<result.sources.Length;i++)result.sources[i]=incenseSources[i];return result;
        }
        void Start()
        {
            var session=GameSession.Current;if(!session||!session.player)return;eye=session.player.eyes;
            for(int i=0;i<plumes.Length;i++)
            {
                var root=CorridorEntranceModels.Group(transform,"Bounded ritual incense plume "+i,Vector3.zero);
                plumes[i]=root.gameObject.AddComponent<IncenseSmokeTrail>();plumes[i].Prepare(eye);
            }
        }
        void Update()
        {
            var session=GameSession.Current;
            if(!session||!session.InputAllowed||!eye||(eye.transform.position-transform.position).sqrMagnitude>32*32){accumulated=0;return;}
            accumulated+=Time.deltaTime;if(accumulated<.05f)return;
            float delta=Mathf.Min(accumulated,.1f);accumulated=0;SimulationTime+=delta;
            for(int i=0;i<plumes.Length;i++)if(plumes[i])
                plumes[i].Advance(transform.TransformPoint(sources[i]),transform.TransformDirection(i==0?Vector3.right:Vector3.left),delta,1);
        }
    }
}
