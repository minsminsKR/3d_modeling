using System;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;

namespace HappyToy.V2
{
    // A separate threat from the harmless school walker. The saved route owns
    // its clock/position; this owner supplies real navigation and contact.
    public sealed class SchoolFlickerPursuit
    {
        const float ContactDepth=.78f,HallContactWidth=1.95f,OtherContactRadius=.78f;
        readonly GameSession session;readonly Transform section;readonly SchoolWalkerRoute route;
        readonly Transform[] contactJoints;
        readonly NavMeshPath path=new NavMeshPath();
        public SchoolFlickerPursuit(GameSession session,Transform section,SchoolWalkerRoute route,Transform creature)
        {
            this.session=session?session:throw new ArgumentNullException(nameof(session));this.section=section?section:throw new ArgumentNullException(nameof(section));this.route=route??throw new ArgumentNullException(nameof(route));
            if(!creature)throw new ArgumentNullException(nameof(creature));
            string[] names={"bone_1","bone_2","bone_3","bone_4","bone_5","bone_6","bone_7","bone_8","bone_9","bone_25","bone_26","bone_27","bone_28","bone_44","bone_45","bone_46","bone_48","bone_49","bone_50"};
            contactJoints=creature.GetComponentsInChildren<Transform>(true).Where(j=>names.Contains(j.name)).ToArray();
            if(contactJoints.Length!=names.Length)throw new InvalidOperationException("Creature contact joints are missing");
        }
        static bool InHall(Vector3 p)=>Mathf.Abs(p.x)<=SchoolLoopPlan.HalfWidth&&p.z>=0&&p.z<=SchoolLoopPlan.Length;
        bool ClearSight(Vector3 from,Vector3 player)
        {
            var origin=section.TransformPoint(from+Vector3.up*.9f);var target=section.TransformPoint(player+Vector3.up*.9f);
            return !Physics.Linecast(origin,target,1<<8,QueryTriggerInteraction.Ignore);
        }
        bool Touches(Vector3 from,Vector3 to,Vector3 player)
        {
            if(!ClearSight(from,player))return false;
            if(InHall(from)&&InHall(to)&&InHall(player))
                return Mathf.Abs(player.x-from.x)<=HallContactWidth&&
                    player.z>=Mathf.Min(from.z,to.z)-ContactDepth&&player.z<=Mathf.Max(from.z,to.z)+ContactDepth;
            var a=new Vector2(from.x,from.z);var b=new Vector2(to.x,to.z);var p=new Vector2(player.x,player.z);
            var line=b-a;float t=line.sqrMagnitude<.000001f?0:Mathf.Clamp01(Vector2.Dot(p-a,line)/line.sqrMagnitude);
            return (p-(a+t*line)).sqrMagnitude<=OtherContactRadius*OtherContactRadius;
        }
        void Defeat()=>session.TryDefeat("붉은 점멸 괴물","괴물이 다가오면 돌아가세요. 닿으면 즉시 쓰러집니다.");
        bool TouchesJoints()
        {
            var controller=session.player.GetComponent<CharacterController>();if(!controller||!controller.enabled)return false;
            var bounds=controller.bounds;
            foreach(var joint in contactJoints)
            {
                float radius=joint.name=="bone_3"?.48f:joint.name=="bone_5"?.38f:.22f;
                if(bounds.SqrDistance(joint.position)>radius*radius)continue;
                if(!Physics.Linecast(joint.position,bounds.center,1<<8,QueryTriggerInteraction.Ignore))return true;
            }
            return false;
        }
        public void Tick(float delta)
        {
            if(!session||!session.player)return;
            var before=route.CaptureState();var from=new Vector3(before.X,0,before.Z);
            var player=section.InverseTransformPoint(session.player.transform.position);player.y=0;
            if(Touches(from,from,player)||TouchesJoints()){Defeat();return;}
            route.AdvancePursuitClock(delta);var now=route.CaptureState();
            long first=SchoolWalkerFlicker.FlashIndex(before.Elapsed),last=SchoolWalkerFlicker.FlashIndex(now.Elapsed);
            for(long flash=first+1;flash<=last;flash++)
            {
                float step=SchoolFlickerDread.Stride(flash);if(step<=0)continue;
                now=route.CaptureState();from=new Vector3(now.X,0,now.Z);
                // In the straight hall the giant occupies the entire width and
                // remains centred; in a connector it follows the NavMesh bends.
                var destination=player;
                if(InHall(from)&&InHall(player))destination.x=0;
                if(!NavMesh.SamplePosition(section.TransformPoint(from),out var start,.8f,NavMesh.AllAreas)||
                   !NavMesh.SamplePosition(section.TransformPoint(destination),out var goal,.8f,NavMesh.AllAreas)||
                   !NavMesh.CalculatePath(start.position,goal.position,NavMesh.AllAreas,path)||path.status!=NavMeshPathStatus.PathComplete)continue;
                var corners=path.corners;Vector3 waypoint=destination;
                for(int i=1;i<corners.Length;i++)
                {var local=section.InverseTransformPoint(corners[i]);local.y=0;if((local-from).sqrMagnitude>.0025f){waypoint=local;break;}}
                var next=Vector3.MoveTowards(from,waypoint,step);next.y=0;
                if(!SchoolLoopPlan.Walkable(next.x,next.z))continue;
                var deltaWorld=section.TransformVector(next-from);float length=deltaWorld.magnitude;
                if(length>.001f&&Physics.CapsuleCast(section.TransformPoint(from+Vector3.up*.4f),section.TransformPoint(from+Vector3.up*1.25f),.28f,deltaWorld/length,out _,length,1<<8,QueryTriggerInteraction.Ignore))continue;
                if(Touches(from,next,player)){Defeat();return;}
                route.MovePursuit(next.x,next.z);
            }
        }
    }
}
