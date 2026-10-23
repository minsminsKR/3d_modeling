using System;

namespace HappyToy.V2
{
    // Metre plan and repeat seam rules. No Unity/session dependency.
    public static class SchoolLoopPlan
    {
        public const int Version=3;
        public const float HalfWidth=2.2f, Length=38.4f, Height=3.25f;
        public const float EntryDepth=4.4f, ConnectorOuterX=8.8f, ConnectorInnerX=4.4f;
        public const float ConnectorCentreX=(ConnectorOuterX+ConnectorInnerX)*.5f;
        public const float TurnbackLength=8, EntryPortalZ=-TurnbackLength, ExitPortalZ=Length+TurnbackLength;
        public const float LinkX=ConnectorCentreX*2, LinkZ=ExitPortalZ-EntryPortalZ;
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
            if(direction==-1)return new SeamPose(-LinkX,EntryPortalZ*2,180);
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
        public static int Crossing(float previousZ,float x,float z)
        {
            if(!Finite(previousZ)||!Finite(x)||!Finite(z))return 0;
            if(previousZ<=ExitPortalZ&&z>ExitPortalZ&&x>ConnectorInnerX&&x<ConnectorOuterX)return 1;
            if(previousZ>=EntryPortalZ&&z<EntryPortalZ&&x>-ConnectorOuterX&&x<-ConnectorInnerX)return -1;
            return 0;
        }
        public static bool Walkable(float x,float z)
        {
            if(!Finite(x)||!Finite(z))return false;
            return z>=0&&z<=Length&&x>=-HalfWidth&&x<=HalfWidth ||
                x>=-ConnectorOuterX&&x<=-HalfWidth&&z>=0&&z<=EntryDepth ||
                x>=HalfWidth&&x<=ConnectorOuterX&&z>=Length-EntryDepth&&z<=Length ||
                x>=-ConnectorOuterX&&x<=-ConnectorInnerX&&z>=EntryPortalZ&&z<=0 ||
                x>=ConnectorInnerX&&x<=ConnectorOuterX&&z>=Length&&z<=ExitPortalZ;
        }
        static bool Finite(float value)=>!float.IsNaN(value)&&!float.IsInfinity(value);
    }
}
