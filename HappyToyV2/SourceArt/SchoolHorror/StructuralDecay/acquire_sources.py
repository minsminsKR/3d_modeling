"""Acquire two primary Poly Haven CC0 scans, retaining originals and official checksums.

The saved source manifest pins every request; existing matching files need no network.
No credentials or secondary mirrors are used. Run from any working directory.
"""
from pathlib import Path
import concurrent.futures
import hashlib
import json
import urllib.request

HERE = Path(__file__).resolve().parent
HEADERS = {"User-Agent": "HappyToyV2/1.0 (source-preserving CC0 asset integration)"}
ASSETS = ("concrete_debris", "namaqualand_stones_01")


def get(url):
    with urllib.request.urlopen(urllib.request.Request(url, headers=HEADERS), timeout=120) as response:
        return response.read()


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    HERE.mkdir(parents=True, exist_ok=True)
    manifest_path = HERE / "source-manifest.json"
    if manifest_path.exists():
        manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    else:
        manifest = {"license": "CC0-1.0", "licenseUrl": "https://polyhaven.com/license", "assets": [], "files": []}
        for key in ASSETS:
            info = json.loads(get("https://api.polyhaven.com/info/" + key))
            files = json.loads(get("https://api.polyhaven.com/files/" + key))
            blend = files["blend"]["4k"]["blend"]
            manifest["assets"].append({"id": key, "source": "https://polyhaven.com/a/" + key,
                "authors": info["authors"], "type": info["type"], "dimensionsMm": info.get("dimensions"),
                "originalTriangles": info.get("polycount"), "apiFilesHash": info.get("files_hash"),
                "sourceKind": "Photographed concrete-debris heightfield PBR scan" if key == "concrete_debris" else "Photogrammetric stone-set mesh; used as small loose aggregate, not mislabelled as plaster"})
            requests = [(key + "_4k.blend", blend)] + list(blend.get("include", {}).items())
            # Both original editable material scenes retain their source image dependencies.
            # AO is an extra physical-map input not linked by the supplied Blender scene.
            ao = files["AO"]["2k"]["jpg"]
            requests.append(("textures/" + key + "_ao_2k.jpg", ao))
            for name, record in requests:
                manifest["files"].append({"path": "Originals/" + key + "/" + name,
                    "url": record["url"], "bytes": record["size"], "officialMd5": record["md5"]})

    def download(record):
        path = HERE / record["path"]
        path.parent.mkdir(parents=True, exist_ok=True)
        if not path.exists() or hashlib.md5(path.read_bytes()).hexdigest() != record["officialMd5"]:
            payload = get(record["url"])
            if len(payload) != record["bytes"] or hashlib.md5(payload).hexdigest() != record["officialMd5"]:
                raise ValueError("Primary source checksum mismatch: " + record["path"])
            path.write_bytes(payload)
        record["sha256"] = sha(path)
        print("SOURCE_OK", record["path"], path.stat().st_size, flush=True)

    with concurrent.futures.ThreadPoolExecutor(max_workers=4) as pool:
        list(pool.map(download, manifest["files"]))
    manifest["totalOriginalBytes"] = sum((HERE / item["path"]).stat().st_size for item in manifest["files"])
    manifest_path.write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8", newline="\n")
    print("ACQUIRED", manifest["totalOriginalBytes"], "bytes")


if __name__ == "__main__":
    main()
