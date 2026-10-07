using System.Collections;
using UnityEngine;
using UnityEngine.AI;

namespace HappyToy.V2
{
    // The first reveal walks out of the authored Annex junction. Placement happens
    // only while inactive and behind real geometry; there is no visible spawn fallback.
    public sealed class V1CyclopseIntro : MonoBehaviour
    {
        public const float EmergenceSpeed = .85f;
        public const float StagingWaitLimit = 8;
        public const float MinimumStagingDistance = 5;
        public const float CrossingSpeed = 1.3f;
        // School first-memory shot: glance at the player and keep walking across
        // the far junction. The original story reveal retains its authored route.
        public bool CrossCorridorOnly;
        public bool LookAtPlayerCompleted { get; private set; }
        public bool SideTurnCompleted { get; private set; }
        public bool PassCompleted { get; private set; }
        const float FloorY = 0, FloorDrift = .4f, ArrivalDistance = .2f;
        static readonly Vector3 AuthoredCorner = new Vector3(13.8f, 0, 0);
        static readonly Vector3 AuthoredReveal = new Vector3(10.6f, 0, 0);
        public bool Completed { get; private set; }
        public bool RoarPlayed { get; private set; }
        public string Phase { get; private set; } = "idle";
        public string FailureReason { get; private set; } = "";
        public int ActivationCount { get; private set; }
        public int RoarCount { get; private set; }
        public int RouteLegsCompleted { get; private set; }
        public int StagingDeferrals { get; private set; }
        public Vector3 DeferredPlayerPosition { get; private set; }
        public Vector3 SelectedStagingPosition { get; private set; }
        public Vector3 SelectedCornerPosition { get; private set; }
        public Vector3 SelectedRevealPosition { get; private set; }
        public Vector3 SelectedExitPosition { get; private set; }
        public float EmergenceDistance { get; private set; }
        public float EmergenceElapsed { get; private set; }
        public bool StagingWasOccluded { get; private set; }
        public int BreathCues { get; private set; }
        public float AnticipationElapsed { get; private set; }
        EncounterRevealAudio anticipation;
        AudioClip roar;
        AudioSource voice;
        GameObject voiceEmitter;
        GameSession session;
        StalkerBrain actor;
        NavMeshAgent agent;
        float previousSpeed, previousStoppingDistance;
        bool previousUpdateRotation;
        bool settingsOwned;

        bool SessionValid => Phase != "resolved" && isActiveAndEnabled && session && GameSession.Current == session &&
            !session.Finished && session.StoryStep < 4 && session.player && actor;
        static float HorizontalDistance(Vector3 a, Vector3 b)
        { a.y = b.y = 0; return Vector3.Distance(a, b); }
        static bool OnIntroFloor(Vector3 point) => EnemyNavigation.SameFloor(point, FloorY) &&
            Mathf.Abs(point.y - FloorY) <= FloorDrift;

