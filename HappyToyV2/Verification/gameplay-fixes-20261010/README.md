# Gameplay fixes, 2026-10-10

Sliding door pulls follow the trailing edge of each moving leaf. Candle danger
continues through the occupied cabinet's actual viewing slit, retaining physical
wall occlusion and the existing heard-movement lifetime. The school classroom
cabinet exit also clears its adjacent desk and chair.

The corridor adds Cyclops at 0–1 memories, Uncat at 2, the actual Mask at 3,
and Baby at 4. Earlier actors remain active. Hwacat stays in the school chapter.
Baby begins stationary and crying, investigates small sounds, chases on actual
sight, and slowly wanders after losing the player. Loud sounds then admit pursuit.
Versioned checkpoints preserve these states and load legacy four-slot data.

The second school memory arms the mannequin. Walking into a clear view of its
body under the corridor-end lamp starts the local zoom; looking away, walls,
an unlit lamp, hiding, or an opaque NPC body delay it. Actual mesh openings stay
visible, including posed skin and unreadable static GPU meshes. Walking/running
camera feedback is retained.

## Evidence

- `unity-targeted-test-summary.json`: 34 distinct passing targeted Unity cases
  across the repair runs (9 EditMode, 25 PlayMode); latest observation per case.
  The final six mannequin cases all passed together.
- `school-reveal.json`: native controlled story and actual W walk PASS, including
  zoom, pause, flashlight, exact saved camera pose, logical look and control return.
  The raw stride-to-rest forward difference remains recorded; the return assay
  checks exact handoff separately from locomotion presentation.
- `mannequin-actual-view.png`: the actual native zoom frame under the lone lamp.
- `baby-behaviour.json`: controlled production Baby navigation/state/checkpoint
  and natural listener DSP PASS. This fixture isolates crying sources and fixes
  cabinet RNG only for its acoustic controls; it is not a full survival route.
- `school-quiet-output.json` / `baby-quiet-output.json`: each current-process
  Windows render session was muted and read back before positive mixer checks.
  Exit joined the worker, restored original session mute and all seven preferences.
  `quiet-preferences-restored.json` independently compares registry values.
- `recorded-audio-inventory.json`: 86 mandatory recordings and 26 retained
  originals passed inventory/hash checks. Baby cry is a real CC0 recording;
  original candidates, edits, attribution and reproducible generator are included.
- `build-inputs.json`: final Unity 6000.6.0f1 Windows release and current
  Assets/Packages/ProjectSettings inventory PASS; source scene protection retained.

Automatic diagnostic launches must use `-quiet-diagnostics`. The guard starts
with listener volume zero and fails closed. An unavailable mute backend allows
silent visual checks but cannot certify positive PCM. It does not certify
unobserved new Windows sessions; audio-device changes immediately close the
listener gain gate. Ordinary gameplay keeps the player's sound settings.

Native checks can be reproduced with the built player:

```powershell
HappyToyV2.exe -quiet-diagnostics -v2-school-reveal-output <outside-project-directory>
HappyToyV2.exe -quiet-diagnostics -v5-baby-output <outside-project-directory>
```

Run Unity's targeted tests with `-quiet-diagnostics -runTests`; the source test
names are listed in the summary. Stage required project sources and run
`python HappyToyV2/Tools/quality/check_source_completeness.py` before pushing.
Native report paths and machine diagnostics are optional evidence, not build
dependencies. Positive pre-device PCM does not certify human listening quality.
