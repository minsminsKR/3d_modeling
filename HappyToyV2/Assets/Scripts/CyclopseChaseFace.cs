using UnityEngine;

namespace HappyToy.V2
{
    // Retains the existing component identity and Configure API for scenes and callers.
    [DefaultExecutionOrder(100), DisallowMultipleComponent]
    public sealed class CyclopseChaseFace : EnemyChaseFace
    {
        public void Configure(SkinnedMeshRenderer renderer, StalkerBrain owner) =>
            Configure(renderer, owner, "CyclopseExpression/Expression");
    }
}
