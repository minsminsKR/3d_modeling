using System;
using UnityEngine;

namespace HappyToy.V2
{
    // Charge is measured in lit gameplay seconds. Pauses and extinguished lamps cost zero.
    public static class FlashlightChargeRules
    {
        public const float Capacity = 180f, PackCharge = 90f;
        public static float Drain(float charge, float seconds, bool lit, bool playing)
        {
            if (!Valid(charge) || !StealthRules.Finite(seconds) || seconds < 0)
                throw new ArgumentException("Invalid flashlight charge/time");
            return lit && playing ? Mathf.Max(0, charge - seconds) : charge;
        }
        public static bool Valid(float charge) => StealthRules.Finite(charge) && charge >= 0 && charge <= Capacity;
        public static float Refill(float charge)
        {
            if (!Valid(charge)) throw new ArgumentException("Invalid flashlight charge");
            return Mathf.Min(Capacity, charge + PackCharge);
        }
    }

    [DisallowMultipleComponent]
    public sealed class PlayerFlashlight : MonoBehaviour
    {
        PlayerMotor player;
        public float Charge { get; private set; } = FlashlightChargeRules.Capacity;
        public float Fraction => Charge / FlashlightChargeRules.Capacity;
        public bool Depleted => Charge <= 0;
        public bool Lit => player && player.flashlight && player.flashlight.enabled;
        public int PacksCollected { get; private set; }
        public int Depletions { get; private set; }

        void Awake() { player = GetComponent<PlayerMotor>(); }
        void Update()
        {
            if (!player || !player.flashlight) return;
            // Read the actual lamp: hiding, tests and authored effects may extinguish it.
            float before = Charge;
            Charge = FlashlightChargeRules.Drain(Charge, Time.deltaTime,
                player.flashlight.enabled && !player.Hidden, !player.Paused && player.isActiveAndEnabled);
            if (Charge > 0) return;
            player.flashlight.enabled = false;
            if (before <= 0) return;
            Depletions++;
            if (player.Feedback) player.Feedback.PlayFlashlight(false);
            if (GameSession.Current) GameSession.Current.Notify("손전등 배터리가 고갈됐습니다. 배터리를 주운 뒤 F로 다시 켜세요.");
        }
        public bool Toggle()
        {
            if (!player || player.Paused || player.Hidden || !player.flashlight) return false;
            if (!player.flashlight.enabled && Depleted)
            {
                GameSession.Current.Notify("배터리가 없습니다. 회랑의 배터리를 찾아 E로 주우세요.");
                return false;
            }
            player.flashlight.enabled = !player.flashlight.enabled;
            if (player.Feedback) player.Feedback.PlayFlashlight(player.flashlight.enabled);
            return true;
        }
        public bool TryRefill()
        {
            if (!player || player.Paused || player.Hidden || Charge >= FlashlightChargeRules.Capacity) return false;
            Charge = FlashlightChargeRules.Refill(Charge); PacksCollected++;
            // Picking up a pack does not change the switch. Press F to relight after depletion.
            if (player.Feedback) player.Feedback.PlayDiscovery();
            GameSession.Current.Notify("배터리 보충 · " + Mathf.CeilToInt(Fraction * 100) + "% · F 손전등");
            return true;
        }
        public void ResetRun()
        {
            Charge = FlashlightChargeRules.Capacity; PacksCollected = Depletions = 0;
        }
        public void Restore(float charge, int packs, int depletions)
        {
            if (!FlashlightChargeRules.Valid(charge) || packs < 0 || packs > 128 || depletions < 0 || depletions > 1000000)
                throw new ArgumentException("Invalid flashlight checkpoint");
            Charge = charge; PacksCollected = packs; Depletions = depletions;
            if (Depleted && player && player.flashlight) player.flashlight.enabled = false;
        }
    }

    public sealed partial class PlayerMotor
    {
        PlayerFlashlight flashlightSystem;
        public PlayerFlashlight FlashlightSystem
        {
            get
            {
                if (!flashlightSystem) flashlightSystem = GetComponent<PlayerFlashlight>();
                if (!flashlightSystem) flashlightSystem = gameObject.AddComponent<PlayerFlashlight>();
                return flashlightSystem;
            }
        }
    }
}
