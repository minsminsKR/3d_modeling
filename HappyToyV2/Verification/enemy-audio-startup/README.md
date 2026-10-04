# Empty filtered-source startup correction preflight

Subsequent actual result: UBA #12 at `f0ed7649` passed all 37 tests and Windows
export, with zero targeted warnings. Original XML, final log and PCM/ZIP hashes
are in `../cloud-tests/build-12-results.json` and `build-12-experience.json`.
The source-preflight report below is retained separately from runtime evidence.

UBA #11 at a7c68b1 passed all 37 Unity tests and exported Windows. Its new enemy
PCM passed, but 40 startup warnings identified AddComponent<AudioSource> on roots
that already had built-in filters (38 Lantern, 2 Cyclopse). Original ZIP/XML/WAV and
all 34 envelopes remain preserved. This patch changes only initialization/cleanup:

- Lantern creates both configured root sources before adding the first filter
- Cyclopse configures an inactive, co-located owned child, then activates it
  before the same roar playback. Completion, cancellation and blocked release
  immediately stop/deactivate and destroy that temporary source/filter/clip
- Common PlayMode setup now observes the exact warning before scene load and
  through reload/unload, failing after cleanup if any appeared. Nothing is muted,
  ignored or expected away in the test logging system
- Existing physical-intro case checks source settings, one filter, co-location and
  destroyed native objects after completion. No case is removed or skipped

Independent review found no remaining must-fix. 80 Python tests, 104 C# files / 877 GUIDs,
protected scene hashes, 143 existing pure C# assertions and 7 genuine Unity API external
compiler stages pass. Compiler warnings remain at baseline. Exact source hashes
are recorded in genuine-api-compile.json. Waveforms, gameplay parameters and the
17-segment strict PCM fixture are unchanged. Actual engine warning elimination and
Windows export were pending at preflight and then passed in build #12
(6 EditMode + 31 PlayMode).
