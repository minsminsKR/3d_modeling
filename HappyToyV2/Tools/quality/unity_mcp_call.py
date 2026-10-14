"""One-shot local Unity MCP inspection/call; stdlib only, no client config changes."""
import argparse
import json
import queue
import shutil
import subprocess
import threading
from pathlib import Path


def tool_failed(result):
    if result.get("isError"):
        return True
    for item in result.get("content", []):
        if item.get("type") != "text":
            continue
        try:
            payload = json.loads(item["text"])
        except (ValueError, KeyError):
            continue
        if isinstance(payload, dict) and payload.get("success") is False:
            return True
    return False


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--project", type=Path, default=Path(__file__).resolve().parents[2])
    parser.add_argument("--tool")
    parser.add_argument("--arguments-file", type=Path)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    executable = shutil.which("unity")
    if not executable:
        raise RuntimeError("Install the documented Unity CLI first")
    process = subprocess.Popen([executable, "mcp", "--project-path", str(args.project.resolve())],
                               stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=subprocess.PIPE,
                               text=True, encoding="utf-8")
    incoming = queue.Queue()
    def read():
        for line in process.stdout:
            try:
                incoming.put(json.loads(line))
            except json.JSONDecodeError:
                pass
    threading.Thread(target=read, daemon=True).start()
    def send(method, params, number=None):
        message = dict(jsonrpc="2.0", method=method, params=params)
        if number is not None:
            message["id"] = number
        process.stdin.write(json.dumps(message) + "\n")
        process.stdin.flush()
    def response(number):
        while True:
            message = incoming.get(timeout=60)
            if message.get("id") == number:
                if "error" in message:
                    raise RuntimeError(message["error"])
                return message["result"]
    try:
        send("initialize", dict(protocolVersion="2024-11-05", capabilities={},
                                clientInfo=dict(name="HappyToyV2-quality", version="1")), 1)
        initialized = response(1)
        send("notifications/initialized", {})
        if args.tool:
            arguments = json.loads(args.arguments_file.read_text(encoding="utf-8-sig")) if args.arguments_file else {}
            send("tools/call", dict(name=args.tool, arguments=arguments), 2)
        else:
            send("tools/list", {}, 2)
        result = dict(server=initialized, result=response(2))
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding="utf-8")
        if args.tool:
            print(json.dumps(result, ensure_ascii=True, indent=2))
            if tool_failed(result["result"]):
                raise RuntimeError("Unity MCP tool reported an error; inspect the saved response")
        else:
            print(json.dumps(dict(server=initialized, tools=[tool["name"] for tool in result["result"]["tools"]]), ensure_ascii=True, indent=2))
    finally:
        process.stdin.close()
        try:
            process.wait(timeout=5)
        except subprocess.TimeoutExpired:
            process.terminate()
            process.wait(timeout=5)


if __name__ == "__main__":
    main()
