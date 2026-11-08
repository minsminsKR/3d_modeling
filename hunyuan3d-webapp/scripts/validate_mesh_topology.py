"""Count final mesh edge incidence and directed orientation on CPU."""
import argparse
import json
from pathlib import Path

import numpy as np
import torch


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("mesh", type=Path)
    args = parser.parse_args()
    data = torch.load(args.mesh, map_location="cpu", weights_only=False)
    vertices = np.asarray(data["vertices"])
    faces = np.asarray(data["faces"])
    if faces.min() < 0 or faces.max() >= len(vertices):
        raise ValueError("Face index outside vertex range")
    if np.any(np.diff(np.sort(faces, axis=1), axis=1) == 0):
        raise ValueError("Degenerate face vertex IDs")
    directed = np.concatenate((faces[:, [0, 1]], faces[:, [1, 2]], faces[:, [2, 0]]))
    ordered = np.sort(directed, axis=1)
    _, inverse, counts = np.unique(ordered, axis=0, return_inverse=True, return_counts=True)
    sign = np.where(directed[:, 0] < directed[:, 1], 1, -1)
    orientation = np.bincount(inverse, weights=sign, minlength=len(counts))
    labels, frequencies = np.unique(counts, return_counts=True)
    result = {"vertices": len(vertices), "faces": len(faces),
              "edge_incidence": {str(int(x)): int(y) for x, y in zip(labels, frequencies)},
              "orientation_mismatches": int(np.count_nonzero(orientation)),
              "closed_oriented": bool(np.all(counts == 2) and not np.any(orientation))}
    print(json.dumps(result, indent=2))


if __name__ == "__main__":
    main()
