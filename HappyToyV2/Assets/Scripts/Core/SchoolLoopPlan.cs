using System;

namespace HappyToy.V2
{
    // Metre plan and repeat seam rules. No Unity/session dependency.
    public static class SchoolLoopPlan
    {
        public const int Version=3;
        public const float HalfWidth=2.2f, Length=38.4f, Height=3.25f;
        public const float PortalX=7, LinkX=14, LinkZ=34;
        public const float EntryDepth=Length-LinkZ;
        public readonly struct SeamPose
        {
            public readonly float X,Z,Yaw;
            public SeamPose(float x,float z,float yaw){X=x;Z=z;Yaw=yaw;}
        }
        // Both departures lead to the same room order on the traveller's left.
        // The rear continuation is a rigid half-turn, never a mirrored model.
        public static SeamPose Continuation(int direction)
        {
            if(direction==1)return new SeamPose(LinkX,LinkZ,0);
            if(direction==-1)return new SeamPose(-LinkX,EntryDepth,180);
            throw new ArgumentOutOfRangeException(nameof(direction));
        }
        public static SeamPose ReturnToCentre(float x,float z,int direction)
        {
            if(!Finite(x)||!Finite(z))throw new ArgumentException("Finite school seam position required");
            var next=Continuation(direction);
            return direction<0?new SeamPose(next.X-x,next.Z-z,180):new SeamPose(x-next.X,z-next.Z,0);
        }
        public readonly struct Room
        {
            public readonly string Id, Label;
            public readonly float Near, Far;
            public Room(string id,string label,float near,float far)
            {Id=id;Label=label;Near=near;Far=far;}
            public float Width=>7.2f;
            public float West=>-HalfWidth-Width;
            public float DoorZ=>Near+.75f;
            public float WindowNear=>Near+1.55f;
            public int DoorCount=>Id=="study"?2:1;
            public float BackDoorZ=>Far-.75f;
            public float WindowFar=>Far-(DoorCount==2?1.55f:.5f);
        }
        public static readonly Room[] Rooms={
            new Room("shop","매점",6,12.8f),
            new Room("study","정독반",14.4f,23.6f),
            new Room("music","음악실",25.2f,33.4f)};
        public static int Crossing(float previousX,float x,float z)
        {
            if(!Finite(previousX)||!Finite(x)||!Finite(z))return 0;
            if(previousX<=PortalX&&x>PortalX&&z>LinkZ&&z<Length)return 1;
            if(previousX>=-PortalX&&x<-PortalX&&z>0&&z<4.4f)return -1;
            return 0;
        }
        public static bool Walkable(float x,float z)
        {
            if(!Finite(x)||!Finite(z))return false;
            return z>=0&&z<=Length&&x>=-HalfWidth&&x<=HalfWidth ||
                x>=-PortalX&&x<=-HalfWidth&&z>=0&&z<=4.4f ||
                x>=HalfWidth&&x<=PortalX&&z>=LinkZ&&z<=Length;
        }
        static bool Finite(float value)=>!float.IsNaN(value)&&!float.IsInfinity(value);
    }
}
