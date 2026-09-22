# Visible-window review retry

Scope: repeat previously blocked native-window inspection and focused threat-stage timing only. No content expansion, pull, commit, or push.

The native game window was successfully activated and inspected using Computer Use screenshots during the scripted route. Observed corridor, classroom, infirmary and event-stage views. Korean objective, notices, stamina and interaction text were readable in the captured 1280 x 720 client views. This was an automated route with direct screenshot inspection, not a full manual playthrough or exhaustive art review. No confirmed gameplay/UI defect requiring a source change was identified in these observations.

`walk.json`: no failure, story step 4, escaped=true, hwacatCompleted=true, one enemy attack and at most two active enemies. Player log search found no Error, Exception or Assertion. The test exited automatically.

`manual-timing.json`: RTX 5060 Ti / Ryzen 5 9600X, development build, 1280 x 720, no resolution changes, zero unfocused callbacks, enoughSamples=true. The filename and built-in measurement label say manual; this run used the automated walk route.

| Stage | Samples | Median ms | P95 ms | P99 ms | Max ms |
| --- | ---: | ---: | ---: | ---: | ---: |
| Calm | 5632 | 5.5552 | 5.9078 | 6.1146 | 29.7129 |
| Threat (story steps 2–3) | 3560 | 5.5560 | 5.8695 | 6.1465 | 11.3377 |

These are focused main-camera render callback intervals, not GPU durations or hardware-presented FPS. Threat-stage classification includes the whole event stage, not exclusively frames where the monster is visible. Screenshot capture occurred during the run. No sustained threat-stage timing regression is evident from this sample; this is not a guarantee for other hardware/resolutions or an extended soak test.

No game source or assets were changed for this retry. Work stopped at the requested verification boundary.
