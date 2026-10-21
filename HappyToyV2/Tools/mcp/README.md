# Local Unity and Blender MCP

Unity uses the official Unity CLI MCP server and the project's existing
`com.unity.pipeline` package. Blender uses the third-party MIT-licensed
[MCP for Blender](https://github.com/ahujasid/mcp-for-blender), pinned to 2.2.1.
These connections operate on the local computer. Keep the original webapp model
and finish a working copy; base-model generation still uses the designated
modeling webapp. Save editable `.blend`, exports, textures, and provenance in
the appropriate `SourceArt` directory.

## Reproduce on Windows

Install Python **3.12**, Blender **4.0.2**, Unity **6000.6.0f1**, the Unity CLI,
and Codex. Blender's official portable download and checksum are documented in
`SourceArt/GraphicsUpgrade/Architecture/README.md`. Add `unity` and `codex` to
PATH. Close Blender first, then run from the repository root with local paths:

```powershell
./HappyToyV2/Tools/mcp/Setup-LocalMcp.ps1 -Python python -Blender 'C:\path\to\blender.exe'
```

This creates an isolated, disposable environment in `Builds/LocalMcp/venv`,
installs the locked public dependencies from PyPI (ignoring unrelated machine indexes only during this process and restoring the environment), copies the bundled addon into Blender's user
addons directory, enables it, and saves addon preferences. A replaced addon is
backed up under `Builds/LocalMcp/backups`. Existing models and startup scenes are
not saved. It adds the named `unity` and `blender` servers to the user's Codex
configuration, retaining other server entries. Existing entries with those two
names are updated. Their startup/tool timeouts are bounded at 30/180 seconds
for geometry/import work, with all unrelated configuration retained. Local paths and personal configuration are not source files.

Open the project in Unity and launch Blender normally (GUI mode). The Blender
addon starts its localhost bridge on port 9876 automatically. A `.blend` file
can override its Auto-Start Server/Port scene properties; use the MCP for Blender
sidebar to reconnect if necessary. Both servers require their editor to be open
for scene operations. Restart MCP servers in Codex Settings after configuration;
an existing chat may need reopening to load newly registered tools.
Unity may show its first-launch Software Terms dialog; the user must review
and accept it before the Editor becomes ready.

Telemetry is disabled in the Blender MCP configuration, and addon telemetry
consent remains off. Do not enable external generation providers to replace the
project's modeling webapp workflow. Do not expose these local bridges publicly.

## Verify

`codex mcp list` verifies registration, not editor connectivity. Use an MCP
client to initialize each server, list tools, then call Unity `editor_status`
and Blender `get_scene_info`. The stdlib-only inspector (Python 3.11+) checks
the registered command and environment directly:

```powershell
python HappyToyV2/Tools/mcp/inspect_mcp.py --server blender --tool get_scene_info --output HappyToyV2/Builds/LocalMcp/blender-status.json
python HappyToyV2/Tools/mcp/inspect_mcp.py --server unity --tool editor_status --output HappyToyV2/Builds/LocalMcp/unity-status.json
```

For Unity, the existing stdlib client also works:

```powershell
python HappyToyV2/Tools/quality/unity_mcp_call.py --tool editor_status --output HappyToyV2/Builds/LocalMcp/unity-status.json
```

Check Unity `set_autotick` before requests that require its main thread. Use
`enable=true`, `interval_ms=100`, `persist=false` for a modest temporary tick.
Never enter Play mode, run tests, or modify game assets merely to check this
connection. Reports and dependency environments remain ignored under `Builds`.

Sources: [Unity MCP](https://docs.unity.com/en-us/unity-cli/replace-mcp-server-unity-cli),
[Codex MCP configuration](https://developers.openai.com/codex/mcp),
[Blender MCP setup and license](https://github.com/ahujasid/mcp-for-blender).

## Lady inspection while the desktop is busy

`lady_snapshot_blender.py` reads only the game LODs/rig/action from the editable
Lady source in a **disposable** Blender MCP session. It changes that session's
viewport areas to Console, appends model datablocks, and writes a JSON report
under `Builds/LadyMcpReview`; it never saves the `.blend`. Run it through
`execute_blender_code` using `exec(compile(...), {"__file__": str(script)})`.
Pass `LADY_REPORT` in that globals dictionary to select a fresh output file.
Do not run this against a Blender session holding the user's unsaved work.

`lady_snapshot_unity.csx` and `lady_pose_unity.csx` are method bodies for the
Unity MCP `eval` tool (`code` is the file text, `timeout` can be 25000). The
existing `Tools/quality/unity_mcp_call.py --tool eval --arguments-file ...`
accepts a JSON file with those arguments. They inspect and destroy temporary
prefab copies, with no `SaveAssets`, reimport, playback, or rendering. The pose
query checks sixteen CPU skin samples of LOD2, not every pose/LOD or actual FPS.

For a busy desktop, start the private Unity Editor with `-batchmode -nographics
-noaudio -v2-audit-no-desktop-interaction -job-worker-count 1`, and Blender with
`--no-window-focus -noaudio --threads 1 --debug-depsgraph-no-threads`. Request a
hidden window, Idle process priority and one logical CPU for both. Close only
those inspection processes when finished. These controls reduce contention;
they do not certify that another game's performance is unaffected. Defer GPU
renders, native gameplay tests and visual material judgments until requested.

## Static prop finishing and review

Use the designated webapp for original generation, then finish a copy through
Blender MCP `execute_blender_code`. Keep each recipe as a standalone script and
embed it in the packed authoring `.blend`. For the Buddhist lantern the recipe is
`SourceArt/CorridorEntrance/finish_entrance_assets.py` plus
`rebake_lantern_atlas.py`. The optional `keep_editor_session=True` argument clears
the **disposable** editor's scene/orphans while retaining its MCP bridge; never
use it in a session holding the user's unsaved work. Normal background exports
keep the existing factory-reset workflow.

Inspect each LOD's silhouette and UV-painted details with `look` (angles and
material shading), not triangle counts alone. The lantern's first aggressive
reduction damaged the ring/roof and fragmented painted ink; its final 20k/12k
budgets retain the silhouette and a selected-to-active original colour/ORM
rebake restores coherent UVs. The original 8K PBR and 948,015 triangles remain
unchanged; game maps are 2K. This is asset cost reduction, not an FPS claim.

`review_static_blender.py` reads live LOD topology, UV finiteness/range, bounds
and packed images. Run via `execute_blender_code` with `__file__` pointing to
the script, and optionally `MODEL_QUERY` / `MODEL_REPORT`. It never saves art.
`review_static_unity.csx` is an `eval` method body; set its Resources key to read
the actual imported meshes, UVs, PBR maps and LOD transitions without modifying
the prefab or scene. Follow this with the affected feature's actual camera checks.

The inspector accepts `--arguments-file <UTF-8 JSON>` and `--timeout <seconds>`
for code tools, avoiding shell quoting and permitting longer bounded geometry
work. It fails on reported execution errors as well as protocol errors. Image
responses stay in the local JSON report with a compact stdout summary. Copy
recipes and model results into SourceArt; leave diagnostic reports under Builds.
Both registered servers require their editors to be running. Existing Codex
chats may need MCP restart/reopening before new native tool names appear; the
stdio inspector can exercise the same registered connections immediately.
