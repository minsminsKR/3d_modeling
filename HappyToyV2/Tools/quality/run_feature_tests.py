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
    "EnemyAttackClockTests", "StealthRulesTests", "SceneRestartGateTests", "LoadingProgressTests",
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
    "corridor-roster": ("Four-memory current corridor without Baby, three staged threats, fourth-memory Lady, real altar/school transition, current UI and legacy checkpoint/record compatibility", [
        ("EditMode", (EDIT + "CorridorLayoutTests", CLOUD + "CorridorSectorPlanTests",
                      CLOUD + "CorridorCheckpointStoreTests", EDIT + "CorridorRecordsTests", CLOUD + "CheckpointTrapCodecTests")),
        ("PlayMode", play_tests("CorridorRosterTests.cs", "CloudCorridorTests.cs", "CloudCorridorThreatProgressionTests.cs",
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
        ("EditMode", CORE_FIXTURES + (CLOUD + "CorridorCheckpointStoreTests", CLOUD + "CheckpointTrapCodecTests")),
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
    "core": ("Engine-independent rules; no scene setup", []),
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
    "ui": ("Title/loading, menu controls, results/history, retry/settings", [
        ("PlayMode", play_tests("CloudLoadingIndicatorTests.cs") + (
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
}


PROFILES["temple-lanterns"]=("Guardian face uplighting and generated corridor ceiling lanterns, original routes, shared PBR/LODs, danger/pause/blackout and owned teardown", [
    PROFILES["entrance-lighting"][1][0],
    ("PlayMode", PROFILES["entrance-lighting"][1][1][1] + play_tests("CorridorBuddhistLanternTests.cs") + (
        PLAY + "HauntedCorridorDressingPreservesColliderDoorItemAndNavigationStateAndReleasesOwnedArt",
        PLAY + "CorridorCeilingFixturesShareRealCandleDangerAndRecover"))])


PROFILES["school-clock"]=("Forward-only progress hours, legacy v3 save migration, real seam walking/pause, generated clock PBR/LOD identity and normal-camera LED readability", [
    ("EditMode", ("HappyToy.V2.CoreTests.SchoolProgressClockTests", "HappyToy.V2.CoreTests.SchoolLoopPlanTests", CLOUD+"SchoolLoopCheckpointTests")),
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

PROFILES["school-walker"]=("One harmless generated rigged straight-lane pedestrian, blocker stop/resume, recorded distance-driven tile contacts in the quiet spatial mixer, walk/idle, physically hidden reappearance after either school seam, pause/talisman, exact v3 walker save/legacy migration, shared art teardown and normal camera", [
    ("EditMode", ("HappyToy.V2.CoreTests.SchoolWalkerRouteTests", "HappyToy.V2.CoreTests.SchoolLoopPlanTests",
                  "HappyToy.V2.CoreTests.SchoolProgressClockTests", CLOUD+"SchoolLoopCheckpointTests",
                  CLOUD+"CheckpointTrapCodecTests", CLOUD+"ChapterCheckpointStoreTests", CLOUD+"CorridorCheckpointStoreTests")),
    ("PlayMode", play_tests("SchoolWalkerTests.cs", "SchoolWalkerCheckpointTests.cs", "SchoolWalkerAudioTests.cs")+(
        PLAY+"SchoolLoopMenuLoadsOnlyTheNewCorridorAndSealsAllThreeRooms",
        PLAY+"SchoolLoopRealWalkingReturnsFromEitherEndToTheSameLeftRoomEntrance",
        PLAY+"SchoolBothOpaqueBendsAreWalkableAndOnlyTheExtendedEndsTeleport",
        PLAY+"SchoolBothSeamViewsMatchTheirVisibleContinuationWithoutMirroring",
        PLAY+"SchoolLoopSuspendRestoresTheNewMapAndRejectsAClassroomPose",
        PLAY+"SchoolClockAdvancesOncePerRealForwardPassKeepsBackwardAndFreezesOnPause",
        PLAY+"SchoolClockSaveRestoresHoursAndLegacyV3StartsAtMidnightWithoutGuessingSuccess",
        PLAY+"SchoolNormalCameraStackKeepsTheApprovedPreviewBrightness",
        PLAY+"SchoolGraphicsVolumeCameraCookieAndReflectionScopeRestoreAllOwnedState"))])

PROFILES["uncat-bell-distance"]=("Recorded Uncat bell at 3/24/36/50m, approaching gain, stereo bearing, physical wall, pause/master mute and real walking-to-pursuit rhythm", [
    ("PlayMode", play_tests("UncatBellDistanceTests.cs")+(
        PLAY+"UncatActualWalkingBellsAccelerateAfterRecognitionAndStopDuringPause",))])

PROFILES["school-horror"]=("Acquired scanned school decay, breached ceiling, actual planar-reflection contribution/cleanup, lighting pause and camera review; preserved loop navigation, closed classrooms, saves and music asset identity", [
    ("EditMode", ("HappyToy.V2.CoreTests.SchoolLoopPlanTests", "HappyToy.V2.CoreTests.SchoolFluorescentClockTests", CLOUD+"SchoolLoopCheckpointTests")),
    ("PlayMode", play_tests("SchoolLoopTests.cs", "SchoolLoopSeamTests.cs", "SchoolHorrorEnvironmentTests.cs")+(
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


def stages(profile: str) -> list[tuple[str, tuple[str, ...]]]:
    selected = list(PROFILES[profile][1])
    if profile != "full":
        if selected and selected[0][0] == "EditMode":
            selected[0] = ("EditMode", CORE_FIXTURES + selected[0][1])
        else:
            selected.insert(0, ("EditMode", CORE_FIXTURES))
    return selected


def commands(profile: str, project: Path, output: Path, unity: str = "unity",
             editor_path: str | None = None, timeout: int | None = None,
             graphics_api: str | None = None, no_desktop_interaction: bool = False,
             selected_stages: list[tuple[str, tuple[str, ...]]] | None = None) -> list[dict]:
    timeout = timeout if timeout is not None else (7200 if profile == "full" else 1800)
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
        editor_args = ["-logFile", str(report.with_suffix(".editor.log"))]
        if profile in ("lady-polish", "model-polish") and mode == "EditMode":
            editor_args += ["-nographics", "-noaudio"]
        if graphics_api:
            editor_args += ["-force-" + graphics_api]
        if no_desktop_interaction:
            editor_args += ["-v2-audit-no-desktop-interaction", "-job-worker-count", "2"]
        command += ["--", *editor_args]
        result.append({"mode": mode, "selectors": selectors, "report": str(report),
                       "editorArguments": editor_args, "command": command})
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
    parser.add_argument("--no-desktop-interaction", action="store_true",
                        help="Isolate batch test input/cursor writes; on Windows hide child windows and use BelowNormal CPU priority")
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
    process_options = {}
    if args.no_desktop_interaction and sys.platform == "win32":
        startup = subprocess.STARTUPINFO()
        startup.dwFlags |= subprocess.STARTF_USESHOWWINDOW
        startup.wShowWindow = subprocess.SW_HIDE
        process_options = {"startupinfo": startup,
                           "creationflags": subprocess.CREATE_NO_WINDOW | subprocess.BELOW_NORMAL_PRIORITY_CLASS}
    summary["windowsHiddenBelowNormalRequested"] = bool(process_options)
    try:
        for stage in plan:
            completed = subprocess.run(stage["command"], cwd=project, check=False, **process_options)
            recorded = {"mode": stage["mode"], "selectors": stage["selectors"],
                        "exitCode": completed.returncode, "report": stage["report"],
                        "editorArguments": stage["editorArguments"]}
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
