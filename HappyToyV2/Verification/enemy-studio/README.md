# CreatureStudio game verification

The current Cyclopse is the user-requested round furry, one-eye human-face model,
generated and saved in the designated modeling webapp (job f94f8013). Uncat is also
installed on the school angry portrait actor; Baby uses its own crawl/chase/cry.
Original GLBs/PBR, references, editable Blender sources and reproduction tools are
under `SourceArt/CreatureStudio`. Baeksugwi and the authored school scene are preserved.

`summary.json` links the current evidence. PlayMode verifies real corridor and
school actors, model/clip/PBR contracts, F1/F2 behavior and checkpoints (5 passing
cases), plus Cyclopse occluded entry, deferred staging and cancellation (3 passing
cases). The native screenshots use the real corridor and player eye camera, with
isolated subjects turned toward the camera for facial review. Native checks cover
actual imported geometry, own clips, eight deformation/ground phases and Baby cry;
AI is disabled in this rendering fixture. They do not certify a complete survival
playthrough, perceived fear or performance on all hardware.

The Windows release player was built with Unity 6000.6.0f1. `build-inputs.json`
verifies current Assets/Packages/ProjectSettings hashes against the build manifest,
artifacts and credits. Native automated verification uses the required silent
process guard, with successful mute/readback and restoration recorded in
`native/diagnostic-audio-silence.json`. Normal user gameplay retains its sound.

`cyclopse-webapp-completed.png` shows completed/saved generation. `native/` contains
actual Cyclopse, Uncat, Baby and Baby cry views; no screenshot is a reference-image
substitute for a runtime actor. Import reports record 22 bones and actual game mesh
budgets for each creature. See the source README for clone/pull and pinned tools.
