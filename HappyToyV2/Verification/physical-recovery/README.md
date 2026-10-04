# Physical sprint recovery and interruption preflight

Subsequent actual result: UBA #13 at b1ca9e86 passed all 40 cases (6 EditMode + 34 PlayMode)
and Windows export, with zero targeted initialization warnings. All three new cases
and the previous 37 passed; build-13 evidence preserves exact XML/log/ZIP/PCM hashes.
The preflight below remains a separate source record.

Observed source defect: held sprint input drained stamina at a fully blocked
CharacterController and advertised running in the HUD, despite zero actual
movement/footsteps. Sprint intent now requests velocity; the real horizontal
movement from each physics step determines exertion. Completely blocked/resting
movement recovers. Diagonal wall slides and curse-slowed sprinting retain the same
cost. Exhaustion is checked afresh every fixed step, including catch-up steps.
Ordinary Update input changes do not overwrite the measured Running outcome before
footstep feedback consumes it. Pause, hiding, disable and stance resets remain explicit.

Walk/run/curse speeds, .18/s drain, .12/s recovery, .05 exhaustion and .25 plus
Shift-release rearming are preserved. Cabinet recovery continues. HUD resting
wording does not assert safety or erase the existing short footstep-noise memory.
The opt-in StaminaAudit now earns exhaustion through real W/S travel across its
verified clear authored spawn lane instead of relying on wall contact; that
standalone audit is NOT RUN and is not counted as UTF evidence.

Nursery warning lighting applies ReducedMotion immediately in paused Settings,
while the existing five-second reveal clock remains frozen. Door-based recovery,
AI perception, enemy timing, item counts, scene bytes and all earlier fixes remain.

Three new engine cases (total 6 EditMode + 34 PlayMode expected) explicitly use:
- Controlled physical floor/wall/ramp, actual keys and CharacterController travel:
  blocked recovery/HUD, diagonal slide, held-input escape, 20 Hz slope, curse,
  actual multiple fixed steps, exhaustion/held Shift/release and cabinet rest
- Authored nursery trigger, actual pause/settings buttons, >5 s paused wall time,
  normal release, result interruption and old native cue/source destruction
- Actual enemy deaths and retry buttons, settings/back/journal/title navigation,
  settings persistence and reset game resources, bounded native UI/texture owners

Fixture placement/isolation is labeled; only the separate unmodified full-route
case is real-input survival evidence. Capture-clock catch-up is not a hardware
frame-rate measurement. No new evidence envelopes or relaxed audio gates.

Source checks: 80 Python cases, 143 existing pure C# assertions, 105 C# syntax/
metadata checks (878 GUIDs) and protected-scene checks pass. Seven external
compiler configurations use genuine Unity 6000.6/package/UTF APIs with zero errors
and baseline warnings. These reports do not execute the Unity tests or export a
player. Actual 40-case runtime/build verification was PENDING at preflight and subsequently
passed in build #13. The standalone opt-in StaminaAudit was not separately run.
