# Audio direction

The user requires realistic horror audio. For new or revised sound effects,
use genuine recordings with an appropriate open license, or convincingly
realistic generated audio. Do not use generic sine/saw/noise synthesis as a
shortcut or fallback. Prefer physical foley and human performances with
documented origins. Editing, filtering, pitch changes and layering recordings
are allowed; describe those edits honestly.

Compare suitable candidates before choosing. Record creator, source URL,
license, original hash and edits in ThirdParty/Audio/manifest.json and keep
distributed AUDIO-CREDITS.txt accurate. Preserve attribution for CC BY assets.
Check imported resources, ownership, pause/mute behavior and the actual game
mix. Missing mandatory cues should fail validation, never silently synthesize
a replacement. A PCM audit does not certify human listening quality.

# Quiet automatic verification — explicit user instruction, 2026-10-10

Automatic sound tests and diagnostic players must produce no audible device
output unless the user explicitly asks to hear them. Use the diagnostic-only
process-session silence guard (`-quiet-diagnostics`, diagnostic `-v*-output`
flags, or Unity `-runTests`). Preserve native pre-device listener DSP for audio
proof only after current-process mute succeeds and is read back. Until then,
retain listener/settings volume zero. A failed guard must keep output zero and
report that positive audio was not verified; never run an audible fallback or
count zero output as a positive mixer pass. Restore process mute/user settings
at exit while the listener remains zero. Ordinary game launches must not be
automatically muted. Automatic command examples must include the quiet guard.
