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
