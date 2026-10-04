# Deliberate firecracker aiming: implementation awaiting engine verification

Optional held right mouse previews the existing throw up to its first physical
collision, or the 1.2-second fuse horizon when nothing is hit. Q still throws
immediately; releasing right mouse cancels without consuming either of the two
starting charges. There are no firecracker pickups, refill systems, enemy markers
or distraction-success indicators.

The restrained line and small surface ring appear only while holding the button.
They use the existing bundled depth-tested vertex-color shader, so scene geometry
occludes them. High contrast increases contrast and width. No pulse, flashing,
moving dashes, camera motion or audio is added. The hint explicitly describes the
first contact or fuse range, never the final bounce/landing position. Moving
obstacles can change the subsequent actual throw.

Shared launch and .02-second sphere-cast steps keep the forecast and physical
projectile consistent across rendered frame rates. Speed (forward 14 + up 3.5),
radius .08, gravity, wall tangent damping .45, floor-stop threshold .6, fuse 1.2s
and total life 11.2s retain their values. A genuinely overlapping launch is now
rejected before consumption; direct controlled launches retain a conservative
freeze as a final safety. Decorative projectile collision is disabled immediately.
This replaces variable-render-frame movement integration with bounded accumulated
steps, without changing engine timing or enemy hearing/recognition rules.

Pause/menu, hiding, result, restoration, disable, cooldown and empty inventory hide
the preview and clear its held intent. A held button from an inactive state cannot
resume aiming until released and pressed again. Throwing also cancels the current
preview. Owned renderers/material are reused and removed with the old player.
The six-line title/pause controls explain optional aiming; the ordinary HUD keeps
its prior compact controls when the button is not held.

Four authored PlayMode fixtures retain all 44 previous cases: real RMB/Q
input and interruption lifecycle; floor/wall/ceiling first-contact parity under
controlled 10/120Hz capture clocks; real aimed distraction with chase immunity and
finite inventory; and two actual authored-camera preview frames. Positioned and
synthetic physics fixtures are labeled separately from the unmodified full survival
route. Actual Unity execution is NOT RUN at this source-preflight stage.

Evidence transport allows 39 files for the previous 36 plus two PNGs and one small
JSON, preserving the 16 MB aggregate and 1.5 MB per-file bounds. Latest actually
verified player remains build #14 at c6b0c1a3, with all 44 tests passed. The new source
must pass independent review, genuine API/source checks, exact-head CI, and one
pinned engine run before a new player is represented as verified.

Independent final source/test review found no unresolved must-fix. Final preflight
passes 110 C# syntax/type inventories (883 GUIDs), all 80 Python cases without
skips, 143 existing pure C# assertions, and seven genuine Unity 6000.6/matching
package API compiler configurations. Existing compiler warnings remain; no engine
execution is implied. Exact source fingerprints and reports are retained here.
All previous 44 test cases and protected scene bytes are unchanged.
