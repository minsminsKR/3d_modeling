# First-person hands and item actions — 2026-10-25

## Scope and quality status

This implements the user's five-item animation request on the restored original
hand surface. The rejected `PlayerHandsV2` mesh/skin is still inactive. Hand colour,
UVs, nails and topology are retained, with independent forearm/wrist weights and
authored Actions added. A successful rule, rig or build check is not approval of
commercial-quality skin or completely penetration-free animation.

The modeling CLI was checked against the designated service. Capabilities and
doctor both returned `AUTH_REQUIRED` (401). No generation was submitted, no login
state was disclosed and no alternate generator was used. This task uses the
retained generated original, so generation authentication is not a dependency.

## Cause of the original motion

The previous hand used a single long wrist bone, one .55-second cosmetic pulse,
and iterative per-frame finger solving. All items took effect before that pulse.
Snappers spawned near the eyes, and traps appeared fully armed on the floor while
the small held prop disappeared. Item selection could replace the visible grip
immediately. A world flashlight fixed to the camera also disagreed with the
visible lamp's axis.

## Ownership and data flow

- `author_hand_animation.py` opens the retained `.blend`, changes only relevant
  weights/forearm hierarchy, authors 22 Actions and samples their actual curves.
  The editable source, inputs, hashes and reproduction instructions are in this
  folder. The `.fbx` and bounded pose/clip JSON are under
  `Assets/Resources/PlayerHandAnimation/`.
- `HandAnimationLibrary` validates and loads the authored samples;
  `HandAnimationRig` applies them to the actual imported bind rotations. It does
  not mistake Blender rest quaternions for FBX local rotations. Props inherit
  palm rotation so wrist motion does not rotate fingers around a fixed item.
- The FBX export clears the preview pose before exporting. Exporting the Relaxed
  preview had baked its curl into the imported bind, then applied a second curl
  at runtime. Exact neutral-bind calibration and an import regression check now
  reject that error. `HandCylinderContact` measures the real torch barrel once
  and permits only a small, anatomically bounded six-degree finishing correction.
- Independent Core `HandActionTimeline`, `HandEquipTransition` and
  `HandMotionState` own timed markers, hidden swaps and bounded damped inertia.
  They depend on neither Unity nor the game session.
- `FirstPersonHands` presents both rigs/props, samples Actions, adds restrained
  locomotion/turn/foot-contact motion and renders the existing isolated hand
  layer. The null-device CPU diagnostic path does not allocate a render target.
- `PlayerHandActions` owns a requested use. It preflights without consuming stock,
  waits for equip, renders the exact release marker and invokes the existing
  inventory once. Inventories continue to own stock, cooldowns, physics and
  effect lifetime. The old immediate owner APIs and `UseSelected` remain for
  scripted interactions/older probes; actual LMB/Q/R/F input uses requests.
- Pause and the item wheel hold pending uses. Selection/hidden state, world
  replacement, large transfer and in-place inventory/checkpoint restore cancel
  uncommitted intent. No half-committed action is stored or guessed on reload.

No new Animator/Rigging package is installed: the previous controller already
owns these transform bones. Baked Blender grip/use/equip samples avoid having
competing bone writers. Runtime calculations add inertia and bounded transitions;
they do not procedurally invent the complete grasp.

## Five item behaviors

| Item | Authored use / commit | Integration |
|---|---|---|
| Flashlight | .36s / .16s | Thumb press and switch/foley share one marker; the actual light origin/axis follow the visible lamp with FOV/aspect conversion and a wall-origin guard. |
| Snapper | .85s / .32s | Small thumb/index/middle grip; staggered opening, windup/reach/recovery; projectile originates at the projected visible hand, with the existing speed, gravity, collision and AI response. |
| Incense | .65s / .26s | Fine pinch, restrained wrist motion and delayed ignition; existing route guidance stays separate from the continuous plume, updated after the current hand pose. |
| Talisman | .75s / .30s | Upper-corner pinch, pinned top and millimetre lower-sheet flex; original top-to-bottom clip/char/glow retained, plus moving flame edge and owned pooled smoke/ash. |
| Trap | 1.15s / .68s | Full world-sized trap supported at its base; fixed validated floor destination, visible lowering and matching release before one armed world object appears. Placement is rechecked at commit. |

The existing talisman ten-second lifetime, time-stop, item counts, saves, route
guidance and enemy rules are retained. Turning, acceleration, walking/sprinting
and stopping use bounded analytic spring motion; Reduced Motion suppresses the
additive presentation and the corresponding throw prediction together. Equip
switches props at a lowered hidden midpoint. After the last talisman finishes
burning, the empty hand follows the authored unequip pose down over .30 seconds
before hiding; pause also holds this recovery.

The trap support check now measures actual vertices within 45mm of the animated
palm contact marker, using that marker's surface normal. Extending an infinite
tilted plane through the opposite end of a 55cm trap is not a hand penetration
test. The final neutral-pose CPU audit measured local support within 2.2mm of
the palm, while that remote frame crossed the old plane by roughly 50mm.

## Unity setup and reproduction

1. Reproduce the Blender export with the pinned command in `README.md`.
2. Run `HappyToy.V2.Editor.PlayerItemsImport.ApplyHand` in the pinned Unity
   6000.6.0f1. It retains the hand prefab/material GUIDs and original texture
   references, and imports the new articulated FBX as a readable generic rig.
3. The existing `PlayerMotor` attaches the hand-action coordinator. No scene
   wiring, hand-authored absolute path, new package, or manual Animator setup is
   required.
4. Run `Tools/quality/run_feature_tests.py --profile hand-animation-logic` for
   focused CPU/null-device source/skin/scene checks. This profile does not certify
   pixels, native animation appearance, sound-device playback or FPS.
