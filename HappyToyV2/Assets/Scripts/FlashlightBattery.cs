using UnityEngine;

namespace HappyToy.V2
{
    [DisallowMultipleComponent]
    public sealed class FlashlightBattery : MonoBehaviour
    {
        public int Collections { get; private set; }
        public bool TryCollect(PlayerMotor player)
        {
            if (!isActiveAndEnabled || !gameObject.activeSelf || !player || !GameSession.Current ||
                !GameSession.Current.InputAllowed || player.Hidden) return false;
            if (!player.FlashlightSystem.TryRefill())
            {
                GameSession.Current.Notify("손전등 배터리가 가득 찼습니다. 이 보급은 그대로 남겨 둡니다.");
                return false;
            }
            Collections++; gameObject.SetActive(false); return true;
        }
    }
}
