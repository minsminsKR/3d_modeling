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
        // A missing extension means legacy observations are unknown, not read.
        // Version 1 has only read flags; version 2 also persists legacy exemptions.
        public int storyVersion, legacyClueExemptMask;
        public bool firstClueRead, secondClueRead;
        // Version 3 is used only by the opt-in walking gate review.
        public bool reviewGateWalk, reviewGateStarted;
        public float reviewGateProgress;
        public Vector3 reviewPreviousPosition;
        // Version 4 contains a pre-temple approach snapshot. Versions 2/3 remain unchanged.
        public bool approachActive, approachAcquired, approachCleared, approachBypassed;
        public PlayerMotor.Progress player;
        public static bool ApproachPoint(Vector3 p) => CorridorCheckpoint.Number(p.x, 488f, 512f) &&
            CorridorCheckpoint.Number(p.y, -.15f, 4f) && CorridorCheckpoint.Number(p.z, 385f, 474.5f);

        public static bool Point(Vector3 p) => CorridorCheckpoint.Number(p.x, 478.5f, 521.5f) &&
            CorridorCheckpoint.Number(p.y, -.15f, .85f) && CorridorCheckpoint.Number(p.z, 473.5f, 528.5f);
        public void Validate()
        {
            if ((version != 2 && version != 3 && version != 4) || !Guid.TryParseExact(token, "N", out _) || phase < 0 || phase > 5 ||
                !CorridorCheckpoint.Number(seconds, 0, 1000000000) || !FlashlightChargeRules.Valid(flashlightCharge) ||
                flashlightPacks < 0 || flashlightPacks > 128 || flashlightDepletions < 0 || flashlightDepletions > 1000000 ||
                !CorridorCheckpoint.Number(prayerElapsed, 0, 4) || prayerElapsed >= 4 ||
                phase != 1 && phase != 4 && prayerElapsed != 0 || gateVisible && phase != 5 ||
                (phase == 0 || phase == 3) && incensePlaced || (phase == 2 || phase == 5) && !incensePlaced ||
                !incensePlaced && prayerElapsed != 0 || player == null ||
                storyVersion < 0 || storyVersion > 2 || legacyClueExemptMask < 0 || legacyClueExemptMask > 3 ||
                storyVersion == 0 && (firstClueRead || secondClueRead || legacyClueExemptMask != 0) ||
                storyVersion == 1 && (legacyClueExemptMask != 0 || secondClueRead && !firstClueRead ||
                    phase >= 1 && !firstClueRead || phase >= 4 && !secondClueRead) ||
                storyVersion == 2 && (firstClueRead && (legacyClueExemptMask & 1) != 0 ||
                    secondClueRead && (legacyClueExemptMask & 2) != 0 ||
                    legacyClueExemptMask == 1 && !secondClueRead ||
                    legacyClueExemptMask == 2 && !firstClueRead ||
                    secondClueRead && !firstClueRead && (legacyClueExemptMask & 1) == 0 ||
                    phase >= 1 && !firstClueRead && (legacyClueExemptMask & 1) == 0 ||
                    phase >= 4 && !secondClueRead && (legacyClueExemptMask & 2) == 0) ||
                version == 2 && (reviewGateWalk || reviewGateStarted || reviewGateProgress != 0 || reviewPreviousPosition != Vector3.zero) ||
                version == 4 && (!approachActive || phase != 0 || reviewGateWalk || reviewGateStarted || reviewGateProgress != 0 || reviewPreviousPosition != Vector3.zero ||
                    approachCleared && !approachAcquired && !approachBypassed || approachBypassed && !approachCleared || player != null &&
                    (player.snapperStockVersion != 1 || !approachAcquired && player.stock != 0 ||
                     !approachCleared && player.position.z > 405.5f)) ||
                version != 4 && (approachActive || approachAcquired || approachCleared || approachBypassed) ||
                version == 3 && (!reviewGateWalk || !Point(reviewPreviousPosition) ||
                    !CorridorCheckpoint.Number(reviewGateProgress, 0, 1) ||
                    !reviewGateStarted && reviewGateProgress != 0 ||
                    reviewGateStarted && (phase != 5 || !gateVisible)))
                throw new ArgumentException("Invalid temple checkpoint");
            if (version == 4) player.ValidateApproach(); else player.ValidateTemple();
            if (flashlightCharge == 0 && player.light) throw new ArgumentException("Empty flashlight cannot be lit");
        }
        public string ToJson()
        {
            Validate();
            string json = JsonUtility.ToJson(this, true);
            if (version != 4)
            {
                json = CheckpointJsonFields.Omit(json, "approachActive");
                json = CheckpointJsonFields.Omit(json, "approachAcquired");
                json = CheckpointJsonFields.Omit(json, "approachCleared");
                json = CheckpointJsonFields.Omit(json, "approachBypassed");
            }
            if (storyVersion < 2)
                json = CheckpointJsonFields.Omit(json, "legacyClueExemptMask");
            if (storyVersion == 0)
            {
                // An explicitly constructed legacy object must serialize like
                // a real old file, with the extension entirely absent.
                json = CheckpointJsonFields.Omit(json, "storyVersion");
                json = CheckpointJsonFields.Omit(json, "firstClueRead");
                json = CheckpointJsonFields.Omit(json, "secondClueRead");
            }
            return json;
        }
        public static TemplePrologueCheckpoint FromJson(string json)
        {
            var data = JsonUtility.FromJson<TemplePrologueCheckpoint>(json);
            if (data == null) throw new ArgumentException("Empty temple checkpoint");
            // Missing extensions identify old saves; explicit null, duplicate,
            // partial, or unsupported extensions are damaged data.
            bool hasVersion = CheckpointJsonFields.Omit(json, "storyVersion") != json;
            bool hasFirst = CheckpointJsonFields.Omit(json, "firstClueRead") != json;
            bool hasSecond = CheckpointJsonFields.Omit(json, "secondClueRead") != json;
            bool hasMask = CheckpointJsonFields.Omit(json, "legacyClueExemptMask") != json;
            if (!hasVersion && (hasFirst || hasSecond || hasMask) ||
                hasVersion && (!hasFirst || !hasSecond ||
                    CheckpointJsonFields.AbsentOrNull(json, "storyVersion") ||
                    CheckpointJsonFields.AbsentOrNull(json, "firstClueRead") ||
                    CheckpointJsonFields.AbsentOrNull(json, "secondClueRead") ||
                    data.storyVersion == 1 && hasMask ||
                    data.storyVersion == 2 && (!hasMask || CheckpointJsonFields.AbsentOrNull(json, "legacyClueExemptMask")) ||
                    data.storyVersion < 1 || data.storyVersion > 2))
                throw new ArgumentException("Incomplete or invalid temple clue state");
            bool hasApproach = CheckpointJsonFields.Omit(json, "approachActive") != json;
            bool hasAcquired = CheckpointJsonFields.Omit(json, "approachAcquired") != json;
            bool hasCleared = CheckpointJsonFields.Omit(json, "approachCleared") != json;
            bool hasBypassed = CheckpointJsonFields.Omit(json, "approachBypassed") != json;
            if (data.version == 4 && (!hasApproach || !hasAcquired || !hasCleared || !hasBypassed ||
                    CheckpointJsonFields.AbsentOrNull(json, "approachActive") ||
                    CheckpointJsonFields.AbsentOrNull(json, "approachAcquired") ||
                    CheckpointJsonFields.AbsentOrNull(json, "approachCleared") ||
                    CheckpointJsonFields.AbsentOrNull(json, "approachBypassed")) ||
                data.version != 4 && (hasApproach || hasAcquired || hasCleared || hasBypassed) &&
                    (!(hasApproach && hasAcquired && hasCleared && hasBypassed) ||
                     CheckpointJsonFields.AbsentOrNull(json, "approachActive") ||
                     CheckpointJsonFields.AbsentOrNull(json, "approachAcquired") ||
                     CheckpointJsonFields.AbsentOrNull(json, "approachCleared") ||
                     CheckpointJsonFields.AbsentOrNull(json, "approachBypassed")))
                throw new ArgumentException("Incomplete approach checkpoint");
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
            if (!hasVersion)
            {
                // Old runs had no observation fields. Exempt their remaining
                // requirements without claiming either note was actually read.
                data.storyVersion = 2;
                data.firstClueRead = false; data.secondClueRead = false;
                data.legacyClueExemptMask = 3;
            }
            data.Validate(); return data;
        }
        public TemplePrologueCheckpoint Copy() => FromJson(ToJson());
    }
}
