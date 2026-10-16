using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;
using static HappyToy.V2.CloudTests.RuntimeAccess;
using Object = UnityEngine.Object;

namespace HappyToy.V2.CloudTests
{
    public sealed partial class CloudPlayModeTests
    {
        [Test]
        public void DiagnosticDesktopCursorSuppressionRequiresExplicitBatchOptIn()
        {
            var policy=RequireType("DiagnosticDesktopPolicy");
            foreach(bool requested in new[]{false,true})foreach(bool batch in new[]{false,true})
                Assert.That((bool)Call(policy,"ShouldSuppressCursorWrites",requested,batch),
                    Is.EqualTo(requested&&batch),"Cursor suppression must require both explicit opt-in and batch execution");
            // Only pure inputs are exercised: no process arguments, real cursor,
            // focus, input devices or normal gameplay settings are modified.
        }

        [UnityTest,Timeout(60000)]
        public IEnumerator NativeAuditMovementRequiresFiniteOptInAndRestoresActualPlayerSpeeds()
        {
            var type=RequireType("NativeAuditMovement");
            float walk=Get<float>(player,"walkSpeed"),run=Get<float>(player,"runSpeed"),clock=Time.timeScale;
            Component movement=null;
            try
            {
                Assert.That(player.GetComponent(type),Is.Null,"Ordinary scene load installed diagnostic acceleration");
                foreach(float invalid in new[]{0f,.99f,12.01f,float.NaN,float.PositiveInfinity,float.NegativeInfinity})
                    Assert.Throws<ArgumentOutOfRangeException>(()=>Call(type,"Attach",player,invalid));
                Assert.That(player.GetComponent(type),Is.Null,"Rejected multiplier left a diagnostic owner behind");
                movement=(Component)Call(type,"Attach",player,1f);Call(movement,"Apply",true);
                Assert.That(Get<bool>(movement,"Active"),Is.False);Assert.That(Get<float>(movement,"AppliedMultiplier"),Is.EqualTo(1));
                Assert.That(Get<float>(player,"walkSpeed"),Is.EqualTo(walk));Assert.That(Get<float>(player,"runSpeed"),Is.EqualTo(run));
                Object.Destroy(movement);yield return null;movement=null;
                movement=(Component)Call(type,"Attach",player,12f);Call(movement,"Apply",false);
                Assert.That(Get<float>(player,"walkSpeed"),Is.EqualTo(walk));Assert.That(Get<float>(player,"runSpeed"),Is.EqualTo(run));
                Call(movement,"Apply",true);Assert.That(Get<bool>(movement,"Active"),Is.True);
                Assert.That(Get<float>(player,"walkSpeed"),Is.EqualTo(walk*12));Assert.That(Get<float>(player,"runSpeed"),Is.EqualTo(run*12));
                Call(movement,"Restore");Assert.That(Get<bool>(Get<object>(movement,"Data"),"restored"),Is.True);
                Assert.That(Get<float>(player,"walkSpeed"),Is.EqualTo(walk));Assert.That(Get<float>(player,"runSpeed"),Is.EqualTo(run));
                Call(movement,"Apply",true);((Behaviour)movement).enabled=false;
                Assert.That(Get<bool>(movement,"Active"),Is.False);Assert.That(Get<bool>(Get<object>(movement,"Data"),"restored"),Is.True);
                Assert.That(Get<float>(player,"walkSpeed"),Is.EqualTo(walk));Assert.That(Get<float>(player,"runSpeed"),Is.EqualTo(run));
                Assert.That(Time.timeScale,Is.EqualTo(clock),"Movement-only diagnostic changed the game clock");
            }
            finally
            {
                if(movement){Call(movement,"Restore");Object.Destroy(movement);}
                Set(player,"walkSpeed",walk);Set(player,"runSpeed",run);
            }
            Debug.Log("HAPPYTOY_NATIVE_MOVEMENT_BOUNDARY_PASS ordinary opt-out, multiplier1/12, finite range rejection and actual player Restore/disable; measured acceleration distance and normal-speed frame classification require the native report");
        }

