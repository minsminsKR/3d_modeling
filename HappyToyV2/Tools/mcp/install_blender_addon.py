"""Enable the pinned MCP addon in Blender; does not save or edit any model."""
import argparse
import hashlib
import json
import os
import shutil
import sys
from pathlib import Path

import addon_utils
import bpy


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--addon-file", type=Path, required=True)
    parser.add_argument("--status-file", type=Path, required=True)
    args = parser.parse_args(sys.argv[sys.argv.index("--") + 1:])
    source = args.addon_file.resolve(strict=True)
    destination = Path(bpy.utils.user_resource("SCRIPTS", path="addons", create=True)) / "blender_mcp.py"
    if destination.exists() and destination.read_bytes() != source.read_bytes():
        backup = args.status_file.parent / "backups" / "blender_mcp.py"
        backup.parent.mkdir(parents=True, exist_ok=True)
        if backup.exists():
            raise RuntimeError(f"Keep the existing backup before updating again: {backup}")
        shutil.copy2(destination, backup)
    shutil.copy2(source, destination)
    os.environ["DISABLE_TELEMETRY"] = "true"
    bpy.utils.refresh_script_paths()
    addon_utils.modules_refresh()
    addon_utils.enable("blender_mcp", default_set=True, persistent=True)
    if "blender_mcp" not in bpy.context.preferences.addons:
        raise RuntimeError("Blender MCP addon did not enable")
    bpy.context.preferences.addons["blender_mcp"].preferences.telemetry_consent = False
    bpy.ops.wm.save_userpref()
    args.status_file.parent.mkdir(parents=True, exist_ok=True)
    args.status_file.write_text(json.dumps({
        "blender_version": bpy.app.version_string,
        "addon_path": str(destination),
        "addon_sha256": hashlib.sha256(destination.read_bytes()).hexdigest(),
        "enabled": True,
        "telemetry_consent": False,
        "auto_start_on_gui_launch": bpy.context.scene.blendermcp_auto_start_server,
    }, indent=2), encoding="utf-8")


if __name__ == "__main__":
    main()
