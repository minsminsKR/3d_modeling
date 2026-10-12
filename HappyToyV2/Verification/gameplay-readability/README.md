# 2026-10-13 gameplay readability feedback

Implemented the five requested changes: large-monster torso wall clearance,
thin white pickup silhouettes, close-threat candle blackout with recovery,
larger Uncat/Cyclopse patrol strides and much larger pursuit swings.

`AI/EnemyBodyClearance` derives the torso/head footprint from real posed skin
in actor-local space. Its trigger query is excluded from navigation baking and
interaction rays. Corrections use supported same-floor NavMesh movement; the
visual is not offset from its AI. Door approach and close-behind waits receive
the measured body reach. Existing model size, rigs, weights and source clips stay
intact. Scripted prone intro poses retain their own placement.

`AI/EnemyLocomotionPose` restores the original rotations before sampling and
adds speed-driven opposing limb swings. Patrol/pursuit upper-limb amplitudes are
28/72 degrees for Cyclopse and 24/58 for Uncat. Attack owns its pose; pause freezes
the layer. Editable settings and their source ownership are documented under
`SourceArt/CreatureStudio/README.md`.

`PickupOutline` uses the actual visible, available pickup meshes and a 1.25px
depth-tested hull. Doors/inspections/candles are not marked as pickups. Cached
hidden renderers remain hidden. Candles extinguish within the existing 2m root
range or 1.5m of a large rendered body, and during close attacks. A .65m release
margin prevents threshold cycling; only previously ignited candles recover,
without a second ignition event or match sound.

Verified with pinned Unity 6000.6.0f1:

- Core EditMode rules: 19/19.
- `readability-05`: 7/7 PlayMode, covering wall overlap, outline availability,
  candle recovery, patrol/chase rotations, real diagonal door passage and pause.
- `ai-doors`: 15/16 initially. The remaining search test used an obsolete 1.4m
  root approach assumption. It now observes the actual stopped agent at a closed
  handle, retaining closed-leaf, pause, passage and evidence assertions, and passed
  in `final-regression.xml`. The other 15 door/search/stair cases passed unchanged.
- Final regressions: wall clearance, both school Cyclopse first-appearance cases
  and the updated search test passed. The awakened-clone fixture initially inherited
  runtime audio voices; after isolating those copied voices before activation,
  `clone-final.xml` passes the single-query/real-rig check. Pickup availability
  passed again in `clone-outline.xml`.
- Fresh release Windows export, complete current input inventory/hashes and
  protected authored scene checked. Inputs: 2,931 files.
- Native `native-02/readability.json`: PASS on Direct3D 12 / RTX 5060 Ti, no errors.
  Both real creatures traversed the corridor with enlarged patrol/chase amplitudes
  and grounded skin. Visible outline differences were 631 pixels for memory,
  484 for firecrackers and 220 for batteries; all three became zero behind opaque
  cover. The actual captured frames were inspected. Device-session silence and
  user preference restoration succeeded.

Fresh NUnit/logs/native captures are local under `Builds/Feedback-20261013`;
the player is `Builds/Windows-Feedback-20261013/HappyToyV2.exe`. These are ignored
reproducible outputs. The checked-in native report below retains the measured
scope; no full survival playthrough or full unfiltered suite is claimed.

Reproduce focused checks with `run_feature_tests.py --profile readability` and
`--profile ai-doors`. The current readability profile also includes the new clone
ownership case. Build with `QualityValidation.BuildWindows`, then run the player
with `-quiet-diagnostics -v2-readability-output <new-directory>`. Optional GIFs and
the contact sheet are generated with `preview_gameplay_readability.py`.
