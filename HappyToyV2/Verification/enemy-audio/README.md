# Enemy identity and spatial audio source preflight

Historical preflight for `a7c68b1`, subsequently executed in UBA #11: all37actual
Unity tests and Windows export passed. See `../cloud-tests/build-11-results.json`
and `build-11-experience.json` for original hashes and independently checked PCM.
Forty empty-source startup warnings were observed; their narrow initialization
correction has a separate preflight and subsequently passed its own #12 engine run.

Purpose: distinguish the four authored Stalker movement families, Lantern and its
transformed form; separate Stalker attack anticipation from walking; make existing
enemy cues respect bounded distance and actual obstruction. No AI speed, perception,
attack timer, item resource, scene, model or existing test change.

- 104 C# files and 877 GUIDs; parser and protected scene/meta/build-selection checks
- 80 Python regressions, including the 34-file / 16 MB transport boundary and old
  build-10 envelope compatibility
- 143 assertions against the actual existing C# attack/ripple/stealth/restart helpers
- Seven genuine Unity 6000.6 external API compiler stages; inspect the report for
  warnings and exact source hashes. This does not execute/import the Unity project
- Independent source review has no outstanding must-fix; fixture isolation and
  natural Lantern transformation timing were corrected before final compilation

Runtime suite definitions: 6 EditMode + 31 PlayMode = 37, retaining all original 33.
The four new cases cover real NavMesh contact cadence, pause/warp/intro behavior,
true attack/dodge/retry cleanup, authored profile bindings, natural transformation,
and actual-main-output PCM in explicitly positioned isolated fixtures. Seventeen
segments preserve six identity cues, finite range, wall/slab/open vertical paths,
stereo, master mute, an in-flight pause/resume and four-enemy overlap.

PCM capture was NOT RUN when this source preflight was created; the subsequent
actual #11capture passed all17segments. Intended output is one
7.26-second native 48 kHz stereo WAV and a segment/geometry/metric JSON. No synthetic
PCM replaces mixer output. Physically audible event captions remain available under
master mute; subtitles still follow their separate setting.

The direct-path low-pass/transmission model is a bounded approximation, not full
room acoustics. Existing actor names resolve profiles; authored binding tests cover
those names. Mannequin retains its existing joint-creak cadence and gains spatial
filtering only. No device listening, subjective fun or target-hardware performance
claim is made. Build #10 remains the latest verified Windows player until a new
exact-source build completes.