        bool Sample(Vector3 point, NavMeshQueryFilter filter, out Vector3 sampled)
        {
            sampled = point;
            if (!NavMesh.SamplePosition(point, out var hit, .4f, filter) || !OnIntroFloor(hit.position)) return false;
            sampled = hit.position;
            return true;
        }
        static bool CompleteFloorPath(Vector3 origin, Vector3 target, NavMeshQueryFilter filter, NavMeshPath path)
        {
            if (!NavMesh.CalculatePath(origin, target, filter, path) || path.status != NavMeshPathStatus.PathComplete)
                return false;
            var corners = path.corners;
            if (corners.Length < 2 || HorizontalDistance(corners[corners.Length - 1], target) > ArrivalDistance) return false;
            float length = 0;
            var previous = origin;
            foreach (var corner in corners)
            {
                if (!OnIntroFloor(corner)) return false;
                length += Vector3.Distance(previous, corner); previous = corner;
            }
            return length > 2 && length <= 6;
        }
        bool OccludedFrom(Vector3 eye, Vector3 staging)
        {
            // Test a conservative body envelope, not just a foot/centre ray or the
            // camera's current facing. Looking away is not safe spawn occlusion.
            for (int x = -1; x <= 1; x++)
                for (int z = -1; z <= 1; z++)
                    for (int y = 0; y < 3; y++)
                    {
                        var target = staging + new Vector3(x * 1.1f, .15f + y * 1.15f, z * 1.1f);
                        if (!Physics.Linecast(eye, target, out var hit, Physics.DefaultRaycastLayers,
                            QueryTriggerInteraction.Ignore) || hit.collider.GetComponentInParent<PlayerMotor>() ||
                            hit.collider.GetComponentInParent<StalkerBrain>()) return false;
                    }
            return true;
        }
        bool TryStaging()
        {
            var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
            if (!Sample(AuthoredCorner, filter, out var corner) ||
                !Sample(CrossCorridorOnly ? AuthoredCorner : AuthoredReveal, filter, out var reveal)) return false;
            var player = session.player;
            var eye = player.eyes ? player.eyes.transform.position : player.transform.position + Vector3.up * 1.6f;
            var path = new NavMeshPath();
            if (!CrossCorridorOnly && !CompleteFloorPath(corner, reveal, filter, path)) return false;
            // Prefer the branch farther from the player, but validate both branches.
            float side = player.transform.position.z > 0 ? -1 : 1;
            for (int i = 0; i < 2; i++)
            {
                var candidate = new Vector3(13.8f, FloorY, side * (i == 0 ? 3.2f : -3.2f));
                if (!Sample(candidate, filter, out var staging) ||
                    HorizontalDistance(staging, player.transform.position) < MinimumStagingDistance ||
                    !CompleteFloorPath(staging, corner, filter, path) || !OccludedFrom(eye, staging) ||
                    !OccludedFrom(new Vector3(0, 1.6f, 0), staging)) continue;
                var exit = reveal;
                if (CrossCorridorOnly && (!Sample(new Vector3(13.8f, FloorY, -staging.z), filter, out exit) ||
                    !CompleteFloorPath(corner, exit, filter, path) ||
                    HorizontalDistance(corner,player.transform.position)<MinimumStagingDistance ||
                    HorizontalDistance(exit,player.transform.position)<MinimumStagingDistance)) continue;
                SelectedStagingPosition = staging; SelectedCornerPosition = corner; SelectedRevealPosition = reveal;
                SelectedExitPosition = exit;
                StagingWasOccluded = true;
                return true;
            }
            return false;
        }

