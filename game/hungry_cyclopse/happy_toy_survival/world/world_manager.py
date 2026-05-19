from __future__ import annotations

from ursina import AmbientLight, DirectionalLight, Entity, Vec3, color, scene

from world.chunk_manager import ChunkManager


class WorldManager:
    def __init__(self):
        scene.fog_density = 0.055
        scene.fog_color = color.rgb(9, 8, 11)
        self.chunk_manager = ChunkManager()
        self.ambient = AmbientLight(color=color.rgba(22, 19, 26, 255))
        self.moon = DirectionalLight(rotation=(50, -20, 0), color=color.rgba(60, 52, 76, 180))
        self.backdrop_markers: list[Entity] = []
        self._create_corridor_silhouettes()

    def _create_corridor_silhouettes(self):
        for i in range(24):
            side = -1 if i % 2 == 0 else 1
            z = (i - 12) * 24
            self.backdrop_markers.append(
                Entity(
                    model="cube",
                    position=(side * 30, 5, z),
                    scale=(1.6, 10, 8),
                    color=color.rgba(20, 18, 22, 180),
                    collider=None,
                )
            )

    def update(self, player_position: Vec3):
        self.chunk_manager.update(player_position)
