# Happy Toy V2 quality checks

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

`EnemyAttackClockChecks.Run()` contains 29 assertions against the actual shared C# helper, including pause, recovery, long frames, invalid inputs, repeated attack requests, reset, and minimum durations. It runs automatically in the safe Unity editor validation command below. With an already installed .NET 8 SDK, it can also run independently:

```sh
dotnet run --project Tools/quality/ClockTests.csproj
```

No .NET/C# compiler was installed in the cloud environment, so these assertions are provided but **NOT RUN** there. The Python tests do not substitute a Python reimplementation of game logic.

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

24 validator tests passed and final source/static checks passed. Unity editor compilation, Windows build, runtime audits, PowerShell runner execution, C# clock execution, rendered UI inspection, survival balance and performance remain **NOT RUN** in this environment. Review each stage independently on the target machine.
