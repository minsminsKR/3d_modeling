using System;

namespace HappyToy.V2
{
    public sealed partial class LightExplorationRun
    {
        bool observationSchool;
        public void PrepareSchoolLoop(MemoryChapter chapter)
        {
            Prepare(chapter.transform,GameSession.Current.player,false);
            observationSchool=true;
        }
    }
    public sealed partial class LightExplorationCheckpoint
    {
        // Explicit stationless schema. Version 1 still requires all its real
        // battery/candle identities; legacy runs are never loosened to empty.
        void ValidateObservationSchool()
        {
            if(version!=2||!FlashlightChargeRules.Valid(charge)||packsCollected!=0||depletions<0||depletions>1000000||
                batteries==null||batteries.Length!=0||candles==null||candles.Length!=0)
                throw new ArgumentException("Invalid observation school lighting checkpoint");
        }
    }
}
