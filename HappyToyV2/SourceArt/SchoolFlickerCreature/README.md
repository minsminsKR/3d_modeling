# School flicker creature — concept reference

Created 2026-10-28 with the built-in imagegen tool for the HappyToyV2 school
anomaly. Video inspiration: `../SchoolAnomalies/Reference/user-reel-capture-20261028.mp4`,
a user-provided recording of https://www.instagram.com/reel/DehELDOvhj2/ .
`Reference/reel-contact-sheet.png` is the actual video-derived contact sheet supplied
to imagegen (SHA-256 `a23676043421a286fecb53a569b29770aff332d856c61f0720a926e551c5d898`).
The generated concept is an original design, not a frame from the reel. The
concept is a 3D modeling input; it is not itself a game-ready asset.

`Reference/concept-original.png` SHA-256:
`bfb7233c6ae5879fc83b02809767c6edecd76cc57a7d56056fce99a245fcb305`.

Prompt:

> Use case: stylized-concept. Create one full-body creature design reference for an original 3D survival-horror game enemy, using the attached video contact sheet only as a motion and silhouette reference. One single creature, centred, front three-quarter view, neutral A-pose, complete head to feet visible with generous margins against a plain light gray background; no text, no scene or dramatic red light, no duplicate views. Preserve the visual idea of a large bulbous hunched head and upper torso, emaciated narrow waist, two long crooked legs with large feet, two long asymmetrical arms with distinct clawed hands, and several thin sinewy spine/tendril structures emerging from the shoulders and back. The material is dark charcoal-black wet organic skin with subtle glistening highlights; disturbing, imposing, uncanny but not graphic or gory. Keep the limbs visually separated from the torso and avoid interlocking or tangling, usable as an image-to-3D modeling and rigging reference. Lighting neutral and even to show detail. Original design, inspired by the silhouette and atmosphere, not an exact recreation.

## Pending model and integration

Target visible span is approximately 2.2 m in the 4.4 m-wide school corridor;
height should remain within the 3.25 m ceiling with clearance for animated poses.
The concept uses a wet dark organic surface, long bent limbs, and a swollen upper
body informed by the user reference. Shape/rig are not yet generated: the A100
CLI returned AUTH_REQUIRED even after the web session was opened. No substitute
base mesh or model generation has been performed. After CLI authentication,
query capabilities and doctor, create one idempotent request on the designated
service, visually inspect the same job in the existing webapp, and retain the
original files, authoring sources, licenses, settings, and job provenance.
Normal passages must keep the ordinary pedestrian; the new actor belongs only
to the red-flicker anomaly. Reconcile the rig, collider, clearance, visual motion,
LOD, checkpoint and stop-distance behavior with the actual resulting model.

## Resume handoff — 2026-10-28

**Status:** only the original concept image and this handoff exist. No 3D
creature, rig, Unity asset or in-game replacement has been generated or applied;
no model-generation request or job was submitted. The existing red-flicker
pedestrian anomaly remains playable. The latest source and reference video were
already on `origin/main` before this handoff.

1. Restore CLI access to the user-designated A100 service using the documented
   hidden `login` prompt or a user-provided protected password-file path. The
   saved browser form opened the web workspace successfully, but the independent
   CLI session still returned HTTP 401 `AUTH_REQUIRED`. Never print or commit a
   password, browser cookies or private CLI state. Use the current user-provided
   tunnel origin; it may change. Then run `capabilities --json` and `doctor --json`
   and use their live supported models, GPU readiness and option defaults.
2. Submit `Reference/concept-original.png` with `modeling_cli.py` on that same
   service. Record the original request UUID, job ID, actual options, inputs and
   hashes. Use the same request UUID for uncertain submissions, then use
   `collect`/`resume` and verified cached downloads, not duplicate generation.
   Keep the original GLB/PBR, reference and generation record in this SourceArt
   tree; preserve licenses and provenance. A temporary server job is not a handoff.
3. Open that job's `web_url` in the existing sidebar browser and visually inspect
   the real model from multiple angles; inspect materials, rig joints and motion.
   Where useful, rig or correct joints on that same job and collect with `--refresh`.
   Base regeneration must use the designated service rather than a substitute mesh.
