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
    // Typed native adaptation of the proven known-map, nearest-goal corridor
    // input strategy. No reflection, NUnit, direct interactions or AI changes.
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
        public bool Passed => passed; public Mouse VirtualMouse => mouse;
        float meters, gameStart;
        Vector3 previous;
        int maximumThreats, doorsOpened, throws, dodges, hidingEntries, suppliesCollected, lastRecovered;
        float alertUntil, lastThrow = -100, lastDodge = -100, lastHide = -100, lastGameTime;
        bool expectEscape, passed, disposed, seekingHiding;
        string stage = "startup";
        public NativeCorridorInputRoute(GameSession session, PlayerMotor player, GameShell shell, Action<Key[]> keys, string evidenceDirectory)
        {
            this.session = session; this.player = player; this.shell = shell; this.keys = keys; this.evidenceDirectory = evidenceDirectory;
            eyes = player.eyes; capsule = player.GetComponent<CharacterController>();
            monsters = Stalkers().Where(x => x.name.EndsWith("— corridor")).ToArray();
            speeds = monsters.Select(x => new Vector2(x.patrolSpeed, x.chaseSpeed)).ToArray();
            previous = player.transform.position; wallStart = Time.realtimeSinceStartup;
            previousMouse = Mouse.current; mouse = InputSystem.AddDevice<Mouse>();
        }
        void Check()
        {
            meters += Vector3.Distance(previous, player.transform.position); previous = player.transform.position;
            lastGameTime = session.ElapsedPlayTime - gameStart; lastRecovered = session.RecordsRecovered;
            observedKeyboard = Keyboard.current;
            maximumThreats = Mathf.Max(maximumThreats, monsters.Count(x => x.gameObject.activeSelf));
            bool terminal = session.Finished && !(expectEscape && session.Escaped);
            if (terminal) SaveEvidence();
            Require(!(terminal), "Run ended at " + stage + " after " + session.ElapsedPlayTime + "s; " + session.DefeatSource + "; at " + player.transform.position);
            Require((Time.realtimeSinceStartup - wallStart) < (590), "Wall budget expired at " + stage);
            Require((session.ElapsedPlayTime - gameStart) < (580), "Game budget expired at " + stage);
            Require(Time.timeScale==1 && Time.captureDeltaTime==0 && player.enabled,"Native natural clock/motor changed");
            Require(Mouse.current==mouse,"Native route mouse ownership changed");
            Require((player.transform.position.y) >= (-.15f) && (player.transform.position.y) <= (.85f), "Left the walkable floor/low-step range at " + player.transform.position);
            Require((Physics.Raycast(player.transform.position + Vector3.up * .15f, Vector3.down, .95f,
                (1 << 8) | (1 << 9), QueryTriggerInteraction.Ignore)), "No physical support below feet at " + player.transform.position);
        }
        void Mark(string text)
        { string entry = session.ElapsedPlayTime.ToString("F2") + "s " + text + " at " + player.transform.position; milestones.Add(entry); Debug.Log("CORRIDOR_INPUT_MILESTONE " + entry); SaveEvidence(); }
        public IEnumerator Run()
        {
            gameStart = session.ElapsedPlayTime;
            if (player.flashlight.enabled) yield return Pulse(Key.F);
            yield return Pulse(Key.C);
            Require((player.Crouching), "Native corridor invariant failed: Is.True"); Mark("dark, quiet stance through F/C");
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
            }
            var exit = Items().Single(x => x.name == "Sealed entrance"); stage = "return to entrance";
            Require((FindApproach(exit, out var exitAt, out _)), "Native corridor invariant failed: Is.True"); yield return Walk(exitAt);
            expectEscape = true; yield return Interact(exit); Require((session.Escaped), "Native corridor invariant failed: Is.True");
            for (int i = 0; i < monsters.Length; i++) Require(new Vector2(monsters[i].patrolSpeed,monsters[i].chaseSpeed)==speeds[i],"Authored corridor threat speed changed");
            Require((maximumThreats) == (4), "Native corridor invariant failed: Is.EqualTo(4)"); Require((meters) > (80), "Native corridor invariant failed: Is.GreaterThan(80)");
            passed = true; Mark("escaped with all five memories");
        }
        IEnumerator TopUpNearbySupply(float maximumDetour)
        {
            var stock = player.Firecrackers;
            if (stock.Count >= 2) yield break;
            Interactable selected = null; Vector3 at = Vector3.zero; float best = maximumDetour;
            foreach (var supply in Items().Where(x => x.gameObject.activeSelf &&
                x.kind.ToString() == "FirecrackerSupply"))
                if (FindApproach(supply, out var point, out var length) && length < best)
                { selected = supply; at = point; best = length; }
            if (!selected) yield break;
            string previousStage = stage; stage = "collect nearby supply";
            yield return Walk(at); int before = stock.Count; yield return Interact(selected);
            Require((stock.Count) == (before + 1), "Native corridor invariant failed: Is.EqualTo(before + 1)"); suppliesCollected++;
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
        StalkerBrain VisibleThreat()
        {
            StalkerBrain closest = null; float nearest = 8;
            foreach (var monster in monsters)
            {
                if (!monster.gameObject.activeSelf) continue;
                var delta = monster.transform.position + Vector3.up - eyes.transform.position;
                if (delta.magnitude >= nearest || !RecognitionCue && Vector3.Angle(eyes.transform.forward, delta) > 80) continue;
                if (Physics.Linecast(eyes.transform.position, monster.transform.position + Vector3.up, out var hit,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) && hit.collider.GetComponentInParent<StalkerBrain>() != monster) continue;
                closest = monster; nearest = delta.magnitude;
            }
            return closest;
        }
        IEnumerator Decoy(StalkerBrain monster)
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
        IEnumerator Dodge(StalkerBrain monster, Vector3 destination)
        {
            var observedGuard = monster.transform.position;
            if (player.Crouching) yield return Pulse(Key.C);
            // Face a visible pursuer and react to the real committed swing,
            // rather than blindly crossing it before the warning has resolved.
            keys(Array.Empty<Key>());
            float baitDeadline = GameTime + 1.35f;
            while (VisibleThreat() == monster && !monster.AttackActive && GameTime < baitDeadline)
            {
                observedGuard = monster.transform.position;
                Steer(observedGuard + Vector3.up, true);
                yield return null; Check();
            }
            bool observedStrike = VisibleThreat() == monster && monster.AttackActive;
            if (observedStrike) observedGuard = monster.transform.position;
            float passNotBefore = GameTime;
            if (observedStrike && monster.AttackRecovery <= 0)
                passNotBefore += .75f * (1 - monster.AttackWindup);
            var away = player.transform.position - observedGuard; away.y = 0;
            var tangent = Vector3.Cross(Vector3.up, away.normalized);
            var selected = new NavMeshPath(); bool found = false; float best = 0;
            foreach (var direction in new[] { tangent, -tangent, away.normalized })
            {
                var at = player.transform.position + direction * 2.2f;
                if (!NavMesh.SamplePosition(at, out var sample, .4f, NavMesh.AllAreas) || Mathf.Abs(sample.position.y) > .15f) continue;
                var path = new NavMeshPath();
                if (!NavMesh.CalculatePath(player.transform.position, sample.position, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete) continue;
                float length = 0; for (int n = 1; n < path.corners.Length; n++) length += Vector3.Distance(path.corners[n - 1], path.corners[n]);
                float clearance = Vector3.Distance(sample.position, observedGuard);
                if (length > 3 || clearance < best) continue;
                selected = path; best = clearance; found = true;
            }
            if (!found) yield break;
            alertUntil = GameTime + 6; lastDodge = GameTime;
            foreach (var corner in selected.corners.Skip(1))
            {
                float deadline = Time.realtimeSinceStartup + 2;
                while (Vector3.Distance(player.transform.position, corner) > .25f && Time.realtimeSinceStartup < deadline)
                {
                    Check(); var delta = corner - player.transform.position; delta.y = 0; Steer(eyes.transform.position + delta, true);
                    keys(Vector3.Angle(player.transform.forward, delta) > 12 ? Array.Empty<Key>() : new[] { Key.W, Key.LeftShift }); yield return null;
                }
            }
            keys(Array.Empty<Key>()); dodges++; Mark(observedStrike ? "evaded an observed committed swing through real movement" : "retreated from observed guard through real movement");
            if (!observedStrike) yield break;
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
                    if (length < 7) foreach (var corner in crossing.corners.Skip(1))
                    {
                        float deadline = Time.realtimeSinceStartup + 2;
                        while (Vector3.Distance(player.transform.position, corner) > .25f && Time.realtimeSinceStartup < deadline)
                        { Check(); var delta = corner - player.transform.position; delta.y = 0; Steer(eyes.transform.position + delta, true);
                            keys(Vector3.Angle(player.transform.forward, delta) > 12 ? Array.Empty<Key>() : new[] { Key.W, Key.LeftShift }); yield return null; }
                    }
                }
            }
            keys(Array.Empty<Key>());
        }
        IEnumerator Walk(Vector3 at)
        {
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
                        if (separation < 3.2f && Vector3.Dot(delta.normalized, (guard.transform.position - player.transform.position).normalized) > .15f && GameTime - lastDodge > .9f)
                        {
                            int before = dodges; yield return Dodge(guard, corner); progressTime = Time.realtimeSinceStartup;
                            // Evasion can carry us past the old corner. Re-plan
                            // from actual feet instead of walking back into it.
                            if (dodges > before) { yield return Walk(at); yield break; }
                            continue;
                        }
                    }
                    if (!player.Crouching && GameTime > alertUntil && !RecognitionCue)
                        yield return Pulse(Key.C);
                    if (Physics.SphereCast(eyes.transform.position, .08f, delta.normalized, out var obstacle, Mathf.Min(2.05f, distance + .1f), Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                    {
                        var door = obstacle.collider.GetComponentInParent<Interactable>();
                        if (door && door.kind.ToString() == "Door" && !door.IsOpen)
                        {
                            yield return Interact(door); doorsOpened++; Mark("opened real door through E");
                            keys(Array.Empty<Key>()); float wait = Time.realtimeSinceStartup + 1.65f;
                            while (Time.realtimeSinceStartup < wait) { yield return null; Check(); }
                            progressTime = Time.realtimeSinceStartup;
                        }
                    }
                    Steer(eyes.transform.position + delta, true);
                    keys(Vector3.Angle(player.transform.forward, delta) > 12 ? Array.Empty<Key>() : !player.Crouching && !player.SprintExhausted ? new[] { Key.W, Key.LeftShift } : new[] { Key.W }); yield return null;
                }
            }
            keys(Array.Empty<Key>()); yield return null; Check();
        }
        IEnumerator Interact(Interactable item)
        {
            keys(Array.Empty<Key>()); float deadline = Time.realtimeSinceStartup + 4;
            while (player.Focus != item)
            {
                Check(); Require((Time.realtimeSinceStartup) < (deadline), "Could not focus " + item.name);
                var collider = item.GetComponentsInChildren<Collider>().First(x => x.enabled && !x.isTrigger);
                Steer(collider.bounds.center); yield return null;
            }
            yield return Pulse(Key.E);
        }
        IEnumerator HideFrom(StalkerBrain guard)
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
        [Serializable] sealed class Evidence { public bool passed; public string stage; public float gameSeconds, physicalMeters; public int maximumThreats, doorsOpened, throws, dodges, hidingEntries, suppliesCollected, recovered; public string[] milestones; }
        void SaveEvidence()
        {
            System.IO.Directory.CreateDirectory(evidenceDirectory);
            System.IO.File.WriteAllText(System.IO.Path.Combine(evidenceDirectory,"native-corridor-route.json"), JsonUtility.ToJson(new Evidence { passed = passed, stage = stage,
                gameSeconds = lastGameTime, physicalMeters = meters, maximumThreats = maximumThreats,
                doorsOpened = doorsOpened, throws = throws, dodges = dodges, hidingEntries = hidingEntries,
                suppliesCollected = suppliesCollected, recovered = lastRecovered, milestones = milestones.ToArray() }, true));
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
            SaveEvidence();
            if (observedKeyboard != null && observedKeyboard.added) InputSystem.QueueStateEvent(observedKeyboard, new KeyboardState());
            if (mouse.added) InputSystem.RemoveDevice(mouse); if (previousMouse != null && previousMouse.added) previousMouse.MakeCurrent();
        }
    }
}
