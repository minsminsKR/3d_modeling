using System;
using UnityEngine;

namespace HappyToy.V2
{
    public enum PlayerItem { Snapper, Trap, Incense, Talisman, Flashlight }

    [DefaultExecutionOrder(40)]
    [DisallowMultipleComponent]
    public sealed class PlayerItemLoadout : MonoBehaviour
    {
        public const int SlotCount = 5;
        // A player capability, never a fabricated stock count or checkpoint value.
        public bool Unlimited => player && GameSession.Current && GameSession.Current.player == player && GameSession.Current.CheatSafeMode;
        public PlayerItem Selected { get; private set; }
        PlayerMotor player;
        bool requireUseRelease = true;
        void Awake() { player = GetComponent<PlayerMotor>(); }
        public void Select(PlayerItem item)
        {
            Validate(item);
            Selected = item;
            if (player && player.Firecrackers && player.Firecrackers.Aim) player.Firecrackers.Aim.Cancel();
        }
        static void Validate(PlayerItem item)
        {
            if ((int)item < 0 || (int)item >= SlotCount) throw new ArgumentOutOfRangeException(nameof(item));
        }
        public int Count(PlayerItem item)
        {
            Validate(item);
            switch (item)
            {
                case PlayerItem.Snapper: return player.Firecrackers.Count;
                case PlayerItem.Trap: return player.Traps.Count;
                case PlayerItem.Incense: return player.Rituals.Incense;
                case PlayerItem.Talisman: return player.Rituals.Talismans;
                default: return 1;
            }
        }
        public bool UseSelected()
        {
            var session = GameSession.Current;
            if (!player || !session || !session.InputAllowed || player.Paused || player.Hidden ||
                session.Shell.ItemWheelOpen || player.Rituals.Burning) return false;
            switch (Selected)
            {
                case PlayerItem.Snapper: return player.Firecrackers.TryThrow();
                case PlayerItem.Trap: return player.Traps.TryPlace();
                case PlayerItem.Incense: return player.Rituals.UseIncense();
                case PlayerItem.Talisman: return player.Rituals.UseTalisman();
                default: return player.FlashlightSystem.Toggle();
            }
        }
        void Update()
        {
            var session = GameSession.Current;
            bool allowed = session && session.InputAllowed && !player.Paused && !player.Hidden &&
                !session.Shell.ItemWheelOpen && !player.Rituals.Burning && PlayerControls.AcceptsInput &&
                session.Shell.GameplayEntryFrame != Time.frameCount && session.Shell.ItemWheelChangedFrame != Time.frameCount;
            if (!allowed) { requireUseRelease = true; return; }
            if (!PlayerControls.UseItemHeld) requireUseRelease = false;
            if (!requireUseRelease && PlayerControls.UseItem) UseSelected();
        }
        void OnApplicationFocus(bool focused) { if (!focused) requireUseRelease = true; }
        void OnDisable() { requireUseRelease = true; }
    }
}
