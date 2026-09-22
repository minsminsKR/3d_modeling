# Happy Toy — Unity V2 development

## Current local review — 2026-09-23

Current build scene: **`Assets/ClassroomReview/SchoolTeacherDesk.unity`**. Improvements continue from user-selected commit `57ad81d` and are included in this handoff. The latest visual addition is an original Blender teacher desk with drawers, steel legs and handles, preserving the register location and collision footprint.

### Continue on another Windows PC

1. Clone `https://github.com/minsminsKR/3d_modeling.git` (branch `main`), or fast-forward an existing clean checkout with `git pull --ff-only origin main`.
2. Install Unity **6000.6.0f1** with Windows build support through Unity Hub; add the repository's `HappyToyV2` directory as the project. Allow package restore and asset import to finish.
3. Open `Assets/ClassroomReview/SchoolTeacherDesk.unity` and press Play. Do not regenerate the scene or open a historical scene variant as the current game.
4. For a standalone executable use **Happy Toy V2 → Build Windows development player**. The resulting `Builds/Windows` folder must be kept together. Builds and Unity caches are excluded from Git; rebuild them on the new PC.
5. Editable Blender files are in `SourceArt`, with export/refinement scripts in `Tools`. Blender **4.5.9** was used locally. Required imported models, textures, materials and Unity `.meta` files are included in the repository.

The focused visible-window route review passed, including threat-stage timing and escape; see `Verification/current-review/visible-review-retry`. This is a development handoff, not a finished V2 release. Further work remains in character-surface polish, audio listening/mix review and event staging.

This scene includes compact room improvements, Blender desk/chair/bed/cabinet models, washroom tile finishes, optional investigation boards and journal entries, directional room lighting and story-linked fixture glow. Cyclopse uses `CandidateShapeSkin`, preserving the V1 texture and animation while reducing rear-surface protrusions without the earlier shape-only deformation regression. The restoration-stage chair movement now matches the journal text. Earlier scene variants remain for comparison, not as launch targets.

Open the selected scene and press Play, or use **Happy Toy V2 → Build Windows development player**. Use this project's `Builds/Windows/HappyToyV2.exe`, not the older separate `UnityV2` project's build. The original `Assets/Generated/SchoolV2 17.unity` remains as a preserved baseline. Do not use the original Generate menu to reproduce the upgrades; `ClassroomUpgrade` is the separate room-upgrade tool, and the selected scene also includes the subsequently validated Cyclopse replacement.

The latest route passed optional E-key inspections, all four story steps, the corrected chair event and escape. Eleven interaction targets passed accessibility checks on the preceding material-only build. Menu and monster checks have their exact build scope recorded in `Verification/ART_REVIEW_PLAN.md`; historical results are not blanket certification of every later change. These are automated checks and selected-view reviews, not a complete human playtest or final art acceptance.

The handoff and development history below describe earlier stages and are retained for context.

## Historical GitHub project handoff — original upload, not current launch instructions

Open this `HappyToyV2` directory in Unity Hub with Unity **6000.6.0f1**. Open `Assets/Generated/SchoolV2 17.unity` and press Play, or choose **Happy Toy V2 → Build Windows development player**. The build appears at `Builds/Windows/HappyToyV2.exe`; distribute the entire Windows build folder together.

This commit contains the active scene and its asset dependencies, scripts, package/project settings and Blender source art. Unity caches, historical generated scenes and the Windows executable are intentionally not committed. No new gameplay work was performed for this upload. The project is a reviewable development build, not a finished V2 release.

`Verification/` contains selected final local test reports. Historical `../outputs/`, `../work/` and `UnityV2/` references in the development notes below describe the original local workspace, not files promised in this repository. Reusable Blender scripts are in `Tools/`; inherited asset licensing/provenance limitations remain documented in `ART_SOURCES.md`.

This is the Unity migration workspace, not a completed 2.0 release. The original Three.js game remains unchanged in `work/latest-snapshot/3d_modeling-main/game/happy_toy`.

## Environment

Installed editor: **6000.6.0f1**. URP 17.6.0, Input System 1.19.0 and AI Navigation 2.0.12 match its shipped URP template. The initial planned 6.3 target was superseded by the editor actually installed by the user.

