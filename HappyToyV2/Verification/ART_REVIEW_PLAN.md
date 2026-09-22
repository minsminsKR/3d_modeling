# Current-commit art review

## Latest selected review build

Latest runtime change: Cyclopse intro safely resolves if restoration deactivates the actor during the roar wait; it no longer proceeds to manipulate an inactive navigation agent. Controlled transition tests passed pause, early restoration and two restart cases (six booleans, zero logged errors), including clean chair/painting/monster/journal/ambience state. Normal intro/roar/AI handoff and linked lighting also passed. Evidence: `current-review/transitions.json`, `outputs/transition-normal-intro/intro.json`; successful build `outputs/unity-event-transition-build.log`. Scene selection remains `SchoolTeacherDesk.unity`.

Audio diagnostic follow-up: opt-in `RouteAudioCapture` recorded 51.456s of 48kHz stereo engine output while the complete route passed (`outputs/audio-review-route`). No truncation or clipped samples; every story stage was non-silent, maximum peak 0.3948 at step 3. WAV is available for listening in workspace outputs; `current-review/audio.json` holds statistics. This is not subjective sound-quality or hardware-volume acceptance, and no mix change was made just to improve a number. Latest build log: `outputs/unity-audio-capture-build.log`, exit 0. Normal launches do not capture audio.

Current scene is `Assets/ClassroomReview/SchoolTeacherDesk.unity`. The original Blender teacher desk adds legs, drawer fronts and pulls within the prior 1.4×0.7×0.76m envelope; original collider and register transform are retained. `teacher-desk/teacher.png` was visually reviewed. Integrity reports 834 renderers and zero missing components/assets. `outputs/unity-teacher-desk-build.log` succeeded; `current-review/teacher-desk-route.json` passed actual register inspection, state-aware doors, restoration chair endpoint and complete escape/Hwacat event. Previous selection statements below are development history.

Latest runtime update: Korean state-aware prompts for doors/cabinet/entrance, plus feedback when the player blocks door closing. `outputs/unity-korean-prompts-build.log` succeeded. `current-review/korean-prompts-route.json` passed actual E-key interactions with door prompt transitions, journal inspections, restoration-chair endpoint and escape. This verifies prompt state, not typography or audio. Selected scene remains `SchoolWashroomFinish.unity`.

Consolidated evidence is now in `current-review/README.md`: current scene captures/integrity, 159 dependency/script/resource file hashes and GUIDs, copied route/access reports and a fresh latest-build UI run (all eighteen booleans passed; seven captures). No missing asset metadata was found and none of the listed dependency files/metas were Git-ignored. This does not stage untracked assets, prove fresh-checkout reproducibility or certify final art/audio quality. Current launch instructions were consolidated in the main README; older selections below are chronological history.

Runtime story follow-up: the current build now aligns the empty-chair event with restoration (story step 4), matching the journal text. The prior silent instantaneous rotation at medical-record collection is removed. The existing chair moves 0.16m and rotates 22 degrees over 0.72 seconds, accompanied by an original spatial synthesized scrape. `outputs/restoration-chair-walk/walk.json` passed: chair pose unchanged through steps 1–3, exactly one cue at step 4, measured final translation/rotation, and successful escape/Hwacat completion. Build log `outputs/unity-restoration-chair-build-2.log` exited 0. No scene footprint expansion. The audio was authored but has not yet received a listening/mix review; this route test does not prove perceived loudness or visual staging quality.

Current selection: `Assets/ClassroomReview/SchoolWashroomFinish.unity`; matching Windows build succeeded (`outputs/unity-washroom-finish-build-2.log`). Original authored tile textures give the washroom floor grout and restrained color variation; a collider-free splashback sits against the sink wall. Fixtures, clues and navigation are unchanged. The first layout guard counted both sink holders and identically named child meshes, so it rejected before saving/building; the corrected guard counts top-level holders only. Visual review: `washroom-finish/washroom.png`. Integrity: 809 renderers, no missing scripts/materials/meshes. `outputs/washroom-finish-access/access.json`: eleven reachable targets, no errors. `outputs/washroom-finish-walk/walk.json`: no failure, optional journal inspections, four story steps, Hwacat completion and escape (two enemies, one attack). No external textures, map expansion or GitHub push. Parent-scene history follows.

