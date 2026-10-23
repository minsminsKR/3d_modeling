using UnityEngine;

namespace HappyToy.V2
{
    public sealed partial class PlayerMotor
    {
        // Whole-world seam translation preserves pitch, stance and held items.
        public void RelocateWorld(Vector3 feet,Quaternion facing)
        {
            SetWorldPose(feet,facing);
            fallSpeed=0;moveDirection=Vector3.zero;currentFocus=null;
        }
        // A hidden corridor seam changes reference space, not the player's motion.
        public void TraverseWorldSeam(Vector3 feet,Quaternion turn)
        {
            moveDirection=turn*moveDirection;
            SetWorldPose(feet,turn*transform.rotation);currentFocus=null;
        }
        void SetWorldPose(Vector3 feet,Quaternion facing)
        {
            bool active=controller.enabled;controller.enabled=false;
            transform.SetPositionAndRotation(feet,facing);
            controller.enabled=active;Physics.SyncTransforms();
        }
    }
}
