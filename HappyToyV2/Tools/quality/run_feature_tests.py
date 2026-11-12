#!/usr/bin/env python3
"""Route changes to fast rules, focused scene tests, or both complete Unity suites.

This runner never builds a player or substitutes these tests for visual/native
release verification. --dry-run prints the exact commands without starting Unity.
"""
from __future__ import annotations

import argparse
from datetime import datetime
import json
import math
import statistics
from pathlib import Path
import re
import shutil
import subprocess
import sys
import uuid
import xml.etree.ElementTree as ET

PROJECT = Path(__file__).resolve().parents[2]
VERSION = "6000.6.0f1"
CORE_FIXTURES = tuple("HappyToy.V2.CoreTests." + name for name in (
    "TemplePrologueClueStateTests", "EnemyAttackClockTests", "StealthRulesTests", "SceneRestartGateTests", "LoadingProgressTests", "FrameRateCounterTests", "HandActionTimelineTests", "HandMotionStateTests",
    "SurfaceRippleBufferTests", "CabinetHidingRulesTests", "CabinetEncounterClockTests", "CabinetTauntMemoryTests", "SnapperStockRulesTests",
    "CorridorBabyMemoryTests", "CorridorThreatRulesTests", "EnemyBellClockTests", "CorridorShelterRulesTests", "CorridorRouteRulesTests", "MonsterPassingRulesTests", "TrapHoldClockTests", "WraithRetreatClockTests", "RitualItemRulesTests", "LadyFinaleClockTests", "LadyHeartbeatClockTests", "LadySnapperFlinchTests", "PatrolProgressClockTests"))
CLOUD = "HappyToy.V2.CloudTests."
EDIT = "HappyToy.V2.Tests.EditMode."
PLAY = CLOUD + "CloudPlayModeTests."


def play_tests(*files: str) -> tuple[str, ...]:
    """The legacy PlayMode files share one partial fixture; route its real methods."""
    selectors = []
    for file in files:
        text = (PROJECT / "Assets/Tests/PlayMode" / file).read_text(encoding="utf-8-sig")
        if not re.search(r"\bpartial\s+class\s+CloudPlayModeTests\b", text):
            raise ValueError("Update the feature route after changing the fixture: " + file)
        methods = re.findall(r"\[(?:UnityTest|Test)(?:[^\]]*)\]\s*public\s+(?:IEnumerator|void)\s+(\w+)\s*\(", text)
        if not methods:
            raise ValueError("No real test methods in feature route: " + file)
        selectors.extend(PLAY + method for method in methods)
    return tuple(selectors)

