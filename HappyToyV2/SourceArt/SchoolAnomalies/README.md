# School anomaly loop — 2026-10-24

A new school run starts with a normal passage. Subsequent draws use 50% anomaly,
with a guaranteed anomaly after three consecutive normal passages (the first
passage counts). Conditional on anomaly, Baby, DancingWalker and Blackout have
identical weights. The forced draw changes the long-run aggregate frequency;
50% describes the ordinary draw, not an observed overall frequency or Exit 8's
undocumented internal probabilities. RNG is private and checkpointed.

Normal + forward and anomaly + backward add one hour; either wrong judgment
resets to 00:00. Only the physically masked existing school seam commits a
judgment, clears the prior event and draws the next one. No eight-hour ending
is added. New event state is optional in map-v3 saves; absent/null old state
migrates to a normal passage while preserving the saved clock.

## Resources and authoring

No newly generated base model or external asset is used. Reuse the tracked,
editable sources and licenses under `SourceArt/CreatureStudio/Baby` (see its
existing handoff) and `SourceArt/SchoolWalker`. The runtime Baby is the existing
`Assets/Resources/EnemyStudio/Baby/BabyRuntime.prefab`, PBR and its cry/chase
clips. Existing recorded `enemy-baby-cry` is loaded through `ExternalAudio`;
its original audio attribution and license remain in the audio catalog/credits.
Baby never re-enters the corridor roster and no old school quest is activated.

## 2026-10-28 presentation revision

The user supplied a 7.3-second screen recording of the reference Instagram reel
https://www.instagram.com/reel/DehELDOvhj2/ . Its red hold, irregular rapid dark
pulses and asymmetrical changes informed the timing, not a new model or copied
animation asset. Exact movement distance cannot be inferred from the small,
moving-camera recording. The supplied third-party reel capture is preserved as
`Reference/user-reel-capture-20261028.mp4` (SHA-256
`c3371b3e4b9e624498f8909b65fed6d83ffcd8e90d8e48ffbc32dcefd3e2c7c5`) for
authoring reference only; it is not a game asset and does not grant rights to
the original creator's video. It is not needed by the runtime.

`SchoolWalkerDance` applies reproducible asymmetric limb poses and upper-body
and head tremor to the existing 34-bone SchoolWalker rig. `SchoolWalkerFlicker`
owns the 7.3-second light/pose timeline, and the real fluorescent lights and
movement use the same state. Movement happens only in the dark pulses, with
collision and obstruction stopping retained. The source model, rig, motions and
licenses remain in `SourceArt/SchoolWalker`; no new generated base is required.

Baby appears immediately at the shop (canonical Z=9.4), behind the player at
the study sign. All school fluorescent fixtures extinguish at that trigger;
the player's flashlight remains available. The existing Baby model is scaled
to 2/3 with its retained cry/chase animation and spatial recording.
`SchoolBabyAtmosphere` uses existing licensed tension air and heartbeat recordings;
seeing Baby starts a heartbeat, and the ambience grows over the minute. Baby
starts pursuit after 60 simulation seconds regardless of proximity and contact
causes defeat. The same pause, talisman, saved state and 1.8-second seam departure
rules remain in effect. Baby witness state survives checkpoint JSON; older saves
without it restore as unwitnessed. Previously saved pursuit remains compatible.

Blackout uses actual SchoolFluorescent event gain on all three visible repeats,
including each LOD's tube emission, downward light and ceiling backscatter.
The farthest tube goes out at one second and the nearest at nine seconds,
with 0.22-second fades. The last tube starts the existing five-second defeat
deadline. Normal school exposure is retained: no screen-wide darkness volume
suppresses the player flashlight. Real cold-window spill, emergency signage
and wet-floor reflections remain natural parts of the unpowered aisle.
Completing a backward seam clears the effect before the next passage renders.
Pause and talisman hold event clocks, movement, manual poses and Baby crying.

## Historical verification (2026-10-24)

