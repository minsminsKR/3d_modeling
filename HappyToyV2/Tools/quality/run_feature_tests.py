#!/usr/bin/env python3
"""Route changes to fast rules, focused scene tests, or both complete Unity suites.

This runner never builds a player or substitutes these tests for visual/native
release verification. --dry-run prints the exact commands without starting Unity.
"""
from __future__ import annotations

import argparse
from datetime import datetime
import json
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
    "EnemyAttackClockTests", "StealthRulesTests", "SceneRestartGateTests",
    "SurfaceRippleBufferTests", "CabinetHidingRulesTests", "CabinetEncounterClockTests",
    "CorridorBabyMemoryTests", "CorridorThreatRulesTests", "EnemyBellClockTests", "CorridorShelterRulesTests", "CorridorRouteRulesTests", "MonsterPassingRulesTests"))
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
    "cabinet-expression": ("All-monster cabinet scan/retreat/rush, physical hit safety and Cyclopse face transitions", [
        ("PlayMode", play_tests("CloudCabinetSurvivalTests.cs", "CloudCabinetExpressionTests.cs"))]),
    "traffic-loading": ("Real loading footsteps/mixer/master mute, physical head-on passing and related corner/door routes", [
        ("PlayMode", (
            PLAY + "StartLoadingRealTitleButtonFadesMenusApproachesDuringPreparationAndRevealsCorridorBeforeInput",
            PLAY + "TitleApproachRecordedContactsReachTheMixerAndObeyMasterMute",
            PLAY + "CorridorLargeMonstersPassHeadOnThroughAnActualOpenDoor",
            PLAY + "CorridorTemporarilyOccupiedDoorMakesOnlyBlockedActorYieldAndResume",
            PLAY + "CorridorCreaturesNavigateARealCornerWithoutWalkingInPlaceAtMarkers",
            PLAY + "ActualCorridorMonstersOpenClosedDoorsFromDiagonalPatrolApproaches"))]),
    "flashlight-hud": ("Flashlight depletion/refill, real icon/charge captures and HUD layout at supported sizes", [
        ("PlayMode", (
            PLAY + "FlashlightBatteryPhysicalInputDepletionPauseOffFullCapAndRefill",
            PLAY + "KoreanMenusRenderAcrossSupportedAspectRatios",
            PLAY + "CompactPickupFeedbackKeepsTheCentralViewClearAcrossScreenSizes"))]),
    "shrines": ("Visible memory stands match collectible positions on current/legacy maps and checkpoint resume", [
        ("PlayMode", (
            PLAY + "MemorySealsStayAboveTheirVisibleStandsOnCurrentAndLegacyMaps",
            PLAY + "HauntedCorridorDressingPreservesColliderDoorItemAndNavigationStateAndReleasesOwnedArt",
            PLAY + "HauntedCorridorRealCameraShowsTimberPaperLanternsSealsAndReadableExitWithFourteenMetreSight",
            PLAY + "CorridorCheckpointJsonRestoresRealScenePickupsDoorsPlayerAndThreatEvidence"))]),
    "title": ("Title head/body/comfort behavior, real start transition and keyboard start", [
        ("PlayMode", play_tests("CloudBaeksugwiHeadTests.cs") + (
            PLAY + "StartLoadingRealTitleButtonFadesMenusApproachesDuringPreparationAndRevealsCorridorBeforeInput",
            PLAY + "TitleApproachRecordedContactsReachTheMixerAndObeyMasterMute",
            PLAY + "MainCorridorFocusedEnterBeginsOnceThroughRealKeyboardInput"))]),
    "shelter": ("One/two-door room topology, actual player/pursuer passage and current/legacy checkpoint restoration", [
        ("EditMode", (EDIT + "CorridorLayoutTests",)),
        ("PlayMode", play_tests("CloudCorridorRevisionTests.cs", "CloudCorridorCheckpointTests.cs", "CloudCorridorSuspendTests.cs") + (
            PLAY + "ActualCorridorMonstersOpenClosedDoorsFromDiagonalPatrolApproaches",
            PLAY + "CorridorAltarRoomIsDistantReachableAndRestoresTheSamePhysicalSeededChamber"))]),
    "corridor": ("Opened-door E focus, visual-only runner, one/two-door shelters and real corner navigation", [
        ("PlayMode", play_tests("CloudCorridorRevisionTests.cs"))]),
    "core": ("Engine-independent rules; no scene setup", []),
    "readability": ("Rendered body wall clearance, pickup borders, candle blackout and creature gait", [
        ("PlayMode", play_tests("CloudGameplayReadabilityTests.cs") + (
            PLAY + "ActualCorridorMonstersOpenClosedDoorsFromDiagonalPatrolApproaches",
            PLAY + "CandleProximityFlickersAndTemporarilyBlackoutsOnlyPreviouslyLitMarkers",
            PLAY + "CreatureStudioPursuitHasOffBeatHeadMotionAndRestoresCalmMotionAfterPause"))]),
    "ui": ("Title/loading, menu controls, results/history, retry/settings", [
        ("PlayMode", (
            PLAY + "StartLoadingRealTitleButtonFadesMenusApproachesDuringPreparationAndRevealsCorridorBeforeInput",
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
        ("EditMode", (CLOUD + "ChapterCheckpointStoreTests", CLOUD + "CorridorCheckpointStoreTests")),
        ("PlayMode", play_tests("CloudChapterCheckpointTests.cs", "CloudCorridorCheckpointTests.cs",
                                "CloudCorridorSuspendTests.cs"))]),
    "full": ("All EditMode and PlayMode tests; player build/native review remains separate", [
        ("EditMode", ()), ("PlayMode", ())]),
}


def stages(profile: str) -> list[tuple[str, tuple[str, ...]]]:
    selected = list(PROFILES[profile][1])
    if profile != "full":
        if selected and selected[0][0] == "EditMode":
            selected[0] = ("EditMode", CORE_FIXTURES + selected[0][1])
        else:
            selected.insert(0, ("EditMode", CORE_FIXTURES))
    return selected


def commands(profile: str, project: Path, output: Path, unity: str = "unity",
             editor_path: str | None = None, timeout: int = 1800,
             graphics_api: str | None = None) -> list[dict]:
    result = []
    for index, (mode, selectors) in enumerate(stages(profile), 1):
        report = output / f"{index:02d}-{mode.lower()}.xml"
        command = [unity, "test", str(project), "--editor-version", VERSION,
                   "--mode", mode, "--output", str(report), "--timeout", str(timeout)]
        if selectors:
            command += ["--filter", ";".join(selectors)]
        if editor_path:
            command += ["--editor-path", editor_path]
        editor_args = ["-logFile", str(report.with_suffix(".editor.log"))]
        if graphics_api:
            editor_args += ["-force-" + graphics_api]
        command += ["--", *editor_args]
        result.append({"mode": mode, "selectors": selectors, "report": str(report),
                       "editorArguments": editor_args, "command": command})
    return result


def inspect_results(report: Path, selectors: tuple[str, ...]) -> dict:
    if not report.is_file():
        raise ValueError(f"No current NUnit results: {report}")
    # Audio/visual diagnostics can make XML very large. Retain case identities
    # and results, not the embedded waveform/capture output or full XML tree.
    attributes, cases = None, []
    for event, element in ET.iterparse(report, events=("start", "end")):
        if attributes is None:
            attributes = dict(element.attrib)
        if event == "end":
            if element.tag == "test-case":
                cases.append((element.get("fullname", element.get("name", "unknown")),
                              element.get("result", "").lower()))
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
            "duration": attributes.get("duration"), "report": str(report)}


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
    parser.add_argument("--timeout", type=int, default=1800, help="Timeout per Unity stage in seconds")
    args = parser.parse_args()
    if args.list:
        for name, (scope, _) in PROFILES.items():
            print(f"{name}: {scope}")
        return 0
    if not args.profile:
        parser.error("Choose --profile or --list; feature checks do not imply a full release gate.")
    if args.timeout <= 0:
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
                    args.timeout, args.graphics_api)
    scope = PROFILES[args.profile][0]
    if args.dry_run:
        print(json.dumps({"profile": args.profile, "scope": scope, "stages": plan}, indent=2), flush=True)
        return 0
    print(json.dumps({"profile": args.profile, "scope": scope,
                      "stages": [{"mode": stage["mode"], "requiredSelectors": len(stage["selectors"]),
                                  "report": stage["report"]} for stage in plan]}, indent=2), flush=True)
    output.mkdir(parents=True)
    summary = {"profile": args.profile, "scope": scope, "status": "RUNNING", "stages": []}
    summary["graphicsApi"] = args.graphics_api or "project-default"
    try:
        for stage in plan:
            completed = subprocess.run(stage["command"], cwd=project, check=False)
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