# Exact fixture/method names, rather than fuzzy substrings. Each required selector
# must appear in fresh NUnit results, so a renamed or mistyped test cannot pass.
PROFILES = {
    "hud-interaction-logic": ("Actual keyboard/gamepad prompts, accessible story/evidence layout and pause; CPU only", [
        ("PlayMode", play_tests("CloudHudPresentationTests.cs"))]),
    "temple-stone-layout-logic": ("Unconnected candidate stone layout deterministic bounds, spacing, exclusions and work limits; CPU only", [
        ("EditMode", ("HappyToy.V2.CoreTests.TempleStoneLayoutRulesTests",))]),
    "temple-concept-environment-logic": ("Temple path, shrine bindings and mist lifecycle; CPU only", [
        ("PlayMode", play_tests("TempleForestPathTests.cs", "TempleShrineDressingTests.cs", "TempleForestAtmosphereTests.cs", "TempleForestBanksTests.cs"))]),
    "temple-offerings-logic": ("Corridor-style altar candles, guardian size and existing interaction; CPU only", [
        ("PlayMode", (PLAY + "TemplePrayerFurnishingsPreserveInteractionAndPhysics", PLAY + "TempleForestOfferingsUseAuthoredVisualsWithoutBlockingThePath"))]),
    "temple-altar-logic": ("Altar binding, existing prayer interaction and resource cleanup; CPU only", [
        ("PlayMode", (PLAY + "TempleAltarBindingReusesPaperWithoutChangingInteractionOrLeavingResources",
                      PLAY + "TemplePrayerFurnishingsPreserveInteractionAndPhysics"))]),
    "title-tremor-presentation": ("Offscreen title head and body motion, planted supports and reduced motion", [
        ("PlayMode", (PLAY + "BaeksugwiTitleHeadAndBodyTrembleAtZeroTimeScaleWithSupportsPlanted",))]),
    "temple-prologue-logic": ("Prayer hold, timed completion and phase transition; no scene or visual review", [
        ("PlayMode", play_tests("TemplePrologueDirectorTests.cs"))]),
    "temple-soil-logic": ("Production Repeat soil and imported edge checks; CPU only", [
        ("PlayMode", (PLAY + "TempleForestEarthCandidateKeepsScaleAndNormalAligned", PLAY + "TempleForestSoilRepeatsWithoutHardSeams"))]),
    "temple-soil-original-logic": ("Diagnostic original Mirror soil settings; CPU only", [
        ("PlayMode", (PLAY + "TempleForestEarthCandidateKeepsScaleAndNormalAligned", PLAY + "TempleForestSoilRepeatsWithoutHardSeams"))]),
    # Retained profile name for existing workflows; the formerly opt-in Repeat soil is now production.
    "temple-soil-candidate-presentation": ("Production Repeat soil material and camera seam review; no desktop input or native FPS claim", [
        ("PlayMode", (PLAY + "TempleForestEarthCandidateKeepsScaleAndNormalAligned", PLAY + "TempleForestSoilMaterialOffscreenReview", PLAY + "TempleForestRepeatSeamOffscreenReview"))]),
    "temple-soil-original-presentation": ("Diagnostic original Mirror soil material comparison; no desktop input or native FPS claim", [
        ("PlayMode", (PLAY + "TempleForestEarthCandidateKeepsScaleAndNormalAligned", PLAY + "TempleForestSoilMaterialOffscreenReview"))]),
    "temple-soil-presentation": ("Opt-in offscreen actual production soil material comparison; no desktop input or native FPS claim", [
        ("PlayMode", (PLAY + "TempleForestSoilMaterialOffscreenReview",))]),
    "temple-altar-soil-cost-presentation": ("Opt-in offscreen ABBA altar vapor and soil cost; not displayed FPS", [
        ("PlayMode", (PLAY + "TempleAltarSoilCostOffscreenAbbaReview",))]),
    "temple-night-sky-presentation": ("Opt-in original/default and cloud/halo sky camera comparison; no desktop input or displayed FPS claim", [
        ("PlayMode", (PLAY + "TempleNightSkyOffscreenReview",))]),
    "temple-facade-color-presentation": ("Opt-in frozen offscreen facade-color ABBA review; no desktop input or displayed FPS claim", [
        ("PlayMode", (PLAY + "TempleFacadeColorOffscreenAbbaReview",))]),
    "temple-candle-presentation": ("Opt-in candle light and existing-light contribution comparison; no desktop input or displayed FPS claim", [
        ("PlayMode", (PLAY + "TempleCandleLightOffscreenReview",))]),
    "temple-altar-vapor-presentation": ("Opt-in offscreen altar vapor on/off and pause baseline; no desktop input or native FPS claim", [
        ("PlayMode", (PLAY + "TempleAltarVaporProductionCameraBaseline",))]),
    "temple-story-ui-presentation": ("Opt-in offscreen actual temple camera and evidence reading HUD, normal/large text at 16:9, 4:3 and 21:9; no desktop input or native FPS claim", [
        ("PlayMode", (PLAY + "TempleStoryEvidenceUiOffscreenAspectReview", PLAY + "TemplePhysicalFocusHudOffscreenReview", PLAY + "TempleForestSoilMaterialOffscreenReview"))]),
    "temple-horizon-logic": ("Visual terrain continuity, backdrop tree placement and safe cleanup; CPU only", [
        ("PlayMode", (PLAY + "TempleForestHorizonExtendsAllFourSidesWithoutCollision",))]),
    "corridor-story-logic": ("First memory story, real temple-to-corridor entry, journal and old/new checkpoint restoration; CPU only", [
        ("PlayMode", play_tests("CloudCorridorStoryTests.cs"))]),
    "temple-approach-logic": ("Alley pickup and first-person throw, new-run approach and saved progress protection", [
        ("PlayMode", play_tests("TempleApproachFlowTests.cs"))]),
    "temple-approach-route": ("Real title, alley input, physical snapper contact, mountain walking and temple arrival", [
        ("PlayMode", play_tests("TempleApproachActualRouteTests.cs"))]),
    "temple-approach-streaming": ("Additive scene lifecycle, continuous mountain seam and environment ownership; CPU only", [
        ("PlayMode", (CLOUD + "ApproachSceneStreamTests", CLOUD + "MountainForkSafetyTests", CLOUD + "ConnectorSafetyTests") + play_tests("TempleApproachStreamingEnvironmentTests.cs", "TempleApproachAudioTests.cs"))]),
    "temple-approach-visual": ("Opt-in real route gameplay camera snapshots", [
        ("PlayMode", (PLAY + "TempleApproachRealTitleKeysAndCollisionReachTemple",))]),
    "temple-approach-seam-visual": ("Opt-in focused real-input village-to-mountain seam camera", [
        ("PlayMode", (PLAY + "TempleApproachFocusedVillageMountainSeam",))]),
    "temple-approach-village-visual": ("Opt-in focused village wall and roof gameplay-camera snapshots", [
        ("PlayMode", (PLAY + "TempleApproachVillageVisualReview",))]),
    "temple-approach-horizon-logic": ("Village horizon clearance, mountain ground and legacy restoration", [
        ("PlayMode", (PLAY + "ApproachClearsHorizonAndTreesWithoutChangingLegacyTemple",))]),
    "temple-approach-existing": ("Prior temple walking, save and title integration after new approach", [
        ("PlayMode", tuple(PLAY + name for name in (
            "TempleFullWalkingRouteUsesGamepadWithoutTeleporting", "TempleFullWalkingRouteUsesKeyboardAndMouseWithoutTeleporting",
            "TempleRealTitleAndInteractionReachCorridor", "TempleSaveRejectsConcurrentCorruptionWithoutOverwriting",
            "TempleSuspendDuringPrayerRestoresOnce")))]),
    "temple-prologue-flow": ("Actual title, two prayers, hold, next day, gate and corridor transition; CPU/physics only", [
        ("PlayMode", play_tests("TemplePrologueFlowTests.cs", "TempleStoryNarrationTests.cs", "TempleClueCollisionTests.cs", "TempleForestPathTests.cs", "TempleIndividualStoneDefaultPathTests.cs", "TempleStonePathCandidateTests.cs", "TempleForestAtmosphereTests.cs", "TempleForestDressingTests.cs", "TempleForestBanksTests.cs", "TempleForestPresenceTests.cs", "TempleForestTreeTests.cs", "TemplePineBranchesCandidateTests.cs", "TemplePineFarCandidateTests.cs", "TempleWideTreeTests.cs", "TempleForestPuddlesTests.cs", "TempleForestFernsTests.cs", "TempleForestUndergrowthTests.cs", "TempleForestBackgroundTreesTests.cs", "TempleForestDepthCandidateTests.cs", "TempleShrineDressingTests.cs", "TempleStoneLanternsTests.cs", "TempleHallPresentationTests.cs", "TempleIncenseVesselTests.cs") + (CLOUD + "TempleStoryEvidenceTests", PLAY + "PhysicalSprintChargesTravelAndRecoversAtObstaclesWithoutInputLatch",))]),
    "temple-forest-environment": ("Forest art, path bounds, background tree collision and older checkpoint clearance; CPU/physics only", [
        ("PlayMode", play_tests("TempleForestPathTests.cs", "TempleIndividualStoneDefaultPathTests.cs", "TempleStonePathCandidateTests.cs", "TempleForestAtmosphereTests.cs", "TempleForestDressingTests.cs", "TempleForestBanksTests.cs", "TempleForestPresenceTests.cs", "TempleForestTreeTests.cs", "TemplePineBranchesCandidateTests.cs", "TemplePineFarCandidateTests.cs", "TempleWideTreeTests.cs", "TempleForestPuddlesTests.cs", "TempleForestFernsTests.cs", "TempleForestUndergrowthTests.cs", "TempleForestBackgroundTreesTests.cs", "TempleForestDepthCandidateTests.cs", "TempleShrineDressingTests.cs", "TempleStoneLanternsTests.cs", "TempleHallPresentationTests.cs", "TempleIncenseVesselTests.cs") + (PLAY + "TempleProductionForestKeepsTrunkCapsulesAndReviewedPineLods", PLAY + "TempleForestSoilRepeatsWithoutHardSeams"))]),
    "temple-prologue-regressions": ("Title keyboard entry, replacing a prepared mode and existing school/corridor checkpoint preservation; CPU only", [
        ("EditMode", (CLOUD + "CorridorCheckpointStoreTests", CLOUD + "ChapterCheckpointStoreTests")),
        ("PlayMode", (PLAY + "MainCorridorFocusedEnterBeginsOnceThroughRealKeyboardInput",
                      PLAY + "MainCorridorSelectionReplacesPreparedTitleSchoolThroughRealReload",
                      PLAY + "CorridorCheckpointRejectsUnknownVersionAndBlockedPoseBeforeMutatingWorld",
                      PLAY + "SchoolCheckpointRejectsCorruptGeometryBeforeMutatingFreshChapter",
                      PLAY + "SavedSlotIsConsumedWhenContinuingAfterSceneReloadFailure",
                      PLAY + "ExternalCatalogContainsImportedRecordedCuesAndIndependentOwnedCopies"))]),
    "temple-prologue-restore": ("Temple pause/save restoration, legacy migration and opt-in walking gate checkpoints; CPU only", [
        ("PlayMode", (PLAY + "TempleClueCheckpointMigratesLegacyAndRejectsInvalidProgress",
                      PLAY + "TempleBoundaryContactsRemainSaveableAndRestorable",
                      PLAY + "TempleSameTokenExternalEditIsPreservedBeforeSaveAndContinue",
                      PLAY + "TempleObstructedResumePreservesPlayerSelectionAndEnvironment",
                      PLAY + "TempleObstructedContinueKeepsSaveAndShowsErrorOnTitle",
                      PLAY + "TempleSuspendDuringPrayerRestoresOnce", PLAY + "TempleVersionOneCheckpointMigratesIncensePlacement",
                      PLAY + "TempleAllSavedPhasesRestoreTheirWorldAndVisibleGate", PLAY + "TempleWalkReviewCheckpointValidatesAndRestoresBeforeOpening",
                      PLAY + "TempleWalkReviewPreservesCrossingInterruptedByPause", PLAY + "TempleRealTitleAndInteractionReachCorridor") + play_tests("TempleClueCollisionTests.cs"))]),
    "hand-animation-logic": ("Authored hand rig and grips, prepare/release/recovery, no early stock spend, pause/wheel/cancel, physical throw and fixed trap placement; CPU/headless scene evidence only, no pixel or FPS certification", [
        ("EditMode", (CLOUD + "HandAnimationAssetTests",)),
        ("PlayMode", play_tests("HandActionIntegrationTests.cs") + (PLAY + "SelectedHandsWrapFlashlightPinchPaperAndSupportSuppliesOnThePalm",))]),
    "snapper-live-review": ("Opt-in actual gameplay snapper action camera capture and release integration", [
        ("PlayMode", (PLAY + "HandTimelineCommitsOneSnapperAtVisibleReleaseAndHoldsDuringPauseAndWheel",))]),
    "trap-live-review": ("Opt-in actual gameplay trap placement camera capture and release integration", [
        ("PlayMode", (PLAY + "HandTrapPlacementOnlyArmsAtFloorReleaseAndRejectsBlockedCommit",))]),
    "item-wheel": ("Real quarter-time movement measured over simulation time, input isolation, selection, fresh press, pause and focus", [
        ("PlayMode", (PLAY + "ItemWheelTabSlowsTimeSelectsOnReleaseAndRequiresFreshUsePress",))]),
    "player-hands": ("Retained original hand/skin, authored articulated forearm and grips, real throw/pause and supported rendered camera aspects", [
        ("PlayMode", play_tests("PlayerHandsTests.cs") + (
            PLAY + "ItemWheelTabSlowsTimeSelectsOnReleaseAndRequiresFreshUsePress",
            PLAY + "LadyBlackoutKeepsOnlyPlayerIncenseLightAndVisibleGuidingSmokeUntilBurnout"))]),
    "corridor-roster": ("Four-memory current corridor without Baby, three staged threats, fourth-memory Lady, real altar/school transition, current UI and legacy checkpoint/record compatibility", [
        ("EditMode", (EDIT + "CorridorLayoutTests", CLOUD + "CorridorSectorPlanTests",
                      CLOUD + "CorridorCheckpointStoreTests", EDIT + "CorridorRecordsTests", CLOUD + "CheckpointTrapCodecTests")),
        ("PlayMode", play_tests("CloudCorridorStoryTests.cs", "CorridorRosterTests.cs", "CloudCorridorTests.cs", "CloudCorridorThreatProgressionTests.cs",
                                "CloudCorridorRecordsTests.cs", "CloudMainCorridorFlowTests.cs", "LadyEncounterTests.cs",
                                "LadyFinaleLightingTests.cs", "LadyItemResponseTests.cs", "LadyHeartbeatTests.cs",
                                "RitualWorldTests.cs", "RitualPickupSupportTests.cs",
                                "CloudCreatureStudioTests.cs", "CloudStudioDistantVisualTests.cs",
                                "CloudHauntedCorridorPresentationTests.cs", "CloudCorridorRolesTests.cs",
                                "CloudCorridorAltarTests.cs", "CorridorEntranceChamberTests.cs") + (
            PLAY + "CorridorCheckpointJsonRestoresRealScenePickupsDoorsPlayerAndThreatEvidence",
            PLAY + "CorridorHistoricalFiveMemoryCheckpointResumesAndResavesWithoutRecreatingBaby",
            PLAY + "CorridorSectorsShowFourGoalIdentitiesAndRealBranchesWithoutChangingPhysics"))]),
    "school-brightness": ("Normal school camera-stack brightness against the approved preview, without a diagnostic-only exposure", [
        ("PlayMode", (PLAY + "SchoolNormalCameraStackKeepsTheApprovedPreviewBrightness",))]),
    "finale-start-audio": ("Three-second clinical terminal recording, native uninterrupted title contacts, visible approach before covered world preparation and shared in-game bank", [
        ("PlayMode", play_tests("LadyHeartbeatTests.cs") + (
            PLAY + "StartLoadingRealTitleButtonFinishesApproachBeforeBlackScreenPreparationAndGatesReveal",
            PLAY + "TitleApproachRecordedContactsReachTheMixerAndObeyMasterMute",
            PLAY + "TitleRecordedPerformanceKeepsEveryContactWithoutFurtherMainThreadTicks",
            PLAY + "MaskRunningContactsUseAnimalRecordingsReachTheMixerAndPause",
            PLAY + "LadyOpensPhysicalDoorAndFindsPlayerAfterUnwitnessedCabinetEntry"))]),
    "patrol-navigation": ("Physical patrol progress at real walls, all directed corners, native door/body clearance, deliberate waits/pause and talisman", [
        ("PlayMode", play_tests("CorridorNavigationRegressionTests.cs", "CloudMonsterDoorTraversalTests.cs", "RitualTimeStopTests.cs") + (
            PLAY + "LargeMonsterRenderedBodyClearsRealCorridorWallOnSupportedNavigation",
            PLAY + "TwoDoorShelterPursuersOpenCrossAndCloseBothPhysicalDoors",
            PLAY + "CorridorWandererPhysicallyDwellsLooksAndResumesPatrolAfterPause",
            PLAY + "CorridorPatrolCanLeaveItsPhysicalMemoryAltarWaypoint"))]),
    "cheats": ("F1/F2 real keys, infinite stamina/items/flashlight, preserved finite stock/checkpoints, OFF restoration and normal item use", [
        ("PlayMode", play_tests("CloudCheatTests.cs") + (
            PLAY + "PhysicalSprintChargesTravelAndRecoversAtObstaclesWithoutInputLatch",
            PLAY + "FlashlightBatteryPhysicalInputDepletionPauseOffFullCapAndRefill",
            PLAY + "TrapPlacementUsesSupportedGroundAndPausedInputNeverConsumesStock",
            PLAY + "IncenseKeepsOneMemoryUntilCollectedThenRetargetsOfferingAndFreezesOnPause",
            PLAY + "ItemWheelTabSlowsTimeSelectsOnReleaseAndRequiresFreshUsePress"))]),
    "wraith-footsteps": ("Shared title/game Baeksugwi recording bank, actual start transition, quiet mixer/master mute and running/pause", [
        ("PlayMode", (
            PLAY + "StartLoadingRealTitleButtonFinishesApproachBeforeBlackScreenPreparationAndGatesReveal",
            PLAY + "TitleApproachRecordedContactsReachTheMixerAndObeyMasterMute",
            PLAY + "TitleRecordedPerformanceKeepsEveryContactWithoutFurtherMainThreadTicks",
            PLAY + "MaskRunningContactsUseAnimalRecordingsReachTheMixerAndPause"))]),
    "model-polish": ("Core plus exact near-skin correspondence, native FBX pose equivalence and distant model budgets", [
        ("EditMode", (CLOUD + "CreatureGamePolishTests", CLOUD + "LadyAuthoredAssetTests")),
        ("PlayMode", (
            PLAY + "StudioDistantSkinsKeepTheOriginalEyeMaskAndFollowTheSameEmissionToggle",
            PLAY + "ModernThreeEnemiesUseTheirOwnClipsDeformAndGroundAtEverySampledPhase",
            PLAY + "CreatureStudioPursuitHasOffBeatHeadMotionAndRestoresCalmMotionAfterPause",
            PLAY + "NaturalEyeLifecycleRestoresCallerMaterialAndIndexedPropertiesAndHonoursLaterMaterialOwner",
            PLAY + "ActualRenderedSkinChoosesBakeCoordinateModeAtOriginalTransformScale"))]),
    "lady-polish": ("Core plus real Lady FBX-to-clip pose equivalence, original rig and reduced imported LOD budgets", [
        ("EditMode", (CLOUD + "LadyAuthoredAssetTests",))]),
    "lady": ("Generated Lady real model and irregular pose, final-memory blackout/frozen actors, unconditional navigation, trap immunity, talisman/pause, checkpoint migration and quiet actual mixer", [
        ("EditMode", tuple("HappyToy.V2.CoreTests." + name for name in (
            "LadyFinaleClockTests", "LadyHeartbeatClockTests", "LadySnapperFlinchTests", "RitualItemRulesTests", "TrapHoldClockTests")) +
            (CLOUD + "CorridorCheckpointStoreTests", CLOUD + "CheckpointTrapCodecTests")),
        ("PlayMode", play_tests("LadyEncounterTests.cs", "LadyFinaleLightingTests.cs", "LadyHeartbeatTests.cs", "LadyItemResponseTests.cs", "RitualTimeStopTests.cs") + (
            PLAY + "ExternalCatalogContainsImportedRecordedCuesAndIndependentOwnedCopies",
            PLAY + "EntranceStartCameraUsesSelectedLightingWithoutIncreasingTheShadowBudget",
            PLAY + "EntranceReflectionUsesActualCavernBoxAndPauseRetainsTheOwnedScope",
            PLAY + "CorridorCheckpointJsonRestoresRealScenePickupsDoorsPlayerAndThreatEvidence",
            PLAY + "CyclopseFaceFollowsChasePauseCalmAndDisableWithoutEditingSharedSkin",
            PLAY + "UncatFaceFollowsChasePauseCalmAndDisableWithoutEditingSharedSkin"))]),
    "entrance-lighting": ("Flame-motivated entrance lighting, no ceiling/ambient fill, actual camera/aspects, pause/comfort, reflections and preserved routes", [
        ("EditMode", (CLOUD + "GraphicsLightingRulesTests",)),
        ("PlayMode", play_tests("EntranceBrightnessTests.cs", "EntranceGuardianFirelightTests.cs") + (
            PLAY + "EntranceRitualArtKeepsSupportedFloorClearRouteAndReleasesOwnedResources",
            PLAY + "GeneratedBuddhistLanternsKeepPaintedInkSharedLodsAndAppearInActualCamera",
            PLAY + "SealedTempleGateRespondsToRealEAndPhysicallyStopsThePlayer",
            PLAY + "LadyFinaleRealCameraNeedsTorchAndRestartRestoresNormalLighting",
            PLAY + "CorridorGraphicsVolumeCameraCookieAndReflectionScopeRestoreAllOwnedState",
            PLAY + "SchoolGraphicsVolumeCameraCookieAndReflectionScopeRestoreAllOwnedState",
            PLAY + "EntranceRitualActualCameraShowsWetFloorSealsCandlesAndGuardiansAcrossAspectRatios",
            PLAY + "EntranceFloorStaysDarkWhenBackingAcrossTheCorridorMouth",
            PLAY + "EntranceReflectionUsesActualCavernBoxAndPauseRetainsTheOwnedScope"))]),
    "cyclopse-voice": ("Recorded breathing during patrol and pursuit, removed scream resources, quiet spatial mixer/pause/mute/time-stop and owned cleanup", [
        ("PlayMode", play_tests("CreatureBreathingAudioTests.cs") + (
            PLAY + "ExternalCatalogContainsImportedRecordedCuesAndIndependentOwnedCopies",))]),
    "corridor-feedback": ("Moving quarter-time item selection, recorded pursuit breathing, three-tick snappers, louder bells, actual animal contacts and brighter local entrance camera", [
        ("PlayMode", play_tests("RitualItemWheelTests.cs", "CreatureBreathingAudioTests.cs", "CorridorSoundFeedbackTests.cs", "EntranceBrightnessTests.cs") + (
            PLAY + "ExternalCatalogContainsImportedRecordedCuesAndIndependentOwnedCopies",
            PLAY + "UncatActualWalkingBellsAccelerateAfterRecognitionAndStopDuringPause",
            PLAY + "RealThrownFirecrackerUsesThreeRecordedTakesAndReleasesOnlyOwnedAudio",
            PLAY + "EntranceRitualActualCameraShowsWetFloorSealsCandlesAndGuardiansAcrossAspectRatios"))]),
    "supplies-voices": ("Supported finite ritual shelf/drawer supplies, version-one save migration, louder walking/pursuit bells and quiet recorded Cyclopse breathing lifecycle/mix", [
        ("EditMode", (CLOUD + "CorridorCheckpointStoreTests", CLOUD + "ChapterCheckpointStoreTests", CLOUD + "CheckpointTrapCodecTests")),
        ("PlayMode", play_tests("RitualWorldTests.cs", "RitualPickupSupportTests.cs", "RitualFurnitureSupplyTests.cs", "CreatureBreathingAudioTests.cs") + (
            PLAY + "UncatActualWalkingBellsAccelerateAfterRecognitionAndStopDuringPause",
            PLAY + "ExternalCatalogContainsImportedRecordedCuesAndIndependentOwnedCopies",
            PLAY + "CorridorDrawerRealERevealsFinitePickupAndRestoresConsumedStateWithNewAndLegacyCheckpoints",
            PLAY + "EmptyDrawersOpenThroughRealInputAndDenseHallCandlesKeepTheirSavedWaymarks",
            PLAY + "SchoolSuspendAndContinueRestoreRealTwoLeafDoorsPlayerMemoriesAndActorsOnce"))]),
    "lady-items": ("Actual blackout camera incense light/smoke, post-blackout ignition and burnout; real front/body snapper impacts, half-second pose/navigation hold, pause/talisman/slow time, cover and forecast regression", [
        ("PlayMode", play_tests("LadyItemResponseTests.cs", "LadyFinaleLightingTests.cs") + (
            PLAY + "IncenseKeepsOneMemoryUntilCollectedThenRetargetsOfferingAndFreezesOnPause",
            PLAY + "LadyAlwaysPursuesThroughDoorsIgnoresTrapAndTalismanHoldsActualHeadAndAgent",
            PLAY + "LadyOpensPhysicalDoorAndFindsPlayerAfterUnwitnessedCabinetEntry",
            PLAY + "FirecrackerFirstPopIsSingleAndComfortSettingsApplyWhilePaused",
            PLAY + "ForecastMatchesPhysicalFirstContactAtLowAndHighSimulatedFrameRates",
            PLAY + "AuthoredCameraRendersWorldAimAndStopsAtPhysicalWall",
            PLAY + "RealThrownFirecrackerUsesThreeRecordedTakesAndReleasesOnlyOwnedAudio"))]),
    "incense": ("Single stable nearest reachable memory, offering fallback, blackout ember/smoke, continuous plume pause, supported pickups and stock save compatibility", [
        ("PlayMode", play_tests("RitualWorldTests.cs", "RitualPickupSupportTests.cs") + (
            PLAY + "IncenseRibbonSurvivesSourceResetAndContinuesGrowing",
            PLAY + "LadyBlackoutKeepsOnlyPlayerIncenseLightAndVisibleGuidingSmokeUntilBurnout",))]),
    "ritual-pickups": ("Real support surfaces, rotated visual contact, above-surface focus targets and preserved pickup/save/presentation ownership", [
        ("PlayMode", play_tests("RitualPickupSupportTests.cs") + (
            PLAY + "RitualStocksFragmentsAndSuppliesRoundTripWithLegacyOptionalState",
            PLAY + "SchoolSuspendAndContinueRestoreRealTwoLeafDoorsPlayerMemoriesAndActorsOnce",
            PLAY + "HauntedCorridorDressingPreservesColliderDoorItemAndNavigationStateAndReleasesOwnedArt",
            PLAY + "FurnishedCorridorThreeSeedNavigationKeepsEveryRoomPickupCabinetAndDoorwayReachable",
            PLAY + "EntranceCavernHasWideTallPhysicalClearanceAndRealInputReachesTheMaze"))]),
    "ritual-items": ("Single-target incense and offering guidance, supported pickups, optional stock save compatibility, 0.25-speed wheel, anatomical hand grips/rendering and all six frozen attackers", [
        ("PlayMode", play_tests("RitualWorldTests.cs", "RitualPickupSupportTests.cs", "RitualTimeStopTests.cs", "RitualItemWheelTests.cs", "PlayerHandsTests.cs") + (
            PLAY + "TrapPlacementUsesSupportedGroundAndPausedInputNeverConsumesStock",
            PLAY + "ArmedTrapCheckpointRestoresStockSuppliesAndModelWithoutReplayingPlacement",
            PLAY + "PortraitFirstAppearanceUsesPhysicalFrameThenProtectedAngryGrace",
            PLAY + "NurseryFirstCryAndCrawlPreparationKeepFullFiveSecondSafetyWindow",
            PLAY + "ArchiveScrapePrecedesRealEmergenceAndRetryResetsItsOwner"))]),
    "traps": ("Supported placement and physical input, six actors held five paused-aware seconds without damage, capture faces, recorded cues and armed-save restoration", [
        ("PlayMode", (
            PLAY + "TrapPlacementUsesSupportedGroundAndPausedInputNeverConsumesStock",
            PLAY + "AllSixMonstersAreRestrainedFiveSecondsWithoutAttackingAndKeepCaptureFaces",
            PLAY + "ArmedTrapCheckpointRestoresStockSuppliesAndModelWithoutReplayingPlacement",
            PLAY + "SchoolSuspendAndContinueRestoreRealTwoLeafDoorsPlayerMemoriesAndActorsOnce",
            PLAY + "UncatCabinetGapePausesAndRestoresAfterEscape"))]),
    "uncat-cabinet-face": ("Uncat dropped cabinet jaw, modeled irregular teeth, legacy cap exclusion, pause/escape, original eyes and Cyclopse lifecycle", [
        ("PlayMode", (
            PLAY + "UncatCabinetDetectionRushesToKill",
            PLAY + "UncatCabinetGapePausesAndRestoresAfterEscape",
            PLAY + "UncatFaceFollowsChasePauseCalmAndDisableWithoutEditingSharedSkin",
            PLAY + "CyclopseFaceFollowsChasePauseCalmAndDisableWithoutEditingSharedSkin"))]),
    "cyclopse-cabinet-face": ("Cyclopse modeled cabinet gape, actual capture/escape, pause, original pursuit and eye lifecycle", [
        ("PlayMode", (
            PLAY + "CyclopseCabinetDetectionReraisesGrinAndRushesToKill",
            PLAY + "CyclopseCabinetGapePausesAndRestoresAfterEscape",
            PLAY + "CyclopseFaceFollowsChasePauseCalmAndDisableWithoutEditingSharedSkin"))]),
    "snappers": ("Six throws per pack, capacity and legacy checkpoint conversion; no PlayMode/player launch", [
        ("EditMode", ("HappyToy.V2.CoreTests.SnapperStockRulesTests",))]),
    "graphics": ("Owned URP depth SSAO, unchanged shadow selection/restore and school/corridor presentation", [
        ("EditMode", (CLOUD + "GraphicsLightingRulesTests",)),
        ("PlayMode", play_tests("CloudGraphicsLightingTests.cs", "CloudGraphicsProbeSupportTests.cs"))]),
    "cabinet-expression": ("Eye emission, physical and third-entry captures, frontal open-mouth death hold, delayed results/pause and save safety for all monsters", [
        ("PlayMode", play_tests("CloudCabinetSurvivalTests.cs", "CloudCabinetExpressionTests.cs", "CloudCabinetTauntTests.cs") + (
            PLAY + "NaturalEyeLifecycleRestoresCallerMaterialAndIndexedPropertiesAndHonoursLaterMaterialOwner",
            PLAY + "NaturalEyeEmissionTogglePreservesPausedChapterCheckpointPhysicsAndSourceAssetsAcrossReload"))]),
    "traffic-loading": ("Real loading footsteps/mixer/master mute, physical head-on passing and related corner/door routes", [
        ("PlayMode", (
            PLAY + "StartLoadingRealTitleButtonFinishesApproachBeforeBlackScreenPreparationAndGatesReveal",
            PLAY + "TitleApproachRecordedContactsReachTheMixerAndObeyMasterMute",
            PLAY + "TitleRecordedPerformanceKeepsEveryContactWithoutFurtherMainThreadTicks",
            PLAY + "CorridorLargeMonstersPassHeadOnThroughAnActualOpenDoor",
            PLAY + "CorridorTemporarilyOccupiedDoorMakesOnlyBlockedActorYieldAndResume",
            PLAY + "CorridorCreaturesNavigateARealCornerWithoutWalkingInPlaceAtMarkers",
            PLAY + "ActualCorridorMonstersOpenClosedDoorsFromDiagonalPatrolApproaches"))]),
    "corridor-performance": ("Bounded passage queries, shared live trap discovery, preserved capture/pause/door behavior and original-bound gameplay LODs; wall-clock player profiling remains separate", [
        ("PlayMode", play_tests("CloudCorridorPerformanceTests.cs", "CloudMaskHorrorModelTests.cs") + (
            PLAY + "CorridorLargeMonstersPassHeadOnThroughAnActualOpenDoor",
            PLAY + "CorridorTemporarilyOccupiedDoorMakesOnlyBlockedActorYieldAndResume",
            PLAY + "TrapPlacementUsesSupportedGroundAndPausedInputNeverConsumesStock",
            PLAY + "AllSixMonstersAreRestrainedFiveSecondsWithoutAttackingAndKeepCaptureFaces",
            PLAY + "ArmedTrapCheckpointRestoresStockSuppliesAndModelWithoutReplayingPlacement",
            PLAY + "HallwayWraithFillsDoorEnvelopeKeepsOriginalAgentAndFitsClonedVisualOnlyOnce"))]),
    "flashlight-hud": ("Flashlight depletion/refill, real icon/charge captures and HUD layout at supported sizes", [
        ("PlayMode", (
            PLAY + "FlashlightBatteryPhysicalInputDepletionPauseOffFullCapAndRefill",
            PLAY + "KoreanMenusRenderAcrossSupportedAspectRatios",
            PLAY + "CompactPickupFeedbackKeepsTheCentralViewClearAcrossScreenSizes"))]),
    "shrines": ("Visible memory stands match collectible positions on current/legacy maps and checkpoint resume", [
        ("PlayMode", (
            PLAY + "MemorySealsStayAboveTheirVisibleStandsOnCurrentAndLegacyMaps",
            PLAY + "HauntedCorridorDressingPreservesColliderDoorItemAndNavigationStateAndReleasesOwnedArt",
            PLAY + "HauntedCorridorRealCameraShowsTimberPaperLanternsSealsAndTempleGateWithFourteenMetreSight",
            PLAY + "CorridorCheckpointJsonRestoresRealScenePickupsDoorsPlayerAndThreatEvidence"))]),
    "door-dust": ("Actual wood dust at door impact, frozen particles on pause, transient cleanup and silent rubble restoration", [
        ("PlayMode", (
            PLAY + "SchoolMaskShattersClosedLeavesInAllMovementStatesAndRestoresSilentRubble",
            PLAY + "MaskRunnerShattersClosedLockedDoorsPassesOpenDoorsAndRestoresRubble"))]),
    "wraith-behavior": ("Corridor post-pursuit/cabinet respite, paused and restored retreat, normal sight and low-velocity route-door contact; school and trapping regression", [
        ("EditMode", (CLOUD + "CorridorCheckpointStoreTests", CLOUD + "ChapterCheckpointStoreTests")),
        ("PlayMode", play_tests("CloudWraithRetreatTests.cs") + (
            PLAY + "MaskRunnerShattersRouteDoorAfterContactAvoidanceStopsItsVelocity",
            PLAY + "MaskRunnerShattersClosedLockedDoorsPassesOpenDoorsAndRestoresRubble",
            PLAY + "MaskRunnerNearWhistleSightStillRespectsRealRoomCoverAndHiding",
            PLAY + "SchoolMaskShattersClosedLeavesInAllMovementStatesAndRestoresSilentRubble",
            PLAY + "MaskThirdOvertCabinetEntryCannotRerollOrEscape",
            PLAY + "AllSixMonstersAreRestrainedFiveSecondsWithoutAttackingAndKeepCaptureFaces"))]),
    "wraith-hallway": ("Visual-only full-height wraith, real closed/locked/open door passage, persistent batched fragments and silent school/corridor restoration", [
        ("PlayMode", (
            PLAY + "HallwayWraithFillsDoorEnvelopeKeepsOriginalAgentAndFitsClonedVisualOnlyOnce",
            PLAY + "BrokenDoorReleasesAnOrdinaryPursuerAlreadyUsingTheOpening",
            PLAY + "MaskRunnerShattersClosedLockedDoorsPassesOpenDoorsAndRestoresRubble",
            PLAY + "SchoolMaskShattersClosedLeavesInAllMovementStatesAndRestoresSilentRubble",
            PLAY + "AdvancedDoorTraversalSchoolMannequinHonorsGazeLightPauseAndRestoresRealPassage",
            PLAY + "StudioRunMovesWithTheAgentAndFreezesOnPause",
            PLAY + "StudioWraithKeepsNavigationAndImportsEighteenSkinnedArms",
            PLAY + "StudioCorridorCloneUsesOneReplacementAndHidesTheFloatingFace",
            PLAY + "ArmedTrapCheckpointRestoresStockSuppliesAndModelWithoutReplayingPlacement"))]),
    "title": ("Unique creature/head Resources keys, title head/body/comfort behavior, real start transition and keyboard start", [
        ("EditMode", (CLOUD + "BaeksugwiHeadResourceTests",)),
        ("PlayMode", play_tests("CloudBaeksugwiHeadTests.cs") + (
            PLAY + "StartLoadingRealTitleButtonFinishesApproachBeforeBlackScreenPreparationAndGatesReveal",
            PLAY + "TitleApproachRecordedContactsReachTheMixerAndObeyMasterMute",
            PLAY + "TitleRecordedPerformanceKeepsEveryContactWithoutFurtherMainThreadTicks",
            PLAY + "MainCorridorFocusedEnterBeginsOnceThroughRealKeyboardInput"))]),
    "shelter": ("One/two-door room topology, actual player/pursuer passage and current/legacy checkpoint restoration", [
        ("EditMode", (EDIT + "CorridorLayoutTests",)),
        ("PlayMode", play_tests("CloudCorridorRevisionTests.cs", "CloudCorridorCheckpointTests.cs", "CloudCorridorSuspendTests.cs") + (
            PLAY + "ActualCorridorMonstersOpenClosedDoorsFromDiagonalPatrolApproaches",
            PLAY + "CorridorAltarRoomIsDistantReachableAndRestoresTheSamePhysicalSeededChamber"))]),
    "corridor": ("Opened-door E focus, visual-only runner, one/two-door shelters and real corner navigation", [
        ("PlayMode", play_tests("CloudCorridorRevisionTests.cs"))]),
    "entrance": ("Wet ritual cavern art, supported clear route, owned ambient/reflection resources, real input and NavMesh access, sealed temple gate and current/legacy checkpoint restoration", [
        ("EditMode", (CLOUD + "CorridorCheckpointStoreTests", CLOUD + "ChapterCheckpointStoreTests",
                      CLOUD + "CheckpointTrapCodecTests", CLOUD + "LightExplorationRulesTests", CLOUD + "GraphicsLightingRulesTests")),
        ("PlayMode", play_tests("CorridorEntranceChamberTests.cs", "CorridorEntranceRitualDressingTests.cs") + (
            PLAY + "CorridorCheckpointJsonRestoresRealScenePickupsDoorsPlayerAndThreatEvidence",
            PLAY + "HauntedCorridorDressingPreservesColliderDoorItemAndNavigationStateAndReleasesOwnedArt",
            PLAY + "HauntedCorridorRealCameraShowsTimberPaperLanternsSealsAndTempleGateWithFourteenMetreSight",
            PLAY + "StartLoadingRealTitleButtonFinishesApproachBeforeBlackScreenPreparationAndGatesReveal",
            PLAY + "SchoolSuspendAndContinueRestoreRealTwoLeafDoorsPlayerMemoriesAndActorsOnce",
            PLAY + "ArmedTrapCheckpointRestoresStockSuppliesAndModelWithoutReplayingPlacement") +
            play_tests("CloudGraphicsLightingTests.cs", "CloudGraphicsProbeSupportTests.cs"))]),
    "core": ("All engine-independent rules; use only when explicitly requested or the entire core is affected", [
        ("EditMode", CORE_FIXTURES)]),
    "readability": ("Rendered body wall clearance, pickup borders, candle blackout and creature gait", [
        ("PlayMode", play_tests("CloudGameplayReadabilityTests.cs") + (
            PLAY + "ActualCorridorMonstersOpenClosedDoorsFromDiagonalPatrolApproaches",
            PLAY + "CandleProximityFlickersAndTemporarilyBlackoutsOnlyPreviouslyLitMarkers",
            PLAY + "CreatureStudioPursuitHasOffBeatHeadMotionAndRestoresCalmMotionAfterPause"))]),
    "loading": ("Loading work progress, lotus renders, real title start and corridor-to-school/retry", [
        ("PlayMode", play_tests("CloudLoadingIndicatorTests.cs") + (
            PLAY + "StartLoadingRealTitleButtonFinishesApproachBeforeBlackScreenPreparationAndGatesReveal",
            PLAY + "MainCorridorAndSchoolRealEntriesKeepBothSuspendSlotsAndRecordsSeparate",
            PLAY + "ActualDeathRetryAndRepeatedUiNavigationKeepOwnedResourcesBounded"))]),
    "fps-settings": ("Real-time FPS sampling, opt-in saved setting, actual UI clicks/reload, school HUD and supported screen sizes", [
        ("PlayMode", play_tests("CloudFpsSettingsTests.cs") + (
            PLAY + "GamepadNavigatesRealMenusAndCancelsWithoutDuplicateSubmit",
            PLAY + "KoreanMenusRenderAcrossSupportedAspectRatios"))]),
    "ui": ("Title/loading, menu controls, results/history, retry/settings", [
        ("PlayMode", play_tests("CloudLoadingIndicatorTests.cs", "CloudFpsSettingsTests.cs") + (
            PLAY + "StartLoadingRealTitleButtonFinishesApproachBeforeBlackScreenPreparationAndGatesReveal",
            PLAY + "MainCorridorFocusedEnterBeginsOnceThroughRealKeyboardInput",
            PLAY + "GamepadNavigatesRealMenusAndCancelsWithoutDuplicateSubmit",
            PLAY + "KoreanMenusRenderAcrossSupportedAspectRatios",
            PLAY + "CompactPickupFeedbackKeepsTheCentralViewClearAcrossScreenSizes",
            PLAY + "LitCandleFocusClearsImmediatelyAndPhysicalOccludersStillBlockObjectsBehindIt",
            PLAY + "ActualDeathRetryAndRepeatedUiNavigationKeepOwnedResourcesBounded",
            PLAY + "NurseryComfortRestoresLightDuringPausedSettingsWithoutAdvancingReveal") +
         play_tests("CloudChapterRecordsTests.cs", "CloudCorridorRecordsTests.cs"))]),
    "ai": ("Detection, pursuit/search, noise arrival and shared cabinet attack rules", [
        ("EditMode", (EDIT + "CloudNoiseInvestigationClockTests", CLOUD + "CorridorBabyMemoryTests")),
        ("PlayMode", play_tests("CloudFeedbackAiTests.cs", "CloudPursuitTests.cs",
                                "CloudNoiseInvestigationArrivalTests.cs", "CloudCabinetSurvivalTests.cs") + (
            PLAY + "PerceivedHearingUsesCurrentTransmissionAndRejectsUnheardOrInactiveCues",
            PLAY + "SilentEnemyStateCannotInventTensionButActualUnseenFootstepsCanAnticipate",
            PLAY + "TrueRecognitionHasBoundedAftermathAndClearsOnRestorationAndRetry"))]),
    "ai-doors": ("Physical door passage, shared ownership, blocked routes and school floors", [
        ("PlayMode", play_tests("CloudMonsterDoorTraversalTests.cs", "CloudAdvancedDoorTraversalTests.cs",
                                "CloudCorridorSearchDoorTests.cs", "CloudSchoolStairPursuitTests.cs"))]),
    "drawer": ("Actual E interaction, collision exclusion and restoration", [
        ("PlayMode", (PLAY + "DrawerRealEOpensAndClosesAtTouchingDistanceWithoutShovingPlayerAndRestoresCollisionPairs",))]),
    "checkpoint": ("Checkpoint validation/storage and actual save/resume", [
        ("EditMode", (CLOUD + "ChapterCheckpointStoreTests", CLOUD + "CorridorCheckpointStoreTests",
                      CLOUD + "CheckpointTrapCodecTests", CLOUD + "LightExplorationRulesTests")),
        ("PlayMode", play_tests("CloudChapterCheckpointTests.cs", "CloudCorridorCheckpointTests.cs",
                                "CloudCorridorSuspendTests.cs"))]),
    "full": ("All EditMode and PlayMode tests; player build/native review remains separate", [
        ("EditMode", ()), ("PlayMode", ())]),
    "full-cpu": ("All EditMode and PlayMode tests with no graphics/audio; GPU-dependent tests may fail or be unavailable", [
        ("EditMode", ()), ("PlayMode", ())]),
}


