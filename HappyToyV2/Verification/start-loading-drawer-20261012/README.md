# Cinematic first corridor entry and close-range drawers — 2026-10-12

The title's actual new-corridor button fades its menu and scrim over 0.75 seconds.
The existing Baeksugwi clone advances toward the title camera on unscaled time,
using its original Run_InPlace gait and independent irregular head action.
Travel approaches a bound that keeps the head at least 1.8 horizontal metres
from the camera. The camera follows the face gradually rather than allowing
the creature to cross behind it during unusually slow preparation.

Corridor geometry, furnishings, visual dressing and mesh batches prepare across
multiple frames; navigation uses NavMeshSurface.UpdateNavMesh asynchronously.
The same production builders retain a synchronous path for their existing
direct preparation/checkpoint contracts. No alternate map or placeholder actor
is used. Once the actual corridor is ready and the approach has been visible
for at least 3.4 seconds, the title fades to black over 0.85 seconds. The actual
corridor then fades in over 1.1 seconds. Gameplay input, time and audio remain
gated until the reveal ends. There is no conventional loading label in this path.
Saved-run consumption occurs only after preparation succeeds; failure restores
a fresh source scene and retains the recovery message.

The loading clone reuses reviewed wrist-local hand surfaces for grounding and
contact shadows. It does not CPU-skin all 952,517 vertices on every frame.
The first implementation did that and timed out the entry checks; the cached
version passes those checks. The source model, original animation and skin are
retained; ordinary idle-title planting and reduced-motion head gating remain.

Drawers no longer reject their user standing at the handle. Only moving-tray/
payload-to-player controller pairs are temporarily ignored during motion or
overlap; original pair states are restored when clear or disabled. Fixed desk
colliders, NPC obstruction and unrelated loose-payload obstruction stay active.
The physical tray still travels 0.30 m and owns its existing finite pickup,
pause behavior and checkpoint state. Empty and occupied drawers are covered.

## Verification

- `playmode-loading.xml`: three passed Unity 6000.6.0f1 tests: real pointer entry
  with moving title during incremental preparation, menu fade/full black/gated
  reveal; existing real title/journal/pause/result restart flow; original title
  head/reduced-motion/body-support regression.
- `playmode-drawers.xml`: two passed tests: real E opening/closing at touching
  distance for empty and occupied trays without player displacement, restoration
  of controller collision pairs and preserved fixed furniture collisions; the
  existing finite pickup, pause and fresh-scene current/legacy checkpoint test.
- `start-loading.json`: final actual Windows player PASS. Preparation spans 278
  observed frames, with 168 advancing approach frames; the title clone travels
  4.98 m and its wrist gait changes 17.28 degrees. Menu opacity reaches zero,
  black reaches one, and input remains gated during the corridor reveal. The
  diagnostic run's transition takes 11.58 seconds, including capture overhead;
  this is not an ordinary-game load-time benchmark. The real tray opens 0.30 m,
  closes, causes zero controlled player displacement and restores its pair.
- `start-contact-sheet.png` and `drawer-contact-sheet.png`: selected final actual
  frames, visually reviewed for menu disappearance, advancing creature, full
  black/corridor reveal and physically extended close-range drawer.
- `diagnostic-audio-silence.json`: process mute verified and original mute/user
  preferences restored successfully at exit; listener remains zero during proof.
- `build-inputs.json`: GRAPHICS_BUILD_INPUTS_PASS for the final Unity 6000.6.0f1
  release player and complete current input graph. A prior rebuild failed on a
  memory-mapped generated lighting-report file; the successful retry replaced it.
- `source-completeness.json`: required editable sources and Unity metadata PASS
  after staging. Generated performance-test Resources JSONs were removed before
  the final build. Earlier failed-run XMLs are optional build history, not current
  passing evidence or required sources.

## Reproduce

Use the repository's pinned Unity 6000.6.0f1 and existing project setup. From the
repository root:

```powershell
unity test HappyToyV2 --editor-version 6000.6.0f1 --mode PlayMode --filter 'StartLoadingRealTitle;DrawerRealEOpens;MainCorridorRealPointerEntry;CorridorDrawerRealEReveals;BaeksugwiTitleHead' --output Verification/start-loading-drawer-20261012/playmode.xml
unity run HappyToyV2 --editor-version 6000.6.0f1 -- -executeMethod HappyToy.V2.Editor.QualityValidation.BuildWindows -v2-release-player -v2-build-output Builds/CinematicStart-Drawer-20261012
```

Run the new Windows player with `-quiet-diagnostics -v2-start-loading-output
<absolute-output-directory>`. The explicit diagnostic keeps the actual title,
clicks the real UI target, records the approach and reveal, then verifies an
actual entrance drawer with the controller held at touching distance. It uses
an isolated profile and the existing process-session audio silence/restore guard.
Playing screenshots compose the actual UI target over the actual player camera.
This controlled fixture is separate from a full survival or performance audit.

With Pillow 10.2.0, use `python HappyToyV2/Tools/quality/preview_start_loading.py
<output-directory>` for a shared-palette loading GIF and contact sheets. Raw
captures, GIFs, isolated profiles, builds and logs are optional outputs under
ignored Builds; reproduction tools and selected evidence are committed.
