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
