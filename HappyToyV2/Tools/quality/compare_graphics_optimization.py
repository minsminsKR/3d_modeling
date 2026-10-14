"""Compare native static-pose render evidence, without claiming displayed FPS."""
import argparse
import json
from pathlib import Path


def compare(before, after):
    for report in (before, after):
        if report.get("status") != "PASS" or report.get("errors"):
            raise ValueError("Both native captures must pass")
    for key in ("unity", "device", "api", "measurement"):
        if before[key] != after[key]:
            raise ValueError("Mismatched comparison: " + key)
    old = {sample["pose"]: sample for sample in before["samples"]}
    new = {sample["pose"]: sample for sample in after["samples"]}
    if set(old) != set(new) or len(old) != 2:
        raise ValueError("Require the same two poses in each school/corridor process")
    changes = []
    for pose, sample in new.items():
        original = old[pose]
        for key in ("frames", "msaa", "eye", "look", "reflectionsExcluded"):
            if sample[key] != original[key]:
                raise ValueError("Mismatched pose: " + pose + "/" + key)
        if sample["frames"] != 128 or not sample["reflectionsExcluded"]:
            raise ValueError("Require the documented controlled reflection-excluded fixture")
        if original["ssaoSource"] != "DepthNormals" or sample["ssaoSource"] != "Depth":
            raise ValueError("Require the actual baseline and optimized SSAO settings")
        changes.append(dict(pose=pose, beforeMs=original["medianMs"], afterMs=sample["medianMs"],
                            medianReductionPercent=100 * (1 - sample["medianMs"] / original["medianMs"]),
                            beforeP95Ms=original["p95Ms"], afterP95Ms=sample["p95Ms"]))
    return dict(status="PASS", scope=before["measurement"], device=before["device"], changes=changes)


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("before", type=Path)
    parser.add_argument("after", type=Path)
    parser.add_argument("output", type=Path)
    args = parser.parse_args()
    result = compare(json.loads(args.before.read_text()), json.loads(args.after.read_text()))
    args.output.write_text(json.dumps(result, indent=2) + "\n")
    print(json.dumps(result, indent=2))