PROFILES["temple-lanterns"]=("Guardian face uplighting and generated corridor ceiling lanterns, original routes, shared PBR/LODs, danger/pause/blackout and owned teardown", [
    PROFILES["entrance-lighting"][1][0],
    ("PlayMode", PROFILES["entrance-lighting"][1][1][1] + play_tests("CorridorBuddhistLanternTests.cs") + (
        PLAY + "HauntedCorridorDressingPreservesColliderDoorItemAndNavigationStateAndReleasesOwnedArt",
        PLAY + "CorridorCeilingFixturesShareRealCandleDangerAndRecover"))])


PROFILES["school-clock"]=("Judgment-based progress hours, legacy v3 save migration, real seam walking/pause, generated clock PBR/LOD identity and normal-camera LED readability", [
    ("EditMode", ("HappyToy.V2.CoreTests.SchoolProgressClockTests", "HappyToy.V2.CoreTests.SchoolAnomalyRulesTests", "HappyToy.V2.CoreTests.SchoolLoopPlanTests", CLOUD+"SchoolLoopCheckpointTests")),
    ("PlayMode", play_tests("SchoolProgressClockIntegrationTests.cs", "SchoolLoopTests.cs", "SchoolLoopSeamTests.cs"))])

PROFILES["school-loop"]=("Same left-room entrance after either departure, rigid rear half-turn/preview, real bidirectional seam walking, retired old gameplay, locked classrooms, v3 saves and generated music-room PBR/LOD camera review", [
    ("EditMode", ("HappyToy.V2.CoreTests.SchoolLoopPlanTests", CLOUD+"SchoolLoopCheckpointTests")),
    ("PlayMode", play_tests("SchoolLoopTests.cs","SchoolLoopSeamTests.cs"))])

