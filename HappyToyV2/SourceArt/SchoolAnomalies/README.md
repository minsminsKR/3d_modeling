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

`Assets/Scripts/SchoolWalkerDance.cs` is the editable additive choreography,
aiming the actual upper/lower arm segments in model space and using the 34-bone names in `SourceArt/SchoolWalker/rig-plan.json`. Base walk/idle
poses are resampled before every additive offset, preventing accumulated bone
rotation. The route moves at 2.8m/s (four times 0.7), stops for the player's
obstruction, and shares its actual distance phase with recorded footsteps.
An independent radius-0.43m, 1.77m capsule blocks the CharacterController; it is
outside architecture layer 8 so it cannot fake corner visibility occlusion.

Baby triggers at canonical Z=15.88m (the front study sign), at station Z=35.4m.
Owned temporary PBR copies and a 1.3m emergence give a 2.2-second apparition;
shared imported materials are restored afterwards.
`prepare_fade_materials.py` derives the two tracked `Resources/SchoolAnomalies`
PBR material hints from the existing Baby Skin, preserving normal/metallic plus
transparent/emissive keyword combinations for player shader retention. Runtime
fade copies disable shadow casting until the opaque authored materials return. Original cry/chase clips
are sampled explicitly. A clear approach within 6m or 60 simulation seconds
from appearance starts 3.15m/s native NavMesh pursuit; a clear contact under
0.9m requests the game's existing defeat path. Spatial crying reaches 42m.

Blackout uses actual SchoolFluorescent event gain on all three visible repeats,
including each LOD's tube emission, downward light and ceiling backscatter.
The farthest tube goes out at one second and the nearest at nine seconds,
with 0.22-second fades. The last tube starts the existing five-second defeat
deadline. Normal school exposure is retained: no screen-wide darkness volume
suppresses the player flashlight. Real cold-window spill, emergency signage
and wet-floor reflections remain natural parts of the unpowered aisle.
Completing a backward seam clears the effect before the next passage renders.
Pause and talisman hold event clocks, movement, manual poses and Baby crying.

## Verification

Run `python HappyToyV2/Tools/quality/run_feature_tests.py --profile school-anomalies
--graphics-api d3d11 --no-desktop-interaction` (single command line). The profile
includes independent probability/judgment/deadline rules, real seam choices,
actual CharacterController obstruction, dance movement/pose, recorded Baby cry
in the quiet DSP mixer, proximity/timeout pursuit, ordered real lamp power,
production-camera torch OFF/ON contribution after all nine lamps extinguish and defeat, JSON/old-v3 restoration and rejection before mutation of Baby poses outside the
real NavMesh, current school
navigation/geometry/clock/walker/audio and normal brightness.

Set `HAPPYTOY_SCHOOL_ANOMALY_REVIEW_DIR` to a new child of `Builds/` for opt-in
unfiltered production-camera PNGs of the dance, crying Baby and partial blackout.
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