The following results predate the 2026-10-28 revision. For non-disruptive CPU
checks of the current revision, run `python HappyToyV2/Tools/quality/run_feature_tests.py
--profile school-anomalies-logic --no-desktop-interaction` on one command line.
This uses `-nographics -noaudio`; it cannot verify appearance or audio quality.

For graphical verification when separately appropriate, run `python HappyToyV2/Tools/quality/run_feature_tests.py --profile school-anomalies
--graphics-api d3d11 --no-desktop-interaction` (single command line). The profile
includes independent probability/judgment/deadline rules, real seam choices,
actual CharacterController obstruction, flicker movement/pose, recorded Baby cry
in the quiet DSP mixer, 60-second pursuit, ordered real lamp power,
production-camera torch OFF/ON contribution after all nine lamps extinguish and defeat, JSON/old-v3 restoration and rejection before mutation of Baby poses outside the
real NavMesh, current school
navigation/geometry/clock/walker/audio and normal brightness.

Set `HAPPYTOY_SCHOOL_ANOMALY_REVIEW_DIR` to a new child of `Builds/` for opt-in
unfiltered production-camera PNGs of the walker, crying Baby and partial blackout.
This is controlled scene/camera verification, not a claim of manual survival
balance or displayed FPS. Player builds use the pinned project pipeline and
input-hash verifier. No player or native diagnostic is launched automatically.

Verified on Unity6000.6.0f1 / Direct3D11: 119 EditMode + 27 distinct PlayMode
cases, including affected-case rechecks after final choreography/material/save
validation changes. Original reports and latest per-case aggregation are under
`Builds/Verification/feature-tests/school-anomalies-20261024-195345/`. Reviewed
production-camera Baby/blackout/dance PNGs are in the targeted recheck
`school-anomalies-recheck-20261024-200448/review/` directory.

## Presentation correction — 2026-10-24

`SchoolBabyEncounter.BeginDeparture` stops and disables the old native agent,
blocks further attack ticks, and applies the same `SchoolLoopPlan.ReturnToCentre`
rigid pose transform as the player. The old ghost's frozen body then uses its
existing transparent PBR copies to fade from its current opacity over 1.8 seconds;
crying uses the same envelope through `EnemyAcoustics.SetBaseGain`, retaining
physical range/occlusion and master mute. Pause and talisman hold the visual and
voice tail. The actor, owned fade materials and owned recording are destroyed only
at zero opacity. A load/scene teardown still clears owned actors immediately under
its loading curtain. The already-resolved departing ghost is cosmetic: a checkpoint
captures the next logical passage and never restores it as an attacking Baby.

`school-anomaly-presentation` is the focused verification profile. It checks actual
keyboard retreat and rigid ghost placement, intermediate body material alpha and
cry gain, stopped agent/contact safety, pause/talisman, late resource release,
current/legacy saves, normal school brightness and recorded walker audio. Its
blackout test renders the same production camera with the real torch OFF/ON after
all nine lamps extinguish, and retains the five-second defeat deadline with the
torch enabled. Optional review PNGs use the existing
`HAPPYTOY_SCHOOL_ANOMALY_REVIEW_DIR` child of `Builds/`.

Presentation verification: 100 EditMode + 9 distinct PlayMode cases passed on
Unity6000.6.0f1 / Direct3D11, including targeted rechecks. With all nine tubes
extinguished, the same wall-facing production camera measured display luma
0.013490 OFF versus 0.027771 ON, with 27,881 pixels visibly brighter at 640x360.
Torch output/cookie/range were unchanged. Body opacity, same-frame cry envelope,
stopped pursuit, rigid backward seam placement, pause/talisman and owned-resource
release passed. Original reports remain under
`Builds/Verification/feature-tests/school-anomaly-presentation-20261024-204505/`;
latest reviewed torch OFF/ON and intermediate ghost PNGs are in
`school-anomaly-presentation-recheck-20261024-205213/review/`.
