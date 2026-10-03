# Happy Toy V2 quality checks

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
