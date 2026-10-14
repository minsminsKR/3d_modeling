using UnityEngine;
using UnityEngine.AI;

namespace HappyToy.V2
{
    // Follow tile centres through tight turns while the AI retains its real
    // final known target and complete route evidence. No warps or wall bypass.
    [DisallowMultipleComponent, DefaultExecutionOrder(-50)]
    public sealed class CorridorRouteGuide : MonoBehaviour
    {
        int[] connections;
        int width, current = -1, next = -1;
        Vector3 origin, centreTarget;
        float spacing;
        bool reachedCentre;
        NavMeshAgent navigation;
        Behaviour navigationOwner;
        bool authoredRotation;
        int passageFacingFrame=-1;
        NavMeshPath segment;
        public Vector3 IntermediateTarget { get; private set; }
        public int GuidedSegments { get; private set; }
        public void FacePassage(Vector3 point,float seconds)
        {
            passageFacingFrame=Time.frameCount;
            var direction=point-transform.position;direction.y=0;
            if(direction.sqrMagnitude>.001f)transform.rotation=Quaternion.RotateTowards(transform.rotation,
                Quaternion.LookRotation(direction),240*seconds);
        }
        public void Configure(int[] topology, int rowWidth, Vector3 firstCell, float cellSpacing, Behaviour owner)
        {
            connections = (int[])topology.Clone(); width = rowWidth; origin = firstCell; spacing = cellSpacing; current = next = -1;
            segment = new NavMeshPath();
            navigationOwner=owner; navigation=GetComponent<NavMeshAgent>(); authoredRotation=navigation.updateRotation; navigation.updateRotation=false;
            var traffic=GetComponent<EnemyPassageTraffic>();if(!traffic)traffic=gameObject.AddComponent<EnemyPassageTraffic>();traffic.Configure(owner);
        }
        int Cell(Vector3 position)
        {
            int x = Mathf.RoundToInt((position.x-origin.x)/spacing), z = Mathf.RoundToInt((position.z-origin.z)/spacing);
            return x < 0 || x >= width || z < 0 || z*width+x >= connections.Length ? -1 : z*width+x;
        }
        bool Centre(int cell, NavMeshAgent agent, out Vector3 point)
        {
            var at = origin + new Vector3(cell % width * spacing, 0, cell / width * spacing);
            var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
            if (NavMesh.SamplePosition(at, out var hit, .25f, filter) && Mathf.Abs(hit.position.y-origin.y)<.2f)
            { point=hit.position; return true; }
            // A memory altar can occupy the exact centre. Use a nearby genuine
            // floor point, keeping the guide out of elevated tabletop islands.
            float best=float.PositiveInfinity; point=at; bool found=false;
            for (int direction=0; direction<8; direction++)
            {
                float angle=direction*Mathf.PI/4; var candidate=at+new Vector3(Mathf.Sin(angle),0,Mathf.Cos(angle))*.9f;
                if (!NavMesh.SamplePosition(candidate,out hit,.2f,filter)||Mathf.Abs(hit.position.y-origin.y)>=.2f) continue;
                float distance=(agent.transform.position-hit.position).sqrMagnitude;
                if(distance<best) { point=hit.position; best=distance; found=true; }
            }
            return found;
        }
        public bool Follow(NavMeshAgent agent, NavMeshPath intended)
        {
            if(connections==null || intended.corners.Length==0) return agent.SetPath(intended);
            int cell=Cell(agent.transform.position), goal=Cell(intended.corners[intended.corners.Length-1]);
            if(cell<0 || goal<0 || cell==goal) return agent.SetPath(intended);
            int step=CorridorRouteRules.NextCell(connections,width,cell,goal);
            if(step<0) return false;
            if(cell!=current || step!=next)
            {
                current=cell; next=step; reachedCentre=false;
                if(!Centre(cell,agent,out centreTarget)) return agent.SetPath(intended);
                // An actor already facing its next opening can leave the tile
                // directly. Do not send it backwards to a centre it just passed.
                var heading=origin+new Vector3(step%width*spacing,0,step/width*spacing)-agent.transform.position; heading.y=0;
                reachedCentre=Vector3.Dot(agent.transform.forward,heading.normalized)>.75f;
            }
            var delta=agent.transform.position-centreTarget; delta.y=0;
            reachedCentre |= delta.sqrMagnitude<.45f*.45f;
            Vector3 destination=centreTarget;
            if(reachedCentre && !Centre(step,agent,out destination)) return agent.SetPath(intended);
            if(!agent.CalculatePath(destination,segment)||segment.status!=NavMeshPathStatus.PathComplete) return false;
            IntermediateTarget=destination; GuidedSegments++; return agent.SetPath(segment);
        }
        void LateUpdate()
        {
            if(passageFacingFrame==Time.frameCount || !navigationOwner || !navigationOwner.isActiveAndEnabled || !EnemyNavigation.Ready(navigation) || navigation.isStopped || Time.timeScale<=0) return;
            var direction=navigation.desiredVelocity; direction.y=0;
            // Use intended motion even when a wall correction cancels actual
            // displacement; otherwise native zero-velocity yaw can stay wedged.
            if(direction.sqrMagnitude>.0036f) transform.rotation=Quaternion.RotateTowards(transform.rotation,
                Quaternion.LookRotation(direction),Mathf.Max(240,navigation.angularSpeed)*Time.deltaTime);
        }
        void OnEnable() { if(navigation) navigation.updateRotation=false; }
        void OnDisable() { current=next=-1; reachedCentre=false; if(navigation) navigation.updateRotation=authoredRotation; }
    }
}
