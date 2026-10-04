# Player-reported presentation preflight

This is the historical source/external-API/model-export preflight. The subsequent
actual Unity build #10 at `705c0639` passed all 33 tests and exported the Windows
player; see `../cloud-tests/build-10-results.json`. Preflight is not a substitute
for those later original XML/camera results.

- 101 C# files, 874 GUIDs, parser and protected scene/meta/build-selection hashes pass
- Seven genuine Unity 6000.6 API compiler stages pass, zero errors; existing warnings remain
- 80 Python regressions pass with no skips, including 32-file/16 MB evidence transport bounds
- 143 assertions against the existing pure C# attack/ripple/stealth/restart helpers pass
- Shared cabinet FBX reimport into Blender preserves 31 mesh/object names and origins;
  six aperture rays are clear while four neighboring panel rays remain blocked
- Independent review fixed an actual recognition-ray mismatch above low cover and
  made the intro audit stop at handoff rather than test unattended-player survival
- Compiler source hashes match the submitted source

Current suite: 6 EditMode + 27 PlayMode = 33 definitions, keeping all 24 prior tests.
New captures: six cabinet world-camera views, four physical corner reveal frames,
five room views with the positioned player's real floor atmosphere, two UI-only
recognition effect frames. Combined with existing evidence this is 32 envelopes.

The cabinet model is intentionally changed, with original scene bytes retained.
Music-room geometry is a scoped runtime repair; two seats/stands add six physical
colliders and four navigation carvers. Actual import bindings, appearance, navigation,
full survival, mixing and cleanup were subsequently checked in build #10. These checks do
not replace the user's subjective play feedback.
