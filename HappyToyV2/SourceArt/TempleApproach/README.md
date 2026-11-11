# Alley and mountain approach

`References/user-alley.png` and `user-mountain.png` are the user's visual
references provided in this session; they are not licensed here for reuse in
other products. The current geometry is an early interpretation, not a match
or visual quality approval. The alley uses existing GraphicsPbr surfaces; the
mountain reuses the existing TempleForest / TemplePrologue LOD assets and
terrain textures. The existing temple checkpoint and authored interactions
remain separate from the new-run-only approach.

The path starts at (500, 0, 386), reaches mountain soil at z=414, and fades
to the original temple spawn after z=446. `AlleySnapperEncounter` owns the
pickup and the sound-triggered obstacle; `TempleApproachFlow` owns the route.
The creature asset and its provenance are recorded under `../AlleyBeast`.
If it has not yet been imported, the encounter is intentionally fail-open to
avoid an invisible hard blocker; that is incomplete content, not a finished
animal encounter. A player-facing save cannot be made until the temple is
reached. Existing temple checkpoints do not depend on this new route.

The imported static creature is now present. A real-input PlayMode route
passed from title through pickup, collision, sound impact, mountain ascent
and temple transition. D3D11 game-camera stills were reviewed; the animal's
FBX axis was corrected after two visibly incorrect attempts. The model has no
approved quadruped animation and slides away; the alley and mountain are far
less detailed than the references. The stills have no HUD and are not native
player, frame-rate or broad visual-quality certification.
