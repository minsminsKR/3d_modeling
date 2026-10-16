using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace HappyToy.V2
{
    [DefaultExecutionOrder(-200)]
    public sealed class PlacedMonsterTrap : MonoBehaviour
    {
        static NavMeshAgent[] actorSnapshot=System.Array.Empty<NavMeshAgent>();
        static GameSession snapshotSession;
        static Transform snapshotRoot;
        static int snapshotFrame=-1,activeTraps;
        readonly Dictionary<NavMeshAgent,Vector3> previous = new Dictionary<NavMeshAgent,Vector3>();
        readonly List<NavMeshAgent> retiredActors=new List<NavMeshAgent>();
        bool registered;
        TrapVisuals visual;
        TrapAudio audioCue;
        EnemyTrapRestraint captured;
        int ticket;
        float closed, spentSeconds;
        public bool Triggered { get; private set; }
        public bool Spent { get; private set; }
        public bool Armed => !Triggered;
        public EnemyTrapRestraint Captured => captured;
        public static int ActorScanCount { get; private set; }
        static NavMeshAgent[] Actors(GameSession session)
        {
            var root=session.player && session.player.Traps?session.player.Traps.WorldRoot:null;
            if(snapshotFrame!=Time.frameCount || !ReferenceEquals(snapshotSession,session) || !ReferenceEquals(snapshotRoot,root))
            {
                actorSnapshot=FindObjectsByType<NavMeshAgent>(FindObjectsSortMode.None);
                snapshotFrame=Time.frameCount;snapshotSession=session;snapshotRoot=root;ActorScanCount++;
            }
            return actorSnapshot;
        }
        void OnEnable(){if(!registered){registered=true;activeTraps++;}}
        void ReleaseSnapshot()
        {
            previous.Clear();retiredActors.Clear();
            if(!registered)return;
            registered=false;activeTraps--;
            if(activeTraps!=0)return;
            actorSnapshot=System.Array.Empty<NavMeshAgent>();snapshotSession=null;snapshotRoot=null;snapshotFrame=-1;
        }
        void OnDisable()=>ReleaseSnapshot();
        void OnDestroy()=>ReleaseSnapshot();
        public static PlacedMonsterTrap Create(Transform parent, Vector3 point, Quaternion facing, bool playPlacement)
        {
            var root = new GameObject("Placed iron monster trap"); root.layer = 2;
            root.transform.SetParent(parent,false); root.transform.SetPositionAndRotation(point+Vector3.up*.006f,facing);
            var trap = root.AddComponent<PlacedMonsterTrap>(); trap.visual = TrapVisuals.Attach(root.transform);
            trap.audioCue = root.AddComponent<TrapAudio>();
            if (playPlacement) trap.audioCue.Play("trap-place");
            return trap;
        }
        void Update()
        {
            var session = GameSession.Current;
            if (!session || session.EncountersResolved) { Destroy(gameObject); return; }
            if (!session.InputAllowed) return;
            if (!Triggered)
            {
                foreach(var entry in previous)if(!entry.Key)retiredActors.Add(entry.Key);
                foreach(var actor in retiredActors)previous.Remove(actor);
                retiredActors.Clear();
                foreach (var actor in Actors(session))
                {
                    // One scene query serves all armed traps in this frame;
                    // destruction/activation and eligibility remain live checks.
                    if(!actor || !actor.gameObject.activeInHierarchy)continue;
                    var next = actor.transform.position;
                    bool had = previous.TryGetValue(actor,out var last); previous[actor] = next;
                    if (!EnemyTrapRestraint.Eligible(actor) || Mathf.Abs(next.y-transform.position.y) > .4f+Mathf.Abs(actor.baseOffset)) continue;
                    var offset = next-transform.position; offset.y = 0;
                    if (had && Vector3.Distance(last,next) < 1.5f && Mathf.Abs(last.y-next.y) < .25f)
                    {
                        var step = next-last; step.y = 0;
                        if (step.sqrMagnitude > .000001f)
                        {
                            var start = last-transform.position; start.y = 0;
                            offset = start+step*Mathf.Clamp01(-Vector3.Dot(start,step)/step.sqrMagnitude);
                        }
                    }
                    if (offset.magnitude > TrapPlacement.Radius+Mathf.Min(.16f,actor.radius*.5f)) continue;
                    captured = EnemyTrapRestraint.TryCatch(actor);
                    if (!captured) continue;
                    Triggered = true; ticket = captured.Sequence; previous.Clear();
                    visual.Consume(); audioCue.Play("trap-trigger"); break;
                }
            }
            if (Triggered && !Spent && (!captured || !captured.Held || captured.Sequence != ticket))
            { Spent = true; audioCue.Play("trap-release"); }
            float target = !Triggered ? 0 : Spent ? .55f : 1;
            closed = Mathf.MoveTowards(closed,target,Time.deltaTime/(Spent ? .22f : .11f)); visual.SetClosed(closed);
            if (Spent) { spentSeconds += Time.deltaTime; if (spentSeconds > 12) Destroy(gameObject); }
        }
    }
}
