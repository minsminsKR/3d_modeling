using System;
using System.Collections.Generic;

namespace HappyToy.V2
{
    [Serializable]
    public sealed class LightExplorationCheckpoint
    {
        public int version = 1, packsCollected, depletions;
        public float charge;
        public Battery[] batteries;
        public Candle[] candles;
        [Serializable] public sealed class Battery { public string id; public bool available; }
        [Serializable] public sealed class Candle { public string id; public bool lit; }
        // Unity can materialize an absent nested class as an empty object. Only that
        // representation may accompany legacy marker 0; substantive new state cannot.
        public bool LegacyEmpty => version>=0 && version<=1 && charge==0 && packsCollected==0 && depletions==0 &&
            (batteries==null || batteries.Length==0) && (candles==null || candles.Length==0);
        public void Validate()
        {
            if (version != 1 || !FlashlightChargeRules.Valid(charge) || packsCollected < 0 || packsCollected > 128 ||
                depletions < 0 || depletions > 1000000 || batteries == null || batteries.Length < 1 || batteries.Length > 128 ||
                candles == null || candles.Length < 1 || candles.Length > 128)
                throw new ArgumentException("Invalid lighting checkpoint");
            var ids = new HashSet<string>(); int consumed = 0;
            foreach (var battery in batteries)
            {
                if (battery == null || string.IsNullOrEmpty(battery.id) || battery.id.Length > 40 || !ids.Add(battery.id))
                    throw new ArgumentException("Invalid battery identity");
                if (!battery.available) consumed++;
            }
            if (consumed != packsCollected) throw new ArgumentException("Battery stock and collected packs disagree");
            ids.Clear();
            foreach (var candle in candles)
                if (candle == null || string.IsNullOrEmpty(candle.id) || candle.id.Length > 40 || !ids.Add(candle.id))
                    throw new ArgumentException("Invalid candle identity");
        }
    }
}