PROFILES["school-turnbacks"]=("Opaque extended school bends, full-width standing/crouched occlusion, real corner walking and delayed transfers, preserved seam motion, clock/save compatibility, gate and unchanged main-hall brightness", [
    ("EditMode", ("HappyToy.V2.CoreTests.SchoolLoopPlanTests", "HappyToy.V2.CoreTests.SchoolProgressClockTests", CLOUD+"SchoolLoopCheckpointTests")),
    ("PlayMode", play_tests("SchoolLoopTests.cs", "SchoolLoopSeamTests.cs", "SchoolProgressClockIntegrationTests.cs", "SchoolRetractableGateTests.cs")+(
        PLAY+"SchoolNormalCameraStackKeepsTheApprovedPreviewBrightness",))])

PROFILES["school-connectors"]=("Dressed school connector legs, two-faced ceiling emergency sign, full fixture/gate clearance, PBR salvage and debris, real routes and existing seam/clock/brightness behaviour", [
    PROFILES["school-turnbacks"][1][0],
    ("PlayMode", PROFILES["school-turnbacks"][1][1][1]+play_tests("SchoolConnectorPresentationTests.cs"))])

PROFILES["school-renewal"]=("Eighteen newly generated school asset types, original PBR/normal maps, cast-iron radiators and double-sided Korean room signs, furniture orientation, both tube-emission LODs and pause, structural collision, closed transparent windows, wet floor reflection and real loop seam continuity", [
    ("EditMode",PROFILES["school-turnbacks"][1][0][1]+("HappyToy.V2.CoreTests.SchoolFluorescentClockTests",)),
    ("PlayMode", tuple(dict.fromkeys(PROFILES["school-turnbacks"][1][1][1]+play_tests(
        "SchoolRenewalArchitectureTests.cs", "SchoolRenewalFurnitureTests.cs", "SchoolFluorescentLodTests.cs",
        "SchoolConnectorPresentationTests.cs", "SchoolHorrorEnvironmentTests.cs", "SchoolLightingContinuityTests.cs",
        "SchoolRadiatorTests.cs", "SchoolRoomSignTests.cs"))))])

