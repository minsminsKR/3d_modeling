# Current feature verification

Start with [CODE_MAP.md](../../CODE_MAP.md) and `run_feature_tests.py --list`.
Use `--profile core|ui|ai|ai-doors|drawer|checkpoint|full` for fast rules, the affected
scene behavior, or both complete Unity suites. All runs use pinned Unity
6000.6.0f1, fresh result directories and strict nonempty/pass validation.
Player build and native/visual verification remain separate checks.

For a fresh Windows player's actual corridor frame evidence, use
`-quiet-diagnostics -v2-corridor-play-output <new-directory> -v2-corridor-performance
-v2-audit-seed 73 -screen-width 1920 -screen-height 1080 -screen-fullscreen 0`.
Record the actual graphics API and keep the same seed, build type, API, resolution,
quality, vSync and target settings for before/after runs. This mode requests the
normal gameplay target of 120, preserves production AI and the real input route,
and skips extra rendering, readback, screenshots, PCM and per-frame audio auditing.
`CorridorPerformanceSampling` observes only the actual main-camera render callback;
fewer than 300 callbacks, a bounded-buffer overflow, headless mode or a null
graphics device is a measurement failure.

Optional `-v2-corridor-performance-patrol-seconds 360` requires performance mode
and accepts 300–480 seconds; omission keeps the original route. After the fourth
memory it cycles real supported maze waypoints through the existing Walk/door/
evasion inputs, starting its observation only once all four enemies are active.
It completes the current movement leg before stopping. The original game/wall/
audit limits of 580/590/600 seconds increase only by the requested patrol duration.
Natural defeat remains a route failure; it does not invalidate already recorded
frame intervals or establish a successful long observation.

`-v2-audit-movement-multiplier 8` optionally accelerates preparation travel by 1–12
(default 1). Only long clear straight segments qualify; corners, doors, item input,
nearby enemies and the entire patrol retain original speeds. Original walk/run
values and restoration are recorded. Clearance uses the original run speed,
requested multiplier and actual frame delta to reserve a braking distance before
the next corner; the forward capsule must clear that same distance.
`renderedOverall` retains accelerated travel, the quarter-speed item wheel and
global enemy time stop. `normalSpeedRenderedOverall`, normal-speed 10-second
windows and the separate minimum normal-speed rolling FPS exclude those states,
pause and their transitions. Normal pace requires the original global time scale,
at least eight real frames and .25 seconds after an excluded state; the entire
rolling history must follow that stabilization. It needs 300 normal-speed samples.
`excludedFromNormalSpeedIntervals` counts these exclusions; the window's legacy
`acceleratedOrTransitionIntervals` field remains an alias for existing readers.
Snapshots include the actual time scale, item-wheel and enemy-time-stop state.

The bounded report records p50/p95/p99/max, intervals over 16.67/20/33.33/50ms,
one-second rolling FPS minima, and one-second memory/profiler-counter snapshots.
CPU/GPU timings are cached delayed `FrameTimingManager` data; valid CPU samples
survive unavailable GPU data. GPU/CPU recorders remain alive for release timing.
Their transition exclusion is a heuristic: delayed timing samples are diagnostic
context, with `cpuGpuNormalSpeedAttributionVerified=false`, and do not establish
normal-pace CPU/GPU attribution. Use the render intervals and clean rolling
history for that pace subset.
Unknown counters are explicitly unavailable, with units and -1 snapshot values.
Loaded object/material/mesh/texture enumeration occurs only before/after collection;
supported counters provide intermediate counts. Ten-second aggregation, one-second
snapshots, 30-second live logs and the route's existing milestone JSON/log writes
have observer cost included. No forced GC or additional render fallback is used.
Route success, measurement validity and the 16.67ms budget are distinct results;
neither a route nor a capture PASS certifies sustained 60fps or hardware presentation.

`entrance` selects Core, both checkpoint stores, `CheckpointTrapCodecTests` and
`LightExplorationRulesTests`, then the four actual
`CorridorEntranceChamberTests` cases: cavern body/roof clearance and real walking
to the maze, E inspection/physical gate blocking, fresh-scene saves from the cavern
and existing maze, and legacy versions 1/4. It also includes existing checkpoint,
presentation ownership, production gate-camera/14m visibility, and actual loading
transition regressions, plus school suspend/restore and armed-trap save restoration.
The production gate-camera case compares an alternate clear-background colour to
detect visible sky or cave-shell gaps while retaining the original 14m enemy check.
It also checks the `-visual` prefab's shared runtime mesh/material and explicit
Resources albedo/MR identity, then compares a temporary white-albedo render to
measure actual texture contribution. Material/property-block bindings are restored
and temporary materials released; imported source assets stay shared and unchanged.
The visual-prefab Resources key must differ from the FBX key because paths omit
extensions; the runtime instantiates the prefab with its external PBR materials.
`CorridorEntranceRitualDressingTests.cs` adds real generated floor/guardian LODs,
physical support and central navigation, fixed spatial batches, shared source
retention and owned mesh/material/texture teardown. Its production-camera renders
at 16:9/4:3/21:9 independently toggle floor, hanging paper, curved brush ink, candles and guardians
to establish actual pixel contributions with the ambient warm lighting. The actual
start orientation is preserved, and each view records user FOV, feet and camera pose. These are
offscreen projection checks, not resized native-window or performance claims.
The owned room probe must cover the actual cavern floor instead of nearest maze
cell zero; its completed-capture/three-second guard, pause, original maze plan and
scope disposal remain in place. Two eligible local shadow lights stay under the
existing global budget. Only two bounded incense histories supply smoke; there
are no grazing ground-fog sheets. Their clock pauses with gameplay without changing
the existing corridor fog, exposure or pickup/collision state.
Floor placement preserves the .14m relief cap, with only .025m above the unchanged
physical support. The canonical 2.8m guardian is uniformly placed at 3.5m beside
the gate; source normalization and scene scale are distinct and intentional.
The fresh-run start is gate-side chamber-local `(0, .03, -9.2)` with yaw 0 degrees toward the maze; the gate,
physical boundaries, NavMesh and checkpoint positions are retained. A 1.25m hero
lantern and two extra foreground candle gatherings establish nearby visual depth
while the existing nine lights and two shadow candidates are retained. Reused
jangseung, jars, dark timber frames and hanging seals now frame the onward view.
The gate material inspection keeps its separate wide overview camera. A fixed
one-shot 128px cavern reflection covers the passage and remains when the travelling reflection moves
into the maze. The real-camera boundary test backs through local z=8/12/14.6/
15.2/17.6/20, checks dark floor brightness and confirms the fixed capture finished.
The profile also checks shared graphics scope restoration in school and corridor.
The cave-to-maze ceiling height step and first timber-floor underside have continuous stone backing. Each actual
start-camera aspect ratio also compares a magenta background to reject exposed
sky above the original lintel, without changing the physical doorway.
The checkpoint codecs preserve omitted/null optional root `traps` fields through
copy/JSON/store boundaries while rejecting malformed present trap state. Gate artwork/render inspection and a fresh player build
remain separate from these integration checks. Full source and repro commands
are in `SourceArt/CorridorEntrance/README.md`.
`CorridorEntranceImport.InspectMeshes` is an opt-in read-only Editor asset log for
UV0 span/count, texture GUID/size and material/keyword/property-block bindings.
It is separate from actual rendered appearance and does not run in ordinary play.