Latest selection is now `Assets/ClassroomReview/SchoolCabinet 1.unity`, built successfully by `CabinetReview.ApplyAndBuild` (`outputs/unity-cabinet-facing-build.log`). It adds the Blender cabinet shell, recessed panels, vents and handles while preserving all four original wall colliders and inside/exit transforms. Handle direction is aligned with the interaction entrance, verified in `cabinet-review/infirmary.png`. Scene integrity reports 808 renderers with zero missing scripts/materials/meshes. `outputs/cabinet-hiding/hiding.json` passed all seven hiding-rule booleans plus footsteps; `outputs/cabinet-walk/walk.json` passed both optional inspections, four story steps, Hwacat completion and escape with no failure. The executable is this cabinet build. This is a visual replacement, not functional hinged doors. No map expansion or third-party asset download occurred. Parent-scene history follows.

`Assets/ClassroomReview/SchoolCandidate 3.unity` is now selected. The executable in this project's `Builds/Windows` was built from that scene (`unity-shape-skin-selected-build-2.log`, exit 0). It retains the lighting/journal improvements and uses the coupled `CandidateShapeSkin` Cyclopse variant. Fixed-rest-pose, fixed-coordinate studio closeups in `cyclopse-geometry/shoulder-close.png` versus `cyclopse-geometry-shape-skin/shoulder-close.png` show reduced rear/shoulder protrusions. The all-frame deformation and full-route checks documented below passed. Earlier shape-only and normals variants are not selected. Visible rough areas remain, so this is an incremental improvement, not final character-art acceptance. Historical selection notes below describe earlier states.

Fresh workspace reports: `outputs/review-final-walk/walk.json` passed both optional inspections, four story stages, escape and Hwacat completion; `outputs/review-final-access/access.json` passed all eleven targets; `outputs/review-final-flow/flow.json` passed all fourteen boolean checks and rendered seven UI frames. `outputs/linked-v1intro/intro.json` observed both fixture on/off states with matching bounce/emission and successful Cyclopse introduction. Lighting/Hwacat captures were visually reviewed. These are automated/selected-view results, not an exhaustive human playtest, audio mix review or performance profile.

All changes remain local; no new commit or push has been made. Historical candidate notes below are chronological and do not override this selection.

### Exploration journal follow-up

The current Windows build also stores the two optional inspection texts in the J journal for the current run. Re-reading does not duplicate entries; restarting clears them. PLAY.md documents that these are not persistent saves.

`outputs/journal-focus-flow/flow.json` passed all eighteen boolean checks and seven rendered UI captures. The start diagnostic logged `page=Title, focused=begin` before physical Enter submission. An earlier run failed to begin play, while its unchanged-build repeat passed; the original intermittent failure's cause is not proven. The audit now allows a panel-processing frame after requesting focus, records the focused element, and stops on start failure rather than reporting misleading downstream journal failures. This is diagnostic hardening, not a proven production input bug fix. The repeat's 720p and 4:3 journal images were visually checked: both complete optional texts fit their cards.

`outputs/journal-final-walk/walk.json` completed the current build with no failure, four story steps, escape, Hwacat completion, two active enemies and one attack. Both optional boards were reached using movement and actual E input, with journal storage and unchanged story progression asserted. This supplements the controlled UI test, which uses direct interaction calls to populate records. These remain automated tests, not a human playtest or full visual-quality acceptance.

### Shape and skin coupled candidate — development history

`Tools/reskin_cyclopse_shape.py` re-evaluates the original V1 Gaussian skin-weight field at the cleaned rear-body vertex positions. It outputs `Assets/Art/CandidateShapeSkin/Cyclopse` and `SourceArt/Cyclopse_candidate_shape_skin.blend`, leaving all prior variants intact. The cleaned geometry, UVs and topology are preserved; weights are intentionally recalculated. This removes the measured deformation regression introduced by moving vertices while retaining weights sampled at their old positions: all-frame maximum edge ratios are Walking 4.686 and Run 5.327, versus 11.90 for the earlier shape-only Run candidate. This is not a claim of perfect deformation. FBX round-trips shift absolute frame labels; the audit covers the complete exported clip ranges.

