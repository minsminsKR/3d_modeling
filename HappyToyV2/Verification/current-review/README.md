# Current local review evidence — 2026-09-23

Selected scene: `Assets/ClassroomReview/SchoolTeacherDesk.unity`. Latest build replaces the teacher-desk primitive visually while preserving its collision and register location. `teacher-desk-route.json` passed the actual E-key register inspection, state-aware door prompts, restoration chair event, Hwacat completion and escape. Older results below retain their original scope.
Base commit: `57ad81d`; current modifications are local, not pushed.

Manual performance preparation: `Tools/Run-PlayReview.ps1` launches an interactive player only when explicitly run. `PlayReviewTiming` records focused gameplay camera-callback intervals, not GPU timing or hardware presentation. The hidden negative-control run `outputs/manual-review-negative` produced no camera samples and correctly marked `enoughSamples=false`; UI checks still passed all eighteen booleans. Thus collection under a visible focused window remains unverified, not a claimed performance pass. No visible window was launched. Latest build: `outputs/unity-manual-review-build.log`, exit 0.

Event-interruption follow-up: `transitions.json` passed six controlled checks with zero logged errors: reach Cyclopse roar, pause freezes it, restoration cancels without reactivating enemies, clean restart after restoration, start Hwacat painting drop, clean restart during that reveal. `V1CyclopseIntro` now checks resolution/inactive state after both waits before handing back to navigation. The normal intro regression also passed (`outputs/transition-normal-intro/intro.json`: complete, roar, AI handoff, survival and both linked-fixture states). This does not replace route testing, arbitrary timing fuzzing or a human playtest. Current build: `outputs/unity-event-transition-build.log`, exit 0.

Audio follow-up: `audio.json` records a full successful route using the opt-in `-v2-audio-output` listener DSP tap. Captured 51.456 seconds of 48kHz stereo, not truncated, with nonzero output in every story stage and zero samples at/above full scale. Highest observed peak was 0.3948 during story step 3. This is one pre-device engine-output measurement at the current game volume, not hardware loudness, a microphone recording, complete source isolation or subjective mix acceptance. The WAV remains in workspace `outputs/audio-review-route/route.wav` (about 9.9MB); it is not included in this evidence folder. Ordinary launches do not instantiate the capture component. No production audio gain was changed from these measurements.

Implementation reference: Unity's [OnAudioFilterRead documentation](https://docs.unity.com/en-us/engine/6000.6/script-reference/unityengine/monobehaviour/onaudiofilterread). The callback copies samples without changing the output buffer; Unity API calls and file I/O stay outside the audio callback.

Latest runtime follow-up: Korean interaction prompts replace legacy English on doors, hiding and the entrance box. Door prompts reflect open/close state; blocked closing explains that the player must step away. `korean-prompts-route.json` is the latest full-route result, additionally asserting that actual E-key door interactions change the Korean prompt. Passed restoration chair endpoints and escape. The UI run below predates this prompt change and is not a fresh screenshot acceptance of it.

| Evidence | Scope |
| --- | --- |
| `dependencies.json` | SHA-256 and GUID list for selected-scene dependencies, all C# scripts, Resources and configured render pipelines; all imported Assets paths checked for missing `.meta`. Not a complete distributable manifest: include ProjectSettings, Packages and SourceArt when handing off the project. |
| `scene-integrity.json`, PNGs | Current scene: 834 renderers, no missing scripts/meshes/materials; selected editor camera views, not an exhaustive runtime art review. |
| `route.json` | Latest executable after restoration-chair fix: actual movement/E interactions, story stages, measured chair endpoint and successful escape/Hwacat event. |
| `ui.json` | Fresh run on the same latest executable: all eighteen UI checks passed, seven UI captures rendered. Source: `outputs/current-review-flow/flow.json`. |
| `access.json` | Same scene before the chair runtime change: eleven targets reachable. Does not validate navigation after every possible dynamic pose. |
| `historical-ui.json` | Earlier shape-selected build: eighteen UI checks and seven captures. Subsequent cabinet/tile/chair changes were not covered by this UI run. |
| `historical-hiding.json` | Cabinet build before tile/chair changes: controlled unseen/witnessed hiding cases, exit, spatial audio configuration and footsteps. Not subjective audio acceptance. |

Reports above were copied from workspace outputs without changing values. `CurrentReviewAudit.Run` regenerates the dependency list and scene captures but does not rerun gameplay tests or create a build. Listed dependency files and metadata were checked against Git ignore rules; none were ignored. Untracked files still require deliberate staging before any future push. No staging, commit or push was performed.

Remaining acceptance work: human playtest and visual/audio review, visible-window performance, final character/material polish. Save/load and broader V2 content are not claimed complete. See the parent ART_REVIEW_PLAN for chronological evidence and limitations.
