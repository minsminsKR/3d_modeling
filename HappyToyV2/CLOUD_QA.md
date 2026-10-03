# Unity Build Automation: real scene tests

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

The first test-enabled UBA result has not yet been recorded at this source revision. A later report must state the actual commit, run URL, mode-specific counts and failures/skips; until then the new runtime suite is **NOT RUN**.
