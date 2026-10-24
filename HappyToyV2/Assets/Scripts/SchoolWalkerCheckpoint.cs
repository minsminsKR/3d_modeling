using System;

namespace HappyToy.V2
{
    // Optional serialized adapter; the independent route owns state validation.
    [Serializable]
    public sealed class SchoolWalkerProgress
    {
        public int version=1,traversal,pendingTraversal=-1,leg,passSide;
        public float x,z,yaw,distance,idleBlend;
        public double elapsed,idleElapsed;
        public SchoolWalkerRoute.State ToState()=>new SchoolWalkerRoute.State
        {
            Traversal=traversal,PendingTraversal=pendingTraversal,Leg=leg,PassSide=passSide,
            X=x,Z=z,Yaw=yaw,Distance=distance,IdleBlend=idleBlend,Elapsed=elapsed,IdleElapsed=idleElapsed
        };
        public void Validate(int loopTraversals)
        {
            if(version!=1)throw new ArgumentException("Unknown school walker checkpoint version");
            var state=ToState();SchoolWalkerRoute.Validate(state);
            if(state.Traversal>loopTraversals||state.PendingTraversal>loopTraversals||
                state.Traversal!=loopTraversals&&state.PendingTraversal!=loopTraversals)
                throw new ArgumentException("School walker and corridor traversal disagree");
        }
        public static SchoolWalkerProgress Capture(SchoolWalkerRoute.State state)
        {
            SchoolWalkerRoute.Validate(state);
            return new SchoolWalkerProgress
            {
                traversal=state.Traversal,pendingTraversal=state.PendingTraversal,leg=state.Leg,passSide=state.PassSide,
                x=state.X,z=state.Z,yaw=state.Yaw,distance=state.Distance,idleBlend=state.IdleBlend,
                elapsed=state.Elapsed,idleElapsed=state.IdleElapsed
            };
        }
    }
}
