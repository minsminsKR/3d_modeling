namespace HappyToy.V2
{
    // Preserve the historical progress-2/3 cohort, resolving the live game mode first.
    // These cohorts do not assert that an enemy is currently chasing the player.
    public static class PerformanceProgressPhase
    {
        public const string Definition="Calm/threat are progression cohorts: threat at progress 2 or 3, calm before 2 and after 3. School/corridor use recovered memories; authored legacy uses StoryStep. These labels do not measure live enemy pursuit.";
        public static int ResolveProgress(bool chapterMode,bool corridorMode,int recordsRecovered,int storyStep)
            =>chapterMode||corridorMode?recordsRecovered:storyStep;
        public static bool IsThreatProgress(int progress)=>progress>=2&&progress<4;
        public static int Progress(GameSession session)=>ResolveProgress(session.ChapterMode,session.CorridorMode,session.RecordsRecovered,session.StoryStep);
        public static bool IsThreat(GameSession session)=>IsThreatProgress(Progress(session));
        public static string Mode(GameSession session)=>session.ChapterMode?"school-memories":session.CorridorMode?"corridor-memories":"legacy-story";
    }
}
