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
installs the locked dependencies, copies the bundled addon into Blender's user
addons directory, enables it, and saves addon preferences. A replaced addon is
backed up under `Builds/LocalMcp/backups`. Existing models and startup scenes are
not saved. It adds the named `unity` and `blender` servers to the user's Codex
configuration, retaining other server entries. Existing entries with those two
names are updated. Local paths and personal configuration are not source files.

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