`ritual-pickups` selects Core and `RitualPickupSupportTests.cs` for actual visible
mesh contact with support, real E collection and fresh-scene restoration. It also
checks basement tables, bare/raised floors and rejection when no support exists,
plus the existing ritual stock/supply JSON round trip, school suspend/restore and
corridor presentation ownership regressions. Three-seed furnishing reachability
and actual cavern-to-maze walking cover the entrance portal's furnishing boundary.
`RitualPickupPlacement` owns physical
support and the interaction box above it;
`RitualWorld` retains pickup identities and optional save-state ownership. The same
support tests also run in `incense` and `ritual-items`. Model originals are unchanged.

`ritual-items` includes `PlayerHandsTests.cs`: imported articulated skin and contact
markers, wrapped flashlight, thumb/index incense and talisman grips, palm-supported
snapper/trap, lower-corner rendering and action/pause lifecycle. `RitualItemWheelTests`
checks the actual Tab/RB wheel and 0.25 gameplay time scale; explicit pause remains
zero. `core` covers the independent `ItemWheelRules` constant and selection rules.
Use `PlayerHandsPreview.Render` / `RenderActions` for silent visual review of the
runtime poses; numerical contact/render coverage does not certify realistic art.
The visibility/action test uses actual URP offscreen projections at 16:9, 4:3 and
21:9 with matching render-target ratios. It does not resize a native game window.

`traps` checks that successful monster capture immediately consumes the trap model
through `TrapVisuals.Consume`, while its root, five-second restraint clock and
trigger/release audio continue. The restraint clock pauses with gameplay.
The six-monster capture test keeps the model
hidden through pause and release. The armed-trap checkpoint regression restores a
visible waiting trap without replaying placement audio; inventory and save formats
remain unchanged.

`title` automatically selects the real methods in `CloudBaeksugwiHeadTests.cs`,
including the title render/crop check at 16:9, 4:3 and 21:9. It checks the larger
title-only silhouette and stronger eye glow in the actual camera output and UI
display area, using a GPU body mask and original-eye on/off comparisons. The UI
crop also preserves both eyes, menu layout and input picking at the three ratios.
It checks slow head motion with planted body supports, reduced motion
and the existing loading/start transition. `CinematicTitleScene` owns clone scale,
head sampling and its camera; `TitleAtmosphere.shader` owns the dark surface and
eye lighting. Shared source models/materials/clips and gameplay Baeksugwi AI remain
unchanged. Rendered appearance and timing are separate from native frame-rate checks.
The opt-in `BaeksugwiHeadProbe` waits for the slow title clock to cover the full
clip (420-second timeout), retaining clip-clock `time` and recording actual capture
`wallSeconds`. `preview_baeksugwi_head.py` uses `wallSeconds` for real GIF intervals;
older manifests retain their existing `time` fallback.

Game-only Baeksugwi geometry is derived by
`SourceArt/BaeksugwiStudio/derive_game_lod.py`; keep its recipe, identity/report and
editable `Baeksugwi_GameLOD.blend`/FBX with the work. `BaeksugwiGameLodImport`
validates original bone-name/bind order and the single protected game mesh before
`MaskHorrorVisual` binds a game clone. The recipe targets 400,000 triangles with a
400,000 maximum and protects complete hands plus all arms. Every source vertex
passes P95/P99/maximum distance guards, and all derived triangle centres and edge
midpoints are checked against the original surface. The former 122,713-triangle
candidate was rejected after images exposed collapsed limbs despite passing its
weaker contact/bounds checks. A regenerated report still requires actual
original-versus-derived Unity image review. Original 32-pose conservative bounds and `updateWhenOffscreen=false`
retain culling. The original source, full title mesh, PBR, clips and AI remain
shared and unchanged. `CloudMaskHorrorModelTests` checks actual bindings/LOD and
eye-bearing UV/render contribution; `CloudWraithHallwayTests` retains the 32-pose
culling/envelope checks. A passing derivation report does not establish 60fps;
compare the same actual corridor route, graphics API and measurement conditions.