PROFILES["school-wayfinding"]=("Generated cast-iron radiators and projecting double-sided Korean room signs, shared PBR/LODs, clear aisles, normal production-camera views, real bidirectional loop walking, matching seam views, gate clearance and approved brightness", [
    ("EditMode", ("HappyToy.V2.CoreTests.SchoolLoopPlanTests", CLOUD+"SchoolLoopCheckpointTests")),
    ("PlayMode", play_tests("SchoolRadiatorTests.cs", "SchoolRoomSignTests.cs")+(
        PLAY+"SchoolLoopRealWalkingReturnsFromEitherEndToTheSameLeftRoomEntrance",
        PLAY+"SchoolBothSeamViewsMatchTheirVisibleContinuationWithoutMirroring",
        PLAY+"SchoolRetractableGateKeepsGeneratedLodsAndTheRealSpawnRouteOpen",
        PLAY+"SchoolNormalCameraStackKeepsTheApprovedPreviewBrightness"))])

PROFILES["school-walker"]=("One generated rigged straight-lane pedestrian with a solid player capsule, blocker stop/resume, recorded distance-driven tile contacts in the quiet spatial mixer, walk/idle, physically hidden reappearance after either school seam, pause/talisman, exact v3 walker save/legacy migration, shared art teardown and normal camera", [
    ("EditMode", ("HappyToy.V2.CoreTests.SchoolWalkerRouteTests", "HappyToy.V2.CoreTests.SchoolLoopPlanTests",
                  "HappyToy.V2.CoreTests.SchoolProgressClockTests", CLOUD+"SchoolLoopCheckpointTests",
                  CLOUD+"CheckpointTrapCodecTests", CLOUD+"ChapterCheckpointStoreTests", CLOUD+"CorridorCheckpointStoreTests")),
    ("PlayMode", play_tests("SchoolWalkerTests.cs", "SchoolWalkerCheckpointTests.cs", "SchoolWalkerAudioTests.cs")+(
        PLAY+"SchoolLoopMenuLoadsOnlyTheNewCorridorAndSealsAllThreeRooms",
        PLAY+"SchoolLoopRealWalkingReturnsFromEitherEndToTheSameLeftRoomEntrance",
        PLAY+"SchoolBothOpaqueBendsAreWalkableAndOnlyTheExtendedEndsTeleport",
        PLAY+"SchoolBothSeamViewsMatchTheirVisibleContinuationWithoutMirroring",
        PLAY+"SchoolLoopSuspendRestoresTheNewMapAndRejectsAClassroomPose",
        PLAY+"SchoolClockJudgesBothDirectionsAndFreezesOnPause",
        PLAY+"SchoolClockSaveRestoresHoursAndLegacyV3StartsAtMidnightWithoutGuessingSuccess",
        PLAY+"SchoolNormalCameraStackKeepsTheApprovedPreviewBrightness",
        PLAY+"SchoolGraphicsVolumeCameraCookieAndReflectionScopeRestoreAllOwnedState"))])

PROFILES["uncat-bell-distance"]=("Recorded Uncat bell at 3/24/36/50m, approaching gain, stereo bearing, physical wall, pause/master mute and real walking-to-pursuit rhythm", [
    ("PlayMode", play_tests("UncatBellDistanceTests.cs")+(
        PLAY+"UncatActualWalkingBellsAccelerateAfterRecognitionAndStopDuringPause",))])

