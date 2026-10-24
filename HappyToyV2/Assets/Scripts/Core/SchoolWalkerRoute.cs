using System;

namespace HappyToy.V2
{
    // One harmless pass through the canonical school hall. Unity owns visibility,
    // while this rule owns bounded motion, personal space and restorable phase.
    public sealed class SchoolWalkerRoute
    {
        public const float StartZ=33,HallEndZ=2.2f,EndZ=1,EndX=-3,DefaultLane=.65f,PassLane=.85f;
        public const float WalkSpeed=.70f,LateralSpeed=.65f,PersonalSpace=.80f;
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
            float elbowZ=HallEndZ+(value.X-DefaultLane)/(EndX-DefaultLane)*(EndZ-HallEndZ);
            bool elbow=value.Leg==1&&value.X>=EndX-.001f&&value.X<=DefaultLane+.001f&&Math.Abs(value.Z-elbowZ)<.003f;
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
        public void Advance(double seconds,bool advancing,float playerX,float playerZ)
        {
            if(!Finite(seconds)||seconds<0||!Finite(playerX)||!Finite(playerZ))
                throw new ArgumentException("Finite school walker step inputs required");
            if(!advancing||seconds==0)return;
            // A stalled graphics frame must never make a person jump metres.
            float dt=(float)Math.Min(seconds,.1);float oldX=state.X,oldZ=state.Z;
            state.Elapsed=Math.Min(MaximumTime,state.Elapsed+dt);
            if(state.Leg==0)Hall(dt,playerX,playerZ);
            else if(state.Leg==1)
            {
                float remaining=(float)Math.Sqrt(Squared(EndX-state.X,EndZ-state.Z));
                float fraction=remaining<.000001f?1:Math.Min(1,WalkSpeed*dt/remaining);
                float nextX=state.X+(EndX-state.X)*fraction,nextZ=state.Z+(EndZ-state.Z)*fraction;
                if(Squared(nextX-playerX,nextZ-playerZ)>=PersonalSpace*PersonalSpace||
                    Squared(nextX-playerX,nextZ-playerZ)>Squared(state.X-playerX,state.Z-playerZ))
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
        void Hall(float dt,float playerX,float playerZ)
        {
            float gap=Math.Abs(state.Z-playerZ);
            bool nearby=gap<6&&Math.Abs(playerX)<2.2f&&state.Z>6;
            if(nearby)
            {
                if(state.PassSide==0)state.PassSide=playerX>0?-1:1;
                // If someone deliberately occupies the chosen lane, yield and
                // smoothly take the genuinely open side instead of pushing them.
                if(gap<1.7f&&Math.Abs(state.PassSide*PassLane-playerX)<.50f)
                    state.PassSide=playerX>0?-1:1;
            }
            else if(gap>6.8f||state.Z<=6)state.PassSide=0;
            float target=state.Z<=6?DefaultLane:state.PassSide==0?DefaultLane:state.PassSide*PassLane;
            float x=Towards(state.X,target,LateralSpeed*dt),z=Math.Max(HallEndZ,state.Z-WalkSpeed*dt);
            float dx=x-state.X,dz=z-state.Z,distance=(float)Math.Sqrt(Squared(dx,dz));
            if(distance>WalkSpeed*dt)
            {float scale=WalkSpeed*dt/distance;x=state.X+dx*scale;z=state.Z+dz*scale;}
            if(Squared(x-playerX,z-playerZ)<PersonalSpace*PersonalSpace)
            {
                z=state.Z;
                // Lateral evasion is allowed only if it opens personal space.
                if(Squared(x-playerX,z-playerZ)<Squared(state.X-playerX,state.Z-playerZ))x=state.X;
            }
            state.X=x;state.Z=z;
            if(state.Z<=HallEndZ+.0001f&&Math.Abs(state.X-DefaultLane)<.0001f)
            {state.X=DefaultLane;state.Z=HallEndZ;state.PassSide=0;state.Leg=1;}
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
