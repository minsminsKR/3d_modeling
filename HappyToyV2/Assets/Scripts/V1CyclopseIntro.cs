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
        public float EmergenceDistance { get; private set; }
        public float EmergenceElapsed { get; private set; }
        public bool StagingWasOccluded { get; private set; }
        AudioClip roar;
        AudioSource voice;
        GameSession session;
        StalkerBrain actor;
        NavMeshAgent agent;
        float previousSpeed, previousStoppingDistance;
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
            if (!Sample(AuthoredCorner, filter, out var corner) || !Sample(AuthoredReveal, filter, out var reveal)) return false;
            var player = session.player;
            var eye = player.eyes ? player.eyes.transform.position : player.transform.position + Vector3.up * 1.6f;
            var path = new NavMeshPath();
            if (!CompleteFloorPath(corner, reveal, filter, path)) return false;
            // Prefer the branch farther from the player, but validate both branches.
            float side = player.transform.position.z > 0 ? -1 : 1;
            for (int i = 0; i < 2; i++)
            {
                var candidate = new Vector3(13.8f, FloorY, side * (i == 0 ? 3.2f : -3.2f));
                if (!Sample(candidate, filter, out var staging) ||
                    HorizontalDistance(staging, player.transform.position) < MinimumStagingDistance ||
                    !CompleteFloorPath(staging, corner, filter, path) || !OccludedFrom(eye, staging) ||
                    !OccludedFrom(new Vector3(0, 1.6f, 0), staging)) continue;
                SelectedStagingPosition = staging; SelectedCornerPosition = corner; SelectedRevealPosition = reveal;
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
            previousSpeed = agent.speed; previousStoppingDistance = agent.stoppingDistance; settingsOwned = true;
            agent.speed = EmergenceSpeed; agent.stoppingDistance = .08f;
            Phase = "emerge";
            foreach (var destination in new[] { SelectedCornerPosition, SelectedRevealPosition })
            {
                var path = new NavMeshPath();
                if (!EnemyNavigation.TryRoute(agent, destination, FloorY, path, 6) ||
                    !ValidLivePath(path, destination) || !agent.SetPath(path))
                { ReleaseBlocked("Emergence route became unavailable"); yield break; }
                agent.isStopped = false;
                float elapsed = 0, stalled = 0;
                var previous = brain.transform.position;
                var progressAnchor = previous;
                while (true)
                {
                    if (!SessionValid || !brain.gameObject.activeInHierarchy)
                    { Cancel(); yield break; }
                    if (!EnemyNavigation.Ready(agent) || !OnIntroFloor(brain.transform.position))
                    { ReleaseBlocked("Emergence agent left its same-floor route"); yield break; }
                    if (!session.InputAllowed)
                    { EnemyNavigation.Stop(agent); previous = brain.transform.position; yield return null; continue; }
                    agent.isStopped = false;
                    float distance = HorizontalDistance(previous, brain.transform.position);
                    EmergenceDistance += distance; previous = brain.transform.position;
                    elapsed += Time.deltaTime; EmergenceElapsed += Time.deltaTime;
                    stalled += Time.deltaTime;
                    if (HorizontalDistance(progressAnchor, brain.transform.position) >= .025f)
                    { stalled = 0; progressAnchor = brain.transform.position; }
                    if (!agent.pathPending && HorizontalDistance(brain.transform.position, destination) <= ArrivalDistance &&
                        agent.remainingDistance <= ArrivalDistance)
                    { RouteLegsCompleted++; break; }
                    if ((!agent.pathPending && (!agent.hasPath || agent.pathStatus != NavMeshPathStatus.PathComplete)) ||
                        elapsed >= 11 || stalled >= 3)
                    { ReleaseBlocked("Emergence route blocked or timed out"); yield break; }
                    yield return null;
                }
            }
            Phase = "roar"; EnemyNavigation.Stop(agent, true);
            var facing = session.player.transform.position - brain.transform.position; facing.y = 0;
            if (facing.sqrMagnitude > .01f) brain.transform.rotation = Quaternion.LookRotation(facing);
            voice = brain.gameObject.AddComponent<AudioSource>(); voice.playOnAwake = false;
            voice.spatialBlend = 1; voice.minDistance = 2; voice.maxDistance = 18; voice.volume = .6f;
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
            roar=AudioClip.Create("V1 Cyclopse descending roar",samples.Length,1,rate,false);roar.SetData(samples,0);voice.PlayOneShot(roar);RoarPlayed=true;RoarCount++;
            if(session.player.eyes&&session.Shell&&
                Vector3.Distance(session.player.eyes.transform.position,voice.transform.position)<=voice.maxDistance)
                session.Shell.ShowCaption("[복도 끝 · 낮게 가라앉는 포효]", 2.6f, 1);
            for (float elapsed = 0; elapsed < 1.3f || (session && !session.InputAllowed);)
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
            RestoreAgentSettings(); agent.isStopped = false; brain.enabled = true;
            Phase = "done"; Completed = true;
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
            if (agent) { agent.speed = previousSpeed; agent.stoppingDistance = previousStoppingDistance; }
            settingsOwned = false;
        }
        void ReleaseBlocked(string reason)
        {
            FailureReason = reason;
            if (!SessionValid || !actor.gameObject.activeInHierarchy) { Cancel(); return; }
            // A newly blocked door/agent must not cause a visible teleport or a
            // false arrival/roar. Let ordinary AI recover from this exact position.
            EnemyNavigation.Stop(agent, true); RestoreAgentSettings();
            actor.enabled = true; Phase = "blocked";
        }
        // The iterator is driven by StoryDirector. Stopping its parent coroutine
        // never reaches its guards, so cancellation explicitly settles owned state.
        public void Cancel()
        {
            Phase = "resolved";
            if (voice) voice.Stop();
            EnemyNavigation.Stop(agent, true); RestoreAgentSettings();
            if (actor) { actor.enabled = false; actor.gameObject.SetActive(false); }
        }
        void OnDisable() { Cancel(); }
        void OnDestroy() { Cancel(); if (roar) Destroy(roar); if (voice) Destroy(voice); }
    }
}