PROFILES["school-horror"]=("Acquired scanned school decay, breached ceiling, actual planar-reflection contribution/cleanup, lighting pause and camera review; preserved loop navigation, closed classrooms, saves and music asset identity", [
    ("EditMode", ("HappyToy.V2.CoreTests.SchoolLoopPlanTests", "HappyToy.V2.CoreTests.SchoolFluorescentClockTests", CLOUD+"SchoolLoopCheckpointTests")),
    ("PlayMode", play_tests("SchoolLoopTests.cs", "SchoolLoopSeamTests.cs", "SchoolHorrorEnvironmentTests.cs", "SchoolFluorescentLodTests.cs")+(
        PLAY+"SchoolGraphicsVolumeCameraCookieAndReflectionScopeRestoreAllOwnedState",
        PLAY+"CorridorGraphicsVolumeCameraCookieAndReflectionScopeRestoreAllOwnedState"))])

PROFILES["school-gate"]=("Generated folded school scissor gate, one stored padlock, actual spawn walking/NavMesh, PBR/LOD source identity and normal camera review with preserved school brightness", [
    ("EditMode", ("HappyToy.V2.CoreTests.SchoolLoopPlanTests", CLOUD+"SchoolLoopCheckpointTests")),
    ("PlayMode", play_tests("SchoolLoopTests.cs", "SchoolRetractableGateTests.cs")+(
        PLAY+"SchoolNormalCameraStackKeepsTheApprovedPreviewBrightness",
        PLAY+"SchoolGraphicsVolumeCameraCookieAndReflectionScopeRestoreAllOwnedState"))])

PROFILES["school-lighting-continuity"]=("School-only Forward+ with player shader retention, frozen-lamp 12.5cm entry traces and reprojected ceiling patches against Forward8, nine-light/offscreen receiver, atlas/pipeline restoration, real surfaces, brightness, walking and saves", [
    ("EditMode", ("HappyToy.V2.CoreTests.SchoolLoopPlanTests", "HappyToy.V2.CoreTests.SchoolFluorescentClockTests", CLOUD+"SchoolLoopCheckpointTests", CLOUD+"GraphicsLightingRulesTests")),
    ("PlayMode", play_tests("SchoolLoopTests.cs", "SchoolHorrorEnvironmentTests.cs", "SchoolLightingContinuityTests.cs", "SchoolRetractableGateTests.cs",
        "CloudGraphicsLightingTests.cs", "CloudGraphicsProbeSupportTests.cs", "CloudLocalShadowTests.cs"))])


PROFILES["school-anomalies"]=("Guaranteed normal first passage, restorable 50-percent draws/three-normal cap, real forward/back judgments, solid walker flicker/pose, Baby shop blackout/cry/60-second chase, ordered blackout/five dark seconds, v3 migration and camera review", [
    ("EditMode", ("HappyToy.V2.CoreTests.SchoolAnomalyRulesTests", "HappyToy.V2.CoreTests.SchoolProgressClockTests", "HappyToy.V2.CoreTests.SchoolWalkerRouteTests", CLOUD+"SchoolLoopCheckpointTests", CLOUD+"CheckpointTrapCodecTests", CLOUD+"ChapterCheckpointStoreTests")),
    ("PlayMode", play_tests("SchoolAnomalyTests.cs", "SchoolProgressClockIntegrationTests.cs", "SchoolWalkerTests.cs", "SchoolWalkerCheckpointTests.cs", "SchoolWalkerAudioTests.cs", "SchoolLoopTests.cs", "SchoolLoopSeamTests.cs")+(
        PLAY+"SchoolNormalCameraStackKeepsTheApprovedPreviewBrightness",PLAY+"SchoolHorrorLightingFreezesOnPauseAndDoesNotLeakRenderSettings"))])

PROFILES["local-shadow-logic"]=("CPU-only corridor shadow selection, atlas limits and restoration", [
    ("PlayMode", (PLAY+"LocalLanternShadowsKeepTorchAndAtMostTwoPointCastersAndRestoreOnRemoval",))])

PROFILES["school-anomalies-logic"]=("CPU-only school anomaly rules, physical seams, solid walker and flickering pose, Baby blackout/shop/timeout/contact and save restoration; no GPU appearance or listening certification", [
    ("EditMode", ("HappyToy.V2.CoreTests.SchoolAnomalyRulesTests", "HappyToy.V2.CoreTests.SchoolProgressClockTests", "HappyToy.V2.CoreTests.SchoolWalkerRouteTests", CLOUD+"SchoolLoopCheckpointTests", CLOUD+"CheckpointTrapCodecTests", CLOUD+"ChapterCheckpointStoreTests")),
    ("PlayMode", (
        PLAY+"SchoolEightCorrectJudgmentsBringMorningAndFinish",
        PLAY+"SchoolAwardPosterChangesForAnomalyAndRestoresAfterward",
        PLAY+"SchoolCleaningFootstepsHoldDuringPauseAndClearOnPassageChange",
        PLAY+"SchoolMovingDoorsHoldDuringPauseAndRestoreClosed",
        PLAY+"SchoolReviewSequenceFollowsActualBackwardTransfers",
        PLAY+"SchoolFlickerFirstReviewFollowsActualBackwardTransfers",
        PLAY+"SchoolAnomalyFirstPassIsNormalAndActualSeamsJudgeBothDirections",
        PLAY+"SchoolWalkerSolidCapsuleAndFlickerTrembleAdvanceAndPause",
        PLAY+"SchoolFlickerCreaturePursuesPlayerAndContactDefeats",
        PLAY+"SchoolFlickerCreatureLimbContactDefeats",
        PLAY+"SchoolBabyBlackoutShopTimeoutContactAndPauseAreHeadlessSafe",
        PLAY+"SchoolBabyRetreatCarriesTheGhostThroughTheSeamAndFadesBodyAndVoiceDuringSafeDeparture",
        PLAY+"SchoolAnomalyCheckpointKeepsTheDrawTimersBabyPoseAndLegacyNormalMigration",
        PLAY+"SchoolWalkerUsesOneSharedRiggedPersonAndHoldsItsActualPoseDuringPauseAndTalisman",
        PLAY+"SchoolWalkerStopsOnHisStraightLaneForAPlayerAndWalksAgainWhenTheWayClears",
        PLAY+"SchoolWalkerCheckpointJsonAndCopyRestoreTheExactPausedPassAndAuthoredPose"))])


PROFILES["school-new-anomalies-logic"]=("CPU-only review sequence, award poster, cleaning and animated door/drawer rules and integration; no rendered or audio quality certification", [
    ("EditMode", ("HappyToy.V2.CoreTests.SchoolAnomalyRulesTests", "HappyToy.V2.CoreTests.SchoolProgressClockTests", CLOUD+"SchoolLoopCheckpointTests")),
    ("PlayMode", (PLAY+"SchoolAwardPosterChangesForAnomalyAndRestoresAfterward",
                  PLAY+"SchoolStudyStudentsRemainSeatedAndTurnOnlyHeads",
                  PLAY+"SchoolCleaningFootstepsHoldDuringPauseAndClearOnPassageChange",
                  PLAY+"SchoolMovingDoorsHoldDuringPauseAndRestoreClosed",
                  PLAY+"SchoolReviewSequenceFollowsActualBackwardTransfers",
                  PLAY+"SchoolFlickerFirstReviewFollowsActualBackwardTransfers",
                  PLAY+"SchoolAnomalyCheckpointKeepsTheDrawTimersBabyPoseAndLegacyNormalMigration"))])

PROFILES["school-cleaning-logic"]=("CPU-only invisible wet footsteps, pause and checkpoint restoration; no rendered or audio quality certification", [
    ("EditMode", ("HappyToy.V2.CoreTests.SchoolAnomalyRulesTests",)),
    ("PlayMode", (PLAY+"SchoolCleaningFootstepsHoldDuringPauseAndClearOnPassageChange",))])

PROFILES["school-cleaning-presentation"]=("Barefoot blood, splash and stained-water camera, school surface ownership, normal brightness and planar reflection", [
    ("EditMode", ("HappyToy.V2.CoreTests.SchoolAnomalyRulesTests",)),
    ("PlayMode", (PLAY+"SchoolCleaningFootstepsHoldDuringPauseAndClearOnPassageChange",
                  PLAY+"SchoolHorrorSurfacesKeepRealPbrInputsAndReleaseOnlyOwnedCopies",
                  PLAY+"SchoolWetFloorReflectsTheActualViewAndReleasesItsPrivateCamera",
                  PLAY+"SchoolNormalCameraStackKeepsTheApprovedPreviewBrightness"))])

PROFILES["school-walker-seam-logic"]=("CPU-only completed walker renewal through both actual masked school seams, route rules, checkpoints and anomaly judgment", [
    ("EditMode", ("HappyToy.V2.CoreTests.SchoolWalkerRouteTests", "HappyToy.V2.CoreTests.SchoolLoopPlanTests", "HappyToy.V2.CoreTests.SchoolAnomalyRulesTests", CLOUD+"SchoolLoopCheckpointTests")),
    ("PlayMode", (PLAY+"SchoolWalkerCompletedAtAnomalyRetreatStartsTheNextPassAtTheSeam",
                  PLAY+"SchoolWalkerKeepsTheSameActorAcrossBothOpaqueSeamsAndOldSaveSpawnLanes",
                  PLAY+"SchoolWalkerCheckpointJsonAndCopyRestoreTheExactPausedPassAndAuthoredPose",
                  PLAY+"SchoolAnomalyCheckpointKeepsTheDrawTimersBabyPoseAndLegacyNormalMigration",
                  PLAY+"SchoolReviewSequenceFollowsActualBackwardTransfers"))])

PROFILES["school-new-anomalies-presentation"]=("Opt-in normal camera review of study students, award poster, bloody footprints and moving windows; logic plus rendered views, no player FPS certification", [
    ("PlayMode", (PLAY+"SchoolStudyStudentsRemainSeatedAndTurnOnlyHeads", PLAY+"SchoolAwardPosterChangesForAnomalyAndRestoresAfterward", PLAY+"SchoolCleaningFootstepsHoldDuringPauseAndClearOnPassageChange", PLAY+"SchoolMovingDoorsHoldDuringPauseAndRestoreClosed"))])

PROFILES["school-anomaly-presentation"]=("All-nine-lamp blackout retains actual torch contribution; retiring Baby carries its rigid seam pose and fades body/cry with paused/talisman-safe cleanup, judgments, active-Baby save/cry/chase and normal camera regressions", [
    ("EditMode", ("HappyToy.V2.CoreTests.SchoolAnomalyRulesTests", "HappyToy.V2.CoreTests.SchoolProgressClockTests", CLOUD+"SchoolLoopCheckpointTests", CLOUD+"CheckpointTrapCodecTests")),
    ("PlayMode", play_tests("SchoolAnomalyTests.cs")+(
        PLAY+"SchoolNormalCameraStackKeepsTheApprovedPreviewBrightness",
        PLAY+"SchoolHorrorLightingFreezesOnPauseAndDoesNotLeakRenderSettings",
        PLAY+"SchoolWalkerRecordedTileContactsReachTheSpatialMixerAndStopForObstructionPauseTalismanAndMute"))])