4. Finish the actual returned mesh and rig in Blender using reproducible tools,
   without altering the user's open session. Inspect joints, weights, silhouette,
   skin deformation, UVs and PBR; produce a packed editable `.blend`, appropriate
   game LODs and export sources. `SourceArt/SchoolWalker/AUTHORING.md`,
   `inspect_character.py` and `finish_character.py`, and `Assets/Editor/
   SchoolWalkerImport.cs` provide patterns, but their paths, proportions and bone
   names are specific to the existing person: adapt them for the new model rather
   than running them unchanged or overwriting that person's assets.
5. Integrate the creature only for the `DancingWalker` anomaly; ordinary passages
   must retain their existing human and walking animations. `SchoolLoopWorld`,
   `SchoolAnomalyDirector`, `SchoolWalkerEncounter`, `SchoolWalkerMotion` and
   `SchoolWalkerFlicker` identify the current boundaries. Add a creature-specific
   prefab/importer and motion owner based on the actual rig rather than assuming
   the person's 34 bone names. Preserve the established red hold/upper-body tremor
   and dark-pulse limb twisting/step timing, pause and talisman behavior.
6. Fit the animated creature's maximum silhouette in the real 4.4 m-wide,
   3.25 m-high corridor, targeting roughly 2.2 m apparent width and 2.5–2.8 m
   height while leaving wall/ceiling clearance. Reconcile its physical capsule and
   `SchoolWalkerRoute` stopping distance, occlusion bounds and obstruction behavior;
   test normal human restoration and ensure old and new v3 walker/checkpoint states
   resume correctly without changing the anomaly judgment rules.
7. Update `CODE_MAP.md`, feature profiles and handoff once ownership and actual
   assets are known. Run `school-anomalies-logic` plus applicable integration
   checks, a pinned Unity player build and source-completeness audit. Inspect the
   actual model's appearance and animation in the game and performance only when
   consistent with the desktop/GPU isolation instructions in `AGENTS.md` and
   `MEMORY.md`; headless rules alone do not certify visual or audio quality.
   Track the full original input, service and Blender sources, exported Unity
   assets with `.meta`, reproducible tools and attribution. Group superseded builds
   only after the new build is verified, as the project instructions require.

## Face reference update — 2026-10-28

The user requested the red-flicker creature use the face in
`Reference/user-face-reference.png`, a face source previously used for an NPC.
`Reference/concept-face-reference.png` is the new image-to-3D concept: the built-in
image generation tool edited `concept-original.png` using the user photo as the
face reference. Prompt: "Edit the first image, a full-body horror game creature
modeling reference. Replace only its face/head front with the recognizable human
face from the second image: preserve the person's facial proportions, eyes, nose,
mouth, hair and likeness, adapted naturally into the creature's dark organic head
with an uncanny threatening expression. Keep the original creature's body,
silhouette, limbs, claws, back tendrils, full-body composition, neutral gray
background and lighting intact. One creature, full body visible, suitable as
image-to-3D reference, no text."

Use this new concept as the image input for the pending generation workflow above.
On this attempt both capabilities and doctor returned HTTP 401 AUTH_REQUIRED for
the designated server. No generation request was submitted and no 3D or Unity
replacement exists yet. Restore the CLI login before proceeding; do not treat the
concept as an in-game result. Preserve the original concept and face source.

## Flower and facial hair revision — 2026-10-28

`Reference/concept-face-flower.png` supersedes the earlier face concept as the
next image-to-3D input. Built-in image generation edited `concept-face-reference.png`
using `user-face-reference.png`: preserve the distinctive purple flower at the
ear and the subject's visible moustache and chin/jaw beard, keeping the original
creature body and neutral full-body composition. The revised image is a concept;
no creature model or in-game replacement has yet been generated.

## Generation request — 2026-10-28

Submitted `Reference/concept-face-flower.png` to the designated A100 service.
Request UUID `12e1b63d-755a-45b8-a174-da212049c751`; job
`aedf9323-2d12-489b-a093-1335ab24ba53`. TRELLIS.2, seed 1234, split GPU,
1536 cascade, 12 steps, 8192 texture, background removal. Input SHA-256
`f0591dc2fee005ac3668b8718bec88926318dfc842002106a0d0eaf695dd5abf`.
Use the same UUID for resume/collect; do not resubmit while this request exists.

## Generated and integrated asset — 2026-10-28