`cabinet-expression` includes the committed death presentation after a real lethal
cabinet strike. `CabinetDeathSequence` freezes gameplay, faces the actual visual
towards the cabinet/player and holds its open-mouth approach for 1.65 unscaled
seconds before the result screen. The integration checks include delayed results,
pause and source restoration. Enemy detection/attack and save rules remain in their
existing owners; the sequence receives an explicit playing predicate and callback.
Asset-only facial previews do not replace these actual death-flow checks.
For explicit visual review, set `HAPPYTOY_CABINET_DEATH_REVIEW` to a new local
output directory before running `cabinet-expression`. The six real death-flow
cases then save `<Monster>-player-cabinet-death.png` at 1280×720 from the production
camera with its cabinet and lighting. Ordinary tests do not take these screenshots;
keep optional captures under `Builds/` or another local evidence directory.

`readability` selects rendered torso wall clearance, pickup visibility, temporary
candle blackout/recovery, enlarged creature gait and real large-monster door
passage. Its silent native companion is `-quiet-diagnostics -v2-readability-output
<new-directory>` on a freshly built player. The native report compares visible
outline pixels and opaque-cover frames and captures actual NavMesh patrol/chase.
`preview_gameplay_readability.py <native-output>` creates optional review GIFs
and a contact sheet with Pillow 10.2.0; it does not alter game/model sources.
Every stage preserves its own Editor log; summaries retain failing/skipped counts
and missing selectors. Large XML capture output is streamed rather than retained
in memory. `--graphics-api d3d11|d3d12` is an explicit Windows Editor-only diagnostic
override, not a player/project setting change or verification of the default API.

# Final result: 54/54, player exported; provider footer mismatch retained

Build #16 at 1f0b6469 passes 6 Edit + 48 Play, zero failures/skips, and Unity Windows
export returns0. Original ZIP 186 entries CRC-pass.51 artifacts (16,638,802 bytes)
verify; corrected real aim/mask/Baby frames and strict audio metrics were reviewed.
Dashboard Success disagrees with the post-publication log footer FAILURE; cause
unresolved. See Verification/cloud-tests/build-16-*. No new engine run or feature
work follows this user-requested handover. Sections below are historical preflight.

# Final intro + aim correction: exact-source engine verification pending

Current target is 54 actual cases, retaining prior 44 plus 4 aim and 6 intro cases.
One old lantern contact fixture now faces its legitimate mask anchor; its original
curse/timing/waveform assertions are unchanged. Transport allows 51 files with the
same 18 MB total / 1.5 MB per-file bound. Ten keyframes and a native event-audio WAV/JSON
are planned. Actual execution/render/export remain pending; build #14 is verified.

# Historical optional-aim source preflight (before build #15)

Four focused cases are being added to the previous 44: held-input lifecycle,
physical first-contact parity, real distraction/chase immunity, and actual camera
preview images. Transport allows 39 files (previous 36 + 2 PNGs + 1 JSON) with the same
18 MB total / 1.5 MB single-file cap. All existing cases remain unchanged. See
`Verification/firecracker-aim/README.md` for scope; build #14 remains verified.

# Current perceived-tension verification: build #14 SUCCESS, 44/44

Exact source c6b0c1a3 passed 6 EditMode + 38 PlayMode and Windows export. Four new
cases prove sensory-only integration, hearing/deferred rejection, bounded aftermath/
cleanup and actual PCM. All prior 40 tests stay intact. All 36 artifacts hash-verify;
new 7.3 s stereo output has five exact-silence controls, nonzero intended phases,
zero clipping and bed-to-nearby-contact RMS .12678. Existing enemy WAV/JSON match #13.
Source/API preflight remains separate from the original XML/log/ZIP/PCM evidence in
Verification/cloud-tests/build-14-*. Factual docs need no additional Unity run.

# Previous recovery/flow verification: build #13 SUCCESS, 40/40

Exact source b1ca9e86 passed 6 EditMode + 34 PlayMode and Windows export. Three new
cases verify real physical sprint/stamina, paused nursery comfort changes and
repeated actual death/retry UI/resource flows. All previous 37 cases remain.
34 envelopes hash-verify, the 17-segment acoustic WAV/JSON match #12 byte-for-byte,
and final targeted initialization warnings/C# errors are 0. The original Windows
ZIP has 186 CRC-valid entries. See Verification/cloud-tests/build-13-* and
Verification/physical-recovery/. Source/API preflight and actual engine evidence
remain distinct; evidence/docs-only follow-up needs no further UBA run.

# Previous enemy-audio verification: build #12 SUCCESS, 37/37

Exact source f0ed7649 passed 6 EditMode + 31 PlayMode and Windows export. The
empty-source startup warning fell from 40 in #11 to 0, with an explicit scene-load/
intro/retry/unload regression. Temporary roar source/filter ownership and destruction
also pass. No PCM or gameplay gate was relaxed.

All 34 envelopes hash-verify; the native 48 kHz stereo 17-segment WAV (7.26 s) and JSON
are byte-identical to #11. Independent PCM checks confirm identities, wall/slab
attenuation, stereo, finite range, mute, in-flight pause/resume and overlap headroom.
See Verification/cloud-tests/build-12-* for exact source/log/ZIP/audio evidence.
The evidence/docs completion does not need another engine run. Captured output is
not a speaker/headphone, long-session or target-hardware performance verdict.

# Previous player-feedback verification: build #10

