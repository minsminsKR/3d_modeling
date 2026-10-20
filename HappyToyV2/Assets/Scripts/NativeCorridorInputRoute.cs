using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace HappyToy.V2
{
    // Shared opt-in known-map input strategy for native audits and PlayMode
    // survival tests. No reflection, NUnit, direct interactions or AI changes.
    public sealed class NativeCorridorInputRoute : IDisposable
    {
        readonly GameSession session; readonly PlayerMotor player; readonly GameShell shell;
        readonly Action<Key[]> keys;
        readonly Camera eyes;
        readonly CharacterController capsule;
        readonly Mouse mouse, previousMouse;
        Keyboard observedKeyboard;
        readonly StalkerBrain[] monsters;
        readonly Vector2[] speeds;
        readonly List<string> milestones = new List<string>();
        readonly float wallStart; readonly string evidenceDirectory;
        readonly int performancePatrolSeconds;
        readonly NativeAuditMovement movement;
        public NativeAuditMovement Movement=>movement;
        float movementLastGame;
        public bool Passed => passed; public Mouse VirtualMouse => mouse;
        [Serializable] public sealed class PatrolEvidence
        {
            public int requestedSeconds,waypointCount,completedLegs,activeThreatsAtStart,minimumActiveThreats;
            public bool started,completed;
            public float preparationWallSeconds,observationStartRouteWallSeconds,observationStartRouteGameSeconds,wallSeconds,gameSeconds,physicalMeters,hiddenGameSeconds;
            public string scope="Optional movement after the fourth memory. Observation starts only after four production enemies are active. Uses the existing keyboard/mouse Walk, doors, finite supplies, evasion and natural hiding RNG. Duration ends after the current real movement leg; no teleport, AI/speed edit or safe mode.";
        }
        public PatrolEvidence PerformancePatrol {get;private set;}
        bool patrolRunning;float patrolPreparationStart,patrolWallStart,patrolGameStart,patrolLastGame;
        float meters, gameStart;
        Vector3 previous;
        int maximumThreats, doorsOpened, throws, dodges, hidingEntries, suppliesCollected, lastRecovered, batteriesCollected, candlesIgnited, lastHidingEntry, lastHidingRolls;
        float lastCharge; string lastHidingOutcome = "None";
        public int BatteriesCollected => batteriesCollected;
        public int CandlesIgnited => candlesIgnited;
        float alertUntil, lastThrow = -100, lastDodge = -100, lastHide = -100, lastGameTime;
        MonoBehaviour lastDodgedActor;
        bool expectEscape, passed, disposed, seekingHiding;
        string stage = "startup";
        public NativeCorridorInputRoute(GameSession session, PlayerMotor player, GameShell shell, Action<Key[]> keys, string evidenceDirectory,int performancePatrolSeconds=0,float movementMultiplier=1)
        {
            if(performancePatrolSeconds!=0 && (performancePatrolSeconds<300||performancePatrolSeconds>480||
                Array.IndexOf(Environment.GetCommandLineArgs(),"-v2-corridor-performance")<0))
                throw new ArgumentException("A 300–480 second patrol requires explicit corridor performance mode");
            this.performancePatrolSeconds=performancePatrolSeconds;
            this.session = session; this.player = player; this.shell = shell; this.keys = keys; this.evidenceDirectory = evidenceDirectory;
            if(float.IsNaN(movementMultiplier)||float.IsInfinity(movementMultiplier)||movementMultiplier<1||movementMultiplier>12)
                throw new ArgumentOutOfRangeException(nameof(movementMultiplier));
            if(movementMultiplier>1)
            {
                movement=NativeAuditMovement.Attach(player,movementMultiplier);
                this.keys=held=>{if(Array.IndexOf(held,Key.W)<0)movement.Restore();keys(held);};
                movementLastGame=session.ElapsedPlayTime;
            }
            eyes = player.eyes; capsule = player.GetComponent<CharacterController>();
            monsters = Stalkers().Where(x => x.name.EndsWith("— corridor")).ToArray();
            speeds = monsters.Select(x => new Vector2(x.patrolSpeed, x.chaseSpeed)).ToArray();
            previous = player.transform.position; wallStart = Time.realtimeSinceStartup;
            previousMouse = Mouse.current; mouse = InputSystem.AddDevice<Mouse>();
        }
        void Check()
        {
            float travelled=Vector3.Distance(previous, player.transform.position);
            meters += travelled; previous = player.transform.position;
            if(movement){movement.ObserveStep(travelled,session.ElapsedPlayTime-movementLastGame);movementLastGame=session.ElapsedPlayTime;}
            lastGameTime = session.ElapsedPlayTime - gameStart; lastRecovered = session.RecordsRecovered;
            observedKeyboard = Keyboard.current;
            int activeThreats=session.Corridor.ActiveThreatCount;
            maximumThreats = Mathf.Max(maximumThreats, activeThreats);
            lastCharge = player.FlashlightSystem.Charge; lastHidingEntry = player.HidingEntryId;
            lastHidingRolls = player.HidingRolls; lastHidingOutcome = player.HidingOutcome.ToString();
            Require(player.HidingRandomSample == null, "Native route may not override the production hiding RNG");
            bool terminal = session.Finished && !(expectEscape && session.Escaped);
            if (terminal) SaveEvidence();
            Require(!(terminal), "Run ended at " + stage + " after " + session.ElapsedPlayTime + "s; " + session.DefeatSource + "; at " + player.transform.position);
            Require((Time.realtimeSinceStartup - wallStart) < (590+performancePatrolSeconds), "Wall budget expired at " + stage);
            Require((session.ElapsedPlayTime - gameStart) < (580+performancePatrolSeconds), "Game budget expired at " + stage);
            bool expectedResult=expectEscape && session.Finished && session.Escaped;
            Require(Time.timeScale==(expectedResult?0:1) && Time.captureDeltaTime==0 && player.enabled,
                "Native natural clock/motor changed: scale="+Time.timeScale+" capture="+Time.captureDeltaTime+
                " motor="+player.enabled+" screen="+shell.Screen+" escaped="+session.Escaped);
            if(expectedResult)Require((shell.Screen==GameShell.Page.Result ||
                session.Corridor.Layout.Version>=3 && shell.Screen==GameShell.Page.ChapterTransition) && !session.InputAllowed,
                "Successful real offering must enter its frozen result or school transition page: "+shell.Screen);
            Require(Mouse.current==mouse,"Native route mouse ownership changed");
            Require((player.transform.position.y) >= (-.15f) && (player.transform.position.y) <= (.85f), "Left the walkable floor/low-step range at " + player.transform.position);
            Require((Physics.Raycast(player.transform.position + Vector3.up * .15f, Vector3.down, .95f,
                (1 << 8) | (1 << 9), QueryTriggerInteraction.Ignore)), "No physical support below feet at " + player.transform.position);
            if(patrolRunning)TrackPatrol(activeThreats,travelled);
        }
        void Mark(string text)
        { string entry = session.ElapsedPlayTime.ToString("F2") + "s " + text + " at " + player.transform.position; milestones.Add(entry); Debug.Log("CORRIDOR_INPUT_MILESTONE " + entry); SaveEvidence(); }
        public IEnumerator Run()
        {
            gameStart = session.ElapsedPlayTime;
            if (!player.Crouching) yield return Pulse(Key.C);
            Require(player.Crouching, "Actual C did not set quiet stance");
            yield return DemonstrateLighting();
            Mark("finite light, battery and candle demonstrated through F/E; quiet stance through C");
            yield return TopUpNearbySupply(18);
            for (int i = 0; i < 5; i++)
            {
                var choices = Items().Where(x => x.gameObject.activeSelf && x.kind == Interactable.Kind.CorridorMemory).ToArray();
                Interactable selected = null; Vector3 at = Vector3.zero; float best = float.PositiveInfinity;
                foreach (var item in choices) if (FindApproach(item, out var point, out float length) && length < best) { selected = item; at = point; best = length; }
                Require((selected) != null, "No physically accessible remaining memory");
                stage = selected.name; yield return Walk(at); yield return Interact(selected);
                Require((session.RecordsRecovered) == (i + 1), "Native corridor invariant failed: Is.EqualTo(i + 1)"); Mark("recovered " + selected.name + " through E");
                if (i < 4) yield return TopUpNearbySupply(14);
                if(i==3&&performancePatrolSeconds>0)yield return Patrol();
            }
            // The audible/visible finale replaces the old stealth phase.
            // Keep actual input, original stamina and Lady's real pursuit intact.
            if (session.Corridor.Lady && session.Corridor.Lady.Active)
            {
                if (player.Crouching) yield return Pulse(Key.C);
                alertUntil = float.PositiveInfinity;
                Mark("responded to finale blackout by running directly to the altar");
            }
            var exit = session.Corridor.Layout.Version>=3 ? session.Corridor.AltarChamber.Offering : Items().Single(x => x.name == "Sealed entrance");
            stage = session.Corridor.Layout.Version>=3 ? "return memories to distant classroom altar" : "return to entrance";
            Require((FindApproach(exit, out var exitAt, out _)), "Native corridor invariant failed: Is.True"); yield return Walk(exitAt);
            expectEscape = true; yield return Interact(exit); Require((session.Escaped), "Native corridor invariant failed: Is.True");
            for (int i = 0; i < monsters.Length; i++) Require(new Vector2(monsters[i].patrolSpeed,monsters[i].chaseSpeed)==speeds[i],"Authored corridor threat speed changed");
            Require((maximumThreats) == (4), "Native corridor invariant failed: Is.EqualTo(4)"); Require((meters) > (80), "Native corridor invariant failed: Is.GreaterThan(80)");
            passed = true; Mark("escaped with all five memories through the active layout's actual exit");
        }
        void TrackPatrol(int activeThreats,float travelled)
        {
            float game=session.ElapsedPlayTime;
            if(!PerformancePatrol.started)
            {
                PerformancePatrol.preparationWallSeconds=Time.realtimeSinceStartup-patrolPreparationStart;
                if(activeThreats==4)
                {
                    PerformancePatrol.started=true;PerformancePatrol.activeThreatsAtStart=4;PerformancePatrol.minimumActiveThreats=4;
                    patrolWallStart=Time.realtimeSinceStartup;patrolGameStart=game;
                    PerformancePatrol.observationStartRouteWallSeconds=patrolWallStart-wallStart;
                    PerformancePatrol.observationStartRouteGameSeconds=game-gameStart;
                    Mark("performance patrol observation began with all four real enemies active");
                }
            }
            if(PerformancePatrol.started)
            {
                PerformancePatrol.minimumActiveThreats=Mathf.Min(PerformancePatrol.minimumActiveThreats,activeThreats);
                Require(activeThreats==4,"Late patrol lost its four active production enemies");
                PerformancePatrol.wallSeconds=Time.realtimeSinceStartup-patrolWallStart;
                PerformancePatrol.gameSeconds=game-patrolGameStart;PerformancePatrol.physicalMeters+=travelled;
                if(player.Hidden)PerformancePatrol.hiddenGameSeconds+=Mathf.Max(0,game-patrolLastGame);
            }
            patrolLastGame=game;
        }
        IEnumerator Patrol()
        {
            RestoreMovement();
            string priorStage=stage;stage="optional four-enemy performance patrol";
            PerformancePatrol=new PatrolEvidence{requestedSeconds=performancePatrolSeconds};
            patrolPreparationStart=Time.realtimeSinceStartup;patrolLastGame=session.ElapsedPlayTime;patrolRunning=true;
            try
            {
                // Build a deterministic graph traversal once. Successive cells
                // follow actual layout edges; the normal Walk still plans from
                // current feet and handles physical doors and observed threats.
                var points=PatrolWaypoints();int cursor=0;float nextSupply=GameTime+60;
                Mark("optional performance patrol requested "+performancePatrolSeconds+" seconds after fourth memory");
                Check();
                while(!PerformancePatrol.started||PerformancePatrol.wallSeconds<performancePatrolSeconds)
                {
                    Require(session.RecordsRecovered==4,"Performance patrol must retain exactly four recovered memories");
                    yield return Walk(points[cursor]);cursor=(cursor+1)%points.Length;
                    PerformancePatrol.completedLegs++;Check();
                    if(GameTime>=nextSupply)
                    {
                        nextSupply=GameTime+60;yield return TopUpNearbySupply(14);
                    }
                }
                PerformancePatrol.completed=true;Mark("completed optional four-enemy performance patrol through real movement");
            }
            finally{patrolRunning=false;keys(Array.Empty<Key>());stage=priorStage;}
        }
        Vector3[] PatrolWaypoints()
        {
            var run=session.Corridor;var layout=run.Layout;
            int start=Enumerable.Range(0,CorridorLayout.Count).OrderBy(cell=>Vector3.SqrMagnitude(run.CellPosition(cell)-player.transform.position)).First();
            var sequence=new List<int>(CorridorLayout.Count*2);var seen=new bool[CorridorLayout.Count];
            void Visit(int cell)
            {
                seen[cell]=true;sequence.Add(cell);
                foreach(int next in layout.Neighbors(cell))if(!seen[next]){Visit(next);sequence.Add(cell);}
            }
            Visit(start);
            var supported=new Dictionary<int,Vector3>();
            foreach(int cell in sequence.Distinct())
            {
                var centre=run.CellPosition(cell);
                foreach(var offset in new[]{Vector3.zero,Vector3.right*.55f,Vector3.left*.55f,Vector3.forward*.55f,Vector3.back*.55f})
                {
                    if(!NavMesh.SamplePosition(centre+offset,out var hit,.35f,NavMesh.AllAreas)||Mathf.Abs(hit.position.y)>.15f)continue;
                    var body=hit.position+capsule.center;float half=capsule.height*.5f-capsule.radius;
                    if(Physics.OverlapCapsule(body-Vector3.up*half,body+Vector3.up*half,capsule.radius-.02f,
                        Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore).Any(collider=>
                            !collider.GetComponentInParent<PlayerMotor>()&&!collider.GetComponentInParent<NavMeshAgent>()))continue;
                    supported.Add(cell,hit.position);break;
                }
            }
            Require(supported.Count>=4,"Too few physically supported patrol waypoints");PerformancePatrol.waypointCount=supported.Count;
            return sequence.Where(supported.ContainsKey).Select(cell=>supported[cell]).ToArray();
        }
        IEnumerator TopUpNearbySupply(float maximumDetour)
        {
            var stock = player.Firecrackers;
            if (stock.Count >= 2) yield break;
            Interactable selected = null; Vector3 at = Vector3.zero; float best = maximumDetour;
            foreach (var supply in Items().Where(x => x.gameObject.activeSelf &&
                x.kind.ToString() == "FirecrackerSupply"))
            {
                var drawer=supply.GetComponentInParent<CorridorDrawer>();
                var focus=drawer && !drawer.ExposesPickup ? drawer.GetComponent<Interactable>() : supply;
                if (FindApproach(focus, out var point, out var length) && length < best)
                { selected = supply; at = point; best = length; }
            }
            if (!selected) yield break;
            string previousStage = stage; stage = "collect nearby supply";
            yield return Walk(at);
            var tray=selected.GetComponentInParent<CorridorDrawer>();
            if(tray && !tray.ExposesPickup)
            {
                if(!tray.IsOpen) yield return Interact(tray.GetComponent<Interactable>());
                float deadline=Time.realtimeSinceStartup+3;
                while(!tray.ExposesPickup) { Require(Time.realtimeSinceStartup<deadline,"Real drawer did not expose supply"); yield return null; Check(); }
                Require(FindApproach(selected,out var pickupAt,out _),"Opened drawer supply lacks a real pickup approach");
                yield return Walk(pickupAt); Mark("opened actual desk drawer through E");
            }
            int before = stock.Count; yield return Interact(selected);
            Require(stock.Count == before + SnapperStockRules.ShotsPerPack && !selected.gameObject.activeSelf,
                "Actual snapper supply did not consume its pickup and add one six-shot pack"); suppliesCollected++;
            Mark("collected finite supply through E"); stage = previousStage;
        }
        IEnumerator Pulse(Key key)
        { keys(Array.Empty<Key>()); yield return null; Check(); keys(new[] { key }); yield return null; keys(Array.Empty<Key>()); yield return null; Check(); }
        void Steer(Vector3 point, bool level = false)
        {
            var delta = point - eyes.transform.position;
            float yaw = Mathf.Atan2(delta.x, delta.z) * Mathf.Rad2Deg;
            float pitch = level ? 0 : Mathf.Clamp(-Mathf.Atan2(delta.y, new Vector2(delta.x, delta.z).magnitude) * Mathf.Rad2Deg, -77, 77);
            float sensitivity = player.sensitivity;
            InputSystem.QueueDeltaStateEvent(mouse.delta, new Vector2(Mathf.Clamp(Mathf.DeltaAngle(player.transform.eulerAngles.y, yaw), -40, 40),
                -Mathf.Clamp(Mathf.DeltaAngle(eyes.transform.localEulerAngles.x, pitch), -30, 30)) / sensitivity);
        }
        Key[] EvasionKeys(Vector3 direction)
        {
            // Move immediately with actual backward/strafe keys while the
            // mouse turns. Waiting to face forward can consume a short windup.
            direction.y=0;direction.Normalize();
            float forward=Vector3.Dot(player.transform.forward,direction),right=Vector3.Dot(player.transform.right,direction);
            var held=new List<Key>();
            if(forward>.3f)held.Add(Key.W);else if(forward<-.3f)held.Add(Key.S);
            if(right>.3f)held.Add(Key.D);else if(right<-.3f)held.Add(Key.A);
            if(!player.Crouching&&!player.SprintExhausted)held.Add(Key.LeftShift);
            return held.ToArray();
        }
        bool EvasionPathClear(NavMeshPath route)
        {
            var previous=player.transform.position;
            float half=Mathf.Max(0,capsule.height*.5f-capsule.radius);
            foreach(var corner in route.corners.Skip(1))
            {
                var delta=corner-previous;delta.y=0;
                if(delta.sqrMagnitude>.0001f)
                {
                    var centre=previous+capsule.center+Vector3.up*.06f;
                    foreach(var hit in Physics.CapsuleCastAll(centre-Vector3.up*half,centre+Vector3.up*half,
                        Mathf.Max(.05f,capsule.radius-.015f),delta.normalized,delta.magnitude,
                        Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore))
                        if(!hit.collider.transform.IsChildOf(player.transform))return false;
                }
                previous=corner;previous.y=player.transform.position.y;
            }
            return true;
        }
        bool FindApproach(Interactable item, out Vector3 at, out float length, Vector3? observer = null)
        {
            at = Vector3.zero; length = float.PositiveInfinity;
            foreach (var collider in item.GetComponentsInChildren<Collider>())
            {
                if (!collider.enabled || collider.isTrigger) continue;
                for (int n = 0; n < 64; n++)
                {
                    float angle = n % 32 * Mathf.PI / 16, radius = n < 32 ? 1 : 1.45f;
                    var candidate = collider.bounds.center + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * radius; candidate.y = .05f;
                    if (!NavMesh.SamplePosition(candidate, out var hit, .35f, NavMesh.AllAreas) || Mathf.Abs(hit.position.y) > .15f) continue;
                    if (observer.HasValue && (!Physics.Linecast(observer.Value + Vector3.up * 1.7f, hit.position + Vector3.up * 1.6f,
                        Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) || !Physics.Linecast(observer.Value + Vector3.up * 1.7f,
                        hit.position + Vector3.up, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))) continue;
                    var center = hit.position + capsule.center; float half = capsule.height * .5f - capsule.radius;
                    if (Physics.CheckCapsule(center - Vector3.up * half, center + Vector3.up * half, capsule.radius - .02f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) continue;
                    var origin = hit.position + eyes.transform.position - player.transform.position;
                    var ray = collider.bounds.center - origin;
                    if (ray.magnitude > 2.15f || !Physics.Raycast(origin, ray.normalized, out var sight, 2.2f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) ||
                        sight.collider.GetComponentInParent<Interactable>() != item) continue;
                    var path = new NavMeshPath();
                    if (!NavMesh.CalculatePath(player.transform.position, hit.position, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete) continue;
                    float distance = 0; for (int k = 1; k < path.corners.Length; k++) distance += Vector3.Distance(path.corners[k - 1], path.corners[k]);
                    if (distance < length) { length = distance; at = hit.position; }
                }
            }
            return !float.IsPositiveInfinity(length);
        }
        float GameTime => session.ElapsedPlayTime;
        bool RecognitionCue
        {
            get { var cue = player.GetComponent<DetectionFeedback>(); return cue && cue.Active; }
        }
        MonoBehaviour VisibleThreat()
        {
            MonoBehaviour closest = null; float nearest = 8, urgency = -1;
            foreach (var monster in monsters.Cast<MonoBehaviour>().Concat(new MonoBehaviour[]{session.Corridor.Mask}))
            {
                if (!monster || !monster.isActiveAndEnabled) continue;
                var delta = monster.transform.position + Vector3.up - eyes.transform.position;
                bool heard=session.Corridor.Lighting.HeardMovement(monster);
                if (delta.magnitude >= 8 || !RecognitionCue && !heard && Vector3.Angle(eyes.transform.forward, delta) > 80) continue;
                if (Physics.Linecast(eyes.transform.position, monster.transform.position + Vector3.up, out var hit,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) && !hit.collider.transform.IsChildOf(monster.transform)) continue;
                // A second actor's imminent swing takes priority over a closer
                // actor that is merely recovering from the previous dodge.
                float danger=AttackActive(monster)&&AttackRecovery(monster)<=0?1+AttackWindup(monster):0;
                if(danger<urgency || danger==urgency&&delta.magnitude>=nearest)continue;
                closest = monster; nearest = delta.magnitude;urgency=danger;
            }
            return closest;
        }
        static bool AttackActive(MonoBehaviour actor) => actor is StalkerBrain stalker ? stalker.AttackActive : ((LanternMaskEncounter)actor).AttackActive;
        static float AttackRecovery(MonoBehaviour actor) => actor is StalkerBrain stalker ? stalker.AttackRecovery : ((LanternMaskEncounter)actor).AttackRecovery;
        static float AttackWindup(MonoBehaviour actor) => actor is StalkerBrain stalker ? stalker.AttackWindup : ((LanternMaskEncounter)actor).AttackWindup;
        IEnumerator Decoy(MonoBehaviour monster)
        {
            var stock = player.Firecrackers; int before = stock.Count;
            var away = monster.transform.position - player.transform.position; away.y = 0;
            var aim = monster.transform.position + away.normalized * 5 + Vector3.up * .2f;
            keys(Array.Empty<Key>()); float deadline = Time.realtimeSinceStartup + .6f;
            while (Vector3.Angle(eyes.transform.forward, aim - eyes.transform.position) > 4 && Time.realtimeSinceStartup < deadline)
            { Steer(aim); yield return null; Check(); }
            yield return Pulse(Key.Q); lastThrow = GameTime;
            if (stock.Count < before) { throws++; Mark("threw real Q decoy beyond visible guard"); }
            float wait = Time.realtimeSinceStartup + 1.5f;
            while (Time.realtimeSinceStartup < wait && !RecognitionCue) { keys(Array.Empty<Key>()); yield return null; Check(); }
        }
        MonoBehaviour StationaryThreat()
        {
            // Actual sight, recognition or admitted audible movement can warn
            // about a pursuer behind us. A real windup must not be ignored just
            // because the creature's navigation pivot is outside a fixed radius.
            var guard=VisibleThreat();
            return guard && (AttackActive(guard) || RecognitionCue &&
                Vector3.Distance(player.transform.position,guard.transform.position)<3.2f) &&
                (guard!=lastDodgedActor||GameTime-lastDodge>.9f) ? guard : null;
        }
        IEnumerator Dodge(MonoBehaviour monster, Vector3 destination, bool continuePast = true)
        {
            var observedGuard = monster.transform.position;
            if (player.Crouching) yield return Pulse(Key.C);
            // Face a visible pursuer and react to the real committed swing,
            // rather than blindly crossing it before the warning has resolved.
            keys(Array.Empty<Key>());
            float baitDeadline = GameTime + 1.35f;
            while (VisibleThreat() == monster && !AttackActive(monster) && GameTime < baitDeadline)
            {
                observedGuard = monster.transform.position;
                Steer(observedGuard + Vector3.up, true);
                yield return null; Check();
            }
            bool observedStrike = VisibleThreat() == monster && AttackActive(monster);
            if (observedStrike) observedGuard = monster.transform.position;
            float passNotBefore = GameTime;
            if (observedStrike && AttackRecovery(monster) <= 0)
                passNotBefore += .75f * (1 - AttackWindup(monster));
            var away = player.transform.position - observedGuard; away.y = 0;
            var tangent = Vector3.Cross(Vector3.up, away.normalized);
            var selected = new NavMeshPath(); bool found = false; float best = 0;
            foreach(float distance in new[]{2.2f,1.4f,.85f})
            foreach (var direction in new[] { tangent, -tangent, away.normalized,
                (away.normalized+tangent).normalized,(away.normalized-tangent).normalized })
            {
                var at = player.transform.position + direction * distance;
                if (!NavMesh.SamplePosition(at, out var sample, .4f, NavMesh.AllAreas) || Mathf.Abs(sample.position.y) > .15f) continue;
                var path = new NavMeshPath();
                if (!NavMesh.CalculatePath(player.transform.position, sample.position, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete) continue;
                float length = 0; for (int n = 1; n < path.corners.Length; n++) length += Vector3.Distance(path.corners[n - 1], path.corners[n]);
                float clearance = Vector3.Distance(sample.position, observedGuard);
                if (length > 3 || clearance < best || !EvasionPathClear(path)) continue;
                selected = path; best = clearance; found = true;
            }
            if (!found) yield break;
            alertUntil = GameTime + 6; lastDodge = GameTime;lastDodgedActor=monster;
            var retreatStart=player.transform.position;
            foreach (var corner in selected.corners.Skip(1))
            {
                float deadline = Time.realtimeSinceStartup + 2;
                while (Vector3.Distance(player.transform.position, corner) > .25f && Time.realtimeSinceStartup < deadline)
                {
                    Check(); var delta = corner - player.transform.position; delta.y = 0; Steer(eyes.transform.position + delta, true);
                    keys(EvasionKeys(delta)); yield return null;
                }
            }
            keys(Array.Empty<Key>());
            if(Vector3.Distance(player.transform.position,retreatStart)<.4f)yield break;
            dodges++; Mark(observedStrike ? "evaded an observed committed swing through real movement" : "retreated from observed guard through real movement");
            if (!observedStrike || !continuePast) yield break;
            // Keep the observation's finite warning time; don't query unseen
            // movement/attack state after turning toward the escape route.
            while (GameTime < passNotBefore) { yield return null; Check(); }
            // After sidestepping, continue past the observed obstruction instead
            // of repeatedly retreating deeper into the same dead end.
            var onward = destination - player.transform.position; onward.y = 0;
            var passing = observedGuard + onward.normalized * 2.6f;
            if (NavMesh.SamplePosition(passing, out var landing, .5f, NavMesh.AllAreas))
            {
                var crossing = new NavMeshPath();
                if (NavMesh.CalculatePath(player.transform.position, landing.position, NavMesh.AllAreas, crossing) && crossing.status == NavMeshPathStatus.PathComplete)
                {
                    float length = 0; for (int n = 1; n < crossing.corners.Length; n++) length += Vector3.Distance(crossing.corners[n - 1], crossing.corners[n]);
                    if (length < 7 && EvasionPathClear(crossing)) foreach (var corner in crossing.corners.Skip(1))
                    {
                        float deadline = Time.realtimeSinceStartup + 2;
                        while (Vector3.Distance(player.transform.position, corner) > .25f && Time.realtimeSinceStartup < deadline)
                        {
                            Check();
                            var nextStrike=StationaryThreat();
                            if(nextStrike&&AttackRecovery(nextStrike)<=0)
                            {yield return Dodge(nextStrike,destination,false);yield break;}
                            var delta=corner-player.transform.position;delta.y=0;Steer(eyes.transform.position+delta,true);
                            keys(EvasionKeys(delta));yield return null;
                        }
                    }
                }
            }
            keys(Array.Empty<Key>());
        }
        IEnumerator Walk(Vector3 at)
        {
            RestoreMovement();
            var path = new NavMeshPath(); Require((NavMesh.CalculatePath(player.transform.position, at, NavMesh.AllAreas, path)), "Native corridor invariant failed: Is.True");
            Require((path.status) == (NavMeshPathStatus.PathComplete), "Native corridor invariant failed: Is.EqualTo(NavMeshPathStatus.PathComplete)");
            foreach (var corner in path.corners.Skip(1))
            {
                float progressTime = Time.realtimeSinceStartup, best = Vector3.Distance(player.transform.position, corner);
                while (Vector2.Distance(new Vector2(player.transform.position.x, player.transform.position.z), new Vector2(corner.x, corner.z)) > .18f)
                {
                    Check(); var delta = corner - player.transform.position; delta.y = 0;
                    float distance = delta.magnitude;
                    if (distance < best - .035f) { best = distance; progressTime = Time.realtimeSinceStartup; }
                    Require((Time.realtimeSinceStartup - progressTime) < (12), "Physically stuck toward " + corner + " at " + player.transform.position);
                    var guard = VisibleThreat();
                    if(guard)RestoreMovement();
                    if (guard)
                    {
                        float separation = Vector3.Distance(player.transform.position, guard.transform.position);
                        if (!seekingHiding && separation < 6 && GameTime - lastHide > 20 &&
                            (player.Stamina < .65f || dodges > 1))
                        { int before = hidingEntries; yield return HideFrom(guard); progressTime = Time.realtimeSinceStartup;
                            if (hidingEntries > before) { yield return Walk(at); yield break; } }
                        var stock = player.Firecrackers;
                        if (separation > 5 && GameTime - lastThrow > 10 && stock.Count > 0 && !RecognitionCue)
                        { yield return Decoy(guard); progressTime = Time.realtimeSinceStartup; continue; }
                        if ((separation < 4.5f || RecognitionCue) && player.Crouching)
                        { yield return Pulse(Key.C); alertUntil = GameTime + 6; progressTime = Time.realtimeSinceStartup; }
                        if ((separation < 3.2f || AttackActive(guard)) && Vector3.Dot(delta.normalized, (guard.transform.position - player.transform.position).normalized) > .15f && (guard!=lastDodgedActor||GameTime-lastDodge>.9f))
                        {
                            int before = dodges; yield return Dodge(guard, corner); progressTime = Time.realtimeSinceStartup;
                            // Evasion can carry us past the old corner. Re-plan
                            // from actual feet instead of walking back into it.
                            if (dodges > before) { yield return Walk(at); yield break; }
                            continue;
                        }
                    }
                    if (!seekingHiding && !player.Crouching && GameTime > alertUntil && !RecognitionCue)
                        yield return Pulse(Key.C);
                    var blockingDoor = DoorOnRoute(delta, distance);
                    if (blockingDoor)
                    {
                        RestoreMovement();
                        int before=dodges;
                        yield return OpenPhysicalDoor(blockingDoor,at);
                        progressTime = Time.realtimeSinceStartup;
                        if(dodges>before) { yield return Walk(at); yield break; }
                        continue; // recompute actual feet/direction after a moving leaf
                    }
                    Steer(eyes.transform.position + delta, true);
                    if(movement)movement.Apply(CanAccelerate(delta,distance,guard));
                    keys(Vector3.Angle(player.transform.forward, delta) > 12 ? Array.Empty<Key>() : !player.Crouching && !player.SprintExhausted ? new[] { Key.W, Key.LeftShift } : new[] { Key.W }); yield return null;
                }
            }
            keys(Array.Empty<Key>()); yield return null; Check();
        }
        bool CanAccelerate(Vector3 direction,float distance,MonoBehaviour visibleGuard)
        {
            float brakingDistance=Mathf.Max(2.75f,movement.Data.originalRunSpeed*movement.Data.requestedMultiplier*Time.unscaledDeltaTime*3+.75f);
            if(patrolRunning||visibleGuard||RecognitionCue||player.Hidden||GameTime<alertUntil||distance<brakingDistance+.25f||
                Vector3.Angle(player.transform.forward,direction)>4)return false;
            // Conservative proximity gating changes speed only, never selects a
            // route using hidden actors. All evasion still uses observed cues.
            foreach(var actor in monsters)if(actor&&actor.gameObject.activeInHierarchy&&Vector3.Distance(actor.transform.position,player.transform.position)<12)return false;
            var mask=session.Corridor.Mask;
            if(mask&&mask.gameObject.activeInHierarchy&&Vector3.Distance(mask.transform.position,player.transform.position)<12)return false;
            var body=player.transform.position+capsule.center+Vector3.up*.08f;float half=Mathf.Max(0,capsule.height*.5f-capsule.radius);
            return !Physics.CapsuleCast(body-Vector3.up*half,body+Vector3.up*half,capsule.radius+.02f,direction.normalized,out _,
                Mathf.Min(distance,brakingDistance),Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore);
        }
        public void RestoreMovement(){if(movement)movement.Restore();}
        Interactable DoorOnRoute(Vector3 direction, float distance)
        {
            float radius = capsule.radius + .02f;
            var center = player.transform.position + capsule.center + Vector3.up * .08f;
            float half = Mathf.Max(0, capsule.height * .5f - capsule.radius);
            if (!Physics.CapsuleCast(center - Vector3.up * half, center + Vector3.up * half, radius,
                direction.normalized, out var hit, Mathf.Min(2.05f, distance + .1f),
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) return null;
            var door = hit.collider.GetComponentInParent<Interactable>();
            if(!door || door.kind!=Interactable.Kind.Door) return null;
            // Body sweeps reach farther than the real 2.2m eye interaction ray.
            // Continue honest locomotion until the closed leaf can actually be
            // focused; stopping at capsule reach strands E outside its range.
            if(!door.IsOpen && Vector3.Distance(eyes.transform.position,
                hit.collider.ClosestPoint(eyes.transform.position))>2.15f) return null;
            return door;
        }
        IEnumerator OpenPhysicalDoor(Interactable door, Vector3 destination)
        {
            keys(Array.Empty<Key>()); float deadline = Time.realtimeSinceStartup + 5;
            while (!door.IsOpen || !door.AtRequestedDoorPose)
            {
                Check(); Require(Time.realtimeSinceStartup < deadline, "Actual door never cleared: " + door.name);
                var guard=StationaryThreat();
                if(guard)
                {
                    int before=dodges;
                    yield return Dodge(guard,destination,false);
                    if(dodges>before) Mark("interrupted physical door wait for an observed threat; replan from actual feet");
                    yield break;
                }
                if (!door.IsOpen)
                {
                    int before=dodges;
                    yield return Interact(door);
                    if (door.IsOpen) { doorsOpened++; Mark("opened/reopened physical door through E"); }
                    if(dodges>before) yield break;
                }
                else yield return null;
            }
        }
        public IEnumerator DemonstrateLighting()
        {
            string previousStage = stage; stage = "finite light and route marker";
            var lamp = player.FlashlightSystem;
            if (!player.flashlight.enabled) yield return Pulse(Key.F);
            Require(player.flashlight.enabled, "Actual F could not light the charged flashlight");
            float beforeDrain = lamp.Charge, waitUntil = GameTime + .3f;
            while (GameTime < waitUntil) { keys(Array.Empty<Key>()); yield return null; Check(); }
            Require(lamp.Charge < beforeDrain, "The lit flashlight did not consume real gameplay charge");
            // Current maps start in the cavern before cell zero. Select the
            // nearest reachable supply by actual path, including that approach.
            Interactable battery = null; Vector3 batteryAt = Vector3.zero; float best = float.PositiveInfinity;
            foreach (var item in Items().Where(x => x.gameObject.activeSelf && x.kind == Interactable.Kind.FlashlightBattery))
                if (FindApproach(item, out var at, out var length) && length < best) { battery = item; batteryAt = at; best = length; }
            Require(battery, "No finite early battery has a physical input route");
            yield return Walk(batteryAt); float beforeRefill = lamp.Charge; int packs = lamp.PacksCollected;
            yield return Interact(battery);
            Require(!battery.gameObject.activeSelf && lamp.PacksCollected == packs + 1 && lamp.Charge > beforeRefill,
                "Actual E did not consume one finite battery and refill the lamp");
            batteriesCollected++; Mark("picked up finite battery through E after actual lit drain");
            Interactable marker = null; Vector3 markerAt = Vector3.zero; best = float.PositiveInfinity;
            foreach (var item in Items().Where(x => x.gameObject.activeSelf && x.kind == Interactable.Kind.Candle))
                if (FindApproach(item, out var at, out var length) && length < best) { marker = item; markerAt = at; best = length; }
            Require(marker, "No nearby candle has a physical input route");
            yield return Walk(markerAt); var candle = marker.GetComponent<WaymarkCandle>(); int ignitions = candle.Ignitions;
            yield return Interact(marker);
            Require(candle.Lit && candle.LocalLight.enabled && candle.Ignitions == ignitions + 1,
                "Actual E did not light the route candle exactly once");
            candlesIgnited++; Mark("lit a route candle through E");
            // A durably lit candle deliberately leaves the focus list. Revisit it
            // through the same real ray/input while proving E cannot relight it.
            float aimDeadline=Time.realtimeSinceStartup+1.5f;bool aimed=false;
            while(!aimed)
            {
                Require(Time.realtimeSinceStartup<aimDeadline,"Could not physically aim at the lit candle");
                Steer(marker.GetComponent<Collider>().bounds.center);yield return null;Check();
                aimed=Physics.Raycast(eyes.transform.position,eyes.transform.forward,out var hit,2.2f,
                    Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore)&&hit.collider.GetComponentInParent<Interactable>()==marker;
            }
            Require(!marker.CanFocus&&player.Focus!=marker,"Already-lit candle remained an actionable focus target");
            yield return Pulse(Key.E);
            Require(candle.HasBeenLit&&candle.Ignitions == ignitions + 1, "Revisiting the lit candle repeated ignition");
            if (player.flashlight.enabled) yield return Pulse(Key.F);
            Require(!player.flashlight.enabled, "Actual F did not extinguish the flashlight");
            float offCharge = lamp.Charge; waitUntil = GameTime + .3f;
            while (GameTime < waitUntil) { keys(Array.Empty<Key>()); yield return null; Check(); }
            Require(lamp.Charge == offCharge, "An extinguished flashlight consumed charge");
            stage = previousStage;
        }

        IEnumerator Interact(Interactable item)
        {
            keys(Array.Empty<Key>()); float deadline = Time.realtimeSinceStartup + 4, retryAt = Time.realtimeSinceStartup + .8f;
            bool reapproached = false;
            while (player.Focus != item)
            {
                Check(); Require((Time.realtimeSinceStartup) < (deadline), "Could not focus " + item.name);
                if (!reapproached && Time.realtimeSinceStartup > retryAt && item.kind!=Interactable.Kind.Door &&
                    FindApproach(item,out var retry,out _))
                {
                    reapproached=true;yield return Walk(retry);keys(Array.Empty<Key>());yield return null;Check();
                    deadline=Time.realtimeSinceStartup+4;continue;
                }
                var guard=StationaryThreat();
                if(guard)
                {
                    int before=dodges;
                    yield return Dodge(guard,item.transform.position,false);
                    if(dodges>before)
                    {
                        Mark("interrupted item focus for an observed threat; approach again through actual input");
                        Require(FindApproach(item,out var at,out _),"No physical re-approach after observed threat: "+item.name);
                        yield return Walk(at);
                        // This is a new focus attempt after actual locomotion;
                        // whole route gameplay/wall budgets continue unchanged.
                        deadline=Time.realtimeSinceStartup+4;
                    }
                    continue;
                }
                var collider = item.GetComponentsInChildren<Collider>().Where(x => x.enabled && !x.isTrigger)
                    .OrderBy(x => Vector3.Distance(x.ClosestPoint(eyes.transform.position), eyes.transform.position)).First();
                Steer(Vector3.Lerp(collider.ClosestPoint(eyes.transform.position), collider.bounds.center, .08f)); yield return null;
            }
            yield return Pulse(Key.E);
        }
        IEnumerator HideFrom(MonoBehaviour guard)
        {
            Interactable cabinet = null; Vector3 destination = Vector3.zero; float best = 30;
            foreach (var item in Items().Where(x => x.name == "Corridor hiding cabinet"))
            {
                // The cabinet's actual entrance must be beyond architectural
                // cover, not merely hidden by the cabinet mesh at a sampled point.
                var entrance = item.outside.position;
                if (Vector3.Distance(entrance, guard.transform.position) < 6) continue;
                if (!Physics.Linecast(guard.transform.position + Vector3.up * 1.7f, entrance + Vector3.up * 1.6f,
                    out var cover, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) ||
                    cover.collider.transform.IsChildOf(item.transform) ||
                    cover.collider.GetComponentInParent<StalkerBrain>()) continue;
                if (!FindApproach(item, out var at, out var length, guard.transform.position) || length >= best) continue;
                // Choose known cover beyond a corner relative to the last observed
                // guard. No hidden actor position is used to select a hiding place.
                bool highCovered = Physics.Linecast(guard.transform.position + Vector3.up * 1.7f, at + Vector3.up * 1.6f,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                bool torsoCovered = Physics.Linecast(guard.transform.position + Vector3.up * 1.7f, at + Vector3.up,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                if (!highCovered || !torsoCovered) continue;
                cabinet = item; destination = at; best = length;
            }
            lastHide = GameTime;
            if (!cabinet) yield break;
            seekingHiding = true;
            try
            {
                if (player.Crouching) yield return Pulse(Key.C);
                alertUntil = GameTime + 8; yield return Walk(destination); yield return Interact(cabinet);
                Require((player.Hidden), "Native corridor invariant failed: Is.True"); hidingEntries++; Mark("entered actual cabinet through E after cover route");
                keys(Array.Empty<Key>()); float deadline = GameTime + 14;
                while (GameTime < deadline && !player.HidingThreatCueActive) { yield return null; Check(); }
                // A passing guard can occupy the real exit capsule. Honour the
                // blocked-exit response instead of assuming E bypasses actors.
                float exitDeadline = GameTime + 10;
                do
                {
                    yield return Pulse(Key.E);
                    if (!player.Hidden) break;
                    float retry = GameTime + .5f;
                    while (GameTime < retry) { yield return null; Check(); }
                } while (GameTime < exitDeadline);
                Require(!(player.Hidden), "Actual cabinet exit stayed physically blocked for ten seconds: " + DescribeExitBlockers(cabinet));
                Mark("left actual cabinet through E"); alertUntil = GameTime + 1.5f;
            }
            finally { seekingHiding = false; }
        }
        string DescribeExitBlockers(Interactable cabinet)
        {
            var exit = cabinet.outside.position;
            var center = exit + player.transform.TransformVector(capsule.center);
            float half = Mathf.Max(0, capsule.height * .5f - capsule.radius);
            return string.Join(", ", Physics.OverlapCapsule(center - player.transform.up * half, center + player.transform.up * half,
                capsule.radius - .02f, ~0, QueryTriggerInteraction.Ignore)
                .Where(x => !x.transform.IsChildOf(player.transform)).Select(x => x.name + " at " + x.transform.position));
        }
        [Serializable] sealed class Evidence
        {
            public bool passed; public string stage, hidingOutcome, hidingRng = "production Unity RNG; no forced sample or seed";
            public float gameSeconds, physicalMeters, flashlightCharge;
            public int maximumThreats, doorsOpened, throws, dodges, hidingEntries, suppliesCollected, recovered,
                batteriesCollected, candlesIgnited, hidingEntryId, hidingRolls;
            public string[] milestones;
            public PatrolEvidence performancePatrol;
            public NativeAuditMovement.Evidence movement;
        }
        void SaveEvidence()
        {
            System.IO.Directory.CreateDirectory(evidenceDirectory);
            System.IO.File.WriteAllText(System.IO.Path.Combine(evidenceDirectory,"native-corridor-route.json"), JsonUtility.ToJson(new Evidence { passed = passed, stage = stage,
                gameSeconds = lastGameTime, physicalMeters = meters, maximumThreats = maximumThreats,
                doorsOpened = doorsOpened, throws = throws, dodges = dodges, hidingEntries = hidingEntries,
                suppliesCollected = suppliesCollected, recovered = lastRecovered, batteriesCollected = batteriesCollected,
                candlesIgnited = candlesIgnited, flashlightCharge = lastCharge, hidingEntryId = lastHidingEntry,
                hidingRolls = lastHidingRolls, hidingOutcome = lastHidingOutcome, milestones = milestones.ToArray(),performancePatrol=PerformancePatrol,movement=movement?movement.Data:null }, true));
        }

        Interactable[] Items() => UnityEngine.Object.FindObjectsByType<Interactable>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Where(item => item.gameObject.scene == session.gameObject.scene).ToArray();
        StalkerBrain[] Stalkers() => UnityEngine.Object.FindObjectsByType<StalkerBrain>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Where(actor => actor.gameObject.scene == session.gameObject.scene).ToArray();
        void Require(bool condition, string reason)
        { if (!condition) throw new InvalidOperationException(reason + "; stage=" + stage + "; records=" + session.RecordsRecovered + "; at=" + player.transform.position); }
        public void Dispose()
        {
            if (disposed) return; disposed = true;
            RestoreMovement();
            SaveEvidence();
            if (observedKeyboard != null && observedKeyboard.added) InputSystem.QueueStateEvent(observedKeyboard, new KeyboardState());
            if (mouse.added) InputSystem.RemoveDevice(mouse); if (previousMouse != null && previousMouse.added) previousMouse.MakeCurrent();
        }
    }
}