PROFILES["temple-forest-gap-presentation"] = (
    "Opt-in front-camera comparison of two visual-only low-LOD forest gap candidates; not a performance audit",
    [("PlayMode", (PLAY + "TempleStoryEvidenceUiOffscreenAspectReview",))])


PROFILES["temple-fern-gap-presentation"] = (
    "Opt-in front camera comparison of individual and combined visual-only fern candidates; not a performance audit",
    [("PlayMode", (PLAY + "TempleFernGapOffscreenReview",))])


PROFILES["temple-atmosphere-art-review"] = (
    "Opt-in original sky, cloud and halo comparison, stone relief and foreground tree visibility; not a performance audit",
    [("PlayMode", (PLAY + "TempleNightSkyOffscreenReview",
                    PLAY + "TempleStoneReliefOffscreenReview",
                    PLAY + "TempleForegroundTreeVisibilityOffscreenReview"))])


PROFILES["temple-ritual-art-review"] = (
    "Opt-in cloudy sky, stone layer and front pine binding reviews; not a performance audit",
    [("PlayMode", (PLAY + "TempleNightSkyOffscreenReview",
                    PLAY + "TempleStoneLayerOffscreenReview",
                    PLAY + "TemplePine11RopeOffscreenPrototypeReview"))])


PROFILES["temple-pine-wrap-review"] = (
    "Opt-in visible pine wrapping comparison and surface contact review; not a performance audit",
    [("PlayMode", (PLAY + "TemplePine11WrappedRopeVariantOffscreenReview",))])


PROFILES["temple-haunted-path-review"] = (
    "Opt-in offscreen candidate path comparison; not a performance audit",
    [("PlayMode", (PLAY + "TempleHauntedPathOffscreenReview",))])


PROFILES["temple-horizon-band-review"] = (
    "Opt-in sky and terrain horizon comparison; not a performance audit",
    [("PlayMode", (PLAY + "TempleHorizonBandOffscreenReview",))])


PROFILES["temple-altar-cord-review"] = (
    "Opt-in altar cord comparison and attachment probes; not a performance audit",
    [("PlayMode", (PLAY + "TempleAltarCordOffscreenReview",))])


PROFILES["temple-horizon-slope-review"] = (
    "Opt-in horizon terrain slope comparison; not a performance audit",
    [("PlayMode", (PLAY + "TempleHorizonSlopeOffscreenReview",))])


PROFILES["temple-altar-cord-occlusion-review"] = (
    "Opt-in altar cord occlusion and lighting comparison; not a performance audit",
    [("PlayMode", (PLAY + "TempleAltarCordOcclusionOffscreenReview",))])


PROFILES["temple-altar-ground-haze-v2-review"] = (
    "Opt-in offscreen altar ground haze second candidate; not a performance audit",
    [("PlayMode", (PLAY + "TempleAltarGroundHazeOffscreenReview",))])


PROFILES["temple-altar-ground-haze-review"] = (
    "Opt-in offscreen altar ground haze ABBA comparison; not a performance audit",
    [("PlayMode", (PLAY + "TempleAltarGroundHazeOffscreenReview",))])


PROFILES["temple-shrine-attached-cord-review"] = (
    "Opt-in true facade attachment and coupled papers comparison; not a performance audit",
    [("PlayMode", (PLAY + "TempleShrineAttachedCordOffscreenReview",))])


PROFILES["temple-shrine-cord-shape-review"] = (
    "Opt-in shrine cord and real hall contact comparison; not a performance audit",
    [("PlayMode", (PLAY + "TempleShrineCordMaterialAndAnchorsOffscreenReview",))])


PROFILES["temple-altar-paper-shape-review"] = (
    "Opt-in independent irregular altar paper comparison; not a performance audit",
    [("PlayMode", (PLAY + "TempleAltarIrregularPaperOffscreenReview",))])


PROFILES["temple-individual-stone-review"] = (
    "Opt-in offscreen individual stone comparison; not a performance audit",
    [("PlayMode", (PLAY + "TempleIndividualStoneOffscreenReview",))])



PROFILES["temple-individual-stone-height-cost-review"] = (
    "Opt-in individual stone heights and offscreen A/B/B/A render cost; not displayed FPS",
    [("PlayMode", (PLAY + "TempleIndividualStoneHeightOffscreenReview",
                   PLAY + "TempleIndividualStoneOffscreenCostAbbaReview"))])


PROFILES["temple-default-stone-camera-review"] = (
    "Opt-in camera review of the actual default individual-stone path",
    [("PlayMode", (CLOUD + "TempleDefaultStoneCameraReviewTests.DefaultStonePathActualCameraReview",))])


PROFILES["temple-horizon-organic-review"] = (
    "Opt-in irregular terrain and sky horizon comparisons; not displayed FPS",
    [("PlayMode", (PLAY + "TempleHorizonOrganicOffscreenReview",))])


PROFILES["temple-gap-fill-review"] = (
    "Opt-in actual gap-stone A/B camera and render-cost review",
    [("PlayMode", (PLAY + "TempleGapFillOffscreenReview",))])


PROFILES["temple-gap-horizon-review"] = (
    "Opt-in gap fill and irregular horizon A/B cameras and render-cost review",
    [("PlayMode", (PLAY + "TempleGapFillOffscreenReview", PLAY + "TempleHorizonOrganicOffscreenReview"))])


PROFILES["temple-guardian-gaze-logic"] = (
    "Two-day guardian head tracking, pause and checkpoint restore; CPU only",
    [("PlayMode", (PLAY + "TempleGuardianHeadsWatchOnlyAfterSecondIncenseAndRestore",))])

PROFILES["temple-guardian-gaze-review"] = (
    "Actual guardian neutral and tracking camera frames",
    [("PlayMode", (PLAY + "TempleGuardianHeadsWatchOnlyAfterSecondIncenseAndRestore",))])
PROFILES["temple-forest-depth-fill-review"] = (
    "Opt-in actual source tree background-density comparison",
    [("PlayMode", (PLAY + "TempleForestDepthFillOffscreenReview",))])

def stages(profile: str) -> list[tuple[str, tuple[str, ...]]]:
    # Do not automatically add broad, unrelated rule fixtures. Every profile
    # explicitly declares its own affected tests; full suites remain opt-in.
    return list(PROFILES[profile][1])


def commands(profile: str, project: Path, output: Path, unity: str = "unity",
             editor_path: str | None = None, timeout: int | None = None,
             graphics_api: str | None = None, no_desktop_interaction: bool = True,
             selected_stages: list[tuple[str, tuple[str, ...]]] | None = None) -> list[dict]:
    timeout = timeout if timeout is not None else (7200 if profile in ("full", "full-cpu") else 1800)
    result = []
    planned_stages = stages(profile) if selected_stages is None else selected_stages
    for index, (mode, selectors) in enumerate(planned_stages, 1):
        report = output / f"{index:02d}-{mode.lower()}.xml"
        command = [unity, "test", str(project), "--editor-version", VERSION,
                   "--mode", mode, "--output", str(report), "--timeout", str(timeout)]
        if selectors:
            command += ["--filter", ";".join(selectors)]
        if editor_path:
            command += ["--editor-path", editor_path]
        # `unity test` owns -batchmode and rejects it in passthrough arguments.
        # Keep the CLI's batch launch and our separate input/window safeguards.
        editor_args = ["-logFile", str(report.with_suffix(".editor.log"))]
        if profile in ("full", "full-cpu"):
            editor_args += ["-noaudio"]
        if profile == "full-cpu":
            editor_args += ["-nographics"]
        if profile in ("temple-approach-horizon-logic", "temple-approach-streaming", "temple-approach-existing", "temple-approach-route", "temple-approach-logic", "temple-guardian-gaze-logic", "temple-offerings-logic", "temple-stone-layout-logic", "hud-interaction-logic", "temple-soil-logic", "temple-soil-original-logic", "temple-concept-environment-logic", "temple-altar-logic", "temple-horizon-logic", "corridor-story-logic", "temple-prologue-flow", "temple-prologue-regressions", "temple-prologue-restore", "hand-animation-logic", "school-anomalies-logic", "school-new-anomalies-logic", "school-cleaning-logic", "school-walker-seam-logic", "local-shadow-logic") or profile in ("core", "lady-polish", "model-polish") and mode == "EditMode":
            editor_args += ["-nographics", "-noaudio"]
        if profile in ("school-new-anomalies-presentation", "school-cleaning-presentation", "temple-story-ui-presentation", "title-tremor-presentation", "temple-altar-vapor-presentation", "temple-candle-presentation", "temple-facade-color-presentation", "temple-altar-soil-cost-presentation", "temple-soil-presentation", "temple-soil-candidate-presentation", "temple-soil-original-presentation"):
            editor_args += ["-noaudio"]
        if profile in ("temple-story-ui-presentation", "temple-soil-presentation", "temple-soil-candidate-presentation", "temple-soil-original-presentation"):
            editor_args += ["-v2-story-ui-review-output", str(output / "story-ui")]
        if profile == "temple-forest-gap-presentation":
            editor_args += ["-noaudio", "-v2-story-ui-review-output", str(output / "story-ui"),
                            "-v2-temple-forest-gap-review"]
        if profile == "temple-fern-gap-presentation":
            editor_args += ["-noaudio", "-v2-temple-fern-gap-output", str(output / "fern-gap")]
        if profile in ("temple-soil-original-logic", "temple-soil-original-presentation"):
            editor_args += ["-v2-forest-earth-original"]
        if profile == "temple-altar-soil-cost-presentation":
            editor_args += ["-v2-temple-cost-output", str(output / "temple-cost")]
        if profile == "temple-night-sky-presentation":
            editor_args += ["-noaudio", "-v2-temple-night-sky-output", str(output / "night-sky")]
        if profile == "temple-altar-cord-occlusion-review":
            editor_args += ["-noaudio", "-v2-altar-cord-occlusion-output", str(output / "altar-cord-occlusion")]
        if profile == "temple-altar-ground-haze-v2-review":
            editor_args += ["-noaudio", "-v2-altar-ground-haze-output", str(output / "altar-ground-haze-v2"), "-v2-altar-haze-variant2"]
        if profile == "temple-altar-ground-haze-review":
            editor_args += ["-noaudio", "-v2-altar-ground-haze-output", str(output / "altar-ground-haze")]
        if profile == "temple-shrine-attached-cord-review":
            editor_args += ["-noaudio", "-v2-shrine-attached-cord-output", str(output / "shrine-attached-cord")]
        if profile == "temple-shrine-cord-shape-review":
            editor_args += ["-noaudio", "-v2-shrine-cord-review-output", str(output / "shrine-cord-shape")]
        if profile == "temple-altar-paper-shape-review":
            editor_args += ["-noaudio", "-v2-altar-paper-review-output", str(output / "altar-paper-shape")]
        if profile == "temple-altar-cord-review":
            editor_args += ["-noaudio", "-v2-altar-cord-review-output", str(output / "altar-cord")]
        if profile == "temple-guardian-gaze-review":
            editor_args += ["-noaudio", "-v2-guardian-gaze-output", str(output / "guardian-gaze")]
        if profile == "temple-gap-horizon-review":
            editor_args += ["-noaudio", "-v2-gap-fill-review-output", str(output / "gap-fill"),
                            "-v2-horizon-organic-review-output", str(output / "horizon-organic")]
        if profile == "temple-forest-depth-fill-review":
            editor_args += ["-noaudio", "-v2-forest-depth-fill-output", str(output / "depth-fill")]
        if profile == "temple-gap-fill-review":
            editor_args += ["-noaudio", "-v2-gap-fill-review-output", str(output / "gap-fill")]
        if profile == "temple-horizon-organic-review":
            editor_args += ["-noaudio", "-v2-horizon-organic-review-output", str(output / "horizon-organic")]
        if profile == "temple-horizon-slope-review":
            editor_args += ["-noaudio", "-v2-horizon-slope-review-output", str(output / "horizon-slope")]
        if profile == "temple-horizon-band-review":
            editor_args += ["-noaudio", "-v2-horizon-band-review-output", str(output / "horizon-band")]
        if profile == "temple-individual-stone-height-cost-review":
            editor_args += ["-noaudio", "-v2-individual-stone-height-output", str(output / "individual-stone-heights"),
                            "-v2-individual-stone-cost-output", str(output / "individual-stone-cost")]
        if profile == "temple-default-stone-camera-review":
            editor_args += ["-noaudio", "-v2-default-stone-review-output", str(output / "default-stone-camera")]
        if profile == "temple-individual-stone-review":
            editor_args += ["-noaudio", "-v2-individual-stone-review-output", str(output / "individual-stone")]
        if profile == "temple-haunted-path-review":
            editor_args += ["-noaudio", "-v2-haunted-path-review-output", str(output / "haunted-path")]
        if profile == "temple-pine-wrap-review":
            editor_args += ["-noaudio", "-v2-pine11-wrap-review-output", str(output / "pine-wrap")]
        if profile == "temple-ritual-art-review":
            editor_args += ["-noaudio", "-v2-temple-night-sky-output", str(output / "night-sky"),
                            "-v2-temple-stone-layer-output", str(output / "stone-layer"),
                            "-v2-pine11-rope-review-output", str(output / "pine11-rope")]
        if profile == "temple-atmosphere-art-review":
            editor_args += ["-noaudio", "-v2-temple-night-sky-output", str(output / "night-sky"),
                            "-v2-temple-stone-relief-output", str(output / "stone-relief"),
                            "-v2-foreground-tree-review-output", str(output / "foreground-tree")]
        if profile == "temple-facade-color-presentation":
            editor_args += ["-v2-facade-color-review-output", str(output / "facade-color")]
        if profile == "temple-candle-presentation":
            editor_args += ["-v2-altar-light-review-output", str(output / "altar-light")]
        if profile == "temple-altar-vapor-presentation":
            editor_args += ["-v2-altar-vapor-output", str(output / "altar-vapor")]
        if graphics_api:
            editor_args += ["-force-" + graphics_api]
        if no_desktop_interaction:
            editor_args += ["-v2-audit-no-desktop-interaction", "-job-worker-count", "2"]
        command += ["--", *editor_args]
        result.append({"mode": mode, "selectors": selectors, "report": str(report),
                       "editorArguments": editor_args, "batchModeManagedByUnityCli": True, "command": command})
    return result


