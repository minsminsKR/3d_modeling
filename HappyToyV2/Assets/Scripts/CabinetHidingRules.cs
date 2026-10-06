using System;

namespace HappyToy.V2
{
    public enum CabinetHidingOutcome { None, Quiet, Survived, Defeated }

    // One player-owned decision per successful entry, shared by every pursuer.
    public static class CabinetHidingRules
    {
        public const float SurvivalChance = .75f;
        public const string RiskExplanation = "추격 중 캐비닛 진입은 한 번만 판정합니다: 생존 75% / 사망 25%. 추격이 끝난 뒤 조용히 들어가면 이 위험 판정을 하지 않습니다.";
        public static void Validate(int entry, int rolls, CabinetHidingOutcome outcome)
        {
            if (entry < 0 || rolls < 0 || rolls > entry || !Enum.IsDefined(typeof(CabinetHidingOutcome), outcome) ||
                (entry == 0) != (outcome == CabinetHidingOutcome.None) ||
                (outcome == CabinetHidingOutcome.Survived || outcome == CabinetHidingOutcome.Defeated) && rolls == 0)
                throw new ArgumentException("Invalid cabinet entry decision");
        }
        public sealed class Decision
        {
            public int EntryId { get; private set; }
            public int Rolls { get; private set; }
            public CabinetHidingOutcome Outcome { get; private set; }
            public CabinetHidingOutcome Resolve(int entry, bool pursued, Func<float> sample)
            {
                // Repeated observers, frames and restored calls keep the original outcome.
                if (entry > 0 && entry == EntryId) return Outcome;
                if (entry <= 0 || entry != EntryId + 1) throw new ArgumentException("Cabinet entry must advance once");
                var outcome = CabinetHidingOutcome.Quiet;
                if (pursued)
                {
                    if (sample == null) throw new ArgumentNullException(nameof(sample));
                    float value = sample();
                    if (float.IsNaN(value) || float.IsInfinity(value) || value < 0 || value > 1)
                        throw new ArgumentException("Cabinet random sample must be in [0,1]");
                    outcome = value < SurvivalChance ? CabinetHidingOutcome.Survived : CabinetHidingOutcome.Defeated;
                    Rolls++;
                }
                EntryId = entry; Outcome = outcome; return outcome;
            }
            public void Restore(int entry, int rolls, CabinetHidingOutcome outcome)
            { Validate(entry, rolls, outcome); EntryId = entry; Rolls = rolls; Outcome = outcome; }
        }
    }
}