        public IEnumerator Play(StalkerBrain brain)
        {
            if (Phase != "idle") yield break;
            session = GameSession.Current; actor = brain;
            if (!SessionValid) { Cancel(); yield break; }
            agent = brain.GetComponent<NavMeshAgent>();
            if (!agent || !agent.enabled)
            { FailureReason = "No enabled NavMesh agent"; Cancel(); yield break; }
            // The director owns one inactive actor. Never relocate an already visible one.
            if (brain.gameObject.activeSelf)
            { FailureReason = "Actor was already active"; Phase = "blocked"; yield break; }
            Phase = "staging";
            float stagingWait = 0, retry = 0;
            while (true)
            {
                if (!SessionValid) { Cancel(); yield break; }
                if (!session.InputAllowed) { yield return null; continue; }
                if (!agent || !agent.enabled)
                { FailureReason = "Staging agent became unavailable"; Cancel(); yield break; }
                retry -= Time.deltaTime;
                if (retry <= 0)
                {
                    retry = .2f;
                    if (TryStaging()) break;
                }
                stagingWait += Time.deltaTime;
                if (stagingWait >= StagingWaitLimit)
                {
                    // Camping this junction must not delete the encounter. Wait
                    // cheaply for a meaningful change before opening a fresh,
                    // bounded staging window; never retry visible placement.
                    Phase = "pending"; StagingDeferrals++;
                    DeferredPlayerPosition = session.player.transform.position;
                    float pendingPoll = .5f;
                    while (true)
                    {
                        if (!SessionValid) { Cancel(); yield break; }
                        if (session.InputAllowed)
                        {
                            pendingPoll -= Time.deltaTime;
                            if (pendingPoll <= 0)
                            {
                                pendingPoll = .5f;
                                if (Vector3.Distance(session.player.transform.position, DeferredPlayerPosition) >= 1)
                                { Phase = "staging"; stagingWait = 0; retry = 0; break; }
                            }
                        }
                        yield return null;
                    }
                }
                yield return null;
            }
            if (brain.gameObject.activeSelf)
            { FailureReason = "Actor became active while staging"; Phase = "blocked"; yield break; }
            // No yield between the final visibility check and activation. The brain
            // cannot take an Update or choose a patrol path before the intro owns it.
            brain.enabled = false; brain.state = StalkerBrain.State.Patrol;
            brain.transform.position = SelectedStagingPosition;
            var approach = SelectedCornerPosition - SelectedStagingPosition; approach.y = 0;
            brain.transform.rotation = Quaternion.LookRotation(approach);
            brain.gameObject.SetActive(true); ActivationCount++;
            if (!EnemyNavigation.Ready(agent))
            { ReleaseBlocked("Staged agent did not bind to NavMesh"); yield break; }
            previousSpeed = agent.speed; previousStoppingDistance = agent.stoppingDistance;
            previousUpdateRotation=agent.updateRotation;settingsOwned = true;
            agent.speed = CrossCorridorOnly ? CrossingSpeed : EmergenceSpeed; agent.stoppingDistance = .08f;
            EnemyNavigation.Stop(agent, true);
            Phase = "anticipation";
            anticipation = EncounterRevealAudio.Ensure(transform);
            anticipation.Play(EncounterRevealAudio.Cue.CyclopseBreath, brain.transform.position + Vector3.up * 1.1f,
                "[모퉁이 뒤 · 거친 숨과 옷 스침]"); BreathCues++;
            while (AnticipationElapsed < 1.05f)
            {
                if (!SessionValid || !brain.gameObject.activeInHierarchy) { Cancel(); yield break; }
                if (!EnemyNavigation.Ready(agent)) { ReleaseBlocked("Anticipation agent lost NavMesh"); yield break; }
                EnemyNavigation.Stop(agent, true);
                if (session.InputAllowed) AnticipationElapsed += Time.deltaTime;
                yield return null;
            }
            anticipation.Stop();
            Phase = "emerge";
            foreach (var destination in CrossCorridorOnly ? new[] { SelectedCornerPosition } :
                new[] { SelectedCornerPosition, SelectedRevealPosition })
            {
                yield return WalkTo(destination);
                if(Phase=="blocked" || Phase=="resolved")yield break;
            }
            Phase = "roar"; EnemyNavigation.Stop(agent, true);
            var facing = session.player.transform.position - brain.transform.position; facing.y = 0;
            if(CrossCorridorOnly)
            {
                agent.updateRotation=false;
                yield return TurnTo(facing,.4f);
                if(Phase=="blocked" || Phase=="resolved")yield break;
            }
            else if (facing.sqrMagnitude > .01f) brain.transform.rotation = Quaternion.LookRotation(facing);
            // The actor already has its movement filter. Configure a fresh inactive
            // emitter before activation rather than auto-playing an empty root source.
            voiceEmitter = new GameObject("Cyclopse intro voice"); voiceEmitter.SetActive(false);
            voiceEmitter.transform.SetParent(brain.transform, false);
            voice = voiceEmitter.AddComponent<AudioSource>(); voice.playOnAwake = false;
            voice.spatialBlend = 1; voice.minDistance = 2;
            voice.maxDistance = CrossCorridorOnly ? 26 : 18; voice.volume = .6f;
            var acoustics = EnemyAcoustics.Bind(voice, brain.transform, .6f);
            // V1's two descending sawtooth voices, bandpass at 245 Hz (Q .85).
            const int rate=24000;var samples=new float[(int)(rate*.78f)];
            float omega=2*Mathf.PI*245/rate,alpha=Mathf.Sin(omega)/(2*.85f),a0=1+alpha;
            float b0=alpha/a0,b2=-alpha/a0,a1=-2*Mathf.Cos(omega)/a0,a2=(1-alpha)/a0;
            double phaseA=0,phaseB=0;float x1=0,x2=0,y1=0,y2=0;
            for(int i=0;i<samples.Length;i++)
            {
                float t=i/(float)rate;phaseA+=136*Mathf.Pow(48f/136,Mathf.Min(t/.72f,1))/rate;phaseB+=143*Mathf.Pow(48f/143,Mathf.Min(t/.72f,1))/rate;
                float x=(.23f*(2*(float)(phaseA%1)-1)+.15f*(2*(float)(phaseB%1)-1))*Mathf.Exp(-10*t);
                float y=b0*x+b2*x2-a1*y1-a2*y2;samples[i]=y;x2=x1;x1=x;y2=y1;y1=y;
            }
            roar=AudioClip.Create("V1 Cyclopse descending roar",samples.Length,1,rate,false);roar.SetData(samples,0);
            voiceEmitter.SetActive(true);voice.PlayOneShot(roar);RoarPlayed=true;RoarCount++;
            if(session.player.eyes&&session.Shell&&
                Vector3.Distance(session.player.eyes.transform.position,voice.transform.position)<=voice.maxDistance&&acoustics.IsAudible(voice))
                session.Shell.ShowCaption("[복도 끝 · 낮게 가라앉는 포효]", 2.6f, 1);
            for (float elapsed = 0; elapsed < (CrossCorridorOnly ? .75f : 1.3f) || (session && !session.InputAllowed);)
            {
                if (!SessionValid || !brain.gameObject.activeInHierarchy)
                { Cancel(); yield break; }
                if (!EnemyNavigation.Ready(agent))
                { ReleaseBlocked("Roaring agent lost NavMesh"); yield break; }
                if (session.InputAllowed) elapsed += Time.deltaTime;
                yield return null;
            }
            if (!SessionValid || !brain.gameObject.activeInHierarchy || !EnemyNavigation.Ready(agent))
            { Cancel(); yield break; }
            if(CrossCorridorOnly)
            {
                LookAtPlayerCompleted=true;Phase="turnAway";
                yield return TurnTo(SelectedExitPosition-brain.transform.position,.45f);
                if(Phase=="blocked" || Phase=="resolved")yield break;
                SideTurnCompleted=true;agent.updateRotation=previousUpdateRotation;Phase="pass";
                yield return WalkTo(SelectedExitPosition);
                if(Phase=="blocked" || Phase=="resolved")yield break;
                PassCompleted=true;
            }
            RestoreAgentSettings(); agent.isStopped = CrossCorridorOnly; brain.enabled = !CrossCorridorOnly;
            Phase = "done"; Completed = true; ReleaseVoice();
        }
        IEnumerator TurnTo(Vector3 direction,float duration)
        {
            direction.y=0;if(direction.sqrMagnitude<.01f)yield break;
            var from=actor.transform.rotation;var to=Quaternion.LookRotation(direction);
            for(float elapsed=0;elapsed<duration;)
            {
                if(!SessionValid || !actor.gameObject.activeInHierarchy){Cancel();yield break;}
                if(!EnemyNavigation.Ready(agent)){ReleaseBlocked("Turning agent lost NavMesh");yield break;}
                EnemyNavigation.Stop(agent,true);
                if(session.InputAllowed)
                {
                    elapsed+=Time.deltaTime;
                    actor.transform.rotation=Quaternion.Slerp(from,to,Mathf.SmoothStep(0,1,Mathf.Clamp01(elapsed/duration)));
                }
                yield return null;
            }
        }
        IEnumerator WalkTo(Vector3 destination)
        {
            var path=new NavMeshPath();
            if(!EnemyNavigation.TryRoute(agent,destination,FloorY,path,6) ||
                !ValidLivePath(path,destination) || !agent.SetPath(path))
            {ReleaseBlocked("Emergence route became unavailable");yield break;}
            agent.isStopped=false;float elapsed=0,stalled=0;
            var previous=actor.transform.position;var progressAnchor=previous;
            while(true)
            {
                if(!SessionValid || !actor.gameObject.activeInHierarchy){Cancel();yield break;}
                if(!EnemyNavigation.Ready(agent) || !OnIntroFloor(actor.transform.position))
                {ReleaseBlocked("Emergence agent left its same-floor route");yield break;}
                if(!session.InputAllowed)
                {EnemyNavigation.Stop(agent);previous=actor.transform.position;yield return null;continue;}
                agent.isStopped=false;
                EmergenceDistance+=HorizontalDistance(previous,actor.transform.position);previous=actor.transform.position;
                elapsed+=Time.deltaTime;EmergenceElapsed+=Time.deltaTime;stalled+=Time.deltaTime;
                if(HorizontalDistance(progressAnchor,actor.transform.position)>=.025f)
                {stalled=0;progressAnchor=actor.transform.position;}
                if(!agent.pathPending && HorizontalDistance(actor.transform.position,destination)<=ArrivalDistance &&
                    agent.remainingDistance<=ArrivalDistance){RouteLegsCompleted++;yield break;}
                if((!agent.pathPending && (!agent.hasPath || agent.pathStatus!=NavMeshPathStatus.PathComplete)) ||
                    elapsed>=11 || stalled>=3)
                {ReleaseBlocked("Emergence route blocked or timed out");yield break;}
                yield return null;
            }
        }
        static bool ValidLivePath(NavMeshPath path, Vector3 destination)
        {
            var corners = path.corners;
            if (corners.Length == 0 || HorizontalDistance(corners[corners.Length - 1], destination) > ArrivalDistance) return false;
            foreach (var corner in corners) if (!OnIntroFloor(corner)) return false;
            return true;
        }
        void RestoreAgentSettings()
        {
            if (!settingsOwned) return;
            if (agent) { agent.speed = previousSpeed; agent.stoppingDistance = previousStoppingDistance;
                agent.updateRotation=previousUpdateRotation; }
            settingsOwned = false;
        }
        void ReleaseBlocked(string reason)
        {
            FailureReason = reason; ReleaseVoice(); if (anticipation) anticipation.Stop();
            if (!SessionValid || !actor.gameObject.activeInHierarchy) { Cancel(); return; }
            // A newly blocked door/agent must not cause a visible teleport or a
            // false arrival/roar. Let ordinary AI recover from this exact position.
            EnemyNavigation.Stop(agent, true); RestoreAgentSettings();
            actor.enabled = !CrossCorridorOnly;
            if(CrossCorridorOnly)actor.gameObject.SetActive(false);
            Phase = "blocked";
        }
        // The iterator is driven by StoryDirector. Stopping its parent coroutine
        // never reaches its guards, so cancellation explicitly settles owned state.
        public void Cancel()
        {
            Phase = "resolved";
            ReleaseVoice(); if (anticipation) anticipation.Stop();
            EnemyNavigation.Stop(agent, true); RestoreAgentSettings();
            if (actor) { actor.enabled = false; actor.gameObject.SetActive(false); }
        }
        void ReleaseVoice()
        {
            if (voice) voice.Stop();
            // Stop/deactivate immediately; deferred destruction must not leave a
            // cancelled cue or its source/filter ticking through the rest of a frame.
            if (voiceEmitter) { voiceEmitter.SetActive(false); Destroy(voiceEmitter); }
            voice = null; voiceEmitter = null;
            if (roar) Destroy(roar);
            roar = null;
        }
        void OnDisable() { Cancel(); }
        void OnDestroy() { Cancel(); }
    }
}
