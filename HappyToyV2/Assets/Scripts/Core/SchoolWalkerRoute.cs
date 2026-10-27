using System;

namespace HappyToy.V2
{
    // One harmless pass through the canonical school hall. Unity owns visibility,
    // while this rule owns bounded motion, personal space and restorable phase.
    public sealed class SchoolWalkerRoute
    {
        public const float StartZ=33,HallEndZ=2.2f,EndZ=1,EndX=-3,DefaultLane=.65f,PassLane=.85f;
        public const float WalkSpeed=.70f,LateralSpeed=.65f,PersonalSpace=.80f;
        const float StopMargin=.015f;
        public const int MaximumTraversal=1000000;
        const double MaximumTime=36000000;
        [Serializable]
        public struct State
        {
            public int Traversal,PendingTraversal,Leg,PassSide;
            public float X,Z,Yaw,Distance,IdleBlend;
            public double Elapsed,IdleElapsed;
        }
        State state;
        public SchoolWalkerRoute(){Reset(0);}
        public float X=>state.X;
        public float Z=>state.Z;
        public bool Completed=>state.Leg==2;
        public State CaptureState()=>state;
        public void Reset(int traversal)
        {
            Traversal(traversal);
            state=new State{Traversal=traversal,PendingTraversal=-1,X=DefaultLane,Z=StartZ,Yaw=180,IdleBlend=1};
        }
        public static void Validate(State value)
        {
            Traversal(value.Traversal);
            if(value.PendingTraversal!=-1&&(value.PendingTraversal<=value.Traversal||value.PendingTraversal>MaximumTraversal))
                throw new ArgumentException("Invalid pending school walker traversal");
            if(value.Leg<0||value.Leg>2||value.PassSide<-1||value.PassSide>1)
                throw new ArgumentException("Invalid school walker route phase");
            if(!Finite(value.X)||!Finite(value.Z)||!Finite(value.Yaw)||!Finite(value.Distance)||!Finite(value.IdleBlend)||
                !Finite(value.Elapsed)||!Finite(value.IdleElapsed)||value.Yaw<0||value.Yaw>=360||
                value.Distance<0||value.Distance>10000000||value.IdleBlend<0||value.IdleBlend>1||
                value.Elapsed<0||value.Elapsed>MaximumTime||value.IdleElapsed<0||value.IdleElapsed>value.Elapsed+.00001)
                throw new ArgumentException("Invalid school walker motion state");
            bool hall=value.Leg==0&&value.X>=-PassLane-.001f&&value.X<=PassLane+.001f&&value.Z>=HallEndZ-.001f&&value.Z<=StartZ+.001f;
            // Older saves can start on either former passing lane. Keep their
            // exact pose and finish a straight hall pass on that saved lane.
            float elbowFraction=(value.Z-EndZ)/(HallEndZ-EndZ);
            float elbowMinX=EndX+(-PassLane-EndX)*elbowFraction;
            float elbowMaxX=EndX+(PassLane-EndX)*elbowFraction;
            bool elbow=value.Leg==1&&elbowFraction>=-.001f&&elbowFraction<=1.001f&&
                value.X>=elbowMinX-.003f&&value.X<=elbowMaxX+.003f;
            bool waiting=value.Leg==2&&Math.Abs(value.X-EndX)<.001f&&Math.Abs(value.Z-EndZ)<.001f;
            if(!hall&&!elbow&&!waiting)throw new ArgumentException("School walker pose is outside its physical route");
            if(value.Leg!=0&&value.PassSide!=0)throw new ArgumentException("A walker outside the hall cannot retain a passing lane");
        }
        public void RestoreState(State value){Validate(value);state=value;}
        public void RequestTraversal(int traversal)
        {
            Traversal(traversal);
            if(traversal<state.Traversal)throw new ArgumentException("Walker traversal cannot move backwards");
            if(traversal==state.Traversal)return;
            state.PendingTraversal=Math.Max(traversal,state.PendingTraversal);
        }
        public bool TryCommitPending(bool physicallyHidden)
        {
            if(!physicallyHidden||state.PendingTraversal<0)return false;
            Reset(state.PendingTraversal);return true;
        }
        public void Advance(double seconds,bool advancing,float playerX,float playerZ,float speedMultiplier=1)
        {
            if(!Finite(seconds)||seconds<0||!Finite(playerX)||!Finite(playerZ)||!Finite(speedMultiplier)||speedMultiplier<0||speedMultiplier>6)
                throw new ArgumentException("Finite school walker step inputs required");
            if(!advancing||seconds==0)return;
            // A stalled graphics frame must never make a person jump metres.
            float dt=(float)Math.Min(seconds,.1);float oldX=state.X,oldZ=state.Z;
            state.Elapsed=Math.Min(MaximumTime,state.Elapsed+dt);
            if(state.Leg==0)Hall(dt,playerX,playerZ,speedMultiplier);
            else if(state.Leg==1)
            {
                float remaining=(float)Math.Sqrt(Squared(EndX-state.X,EndZ-state.Z));
                float fraction=remaining<.000001f?1:Math.Min(1,WalkSpeed*speedMultiplier*dt/remaining);
                float nextX=state.X+(EndX-state.X)*fraction,nextZ=state.Z+(EndZ-state.Z)*fraction;
                if(ClearStep(nextX,nextZ,playerX,playerZ))
                {state.X=nextX;state.Z=nextZ;}
                if(Math.Abs(state.X-EndX)<.0001f&&Math.Abs(state.Z-EndZ)<.0001f)
                {state.X=EndX;state.Z=EndZ;state.Leg=2;}
            }
            float dx=state.X-oldX,dz=state.Z-oldZ;
            float moved=(float)Math.Sqrt(Squared(dx,dz));
            state.Distance=Math.Min(10000000,state.Distance+moved);
            if(moved>.000001f)
            {
                float desired=(float)(Math.Atan2(dx,dz)*180/Math.PI);
                if(desired<0)desired+=360;
                state.Yaw=Turn(state.Yaw,desired,180*dt);
            }
            state.IdleBlend=Towards(state.IdleBlend,moved>.00001f?0:1,dt/.22f);
            if(state.IdleBlend>0)state.IdleElapsed=Math.Min(state.Elapsed,state.IdleElapsed+dt);
        }
        void Hall(float dt,float playerX,float playerZ,float speedMultiplier)
        {
            state.PassSide=0;
            float z=Math.Max(HallEndZ,state.Z-WalkSpeed*speedMultiplier*dt);
            // Hold the current straight lane. Someone in front makes him stand,
            // while someone behind must not prevent him from moving away.
            if(ClearStep(state.X,z,playerX,playerZ))state.Z=z;
            if(state.Z<=HallEndZ+.0001f)
            {state.Z=HallEndZ;state.Leg=1;}
        }
        bool ClearStep(float x,float z,float playerX,float playerZ)
        {
            float gap=Squared(state.X-playerX,state.Z-playerZ),nextGap=Squared(x-playerX,z-playerZ);
            // Ignore tiny grounded-player jitter at the stopping boundary. Once
            // close, moving toward the player cannot restart a creeping gait.
            return nextGap>gap || gap>(PersonalSpace+StopMargin)*(PersonalSpace+StopMargin)&&
                nextGap>=PersonalSpace*PersonalSpace;
        }
        static float Towards(float current,float target,float maximum)
            =>Math.Abs(target-current)<=maximum?target:current+(target>current?maximum:-maximum);
        static float Turn(float current,float target,float maximum)
        {
            float delta=(target-current+540)%360-180;
            float value=current+Math.Max(-maximum,Math.Min(maximum,delta));
            return (value+360)%360;
        }
        static float Squared(float x,float z)=>x*x+z*z;
        static bool Finite(double value)=>!double.IsNaN(value)&&!double.IsInfinity(value);
        static void Traversal(int value)
        {if(value<0||value>MaximumTraversal)throw new ArgumentOutOfRangeException(nameof(value));}
    }
}
