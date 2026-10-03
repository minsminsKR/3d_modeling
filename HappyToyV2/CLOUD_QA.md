# Unity Build Automation: real scene tests

## Latest verified result

**UBA build #4: SUCCESS**, on exact tested commit `58f5cf949380d28574ab4a809c0c903ef9a27811`, Unity **6000.6.0f1**, Windows Micro. Both NUnit artifacts were independently parsed: **6/6 EditMode + 6/6 PlayMode passed, 0 failed/inconclusive/skipped**. The previously failing real-footstep hearing/recognition case passes with the interior, physically validated fixture. The Windows player export and final status also succeeded.

The test ZIP has **73,163,952 bytes**, all **186 entries** pass CRC, and SHA-256 `1bcf7687acdc65706cf4cb1c877bce132130c680b6551feb3fcc1b5b75230307`. It contains the executable, production assembly and scene; no UTF/NUnit test assemblies are included. It still contains Unity's `BackUpThisFolder_ButDontShipItWithYourGame` folder and is a development/testing artifact, not an approved release package. Final logs have no C# compiler-error or Shader-error lines; existing warnings remain. Billable time was 9m41s.

Sanitized evidence: `Verification/cloud-tests/build-4-results.json`, including hashes of both original XML artifacts and the complete terminal log. This documentation-only follow-up does not change the tested Assets, Packages, ProjectSettings or source tools. The source-quality CI for the tested code also passed: https://github.com/minsminsKR/3d_modeling/actions/runs/37153309430.

This is a controlled automated scene/physics/state milestone. Manual survival completion, rendered UI/art quality, audio listening, all encounters, long-run stability and target hardware performance remain open. Prior failures below are retained as history.

## Scope

UBA Windows build #1 succeeded on Unity **6000.6.0f1** for commit `02e6107dffcb21f1748ceb49ef8cabd68b8c50bd`. LFS checkout and the authored SchoolAnnex scene import succeeded. That run built a Windows player; it did not execute gameplay tests. The downloaded 73,163,991-byte ZIP passed CRC verification (SHA-256 `76beddb023a909e80b53cadda9dca15c7d35206ec670f4c08b501604931cc291`). The build used the Microsoft Basic Render Driver, so it is not a visual quality or target-GPU performance sign-off.

This change adds **6 EditMode + 6 PlayMode Unity Test Framework tests**. It does not launch any legacy audit that calls `Application.Quit`, and it does not call scene generation or save APIs. Test assemblies use a strict reflection bridge to the actual `Assembly-CSharp`/`Assembly-CSharp-Editor` types because Unity test asmdefs cannot reference predefined assemblies directly. Missing types/members fail assertions; there is no ignored/mocked fallback. Existing serialized gameplay assembly identities and all scene bytes stay intact.

UTF **1.8.0** is now an explicit manifest dependency, matching the exact built-in version already resolved in the lockfile and first UBA build. No package version was upgraded. NUnit stays at built-in **2.1.0**. The three test asmdefs are not auto-referenced, use TestAssemblies references, and do not replace production assemblies.

## Discovered test contract

EditMode (`HappyToy.V2.EditModeTests`, six tests):
- Actual 29 attack-clock, 20 ripple, 68 stealth and 26 restart assertions, each exposed as a separate NUnit test
- Exact Unity version and production assembly identity guard
- Real imported-scene validation: protected hashes, missing scripts, session/player/camera, required record inventory and nine Korean signs. Scene setup restores afterward without saving the authored scene

PlayMode (`HappyToy.V2.PlayModeTests`, six tests):
- Title freezes input, annex evidence blocks premature restoration/escape, real callbacks restore all seven records, duplicates do not increase discovery count
- Journal/pause behavior and mixed duplicate restart requests; new session resources and explicit title destination
- Real CharacterController stance, overhead obstruction, camera height, actual keyboard-driven foot contacts, quiet crouching and blocked sprint
- Baked NavMesh plus clear collision/raycast approaches for all seven mandatory record objects and the exit; doors must actually finish opening
- Actual player footstep delivery to a controlled enemy, floor/pause sound guards, gradual recognition, continued pursuit after crouch/light change, wall occlusion
- Real attack warning, paused timing, dodge/recovery and lethal stationary contact

The suites intentionally use controlled actor positions and direct record interactions. They do **not** prove manual survival difficulty, a complete input-driven route, rendered menu bounds, audio quality, hardware performance, or every monster encounter.

Tests use bounded realtime watchdogs for scene/condition waits. Scaled gameplay completion is checked through actual state (door position, awareness, attack phase), not assumed from wall-clock delay. Each PlayMode fixture unloads its game scene, removes the injected keyboard and restores the prior current keyboard, seven preference keys including prior absence, time/audio/cursor/input globals and the reload request. Cleanup does not unload the UTF runner's scene if setup fails. All assertions remain failures if contracts change.

## UBA settings

On the existing Windows Micro target, keep exact Unity 6000.6.0f1, project folder `HappyToyV2`, the PR7 branch and the preserved build scene. In Advanced Settings > Tests enable:
1. Run my project's unit tests when building
2. Run EditMode tests
3. Run PlayMode tests
4. Mark build as failed if any test fails