Open this directory through Unity Hub. Use **Happy Toy V2 → Generate first playable scene** to create a NEW scene under Assets/Generated (existing scenes are not overwritten). Use **Build Windows development player** to build. Generated materials/pipeline assets deliberately use unique paths; regeneration is for development, not normal gameplay.

Controls: WASD, mouse look, Shift sprint, F flashlight, E interaction, Esc pause. Current Korean HUD uses Windows Malgun Gothic through OS font lookup; final packaged typography/UI is pending.

## First implementation milestone

- 24 authored GLB props converted by Blender to native FBX, with source hashes and PBR constant sidecars.
- User revised the scope to a compact, high-detail game, not wholesale map migration. Current layout: 18 m × 3.2 m corridor, 56 m² classroom, 25 m² washroom and 25 m² infirmary. Storage is part of the classroom, not another wing.
- Original story: Last Attendance. Investigate the erased register, recover a ribbon, read the infirmary record, restore the classroom preparation box, then return to the entrance. Four new Blender-authored story models accompany these beats.
- Story-state hooks: dismissal bell, brief light interruption and telegraphed stalker activation after the ribbon, displaced chair after the medical record, calmer corridor after restoration.
- First-person collision controller, stamina, flashlight, occluded interaction ray, sliding doors, hiding cabinet, four-name collection and exit.
- NavMesh patrol / hearing / pursuit / last-known-position search. Active enemy is now V1 Cyclopse with its original textured skinned mesh and walk/run clips. The earlier mannequin and original warden experiment are no longer the active enemy.
- URP lighting, fog, ACES, restrained bloom and vignette. This establishes a rendering baseline, not verified final art quality.

## Full V2 scope still required

1. Deepen these three rooms and one incident. Do NOT restore the old sprawling map or add empty rooms. Adapt only events that support this story, with deliberate pacing and meaningful return visits.
2. Continue porting and refining V1 monsters/events (Uncat, Baby, Hwacat / angry transformation, Lovely Doll), selected to fit the compact rooms. Cyclopse is the first port; preserve the existing identities. Improve attack animation, collision/visual envelope and audio-driven stalking rather than replacing V1 content with unrelated new monsters.
3. Preserve textured materials and implement PBR surface wear, authored room composition, baked lighting/occlusion and performance budgets. FBX constant-color conversion alone is not final material migration.
4. Final Korean HUD, journal, settings, accessibility, save/load, audio mixing, spatial ambience and title/death/clear flow.
5. End-to-end gameplay tests, model/floor/collision checks, graphics captures, standalone build, hardware performance and packaging.

## Verification

### Review handoff — user-requested stop, 2026-09-15

- The user asked to finish only the current Cyclopse surface issue, then stop for review. No further feature/model work should be started without a new resume request. This handoff is not completion of the full V2 goal.
- Current scene: `Assets/Generated/SchoolV2 17.unity`; build `../outputs/unity-review-ready-build.log` succeeded. Executable remains `Builds/Windows/HappyToyV2.exe`; keep the entire Windows directory together.
- Controlled same-pose unlit/back-cull/two-sided renders showed that the gray side areas were not missing texture or an absent face in that pose. Disabling Cyclopse's specular highlights and environment reflections removed the gray plastic-like sheen while preserving lit diffuse shading and the original texture. Comparison: `../outputs/unity-surface-ready/` versus `../outputs/unity-surface-review/lit.png`. This does not resolve every side fold or every animation pose.
- The first diagnostic attempt lacked a build-retained Unlit shader and stalled; its exact test process was terminated. A referenced diagnostic material and timeout were added. That failed test is superseded by `unity-surface-ready` and `unity-surface-review`, both exit 0. Diagnostic materials are never applied during ordinary gameplay.
- Final review build: surface, v1monster, walk and flow audits returned exit 0 (`../outputs/unity-<audit>-review/`). Two-enemy scripted escape and all 14 UI state/settings checks remain intact; no exception/error matches in those four logs. Test processes have exited. Development is stopped for user review, not a full V2 release declaration.

### Blender-refined Cyclopse skin — 2026-09-15

- Same-build v1monster, attackpose, doorclearance, walk, hwacat, attack, flow, access and ambience audits all returned exit 0 (`../outputs/unity-<audit>-refined/`). The two-enemy route completed four investigations, Hwacat transformation and escape in 2391 movement updates with one attack. These nine targeted checks do not replace full animation/frame/performance acceptance; no error/exception matches were found in these run logs.

