# Distinct first appearances: source implementation, engine run pending

The existing authored school, routes and original monster assets are retained.
This iteration gives each first encounter a different physical rhythm rather than
adding a repeated loud jump scare, camera lock or forced damage. Optional aiming
from 9d688adb is included in the next combined engine run; build #14 at c6b0c1a3
with 44 actual passing cases remains the latest verified player meanwhile.

- Cyclopse: the original corner activation remains conservatively occluded. After
  placement, a stationary 1.05s spatial breath precedes the existing .85m/s physical
  walk, arrival roar and AI handoff. The director's 1.08s repeated on/off prelude is
  now one shallow voltage dip, with the same subsequent two-second wait
- Hwacat: 1s frame strain, .7s smooth portrait fall and one impact, original stand,
  1.15s toy movement, .45s sudden stillness, then a distinct dry jaw cue and angry
  silhouette. The full 1.3s harmless angry grace and ordinary attack windup remain.
  Result/disable/restoration now settle the event and restore the original portrait
- Baby: the same five harmless seconds now contain 2.7s of restrained whimper,
  stillness until 4.1s, then slow crawl preparation and one wet contact on the real
  water. Brain/agent remain disabled until 5s; northward escape remains available.
  One slow lamp recovery replaces oscillation; comfort keeps its original intensity
- Uncat: a .8s scrape is anchored to an existing archive bookcase, before the
  unchanged 3.55s protected physical emergence. Early arrival earns a listening hold.
  A single slow light dip replaces repeating oscillation. Local caption and sound
  remain separate from the recovered archive record so its warning is not overwritten
- Lantern mask: the distant lamp can still wander/investigate legitimate sound.
  First mask appearance needs same-floor, nonhidden, on-screen, physically clear
  sight within 12m. Ticks/light → mask rise → still beat lasts 2.2s with no acquisition,
  navigation or attacks. Once started, retreat or looking away does not stall it
- Optional MaskWraith escalation: the existing avoidable curse still grants five
  stationary seconds. Lantern contraction, tall narrow growth, late shoulders and
  mask attachment produce distinct silhouettes with a restrained growth voice.
  It still follows last confirmed curse information, not escaped hidden players
- Mannequin: the existing visible display triggers a 2.8s harmless still/partial turn/
  rigid hold/finish/still sequence and synchronized joints. It never translates
  during this observed first turn. Existing released gaze/flashlight fairness stays
- The friendly LovelyDoll guide remains friendly and unchanged

EncounterRevealAudio owns configured inactive-before-activation spatial emitters,
original deterministic distinct waveforms, finite range/physical occlusion and
native clip cleanup. Beats replace their preceding voice rather than stacking
one-shots. Master volume, listener pause, captions and comfort settings remain
in charge. No new locator HUD or enemy-state polling is used for presentation.
ReducedMotion removes rocking/light variation and water ripples; pause does not
advance event clocks or resample a transformed lantern pose.

Six new controlled PlayMode cases are being prepared for safety, genuine event
triggers, floor/viewport/LOS admission, repeated visits, retreat, pause/comfort and
cancellation/retry. Ten real authored-camera keyframes and a native 48k stereo
montage of actual event excerpts plus JSON are planned. Explicitly positioned
fixtures, selected-source isolation and disjoint audio segments are not a human
playthrough. Existing full-route and ordinary attack/audio tests remain gates.

One old fixture is intentionally adapted: PlaceForLanternContact now points the
camera at the new stable first-sight mask anchor before the existing contact test.
The original curse timing, five-second transformation, waveform and fairness
assertions are unchanged. No existing test is removed or silently skipped.

Together with the four pending aim cases, the combined target is 54 tests
(6 EditMode + 48 PlayMode). Evidence transport allows 51 files with unchanged 16 MB total
and 1.5 MB single-file limits. Actual import, execution, render inspection, PCM checks
and Windows export remain PENDING until one exact-source cloud run completes.

Final independent review has no unresolved must-fix. It found and verified fixes
for the old lantern fixture's gaze anchor and a paused transformed-mask pose reset.
A test-only order-300 observer now checks actual rendered-body mask attachment
after production LateUpdate before pausing, rather than an intermediate pose.
Upstairs/basement camera fixtures let the real floor atmosphere settle before
triggering their events. Historical opt-in lantern/mannequin probes were aligned
with new timing but are not claimed executed.

Preflight: 113 C# sources, 886 GUIDs, all 80 Python cases (no skips), 143 existing
pure C# assertions, protected authored-scene hashes and seven genuine Unity API
compiler configurations pass. Source fingerprints match the frozen implementation.
These reports are distinct from the still-pending actual 54-case engine run.
