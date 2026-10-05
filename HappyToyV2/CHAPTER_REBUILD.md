# Five-memory school chapter

The current user direction replaces the default flat random corridor with the preserved school's three physical floors. This is uncommitted work above `9f97bb9e51899b4a4b08aa4100cff60fc4e05146`. Git pull on 2026-10-05 confirmed that upstream branch was already current; local work was preserved.

## Runtime progression

| Memory | Location | Result |
| --- | --- | --- |
| 1 | 1F, less than two metres from the original opening spawn | Original Cyclopse starts patrolling |
| 2 | 1F washroom | Original mannequin becomes active; only moves when unobserved and the flashlight is on |
| 3 | 2F, reached through the original northern stairwell | Original floating mask becomes active on 2F |
| 4 | 2F portrait room | First interaction starts the original frame-drop, stand-up, dance and angry transformation. Collection requires the completed event and at least half a second of real camera visibility of Hwacat |
| 5 | Flooded basement nursery, reached through the southern stairwell | Baby's original warning and crawling reveal must finish before the name tag can be collected |

No monster is active at chapter start. Memories are collected in order. Completing memory five leaves the threats active until the player returns to the original 1F exit. Camera and movement remain under player control during the portrait event. Pause stops reveal timers.

`MemoryChapter` configures existing actors and interactions without saving or regenerating the scene. `ChapterAtmosphere` changes runtime surface materials and adds abandoned ceiling boards, cable loops, damaged frames and graphite drawings, damp marks, wall tiles, original classroom furniture and basement hospital furniture. Physical supports and furniture have colliders and NavMesh carving. Existing stairs, water surfaces and school geometry remain intact. Local cold/warm light pools and room audio cover the three floors.

The original random corridor APIs remain available for the earlier corridor checkpoint. The title's main action starts the school chapter, and an older checkpoint is explicitly labelled as an earlier corridor. Chapter suspension and chapter result statistics are not implemented; the pause screen states that returning to title resets chapter progress. Earlier corridor records are labelled separately.

## Verification records

All logs and XML are under the task's `work/unity-verification` directory. A controlled scene placement probe is not a full survival run.

- `chapter-compile.log`: read-only editor validation and compilation passed. Original scene/build selection hashes unchanged.
- `chapter-stages-01.xml`: 1/2 passed. The portrait probe's camera was overwritten by the enabled PlayerMotor; the controlled render/probe now explicitly disables only that motor so its intended camera orientation persists.
- `chapter-stages-input-02.xml`: 3/4 passed. Five-memory three-floor navigation/render tour, actual staged events, and gamepad menus passed. The real-input route died after the fourth memory, with the floating mask's live slow curse and angry Hwacat both present. No full-survival claim from this run.
- `chapter-input-art-03.xml`: 3/3 passed. Added physical furniture, actual input survival (76.83 seconds, 226.44 metres, four physical stair legs) and original mannequin intro passed. Threats retained their original speeds and activation; no player warps or enemy isolation in the full route.
- `chapter-policy-flow-04.log`: compile failure in the newly added test due to a missing UIElements using directive. Fixed before rerunning.
- `chapter-policy-flow-05.xml`: 4/5 passed. Chapter mannequin gaze/light movement rule, stage/reveal gates, gamepad menus and preservation of an earlier corridor checkpoint passed. Chapter menu restart exposed presentation Start ordering: the early chapter conversion changed the roster ID before the original dressing ran.
- `chapter-current-06.xml`: 3/3 passed after making presentation initialization explicitly idempotent before conversion and parenting the roster's visual dressing to its original interaction. Genuine input survival passed again (76.87 seconds, 226.48 metres); chapter start/death/restart/title menus and five-memory render/navigation tour also passed.
- `chapter-preview-07-native-build.log`: Windows development build and editor validation passed. All 1,828 build input SHA-256 values matched the saved source after compilation. `NativeChapterAudit` is opt-in through `-v2-chapter-output`; it does not run during ordinary play.
- `native-chapter-07/native-chapter.json`: actual Windows/Direct3D12/RTX 5060 Ti startup passed, five seconds of chapter gameplay, 253 physical movement updates, zero active stalkers, zero unsupported materials and zero errors. First-floor support, live NavMesh and both imported textures were present. This is startup evidence; full five-memory survival remains the real-input Editor play run above.

Existing corridor seed 73/211 survival, checkpoint and previous build results apply to that older mode only. They do not certify the school chapter. Render images are engine captures at controlled camera positions for art review, not hand-painted mockups or proof of first-time-player difficulty.

Preserved authored scene SHA-256: `0f2d25f211c76c7aa5299702ad15cf52d042f5a316ef32f5c4f43d69994aaede`.
Preserved build-settings SHA-256: `bba9b21dd739d2451b44faa463225a94be4ea90b6c6ff284b27a14b06de2ef21`.
