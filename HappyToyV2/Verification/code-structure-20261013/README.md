# Code structure verification — 2026-10-13

Verification record. **The full suite is not green.** The initial complete run
preceded fixture cleanup; no final complete-suite rerun was performed. The scoped
checks, current-input Windows build, quiet native annex flow and staged source
audit are complete, with the regression limitations below.
Counts, failing names and SHA-256 hashes are in [summary.json](summary.json).

The refactor gives UI screens their own controls and presentation inputs/actions,
and gives AI collaborators sensing, committed attacks and physical door passage.
Seven unchanged rule sources moved into the engine-free `HappyToy.V2.Core`
assembly with their existing metadata. Scene coordinators still connect gameplay,
input and checkpoint adapters. `CODE_MAP.md` maps features to code and verification
profiles; both project `AGENTS.md` files persist these development instructions.
The goal is to reduce routine source reading and checking scope. No time/token
reduction percentage or gameplay performance improvement was measured.

| Observed check | Result / limit |
|---|---|
| Initial complete EditMode | 90/90 passed. |
| Initial complete PlayMode | 187/231 passed; 44 failed. Failure names are retained in JSON. |
| Initial full-run feature coverage | Exact AI profile selectors passed 16/16; drawer selector passed 1/1. These are subsets of the same full run, not separate reruns. |
| Additional AI follow-ups | Three distinct sensing, unseen-footstep and recognition-restoration cases passed after fixture cleanup. This does not represent a combined 19-case AI run. |
| Narrow baseline with original `GameShellView` and `StalkerBrain` at `20bc083` | 3/6 passed, 3 failed. Those three failure names also occur in the initial full run. Core, test support and resources remained current: this is a limited coordinator comparison, not a clean whole-repository baseline. |
| Core moves | All seven source Git blobs match `20bc083`; metadata/GUIDs were preserved. |
| Python checks reported by coordinator | Validator 28/28 and profile routing 7/7 passed. |
| Initial genuine Unity API compilation | All 11 stages passed before the fixture and opt-in diagnostic corrections. It was not rerun afterward; current Unity Editor/Windows compilation is verified by the final build, quiet PCM pair and native flow. Compilation does not replace full regression execution. |
| Current DX11 UI profile | 15/16 PlayMode passed. The actual school input route was defeated by Cyclopse at `chapter-memory-1` with two memories recovered. The original-coordinator comparison reproduces that failure. |
| Current DX11 AI doors profile | Core EditMode 19/19 and PlayMode 16/16 passed; PlayMode duration 365.036 seconds. |
| Current DX11 checkpoint profile | EditMode 30/30 and PlayMode 12/12 passed; PlayMode duration 158.110 seconds. |
| Second original-coordinator comparison, DX11 | 3/4 passed. The school route fails at the same stage with player `(24.18, .08, 6.18)`, Cyclopse `(23.26, .04, 5.02)`, three attacks and two memories. Hearing, unseen footsteps and recognition restoration pass. This establishes a matching school-route failure for the limited coordinator baseline. |
| Three DX12 profile attempts | UI, AI doors and checkpoint processes crashed natively, with GPU unrecoverable diagnostics reported by the coordinator. Their PlayMode XML is absent; these are interrupted runs, not passing suites. |
| Seven selected fixture follow-ups | 4/7 passed: the three AI candidates and real-listener PCM. The two complete corridor routes fail later on a zero time scale and door focus; they are not claimed fixed. First-appearance audio failed a test-only pivot-distance assumption subsequently removed. |
| Final current-model/recorder pair, DX11 | First-appearance audio and real-listener PCM passed 2/2, CLI exit 0, in 29.963 seconds after the minimal fixture correction. |
| Final quiet audio pair, DX11 | Actual first-appearance and listener PCM passed 2/2, CLI exit 0, in 29.882 seconds after the diagnostic guard correction. |
| Current Windows player | Build manifest PASS, CLI exit 0, Unity 6000.6.0f1; manifest created `2026-10-12T17:18:59Z`. Current build inputs verified PASS for all 2,919 Assets/Packages/ProjectSettings files; artifact hashes and copied credits match. |
| Quiet native Windows annex flow, DX11 | CLI exit 0; all 27 Boolean checks true, 10/10 frames rendered, eight inspections stored, two journal pages visited and zero reported errors. The coordinator reviewed settings, 4:3 journal and result images: Korean text was readable with no observed clipping. This explicit `Begin()` annex assay excludes the new main-corridor title entry and complete survival routes. |
| Staged source-completeness audit | PASS, 3,387 sources, zero missing sources or unresolved LFS pointers; recorded by the coordinator after staging. |
| Final static source check | PASS: 337 C# scripts, 1,485 asset GUIDs and 968 authored scene components. Four existing package script GUIDs still require Unity's AssetDatabase; static checks do not replace the engine/build checks. |
| Existing local work | The coordinator's final byte comparison confirms all 58 initial snapshot files preserved, including GraphicsPipeline flags normalized by the build. Only related code, tests, tools and documentation are staged. |

The narrow baseline's hearing, checkpoint and unseen-footstep tests passed in
isolation after failing in the full run. The first-appearance audio test failed
inside a nested iterator after starting recording and setting a `.01` capture
clock; later tests reported the same capture clock and an already-recording error.
Together these observations strongly suggest cross-test contamination. They do
**not** establish that all 44 failures existed before this refactor.

The scoped fixture corrections register recorder ownership for shared teardown,
restore the capture clock even when a nested iterator fails, sample the current
creature's actual baked head geometry, and verify that a lit candle stays
unfocusable and cannot ignite twice through another real E press. The follow-up
real-listener capture passed immediately after first-appearance audio failed,
providing direct evidence of recorder/capture-clock isolation. An unsupported
centroid-to-bone-pivot distance assumption was removed: imported pivots need not
coincide with weighted-mesh centroids. Actual weighted visible head geometry,
model/joint ownership, leading face, bodyfront/socket/facing and pause checks
remain; the final audio pair passed. The two complete corridor routes still fail
later on other conditions. No complete-suite success or general route fix is claimed.

The earlier native flow's sole failure was `settingsPersisted`: the existing
diagnostic audio guard reset volume whenever a new shell appeared. Its minimal
correction restores startup volume only for the first shell and preserves later
saved volume through the existing hardware-mute/`FilterVolume` gate. The opt-in
flow audit now allows two layout frames and an end-of-frame after resizing and
waits for actual restart conditions instead of a fixed half-second. The final
native flow and quiet audio pair passed afterward; these diagnostic corrections
do not change Core rules or ordinary gameplay. Three crash-generated test scenes
and their metadata were removed from source inputs and retained locally in ignored
build evidence. Build-input hashes were checked again after the final rebuild.

Raw XML (including the approximately 137 MB full PlayMode report), editor logs and
captures remain local under ignored `Builds/CodeStructure-20261013/`. This small
tracked record retains result counts, failing names, source identity and artifact
hashes. It does not present ignored evidence as a required editable project source.
