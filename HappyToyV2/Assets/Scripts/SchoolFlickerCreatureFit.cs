using UnityEngine;

namespace HappyToy.V2
{
    // Compress the already-posed visual in section axes at the narrow bend.
    // The physical pedestrian capsule and its saved route are independent.
    public static class SchoolFlickerCreatureFit
    {
        public static Vector3 Scale(Bounds pose,Vector3 position,Quaternion yaw)
        {
            float minX=float.PositiveInfinity,maxX=float.NegativeInfinity,minZ=float.PositiveInfinity,maxZ=float.NegativeInfinity;
            for(int i=0;i<4;i++)
            {
                var corner=new Vector3((i&1)==0?pose.min.x:pose.max.x,0,(i&2)==0?pose.min.z:pose.max.z);
                var p=yaw*corner;minX=Mathf.Min(minX,p.x);maxX=Mathf.Max(maxX,p.x);minZ=Mathf.Min(minZ,p.z);maxZ=Mathf.Max(maxZ,p.z);
            }
            // The hall and its left elbow overlap in the 4.4m square. Switch
            // to the elbow rectangle only once the actor reaches its centre.
            bool elbow=position.z<=SchoolWalkerRoute.HallEndZ+.001f;
            float left=elbow?-SchoolLoopPlan.ConnectorOuterX+.12f:-SchoolLoopPlan.HalfWidth+.12f;
            float right=SchoolLoopPlan.HalfWidth-.12f;
            float near=.12f,far=elbow?SchoolLoopPlan.EntryDepth-.12f:SchoolLoopPlan.Length-.12f;
            return new Vector3(Axis(minX,maxX,position.x,left,right),1,Axis(minZ,maxZ,position.z,near,far));
        }
        static float Axis(float low,float high,float at,float minimum,float maximum)
        {
            float scale=1;
            if(low<0)scale=Mathf.Min(scale,Mathf.Max(0,(at-minimum)/-low));
            if(high>0)scale=Mathf.Min(scale,Mathf.Max(0,(maximum-at)/high));
            return Mathf.Clamp01(scale);
        }
    }
}
