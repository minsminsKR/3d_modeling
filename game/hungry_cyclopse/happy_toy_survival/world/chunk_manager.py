from __future__ import annotations

from ursina import Entity, Vec3, color, destroy

from core.config import ASSETS, CHUNK_RADIUS, CHUNK_SIZE, WORLD_SEED
from world.noise_generator import ValueNoise2D
from world.prop_spawner import PropSpawner


class ChunkManager:
    def __init__(self):
        self.noise = ValueNoise2D(WORLD_SEED)
        self.prop_spawner = PropSpawner()
        self.chunks: dict[tuple[int, int], Entity] = {}

    def _chunk_key(self, position: Vec3) -> tuple[int, int]:
        return (round(position.x / CHUNK_SIZE), round(position.z / CHUNK_SIZE))

    def _create_chunk(self, key: tuple[int, int]):
        cx, cz = key
        center = Vec3(cx * CHUNK_SIZE, 0, cz * CHUNK_SIZE)
        y = self.noise.sample(center.x, center.z) * 0.2
        floor_texture = ASSETS.textures.get("floor")
        chunk = Entity(
            model="plane",
            texture=str(floor_texture) if floor_texture else None,
            color=color.rgb(36, 34, 37),
            position=(center.x, y, center.z),
            scale=(CHUNK_SIZE, 1, CHUNK_SIZE),
            collider=None,
        )
        self.chunks[key] = chunk
        self.prop_spawner.spawn_for_chunk(key, center, CHUNK_SIZE, self.noise)

    def update(self, player_position: Vec3):
        current = self._chunk_key(player_position)
        wanted = set()
        for x in range(current[0] - CHUNK_RADIUS, current[0] + CHUNK_RADIUS + 1):
            for z in range(current[1] - CHUNK_RADIUS, current[1] + CHUNK_RADIUS + 1):
                key = (x, z)
                wanted.add(key)
                if key not in self.chunks:
                    self._create_chunk(key)
        for key in list(self.chunks.keys()):
            if key not in wanted:
                destroy(self.chunks.pop(key))
                self.prop_spawner.remove_chunk(key)