- Current scene is `Assets/Generated/SchoolV2 16.unity`; build `../outputs/unity-refined-skin-build.log` succeeded. Cyclopse now uses derived FBXs under `Assets/Art/Refined/Cyclopse/`, with editable `SourceArt/Cyclopse_refined.blend`. Original V1 FBXs still hash-match the supplied snapshot; texture, topology and UV layout are retained by the refinement script. Room geometry and gameplay rules are unchanged.
- Blender inspection reproduced severe deformation in the original animation, before Unity: neighboring head/arm weights changed sharply along short edges. In four sampled frames (1, 8, 16, 24), maximum posed/rest edge ratios dropped from 43.02 to 6.22 for Walking and 42.98 to 6.89 for Run after geometric weight smoothing. Detailed before/after counts: `../outputs/cyclopse-skin-refinement/weights.json`. The metric does not prove all frames are free of stretching.
- Larger-influence blending is preserved on import and at runtime to avoid reintroducing hard weight discontinuities. See [Unity skin weight import documentation](https://docs.unity3d.com/ja/2022.3/ScriptReference/ModelImporterSkinWeights.html). Profiling the additional skinning cost remains open.
- In-game attack/locomotion captures show reduced sharp shoulder stretching; the normal-texture identity remains. Locomotion still changes the leg pose, attack anticipation/reach/recovery works for both V1 actors, sampled ground gaps are zero and the 128-pose door test fits the 2.4m opening. Side folds/gray surfaces still need further mesh/UV review: this is an improved skin, not final model acceptance. Diagnostic attack images use fill lighting; the locomotion image uses the test's normal scene lighting.

### V1 skeletal attack posing — 2026-09-15

- `V1AttackPose` layers authored arm anticipation/reach/recovery onto the original Cyclopse/Hwacat angry skeletons. It restores the prior local rotations before each animation evaluation to avoid accumulating offsets, and does not modify original FBXs or locomotion clips. The last 0.18 seconds of the existing 0.75-second warning bring the reach forward; existing hit timing/range and 0.9-second recovery remain unchanged.
- Initial full humanoid reaches visibly stretched Cyclopse's torso. That candidate is superseded by bounded model-specific rotations: Cyclopse shoulder 18°, elbow 30°, no added spine twist; angry Hwacat uses a larger but limited reach. Current build: `../outputs/unity-attackpose-bounded-build.log`.
- `../outputs/unity-attackpose-bounded/attackpose.json` passed both actors' rig/anticipation/strike/recovery and controlled-dodge survival checks. Right-hand travel between sampled windup and strike was 0.271 m / 0.349 m; sampled ground gap was zero. Screenshots were inspected after moving the diagnostic camera inside the corridor. They use an added inspection fill light, not normal gameplay lighting.
- **Not final animation acceptance:** Cyclopse still shows stretched/flat side triangles, also visible in the recovered locomotion pose. Its inherited skin weights/mesh need a dedicated Blender inspection and refinement; bounding the new attack is not a re-skinning fix. Angry Hwacat's reach reads more clearly, but contact alignment, every attack frame and normal-light telegraph readability remain open. This is procedural posing, not a finished authored attack clip.
- Bounded build attackpose, attack, v1monster and walk audits all returned exit 0 (`../outputs/unity-<audit>-bounded/`). Stationary attack timing, original skeletal locomotion and the two-enemy escape route remain intact. Prior unbounded-pose regression evidence is not substituted for this final-build check; UI/Hwacat event checks on the preceding build are under `*-pose/`.

### Spatial room ambience — 2026-09-15

- Same-build ambience, walk and flow audits all returned exit 0 (`../outputs/unity-<audit>-ambience/`). The existing two-enemy route still completed four investigations, Hwacat transformation, one attack and escape in 2391 movement updates. UI flow retained 14 boolean checks and seven rendered frames. No error/exception matches in these run logs; subjective audio quality is not established by these checks.

- Added three restrained room-local emitters: washroom drops, infirmary electrical wiring and classroom draft. Drop/drone synthesis adapts V1 SoundManager; the draft is original filtered noise. No external audio was downloaded. Sources use 3D distance falloff, lower priority than enemy cues and zero Doppler.
- Every 0.2 seconds a listener-to-source ray checks obstruction; closed geometry reduces gain to 28% and low-passes to 850 Hz, interpolated to avoid abrupt switching. This is a simple single-ray approximation, not acoustic propagation through doorways. Chase halves the ambience mix to leave room for enemy cues; restoration fades it to 25%. Existing global menu pause and master volume apply.
- Build: `../outputs/unity-ambience-build.log`. `../outputs/unity-ambience-ambience/ambience.json` passed spatial-source configuration, finite/non-clipping generated clip data, unobstructed room/closed-door gain and filter probes, paused mixing, resume and restoration quiet. Probes relocate the controller-disabled player and directly advance story state: this is a controlled mixing test, not a listening session or full playthrough. Headphone/speaker audibility, subjective mix quality, loop repetition and hardware audio latency remain unverified.

### Sprint exhaustion and recovery — 2026-09-15

- Same-build stamina, walk, flow, hiding and attack audits returned exit 0 (`../outputs/unity-<audit>-stamina/`). The existing AI-on two-enemy route still escaped with four investigations, completed Hwacat transformation, one attack and 2391 movement updates. Flow retained all 14 checks/seven renders. This is targeted regression coverage, not an exhaustive difficulty or animation acceptance pass.

- Removed the `Stamina > .05` sprint oscillation: after exhaustion the player walks until stamina is at least 25% and Shift is released. Pressing Shift again begins a new sprint. Walk/run speeds and drain/recovery rates are unchanged; paused UI still freezes stamina. HUD text distinguishes recovering from ready-to-release, with an exhaustion bar color as a secondary cue.
- `../outputs/unity-stamina-build.log` built successfully. `../outputs/unity-stamina-stamina/stamina.json` passed seven checks through virtual W/Shift input: initial sprint, exhaustion, no resumed pulses during 2.5 seconds of held Shift, pause freeze, recovery above 25%, release rearming and renewed sprint. The test does not inject stamina or move the actor; it eventually reaches a wall, so this is a stamina/input-state test, not a traversal performance test. The changed HUD text has not yet had a dedicated rendered visual review.

### V1 Hwacat reveal grounding — 2026-09-15

- Baseline live-event measurement found the normal model's lowest rendered vertex ranging from -0.1847 m to +0.2818 m relative to the y=0 floor (`../outputs/unity-hwacat-ground-baseline/hwacat.json`). V1 already had a snap-to-ground stage, which was missing in this Unity reveal.
- Added `V1RevealGrounding` to the existing event at runtime, preserving the current scene, original FBXs, animation poses and event actor position. The component compensates imported FBX scale, measures skinned vertices and applies a bounded vertical visual offset after animation evaluation. It does not move the gameplay actor or change AI/trigger timing.
- Build `../outputs/unity-reveal-ground-build.log` succeeded. The 60 FPS opt-in event audit sampled 307 stand/dance frames with lowest vertex within 0.000001 m of the floor; actor position stayed unchanged. Mid/late dance renders were inspected. Evidence: `../outputs/unity-hwacat-ground/`.
- This covers the stand/dance segment actually played in the current event, not the entire unused source dance clip, foot sliding, all individual toes/hands, other terrain or final animation quality. Per-frame skin baking and overall performance remain to be profiled. The original V1 snapping behavior is retained rather than introducing new jump semantics.
- Same-build `hwacat`, `walk` and `flow` runs returned exit 0 (`../outputs/unity-<audit>-ground/`). Walking completed four investigations and escape with both enemies active, Hwacat complete, one attack and 2391 movement updates. UI flow retained all 14 boolean checks and seven rendered frames. These are targeted regressions for this visual-only change, not a fresh run of every independent audit.

### Rendered UI, settings and journal — 2026-09-15

- Current build: `../outputs/unity-ui-verified-build.log` (exit 0). Runtime UI Toolkit now renders menus/HUD through `GameShellView`; `GameShell` owns state, pause and settings. Enter submits the focused UI button once, rather than also triggering a separate start action.
- `../outputs/unity-ui-verified/flow.json` passed 14 boolean checks and seven non-black UI captures. Pointer events selected settings and changed volume/sensitivity; saved values were loaded by a fresh scene/player. The opt-in audit restores the pre-test preference values and key presence, including on its timeout path.
- Actual UI panel captures were inspected: Korean journal at 1600×900, 1280×720 and 1024×768; settings and death screen at 1600×900. Smaller journal layouts kept every label/button inside the panel. This supersedes the black IMGUI capture limitation below. Non-black detection alone is not visual acceptance; screenshots were separately reviewed.
- Scope limits: pointer events are injected into the UI panel, not physical OS mouse clicks; settings persistence is checked across a scene restart, not a separate process launch. This does not prove complete HUD/world composition, every page at every resolution, accessibility or final typography. OS Malgun Gothic remains a Windows font dependency.
- Same-build game regressions all returned exit 0: walk, hwacat, v1intro, attack, hiding, smoke, access, doorclearance and v1monster (`../outputs/unity-<audit>-ui/`). No exception/error matches in those run logs. The AI-on walk completed four investigations, Hwacat transformation, maximum two active enemies, one attack and escape in 2388 movement updates. These retain the controlled-test/scripted-route scope described below, not an overall release acceptance claim.

### Earlier game flow and journal — 2026-09-15 (superseded UI renderer)

- Final build: `../outputs/unity-shell-final-build.log` (exit 0). Final flow audit: `../outputs/unity-flow-final/flow.json` (all nine behavioral checks true, `uiFramesRendered=0`). The final change only added capture-validity reporting to the audit; gameplay/UI code is the same as the nine successful `*-shell` regressions below.
- Added GameShell: title/start, pause/resume, discovered-record journal, volume/sensitivity settings, death/clear and restart/title navigation. GameSession owns whether gameplay input is allowed; PlayerMotor no longer independently toggles pause. Menus freeze scaled time and AudioListener playback; J opens/closes the journal, Esc pauses/resumes, Enter starts from the title.
- Settings use local PlayerPrefs. The existing standalone audits call the same Begin path automatically; `-v2-flow-output` retains the title and exercises Enter/J/Esc with virtual keyboard input.
- `../outputs/unity-flow/flow.json` verifies title input rejection/frozen movement, start, journal information gating, frozen movement/stamina/audio while reading, resume, pause, death screen state and clean scene restart. Existing walk/hwacat/v1intro/attack/hiding/smoke/access/doorclearance/v1monster regression runs all returned exit 0 under `../outputs/unity-<audit>-shell/`; two-enemy walking escape remains intact.
- **Visual UI verification is NOT complete.** Hidden-window ScreenCapture produced black images. FlowAudit now separately records `uiFramesRendered`; exit 0 covers behavior only, not visual rendering. The IMGUI UI is a first implementation, not final polished UI acceptance. Next work must provide a reliable actual UI render (e.g. a runtime UI panel with an offscreen render target), inspect text/focus/layout at multiple resolutions, and test mouse/settings controls and persistence. Do not treat camera-only world renders as proof of HUD/menu appearance.

### Hwacat and physical/visual integration — latest 2026-09-14

- Final integration build returned exit 0 (`../outputs/unity-v1-integration-build.log`). All nine standalone audits returned exit 0: doorclearance, v1monster, hwacat, walk, v1intro, attack, hiding, smoke, access. Evidence is under `../outputs/unity-<audit>-integration/`; the logs contain no error/exception matches. Full walk records four investigations, completed Hwacat transformation, maximum two enabled enemies, one attack, 2400 movement updates and successful escape. This remains a scripted-route test, not a complete human difficulty/UX review.
- The 128-pose door sample stayed within x=-0.940..1.068m and height 1.886m; all three twin doors moved and the edge-close guard passed. The sampled chase frame's compensated mesh sole was at floor y=0. Startup/portrait/angry renders were inspected after UV correction. All seven newly copied Hwacat/angry/portrait files match the V1 originals by hash.
- Current scene is `SchoolV2 15.unity`. V1 Hwacat's original portrait, standing, stand-up, dance and angry Zombie Run assets are now used in the infirmary. Reading the medical record drops the portrait, plays the normal-model sequence, swaps to the angry model without opaque overlap, and activates a second pursuer. Restoration resolves both threats. The room sizes have not changed.
- Cyclopse had a wider rendered body than the former 1.8m doorway. All three openings are now 2.4m with two 1.19m leaves moving into opposite wall pockets. Fixed latches remain accessible, and close protection covers the full opening rather than only its center. Image-face UVs keep door and portrait textures upright.
- Animation grounding samples the actual skinned mesh and offsets the visual only; the controller/NavMesh actor is not teleported. FBX scale compensation matters for this asset's approximately 95x imported hierarchy. See the [Unity BakeMesh reference](https://docs.unity3d.com/ScriptReference/SkinnedMeshRenderer.BakeMesh.html). The previous uncompensated sampler was rejected after measurements disagreed with the render.
- The first two-enemy scripted route failed while resting after waking Hwacat. Updated the input-driven route to recover stamina before reading the record and leave during the transformation. The revised route completed with `hwacatCompleted=true` and `maximumActiveEnemies=2` in `../outputs/unity-walk-double-door/walk.json`. No enemy disabling or player teleporting is used by this walk test.
- `DoorClearanceAudit` samples 64 poses from each Cyclopse clip and compares its envelope with the opening, then checks both leaves and edge-close protection. This is not proof of all arbitrary turns/body contacts around every desk. General wall/corner clearance, normal Hwacat foot contact across its full dance, final attacks, UI/title/journal and atmosphere/performance work remain open.

### V1 content direction and migration — 2026-09-14

- Current build log: `../outputs/unity-v1-final-build.log` (exit 0). Standalone v1monster, v1intro, walk, attack, hiding, smoke and access audits all returned exit 0; results are in `../outputs/unity-<audit>-v1-final/`. Full walk: storyStep 4, escaped true, 2565 physical movement updates, one attack. No error/exception matches in these run logs. This supersedes the prior mannequin/warden build evidence for current gameplay.
- User explicitly requested existing V1 monster models/events. The experimental attendance-warden Blender source remains available but is not instantiated by the current scene builder. `SchoolV2 11.unity` uses V1 Cyclopse.
- Copied the original Cyclopse Walking/Run FBXs and texture without changing their bytes (source/copy SHA256 comparison passed). Unity derives smoothed normals, URP skin and looping clips with root drift removed, including V1's run vertical lock.
- `V1MonsterAudit` confirmed the textured 28-bone mesh and changing leg pose while the chase clip advances, with a rendered in-game capture. This checks one animation interval, not every frame or all collision clearances.
- `V1CyclopseIntro` ports the V1 advance/roar/AI-handoff sequence and synthesized descending roar. It adapts distances to this corridor and does not teleport or lock the player camera. `V1IntroAudit` directly triggers story stages and verifies completion, emitted roar and active AI. It is a controlled event test, not a playthrough.
- Further work: remaining V1 character/event ports, proper attack clips, visual/body clearance through doors, foot contact polish, all room visuals, HUD/title/journal, settings, progression pacing and release packaging. The complete V2 goal is still open.
- Provenance and external-asset policy: `ART_SOURCES.md`. External publicly available assets may supplement V1 content only after license/redistribution checks.

`DevelopmentSmoke` runs only with `-v2-smoke-output <directory>`. It checks grounding, NavMesh availability, missing shaders, a moving door and collection/escape wiring, and captures a frame. It deliberately bypasses travel for collection; it does NOT prove survival completion.

The engine import/build logs are stored in the parent workspace's outputs directory. Do not treat source files or exported FBXs alone as successful engine execution.

### Verified 2026-09-14 — local playable milestone

- Windows development build completed with Unity 6000.6.0f1; active scene remains `Assets/Generated/SchoolV2 9.unity`. Build log: `../outputs/unity-local-final-build.log`.
- Full AI-on `WalkAudit` completed all four investigations and escaped through the entrance (exit 0, storyStep 4, escaped true, 2369 physical movement updates, one enemy attack). The route opens the infirmary in advance, closes its door to recover stamina, sidesteps the attack cue, waits for the strike to miss, and returns to the classroom. Result: `../outputs/unity-walk-local-final/walk.json`.
- The walk audit uses virtual keyboard/mouse input and normal interaction focus, with no actor teleporting, direct collection calls or enemy disabling. It still uses computed NavMesh routing and programmatic aiming; it proves this route can complete, not that first-time-player difficulty or every route is validated.
- Fixed hidden-window audit input being disabled when unfocused. `IgnoreFocus` is configured only inside the opt-in walk test, not normal gameplay.
- Enemy strikes now have a 0.75-second warning and 0.9-second recovery. Standalone AttackAudit confirms a stationary target survives the warning but is caught by the strike; the full walk demonstrates a successful dodge. This is audio/behavior telegraphing, not a finished attack animation.
- Smoke, Access, Hiding and Attack regression runs all returned exit 0 on this same build. All nine access entries passed; smoke reports 399 renderers, no missing materials and no captured errors. Hiding confirms unseen entry safety, witnessed capture and spatial footsteps. Results: `../outputs/unity-{smoke,access,hiding,attack}-local-final/`. No exception/error matches in these final run logs.
- Reviewed the startup camera render. It is not a HUD capture or a full visual acceptance pass. Existing texture orientation, animation, UI and production-art limitations remain in scope for later development.
- This closes the current local playable/test milestone, not the complete polished V2 release. No cloud transfer or GitHub push was performed.

The second standalone walk run also returned exit 0 with storyStep 4 and escaped true: `../outputs/unity-walk-local-repeat/walk.json`. This reproduces one scripted route; it is not an independent player strategy.

### Historical verification 2026-09-13 (superseded where noted above)

- Current build uses `SchoolV2 9.unity`. After furniture tops were removed from the NavMesh, regression checking exposed an unreachable preparation box beside a classroom chair. Moved crate/box to the free rear corner. All 9 AccessAudit entries pass again with no captured runtime errors. Full AI-on walking/escape remains unproven.
- Latest geometry/build is `SchoolV2 8.unity`. Added distance-driven spatial enemy footsteps (original synthesized audio). Hiding now records actual line of sight at entry, not stale chase memory. Vision checks head and torso. A witnessed hiding capture requires reaching the cabinet entrance; an unseen entry remains safe. Moved the cabinet deeper into the infirmary to give both player and enemy a usable approach.
- `HidingAudit` (`-v2-hiding-output <directory>`) passed in the standalone player: occluded entry safe, exit usable, visible entry witnessed and subsequently caught at the entrance; spatial audio configured and a footstep emitted during the approach. The cases deliberately reposition actors and invoke hiding directly; this is rule-level verification, not a survival playthrough. Result: `../outputs/unity-hiding/hiding.json`.
- `WalkAudit` (`-v2-walk-output <directory>`) drives virtual W/E input through the normal PlayerMotor, uses the real CharacterController and interaction focus, and never calls Use/Collect or teleports. Live AI/events remain enabled. Latest run physically reached and investigated register and ribbon, then was caught on the route to the infirmary at (4.67, .08, -.008). This is NOT full survival completion; results: `../outputs/unity-walk/walk.json`.
- Walking exposed two real defects missed by point-access tests: NavMesh routed over desk tops (now all non-floor furniture surfaces marked Not Walkable); downward interaction rays hit the player's own collider (player now on Ignore Raycast, interaction uses default mask, enemy rays still explicitly include player). Added a center aiming dot with focus color.
- Latest generated geometry: `SchoolV2 7.unity`; latest executable also includes the self-ray fix. Earlier scene numbers below are historical verification snapshots.
- Latest scene/build: `Assets/Generated/SchoolV2 6.unity`. AccessAudit (`-v2-access-output <directory>`) verified all 9 interactions with a complete NavMesh path, clear standing capsule and unoccluded <=2.2m ray to the correct interactable. Doors are opened programmatically for this audit; it does not prove walking or survival completion. Results and clue-view renders: `../outputs/unity-access/`.
- Fixed disappearing door controls with fixed frame latches. Moved the ribbon onto the actual basin deck and medical record onto a wall clipboard. Split sink collision into component boxes so its empty upper space does not occlude the ribbon. Separated floor material from furniture wood and reduced blown-out flashlight highlights.
- Unity 6000.6.0f1 imported assets, compiled scripts, generated `Assets/Generated/SchoolV2 3.unity` and built `Builds/Windows/HappyToyV2.exe` successfully.
- Standalone smoke run returned exit code 0: grounded, NavMesh available, moving door, ordered four-stage story/escape wiring, no missing shaders and no captured runtime errors. `../outputs/unity-smoke/smoke.json` contains the result.
- Explicit URP camera rendering produced `../outputs/unity-smoke/first-frame.png`. Hidden-window screen capture alone was black and was replaced with a render request. This capture does not include OnGUI HUD.
- Fixed an extremely-high-FPS grounding problem by moving CharacterController motion to FixedUpdate. Initial enemy is inactive until its story beat, avoiding premature NavMesh initialization.
- Current materials reuse four V1 surface textures. Textured character material preservation, door texture orientation, consistent wall trim mapping, detailed room visual review and full survival playthrough remain open, alongside the full production scope above.