No pre-export/post-export or shell hook is required for this suite. Do not add the legacy audit command-line flags to UTF runs. Start only a reviewed/pushed commit and record that exact revision from the build log. Avoid simultaneous builds and check the remaining approved free allocation before retrying.

A successful build or enabled toggle is not enough: the run must provide nonzero EditMode **and** PlayMode discovery, all **12 named tests** executed, **0 failures / 0 skips**, and its NUnit result artifact. Missing or zero test results leave this gate unverified. Preserve failed reports for diagnosis instead of changing assertions to get a green run.

Official references:
- https://docs.unity.com/en-us/build-automation/reference/unit-tests
- https://docs.unity.com/en-us/engine/6000.6/manual/scripting/test-framework-introduction/getting-started/edit-mode-vs-play-mode-tests

## Validation before the first test-enabled cloud run

The existing 143 pure C# checks and Python source validation remain separate. `Tools/quality/compile_unity_api.py` now models all three test asmdefs as separate compiler stages, uses the real UTF/NUnit references, validates their declared boundaries, and fails when a test source/asmdef/reference is unsupported. It never treats a syntax or external API compile as UTF execution.

## First test-enabled run and diagnosed harness fixes

UBA build #2 executed all 12 tests at `33f7b14534a5fdf1d4b9311906a64674b8468807`: EditMode **5/6 passed**, PlayMode **4/6 passed**, **0 skipped**. The fail-on-test gate correctly stopped the build. The real attack/pause/dodge, eight mandatory navigation approaches, journal/restart and record/escape cases passed. Results and original NUnit XML hashes are retained in `Verification/cloud-tests/build-2-results.json`.

Three failures are not being relabeled as passes:
- The strict protected metadata hash failed. Converting the original LF `.meta` file to CRLF reproduces the exact UBA failure hash. `.gitattributes` now pins LF for that file and EditorBuildSettings.asset. A fresh checkout with `core.autocrlf=true` retains both original hashes; no expected hash or preservation assertion changed
- Both keyboard-driven tests failed before their expected stance/hearing state. The fixture omitted Input System's Editor game-input routing setting. Versioned 1.19 source requires both `IgnoreFocus` and `AllDeviceInputAlwaysGoesToGameView` for unfocused batch-Editor input. The fixture now temporarily sets/restores both, directly observes queued key delivery, and records dynamic input/motor state on failure. There is no manual input update, direct substitute for the tested keypress, ignored test, or weakened gameplay assertion

This identifies a definite missing harness configuration, not a proven production input bug. The subsequent run below confirms the metadata and crouch fixes, while exposing a separate invalid hearing-fixture placement. At that stage the full 12-test gate remained **FAILED / RETEST REQUIRED**; build #4 above now passes it. The new external compiler preflight is in `Verification/cloud-tests/build-3-preflight`; it is not a Unity rerun.

Input routing reference: https://raw.githubusercontent.com/Unity-Technologies/InputSystem/1.19.0/Packages/com.unity.inputsystem/InputSystem/InputManager.cs (gameShouldGetInputRegardlessOfFocus and unfocused Editor event routing).

## Second test-enabled run and hearing-fixture correction

UBA build #3 executed all 12 tests at `46ea8e1661decbb8b44f30ee89c8eb95e969c249`: EditMode **6/6 passed**, PlayMode **5/6 passed**, **0 skipped**. A fresh source clone applied the LF attributes, and the strict imported-scene preservation test passed. Actual keyboard-driven crouch/stand/headroom, walking/crouch footsteps and blocked-sprint silence now passed. The fail-on-test gate still stopped the build; no new player artifact was produced. See `Verification/cloud-tests/build-3-results.json`.

The remaining hearing case observed W input and movement, but the player fell to y=-42.37 with no grounded foot contacts. Serialized geometry proves its nominal player start at x=-8.9 intersects the west wall and exit plaque and overhangs the floor with the real 0.3 m capsule. The fixture sampled only its enemy anchor, then blindly offset the player. Its immediate grounding check did not require a new physics move, and its 3D displacement check could be satisfied by falling. The actual corridor floor is present and enabled; this is an invalid fixture placement, not evidence of a missing production floor. Exact depenetration and stale-versus-transient grounding mechanics are not established by static inspection.

Only that hearing fixture now uses an interior anchor at x=-4.5. Before testing sound, it verifies the real authored floor and standing-capsule clearance across the lane, a clear baked-NavMesh segment, at least three fresh motor physics updates, grounding and floor height. Input must produce horizontal grounded progress; real footstep count, enemy reception and post-step grounding must all pass. No synthetic floor/noise, direct footstep call, skip, relaxed timeout or production change was added. The passing attack case retains its original anchor.

The corrected fixture was subsequently exercised successfully by pinned build #4, as recorded above. The seven-stage genuine-API compilation and source checks in `Verification/cloud-tests/build-4-preflight` remain separate preflight evidence; the actual NUnit artifacts establish runtime success for these 12 controlled tests.
