# Korean searching Baby utterance

The written line is **어디..? 어디있어??**. Two separate generated questions,
`어디?` and `어디 있어?`, place a hesitation between the words while keeping
their spoken content intact. Three local neural candidates are retained:
`seeking-soft` (speed .94, pause .48 s), `seeking-hesitant` (speed .88, pause .63 s)
and a neutral comparison (speed 1.0, pause .16 s). The neutral candidate was
rejected because its short gap leaves little searching hesitation. The two
selected candidates alternate so the actor does not repeat one identical take.

MeloTTS-Korean is a publicly downloadable **MIT** licensed model and library by
MyShell.ai. Its pinned model card and license are included here. The supported
Korean path uses Kiyoung Kim's LMkor Korean BERT context encoder (Apache 2.0),
also pinned and credited. The input text was supplied for this project; no
speaker recording, user's microphone, voice service/account or real-person
voice clone was used. These are **generated character voices**, not recordings
of a real baby or a child performer. The existing genuine infant cry remains
independently credited to its CC0 recording creator.

Windows' installed Heami desktop voice was considered but not selected: its
desktop voice redistribution terms were not used as a substitute for the
explicitly licensed neural model. Merely playing an adult voice at a faster
sample rate would also shorten Korean consonants and hesitation, so it was
rejected as an authoring route. Instead the selected neural speech is edited
with independent voiced pitch and tract formants, preserving the original
phrase durations. Prepared median voiced pitch is about 309/296 Hz, with
1.16/1.18 formant shifts and restrained .38 peak. The selected raw speech and
exact edits remain available in the canonical manifest for future tuning.

The two authored words use a reviewed pronunciation table: `어디 → 어디`,
`있어 → 이써`. Melo's normal Korean-to-jamo and language-encoder steps then run
unchanged. This avoids the unrelated general morphology backend's Windows
Unicode-path dictionary failure and auto-install side effect. The generator
rejects unreviewed words rather than guessing or silently falling back.

No candidate/result was played through a speaker during automated authoring.
Data and independent speech-recognition checks assess file integrity and the
line, not human listening quality or a guarantee that a person will perceive
the voice as a particular age. Native Unity import/mix/lifecycle checks run
only under the explicit diagnostic process silence guard.

Offline cue regeneration requires only the retained raw WAVs and
`SourceArt/Audio/requirements-baby-mutter.txt`, using
`SourceArt/Audio/render_baby_mutter.py`. Optional neural raw-speech regeneration
is fully described in the Audio README and `generation.json`; its fixed model
weights are checked by SHA-256, not redistributed inside the game or required
for normal clone/build/play. License notices are in `MeloTTS-MIT.txt` and
`LMkor-Apache-2.0.txt` and distributed credits.
