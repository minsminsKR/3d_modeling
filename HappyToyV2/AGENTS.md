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

# Monster identity and naming — explicit user correction, 2026-10-12

- 운캣 (Uncat) is the four-legged cat monster appearing in Chapter 1.
- 화캣 (Hwacat / Hwacat_angry) is a separate school-chapter-only monster:
  it dances, transforms into a devil-like form, and then chases the player.
- Never call Uncat 화캣 or describe the two as the same monster. Show names
  and model screenshots separately. Shared models or a runtime alias in the
  current code are an implementation issue, not evidence of identical identity.
- EnemyVisualRefinement must preserve the Hwacat_angry key and use its own
  demonic model and original locomotion clips; never alias it to Uncat.
  The incorrect Hwacat_angry -> Uncat binding was removed on 2026-10-12.
  The school normal doll retains its authored stand/dance clips before transforming.
