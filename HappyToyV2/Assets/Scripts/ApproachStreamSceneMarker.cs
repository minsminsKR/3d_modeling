using UnityEngine;

namespace HappyToy.V2
{
    // Claims distinguish overlapping loads of the same scene during restarts.
    public sealed class ApproachStreamSceneMarker : MonoBehaviour
    {
        internal bool Claimed;
    }
}
