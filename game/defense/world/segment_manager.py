from __future__ import annotations
import random
from ursina import Entity, Vec3, color, destroy


class SegmentManager:
    def __init__(self):
        self.segment_length = 42.0
        self.segments: list[Entity] = []
        self.decorations: list[Entity] = []
        self.next_z = -20.0
        for _ in range(7):
            self.spawn_segment()

    def spawn_segment(self):
        index = len(self.segments)
        road_color = color.rgb32(12 + (index % 2) * 5, 16 + (index % 2) * 5, 24)
        road = Entity(
            model="cube",
            color=road_color,
            scale=(13.0, 0.1, self.segment_length),
            position=(0, -0.05, self.next_z + self.segment_length * 0.5),
        )
        self.segments.append(road)

        for side_x in (-6.4, 6.4):
            rail = Entity(
                model="cube",
                color=color.rgba32(30, 220, 255, 180),
                scale=(0.22, 0.55, self.segment_length),
                position=(side_x, 0.25, road.z),
            )
            self.decorations.append(rail)

        for lane_x in (-3.0, 0.0, 3.0):
            stripe = Entity(
                model="cube",
                color=color.rgba32(30, 90, 120, 120),
                scale=(0.04, 0.035, self.segment_length * 0.55),
                position=(lane_x, 0.02, road.z),
            )
            self.decorations.append(stripe)

        if index > 1:
            for _ in range(random.randint(1, 3)):
                x = random.choice([-5.4, 5.4, random.uniform(-4.5, 4.5)])
                z = self.next_z + random.uniform(8.0, self.segment_length - 8.0)
                deco = Entity(
                    model="cube",
                    color=random.choice([color.rgb32(50, 80, 120), color.rgb32(90, 60, 120), color.rgb32(80, 90, 70)]),
                    scale=(random.uniform(0.35, 0.8), random.uniform(0.5, 1.5), random.uniform(0.35, 1.2)),
                    position=(x, 0.25, z),
                )
                self.decorations.append(deco)

        self.next_z += self.segment_length

    def update(self, player_z: float):
        while self.next_z < player_z + self.segment_length * 6:
            self.spawn_segment()
        cutoff = player_z - self.segment_length * 2
        for road in list(self.segments):
            if road.z + self.segment_length * 0.5 < cutoff:
                destroy(road)
                self.segments.remove(road)
        for deco in list(self.decorations):
            if deco.z < cutoff:
                destroy(deco)
                self.decorations.remove(deco)

    def clear(self):
        for entity in self.segments + self.decorations:
            destroy(entity)
        self.segments.clear()
        self.decorations.clear()