Rig run `746c2d23-78ec-468e-b011-2163acef4078` retains the original 52-bone,
735159-vertex AI skin and four procedural service motion presets. `Generated/`
contains the original model, rigged original, animated rig and generation and
rigging records. The verified 844428085-byte versioned collection archive has
SHA-256 `a4a6ee1786f2ae211bb419a77f4003c3396e9f7492f16a73476a19dd512c02ff`;
the archive cache is optional, and originals/records are retained separately here.
The web model was rotated to inspect front, side and back, and the web joint editor
and waving preview were inspected; no joint edits were made. The flower is visible.

Blender 4.0.2 `finish_creature.py` preserves the source rig in the packed
`SchoolFlickerCreature.blend`, creates 120000/45000/15000-face skinned collapse
LODs and exports `Assets/Resources/SchoolFlickerCreature/creature.fbx`. The source
mesh's UVs and AI weights are kept by the decimator; thin tendrils and facial
fidelity still require in-game visual review. `export_textures.py` extracts PBR
from `Generated/rigged.glb`, resizes the source base color and converts glTF ORM
to Unity metallic/smoothness with Pillow/numpy from `requirements.txt`.
Unity 6000.6.0f1 `SchoolFlickerCreatureImport.Apply` produces the material,
normalized 2.5m prefab and LOD group. `SchoolFlickerCreatureMotion` maps the
actual inspected 52-joint skeleton and uses the existing flicker clock; normal
passages retain the existing person. Collider and stop policy remain the existing
pedestrian body. Run `school-anomalies-logic` for the headless rules and scene
integration. GPU appearance, floor/ceiling clearance through animated poses,
facial/flower fidelity after decimation, and native player performance require
separate in-game review; automated headless tests do not establish these.

Validation: `school-anomalies-logic` 151 EditMode and 8 PlayMode tests passed
in `Builds/Verification/feature-tests/20261028-092504-3cd4e00f`; the play test
checks creature/human visibility, flicker movement and pause. Windows player
`Builds/Windows-FlickerCreature-20261028` built successfully with Unity 6000.6.0f1;
its 4608 current inputs and required artifacts passed `verify_build_inputs.py`.
`check_source_completeness.py` passed after staging. No player was launched and
no in-game visual, collision-envelope or performance review is claimed.

## In-game size, colour and centre-line correction — 2026-10-28

The user's game screenshot showed a small, pale creature. The original colour
texture is retained; the generated high metallic response was replaced by a low
metallic/smoothness response and a subtle albedo-driven emission fill so colour
is less easily washed out or lost in the red corridor. UV comparison against the
original source using nearby candidate vertices found median difference 0.00014
and 90th percentile 0.00043; this is a geometry/UV check, not rendered evidence.

`SchoolFlickerCreatureImport` now measures the actual baked skinned vertices,
normalizes the neutral silhouette to 2.2m wide and places its lowest vertex at
floor level (the corridor is 4.4m wide). It validates the 3.2m ceiling bound.
The renderer's inflated culling bounds are not used as size measurements. The
flicker passage recentres the actual walker route at x=0, including restored
hall states and pending spawns; regular pedestrians keep their existing lane.
The replacement player must be visually checked after the build, since the
user's game is not interrupted and CPU tests cannot certify apparent size,
colour, shader presentation or animated clearance in the running player.

Root cause confirmed by the previously failing scene test: both `creature.fbx`
and `creature.prefab` shared the Resources key `SchoolFlickerCreature/creature`.
The runtime resolved the FBX's untextured default `Lit` material and unnormalized
source geometry. The prefab is now `creature-visual.prefab` and the encounter
loads only that unambiguous key; its GUID is preserved when renaming the asset.


Corrected build verification: `school-anomalies-logic` passed 152 EditMode
and 8 PlayMode tests in `Builds/Verification/feature-tests/20261028-095520-f70f2ddc`.
The scene test asserts the loaded creature's actual texture, emission, centred
approach, baked neutral 2.2m geometry, floor and ceiling. A Windows player was
built at `Builds/Windows-FlickerFix-20261028`; the complete 4610-input build
fingerprint passed. No native player or GPU appearance audit was run while
protecting the user's desktop, so the final screenshot appearance remains to be
checked in ordinary play.

## Abrupt strobe performance — 2026-10-28

