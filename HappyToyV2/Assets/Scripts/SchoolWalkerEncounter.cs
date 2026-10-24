using System;
using UnityEngine;

namespace HappyToy.V2
{
    // One ordinary pedestrian for each actual school traversal. The scene root
    // owns its lifetime; it has no enemy, attack, navigation or blocking body.
    [DefaultExecutionOrder(100),DisallowMultipleComponent]
    public sealed class SchoolWalkerEncounter : MonoBehaviour
    {
        public const float Height=1.77f,FeetHeight=.006f;
        readonly SchoolWalkerRoute route=new SchoolWalkerRoute();
        readonly Plane[] cameraPlanes=new Plane[6];
        GameSession session;
        Transform section;
        Renderer[] body;
        public Transform Walker=>transform;
        public Transform Visual {get;private set;}
        public SchoolWalkerMotion Motion {get;private set;}
        public int Traversal=>route.CaptureState().Traversal;
        public int PendingTraversal=>route.CaptureState().PendingTraversal;
        public float Distance=>route.CaptureState().Distance;
        public bool Completed=>route.Completed;
        public bool Prepared=>session&&Visual&&Motion;
        public static SchoolWalkerEncounter Build(GameSession owner,Transform centre,GameObject prefab,
            AnimationClip walk,AnimationClip idle=null,float metresPerCycle=.91f)
        {
            if(!owner||!centre||!prefab)throw new ArgumentException("Explicit school walker scene and model inputs required");
            if(centre.GetComponentInChildren<SchoolWalkerEncounter>(true))throw new InvalidOperationException("Only one school pedestrian may exist");
            var actor=new GameObject("School — recurring ordinary pedestrian").AddComponent<SchoolWalkerEncounter>();
            actor.transform.SetParent(centre,false);
            try
            {
                actor.session=owner;actor.section=centre;
                actor.Visual=Instantiate(prefab,actor.transform,false).transform;
                if(actor.Visual.GetComponentsInChildren<Collider>(true).Length>0||actor.Visual.GetComponentsInChildren<Rigidbody>(true).Length>0)
                    throw new InvalidOperationException("School walker art must not block the player's route");
                var group=actor.Visual.GetComponentInChildren<LODGroup>();
                if(!group||group.GetLODs().Length!=3||actor.Visual.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length<3)
                    throw new InvalidOperationException("School walker requires three genuine rigged game LODs");
                foreach(var item in actor.GetComponentsInChildren<Transform>(true))item.gameObject.layer=8;
                actor.body=actor.Visual.GetComponentsInChildren<Renderer>(true);
                actor.Motion=actor.gameObject.AddComponent<SchoolWalkerMotion>();
                actor.Motion.Prepare(actor.Visual.gameObject,walk,idle,metresPerCycle);
                actor.ApplyPose();return actor;
            }
            catch{Destroy(actor.gameObject);throw;}
        }
        public SchoolWalkerRoute.State CaptureState()=>route.CaptureState();
        public void ValidateRestoreState(SchoolWalkerRoute.State state)=>SchoolWalkerRoute.Validate(state);
        public void RestoreState(SchoolWalkerRoute.State state)
        {ValidateRestoreState(state);route.RestoreState(state);ApplyPose();}
        // Called while the checkpoint loading curtain covers the restored scene.
        public void RestoreTraversal(int traversal)
        {
            route.Reset(traversal);
            if(section&&session&&session.player)
            {
                var player=section.InverseTransformPoint(session.player.transform.position);
                if(Mathf.Abs(player.z-SchoolWalkerRoute.StartZ)<1.5f&&Mathf.Abs(player.x-SchoolWalkerRoute.DefaultLane)<1.5f)
                {var initial=route.CaptureState();initial.X=player.x>=0?-SchoolWalkerRoute.PassLane:SchoolWalkerRoute.PassLane;route.RestoreState(initial);}
            }
            ApplyPose();
        }
        // Ordinary looking/turning never resets the actor. Only a completed map
        // traversal requests a new pass, and both old/new bodies must be hidden.
        public void OnTraversal(int traversal)=>route.RequestTraversal(traversal);
        void LateUpdate()
        {
            if(!Prepared||!session.InputAllowed||session.Finished||EnemyTimeStop.Active)return;
            var camera=session.player?session.player.eyes:null;
            if(route.CaptureState().PendingTraversal>=0&&camera)
            {
                var current=BodyBounds();
                float radius=Mathf.Max(.52f,current.extents.x,current.extents.z);
                var spawn=new Bounds(section.TransformPoint(new Vector3(SchoolWalkerRoute.DefaultLane,FeetHeight+Height*.5f,SchoolWalkerRoute.StartZ)),
                    new Vector3(radius*2,Mathf.Max(Height,current.size.y),radius*2));
                if(route.TryCommitPending(PhysicallyHidden(camera,current)&&PhysicallyHidden(camera,spawn)))ApplyPose();
            }
            var player=section.InverseTransformPoint(session.player.transform.position);
            route.Advance(Time.deltaTime,true,player.x,player.z);ApplyPose();
        }
        public Bounds BodyBounds()
        {
            var bounds=new Bounds(transform.position+Vector3.up*Height*.5f,new Vector3(.9f,Height,.65f));
            if(body!=null)foreach(var renderer in body)if(renderer)bounds.Encapsulate(renderer.bounds);
            return bounds;
        }
        bool PhysicallyHidden(Camera camera,Bounds bounds)
        {
            GeometryUtility.CalculateFrustumPlanes(camera,cameraPlanes);
            bool inside=GeometryUtility.TestPlanesAABB(cameraPlanes,bounds);
            // Occlusion is mandatory even behind the camera: an unoccluded man
            // can still be visible in the wet floor's actual planar reflection.
            bool physicallyBlocked=true;
            for(int sample=0;sample<9;sample++)
            {
                Vector3 point=bounds.center;
                if(sample<8)
                {
                    var sign=new Vector3((sample&1)==0?-1:1,(sample&2)==0?-1:1,(sample&4)==0?-1:1);
                    point+=Vector3.Scale(bounds.extents,sign);
                }
                var ray=point-camera.transform.position;
                if(ray.sqrMagnitude<.01f)return false;
                bool blocked=Physics.Raycast(camera.transform.position,ray.normalized,ray.magnitude-.025f,1<<8,QueryTriggerInteraction.Ignore);
                if(!blocked)
                {
                    if(inside)return false;
                    physicallyBlocked=false;
                }
            }
            // Frustum is evaluated for the actual game camera, never the
            // renderer.isVisible flag shared with the reflection camera.
            return physicallyBlocked;
        }
        void ApplyPose()
        {
            if(!section||!Motion)return;
            var state=route.CaptureState();
            transform.localPosition=new Vector3(state.X,FeetHeight,state.Z);
            transform.localRotation=Quaternion.Euler(0,state.Yaw,0);Motion.Apply(state);
        }
    }
}