5. `player-hands` remains the separate actual rendered camera/aspect review and
   is deferred while the user is gaming. Native player/FPS tests are also deferred.

The test CLI itself supplies its reserved `-batchmode` flag. Passing that same
flag again after `--` is rejected by Unity CLI; the runner records CLI-managed
batch mode and separately requests hidden windows, low priority and input/cursor
isolation. CPU scene diagnostics omit the title's render requests only on the
explicit null-device diagnostic path; ordinary title playback is unchanged.

## Remaining visual limits

The retained generated skin is not a scanned hand, and its close-up surface has
not been replaced. The forearm ends at an elbow-side control without shoulder or
upper-arm body geometry. The thumb switch touch is still an approximation.
Skeletal pad/contact distances are not proof that every skin triangle avoids
every prop for every pose. Viewmodel-to-world trap outline matching under
different FOVs is approximate, although its release position is fixed and
validated. CPU geometry stills use approximate materials; actual Unity shader
appearance, native movement and performance require a separately requested GPU
review while it will not interrupt the user's work.

Fresh verification/build results and actual reviewed-frame hashes are retained
alongside the delivered build. Earlier failed attempts remain in Builds for an
honest record; they are not counted as passing checks.

## Changed and added files

Paths below are relative to the Unity project. Every new Unity script, shader,
folder and resource also includes its `.meta`; existing script/prefab GUIDs are
retained.

| Responsibility | Files |
|---|---|
| Editable art and reproduction | `SourceArt/PlayerHandAnimation/{hand-animation.blend,author_hand_animation.py,authoring-report.json,README.md,IMPLEMENTATION.md,review_cpu.py,render_unity_cpu_review.py}`; repository `.gitattributes` for the Blender LFS source |
| Imported authored data | `Assets/Resources/PlayerHandAnimation/{geometry.fbx,poses.json}`; `Assets/Resources/PlayerItems/hand.prefab`; original `hand/{held,nail,surface}.mat`; `Assets/Editor/PlayerItemsImport.cs` |
| Independent rules | `Assets/Scripts/Core/{HandActionTimeline,HandMotionState}.cs` |
| Rig, presentation and action ownership | `Assets/Scripts/{HandAnimationLibrary,HandAnimationRig,HandCylinderContact,FirstPersonHands,PlayerHandActions}.cs` |
| Existing item/input integration | `Assets/Scripts/{PlayerMotor,PlayerItemLoadout,PlayerFlashlight,FirecrackerInventory,FirecrackerAim,MonsterTrapInventory,PlayerRitualInventory,IncenseGuidance}.cs` |
| Paper | `Assets/Resources/{PlayerItemSurface,HeldPaperParticles}.shader`; `Assets/Scripts/HeldPaperEffects.cs` |
| Explicit null-device diagnostics | `Assets/Scripts/{DiagnosticDesktopPolicy,CinematicTitleScene,GraphicsLightingPresentation,SchoolHorrorLighting}.cs`; `Assets/Editor/HandAnimationCpuReview.cs` |
| Regression checks | `Assets/Tests/EditMode/{HandAnimationAssetTests,Core/HandActionTimelineTests,Core/HandMotionStateTests}.cs`; `Assets/Tests/PlayMode/{HandActionIntegrationTests,PlayerHandsTests}.cs` |
| Verification tooling and handoff | `Tools/quality/{run_feature_tests.py,retry_failed_tests.py,test_run_feature_tests.py,README.md}`; `CODE_MAP.md`; repository `MEMORY.md` |

Unrelated existing creature-face/source-mask working changes are outside this
hand-animation change. Build outputs, CPU renders/OBJ previews, CLI cookies,
machine state, `.blend1` backups and Python caches are excluded from source.

## Verification performed

- **104 EditMode cases passed**: independent Core rules plus the actual imported
  neutral bind, hierarchy/weights, all authored channels/markers and finite baked
  skin. The standalone 23 hand-rule cases are included in that count, not added
  again.
- **10 PlayMode cases passed**: nine cases from the focused full scene run and
  the corrected Reduced Motion test setup in a fresh isolated run. This covers
  actual item owners, release/placement, pauses/wheel/cancellation/restoration,
  five grips, current incense tip, paper clipping/flames/particle pause, and the
  final paper's full ten-second lifetime and empty-hand recovery. The earlier
  fixture setup failure and all earlier failed runs are retained in Builds.
- **16 Python verification-runner cases passed**. The CLI-managed batch flag,
  hidden low-priority startup and captured CLI failure logs were checked.
- **10 final CPU poses reviewed**: final Unity export's OBJ hashes and material
  records match the reviewed CPU stills exactly. All central-rectangle occupancy
  counts are zero. Compensated BakeMesh output matched independently sampled
  weighted skin positions within .229mm; the exporter rejects double scale.
- **Source completeness passed** after staging: 5,907 required source files,
  no missing indexed authoring file and no unresolved local LFS pointer.

These are focused source, rule, scene-logic and CPU geometry results, not the
complete Unity suites or a native GPU visual/performance approval. The detailed
XML, aggregate report, source/input hashes and CPU review records accompany the
new build under its `HandAnimationVerification` directory.

The release Windows player `Builds/HandsAnimation-20261025-180818/HappyToyV2.exe`
was built with pinned Unity 6000.6.0f1 in hidden null-graphics batch mode. The
build command exited 0 and emitted `HAPPYTOY_QUALITY_BUILD_PASS`. The fresh
full-input/graphics verifier returned `GRAPHICS_BUILD_INPUTS_PASS`, with no
errors. The executable was not launched during this task. Existing latest-link
and old-build grouping follow the standing workspace policy after verification.