Unity candidate scene `Assets/ClassroomReview/SchoolCandidate 3.unity` preserves the current lighting/journal scene and replaces the Cyclopse visual only. `outputs/shape-skin-monster/monster.json` passed texture, bone motion, height and ground checks. `outputs/shape-skin-walk/walk.json` passed both optional journal inspections, four story stages, escape and Hwacat completion (two enemies, one attack). The rear surface still has rough areas. Its first runtime image cannot be judged against the earlier candidate screenshot as a controlled comparison because lighting changed; a same-lighting baseline comparison is required before selecting this candidate. The selected scene remains `SchoolLighting 2.unity`.

The same-lighting baseline was subsequently rebuilt successfully (`outputs/unity-shape-comparison-base-build.log`, exit 0). `outputs/shape-baseline-monster` passed the same runtime checks. Side/front runtime images at approximately 0.35s chase time show only a subtle difference in this dark view; they do not provide enough evidence of a worthwhile rear/shoulder visual improvement to select the candidate. A fixed-pose rear close-up is the next useful comparison. The current executable has been restored to the selected `SchoolLighting 2.unity` baseline with the completed journal feature, not the shape-skin candidate.

Selection follow-up: fixed-coordinate closeups confirmed reduced protrusions; the candidate is now selected as described at the top. `outputs/shape-selected-flow/flow.json` passed all eighteen checks and seven UI renders on the selected build, including start, journal storage/deduplication/display/reset and settings persistence. The earlier full route evidence is `outputs/shape-skin-walk/walk.json` for this same scene. No new commit or push was made.

### Performance measurement caveat

Follow-up completed: `render-performance-2026-09-23.json` contains 496 synchronized render samples and a successful escape. The only >16.667ms sample was index 0 at the initial player position before collecting any story item (142.045ms). All subsequent sampled renders were below 16.667ms. This supports an initial offscreen-render setup cost in this diagnostic, not a claim about ordinary visible-window startup or every gameplay frame. No production optimization was made based solely on this diagnostic outlier.

The first forced-render route completed successfully on RTX 5060 Ti / Ryzen 5 9600X (D3D12, development build, 1280x720). Non-threat render-plus-readback samples: n=300, median 2.464ms, p95 3.023ms, maximum 118.408ms. Threat samples: n=196, median 2.481ms, p95 3.129ms, maximum 11.373ms. There was one sample over 50ms. A follow-up probe records sample index, story stage and position for slow samples to distinguish initial setup from recurring locations; that follow-up is pending. Do not convert these sparse offscreen measurements to claimed gameplay FPS.

The initial `outputs/route-performance/performance.json` is **not a rendering/FPS benchmark**. It measured hidden-window update intervals (hundreds of thousands of updates with sub-millisecond times) while visible presentation was absent. Its escape result is useful functional evidence; its timings must not be used to claim high frame rates. The replacement probe explicitly submits a 1280x720 scene render and synchronously reads one pixel to wait for GPU completion, at most ten samples per second. This measures offscreen render-plus-readback cost, not full game-frame or display-presentation latency. Hardware, development-build overhead, readback overhead and sparse sampling limit generalization.

Baseline: `57ad81d3b4ea1c9e1021563f87679ca7cfad7dd2`. Do not pull a newer branch or substitute the separate local `UnityV2` project.

## Scope

Improve the existing compact school, preserving the V1 characters and story progression. Start with a classroom quality reference, then apply the agreed quality to the washroom, infirmary and corridor. Do not expand the map to add empty space.

## Baseline observations (2026-09-23)

- Git working tree was clean before this review.
- Build settings select `Assets/Generated/SchoolV2 17.unity`.
- The scene builder defines an 8 by 7 metre classroom with twelve desks and a central aisle.
- Classroom desks, chairs, chalkboard and teacher desk are primitive boxes in the builder. This is a source observation, not a rendered-scene quality verdict.
- A fresh import of this published project has started. The sandbox account lacked the Unity entitlement; the user-account launch resolved entitlement details and started AssetDatabase refresh.

