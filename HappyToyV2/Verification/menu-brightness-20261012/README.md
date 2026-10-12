# Title display brightness and truthful preview — 2026-10-12

The user's actual player screenshot was substantially darker than the supplied
offscreen title PNG. Sampled dark UI/background channels followed an extra sRGB
encoding in the former offscreen render target. That image was layout evidence,
not a reliable representation of the normally presented title brightness.

The title shader now encodes its scene colour once into a UNorm render texture
for UI presentation, so the weak face light and body contour survive the real
player output. This preserves the twelve planted supports, original-surface red
eye emission, existing controls and gameplay scene. The UI audit render target
uses UNorm and explicitly linear readback to avoid the old extra encoding.

Target-texture UI alpha blending still differs slightly from backbuffer blending;
it is measured separately as rawOffscreenMeanError. Therefore the supplied
`title-normal-1600x900.png` is captured from the real player backbuffer, rather
than an offscreen panel. The audit compares real frames before/after restoring
normal display output; displayPreviewMeanError must remain below .003. A blank
hidden-window backbuffer is rejected, never treated as a valid brightness pass.

Run the opt-in menu audit in a visible interactive Windows player window with
`-v2-menu-face-output <optional evidence directory> -quiet-diagnostics` and
1600x900 windowed resolution. It restores user preferences and uses an isolated
save directory. The normal player has no audit process or capture mode.

`menu-face-report.json` retains the actual-display comparison plus eleven UI
frame/layout, settings, pause/resume, grounding and glow checks. Other aspect
ratio fixtures are offscreen geometry evidence. `build-inputs.json` checks the
fresh player and current source hashes; `source-completeness.json` checks all
required editable sources after staging. Full gameplay or performance is not
claimed. Source changes and verification are stored locally; no push is made.
