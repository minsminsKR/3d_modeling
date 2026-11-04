using System;
using UnityEngine;

namespace HappyToy.V2
{
    // Collider and normal PlayerMotor focus own physical reach and occlusion.
    // The world supplies the phase-specific action and availability.
    public sealed class TemplePrologueTarget : MonoBehaviour
    {
        Func<bool> available;
        Action<PlayerMotor> action;
        public bool Available => available != null && available();
        public void Prepare(Func<bool> canUse, Action<PlayerMotor> use)
        {
            available = canUse ?? throw new ArgumentNullException(nameof(canUse));
            action = use ?? throw new ArgumentNullException(nameof(use));
        }
        public void Use(PlayerMotor player)
        {
            if (player && Available) action(player);
        }
    }
}
