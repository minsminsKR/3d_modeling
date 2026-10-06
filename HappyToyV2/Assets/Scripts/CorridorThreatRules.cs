namespace HappyToy.V2
{
    public enum CorridorThreatRole { Authored, Watchman, Listener, Tracker, Wanderer }

    // Roles change the evidence an actor can gather, never grant knowledge of
    // an unseen player's location. Authored preserves the original school AI.
    public readonly struct CorridorThreatRules
    {
        public readonly float SightRange, SightCone, HearingScale, ChaseMemory, SearchSeconds, PatrolDwell;
        CorridorThreatRules(float sight, float cone, float hearing, float memory, float search, float dwell)
        { SightRange = sight; SightCone = cone; HearingScale = hearing; ChaseMemory = memory; SearchSeconds = search; PatrolDwell = dwell; }

        public static CorridorThreatRules For(CorridorThreatRole role)
        {
            switch (role)
            {
                case CorridorThreatRole.Watchman: return new CorridorThreatRules(18, 75, 1, 5, 6.5f, 0);
                case CorridorThreatRole.Listener: return new CorridorThreatRules(8, 65, 1.8f, 5, 6.5f, 0);
                case CorridorThreatRole.Tracker: return new CorridorThreatRules(14, 65, 1, 8, 10, 0);
                case CorridorThreatRole.Wanderer: return new CorridorThreatRules(14, 65, 1, 5, 6.5f, 1.8f);
                default: return new CorridorThreatRules(14, 65, 1, 5, 6.5f, 0);
            }
        }
        public static string Counterplay(CorridorThreatRole role)
        {
            switch (role)
            {
                case CorridorThreatRole.Watchman: return "사이클롭스는 긴 직선과 넓은 각도를 살핍니다. 불을 끄고 낮게 움직이며 모퉁이 뒤로 시야를 끊으세요.";
                case CorridorThreatRole.Listener: return "언캣은 먼 발소리까지 듣습니다. 낮은 자세로 이동하고, 들키기 전에 폭죽으로 다른 길을 조사하게 하세요.";
                case CorridorThreatRole.Tracker: return "화캣은 마지막으로 본 곳을 오래 수색합니다. 추격 중 캐비닛 진입은 생존 75% / 사망 25%이므로, 추격을 끊고 조용히 들어가세요.";
                case CorridorThreatRole.Wanderer: return "베이비는 순찰 중 멈춰 주변을 둘러봅니다. 멈췄다고 안심하지 말고 얼굴이 향하는 쪽을 확인하세요.";
                default: return "";
            }
        }
    }
}
