using UnityEngine;

namespace HappyToy.V2
{
    // Compress the already-posed visual in section axes at the narrow bend.
    // The physical pedestrian capsule and its saved route are independent.
    public static class SchoolFlickerCreatureFit
    {
        public static Vector3 Scale(Bounds pose,Vector3 position,Quaternion yaw,out Vector3 offset)
        {
            float minX=float.PositiveInfinity,maxX=float.NegativeInfinity,minZ=float.PositiveInfinity,maxZ=float.NegativeInfinity;
            for(int i=0;i<4;i++)
            {
                var corner=new Vector3((i&1)==0?pose.min.x:pose.max.x,0,(i&2)==0?pose.min.z:pose.max.z);
                var p=yaw*corner;minX=Mathf.Min(minX,p.x);maxX=Mathf.Max(maxX,p.x);minZ=Mathf.Min(minZ,p.z);maxZ=Mathf.Max(maxZ,p.z);
            }
            // The hall and its left elbow overlap in the 4.4m square. Switch
            // to the elbow rectangle only once the actor reaches its centre.
            bool entry=position.z<=SchoolWalkerRoute.HallEndZ+.001f;
            bool exit=position.z>=SchoolLoopPlan.Length-SchoolWalkerRoute.HallEndZ-.001f;
            float left=entry?-SchoolLoopPlan.ConnectorOuterX+.12f:-SchoolLoopPlan.HalfWidth+.12f;
            float right=exit?SchoolLoopPlan.ConnectorOuterX-.12f:SchoolLoopPlan.HalfWidth-.12f;
            float near=exit?SchoolLoopPlan.Length-SchoolLoopPlan.EntryDepth+.12f:.12f;
            float far=entry?SchoolLoopPlan.EntryDepth-.12f:SchoolLoopPlan.Length-.12f;
            if(position.z<.12f&&position.x<=-SchoolLoopPlan.ConnectorInnerX-.12f){left=-SchoolLoopPlan.ConnectorOuterX+.12f;right=-SchoolLoopPlan.ConnectorInnerX-.12f;near=SchoolLoopPlan.EntryPortalZ+.12f;far=SchoolLoopPlan.EntryDepth-.12f;}
            else if(position.z>SchoolLoopPlan.Length-.12f&&position.x>=SchoolLoopPlan.ConnectorInnerX+.12f){left=SchoolLoopPlan.ConnectorInnerX+.12f;right=SchoolLoopPlan.ConnectorOuterX-.12f;near=SchoolLoopPlan.Length-SchoolLoopPlan.EntryDepth+.12f;far=SchoolLoopPlan.ExitPortalZ-.12f;}
            float x=Axis(minX,maxX,position.x,left,right,.25f,out float shiftX);
            float z=Axis(minZ,maxZ,position.z,near,far,float.PositiveInfinity,out float shiftZ);
            offset=new Vector3(shiftX,0,shiftZ);return new Vector3(x,1,z);
        }
        static float Axis(float low,float high,float at,float minimum,float maximum,float maxShift,out float offset)
        {
            float size=high-low;
            float scale=size>0?Mathf.Clamp01((maximum-minimum)/size):1;
            float lower=minimum-at-low*scale,upper=maximum-at-high*scale;
            offset=Mathf.Clamp(0,lower,Mathf.Max(lower,upper));
            // Keep the torso near the pursuit centreline whenever that line is
            // inside the section, then compress only what the walls require.
            float capped=Mathf.Clamp(offset,-maxShift,maxShift),centre=at+capped;
            if(low<0&&high>0&&centre>=minimum&&centre<=maximum)
            {
                offset=capped;
                scale=Mathf.Min(scale,Mathf.Min((centre-minimum)/-low,(maximum-centre)/high));
            }
            return scale;
        }
    }
}
