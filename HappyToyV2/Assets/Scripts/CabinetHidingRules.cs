using System;

namespace HappyToy.V2
{
    // Defeated is the legacy save name for a vulnerable entry, not an instant death.
    public enum CabinetHidingOutcome { None, Quiet, Survived, Defeated }

    // One player-owned decision per successful entry, shared by every pursuer.
    public static class CabinetHidingRules
    {
        public const float SurvivalChance = .75f;
        public const string RiskExplanation = "추격 중 캐비닛 진입은 한 번만 판정합니다: 은신 성공 75% / 발각 위험 25%. 들어가는 모습을 본 적이 실제 문 앞에 도착해 공격해야 붙잡힙니다. 문 앞의 공격 예고가 들리면 빠져나오세요.";
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