Exact source `705c0639f1bb892a92057481f93381050c3626c8` passed **33 actual Unity tests**
(6 EditMode + 27 PlayMode, zero failed/skipped) and exported the Windows player.
All six user feedback fixes have engine/fixture coverage;32 envelopes and actual
camera views were decoded/reviewed. The original strict route/audio gates remain.
See `Verification/cloud-tests/build-10-results.json` and `build-10-experience.json`
for hashes and limits. No further build is needed for this evidence-only update.

# Previous personal-play verification: build #9

Source `7c13e7f98e40d5e2d89c43f71b41c073eb1ec5d3` passed all **24 actual Unity tests**
(6 EditMode + 18 PlayMode, zero failures/skips) and exported the Windows player.
Three item fixtures and the extended cabinet HUD/cue/pause/retry case passed.
The existing route/audio gates stayed strict. The original ZIP is preserved;
see `Verification/cloud-tests/build-9-results.json` and `build-9-experience.json`.
Source preflight remains in `Verification/personal-play-feedback/`.

# Previous verified milestone: build #8

Exact source923060837b0cb8585d16d9bfffb961434a850504 passed **all21Unity tests (6EditMode+15PlayMode,0failed/skipped/inconclusive)** and exported the Windows player. The five pursuit cases and real-input seven-record route passed. The existing strict AudioRenderer gate now records actual nonzero/unclipped active PCM and zero paused PCM using official-Recorder-style empty Render calls; empty buffers add no samples. This is offline engine mixing, not headset/speaker or complete sound-design acceptance.

Both XMLs, finalSUCCESS log,13artifact envelopes, originalZIP 186 CRCs and safe derivative185unchanged payloads were verified. Source, audio, route and packaging boundaries/hashes are in `CLOUD_QA.md` and `Verification/cloud-tests/build-8-*`. All9PNGs match reviewed#7. The safe derivative preserves development identity/instrumentation and does not clear asset rights or targetWindows smoke tests. No commercial release readiness is implied.

# Build #6 experience and packaging tools

The build #6 runtime suite had **6 EditMode + 10 PlayMode**. UBA #6 at `e3122fb` passed 15/16: the real-input full route, corrected UI layouts and world-sign/camera render passed. Audio capture still returned zero samples, so the strict build failed and no player was exported. The later pause-card caption clarification is source-tested only; actual engine evidence stays pinned to `e3122fb`. See `CLOUD_QA.md` for the true survival strategy, actual camera/UI/audio evidence, and remaining human/hardware limits.

Decode verified output from a downloaded NUnit XML (use the independently verified full checkout commit):

```sh
python Tools/quality/extract_cloud_evidence.py /external/build-playmode.xml --output /external/new-evidence --build NUMBER --commit FULL40CHARACTERSHA
```

The extractor verifies transport hashes and rejects missing/conflicting chunks, unsafe names and existing output directories. A decoded image is real engine output to inspect, not automatic visual acceptance. UBA software-renderer/readback timings and nosound mixer WAVs do not certify target-device performance or listening quality.

`package_windows_candidate.py` makes a separate, deterministic Windows candidate ZIP from an existing verified artifact. It preserves the original, verifies all CRCs and retained bytes, rejects unsafe archive paths and missing player/runtime files, and writes a per-file hash manifest with exact exclusions. It does not change development flags or branding, smoke-test the player, clear third-party licenses or authorize publishing. Run `--help` for its required destination options. The old build #4 source artifact remains development/test evidence even when its backup folder is removed.

---

# Happy Toy V2 quality checks

## Real cloud Unity test suite (2026-10-03)

See `CLOUD_QA.md` for the 6 EditMode + 6 PlayMode tests and exact UBA toggles/discovery contract. These are isolated UTF assemblies exercising the preserved imported scene and engine state/physics/NavMesh, not the standalone C# harness. UBA build #4 on `58f5cf949380d28574ab4a809c0c903ef9a27811` passed all 12 actual tests (6 EditMode + 6 PlayMode, zero failures/skips/inconclusive) and exported the Windows player successfully. `Verification/cloud-tests/build-4-results.json` preserves exact artifact counts and hashes; later documentation-only updates do not change that tested source. Manual survival, rendering/audio and target-hardware QA remain separate.

For external compiler validation with tests, also supply `--test-framework-assemblies` containing genuine `nunit.framework.dll`, `UnityEngine.TestRunner.dll` and `UnityEditor.TestRunner.dll` from the matching Unity 6000.6 template/UTF 1.8.0. The compiler now runs four production stages and three isolated test stages. It fails rather than silently excluding test code.

## Latest lifecycle milestone (2026-10-03)

The actual shared C# harness now has **143 assertions**, adding 26 SceneRestartGate checks to the 117 below. The `flow` audit adds mixed duplicate restart requests, explicit Title return and reset resources. The `transitions` audit now respects all annex prerequisites and checks controlled interruption/cleanup. Both runtime audits remain **NOT RUN**; pure gate tests do not execute Unity SceneManager or inject engine I/O failures. Select prepared runtime cases with `-Audit flow,transitions`; fresh source evidence is in `Verification/quality-lifecycle`.

## Current stealth milestone (2026-10-03)

The console harness now executes **117 actual C# assertions**: 29 attack-clock, 20 ripple and 68 stealth-rule/awareness checks. The safe Editor validation command also runs all three groups. Source CI automatically includes the new helpers through `ClockTests.csproj`.

The new opt-in `stealth` runtime audit checks real-input crouch, capsule/eye height, overhead stand blocking, pause, actual emitted walking/crouch contacts, and silence while stationary or blocked by a wall. Select it with `-Audit stealth`. It is **NOT RUN**; it does not test enemy reception/routing, wet surfaces, cabinet exits, balance or survival completion. Those require dedicated runtime scenarios. Current source evidence is in `Verification/quality-stealth`; all older results below retain their original historical scope.

