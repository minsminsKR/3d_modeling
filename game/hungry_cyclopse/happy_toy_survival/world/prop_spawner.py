from __future__ import annotations

import random

from ursina import Entity, Vec3, color, destroy

from core.asset_loader import safe_entity
from core.config import ASSETS


class PropSpawner:
    def __init__(self):
        self.props_by_chunk: dict[tuple[int, int], list[Entity]] = {}

    def spawn_for_chunk(self, chunk_key: tuple[int, int], center: Vec3, chunk_size: int, noise):
        if chunk_key in self.props_by_chunk:
            return
        rng = random.Random(hash(chunk_key) + 31)
        count = rng.randint(6, 12)
        spawned: list[Entity] = []
        prop_files = ASSETS.props
        for _ in range(count):
            x = center.x + rng.uniform(-chunk_size * 0.45, chunk_size * 0.45)
            z = center.z + rng.uniform(-chunk_size * 0.45, chunk_size * 0.45)
            y = noise.sample(x, z) * 0.35
            scale = rng.uniform(0.7, 2.5)
            rotation_y = rng.uniform(0, 360)
            if prop_files:
                model = str(rng.choice(prop_files))
                prop = safe_entity(model=model, fallback_model="cube", position=(x, y, z), scale=scale, rotation_y=rotation_y)
            else:
                prop = Entity(model="cube", position=(x, y + scale * 0.35, z), scale=(scale, scale * 1.6, scale), color=color.rgb(70, 62, 66), rotation_y=rotation_y)
            prop.collider = "box" if scale > 2.1 else None
            prop.enabled = True
            spawned.append(prop)
        self.props_by_chunk[chunk_key] = spawned

    def remove_chunk(self, chunk_key: tuple[int, int]):
        for prop in self.props_by_chunk.pop(chunk_key, []):
            destroy(prop)
