using System;

namespace HappyToy.V2
{
    public enum SchoolAnomalyKind { Normal, Baby, DancingWalker, Blackout }

    // Restorable private RNG: looking, saving and loading never reroll a passage.
    public sealed class SchoolAnomalyDeck
    {
        public const int AnomalyPercent=50,MaximumNormalStreak=3;
        [Serializable] public struct State
        {
            public uint Random;
            public int Passage,NormalStreak;
            public SchoolAnomalyKind Kind;
        }
        State state;
        public SchoolAnomalyDeck(uint seed)
        {
            if(seed==0)throw new ArgumentOutOfRangeException(nameof(seed));
            state=new State{Random=seed,NormalStreak=1};
        }
        public SchoolAnomalyKind Kind=>state.Kind;
        public State Capture()=>state;
        public void Restore(State value){Validate(value);state=value;}
        public static void Validate(State value)
        {
            if(value.Random==0||value.Passage<0||value.Passage>1000000||!Enum.IsDefined(typeof(SchoolAnomalyKind),value.Kind)||
                value.NormalStreak<0||value.NormalStreak>MaximumNormalStreak||
                (value.Kind==SchoolAnomalyKind.Normal)!=(value.NormalStreak>0)||
                value.Passage==0&&(value.Kind!=SchoolAnomalyKind.Normal||value.NormalStreak!=1))
                throw new ArgumentException("Invalid school anomaly deck");
        }
        public static bool Correct(int direction,SchoolAnomalyKind kind)
        {
            if(direction!=1&&direction!=-1)throw new ArgumentOutOfRangeException(nameof(direction));
            if(!Enum.IsDefined(typeof(SchoolAnomalyKind),kind))throw new ArgumentOutOfRangeException(nameof(kind));
            return (direction==1)==(kind==SchoolAnomalyKind.Normal);
        }
        public bool Complete(int direction)
        {
            bool correct=Correct(direction,state.Kind);
            state.Passage=Math.Min(1000000,state.Passage+1);
            bool anomaly=state.NormalStreak>=MaximumNormalStreak||Next()%100<AnomalyPercent;
            state.Kind=anomaly?(SchoolAnomalyKind)(1+Next()%3):SchoolAnomalyKind.Normal;
            state.NormalStreak=anomaly?0:state.NormalStreak+1;
            return correct;
        }
        uint Next(){uint x=state.Random;x^=x<<13;x^=x>>17;x^=x<<5;return state.Random=x;}
    }
}
