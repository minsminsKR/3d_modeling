using UnityEngine;

namespace HappyToy.V2
{
    public sealed partial class PlayerMotor
    {
        // Whole-world seam translation preserves pitch, stance and held items.
        public void RelocateWorld(Vector3 feet,Quaternion facing)
        {
            bool active=controller.enabled;controller.enabled=false;
            transform.SetPositionAndRotation(feet,facing);
            fallSpeed=0;moveDirection=Vector3.zero;currentFocus=null;
            controller.enabled=active;Physics.SyncTransforms();
        }
    }
}
