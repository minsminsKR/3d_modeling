using System;
using System.Globalization;
using UnityEngine;

namespace HappyToy.V2
{
    public enum ChapterAction { FirecrackerThrown, HidingEntered, DoorClosed }

    // Optional in version-one school checkpoints. Old saves honestly track only
    // actions after continuation, rather than inventing earlier actions.
    [Serializable]
    public sealed class ChapterRunProgress
    {
        public string token;
        public bool complete;
        public int throws, hides, closedDoors;
        public float distance;
        public static ChapterRunProgress New(bool complete=true) => new ChapterRunProgress {
            token=Guid.NewGuid().ToString("N"),complete=complete };
        public void Validate()
        {
            if(!Guid.TryParseExact(token,"N",out _) || throws<0 || throws>1000000000 || hides<0 || hides>1000000000 ||
                closedDoors<0 || closedDoors>1000000000 || !CorridorCheckpoint.Number(distance,0,1000000000))
                throw new ArgumentException("Invalid school run metrics");
        }
        public ChapterRunProgress Copy() => JsonUtility.FromJson<ChapterRunProgress>(JsonUtility.ToJson(this));
    }

    [Serializable]
    public sealed class ChapterRunReport
    {
        public string token, endedUtc, defeatSource;
        public bool escaped, actionsComplete;
        public int recovered, firecrackersRemaining, throws, hides, closedDoors;
        public float seconds, staminaRemaining, distance;
        public void Validate()
        {
            if(!Guid.TryParseExact(token,"N",out _) || !DateTimeOffset.TryParseExact(endedUtc,"O",CultureInfo.InvariantCulture,DateTimeStyles.None,out _) ||
                defeatSource==null || defeatSource.Length>80 || recovered<0 || recovered>5 || escaped && recovered!=5 ||
                firecrackersRemaining<0 || firecrackersRemaining>FirecrackerInventory.Capacity ||
                !CorridorCheckpoint.Number(seconds,0,1000000000) || !CorridorCheckpoint.Number(staminaRemaining,0,1))
                throw new ArgumentException("Invalid finished school run");
            new ChapterRunProgress {token=token,complete=actionsComplete,throws=throws,hides=hides,closedDoors=closedDoors,distance=distance}.Validate();
        }
        public ChapterRunReport Copy() => JsonUtility.FromJson<ChapterRunReport>(JsonUtility.ToJson(this));
    }
}
