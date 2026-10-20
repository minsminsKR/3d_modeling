"""Read a named local MCP configuration and inspect its real server connection."""
import argparse
import json
import os
import queue
import subprocess
import threading
import tomllib
from pathlib import Path


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--server", choices=("unity", "blender"), required=True)
    parser.add_argument("--tool")
    parser.add_argument("--arguments", type=json.loads, default={})
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--config", type=Path, default=Path.home() / ".codex" / "config.toml")
    args = parser.parse_args()
    config = tomllib.loads(args.config.read_text(encoding="utf-8-sig"))["mcp_servers"][args.server]
    environment = dict(os.environ, **config.get("env", {}))
    args.output.parent.mkdir(parents=True, exist_ok=True)
    incoming = queue.Queue()
    with args.output.with_suffix(".stderr.log").open("w", encoding="utf-8") as stderr:
        process = subprocess.Popen(
            [config["command"], *config.get("args", [])],
            cwd=config.get("cwd"), env=environment,
            stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=stderr,
            text=True, encoding="utf-8",
            creationflags=subprocess.CREATE_NO_WINDOW if os.name == "nt" else 0,
        )

        def read():
            for line in process.stdout:
                try:
                    incoming.put(json.loads(line))
                except json.JSONDecodeError:
                    pass
            incoming.put(None)

        threading.Thread(target=read, daemon=True).start()

        def request(method, params, number=None):
            message = dict(jsonrpc="2.0", method=method, params=params)
            if number is not None:
                message["id"] = number
            process.stdin.write(json.dumps(message) + "\n")
            process.stdin.flush()
            if number is None:
                return None
            while True:
                message = incoming.get(timeout=60)
                if message is None:
                    raise RuntimeError(f"MCP exited; inspect {stderr.name}")
                if message.get("id") == number:
                    if "error" in message:
                        raise RuntimeError(message["error"])
                    return message["result"]

        try:
            initialized = request("initialize", dict(
                protocolVersion="2024-11-05", capabilities={},
                clientInfo=dict(name="HappyToyV2-MCP-inspector", version="1")), 1)
            request("notifications/initialized", {})
            tools = request("tools/list", {}, 2)
            report = dict(server=initialized, tools=tools["tools"])
            if args.tool:
                if args.tool not in {tool["name"] for tool in report["tools"]}:
                    raise RuntimeError(f"Server does not offer {args.tool}")
                report["result"] = request("tools/call", dict(name=args.tool, arguments=args.arguments), 3)
            args.output.write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
            print(json.dumps(dict(server=initialized["serverInfo"],
                                  tool_count=len(report["tools"]),
                                  result=report.get("result")), ensure_ascii=True, indent=2))
            result = report.get("result", {})
            if result.get("isError"):
                raise RuntimeError("MCP tool reported an error")
            for item in result.get("content", []):
                if item.get("type") == "text":
                    try:
                        payload = json.loads(item["text"])
                    except (ValueError, KeyError):
                        continue
                    if isinstance(payload, dict) and (payload.get("success") is False or payload.get("error")):
                        raise RuntimeError("Editor reported an error; inspect the saved response")
        finally:
            process.stdin.close()
            try:
                process.wait(timeout=5)
            except subprocess.TimeoutExpired:
                process.terminate()
                process.wait(timeout=5)


if __name__ == "__main__":
    main()