        static object PaceCall(Component sampler,string name,params object[] args) =>
            sampler.GetType().GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(sampler,args);
        static T PaceField<T>(Component sampler,string name) =>
            (T)sampler.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(sampler);
        static void PaceSet(Component sampler,string name,object value) =>
            sampler.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(sampler,value);
        static bool PaceNormal(Component sampler) =>
            (bool)sampler.GetType().GetProperty("NormalGameplayPace",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(sampler);
        static void AssertPaceExcludedByActualUpdate(Component sampler)
        {
            PaceSet(sampler,"normalFramesSinceExclusion",50);PaceSet(sampler,"normalEligibleSince",100d);
            PerformanceTick(sampler);
            Assert.That(PaceField<int>(sampler,"normalFramesSinceExclusion"),Is.Zero);
            Assert.That(PaceField<double>(sampler,"normalEligibleSince"),Is.EqualTo(-1));
        }

        [UnityTest,Timeout(60000)]
        public IEnumerator DiagnosticNormalPaceExcludesActualSlowWheelGlobalTimeStopAndPause()
        {
            yield return RevisionPrepare();
            var sampler=new GameObject("CloudQA clock classification only").AddComponent(RequireType("CorridorPerformanceSampling"));
            ((Behaviour)sampler).enabled=false;
            var rituals=Get<Component>(player,"Rituals");
            try
            {
                // Never Begin: no render subscription, capture, counters or FPS
                // measurement. Exercise the real gameplay state and Update gate.
                PaceSet(sampler,"session",session);PaceSet(sampler,"collecting",true);
                PaceSet(sampler,"camera",Get<Camera>(player,"eyes"));
                PaceSet(sampler,"nextSnapshot",double.PositiveInfinity);PaceSet(sampler,"nextLog",double.PositiveInfinity);
                Assert.That(PaceNormal(sampler),Is.True);
                Call(shell,"SetItemWheelOpen",true);
                Assert.That(Get<bool>(session,"InputAllowed"),Is.True,"The slow wheel must exercise the previously missed active-input path");
                Assert.That(Time.timeScale,Is.EqualTo(.25f));Assert.That(PaceNormal(sampler),Is.False);
                AssertPaceExcludedByActualUpdate(sampler);Call(shell,"SetItemWheelOpen",false);
                Assert.That(PaceNormal(sampler),Is.True);
                Time.timeScale=.5f;Assert.That(PaceNormal(sampler),Is.False,"A changed global clock was accepted without the wheel flag");
                AssertPaceExcludedByActualUpdate(sampler);Time.timeScale=1;
                Call(rituals,"Restore",1,0,1);Assert.That((bool)Call(rituals,"UseTalisman"),Is.True);
                Assert.That(Time.timeScale,Is.EqualTo(1));Assert.That(PaceNormal(sampler),Is.False);
                AssertPaceExcludedByActualUpdate(sampler);Call(rituals,"EndEffect");
                Assert.That(PaceNormal(sampler),Is.True);
                PaceSet(sampler,"previous",100d);Call(shell,"Pause");
                // Model a pause noticed by the real-camera callback before
                // another Update can run. No rendering or capture is submitted.
                PaceSet(sampler,"normalFramesSinceExclusion",50);PaceSet(sampler,"normalEligibleSince",100d);
                PaceCall(sampler,"Rendered",default(UnityEngine.Rendering.ScriptableRenderContext),Get<Camera>(player,"eyes"));
                Assert.That(PaceField<int>(sampler,"normalFramesSinceExclusion"),Is.Zero);
                Assert.That(PaceField<double>(sampler,"previous"),Is.EqualTo(-1));
                Assert.That(PaceNormal(sampler),Is.False);AssertPaceExcludedByActualUpdate(sampler);
                Assert.That(PaceField<double>(sampler,"previous"),Is.EqualTo(-1));
                Call(shell,"Resume");Assert.That(PaceNormal(sampler),Is.True);
                Assert.That(PaceField<int>(sampler,"normalFramesSinceExclusion"),Is.Zero,"Resume bypassed the stabilization history");
            }
            finally
            {
                Call(rituals,"EndEffect");Call(shell,"SetItemWheelOpen",false);Call(shell,"Resume");
                Object.DestroyImmediate(sampler.gameObject);
            }
            Debug.Log("HAPPYTOY_DIAGNOSTIC_PACE_CLOCK_PASS actual wheel .25/input-active, global enemy stop, pause/resume exclusions; classification only, not native FPS");
        }

        [Test]
        public void DiagnosticNormalPaceWaitsForFramesTimeAndEntireCleanRollingHistory()
        {
            var sampler=new GameObject("CloudQA bounded pace history only").AddComponent(RequireType("CorridorPerformanceSampling"));
            ((Behaviour)sampler).enabled=false;
            try
            {
                Assert.That((bool)PaceCall(sampler,"ObservePace",10d,false),Is.False);
                for(int frame=1;frame<=7;frame++)
                    Assert.That((bool)PaceCall(sampler,"ObservePace",10d+frame*.04,true),Is.False,"Seven frames cannot establish the eight-frame boundary");
                Assert.That((bool)PaceCall(sampler,"ObservePace",10.32d,true),Is.True);
                PaceCall(sampler,"ObservePace",11d,false);
                for(int frame=1;frame<=8;frame++)
                    Assert.That((bool)PaceCall(sampler,"ObservePace",11d+frame*.02,true),Is.False,"Eight rapid frames cannot bypass the quarter-second boundary");
                Assert.That((bool)PaceCall(sampler,"ObservePace",11.249d,true),Is.False);
                Assert.That((bool)PaceCall(sampler,"ObservePace",11.25d,true),Is.True);
                PaceCall(sampler,"ObservePace",100d,false);
                for(int frame=1;frame<=8;frame++)PaceCall(sampler,"ObservePace",100d+frame*.04,true);
                PaceCall(sampler,"Rolling",.5d,false,100.5d);PaceCall(sampler,"Rolling",.6d,true,101.1d);
                Assert.That(double.IsPositiveInfinity(PaceField<double>(sampler,"minimumRollingFps")),Is.False,"Raw slow/transition intervals were discarded");
                PaceCall(sampler,"Rolling",.2d,true,101.3d);
                Assert.That(double.IsPositiveInfinity(PaceField<double>(sampler,"minimumNormalRollingFps")),Is.True,
                    "A long interval spanning the excluded epoch entered normal rolling evidence");
                PaceCall(sampler,"Rolling",.8d,true,102.1d);
                Assert.That(double.IsPositiveInfinity(PaceField<double>(sampler,"minimumNormalRollingFps")),Is.False,
                    "Wholly stabilized rolling history never became eligible");
            }
            finally{Object.DestroyImmediate(sampler.gameObject);}
        }

        static T TrapPerformanceStatic<T>(string name)
        {
            var type=RequireType("PlacedMonsterTrap");
            var property=type.GetProperty(name,BindingFlags.Public|BindingFlags.Static);
            if(property!=null)return (T)property.GetValue(null);
            var field=type.GetField(name,BindingFlags.NonPublic|BindingFlags.Static);
            Assert.That(field,Is.Not.Null,"Missing trap lifetime diagnostic "+name);
            return (T)field.GetValue(null);
        }
        static void PerformanceTick(Component component)
        {
            var method=component.GetType().GetMethod("Update",BindingFlags.Instance|BindingFlags.NonPublic);
            Assert.That(method,Is.Not.Null);method.Invoke(component,null);
        }
        Component PerformanceTrap(Vector3 point,Transform root) =>
            (Component)Call(RequireType("PlacedMonsterTrap"),"Create",root,point,Quaternion.identity,false);
        static void AssertTrapSnapshotReleased()
        {
            Assert.That(TrapPerformanceStatic<NavMeshAgent[]>("actorSnapshot"),Is.Empty);
            Assert.That(TrapPerformanceStatic<object>("snapshotSession"),Is.Null);
            Assert.That(TrapPerformanceStatic<object>("snapshotRoot"),Is.Null);
            Assert.That(TrapPerformanceStatic<int>("snapshotFrame"),Is.EqualTo(-1));
            Assert.That(TrapPerformanceStatic<int>("activeTraps"),Is.Zero);
        }
        Component[] PerformanceTrafficActors(out NavMeshAgent[] agents,out Vector3 centre,out Vector3 forward)
        {
            var door=Components("Interactable").First(x=>Get<object>(x,"kind").ToString()=="Door" && x.name=="Corridor sliding door");
            centre=door.transform.position;forward=door.transform.forward;
            agents=new NavMeshAgent[2];var traffic=new Component[2];
            var starts=new[]{centre,centre-forward*4};var goals=new[]{centre-forward*4,centre+forward*4};
            for(int i=0;i<2;i++)
            {
                var root=new GameObject(i==0?"CloudQA Cyclopse traffic":"CloudQA Uncat traffic");
                root.transform.SetParent(session.transform,false);root.transform.position=starts[i];
                var agent=root.AddComponent<NavMeshAgent>();agents[i]=agent;
                agent.radius=.3f;agent.height=1.8f;agent.speed=i==0?3.4f:0;
                Assert.That(NavMesh.SamplePosition(starts[i],out var at,.4f,NavMesh.AllAreas)&&agent.Warp(at.position),Is.True);
                root.transform.rotation=Quaternion.LookRotation(i==0?-forward:forward);
                traffic[i]=root.AddComponent(RequireType("EnemyPassageTraffic"));Call(traffic[i],"Configure",agent);
                var path=new NavMeshPath();Assert.That(agent.CalculatePath(goals[i],path),Is.True);
                agent.SetPath(path);agent.isStopped=i==0;Call(traffic[i],"ObserveRoute",goals[i]);
            }
            return traffic;
        }

        static string PerformanceTrafficDiagnostics(Component[] traffic,NavMeshAgent[] agents) =>
            string.Join("\n",traffic.Select((actor,i)=>
            {
                var flags=BindingFlags.Instance|BindingFlags.NonPublic;
                var type=actor.GetType();var agent=agents[i];
                bool ready=agent && agent.enabled && agent.isOnNavMesh;
                return actor.name+" position="+actor.transform.position+" ready="+ready+
                    " live="+type.GetProperty("Live",flags).GetValue(actor)+
                    " heading="+type.GetProperty("Heading",flags).GetValue(actor)+
                    " blockedTime="+type.GetField("blockedTime",flags).GetValue(actor)+
                    " priority="+Get<int>(actor,"RightOfWayPriority")+
                    " intent="+Get<Vector3>(actor,"IntendedPoint")+
                    (ready?" stopped="+agent.isStopped+" hasPath="+agent.hasPath+" status="+agent.pathStatus+" velocity="+agent.velocity:string.Empty);
            }));

        static void AssertPerformanceRouteLive(Component traffic,NavMeshAgent agent,Vector3 goal,Vector3 forward)
        {
            Assert.That(agent.enabled && agent.isOnNavMesh,Is.True,"The fixture detached its native actor from the real corridor");
            var route=new NavMeshPath();
            Assert.That(agent.CalculatePath(goal,route) && route.status==NavMeshPathStatus.PathComplete,Is.True,
                "The retreat barrier also cut the actor's genuine forward route");
            agent.SetPath(route);agent.isStopped=false;Call(traffic,"ObserveRoute",goal);
            Assert.That(agent.hasPath,Is.True,"A blocked fixture must retain its native route");
            var heading=goal-agent.transform.position;heading.y=0;
            Assert.That(Vector3.Dot(heading.normalized,forward),Is.GreaterThan(.99f),"The blocked actor lost its forward intent");
            Assert.That((bool)traffic.GetType().GetProperty("Live",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(traffic),Is.True,
                "The blocked actor's actual owner/navigation/intent gate is not live");
        }

        [UnityTest,Timeout(90000)]
        public IEnumerator CorridorFailedSidingSearchesStayBoundedAndRetryAfterTheLaneChanges()
        {
            yield return RevisionPrepare();((Behaviour)player).enabled=false;PlacePlayer(new Vector3(200,5,200),false);
            foreach(var door in Components("Interactable").Where(x=>Get<object>(x,"kind").ToString()=="Door"))Call(door,"OpenForPursuer");
            yield return Delay(1.5f);
            var traffic=PerformanceTrafficActors(out var agents,out _,out var forward);
            agents[0].radius=1.2f; // The same genuine doorway footprint used by the passing regression.
            var goals=traffic.Select(actor=>Get<Vector3>(actor,"IntendedPoint")).ToArray();
            agents[1].isStopped=true;Call(traffic[1],"Cancel");
            var origin=agents[1].transform.position;
            var barrier=new GameObject("CloudQA temporarily closed retreat behind the traffic lane");
            barrier.transform.SetParent(session.transform,false);
            barrier.transform.SetPositionAndRotation(origin-forward*2.1f+Vector3.up*1.2f,Quaternion.LookRotation(forward));
            // Its front ends 35cm behind the stationary actor. The forward
            // doorway route stays open while all short backward sidings close.
            barrier.AddComponent<BoxCollider>().size=new Vector3(7,3,3.5f);
            var obstacle=barrier.AddComponent<NavMeshObstacle>();obstacle.shape=NavMeshObstacleShape.Box;
            obstacle.size=new Vector3(7,3,3.5f);obstacle.carving=true;obstacle.carveOnlyStationary=false;
            var filter=new NavMeshQueryFilter{agentTypeID=agents[1].agentTypeID,areaMask=agents[1].areaMask};
            Vector3 closedFloor=origin-forward;
            try
            {
                yield return Wait(()=>!NavMesh.SamplePosition(closedFloor,out _, .18f,filter),3,
                    "The physical retreat barrier never updated the real NavMesh",()=>PerformanceTrafficDiagnostics(traffic,agents));
                AssertPerformanceRouteLive(traffic[1],agents[1],goals[1],forward);
                Assert.That(Get<int>(traffic[0],"RightOfWayPriority"),Is.LessThan(Get<int>(traffic[1],"RightOfWayPriority")),
                    "The blocker must keep the real Cyclopse/Uncat right of way");
                yield return Wait(()=>Get<int>(traffic[1],"SidingSearches")>0,3,"The blocked actor never tested a siding",
                    ()=>PerformanceTrafficDiagnostics(traffic,agents));
                Assert.That(Get<bool>(traffic[1],"Yielding"),Is.False,"A retreat crossed the still-closed physical barrier");
                Assert.That(agents[1].hasPath,Is.True,"Failed sidings discarded the still-open forward route");
                int searches=Get<int>(traffic[1],"SidingSearches");float started=Time.time;
                yield return Wait(()=>Time.time-started>=.8f,5,"Gameplay clock did not advance");
                int attempts=Get<int>(traffic[1],"SidingSearches")-searches;
                Assert.That(attempts,Is.InRange(1,Mathf.CeilToInt((Time.time-started)/.25f)+1),"Failed sidings were retried every rendered frame");
                Assert.That(Get<bool>(traffic[1],"Yielding"),Is.False,"The retry budget check accidentally opened a siding");
                Assert.That(Get<int>(traffic[0],"YieldsStarted"),Is.Zero);
                Call(shell,"Pause");searches=Get<int>(traffic[1],"SidingSearches");yield return Delay(.15f);
                Assert.That(Get<int>(traffic[1],"SidingSearches"),Is.EqualTo(searches));Call(shell,"Resume");
                // Reopen only the actual retreat space; both native radii and
                // the original forward targets remain the same across retry.
                obstacle.enabled=false;barrier.GetComponent<BoxCollider>().enabled=false;
                yield return Wait(()=>NavMesh.SamplePosition(closedFloor,out _, .18f,filter),3,
                    "Removing the retreat barrier never restored its genuine floor");
                Call(traffic[0],"ObserveRoute",goals[0]);
                AssertPerformanceRouteLive(traffic[1],agents[1],goals[1],forward);
                searches=Get<int>(traffic[1],"SidingSearches");
                yield return Wait(()=>Get<bool>(traffic[1],"Yielding"),2,"A newly clear real siding was never retried",
                    ()=>PerformanceTrafficDiagnostics(traffic,agents));
                Assert.That(Get<int>(traffic[1],"SidingSearches"),Is.GreaterThan(searches),"Opening the real retreat did not issue a fresh siding search");
                Assert.That(agents[0].radius,Is.EqualTo(1.2f));Assert.That(agents[1].radius,Is.EqualTo(.3f));
                Assert.That(Get<int>(traffic[0],"YieldsStarted"),Is.Zero,"Both actors yielded after the retry");
            }
            finally {Object.Destroy(barrier);}
        }

        [UnityTest,Timeout(90000)]
        public IEnumerator CorridorYieldPathQueriesAreBoundedAndTimeStopKeepsImmediateLatestTargetResume()
        {
            yield return RevisionPrepare();((Behaviour)player).enabled=false;PlacePlayer(new Vector3(200,5,200),false);
            foreach(var door in Components("Interactable").Where(x=>Get<object>(x,"kind").ToString()=="Door"))Call(door,"OpenForPursuer");
            yield return Delay(1.5f);
            var traffic=PerformanceTrafficActors(out var agents,out var centre,out var forward);agents[0].radius=1.2f;
            float stopping=agents[1].stoppingDistance;
            yield return Wait(()=>Get<bool>(traffic[1],"Yielding"),3,"Blocked actor never started its genuine yield");
            int plans=Get<int>(traffic[1],"YieldPathPlans");float started=Time.time;
            yield return Wait(()=>Time.time-started>=.65f,5,"Gameplay clock did not advance");
            Assert.That(Get<int>(traffic[1],"YieldPathPlans")-plans,
                Is.InRange(1,Mathf.CeilToInt((Time.time-started)/.2f)+1),"Local yield routes were calculated every frame");
            var rituals=Get<Component>(player,"Rituals");Call(rituals,"Restore",1,0,1);
            Assert.That((bool)Call(rituals,"UseTalisman"),Is.True);
            plans=Get<int>(traffic[1],"YieldPathPlans");yield return Delay(.3f);
            Assert.That(Get<bool>(traffic[1],"Yielding"),Is.True,"Time stop consumed the yield deadline");
            Assert.That(Get<int>(traffic[1],"YieldPathPlans"),Is.EqualTo(plans),"Time stop kept querying navigation");
            Call(rituals,"EndEffect");
            Assert.That(NavMesh.SamplePosition(centre-forward*3,out var latest,.4f,NavMesh.AllAreas),Is.True);
            Call(traffic[1],"ObserveRoute",latest.position);agents[1].speed=1.45f;
            traffic[0].gameObject.SetActive(false);
            yield return null;
            Assert.That(Get<bool>(traffic[1],"Yielding"),Is.False,"Peer removal waited for the throttled replan timer");
            Assert.That(Get<int>(traffic[1],"RoutesResumed"),Is.GreaterThan(0));
            Assert.That(Vector3.Distance(agents[1].destination,latest.position),Is.LessThan(.02f),"Resume restored stale intent");
            Assert.That(agents[1].stoppingDistance,Is.EqualTo(stopping).Within(.001f));
            yield return Wait(()=>Vector3.Distance(agents[1].transform.position,latest.position)<.35f,5,"Actor did not follow its latest intended target");
        }

        [UnityTest,Timeout(90000)]
        public IEnumerator PlacedTrapsShareLiveActorDiscoveryAndDropEverySceneReferenceWithTheirLastOwner()
        {
            yield return RevisionPrepare();((Behaviour)player).enabled=false;PlacePlayer(new Vector3(200,5,200),false);
            var root=Get<Transform>(Get<Component>(player,"Traps"),"WorldRoot");
            Assert.That(NavMesh.SamplePosition(ReadabilityActor("Cyclopse").transform.position,out var at,1.5f,NavMesh.AllAreas),Is.True);
            var traps=new[]{PerformanceTrap(at.position,root),PerformanceTrap(at.position,root),PerformanceTrap(at.position,root)};
            var extra=new GameObject("CloudQA dynamically removed agent");extra.transform.SetParent(root,false);extra.transform.position=at.position;
            var removed=extra.AddComponent<NavMeshAgent>();Assert.That(removed.Warp(at.position),Is.True);
            int scans=TrapPerformanceStatic<int>("ActorScanCount");
            foreach(var trap in traps)PerformanceTick(trap);
            Assert.That(TrapPerformanceStatic<int>("ActorScanCount"),Is.EqualTo(scans+1),"Armed traps did separate whole-scene queries");
            Assert.That(TrapPerformanceStatic<NavMeshAgent[]>("actorSnapshot"),Does.Contain(removed));
            Object.DestroyImmediate(extra);
            Assert.DoesNotThrow(()=>PerformanceTick(traps[1]),"An agent destroyed after the shared scan left a live transform access");
            var actor=StalkerAt(at.position);actor.transform.SetParent(root,true);
            PerformanceTick(traps[0]);Assert.That(Get<bool>(traps[0],"Triggered"),Is.False,"The fixture must add its actor after this frame's scan");
            yield return Wait(()=>traps.Any(trap=>Get<bool>(trap,"Triggered")),1,"A newly created genuine monster was absent from later snapshots");
            Assert.That(traps.Count(trap=>Get<bool>(trap,"Triggered")),Is.EqualTo(1),"Two traps caught the same held actor");
            Assert.That((bool)Call(RequireType("EnemyTrapRestraint"),"IsHeld",actor),Is.True);
            foreach(var trap in traps)trap.gameObject.SetActive(false);
            AssertTrapSnapshotReleased();
            var armed=traps.First(trap=>Get<bool>(trap,"Armed"));armed.gameObject.SetActive(true);
            scans=TrapPerformanceStatic<int>("ActorScanCount");PerformanceTick(armed);
            Assert.That(TrapPerformanceStatic<int>("ActorScanCount"),Is.EqualTo(scans+1),"Re-enabling the last owner reused a retired scene snapshot");
            foreach(var trap in traps)Object.Destroy(trap.gameObject);
            yield return null;AssertTrapSnapshotReleased();
        }

        [UnityTest,Timeout(90000)]
        public IEnumerator TrapActorSnapshotRebindsAChangedWorldInTheSameFrameAndRejectsInactiveActors()
        {
            Call(session,"CreateChapter");IsolateThreats();Begin();((Behaviour)player).enabled=false;
            Assert.That(NavMesh.SamplePosition(player.transform.position,out var at,1.5f,NavMesh.AllAreas),Is.True);
            PlacePlayer(at.position+Vector3.up*5,false);
            var inventory=Get<Component>(player,"Traps");var oldRoot=Get<Transform>(inventory,"WorldRoot");
            NavMeshHit away=default;bool found=false;
            foreach(var side in new[]{Vector3.right,Vector3.left,Vector3.forward,Vector3.back})
                if(NavMesh.SamplePosition(at.position+side,out away,.4f,NavMesh.AllAreas)&&Vector3.Distance(away.position,at.position)>.6f){found=true;break;}
            Assert.That(found,Is.True,"No genuine floor point outside the trap footprint");
            var inactive=StalkerAt(away.position);inactive.transform.SetParent(session.transform,true);
            var trap=PerformanceTrap(at.position,oldRoot);PerformanceTick(trap);
            inactive.gameObject.SetActive(false);inactive.transform.position=at.position;
            PerformanceTick(trap);
            Assert.That(Get<bool>(trap,"Triggered"),Is.False,"An actor disabled after discovery was caught from a stale snapshot");
            var actor=StalkerAt(at.position);actor.transform.SetParent(session.transform,true);
            var next=new GameObject("CloudQA replacement trap world");next.transform.SetParent(session.transform,false);
            int scans=TrapPerformanceStatic<int>("ActorScanCount");Call(inventory,"ConfigureWorld",next.transform);
            var replacement=PerformanceTrap(at.position,next.transform);PerformanceTick(replacement);
            Assert.That(TrapPerformanceStatic<int>("ActorScanCount"),Is.EqualTo(scans+1),"Changing world ownership kept the previous same-frame actor snapshot");
            Assert.That(TrapPerformanceStatic<object>("snapshotRoot"),Is.SameAs(next.transform));
            Assert.That(Get<bool>(replacement,"Triggered"),Is.True,"The new world's genuine actor was absent from its same-frame discovery");
            Assert.That(Get<Component>(replacement,"Captured").gameObject,Is.SameAs(actor.gameObject),"The inactive old actor was captured instead");
            inactive.gameObject.SetActive(true);Assert.That(inactive.GetComponent<NavMeshAgent>().Warp(at.position),Is.True);
            yield return Wait(()=>Get<bool>(trap,"Triggered"),1,"A reactivated monster was never discovered");
            Assert.That(Get<Component>(trap,"Captured").gameObject,Is.SameAs(inactive.gameObject));
            trap.gameObject.SetActive(false);replacement.gameObject.SetActive(false);AssertTrapSnapshotReleased();
        }
    }
}
