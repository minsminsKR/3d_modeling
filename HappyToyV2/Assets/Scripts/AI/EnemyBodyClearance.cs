using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;

namespace HappyToy.V2
{
    // A trigger-only query shape, excluded from navigation baking and ray hits.
    // Keep the rendered torso away from static geometry on the actual NavMesh.
    internal sealed class EnemyBodyClearance
    {
        readonly Transform owner;
        readonly BoxCollider shape;
        readonly Collider[] nearby = new Collider[64];
        public int Corrections { get; private set; }
        public float HorizontalReach => Mathf.Max(shape.size.x * Mathf.Abs(owner.lossyScale.x),
            shape.size.z * Mathf.Abs(owner.lossyScale.z)) * .5f +
            new Vector2(shape.center.x * owner.lossyScale.x, shape.center.z * owner.lossyScale.z).magnitude;
        public static Bounds RenderedBodyBounds(Transform model, Transform actor)
        {
            var rig = model.GetComponent<EnemyStudioRig>();
            if (!rig) return LocalBounds(MonsterPresentationScale.Bounds(model), actor);
            bool any = false; var bounds = new Bounds();
            foreach (var skin in rig.primarySkins)
            {
                var torso = new bool[skin.bones.Length];
                for (int bone = 0; bone < torso.Length; bone++)
                {
                    string name = skin.bones[bone].name;
                    torso[bone] = name.EndsWith(":Hips") || name.Contains(":Spine") || name.EndsWith(":Neck") || name.EndsWith(":Head");
                }
                var points = EnemyStudioRig.PosedVertices(skin);
                var counts = skin.sharedMesh.GetBonesPerVertex(); var weights = skin.sharedMesh.GetAllBoneWeights();
                int cursor = 0;
                for (int vertex = 0; vertex < points.Length; vertex++)
                {
                    float bodyWeight = 0;
                    for (int weight = 0; weight < counts[vertex]; weight++, cursor++)
                        if (torso[weights[cursor].boneIndex]) bodyWeight += weights[cursor].weight;
                    // Extended hands/feet are animated; the torso/head is the solid body.
                    if (bodyWeight < .65f) continue;
                    var local = actor.InverseTransformPoint(points[vertex]);
                    if (!any) { bounds = new Bounds(local, Vector3.zero); any = true; }
                    else bounds.Encapsulate(local);
                }
            }
            return any ? bounds : LocalBounds(MonsterPresentationScale.Bounds(model), actor);
        }
        static Bounds LocalBounds(Bounds world, Transform actor)
        {
            var bounds = new Bounds(actor.InverseTransformPoint(world.center), Vector3.zero);
            for (int x = -1; x <= 1; x += 2)
                for (int y = -1; y <= 1; y += 2)
                    for (int z = -1; z <= 1; z += 2)
                        bounds.Encapsulate(actor.InverseTransformPoint(world.center + Vector3.Scale(world.extents, new Vector3(x, y, z))));
            return bounds;
        }
        public EnemyBodyClearance(Transform actor, Bounds bounds)
        {
            owner = actor;
            // Corridor actors can be cloned after a school actor has awakened.
            // Rebind its copied query instead of leaving two active shapes.
            var existing = actor.Find("Rendered body clearance query");
            var query = existing ? existing.gameObject : new GameObject("Rendered body clearance query");
            query.layer = 2; query.transform.SetParent(actor, false);
            shape = query.GetComponent<BoxCollider>(); if (!shape) shape = query.AddComponent<BoxCollider>();
            shape.enabled = true; shape.isTrigger = true;
            var modifier = query.GetComponent<NavMeshModifier>(); if (!modifier) modifier = query.AddComponent<NavMeshModifier>();
            modifier.ignoreFromBuild = true;
            shape.center = bounds.center;
            var size = bounds.size;
            // Leave the sole and ceiling clearance to navigation/ground mounting.
            var scale = actor.lossyScale;
            size.y = Mathf.Max(.2f / Mathf.Abs(scale.y), size.y - .14f / Mathf.Abs(scale.y));
            size.x += .10f / Mathf.Abs(scale.x); size.z += .10f / Mathf.Abs(scale.z);
            shape.size = size;
        }
        public void Resolve(NavMeshAgent agent)
        {
            if (!EnemyNavigation.Ready(agent)) return;
            Physics.SyncTransforms();
            for (int iteration = 0; iteration < 4; iteration++)
            {
                var half = Vector3.Scale(shape.size * .5f, owner.lossyScale);
                int count = Physics.OverlapBoxNonAlloc(owner.TransformPoint(shape.center), half, nearby,
                    owner.rotation, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                Vector3 correction = Vector3.zero;
                for (int i = 0; i < count; i++)
                {
                    var other = nearby[i];
                    if (!other || other.transform.IsChildOf(owner) || other.GetComponentInParent<PlayerMotor>() ||
                        other.GetComponentInParent<StalkerBrain>() || other.GetComponentInParent<LanternMaskEncounter>() ||
                        other.GetComponentInParent<WeepingAngelEncounter>()) continue;
                    if (Physics.ComputePenetration(shape, owner.position, owner.rotation, other,
                        other.transform.position, other.transform.rotation, out var direction, out float distance) &&
                        Mathf.Abs(direction.y) < .55f)
                    {
                        direction.y = 0;
                        var push = direction.normalized * (distance + .015f);
                        if (push.sqrMagnitude > correction.sqrMagnitude) correction = push;
                    }
                }
                if (correction.sqrMagnitude < .00001f) break;
                correction = Vector3.ClampMagnitude(correction, .18f);
                var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
                if (!NavMesh.SamplePosition(owner.position + correction, out var hit, .2f, filter) ||
                    Mathf.Abs(hit.position.y - owner.position.y) > .2f) break;
                var move = hit.position - owner.position; move.y = 0;
                if (Vector3.Dot(move, correction) <= 0) break;
                agent.Move(move); Corrections++;
            }
        }
        public float SurfaceDistance(Vector3 point)
        {
            var local = owner.InverseTransformPoint(point) - shape.center;
            var half = shape.size * .5f;
            local = new Vector3(Mathf.Clamp(local.x, -half.x, half.x), Mathf.Clamp(local.y, -half.y, half.y),
                Mathf.Clamp(local.z, -half.z, half.z));
            return Vector3.Distance(point, owner.TransformPoint(shape.center + local));
        }
    }
}
