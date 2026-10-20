namespace HappyToy.V2
{
    // Native desired velocity is not evidence that a rendered body has moved.
    // The caller measures displacement from a stable physical-position anchor.
    public sealed class PatrolProgressClock
    {
        public const float MinimumDisplacement=.2f, BlockedSeconds=3;
        float elapsed;
        public int Recoveries {get;private set;}
        public void Reset(){elapsed=0;}
        public bool Tick(float seconds,float displacement,bool walking)
        {
            if(!StealthRules.Finite(seconds)||seconds<=0||!StealthRules.Finite(displacement)||displacement<0)return false;
            if(!walking||displacement>=MinimumDisplacement){Reset();return false;}
            elapsed+=seconds;
            if(elapsed<BlockedSeconds)return false;
            Reset();Recoveries++;return true;
        }
    }
}
