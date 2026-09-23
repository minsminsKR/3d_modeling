using System.Collections;
using UnityEngine;
using UnityEngine.AI;

namespace HappyToy.V2
{
    // Always-active actors are serialized with their agent disabled. Native agent OnEnable
    // can otherwise run before the scene's NavMeshSurface registers its baked data in players.
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class NavMeshStartup:MonoBehaviour
    {
        public bool Ready {get;private set;}
        IEnumerator Start()
        {
            var agent=GetComponent<NavMeshAgent>();float deadline=Time.realtimeSinceStartup+5;
            var filter=new NavMeshQueryFilter{agentTypeID=agent.agentTypeID,areaMask=agent.areaMask};
            while(Time.realtimeSinceStartup<deadline)
            {
                if(NavMesh.SamplePosition(transform.position,out var hit,.75f,filter)&&Mathf.Abs(hit.position.y-transform.position.y)<.5f)
                {
                    transform.position=hit.position;agent.enabled=true;
                    if(agent.isOnNavMesh){Ready=true;yield break;}
                    agent.enabled=false;
                }
                yield return null;
            }
            Debug.LogError("No walkable spawn for "+name,this);
        }
    }
}