def inspect_results(report: Path, selectors: tuple[str, ...]) -> dict:
    if not report.is_file():
        raise ValueError(f"No current NUnit results: {report}")
    # Audio/visual diagnostics can make XML very large. Retain case identities
    # and results, not the embedded waveform/capture output or full XML tree.
    attributes, cases, timings = None, [], []
    for event, element in ET.iterparse(report, events=("start", "end")):
        if attributes is None:
            attributes = dict(element.attrib)
        if event == "end":
            if element.tag == "test-case":
                cases.append((element.get("fullname", element.get("name", "unknown")),
                              element.get("result", "").lower()))
                try:
                    seconds = float(element.get("duration", ""))
                except ValueError:
                    seconds = None
                if seconds is not None and math.isfinite(seconds) and seconds >= 0:
                    timings.append({"name": cases[-1][0], "seconds": seconds})
            element.clear()
    incomplete = [name for name, result in cases if result != "passed"]
    names = [name for name, _ in cases]
    missing = [selector for selector in selectors
               if not any(name == selector or name.startswith(selector + ".") or
                          name.startswith(selector + "(") for name in names)]
    errors = []
    if not cases:
        errors.append(f"Zero tests ran: {report}")
    if incomplete or attributes.get("result", "").lower() != "passed":
        errors.append(f"Failed, skipped or incomplete tests in {report}: " + "; ".join(incomplete[:10]))
    if missing:
        errors.append("Required tests absent from current results: " + "; ".join(missing))
    return {"total": len(cases), "passed": sum(result == "passed" for _, result in cases),
            "failed": sum(result == "failed" for _, result in cases),
            "skipped": sum(result == "skipped" for _, result in cases),
            "incomplete": incomplete, "missing": missing, "errors": errors,
            "duration": attributes.get("duration"), "report": str(report),
            "sourceResult": attributes.get("result"),
            "timing": {"reportedCases": len(timings),
                       "summedCaseSeconds": sum(row["seconds"] for row in timings),
                       "medianCaseSeconds": statistics.median(row["seconds"] for row in timings) if timings else None,
                       "over60Count": sum(row["seconds"] > 60 for row in timings),
                       "over60Seconds": sum(row["seconds"] for row in timings if row["seconds"] > 60),
                       "slowest": sorted(timings, key=lambda row: row["seconds"], reverse=True)[:10]}}


def read_results(report: Path, selectors: tuple[str, ...]) -> dict:
    result = inspect_results(report, selectors)
    if result["errors"]:
        raise ValueError("; ".join(result["errors"]))
    return result


def desktop_process_options() -> dict:
    """Request hidden, low-priority children; never manipulate desktop focus."""
    if sys.platform != "win32":
        return {}
    startup = subprocess.STARTUPINFO()
    startup.dwFlags |= subprocess.STARTF_USESHOWWINDOW
    startup.wShowWindow = subprocess.SW_HIDE
    return {"startupinfo": startup,
            "creationflags": subprocess.CREATE_NO_WINDOW | subprocess.BELOW_NORMAL_PRIORITY_CLASS}


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--profile", choices=PROFILES)
    parser.add_argument("--list", action="store_true", help="List the scope of each profile")
    parser.add_argument("--dry-run", action="store_true", help="Print command arrays; do not run Unity")
    parser.add_argument("--project", type=Path, default=PROJECT)
    parser.add_argument("--output-dir", type=Path, help="A new, empty result directory (never reuse stale results)")
    parser.add_argument("--editor-path", help="Optional path to the installed pinned Unity executable")
    parser.add_argument("--graphics-api", choices=("d3d11", "d3d12"),
                        help="Optional Windows Editor-only override; leaves project/player settings unchanged")
    parser.add_argument("--no-desktop-interaction", action="store_true", default=True,
                        help="Enabled by default: isolate batch input/cursor writes, hide Windows child windows and use BelowNormal CPU priority")
    parser.add_argument("--timeout", type=int, help="Timeout per Unity stage in seconds (full: 7200; focused profiles: 1800)")
    args = parser.parse_args()
    if args.list:
        for name, (scope, _) in PROFILES.items():
            print(f"{name}: {scope}")
        return 0
    if not args.profile:
        parser.error("Choose --profile or --list; feature checks do not imply a full release gate.")
    if args.timeout is not None and args.timeout <= 0:
        parser.error("--timeout must be positive")
    project = args.project.resolve()
    version_file = project / "ProjectSettings/ProjectVersion.txt"
    if not version_file.is_file() or f"m_EditorVersion: {VERSION}" not in version_file.read_text(encoding="utf-8"):
        parser.error(f"Expected this project's pinned Unity {VERSION}")
    output = (args.output_dir or project / "Builds/Verification/feature-tests" /
              (datetime.now().strftime("%Y%m%d-%H%M%S") + "-" + uuid.uuid4().hex[:8])).resolve()
    if output.exists():
        parser.error("--output-dir already exists. Use a new directory so stale results cannot validate this run.")
    unity = shutil.which("unity")
    if not unity and not args.dry_run:
        parser.error("The Unity CLI is unavailable; install the documented project tools before running tests.")
    plan = commands(args.profile, project, output, unity or "unity", args.editor_path,
                    args.timeout, args.graphics_api, args.no_desktop_interaction)
    scope = PROFILES[args.profile][0]
    if args.dry_run:
        print(json.dumps({"profile": args.profile, "scope": scope,
                          "noDesktopInteractionRequested": args.no_desktop_interaction, "stages": plan}, indent=2), flush=True)
        return 0
    print(json.dumps({"profile": args.profile, "scope": scope,
                      "noDesktopInteractionRequested": args.no_desktop_interaction,
                      "stages": [{"mode": stage["mode"], "requiredSelectors": len(stage["selectors"]),
                                  "report": stage["report"]} for stage in plan]}, indent=2), flush=True)
    output.mkdir(parents=True)
    summary = {"profile": args.profile, "scope": scope, "status": "RUNNING", "stages": []}
    summary["graphicsApi"] = args.graphics_api or "project-default"
    summary["noDesktopInteractionRequested"] = args.no_desktop_interaction
    process_options = desktop_process_options()
    summary["windowsHiddenBelowNormalRequested"] = bool(process_options)
    try:
        for stage in plan:
            completed = subprocess.run(stage["command"], cwd=project, check=False,
                                       stdout=subprocess.PIPE, stderr=subprocess.STDOUT,
                                       text=True, encoding="utf-8", errors="replace", **process_options)
            cli_log = Path(stage["report"]).with_suffix(".cli.log")
            cli_log.write_text(completed.stdout or "", encoding="utf-8")
            recorded = {"mode": stage["mode"], "selectors": stage["selectors"],
                        "exitCode": completed.returncode, "report": stage["report"],
                        "editorArguments": stage["editorArguments"], "cliLog": str(cli_log),
                        "batchModeManagedByUnityCli": stage["batchModeManagedByUnityCli"]}
            summary["stages"].append(recorded)
            result = inspect_results(Path(stage["report"]), stage["selectors"])
            recorded.update(result)
            if completed.returncode or result["errors"]:
                raise ValueError(f"{stage['mode']} exit code {completed.returncode}: " +
                                 "; ".join(result["errors"]))
        summary["status"] = "PASS"
    except (OSError, ValueError, ET.ParseError) as error:
        summary["status"] = "FAIL"
        summary["reason"] = str(error)
        print(str(error), file=sys.stderr)
    finally:
        (output / "summary.json").write_text(json.dumps(summary, indent=2) + "\n", encoding="utf-8")
    return 0 if summary["status"] == "PASS" else 1


if __name__ == "__main__":
    raise SystemExit(main())