Final external Unity API compilation covers 85 C# files in four configurations, with zero errors and existing obsolete-API/reference/serialized-field warnings. This is not Unity import, rendering or a player build. `RELEASE_READINESS.md` records those remaining gates.

These tools never regenerate or expand the authored school. `scene_baseline.json` protects the exact selected scene, its metadata and build selection. A mismatch needs review; do not replace the hash just to get a green check.

## 1. Checks runnable without Unity

Use Python 3.10+ from the project root:

```sh
python -m pip install -r Tools/quality/requirements.txt
python -m unittest discover -s Tools/quality -p 'test_*.py' -v
python Tools/quality/validate_project.py --require-parser --output Verification/quality-static/source-validation.json
```

An isolated virtual environment is fine. The parser dependencies are pinned; there are no other Python dependencies. Without them, omit `--require-parser` for metadata/scene checks, but syntax and API checks are explicitly `NOT RUN`. During the cloud upgrade dependencies were isolated in `/tmp/happytoy-quality-deps`, so its commands used `PYTHONPATH=/tmp/happytoy-quality-deps`.

Checks cover:
- Every asset/folder has metadata; GUIDs are valid and unique; no orphan metadata or unresolved LFS pointer files
- The authored scene, its GUID, and selected build scene remain byte-identical
- Main-scene script references resolve to local scripts or four inventoried pre-existing package GUIDs; Unity must resolve those package types
- Every Assets C# file has a valid syntax tree and matching declared class/struct filename
- Required cross-file public API names exist in parsed declarations (comments and strings cannot satisfy them)
- Every listed runtime audit has an actual opt-in flag, exit status and expected output contract
- 24 fixture-based tests deliberately break metadata, scene bytes, source syntax, APIs and the audit manifest to verify failure detection

These checks do not compile C#, resolve Unity APIs, exercise physics/NavMesh, import the font, measure gameplay performance, or prove that any rendered screen fits. An unchanged scene is preservation evidence, not a gameplay pass.

## 2. Pure C# attack-clock checks

`EnemyAttackClockChecks.Run()` contains 29 assertions against the actual shared C# helper, including pause, recovery, long frames, invalid inputs, repeated attack requests, reset, and minimum durations. It runs automatically in the safe Unity editor validation command below. The same harness now runs 20 SurfaceRippleBuffer assertions covering contact bounds, expiry, pause, malformed input and comfort-mode clearing. With an already installed .NET 8 SDK, it can also run independently:

```sh
dotnet run --project Tools/quality/ClockTests.csproj
```

The initial cloud review did not execute these assertions. On 2026-10-02, the CI command was executed locally with Microsoft .NET SDK **8.0.425** installed in temporary storage: the actual C# project built with **0 warnings / 0 errors**, and all **29 assertions passed**. This compiles only the shared helper and its console harness, not Unity scripts or Unity APIs. The Python tests do not substitute a Python reimplementation of game logic.

## Read-only GitHub CI

`.github/workflows/happytoy-quality.yml` checks pull requests targeting `main`, and pushes to `main` or `quality/happytoy-v2-**`, when HappyToyV2, the workflow, or root LFS attributes change. The single Ubuntu job has a 15-minute timeout and cancels superseded runs.

It runs the 24 Python regression tests, the parser-required static validator, and the actual shared C# attack-clock assertions using .NET 8. A temporary `global.json` selects the SDK returned by the setup action, even if the runner also has newer SDKs installed. Generated reports and .NET build products go to runner temporary storage. Each stage has its own step log and summary status; a failure is not masked by log capture. Historical files under `Verification` are never overwritten or counted as new evidence.

