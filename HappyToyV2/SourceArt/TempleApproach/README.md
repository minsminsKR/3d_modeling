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

The alley now has its own dusk sky, lighting and fog, restored to the existing
night environment at the mountain boundary. The old shop facade and freezer
are generated assets; their inputs, editable sources and limitations are in
`Storefront/README.md` and `Freezer/README.md`. Street cracks, drains, signage,
wires and roof detail supplement the existing collision geometry. The mountain
adds irregular paving, lanterns, foliage and a view of the existing temple.
The visual result still falls well short of the supplied photorealistic references.

The animal uses a retargeted, morph-baked running candidate and turns toward its
retreat direction after the actual snapper impact. The original static model is
a fallback. Foot locking, deformation and visual quality remain incomplete;
see `../AlleyBeast/RunCandidate/README.md` for source, license and measurements.
A real-input PlayMode route passed from title through pickup, collision, sound
impact, mountain ascent and temple transition. D3D11 game-camera stills and
24 motion samples were reviewed; these omit the HUD and are not a native player,
frame-rate or broad visual-quality certification. Interactive modeling-service
inspection was unavailable and remains outstanding.
