# Pursuit iteration preflight

2026-10-04. Source checks only; the five new Unity fixtures and production pursuit changes have not yet been executed in Unity. Last completed engine evidence remains build #6 at e3122fb (15/16 passed; audio failed; no export).

- Python: 77 tests passed, no skips
- Pure C#: 143 assertions passed against actual shared helpers (29 attack, 20 ripple, 68 stealth, 26 restart); zero build warnings/errors
- Static parser: 95 C# files, 868 GUIDs, exact protected scene/meta/build-selection bytes preserved
- Genuine Unity 6000.6 API compilation: seven production/test stages passed, zero errors. Existing production warnings remain; test PlayMode has four deprecated diagnostic-API warnings, not a clean-warning result
- Independent read-only review corrected an unreachable-search infinite loop and a false visit counter before this final compile. The actual source hashes in the compiler report match the source submitted with this preflight

The current UTF suite defines 6 EditMode + 15 PlayMode tests. New controlled fixtures isolate/place actors and establish explicit stale evidence where named; they are not represented as an ordinary playthrough. The separate seven-record route still requires real controls, all encounter owners and original enemy speeds. Audio callback output is separately labeled diagnostics, never a substitute for the strict existing AudioRenderer gate.
