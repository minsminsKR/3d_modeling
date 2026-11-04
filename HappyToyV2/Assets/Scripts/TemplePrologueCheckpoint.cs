using System;
using UnityEngine;

namespace HappyToy.V2
{
    [Serializable]
    public sealed class TemplePrologueCheckpoint
    {
        public int version = 2, phase;
        public string token;
        public float prayerElapsed, seconds, flashlightCharge;
        public int flashlightPacks, flashlightDepletions;
        public bool gateVisible, incensePlaced;
        public PlayerMotor.Progress player;

        public static bool Point(Vector3 p) => CorridorCheckpoint.Number(p.x, 478.5f, 521.5f) &&
            CorridorCheckpoint.Number(p.y, -.15f, .85f) && CorridorCheckpoint.Number(p.z, 473.5f, 528.5f);
        public void Validate()
        {
            if (version != 2 || !Guid.TryParseExact(token, "N", out _) || phase < 0 || phase > 5 ||
                !CorridorCheckpoint.Number(seconds, 0, 1000000000) || !FlashlightChargeRules.Valid(flashlightCharge) ||
                flashlightPacks < 0 || flashlightPacks > 128 || flashlightDepletions < 0 || flashlightDepletions > 1000000 ||
                !CorridorCheckpoint.Number(prayerElapsed, 0, 4) || prayerElapsed >= 4 ||
                phase != 1 && phase != 4 && prayerElapsed != 0 || gateVisible && phase != 5 ||
                (phase == 0 || phase == 3) && incensePlaced || (phase == 2 || phase == 5) && !incensePlaced ||
                !incensePlaced && prayerElapsed != 0 || player == null)
                throw new ArgumentException("Invalid temple checkpoint");
            player.ValidateTemple();
            if (flashlightCharge == 0 && player.light) throw new ArgumentException("Empty flashlight cannot be lit");
        }
        public string ToJson() { Validate(); return JsonUtility.ToJson(this, true); }
        public static TemplePrologueCheckpoint FromJson(string json)
        {
            var data = JsonUtility.FromJson<TemplePrologueCheckpoint>(json);
            if (data == null) throw new ArgumentException("Empty temple checkpoint");
            if (data.version == 1)
            {
                // Version 1 started its prayer clock together with the hand action.
                // The old authored marker was at .26 seconds; resume an unfinished
                // placement from its beginning instead of guessing a hand pose.
                bool praying = data.phase == 1 || data.phase == 4;
                data.incensePlaced = data.phase == 2 || data.phase == 5 || praying && data.prayerElapsed >= .26f;
                if (praying && !data.incensePlaced) data.prayerElapsed = 0;
                data.version = 2;
            }
            data.Validate(); return data;
        }
        public TemplePrologueCheckpoint Copy() => FromJson(ToJson());
    }
}
