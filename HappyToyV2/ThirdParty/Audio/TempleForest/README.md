# Temple forest footsteps

Selected: Sheyvan (Philipp Grzemba), [Steps Mud Gravel Forest Sneaker](https://freesound.org/people/Sheyvan/sounds/830876/), CC0 1.0. The preserved `sources/forest-sneaker-hq.mp3` is Freesound's public high-quality preview, not the original download. SHA-256: `c50bc4343fce1cfdd308ab7f41334f459f1dcd9b54a94ef046d1f243385a4236`.

Compared: straget, [A Walk with stop.wav](https://freesound.org/people/straget/sounds/414921/), CC0 1.0. Its preserved public HQ preview `sources/gravel-walk-hq.mp3` has SHA-256 `a69b4e4103a0afaed1a969ce82fabcf3e035892a68b54220e97cd2e4b8e0865f`. It is a longer gravel-road walk; the selected recording specifically describes forest ground and offers separated shoe contacts. This choice is based on source descriptions and measured waveforms; no subjective listening test has been performed.

`SourceArt/Audio/render_temple_forest_steps.py` decodes the selected source to mono 48 kHz and extracts four contacts around 14.28, 16.75, 18.33 and 20.63 seconds, with level adjustment and short edge fades. It requires Python 3.10+ and ffmpeg on PATH; development used ffmpeg `N-55702-g920046a`. The Unity WAVs, exact hashes and editing details are in `ThirdParty/Audio/manifest.json`. The original preview is not replaced. Test output remains muted; listening and in-game mix approval are still required.