The workflow uses only `contents: read`, does not retain checkout credentials, and needs no repository secrets or Unity license. Sparse checkout is limited to HappyToyV2 (plus Git's cone-mode root files), automatic LFS download is disabled, and the only explicitly hydrated LFS object is `HappyToyV2/Assets/Annex/SchoolAnnex.unity`. It never downloads the monorepo's entire LFS collection, edits authored scenes, deploys, or changes repository settings.

Official Actions are pinned to verified release commit SHAs:
- [checkout v7.0.1](https://github.com/actions/checkout/commit/3d3c42e5aac5ba805825da76410c181273ba90b1)
- [setup-python v7.0.0](https://github.com/actions/setup-python/commit/5fda3b95a4ea91299a34e894583c3862153e4b97)
- [setup-dotnet v6.0.0](https://github.com/actions/setup-dotnet/commit/a98b56852c35b8e3190ac28c8c2271da59106c68)

**A green source-quality job does not verify Unity compilation, package/API resolution, Windows builds, runtime gameplay, rendered UI, survival balance, or performance.** Those remain separate opt-in checks below.

## 3. Opt-in Unity validation/build/runtime audits

Unity **6000.6.0f1** and a licensed Windows editor with the Windows build module are required for the complete runner. It never installs software, signs in, alters credentials, pushes, or publishes. Close another editor instance using this project first.

Editor compile, missing-script/reference inventory and pure clock assertions only:

```powershell
.\Tools\quality\Run-UnityQuality.ps1 -UnityEditor 'C:\Program Files\Unity\Hub\Editor\6000.6.0f1\Editor\Unity.exe'
```

Build the preserved scene and run the main suite (game windows will open):

```powershell
.\Tools\quality\Run-UnityQuality.ps1 -UnityEditor 'C:\Program Files\Unity\Hub\Editor\6000.6.0f1\Editor\Unity.exe' -BuildPlayer -Audit flow,smoke,enemy-fairness,stamina,access,cabinets,rooms,walk
```

Optional additional legacy regressions:

```powershell
.\Tools\quality\Run-UnityQuality.ps1 -UnityEditor 'C:\Program Files\Unity\Hub\Editor\6000.6.0f1\Editor\Unity.exe' -Audit attack,hiding,doorclearance
```

The runner calls only `HappyToy.V2.Editor.QualityValidation.Validate` or `.BuildWindows`. It never calls scene authoring/builders. It verifies protected hashes before and after, writes a build-input fingerprint, refuses stale/unverified players by default, checks process exit codes and required JSON, records logs, and times out each child process. `-AllowExistingPlayer` explicitly allows diagnostic checks of an unverified old player; those results cannot validate current source. Output goes to a fresh `Verification/quality-runs/<timestamp>/` folder.

Do not add `-nographics` or run the UI audit in a headless player. `flow` captures the actual UI render target and pointer input at 1600×900, 1280×720 and 1024×768. It visits every inspection page, checks pause/menu return paths and restart, exercises all settings/defaults, and restores the pre-audit preferences on normal exit or its own watchdog timeout. A forced OS process kill can prevent cleanup; use a test OS account if existing preferences matter.

`smoke` exercises controlled scene-aware collection/restore/escape plumbing using direct interactions. `enemy-fairness` uses controlled actors. Neither is a survival completion test. `walk` moves through the actual route using simulated player inputs, but its deterministic path does not use every strategy available to a human. Its failure is useful diagnostic evidence and should be investigated, not relabeled as success.

`audit_manifest.json` lists exact flags, file-vs-directory arguments, outputs, timeouts and scope. Every entry stays `NOT RUN`: historical results elsewhere in Verification are not evidence that this upgrade passed. Fresh execution results are written separately.

## Recorded cloud result

On 2026-10-02, all **24 validator tests**, parser-required source/static checks, and **29 actual pure C# clock assertions** passed locally. The C# harness used .NET SDK 8.0.425; its build had 0 warnings and 0 errors. Workflow YAML, security/scope invariants and embedded shell syntax were checked locally. GitHub-hosted execution is a separate result and must be checked for the published commit. Unity editor compilation, Windows build, runtime audits, PowerShell runner execution, rendered UI inspection, survival balance and performance remain **NOT RUN** in this environment. Review each stage independently on the target machine.


## Optional genuine-API and shader checks without opening the project

The cloud surface pass also used the already installed official Unity 6000.6.0f1 editor files. It compiled all 82 project C# files against genuine Unity/package references in four configurations, and compiled 96 DXIL plus 8 Vulkan SPIR-V shader variants. These are **external compiler checks**, not Unity import/serialization, ShaderLab processing, actual player builds or rendering. The C# checks report existing obsolete-API/serialized-field/reference-compatibility warnings; no clean-warning claim is made for that stage.

Two read-only reproduction tools are included. Neither downloads software or activates Unity. Supply your installed SDK/compiler and exact matching editor packages; keep outputs outside the project:

```sh
python Tools/quality/compile_unity_api.py --editor-data "$UNITY_EDITOR_DATA" --template-assemblies "$MATCHING_TEMPLATE_SCRIPT_ASSEMBLIES" --dotnet-root "$DOTNET_ROOT" --sdk-version 8.0.425 --output "$OUTPUT/api"
python Tools/quality/compile_surface_shaders.py --dxc "$DXC" --include-root "$UNITY_INCLUDE_ROOT" --output "$OUTPUT/shaders"
```

For Unity's Linux editor, the reference assemblies are in Data/Managed, Data/NetStandard and its matching URP blank template ScriptAssemblies cache. The include root must contain a Packages directory mapped to Data/Resources/PackageManager/BuiltInPackages, whose Core and Universal manifests must both read 17.6.0. Use the official Microsoft DirectXShaderCompiler distribution. The recorded compiler was 1.9.2609.5. The script extracts the single HLSLPROGRAM, exercises Forward/Forward+ and lighting/shadow/fog/instancing variants, writes source hashes and individual logs, and returns nonzero on a failed compile.

Final evidence is in Verification/quality-surface/. Do not interpret the shader's analytical alpha range as proof of visibility or appearance: its actual font/material import and scene rendering still require Unity execution.

## Cabinet slit and gameplay overlay review

After a fresh `QualityValidation.BuildWindows` build, run its actual Windows player:

```powershell
& '<fresh-build>/HappyToyV2.exe' -v2-cabinet-peek-output '<fresh-evidence-folder>' -screen-width 1280 -screen-height 720 -screen-fullscreen 0 -logFile '<fresh-evidence-folder>/Player.log'
```

This opt-in controlled audit creates the seed-73 corridor, freezes threats, checks all
14 cabinet meshes for an open slit surrounded by opaque door geometry, and drives
the real F-key input while hidden. It verifies battery drain/pause/depletion, switch
state on exit, absent branch signs and notice/threat overlays, and short latch clips.
It writes `cabinet-peek.json` and native lamp-on/off camera PNGs, then exits nonzero
on failure. It does not certify survival, human listening or frame rate.

The slit geometry is derived at runtime from the retained FBX by `CabinetPeekWindow`;
the imported mesh and original collision are preserved. The latch WAVs can be
regenerated without extra libraries using `python SourceArt/Audio/render_cabinet_latch.py`.

## School first appearance camera review

Run a fresh player with `-v2-school-reveal-output <fresh-evidence-folder>`.
The controlled review invokes the real first and second school memory events,
captures the actual corridor zoom and occluded Cyclopse emergence, and captures the
mannequin under its solitary ceiling spotlight in a different physical corridor.
It checks that the player capsule stays put, gameplay input is held during shots,
pause freezes the shot clock, and the original view/control return. It writes
`school-reveal.json` and PNGs, then exits nonzero on failure. It does not certify
survival balance, human listening, or frame rate. Chapter checkpoint tests separately
cover restoration without replaying the completed first appearances.

The school Cyclopse now crosses the junction at constant corridor x, looks at the
player, turns sideways and exits the opposite branch. The audit also checks held
zoom, no pursuit/attack during the shot, and the pause-aware four-second grace
after control returns. `CloudMonsterPresenceTests` checks the actual posed skin
height independently of the sizing helper, retained root/navigation dimensions,
ground contact, and restoration after the second camera shot.

## Movement, focus and pursuit presentation

Use Unity **6000.6.0f1** and a fresh `QualityValidation.BuildWindows` output.
`LocomotionCameraMotionTests` covers distance-driven gait, settling and landing.
The three `CameraMotion...` PlayMode tests cover physical walking/sprinting, held
input against a wall, pause, reduced motion, cabinet exit and school-shot ownership.
`WindowFocusChangesKeepRunAliveAndPreserveExplicitPause` and
`FocusDisabledGamepadDoesNotOpenPauseButDisconnectStillDoes` exercise focus/pause
notifications and Input System device disable/removal; they do not send actual
desktop Alt+Tab input.

Run the fresh Windows player with
`-v2-horror-feedback-output <fresh-evidence-folder>` for the opt-in controlled
presentation assay. It uses genuine sight acquisition and Chase on a stationary
real actor, checks continuous pursuit, disengagement, explicit pause and reduced
motion, and retains camera/UI images plus native listener DSP audio. Its capture
scope and any failure are recorded in `horror-feedback.json`. Test preferences are
restored before exit. Capture outputs, builds and editor caches are optional local
evidence; the C# generators and audits with their metadata are required source.

The pursuit scope now uses URP's native FilmGrain with an original generated high-contrast alpha texture for the brief recognition
burst and continued witnessed Chase, with a generated band-limited interference
clip underneath real enemy contacts. The native assay saves grain-disabled world
references and checks actual central camera pixel differences with the same frozen
pose; a nonzero Volume parameter alone is insufficient. Pause, hiding and reduced
motion suppress the rendered noise.

`SchoolClassroomDoorOpensWithoutFloatingInteriorSign` opens the real 1-2 classroom
door and captures the production player camera looking through the opening at eye
level. The runtime `Empty school portrait` formerly generated at (-4.8, 1.75, 1.46)
floated inside that opening; its frame, backing and drawing are no longer generated.
The test rejects a portrait root in the opening and also checks the suppressed
`CLASSROOM sign interior` from inside the room. The exterior plaque and original
door/lintel collision remain. The original scene is not regenerated.

`CandleActualNormalMotionWarningGrowsFlickerDepthAndCompletedPulseCount` samples
natural frames at approximately 10, 6 and 3 metres, measuring real light intensity,
flame emission, height, completed pulses and readable dark holds. A slower dying
flame / dark hold / strong catch now replaces the former fast small flutter.
`CandleActualFarSightStrengthensFlickerBeforeEnemyUpdateAndPhysicalCoverPreventsIt`
checks player-camera sight of a real monster beyond 12m before it sees the player,
physical occlusion and pursuit. Enabled monster geometry in the camera viewport
with a clear physical ray starts the warning; enemy `CanSeePlayer()` alone does not.
`CandleActualUnseenMovementStartsWarningAndRejectsUnheardContacts` drives the real
owned enemy through NavMesh movement and production foot contacts behind cover.
Only actually playing movement audio accepted by the existing acoustics/mute/master
gain checks creates actor-specific 1.25s hearing memory. It checks silence, source
mute, master zero, range, insufficient transmitted gain, pause, expiry and retry.
Mannequin joint movement uses the same admission path. Actual cues can qualify
across floors; physical blackout requires a same-floor enemy within two metres or
an active attack. Harmless scripted Cyclopse/mannequin camera appearances can be
perceived while their brains are disabled without causing contact blackout; mask
and mannequin first-sight turns can warn before their harmful release.
`CandleProductionCameraShowsNearDarkHoldsAndBrightCatchesWithFlashlightInBothModes`
captures the production camera at a clear, physical 2.3m approach in the corridor
and school with the ordinary flashlight enabled. Safe, near-dark and near-bright
images plus measured flame screen height and surrounding pixel contrast prevent
a nonzero intensity/MPB value from passing as a visible effect.
These candle camera images use 95-quality JPEG to stay inside the unchanged 1.5MB
artifact envelope. Pixel assertions measure the original unencoded textures.
`CandleViolentNearWarningFreezesOnPauseAndRecoversSilentlyAfterBlackout`
checks that the severe near-warning freezes during pause and that automatic recovery
retains durable ignition without replaying the match. Keep contact blackout,
ownership, comfort and checkpoint tests in the same focused test run.

## Background validation

When the user is working or gaming, keep editor/authoring work in batch or
`--background` mode without opening windows, changing focus or using OS input.
Use BelowNormal process priority. Pure rule/source checks can continue; defer
sustained GPU rendering and native FPS measurements while they would compete
with the user's game. The route's simulated keys/mouse are in-process Unity
Input System devices, never desktop input.

`run_feature_tests.py --profile core --no-desktop-interaction --dry-run` previews
the explicit `-v2-audit-no-desktop-interaction` Editor argument. Omit `--dry-run`
to execute the selected batch checks. The option defaults off and
is recorded as `noDesktopInteractionRequested` in the plan and run summary.
The same option passes `-job-worker-count 2` to limit the diagnostic Editor's Job System workers.
On Windows, the runner also starts each test child with
`STARTF_USESHOWWINDOW`/`SW_HIDE`, `CREATE_NO_WINDOW`, and
`BELOW_NORMAL_PRIORITY_CLASS`; the run summary records
`windowsHiddenBelowNormalRequested`. With the option off, or on non-Windows
hosts, it adds no subprocess startup/creation options. BelowNormal requests CPU
scheduling priority; it does not cap GPU work or guarantee that another game's
frame rate is unaffected. Direct editor/authoring launches outside this runner
still need their own hidden batch/background launch and priority settings.
`DiagnosticDesktopPolicy` suppresses the shell/motor/native-corridor audit's
cursor writes and the item wheel's OS cursor-centering command only when that
explicit flag and `Application.isBatchMode` are
both true, including initial scene Awake and the common PlayMode fixture cleanup.
Under that same opt-in condition, the common scene fixture snapshots and disables
only the initially enabled keyboard/mouse/gamepad/joystick devices before enabling
background input. Its final cleanup re-enables only that snapshot and restores
the prior current devices that still exist. Initially disabled devices stay
disabled; virtual devices created later by tests are untouched. Later physical
hotplug is outside this initial snapshot and remains unsupported during a run.
No cursor unlock, focus
operation, desktop input or global argument mutation is used to test the policy;
`DiagnosticDesktopCursorSuppressionRequiresExplicitBatchOptIn` checks all four
pure input combinations in the `corridor-performance` profile.

The native corridor audit rejects this flag outside batch mode. Its report records
batch/request/suppression separately; suppression is not a claim that a window is
hidden or that its frame rate represents foreground play. It snapshots and
disables pre-existing keyboard, mouse, gamepad and joystick devices inside that
Unity process and restores them plus their prior current devices. Later device
hotplug is outside this snapshot. No OS-wide input suppression is performed.
Other native audits such as `NativeSchoolPlayAudit` and `FlowAudit` are not covered
by this cursor policy and are not supported by this background validation mode.

Unity player `-batchmode` is headless; `-nographics` omits the graphics device.
Neither certifies foreground gameplay frame rate. The native performance report
rejects those modes even if callbacks occur. A hidden startup request, whether
from this runner or `Start-Process -WindowStyle Hidden`, does not guarantee that
Unity's later window/cursor operations stay hidden; retain batch execution and
the explicit cursor policy together.
Use the editor's explicit camera stills for background visual checks when GPU
work is appropriate; label them as offscreen evidence, separate from player FPS.

See the pinned Unity manual's Player command-line arguments and the
`RenderPipelineManager.endCameraRendering` API documentation. The latter reports
camera rendering, not hardware presentation.

## Authored corridor furniture

`CorridorFurnitureImport.Ensure` configures the five original FBX resources with
metre scale, preserved hierarchy, imported normals, Mikk tangents and no imported
physics or animation. The Windows quality build invokes this import recipe.
`CorridorFurnitureAssetTests` checks actual imported vertices, finite UVs, normals,
material slots, bounded triangle counts and the desk's independent `Drawer`.
Editable `.blend`, generator, validator and license are in
`SourceArt/CorridorFurnishings`; follow its README for Blender 4.0.2 regeneration.

`FurnishedCorridorThreeSeedNavigationKeepsEveryRoomPickupCabinetAndDoorwayReachable`
uses seeds 73, 211 and 509 to test all 81 room paths, real 2.2m interaction rays,
body clearance, cabinet exits and open doorway capsules. All furniture is present
during the navigation bake; elevated furniture surfaces and full open-drawer
footprints are marked NotWalkable without phantom physics.
`CorridorDrawerRealERevealsFinitePickupAndRestoresConsumedStateWithNewAndLegacyCheckpoints`
uses actual mouse aiming and E to expose and collect a finite drawer item, checks
closed-item gating, silent consumed-stock persistence, paused movement, rejected
invalid data and fresh-scene new/legacy checkpoint restoration.
`CorridorFurnishingsActualTorchCameraShowsDeskDrawerShelvesAndAuthoredPbrDetails`
saves production-camera JPEG95 views of the desk, open tray with its item, shelf
and close detail, retaining the original flashlight/fog/light setup. Renderer-off
pixel differences only prove the authored models reached the camera; visual art
review is separate. Repeat navigation/visible-item checks after moving a pickup.

Run these with the existing corridor finite-stock/checkpoint/suspend, physical
monster-door passage and cabinet/aim regressions. Existing direct-API stock
fixtures now expose their real drawer before collecting; the dedicated drawer
test retains closed rejection and real input coverage. The finite supply and
battery identities/counts remain eight and six. Summary:
`Verification/corridor-furnishings/summary.json`.

`supplies-voices` adds finite incense/fragments to four empty supply drawers and
six lower shelf bays, preserving the original memory-side identities. It checks
real support/real E input, version-two exact identities and legacy version-one
stocks/missing optional fields. The four-tray legacy furniture filter still
identifies its original firecracker drawers. It also covers increased Uncat bell
gains and actual Cyclopse panting loop PCM/occlusion/pause/master-mute/range,
talisman time stop and independent owned-clip cleanup, using the quiet guard.

## 회랑 백수귀 행동 검사

`python HappyToyV2/Tools/quality/run_feature_tests.py --profile wraith-behavior --graphics-api d3d11 --no-desktop-interaction`
는 Core와 두 저장소, 실제 추격 만료·캐비닛 생존 후 먼 곳으로 이동하는 이탈,
일시정지/부적 정지/중단 복원과 재목격, 문 접촉 시 0속도·가림·일시정지·경로 제외,
기존 열린/잠긴 문·학교 파괴·캐비닛 강제 포획·덫 회귀를 무음 검사한다.
24초는 이동을 포함한 이탈 시간이며 실제 시야를 무효화하지 않는다.
새 플레이어 실행·생존 난이도·기기 청감 인증은 포함하지 않는다.