At the user's request the neutral skinned silhouette is 3.9m wide and 3.0m
high in the 4.4m school corridor. Width and height are adjusted independently
to fit the existing ceiling. Each light-off boundary increments the shared
`SchoolWalkerFlicker.FlashIndex`: the creature's 17 selected actual rig joints
snap directly to a deterministic contorted pose and its route takes a 0.25m
obstruction-checked step. The pose and position remain fixed between boundaries;
there is no interpolation or tremor. The same saved passage elapsed time
reconstructs the index on restore; pause and talisman hold it. Normal pedestrian
movement is unchanged. Creature renderer motion vectors are forced off and the
school volume overrides motion-blur intensity to zero. The test suite verifies
boundary counts, obstruction, holds, route/pose and normal checkpoint behavior.
Extreme poses can extend beyond the neutral fitted bounds; wall/ceiling
clearance and the final strobe impression still need in-game visual review.

The 16 repeating pose seeds were chosen from 128 candidates by baking the
actual high-detail skinned mesh in a headless editor. The selected poses fit
within x = ±2.16m and y = -0.06m..3.20m before the tiny feet offset. The
reproducible diagnostic checks all 16 with the fitted prefab; it is geometric
evidence, not a rendered camera or lighting-quality review. The former 13
unrestricted random poses could cross the walls or ceiling and are not used.

Validation: `school-anomalies-logic` passed 153 EditMode and 8 PlayMode tests
in `Builds/Verification/feature-tests/20261028-101532-19f0128d`; the actual
light-off/held-dark/next-light-off pose assertions are included. The fitted
16-pose skinned geometry diagnostic completed without out-of-hall bounds.
Unity 6000.6.0f1 built `Builds/Windows-StrobeCreature-20261028` and all 4610
current build input hashes passed. No GPU presentation or native-player
inspection was run; visual impression and rotated elbow-corner clearance remain
unverified in actual gameplay.

## Ground contact, inversion and opaque strobe — 2026-10-28

The flicker-only `SchoolFlickerCurtain` owns an independent non-interactive
UI Toolkit panel above the HUD: the entire screen is black exactly while the
school flicker lights are off, then clears without a fade. Pause and talisman
hold the existing timeline. Other blackout events retain their previous rules.

The actual walker root uses the preserved deck RNG and flash index to select
upright or 180-degree inverted poses deterministically; checkpoint restoration
therefore does not reroll the orientation. Upright poses keep both soles on the
floor (within 1cm in the baked geometry check); inverted poses anchor the head
at the floor. Hip and leg rotations are held at their bind pose to keep the
soles grounded while torso, neck, arms and wrists snap. Three inverted variants
use alternative fitted seeds to keep projections out of the floor. Each of
32 pose/orientation combinations is measured at 3.9m width and within the
4.4m corridor and 3.25m ceiling. The independent UI curtain obscures the
camera including flashlight and HUD only during the flicker dark interval.

`FlickerCreatureDiagnostics.Inspect` bakes and verifies all 32 fitted poses,
then generates `Assets/Resources/SchoolFlickerCreature/pose-calibration.json`.
The runtime reads these offsets/scales and foot corrections without baking per
flash. The build validates hashes of the prefab and motion script and rejects
stale calibration; rerun the diagnostic after either changes. User desktop
isolation and GPU/native-player deferral remain in force. The CPU checks do not
certify actual screen pixels, limb appearance or performance in gameplay.

Validation: headless fitted-skin diagnostic passed 32 variants; the
`school-anomalies-logic` profile passed 153 EditMode and 8 PlayMode cases in
`Builds/Verification/feature-tests/20261028-110020-44090363`. A new player build
and its input fingerprint are recorded separately after they complete.
Player build: `Builds/Windows-StrobeGrounded-20261028` completed under Unity
6000.6.0f1; `verify_build_inputs.py` passed all 4614 current inputs. No GPU or
native player screenshot was produced while respecting desktop isolation.


2026-10-28 visibility revision: The opaque strobe curtain is no longer created.
During each dark pulse school lights, ambient and reflections are suppressed while
the player flashlight retains its own settings. On red-lit holds the existing
texture-based creature emission is strengthened with per-renderer property blocks;
it is zero during dark pulses. Model geometry, rig, calibration and source textures
are unchanged. Visual brightness and readability still require in-game review.


2026-10-28 red hold revision: every lit hold lasts three simulation seconds,
with discrete full-body tremor over the unchanged calibrated pose. Feet or the
inverted head remain anchored. A depth-tested red billboard sits behind the
creature and is disabled during the short dark pulse. The generated model, pose
calibration and textures are unchanged; actual visual appearance still needs review.


