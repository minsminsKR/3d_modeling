# Perceived tension soundscape: source preflight

Subsequent actual result: UBA #14 at c6b0c1a3 passed all 44 tests (6 EditMode +
38 PlayMode) and Windows export. New 7.3 s stereo PCM passes the quiet/aftermath/
headroom/control gates; independently measured bed/contact RMS is .12678. All
36 artifacts verify. Exact results are in ../cloud-tests/build-14-*; preflight
below remains distinct from runtime evidence.

This is an original player-local horror-pacing layer. Quiet exploration can become
uneasy after genuinely audible enemy contacts; accepted real recognition raises a
stronger subjective pulse/air bed. The aftermath settles instead of instantly
turning off when an unseen enemy state changes. No enemy-state, proximity-radar,
directional imitation, camera or HUD effect drives the layer.

Production rules:
- DetectionFeedback keeps its existing actual-sight validation and four-second
  coalescing; only its accepted cue reports recognition
- StalkerFootsteps reports after actual movement/attack PlayOneShot emissions,
  including the existing brain-disabled corner walk and both Lantern forms
- Deferred hearing admission uses real active/native-playing sources, current
  physical acoustic transmission and finite distance, one-shot gain, master volume,
  source mute and current session ownership. Rejection changes no stress/hold/duck
- Ordinary contacts cap at .35, recognition .85, attacks at .95; max strength rather
  than additive stacking. Weaker contacts cannot pin a stronger recognition hold
- Rise is 3.5/s, release .16/s with short bounded holds. Strong aftermath settles in
  roughly 5–7 s without evidence; repeated ordinary contacts settle to their low cap
- Original deterministic low pulse/air clips are nonspatial and low-gain. A brief
  .24s duck lowers only this new bed during real cues. Pulse pitch follows smoothed
  stress from .9 to 1.2; ReducedMotion uses steady pitch 1 and .45 gain. Air stays 1
- Master mute/pause respected, paused comfort changes applied without timer advance,
  exact idle quiet, terminal restoration/result/disable clear, owned retry cleanup
- Existing stamina-driven exertion breath, enemy waveforms/gains/AI, item resources,
  player movement and the authored scene remain unchanged

RoomAmbience and FloorAtmosphere now use this sensory history for their bounded
mix gain instead of reading a nearby enemy's hidden Chase flag. No new directional
hint or permanent safety indicator is added. Routine mannequin creaks and scripted
roars are not independent anticipation inputs; genuine mannequin recognition still
uses the accepted detection path.

Four added PlayMode fixtures are designed for natural heard movement/attacks,
silent hidden-state negative controls, actual transmission/mute admission, true
recognition/coalescing/aftermath/lifecycle, and actual main-output PCM. Direct event
controls used to isolate the mix are explicitly distinct from natural integration
and the separate unmodified real-input seven-record survival route.

The proposed twelve-segment stereo montage includes exact quiet/onset/aftermath/
quiet, original footstep-only versus bed-only/combined, mute/pause/resume/comfort
and result silence. Sample ranges and separately discarded flushes are reported.
Its .3 RMS bed-to-nearby-footstep ceiling is an engineering headroom check, not a
psychoacoustic masking or subjective-listening certification. The 36-file transport
cap preserves all 34 previous files plus this WAV/JSON, without increasing the 16 MB
aggregate or 1.5 MB single-file bounds.

Independent review found no remaining must-fix. 80 Python cases, 143 existing
pure C# assertions, 107 C# syntax/metadata checks (880 GUIDs), protected-scene
hashes and seven genuine Unity API compiler configurations pass. Existing compiler
warnings remain; no engine execution is implied by compilation. Exact source
fingerprints and reports are recorded here. Actual 44-case engine verification, new PCM and Windows export were PENDING at
preflight and subsequently passed in build #14. Both original players are retained.
