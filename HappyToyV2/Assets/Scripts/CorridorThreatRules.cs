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
                case CorridorThreatRole.Tracker: return "화캣은 마지막으로 본 곳을 오래 수색합니다. 캐비닛에 들어가는 모습을 보였다면 문 앞까지 따라올 수 있습니다. 공격 예고가 들리면 빠져나오세요.";
                case CorridorThreatRole.Wanderer: return "베이비는 울며 작은 소리를 조사합니다. 들킨 뒤에는 배회하며 큰 소리를 쫓으므로, 시야를 끊고 조용히 이동하세요.";
                default: return "";
            }
        }
    }
}
