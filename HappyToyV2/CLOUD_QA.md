# Unity Build Automation: real scene tests

## Latest result: build #13 SUCCESS, 40/40 tests

Tested/exported source: `b1ca9e86efa60c0159e03556b91c95228b896644`.
[Exact-source CI 37208046390](https://github.com/minsminsKR/3d_modeling/actions/runs/37208046390)
passed. Original XML confirms **6 EditMode + 34 PlayMode**, zero failures/skips/
inconclusive. All 37 previous cases plus the three new recovery cases passed:

- Real CharacterController sprint cost, settled wall-contact recovery and truthful HUD,
  diagonal slide cost, obstacle removal with held input, a physical slope at 20 Hz,
  curse-slowed sprint, actual multiple fixed steps, exhaustion/Shift release and cabinet rest
- Actual authored nursery trigger and paused Settings buttons: immediate comfort lighting,
  >5s wall wait with a frozen game clock, normal reveal release and result/retry cleanup
- Two actual enemy deaths and retry-button flows, settings/back/journal/title navigation,
  preference persistence with reset game resources, destroyed old owners and bounded
  native UI panels/render targets/recognition textures

The unchanged map-aware real-input seven-record route passed in 79.439751 game seconds,
237.701294 physical meters, four stair legs, one hiding entry and two firecrackers.
All encounter owners were retained. These cases are not human fun or hardware-FPS tests.

All 34 envelopes hash-verify (10,562,700 bytes). Independent PCM recomputation passes;
the 17-segment 7.26 s acoustic WAV/JSON are byte-identical to #12. Both original XML and
final log contain zero targeted audio initialization warnings. Selected stationary-HUD,
settings and music-room images were visually checked; UI targets are not world composites.

Terminal SUCCESS and Windows export are confirmed. Original ZIP is 73,179,663 bytes,
186 CRC-valid entries, root `HappyToyV2.exe`, production scene/runtime present and no
project test/NUnit assemblies. SHA256:
`048345ffc5673ffa072bf778a26ff306c1b583599653ed49bd4c99c45bf13fa3`.
Original archive retained unchanged, including its original Burst backup text; no derivative
or new release-profile build. Final log is 2,338,849 bytes, SHA256
`928bf82b459b2e4b43a891eeeb0cbea20571a9312dab0932b2f708a0f8911ec0`,
with zero C# errors. See `Verification/cloud-tests/build-13-*` for exact evidence.
Subsequent documentation-only changes do not change this tested/exported source.

## Previous result: build #12 SUCCESS, 37/37 tests and no empty-source warnings

Tested/exported source: `f0ed764973c8b831d236299452077d50946a6bee`.
[Exact-source CI 37204689253](https://github.com/minsminsKR/3d_modeling/actions/runs/37204689253)
passed. Original XML independently confirms 6 EditMode + 31 PlayMode passed,
zero failures/skips/inconclusive. The warning guard, temporary roar-emitter
ownership/destruction and restart cleanup passed. Final log confirms successful
player export and terminal SUCCESS, zero C# errors and **zero** instances of the
empty-filter source warning observed 40 times in #11. Existing compiler warnings
are not presented as eliminated.

All 34 envelopes verify (10,567,444 bytes). The 17-segment 7.26-second acoustic WAV
and JSON are byte-identical to #11, preserving its six identities, physical wall/
floor attenuation, stereo direction, zero controls, uninterrupted pause/resume
and unclipped four-enemy overlap. Independent signed16 PCM recomputation agrees
with the engine metrics. The existing strict general mix gate also still passes.

The map-aware, real-input seven-record route passed in 79.657990 gameplay seconds,
237.873581 physical meters, four stair legs, one hiding entry and two firecrackers,
with all encounter owners retained. This is one automated strategy, not manual
balance, subjective fear or target-hardware performance certification.

Original Windows ZIP: **73,179,152 bytes / 186 CRC-valid entries**, root
`HappyToyV2.exe`; runtime and scene present, no project test/NUnit assemblies.
ZIP SHA256: `d4cbf2ff59725817ff65f010932dd1051469906cbe792c7006eaed9e2fc26b17`.
Production assembly SHA256: `a2ec87f1d181718a50d37df08df9c6727c1a87acaf80b3d20b5d2973400a687c`.
Expected profile/acoustic/owned-voice code is present. The original backup report
is retained; no derivative or commercial-release profile is claimed.

Final log: 2,300,444 bytes, SHA256
`92e529ad7b2d2a62a1a9a1d4369c13ebc5aadd60892aa2bcb997eb76576d2698`.
Evidence: `Verification/cloud-tests/build-12-results.json` and
`build-12-experience.json`. Original #11 and its identical Library WAV are preserved.
This completion update changes only evidence/docs; no further engine run is needed
for it. Device/headphone listening and long-session human feedback remain distinct.

## Previous enemy-audio result: build #11 SUCCESS, 37/37 actual tests

Tested/exported source: `a7c68b149ecb570b7a2d8dd47b7e6a9bfca276e5`.
Both original XMLs confirm 6 EditMode + 31 PlayMode passed, zero failed/skipped/
inconclusive. Exact-source CI37202392630 passed. All original 33 cases remain.
Four new cases pass real NavMesh cadence, stationary/warp/pause and scripted walk,
true attack/dodge/retry cleanup, authored profile binding and natural Lantern
transformation, plus isolated main-output acoustic comparisons.

All 34 envelopes hash-decode (10,567,421 bytes). The new WAV is 1,393,964 bytes,
native 48 kHz signed16 stereo, 348,480 frames / 7.26 seconds in 17 indexed segments.
Its SHA256 is `efdf6dc7a9ed5d4c32c621693f165e2d6df8104561d3b000445e0abcb091a582`.
Independent PCM recomputation agrees with every reported peak/RMS/channel metric
within one quantization step. Six identity segments are nonzero and distinct.
Wall/clear RMS ratio is 0.378369; blocked-floor/open-vertical ratio is 0.139058.
Left/right cues separate channels. Beyond-range, master-zero and paused segments
contain only zero values. The same in-flight cue resumes without replay; four
nearby Stalker sounds overlap without clipping (peak 0.225623). These are positioned,
isolated production-source fixtures, not a device-listening or full-game balance verdict.

Original Windows ZIP: 73,179,181 bytes / 186 CRC-valid entries, root `HappyToyV2.exe`,
SHA256 `b2c4edeac361de696d14f5003651b9a12dd0acaa3c55056f3ff126881650778d`.
Final log ends SUCCESS, zero C# errors. Original backup report remains; no derivative.
Evidence and inspection: `Verification/cloud-tests/build-11-results.json` and
`build-11-experience.json`. Original ZIP/XML/WAV are preserved separately.

### Narrow initialization correction: subsequently verified in #12

Build #11 also emitted 40 new empty filtered-source startup warnings (38 Lantern,
2 Cyclopse). Stack traces originate at AddComponent<AudioSource>, before explicit
PlayOneShot; the later actual PCM tests passed. The follow-up orders Lantern's
source creation before filter binding and configures Cyclopse's owned inactive
emitter before activation. Temporary voice/filter/clip cleanup is explicit on
completion/cancel/blocked release. No waveform, gameplay or acoustic tuning changes.
Common PlayMode setup now fails on that exact warning through scene load/retry/unload;
the existing intro case also checks native object destruction after handoff.
Independent review and exact-source preflight are in `Verification/enemy-audio-startup/`.
All 37 cases and strict mixer assertions remained; build #12 above independently
verified warning elimination and the revised Windows player. #11 remains its own
historical runtime result rather than evidence retroactively assigned to new source.

## Previous player-feedback result: build #10 SUCCESS, 33/33 actual tests

Tested/exported source: `705c0639f1bb892a92057481f93381050c3626c8`.
Both original XML suites independently confirm **6/6 EditMode + 27/27 PlayMode**,
zero failed/skipped/inconclusive. Final log ends SUCCESS at 09:08:16 UTC on
2026-10-04. Source CI [37189833520](https://github.com/minsminsKR/3d_modeling/actions/runs/37189833520)
passed on that exact source. Its completed result update changed only evidence/docs;
the subsequent enemy-audio implementation above requires its own verification.

Verified on the real engine:
- Three cabinets keep their original imported mesh bindings and four collision
  boxes each. Six actual standing/crouched-wide views show exterior room geometry
  through genuine narrow slits, with intact opaque door panels
- Cyclopse activates behind actual geometry and traverses 6.117772 m in 7.963809
  gameplay seconds before one arrival-gated roar. Four frames show hidden/partial/
  corner/forward stages. Pause, cancellation, deferred staging after camping and
  single AI handoff pass
- Recognition reuses actual observer sight, including over low cover, rejects
  walls/other floors/stale events, does not stack stings, ends promptly and respects
  pause/reduced motion/retry. Two effect captures are UI-only transparency targets,
  not final world composites or proof of subjective intensity/audibility
- Music-room layout/reload, six physical added colliders, four actual carved NavMesh
  footprints, original records/cabinet/crossing access pass. Five real-camera views
  show the piano/seats and wall-backed signs; the positioned player's real floor fog
  is allowed to settle before capture. No walk-through is claimed for those fixtures
- Actual F input still switches the flashlight with no on/off status or toggle caption
- Original real-input seven-record strategy survives: 79.374 s / 237.609 m, four stair
  legs, one cabinet and two items, all encounter owners retained
- Existing strict audio gate: 96,000 actual active samples, peak 0.03640747,
  RMS 0.00190542, zero clipping; 24,000 paused samples have peak 0

All **32** artifact envelopes (9,142,325 bytes) were independently hash-decoded.
The new camera/effect images were inspected, while interpretation limits remain
explicit. No target-device manual performance/listening acceptance is claimed.

Original ZIP: **73,175,011 bytes / 186 entries**, every CRC passes, SHA256
`534e001f1afd81947e07f0f28b6b34e32e8af6d507d132d61cf0712024654a09`.
Extract the whole archive and run root `HappyToyV2.exe`. The production DLL contains
the new cabinet/intro/room/recognition code; scene/runtime dependencies are present
and no project UTF/NUnit test assemblies ship. The original Burst backup report
remains; no derivative or release-profile build was made. Earlier #9 is preserved.

Exact records: `Verification/cloud-tests/build-10-results.json` and
`build-10-experience.json`. Source preflight is retained in
`Verification/player-reported-presentation/`. Those results remain pinned to build #10. The owner subsequently requested
continued personal-play improvements; the next bounded scope is recorded above.

## Previous personal-play result: build #9 SUCCESS, 24/24 actual tests

Exact tested/exported source: `7c13e7f98e40d5e2d89c43f71b41c073eb1ec5d3`.
Both original XML suites independently confirm **6/6 EditMode + 18/18 PlayMode**,
zero failed/skipped/inconclusive. The final log ends SUCCESS. Source CI
[37186371244](https://github.com/minsminsKR/3d_modeling/actions/runs/37186371244)
also passed. This evidence-only follow-up changes no tested gameplay or tests.

- All three new firecracker fixtures passed: first pop emitted once, separate
  denial feedback preserves story notices, finite inventory, frozen pause/cooldown,
  comfort/subtitle/mute settings, result and restart cleanup
- The cabinet fixture passed unseen/witnessed fairness, one cue per actual door
  attack, event-based HUD, paused time and old-source/clip cleanup after retry
- Both new HUD captures were inspected and readable. They are UI-only black-background
  fixture images. The door-warning image retains the preceding blocked-exit notice;
  it is not a pristine gameplay view or proof the exit is clear
- The original route survived with all seven records in 79.712 seconds / 237.630 m,
  four stair legs, one cabinet and two items. This remains one map-aware strategy
- The existing strict active/pause audio gate passes: 96,000 real active samples,
  zero clipping, and 24,000 paused zero samples. These WAVs match #8 byte-for-byte;
  this does not isolate the new door/pop effects or certify device listening
- Original ZIP: 73,167,963 bytes / 186 entries, all CRCs valid, SHA256
  `453cd8d7888b7439a4b6637b1d06a1b302cc511c8d40f959904690420b59fb3f`.
  Root executable: `HappyToyV2.exe`. New runtime methods are in the production DLL;
  scene/runtime dependencies are present and UTF/NUnit test assemblies are absent.
  The original backup text remains; no derivative or release-profile build is claimed

Exact records: `Verification/cloud-tests/build-9-results.json` and
`build-9-experience.json`. Preflight: `Verification/personal-play-feedback/`.
The goal is personal enjoyment. A passing test suite does not replace the user's
actual play feedback. Previous build #8 remains preserved.

## Previous engine result: build #8 SUCCESS, all 21 actual tests passed

Pinned source **`923060837b0cb8585d16d9bfffb961434a850504`**, Unity6000.6.0f1 / Windows Micro. Both original NUnit artifacts were independently parsed: **6/6EditMode +15/15PlayMode,0failures,0skips,0inconclusive**. The final log ends **SUCCESS** and the Windows player export completed. This run closes the prior missing EditMode result and strict AudioRenderer capture gate; #7's early process exit remains unexplained historical evidence, not a retroactive pass.

Verified:
- All five new pursuit/hiding/item cases pass again, with local evidence-only search, bounded unreachable-route handling, witnessed versus unseen cabinet entry, repeated-entry vulnerability, finite real-Q firecracker behavior and lantern own-floor investigation
- The real-input, all-encounters-enabled seven-record route survives in **79.407seconds /237.542meters**, four stair legs, one cabinet and two firecrackers, with original actor speeds intact. This remains one map-aware automated strategy
- The original strict audio gate passes. **14empty Render calls add zero samples** and each opens1024available frames; **47positive renders provide96,000actual samples**. Float peak0.0363464/RMS0.00190503,0clipping/nonfinite; after the pause flush,24,000real captured samples have peak0
- Active and paused PCM files were independently validated: stereo48kHz/16bit, respectively1.0second with nonzero content and0.25second of zero samples. This is offline-clock engine-mix evidence, not subjective or hardware listening approval. Combined ambience+F input does not isolate flashlight audibility, footsteps/firecracker completeness or spatial attenuation
- All nine PNGs are byte-identical to reviewed#7 images; no duplicate/new visual-quality claim is needed. The prior caption correction is included

The missing call on zero-count frames was a capture-harness issue: official Recorder-style per-frame Render now advances the capture cycle. It is not merely one-time priming;14empty calls occurred throughout the active capture. All original completeness, positive-buffer Render, finite/nonzero/unclipped and pause assertions remained unchanged. Source CI37179959322 passed on the exact tested commit.

Original Windows ZIP: **73,166,499bytes /186entries**, SHA256`fde361d88b69e4c2ee48bdbf5d71c631bcea9bf2bae2658ab4fb315d0be3b67f`. CRCs, required player/runtime/scene and absent UTF/NUnit test assemblies were checked. A separate verified candidate excludes only one228,583-byte Burst backup report, retaining all185other payloads unchanged; it is67,573,893bytes, SHA256`7b5eb902952c3328bbe3b536a10b4ca8246624042cc64fbba8f411e0fca2c8be`. Two packaging runs produced identical ZIPs. App development identity/instrumentation and asset-rights/runtime-smoke limitations remain; removing the backup is not a release-mode conversion.

Exact records: `Verification/cloud-tests/build-8-results.json`, `build-8-experience.json`, `build-8-candidate.manifest.json` and `build-8-packaging.md`. The historical evidence/docs update at `0ec7588` changed no tested game or test code; its engine evidence stays pinned to `9230608`. The newer personal-play source update above changes gameplay and is awaiting its own engine run. `RELEASE_READINESS.md` is a historical optional distribution checklist, not a prerequisite for personal play.

## Previous engine result: #7 pursuit cases passed, audio gate failed; EditMode unverified

Pinned source `b4a454e6fcf30d4716972101fafa2350f2b0d0e7`, Unity6000.6.0f1 / Windows Micro. **PlayMode14/15 passed,1 failed,0 skipped/inconclusive. Six EditMode tests are unverified in this run**: that process shut down during its first assembly reload after9.667seconds, without an XML artifact or test-completion marker. No specific crash cause appears in the available log. The later PlayMode process compiled and ran the same source. This is **not20/21 passed**. Terminal build status is FAILURE; no player was exported.

Actual new outcomes:
- All five pursuit fixtures passed: occlusion-preserved evidence, arrival look-around and local movement, pause/reacquisition/bounded search, rejected-route fallback, actual cabinet witness versus unseen hiding, blocked exit, repeated-entry vulnerability, real Q/fuse/finite inventory/decoy expiry, and lantern own-floor sound investigation with the player at another floor height
- The unchanged all-encounters-enabled real-input seven-record strategy passed in **80.132gameplay seconds /237.730meters**, four stairs, one cabinet, two firecrackers and3952motor updates. The final route diagnostic records one stalker attack started. This remains one map-aware automated strategy, not human balance or every encounter
- All listed camera/sign/UI tests passed again. Nine PNGs were hash verified. Seven are byte-identical to build#6; the changed pause and ultrawide settings images were viewed. The clarified pause caption **주요 단계0/4** is now engine-rendered and fits

Audio diagnosis made a real distinction: before recording mode, the actual listener callback captured **96,000real samples at48kHz stereo (one second)** in47callbacks, with0nonfinite/0clipped, float peak0.0137448/RMS0.00104616 and DSP advancing1.002667seconds. The original192,044-byte PCM WAV was recovered from XML and independently validated (48,000frames,49,525nonzero16-bit samples, SHA256`588c723400a3dd539fe2d77a29095e1c5a0b4cd6c7f77261ae30a1b595d0a667`). This is actual pre-device game-mix evidence, not a synthetic waveform or listening certification.

After `AudioRenderer.Start`, all961sample-count polls returned0 over8.0029wall seconds; no Render call occurred and DSP stayed178.112→178.112. The strict96,000-sample gate failed. This localizes the failure to the recording/polling path; it does not prove the exact cause or satisfy flashlight/pause/device acceptance. A subsequent narrow experiment follows [official Recorder5.1.7](https://download.packages.unity.com/com.unity.recorder/-/com.unity.recorder-5.1.7.tgz), package/Editor/Sources/Recorders/_Inputs/Audio/AudioInput.cs, `AudioInput.NewFrameReady` (registry SHA1 verified `0b23bb7a1fb4a062b8640ef5b1d174ca9b070467`): call Render even when the allocated capture-frame buffer has zero length, add no samples for such a call, and keep all existing positive-buffer, completeness, nonzero, clipping and pause assertions. No guessed positive buffer or test exclusion is permitted. This subsequent change is **not covered by #7**.

Exact case/log hashes are in `Verification/cloud-tests/build-7-results.json`; survival, WAV validation, image hashes and interpretation boundaries are in `build-7-experience.json`. Last successful player export remains#4. Fresh EditMode XML and the complete strict audio result are required from a later pinned run.

## Pursuit implementation and fixture scope

The current suite has **6 EditMode + 15 PlayMode tests (21 total)**. Build#7 passed all five new controlled fixtures; its exact partial-suite outcome and remaining gates are above. No new successful Windows export or AudioRenderer acceptance is claimed.

Two concrete runtime gaps were found in the source:
- `StalkerBrain.Search` repeatedly routed to one stale point, with no local movement or look-around. It now preserves the last confirmed evidence as an anchor, first reaches/attempts that point, then scans and chooses bounded reachable same-floor branches. Transit is capped at four seconds and actual local inspection at 6.5 seconds. Route rejection starts a bounded fallback scan; it cannot trap the state forever or count an unreachable point as visited. Only renewed sight or an accepted real sound updates confirmed evidence
- `LanternMaskEncounter` accepted same-floor sounds but then froze its investigation whenever the player was on another floor. Investigation and ordinary wandering now continue on valid own-floor paths; direct cross-floor chase still stops, and the shared path/sight/floor restrictions remain

New `CloudPursuitTests` cases cover actual brain/NavMesh state loops, sight barriers, local movement/look-around, pause, reacquisition, finite search when all routes reject, authored music-cabinet witness versus unseen entry, blocked exits, repeated entry during a warned attack, real Q input and physical firecracker fuse/attraction/expiry, finite inventory, and a lantern following accepted own-floor noise while the player is at another floor height. These are explicitly controlled fixtures: threats are isolated and actors are placed; the unreachable case injects initial stale evidence, and cabinet setup uses its public interaction API. They are separate from the unchanged real-input, all-encounters-enabled seven-record survival strategy. No fixture pass establishes human difficulty or every enemy interaction.

The audio gate remains strict and unchanged. A new **diagnostic-only** listener `OnAudioFilterRead` probe observes up to one second of the authored game's genuine pre-device mix before `AudioRenderer.Start`, with a three-second wall watchdog. It copies incoming data without altering it or generating samples, reports missing/partial/complete data plus DSP/source progress, and emits a WAV only for real nonempty finite stereo samples. It does not substitute for the renderer/nonzero/clipping/pause assertions or certify device listening. Renderer polling now reports DSP deltas, capture-frame counts and actual Render-call count, so zero returned counts can be distinguished from silent rendered samples. The original #6 zero-sample failure remains recorded.

Source preflight is recorded under `Verification/cloud-tests/pursuit-preflight/`; these reports are external compilation/static/pure-C# checks, **not Unity execution**. Build#7 subsequently exercised all15PlayMode tests as above. The missing six EditMode results and strict audio failure still require a later pinned run.

## Previous engine result: build #6 failed only on audio capture, 15/16 passed

Pinned source `e3122fb0c83659100904e93d486f5d97093f721d`, Unity 6000.6.0f1 / Windows Micro: **6/6 EditMode + 9/10 PlayMode passed; 1 failed; 0 skipped/inconclusive**. The terminal log ends FAILURE and the unchanged strict gate prevented player export. Exact XML/log hashes and the remaining free allocation are in `Verification/cloud-tests/build-6-results.json`. Source CI [37174240095](https://github.com/minsminsKR/3d_modeling/actions/runs/37174240095) passed.

Verified improvements:
- The same real-input seven-record strategy passed again: **79.70 gameplay seconds, 237.63 meters, four stair legs, seven records, one cabinet and two firecrackers**, with all encounter owners and original stalker speeds intact. This is repeatability of a map-aware strategy, not two different strategies or first-time-player balance
- **All listed UI layouts passed** with default/large text and high contrast: title720p, settings720p/4:3/ultrawide, pause4:3, HUD720p, empty journal1080p. Nine actual PNGs were recovered through the strict hash/chunk envelope and individually viewed. No obvious clipping is present in those captures. Result screens and populated/paginated journal remain outside this capture coverage
- The real corridor render and15-sign contracts passed. Reversed/overlapping legacy room labels seen in #5 are absent; the original paired faces/words/transforms and shared GUI font remain intact. Current corridor materials are visible, but the sign correction alone is not claimed to explain every shading difference
- The HUD PNG is a transparent UI-only layer, separate from the authored-world camera image. Render timings remain software-driver offscreen/readback observations, not target-hardware FPS

The only remaining test failure is **0 of 96,000 real AudioRenderer samples**, even with `captureDeltaTime=0.01666667`, `captureFramerate=60`, active gameplay (`timeScale=1`, listener unpaused), stereo capabilities and a nonzero DSP timestamp. A single timestamp does not establish that the DSP clock advanced during capture. UBA reports FMOD nosound. No WAV was produced and the pause-silence phase was not reached. This is an unresolved cloud audio-capture limit, not evidence that the game is silent. The fixed-clock experiment did not solve it; no further blind same-worker retries, fake waveform, ignored test or weaker audio assertion were used. Real mixed-output and device-listening acceptance remain open.

The later narrow pause caption correction changes “recovered records /4” to **“main stages /4”**, matching the four primary story stages while HUD/journal correctly count all seven records. That copy edit has source validation only; these actual engine results and screenshots remain pinned to `e3122fb`. The nine image hashes, full route report and review boundaries are in `Verification/cloud-tests/build-6-experience.json`. Last successful Windows player export remains #4; the safe derivative package also contains #4 code.

## Previous expanded result: build #5 failed, 14/16 checks passed

At exact commit `db36acf3939fbec39a5bcb9228e09668975ad6eb`, UBA #5 ran **6/6 EditMode + 8/10 PlayMode**, with **0 skipped/inconclusive**. The strict gate correctly prevented player export after two failures. Original XML and terminal log hashes are in `Verification/cloud-tests/build-5-results.json`.

New verified evidence:
- The full authored seven-record route **passed**: 79.38 gameplay seconds, 237.54 physical meters, both stair round-trips, real cupboard entry/exit and two finite firecrackers. All encounter owners stayed enabled and authored stalker speeds stayed unchanged. This is a deterministic, map-aware survival strategy, not first-time-player balance or every optional encounter
- The real starting camera **rendered successfully**. The exact PNG was recovered from NUnit output and SHA-256 verified. Microsoft Basic Render Driver / D3D12 offscreen-render plus full-readback samples were 595.750 ms (first warmup), then 164.612/147.080/147.958/149.269/145.952 ms. These values are not presented FPS or target-hardware certification
- The actual title PNG contains rendered Korean glyphs. Its layout test detected the title line needing 96.25 units within 82.5, and the start button needing 31.25 within 28.75. The next source increases the title-only line box and removes inherited button vertical padding without shrinking the font. The test now collects all menu layout errors before failing, preserving subsequent captures
- Visual inspection of the camera PNG found backward/overlaid legacy room signs. This is a player-facing rendering defect distinct from the nine managed Korean signs; the targeted correction is being verified with unchanged scene bytes

Audio **did not capture any samples** (0 of 96,000); this is not evidence of silent game audio. UBA initializes FMOD on nosound output. A bounded next experiment follows Unity Recorder's established constant-rate path: save/restore `Time.captureDeltaTime`, use 1/60 before AudioRenderer starts, keep actual Render/nonzero/clipping/pause assertions and log clock/DSP diagnostics. The [official Recorder package](https://download.packages.unity.com/com.unity.recorder/-/com.unity.recorder-5.1.7.tgz) also supports variable playback, so this clock is not claimed as a universal API prerequisite. Recorder itself [does not support batch Editor](https://docs.unity3d.com/Packages/com.unity.recorder@5.1/manual/KnownIssues.html#recorder-doesnt-work-when-running-the-editor-in-batch-mode); no guarantee of batch/nosound capture is made. Missing data will remain a failure, not a synthetic waveform or inferred listening pass.

Artifact transport hashes, complete survival report and visual findings are retained in `Verification/cloud-tests/build-5-experience.json`. The original PNG/XML/log files remain separate evidence. Production UI/sign corrections and the revised audio harness require a new pinned run. Last successful player export remains #4.

## Expanded gate scope

At build #6, the expanded suite contained **6 EditMode + 10 PlayMode tests (16 total)**. Build #6 established the route, camera/sign and listed UI cases above; audio remains a failed/unverified gate:

- `AuthoredSevenRecordRouteSurvivesWithRealMovementAndInteraction`: begins at the preserved authored spawn, then uses injected keyboard/mouse controls through the real motor and focus/E path; all encounter owners remain enabled and authored stalker speeds remain unchanged. The route must climb and descend both staircases continuously, recover all seven records and escape. Actual hiding and two finite firecrackers form a single deterministic survival strategy. No direct collection, actor disabling, fixture relocation or invulnerability is used. A defeat or blocked route fails; this is not a first-time-player difficulty or all-encounter certification
- `KoreanMenusRenderAcrossSupportedAspectRatios`: actual UI Toolkit render targets at 720p, 1080p, 4:3 and ultrawide, large-text/high-contrast settings, text/button bounds and Korean sample-glyph checks. Every target is cleared before capture to reject stale/undefined output. PNGs still require visual inspection; automated content checks do not certify complete art or all text
- `RealAuthoredCameraProducesRenderAndTimingEvidence`: actual starting camera, authored geometry/materials and Unity URP rendering. Six 960×540 offscreen render plus full-readback timings are reported, including the first warmup. These are not presented-frame FPS or target-hardware benchmarks
- `RealListenerMixCapturesDuringFlashlightInputAndPauseSilence`: Unity AudioRenderer records the actual main output while F toggles the real flashlight, then checks the paused mix. It checks finite/non-silent/unclipped active samples and near-silent paused samples. Ambient sound and flashlight are a combined mix, not an isolated flashlight audibility assertion; FMOD nosound on UBA cannot certify headset/speaker playback or subjective mixing

PNG/WAV evidence is written outside Assets and also embedded in bounded `HAPPYTOY_ARTIFACT_*` envelopes in NUnit output so a failed build still retains it. No UBA hook, upload endpoint or new credential is needed. `Tools/quality/extract_cloud_evidence.py` validates every chunk, declared length, SHA-256, safe filename and total-size bound before extracting into a fresh external directory. The build number and full source commit must be verified separately from the UBA checkout log; envelopes alone do not authenticate the source revision. Original NUnit/log files must be retained.

The historical `Verification/annex/full-route-first/walk.json` recovered six prerequisite records but died on its basement return. The earlier successful compact-room route is not the authored seven-record Annex scene. This new gate exists to find and correct genuine route, encounter or driver problems without relabeling that failure as a pass. Authored scene bytes and build selection remain protected.

## Latest verified result

**UBA build #4: SUCCESS**, on exact tested commit `58f5cf949380d28574ab4a809c0c903ef9a27811`, Unity **6000.6.0f1**, Windows Micro. Both NUnit artifacts were independently parsed: **6/6 EditMode + 6/6 PlayMode passed, 0 failed/inconclusive/skipped**. The previously failing real-footstep hearing/recognition case passes with the interior, physically validated fixture. The Windows player export and final status also succeeded.

The test ZIP has **73,163,952 bytes**, all **186 entries** pass CRC, and SHA-256 `1bcf7687acdc65706cf4cb1c877bce132130c680b6551feb3fcc1b5b75230307`. It contains the executable, production assembly and scene; no UTF/NUnit test assemblies are included. It still contains Unity's `BackUpThisFolder_ButDontShipItWithYourGame` folder and is a development/testing artifact, not an approved release package. Final logs have no C# compiler-error or Shader-error lines; existing warnings remain. Billable time was 9m41s.

Sanitized evidence: `Verification/cloud-tests/build-4-results.json`, including hashes of both original XML artifacts and the complete terminal log. This documentation-only follow-up does not change the tested Assets, Packages, ProjectSettings or source tools. The source-quality CI for the tested code also passed: https://github.com/minsminsKR/3d_modeling/actions/runs/37153309430.

This is a controlled automated scene/physics/state milestone. Manual survival completion, rendered UI/art quality, audio listening, all encounters, long-run stability and target hardware performance remain open. Prior failures below are retained as history.

## Scope

UBA Windows build #1 succeeded on Unity **6000.6.0f1** for commit `02e6107dffcb21f1748ceb49ef8cabd68b8c50bd`. LFS checkout and the authored SchoolAnnex scene import succeeded. That run built a Windows player; it did not execute gameplay tests. The downloaded 73,163,991-byte ZIP passed CRC verification (SHA-256 `76beddb023a909e80b53cadda9dca15c7d35206ec670f4c08b501604931cc291`). The build used the Microsoft Basic Render Driver, so it is not a visual quality or target-GPU performance sign-off.

This change adds **6 EditMode + 6 PlayMode Unity Test Framework tests**. It does not launch any legacy audit that calls `Application.Quit`, and it does not call scene generation or save APIs. Test assemblies use a strict reflection bridge to the actual `Assembly-CSharp`/`Assembly-CSharp-Editor` types because Unity test asmdefs cannot reference predefined assemblies directly. Missing types/members fail assertions; there is no ignored/mocked fallback. Existing serialized gameplay assembly identities and all scene bytes stay intact.

UTF **1.8.0** is now an explicit manifest dependency, matching the exact built-in version already resolved in the lockfile and first UBA build. No package version was upgraded. NUnit stays at built-in **2.1.0**. The three test asmdefs are not auto-referenced, use TestAssemblies references, and do not replace production assemblies.

## Discovered test contract

EditMode (`HappyToy.V2.EditModeTests`, six tests):
- Actual 29 attack-clock, 20 ripple, 68 stealth and 26 restart assertions, each exposed as a separate NUnit test
- Exact Unity version and production assembly identity guard
- Real imported-scene validation: protected hashes, missing scripts, session/player/camera, required record inventory and nine Korean signs. Scene setup restores afterward without saving the authored scene

PlayMode (`HappyToy.V2.PlayModeTests`, six tests):
- Title freezes input, annex evidence blocks premature restoration/escape, real callbacks restore all seven records, duplicates do not increase discovery count
- Journal/pause behavior and mixed duplicate restart requests; new session resources and explicit title destination
- Real CharacterController stance, overhead obstruction, camera height, actual keyboard-driven foot contacts, quiet crouching and blocked sprint
- Baked NavMesh plus clear collision/raycast approaches for all seven mandatory record objects and the exit; doors must actually finish opening
- Actual player footstep delivery to a controlled enemy, floor/pause sound guards, gradual recognition, continued pursuit after crouch/light change, wall occlusion
- Real attack warning, paused timing, dodge/recovery and lethal stationary contact

The suites intentionally use controlled actor positions and direct record interactions. They do **not** prove manual survival difficulty, a complete input-driven route, rendered menu bounds, audio quality, hardware performance, or every monster encounter.

Tests use bounded realtime watchdogs for scene/condition waits. Scaled gameplay completion is checked through actual state (door position, awareness, attack phase), not assumed from wall-clock delay. Each PlayMode fixture unloads its game scene, removes the injected keyboard and restores the prior current keyboard, seven preference keys including prior absence, time/audio/cursor/input globals and the reload request. Cleanup does not unload the UTF runner's scene if setup fails. All assertions remain failures if contracts change.

## UBA settings

On the existing Windows Micro target, keep exact Unity 6000.6.0f1, project folder `HappyToyV2`, the PR7 branch and the preserved build scene. In Advanced Settings > Tests enable:
1. Run my project's unit tests when building
2. Run EditMode tests
3. Run PlayMode tests
4. Mark build as failed if any test fails

No pre-export/post-export or shell hook is required for this suite. Do not add the legacy audit command-line flags to UTF runs. Start only a reviewed/pushed commit and record that exact revision from the build log. Avoid simultaneous builds and check the remaining approved free allocation before retrying.

A successful build or enabled toggle is not enough: the run must provide nonzero EditMode **and** PlayMode discovery, all **12 named tests** executed, **0 failures / 0 skips**, and its NUnit result artifact. Missing or zero test results leave this gate unverified. Preserve failed reports for diagnosis instead of changing assertions to get a green run.

Official references:
- https://docs.unity.com/en-us/build-automation/reference/unit-tests
- https://docs.unity.com/en-us/engine/6000.6/manual/scripting/test-framework-introduction/getting-started/edit-mode-vs-play-mode-tests

## Validation before the first test-enabled cloud run

The existing 143 pure C# checks and Python source validation remain separate. `Tools/quality/compile_unity_api.py` now models all three test asmdefs as separate compiler stages, uses the real UTF/NUnit references, validates their declared boundaries, and fails when a test source/asmdef/reference is unsupported. It never treats a syntax or external API compile as UTF execution.

## First test-enabled run and diagnosed harness fixes

UBA build #2 executed all 12 tests at `33f7b14534a5fdf1d4b9311906a64674b8468807`: EditMode **5/6 passed**, PlayMode **4/6 passed**, **0 skipped**. The fail-on-test gate correctly stopped the build. The real attack/pause/dodge, eight mandatory navigation approaches, journal/restart and record/escape cases passed. Results and original NUnit XML hashes are retained in `Verification/cloud-tests/build-2-results.json`.

Three failures are not being relabeled as passes:
- The strict protected metadata hash failed. Converting the original LF `.meta` file to CRLF reproduces the exact UBA failure hash. `.gitattributes` now pins LF for that file and EditorBuildSettings.asset. A fresh checkout with `core.autocrlf=true` retains both original hashes; no expected hash or preservation assertion changed
- Both keyboard-driven tests failed before their expected stance/hearing state. The fixture omitted Input System's Editor game-input routing setting. Versioned 1.19 source requires both `IgnoreFocus` and `AllDeviceInputAlwaysGoesToGameView` for unfocused batch-Editor input. The fixture now temporarily sets/restores both, directly observes queued key delivery, and records dynamic input/motor state on failure. There is no manual input update, direct substitute for the tested keypress, ignored test, or weakened gameplay assertion

This identifies a definite missing harness configuration, not a proven production input bug. The subsequent run below confirms the metadata and crouch fixes, while exposing a separate invalid hearing-fixture placement. At that stage the full 12-test gate remained **FAILED / RETEST REQUIRED**; build #4 above now passes it. The new external compiler preflight is in `Verification/cloud-tests/build-3-preflight`; it is not a Unity rerun.

Input routing reference: https://raw.githubusercontent.com/Unity-Technologies/InputSystem/1.19.0/Packages/com.unity.inputsystem/InputSystem/InputManager.cs (gameShouldGetInputRegardlessOfFocus and unfocused Editor event routing).

## Second test-enabled run and hearing-fixture correction

UBA build #3 executed all 12 tests at `46ea8e1661decbb8b44f30ee89c8eb95e969c249`: EditMode **6/6 passed**, PlayMode **5/6 passed**, **0 skipped**. A fresh source clone applied the LF attributes, and the strict imported-scene preservation test passed. Actual keyboard-driven crouch/stand/headroom, walking/crouch footsteps and blocked-sprint silence now passed. The fail-on-test gate still stopped the build; no new player artifact was produced. See `Verification/cloud-tests/build-3-results.json`.

The remaining hearing case observed W input and movement, but the player fell to y=-42.37 with no grounded foot contacts. Serialized geometry proves its nominal player start at x=-8.9 intersects the west wall and exit plaque and overhangs the floor with the real 0.3 m capsule. The fixture sampled only its enemy anchor, then blindly offset the player. Its immediate grounding check did not require a new physics move, and its 3D displacement check could be satisfied by falling. The actual corridor floor is present and enabled; this is an invalid fixture placement, not evidence of a missing production floor. Exact depenetration and stale-versus-transient grounding mechanics are not established by static inspection.

Only that hearing fixture now uses an interior anchor at x=-4.5. Before testing sound, it verifies the real authored floor and standing-capsule clearance across the lane, a clear baked-NavMesh segment, at least three fresh motor physics updates, grounding and floor height. Input must produce horizontal grounded progress; real footstep count, enemy reception and post-step grounding must all pass. No synthetic floor/noise, direct footstep call, skip, relaxed timeout or production change was added. The passing attack case retains its original anchor.

The corrected fixture was subsequently exercised successfully by pinned build #4, as recorded above. The seven-stage genuine-API compilation and source checks in `Verification/cloud-tests/build-4-preflight` remain separate preflight evidence; the actual NUnit artifacts establish runtime success for these 12 controlled tests.
