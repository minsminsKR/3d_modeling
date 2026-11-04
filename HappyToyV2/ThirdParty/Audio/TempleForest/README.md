# Temple forest footsteps

Forest wind: Noted451, [Wind Through Trees ambience.wav](https://freesound.org/people/Noted451/sounds/531016/),
CC0 1.0. `sources/wind-through-trees-hq.mp3` is Freesound's public HQ preview,
not the original WAV; its SHA-256 is
`2ee7c4bf9fc169a0727a6d7bde2d8b801987fc8b6f0cf27f6bb5040ac9ba38d5`.
`SourceArt/Audio/render_temple_forest_wind.py` decodes the preserved preview
and creates a 50-second mono loop from 8–60s with a two-second wrap crossfade.
The game uses this real outdoor recording as a diffuse background instead of
the indoor corridor wind. Source description, license and waveform were checked;
subjective listening and in-game audio balance are still unverified.

Selected: Sheyvan (Philipp Grzemba), [Steps Mud Gravel Forest Sneaker](https://freesound.org/people/Sheyvan/sounds/830876/), CC0 1.0. The preserved `sources/forest-sneaker-hq.mp3` is Freesound's public high-quality preview, not the original download. SHA-256: `c50bc4343fce1cfdd308ab7f41334f459f1dcd9b54a94ef046d1f243385a4236`.

Compared: straget, [A Walk with stop.wav](https://freesound.org/people/straget/sounds/414921/), CC0 1.0. Its preserved public HQ preview `sources/gravel-walk-hq.mp3` has SHA-256 `a69b4e4103a0afaed1a969ce82fabcf3e035892a68b54220e97cd2e4b8e0865f`. It is a longer gravel-road walk; the selected recording specifically describes forest ground and offers separated shoe contacts. This choice is based on source descriptions and measured waveforms; no subjective listening test has been performed.

`SourceArt/Audio/render_temple_forest_steps.py` decodes the selected source to mono 48 kHz and extracts four contacts around 14.28, 16.75, 18.33 and 20.63 seconds, with level adjustment and short edge fades. It requires Python 3.10+ and ffmpeg on PATH; development used ffmpeg `N-55702-g920046a`. The Unity WAVs, exact hashes and editing details are in `ThirdParty/Audio/manifest.json`. The original preview is not replaced. Test output remains muted; listening and in-game mix approval are still required.

The four contacts repeat in a fixed sequence. Their original peak-normalized
50 ms RMS maxima were approximately .0716, .0695, .0971 and .1240, making
the last contact disproportionately strong by this measurement. The
generator now attenuates only clips above .085 maximum 50 ms RMS; it does not
boost quieter contacts. The resulting maxima are .0716, .0695, .0850 and
.0850. This is a waveform consistency check, not a listening or mix verdict.