2026-10-28 timing rollback: the three-second lit hold was reverted to the
previous irregular flicker timeline. The red backlight and lit-phase tremor
remain; position and contorted pose change at each dark-pulse onset.


Readability: adjust Assets/Resources/SchoolFlickerCreature/ReadabilitySettings.asset
in the Unity Inspector. The skin-only directional and Fresnel overlay is synced
to the strobe and disabled in dark pulses, as is the red backdrop. Near/far
fill follows camera distance. Existing neutral tonemapping, -0.10 exposure,
0 contrast, 0.12 bloom and 0.12 vignette remain a starting point; assess the
actual distant and close camera views before making visual quality claims. The
additional skinned pass requires separate GPU performance measurement.


Grounding revision: after pose and tremor, sampled Unity skinned foot/head
vertices are aligned to the school floor; results are cached per tremor step.
The calibrated 3.9m width is retained in the 4.4m hallway. This CPU skinning
work requires separate performance and player-view review.


2026-10-28 environment-backlight revision (supersedes the older visibility,
red-hold and readability notes above): The creature material has no emission,
actor-following halo or separate rim pass. A school-owned fixed spot and an
end-wall wash at the far end provide the red background; school-scoped ambient,
reflection and fog settings are restored when the event ends. Short irregular
reveals alternate with darkness; each position/pose step happens at dark onset.
The player's flashlight remains independent. Adjust the fixture position, wall
position/size, colour, range, intensity, cone angle and wall brightness in
`Assets/Resources/SchoolFlickerCreature/EmergencyBacklightSettings.asset`.
The wash is a depth-tested approximation, and fog is ordinary school-wide fog,
not a volumetric simulation. GPU appearance, legibility and performance require
a separate player-camera review. Source model, rig and calibration are unchanged.

Verification: `school-anomalies-logic` passed 153 EditMode and 8 PlayMode tests;
Unity 6000.6.0f1 Windows build and current build-input verification passed.
No GPU render, player-view inspection or performance audit was run.


2026-10-28 rapid strobe revision: the fixed environment backlight reveals the
creature four times per simulation second: 0.09 s lit and 0.16 s dark. Each dark
onset snaps the pose and advances up to 0.45 m, with the full movement segment
checked against the player. Pause and talisman hold elapsed time. Existing
checkpoint elapsed time is retained and interpreted using the new strobe cycle.
GPU presentation, eye-level silhouette and runtime performance remain unverified.

Verification: `school-anomalies-logic` passed 153 EditMode and 8 PlayMode
tests; `hand-animation-logic` passed 104 EditMode and 11 PlayMode tests.
The Unity Windows build and input verification passed. No GPU view or FPS
audit was run under the desktop isolation policy.


2026-10-28 real-emergency-light correction (supersedes the fixed wall wash
and rapid-strobe notes above): The former large transparent red wall mesh and
its unlit shader have been removed. A small fixed school fixture supplies a
real corridor-facing spot and a short-range point light for the actual rear
wall and surrounding geometry. The school shadow budget includes the spot at
the pipeline shadow distance; no light or billboard follows the actor. Four
0.09-second reveals per second occur at uneven intervals. Pose and 0.45 m
steps change at each dark onset; each lit pose is static. Geometry, generated
model and saved elapsed time are retained, although a previously saved event
may resume at a different pose under the revised schedule.

`Tools/quality/run_school_backlight_review.py` runs the opt-in isolated real
URP player-camera comparison; its reports and PNGs are generated under Builds.
The school fog is ordinary global fog, not volumetric light scattering.

Actual-camera isolated GPU review (2026-10-28): the fixed fixture illuminates
the school floor, walls and ceiling, and spot-shadow on/off comparison changes
visible structure surfaces. The same camera produced empty, distant-creature
and near-creature frames. A baked-skin foreground mask found only a small
number of changed background pixels with creature shadow casting enabled;
reflections and LOD differences remain possible, so creature-shadow quality is
not certified. These stationary captures do not verify FPS, moving presentation
or all player approach directions. Measurements and frames are optional build
outputs reproducible with the review runner, not source assets.

Verification: `school-anomalies-logic` passed 153 EditMode and 8 PlayMode,
`local-shadow-logic` passed 100 EditMode and 1 PlayMode; the isolated GPU
review passed, and the Windows build/input verification succeeded.