## Implementation and acceptance sequence

1. Finish fresh import and build the baseline without regenerating or overwriting its scene. Capture representative classroom views and check missing scripts/materials.
2. Author reusable classroom furniture with bevels, structurally credible frames and restrained wear; retain source models and material provenance. Preserve the central aisle and access to the register and restoration box.
3. Add a coherent teaching wall, window treatment and restrained classroom storytelling. Keep the erased-child/last-attendance narrative, not unrelated decoration.
4. Improve lighting and material response against identical before/after camera views. Validate player-height readability, collision, door travel, navigation and interaction reach.
5. Apply the quality pass to the other existing spaces; refine V1 deformation and event staging without replacing their identity.
6. Build and verify the complete register-to-escape route, monster events, menu flow and a human-review executable. Clearly separate automated evidence from visual and playtest judgement.

## Fresh baseline evidence

- User-account Unity import completed with exit code 0.
- Found 56 missing generated-asset metadata files in the published copy. Recovered their original GUIDs only after each underlying asset's SHA256 matched the separate original project; the scene and model contents were not replaced.
- `classroom-baseline/scene-integrity.json`: 407 renderers; zero missing scripts, materials and meshes. This checks object references, not all texture/animation dependencies or gameplay.
- Three editor-rendered baseline views are saved alongside the report. The entry view shows weak furniture/wall readability and an over-bright ceiling source. Editor captures do not substitute for player-build tests.
- Original Blender desk/chair exports are ready; placement and visual acceptance remain pending.

## Current candidate

`Assets/ClassroomReview/SchoolClassroom 4.unity` is selected for builds. The original baseline remains intact. The upgrade command starts from that baseline and saves a new unique scene, rather than modifying the original. It now includes new desk/chair models, window frames, lower wall protection, chalkboard trim, local lighting, readable two-sided room signs, washroom stall corrections and an original infirmary bed.

The candidate built successfully in Unity 6000.6.0f1; scene inspection recorded 721 renderers and zero missing scripts/materials/meshes. The previous classroom-only candidate passed the full walking route (four story steps, escape, Hwacat completion, two active enemies, one attack). After the additional washroom/bed changes, all nine access-audit targets passed again (`outputs/rooms-access/access.json` in the original workspace). The fresh walking test is still pending; do not transfer its predecessor's result to this build.

Remaining: finish fresh walking test, visual quality passes and narrative dressing, corridor treatment, V1 motion/event refinements, performance review and final review build. Editor first-view texture initialization can affect captures; compare the later `entry-warmed` image and runtime captures rather than accepting the initial entry frame alone.

## Subsequent verification and candidate work

- The washroom/bed build passed the fresh route: four story steps, escaped, Hwacat completed, two active enemies and one attack (`outputs/rooms-walk/walk.json`).
- Added optional dismissal/seating record boards. The seating board initially failed access at the desk row and was moved to the open teaching-wall area. All eleven interaction targets then passed (`outputs/inspection-access/access.json`).
- The extended walking audit used actual E-key input on both boards, verified the notice and unchanged story count, then completed the escape (`outputs/inspection-walk/walk.json`, 2511 movement updates). This validates the notice state, not a visual typography review.
- Full-frame Blender deformation audit of V1/current/candidate exports is saved in `cyclopse-full-clip/deformation.json`. Worst walking edge stretch: 43.19 / 6.22 / 4.69; worst running: 52.11 / 7.70 / 5.33. Topology and UVs are unchanged by the candidate's broader Gaussian weight smoothing (sigma .08 versus .055).
- `Assets/ClassroomReview/SchoolCandidate.unity` is an unaccepted visual candidate. Its build temporarily occupies `Builds/Windows`; the editor build-scene selection is restored after comparison building. Runtime comparison/route tests are in progress. Do not present it as a fully verified release.

No completion claim is supported yet. Old verification JSON files describe the previous build, not these future changes.
