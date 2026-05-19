from __future__ import annotations

import math
import random


class ValueNoise2D:
    """Tiny deterministic value noise so the prototype has no extra dependency."""

    def __init__(self, seed: int = 0):
        self.seed = seed

    def _hash(self, x: int, z: int) -> float:
        rng = random.Random((x * 73856093) ^ (z * 19349663) ^ self.seed)
        return rng.uniform(-1.0, 1.0)

    @staticmethod
    def _smooth(t: float) -> float:
        return t * t * (3.0 - 2.0 * t)

    def sample(self, x: float, z: float, frequency: float = 0.05) -> float:
        x *= frequency
        z *= frequency
        x0 = math.floor(x)
        z0 = math.floor(z)
        xf = x - x0
        zf = z - z0
        sx = self._smooth(xf)
        sz = self._smooth(zf)
        a = self._hash(x0, z0)
        b = self._hash(x0 + 1, z0)
        c = self._hash(x0, z0 + 1)
        d = self._hash(x0 + 1, z0 + 1)
        top = a + (b - a) * sx
        bottom = c + (d - c) * sx
        return top + (bottom - top) * sz
