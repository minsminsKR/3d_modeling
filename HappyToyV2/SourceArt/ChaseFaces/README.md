# Uncat and Baby chase faces

User direction, 2026-10-14: apply terrifying, repulsive faces to Uncat and Baby
during pursuit and the cabinet detection rush, following the approved Cyclopse
behavior. Uncat is the Chapter 1 cat, distinct from the school Hwacat doll.

Each directory preserves the exact orthographic edit input, original built-in
ImageGen output, full prompt/tool provenance, projection parameters and SHA-256
hashes. These are new generated facial textures on the existing webapp-generated
models, not regenerated base models. No external image or new third-party asset
was added. The existing CreatureStudio sources, rigs, Blender files, material
atlases, clips and licenses remain the editable base-model sources.

Uncat receives a leathery human-cat predatory grin, wrinkled stare, stained teeth
and slick tongue. Baby receives a cracked porcelain smile, needlelike ceramic
teeth, dark tear tracks and pinpoint pupils. Teeth and smile detail are painted
on the original facial surfaces; they are not independent new tooth geometry.

## Reproduce and edit

Use pinned Unity 6000.6.0f1 and the repository project. Explicit editor command
`HappyToy.V2.Editor.CreatureFaceAuthoring.RenderTargets` renders both original
patrol-zero skins and writes `generation-edit-target.png` plus `projection.json`.
It is a deliberate authoring step: preserve the old input/provenance before
changing it. Submit the target to built-in ImageGen with the recorded prompt,
save its original PNG here and update hashes/provenance if selecting a new image.

`HappyToy.V2.Editor.CreatureFaceImport.Apply` bakes
`Assets/Resources/ChaseFaces/{Uncat,Baby}/Expression.asset`, PNG and shader variant
anchor material. The importer hashes its recipe, original texture, model,
projection and pose to detect stale resources. BuildWindows calls Ensure.
`CreatureFaceAuthoring.RenderApplied` produces disposable aligned mesh previews
under `Builds/ChaseFace-ArtReview`, without replacing generation inputs.

The profile stores a fixed camera basis from the two authored eye landmarks.
Secondary UVs and soft frontal masks follow the original skinned vertices.
Uncat's long muzzle needs deeper mouth coverage than Baby. Original UV0,
normals, skeleton, animations and body support geometry remain intact.
Uncat also has an original open-mouth gap through which the torso is visible.
The importer bakes a small dark elliptical mouth interior in head-bone space;
it grows from its own centre with the expression and is hidden again at calm.
This local mesh finishing retains the original face, teeth and tongue surfaces.

`EnemyChaseFace` owns each actor's mesh/material copies and the state transition.
`CyclopseChaseFace` retains its existing component GUID/API as a compatibility
adapter. The shared serialized data type retains its original class identity.
Chase or attack blends the face in over 0.45 seconds; Search/Patrol blends back
over 0.7 seconds. Pause freezes it, disable restores it, defeat keeps the attack
face. Existing cabinet state transitions provide calm scan/retreat and renewed
aggressive detection; the cabinet decision rules are unchanged.

Follow-up, 2026-10-14: `MonsterRedEyes` now registers expression materials as
explicit alternate surfaces. Its original UV0 eye masks, emission toggle and
pulse continue during chase/rush; the lit face shader preserves that emission.
No new eyes or overlay geometry are added. Native portraits compare the same
pose with eye emission on/off and require visible red pixel differences around
every original eye, in addition to material/property checks.

Cabinet follow-up: three close, actually witnessed entries at the same cabinet
by the same monster now force a direct rush and committed capture. The entry
counter is per monster, never per frame or multiplied by several observers.
Quiet use cannot erase it; moving over 12 metres from that cabinet clears it.
History survives optional checkpoint fields, while old saves start empty.
The third rush retains physical path/arrival and fresh warning/impact checks;
exit spam is blocked while that committed rush or strike is active. Disabled
threats, blocked paths, recovery and safe mode cannot permanently lock the exit.

## Verify

Run `python Tools/quality/run_feature_tests.py --profile cabinet-expression`
and the `readability` profile. The focused checks include actual pursuit,
pause, original material restoration, shared mesh/UV preservation and detected
cabinet strikes for all three faces.

A fresh player accepts `-quiet-diagnostics -v2-cabinet-expression-output <new-dir>
-v2-expression-creature Uncat` (or `Baby`, default `Cyclopse`). Each opt-in run
uses isolated saves and quiet process audio, physically approaches/scans/leaves
a protected cabinet, then detects/rushes/strikes a vulnerable entry and captures
the actual lit chase, calm and rush faces. It never installs in normal play.
Add `-v2-cabinet-taunt` for the controlled three-entry sequence with safe RNG
on every entry: the third must still rush, reject exit/reroll spam and strike.
